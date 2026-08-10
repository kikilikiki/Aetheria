using System.Net.Sockets;
using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Discord;
using Aetheria.Server.Persistence;
using Aetheria.Server.World;
using Aetheria.Shared;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;
using Aetheria.Shared.Network;
using Aetheria.Shared.Network.Packets;
using Aetheria.Shared.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aetheria.Server.Networking;

/// <summary>
/// Gère une connexion TCP de jeu unique : lit les packets du client, les traite, répond.
/// Chaque session tourne sur son propre thread (I/O bloquante volontairement simple pour
/// cette première version — voir <c>Docs/README.md</c> pour la feuille de route réseau).
/// Enregistrée dans <see cref="WorldSessionRegistry"/> une fois entrée dans le monde, pour que
/// les autres joueurs la voient bouger en temps réel (voir GDD — visibilité globale).
/// </summary>
public sealed class PlayerSession(
    TcpClient client,
    SessionTokenStore tokenStore,
    IDbContextFactory<AetheriaDbContext> dbContextFactory,
    WorldSessionRegistry registry,
    DuelInviteService duelInvites,
    ILogger<PlayerSession> logger)
{
    private readonly object _writeLock = new();
    private NetworkStream? _stream;

    public Guid CharacterId { get; private set; }
    public string CharacterName { get; private set; } = string.Empty;
    public Guid UserId { get; private set; }
    public UserRank Rank { get; private set; } = UserRank.Joueur;

    /// <summary>Voir GDD/demande utilisateur — "le panel admin en jeu [est] pour les admins" : envoyé au client via EnterWorldAcceptedPacket pour donner accès au panel admin en jeu même sans le grade Fondateur.</summary>
    public bool IsAdmin { get; private set; }
    public int PositionX { get; private set; }
    public int PositionY { get; private set; }

    /// <summary>Voir GDD/demande utilisateur — "en dessous du pseudo affiche le niveau du joueur pour que en multijoueur on puisse voir le niveau des autres" : capturé à l'entrée dans le monde (voir HandleEnterWorld), pas re-synchronisé en direct si le joueur monte de niveau en cours de session (se rafraîchit à la prochaine reconnexion).</summary>
    public int Level { get; private set; } = 1;

    /// <summary>Voir GDD/demande utilisateur — "Titres/emblèmes affichés à côté du pseudo" : réutilise le titre PvP déjà existant (voir CharacterEntity.ActiveTitle/TitleCatalog/ProfileService), capturé à l'entrée dans le monde (voir HandleEnterWorld) comme <see cref="Level"/> ci-dessus.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Mis à jour immédiatement par la commande <c>/nick</c> (voir <see cref="HandleChatCommand"/>) — sans attendre une reconnexion.</summary>
    public void UpdateCharacterName(string newName) => CharacterName = newName;

    /// <summary>Voir GDD/demande utilisateur — "/reply" : nom du dernier joueur ayant chuchoté à CETTE session, renseigné côté destinataire quand un message privé arrive (voir HandleChatMessage, ChatChannel.Prive).</summary>
    private string? _lastWhisperFromCharacterName;
    public void RecordIncomingWhisper(string fromCharacterName) => _lastWhisperFromCharacterName = fromCharacterName;

    /// <summary>
    /// Voir GDD/demande utilisateur — "panel admin en jeu... kick" : ferme la connexion TCP, ce
    /// qui débloque <see cref="Run"/> (IOException/lecture nulle) et déclenche le nettoyage normal
    /// (désenregistrement, notification aux autres joueurs) dans son bloc <c>finally</c>.
    /// </summary>
    public void Kick()
    {
        try
        {
            client.Close();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private bool HasEnteredWorld => CharacterId != Guid.Empty;

    public void Run(CancellationToken ct)
    {
        using var _ = client;
        var remoteEndPoint = client.Client.RemoteEndPoint;
        _stream = client.GetStream();

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var packet = PacketFraming.ReadPacket(_stream);
                if (packet is null)
                {
                    break;
                }

                HandlePacket(packet);
            }
        }
        catch (IOException)
        {
            // Connexion interrompue par le client : rien de plus à faire.
        }
        finally
        {
            if (HasEnteredWorld)
            {
                registry.Unregister(CharacterId);
                registry.BroadcastExcept(CharacterId, new PlayerLeftPacket { CharacterId = CharacterId });
                SaveLastPosition();
            }

            logger.LogInformation("Session terminée pour {Endpoint} (personnage {CharacterId}).", remoteEndPoint, CharacterId);
        }
    }

    /// <summary>Envoi thread-safe : appelé aussi bien par le thread de cette session que par ceux d'autres sessions (diffusion via <see cref="WorldSessionRegistry"/>).</summary>
    public void SendPacket(IPacket packet)
    {
        if (_stream is null)
        {
            return;
        }

        lock (_writeLock)
        {
            try
            {
                PacketFraming.WritePacket(_stream, packet);
            }
            catch (IOException)
            {
                // La session en cours de fermeture ignore les diffusions tardives.
            }
        }
    }

    private void HandlePacket(IPacket packet)
    {
        switch (packet)
        {
            case PingPacket ping:
                SendPacket(new PongPacket { TimestampUtcTicks = ping.TimestampUtcTicks });
                break;

            case EnterWorldRequestPacket enterWorld:
                HandleEnterWorld(enterWorld);
                break;

            case PlayerMovePacket move:
                HandlePlayerMove(move);
                break;

            case ChatMessagePacket chat:
                HandleChatMessage(chat);
                break;

            case DuelResponsePacket duelResponse:
                HandleDuelResponse(duelResponse);
                break;

            default:
                logger.LogWarning("Packet {OpCode} reçu mais non géré par PlayerSession.", packet.OpCode);
                break;
        }
    }

    private void HandleEnterWorld(EnterWorldRequestPacket request)
    {
        if (!tokenStore.TryValidate(request.SessionToken, out var userId))
        {
            SendPacket(new EnterWorldRejectedPacket { Reason = "Session invalide ou expirée." });
            return;
        }

        using var db = dbContextFactory.CreateDbContext();
        var character = db.Characters.FirstOrDefault(c => c.Id == request.CharacterId && c.UserId == userId);

        if (character is null)
        {
            SendPacket(new EnterWorldRejectedPacket { Reason = "Personnage introuvable pour ce compte." });
            return;
        }

        CharacterId = character.Id;
        CharacterName = character.Name;
        Level = character.Level;
        Title = character.ActiveTitle ?? string.Empty;
        UserId = userId;
        var user = db.Users.FirstOrDefault(u => u.Id == userId);
        Rank = user?.Rank ?? UserRank.Joueur;
        IsAdmin = user?.IsAdmin ?? false;

        // Voir GDD/demande utilisateur — "restaurer la position du joueur en quittant/revenant" :
        // reprend la dernière position sauvegardée (voir Run, bloc finally). (0,0) n'est PAS la
        // capitale (c'est le coin en haut à gauche de la carte, voir WorldMap.cs) — un personnage
        // jamais sauvegardé (0,0) doit spawn au centre du village (voir WorldMap.SpawnPosition,
        // taille de carte 50 côté client → (25,27)) plutôt que dans ce coin.
        PositionX = character.LastPositionX == 0 && character.LastPositionY == 0 ? 25 : character.LastPositionX;
        PositionY = character.LastPositionX == 0 && character.LastPositionY == 0 ? 27 : character.LastPositionY;

        SendPacket(new EnterWorldAcceptedPacket
        {
            CharacterId = character.Id,
            PositionX = PositionX,
            PositionY = PositionY,
            Rank = Rank,
            IsAdmin = IsAdmin,
        });

        // Snapshot des joueurs déjà connectés (voir GDD — visibilité globale) : une série de
        // PlayerJoined plutôt qu'un packet "snapshot" séparé, pris AVANT de s'enregistrer
        // soi-même pour ne pas se recevoir soi-même dans la liste.
        foreach (var other in registry.All())
        {
            SendPacket(new PlayerJoinedPacket
            {
                CharacterId = other.CharacterId,
                Name = other.CharacterName,
                PositionX = other.PositionX,
                PositionY = other.PositionY,
                Rank = other.Rank,
                Level = other.Level,
                Title = other.Title,
            });
        }

        registry.Register(this);
        registry.BroadcastExcept(CharacterId, new PlayerJoinedPacket
        {
            CharacterId = CharacterId,
            Name = CharacterName,
            PositionX = PositionX,
            PositionY = PositionY,
            Rank = Rank,
            Level = Level,
            Title = Title,
        });

        logger.LogInformation("{CharacterName} est entré dans le monde.", character.Name);
    }

    private void HandlePlayerMove(PlayerMovePacket move)
    {
        if (!HasEnteredWorld)
        {
            return;
        }

        // Voir GDD/demande utilisateur — "en combat on peut encore traverser les mur" : validation
        // d'emprise au sol des bâtiments (voir TownLayout, partagé avec le Client) — la seule
        // collision qui existait nulle part jusqu'ici. Portée de déplacement encore non vérifiée
        // (voir GDD — Phase G pour un monde partagé complet) : un mouvement hors bâtiment/hors
        // carte est simplement ignoré (la position précédente reste affichée), pas de packet de
        // rejet dédié.
        if (!TownLayout.IsWalkable(move.TargetX, move.TargetY, TownLayout.DefaultSize))
        {
            return;
        }

        PositionX = move.TargetX;
        PositionY = move.TargetY;

        var update = new PlayerPositionUpdatePacket { CharacterId = CharacterId, PositionX = PositionX, PositionY = PositionY };
        SendPacket(update);
        registry.BroadcastExcept(CharacterId, update);
    }

    /// <summary>
    /// Voir GDD/demande utilisateur — "restaurer la position du joueur en quittant/revenant" :
    /// écrite une seule fois à la déconnexion (voir Run, bloc finally) plutôt qu'à chaque
    /// déplacement — la position en cours de session n'a besoin d'être exacte en base que pour la
    /// prochaine reconnexion, pas en temps réel, donc pas d'écriture DB à chaque pas.
    /// </summary>
    private void SaveLastPosition()
    {
        try
        {
            using var db = dbContextFactory.CreateDbContext();
            var character = db.Characters.FirstOrDefault(c => c.Id == CharacterId);
            if (character is null)
            {
                return;
            }

            character.LastPositionX = PositionX;
            character.LastPositionY = PositionY;
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Échec de la sauvegarde de la position pour {CharacterId}.", CharacterId);
        }
    }

    /// <summary>
    /// Tchat global (tout le monde) ou tchat de guilde (voir GDD/demande utilisateur). Le serveur
    /// ignore le nom/grade envoyés par le client (usurpation impossible) et renseigne les siens.
    /// Les messages commençant par "/" sont traités comme des commandes réservées aux
    /// modérateurs/administrateurs/fondateurs (voir <see cref="HandleChatCommand"/>) plutôt que
    /// diffusés. Le grade/mute sont relus depuis la base à chaque message (pas depuis le cache de
    /// <see cref="HandleEnterWorld"/>) pour qu'un changement s'applique immédiatement, sans
    /// attendre une reconnexion.
    /// </summary>
    private void HandleChatMessage(ChatMessagePacket chat)
    {
        if (!HasEnteredWorld)
        {
            return;
        }

        var trimmed = chat.Message.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        using var db = dbContextFactory.CreateDbContext();
        var self = db.Users.Where(u => u.Id == UserId).Select(u => new { u.IsAdmin, u.Rank, u.IsMuted, u.PremiumGradeTier }).FirstOrDefault();
        if (self is null)
        {
            return;
        }

        if (trimmed.StartsWith("/duel", StringComparison.OrdinalIgnoreCase))
        {
            // Voir GDD/demande utilisateur — "ajouter les demandes en duel pour le pvp" :
            // commande ouverte à tout le monde (contrairement à HandleChatCommand, réservée
            // aux modérateurs/admin/fondateur), donc traitée à part, avant ce filtrage.
            HandleDuelCommand(db, trimmed, chat.Channel);
            return;
        }

        // Voir GDD/demande utilisateur — grande liste de commandes de tchat. Celles ouvertes à
        // tout le monde (pas seulement modérateur/admin/fondateur) sont traitées ici, avant le
        // filtrage de HandleChatCommand — voir TryHandlePublicCommand.
        if (trimmed.StartsWith('/') && TryHandlePublicCommand(db, trimmed, chat.Channel, self.Rank))
        {
            return;
        }

        if (trimmed.StartsWith('/'))
        {
            HandleChatCommand(db, trimmed, chat.Channel, self.IsAdmin, self.Rank);
            return;
        }

        if (self.IsMuted)
        {
            SendPacket(new ChatMessagePacket { SenderName = "Système", Message = "Vous êtes muet(te) et ne pouvez pas parler dans le tchat.", Channel = chat.Channel });
            return;
        }

        var outgoing = new ChatMessagePacket
        {
            SenderName = CharacterName,
            Message = trimmed,
            Channel = chat.Channel,
            Rank = self.Rank,
            TargetCharacterName = chat.TargetCharacterName,
            SenderGradeTier = self.Rank == UserRank.Fondateur ? PremiumService.MaxTier : self.PremiumGradeTier,
        };

        if (chat.Channel == ChatChannel.Prive)
        {
            // Voir GDD/demande utilisateur — "discussion privée" avec un ami : envoyé uniquement
            // au destinataire et renvoyé à l'expéditeur (pour que son propre client affiche le
            // message envoyé), jamais diffusé au reste du monde comme Global/Guild.
            var target = registry.FindByCharacterName(chat.TargetCharacterName);
            if (target is null)
            {
                SendPacket(new ChatMessagePacket { SenderName = "Système", Message = $"{chat.TargetCharacterName} n'est pas connecté(e).", Channel = ChatChannel.Prive, TargetCharacterName = chat.TargetCharacterName });
                return;
            }

            target.SendPacket(outgoing);
            target.RecordIncomingWhisper(CharacterName);
            SendPacket(outgoing);
            return;
        }

        if (chat.Channel == ChatChannel.Guild)
        {
            var guildId = db.GuildMembers
                .Where(m => m.CharacterId == CharacterId)
                .Select(m => (Guid?)m.GuildId)
                .FirstOrDefault();

            if (guildId is null)
            {
                SendPacket(new ChatMessagePacket
                {
                    SenderName = "Système",
                    Message = "Vous n'êtes dans aucune guilde.",
                    Channel = ChatChannel.Guild,
                });
                return;
            }

            var guildMemberIds = db.GuildMembers
                .Where(m => m.GuildId == guildId)
                .Select(m => m.CharacterId)
                .ToHashSet();

            foreach (var session in registry.All().Where(s => guildMemberIds.Contains(s.CharacterId)))
            {
                session.SendPacket(outgoing);
            }
        }
        else
        {
            foreach (var session in registry.All())
            {
                session.SendPacket(outgoing);
            }
        }
    }

    /// <summary>
    /// Voir GDD/demande utilisateur — "ajouter les demandes en duel pour le pvp", puis "propose un
    /// pvp, si la personne est en team tout les membres doivent accepter" : ouvert à tout le monde
    /// (contrairement à <see cref="HandleChatCommand"/>). Si le personnage ciblé est en groupe,
    /// TOUS ses membres connectés reçoivent l'invitation et doivent l'accepter (voir
    /// <see cref="HandleDuelResponse"/>) — le groupe du défieur, lui, est engagé sans confirmation
    /// individuelle (le simple fait de lancer /duel vaut consentement de sa part).
    /// </summary>
    private void HandleDuelCommand(AetheriaDbContext db, string command, ChatChannel replyChannel)
    {
        void Reply(string message) => SendPacket(new ChatMessagePacket { SenderName = "Système", Message = message, Channel = replyChannel });

        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            Reply("Usage : /duel <pseudo>");
            return;
        }

        var target = registry.FindByCharacterName(parts[1]);
        if (target is null)
        {
            Reply($"{parts[1]} n'est pas connecté(e).");
            return;
        }

        if (target.CharacterId == CharacterId)
        {
            Reply("Impossible de vous défier vous-même.");
            return;
        }

        var challengerTeam = ResolvePartyCharacterIds(db, CharacterId);
        var targetTeam = ResolvePartyCharacterIds(db, target.CharacterId);

        if (challengerTeam.Contains(target.CharacterId))
        {
            Reply("Impossible de défier un membre de votre propre groupe.");
            return;
        }

        // Voir GDD/demande utilisateur — seuls les membres du groupe ciblé réellement connectés
        // doivent accepter (sinon un membre hors ligne bloquerait indéfiniment le duel).
        var onlineTargetTeam = targetTeam.Where(id => registry.FindByCharacterId(id) is not null).ToList();
        if (onlineTargetTeam.Count == 0)
        {
            Reply($"{parts[1]} n'est pas connecté(e).");
            return;
        }

        duelInvites.CreateInvite(CharacterId, CharacterName, challengerTeam, onlineTargetTeam);

        foreach (var memberId in onlineTargetTeam)
        {
            registry.FindByCharacterId(memberId)?.SendPacket(new DuelInvitePacket { FromCharacterName = CharacterName, TargetTeamSize = onlineTargetTeam.Count });
        }

        var teamSuffix = onlineTargetTeam.Count > 1 ? " et son groupe" : "";
        Reply($"Demande de duel envoyée à {target.CharacterName}{teamSuffix}.");
    }

    /// <summary>Résout le groupe (voir <see cref="PartyEntity"/>) du personnage, lui inclus — juste lui-même s'il n'est dans aucun groupe.</summary>
    private static IReadOnlyList<Guid> ResolvePartyCharacterIds(AetheriaDbContext db, Guid characterId)
    {
        var partyId = db.PartyMembers.Where(m => m.CharacterId == characterId).Select(m => (Guid?)m.PartyId).FirstOrDefault();
        return partyId is null
            ? [characterId]
            : db.PartyMembers.Where(m => m.PartyId == partyId).Select(m => m.CharacterId).ToList();
    }

    /// <summary>
    /// Réponse d'un des membres du groupe défié (voir <see cref="HandleDuelCommand"/>). Un refus
    /// annule tout pour tout le monde. Une fois TOUS les membres requis acceptés, le défieur reçoit
    /// <see cref="TeamDuelReadyPacket"/> : son client appelle alors <c>POST /api/pvp/team-challenge</c>
    /// (voir <c>CombatService.StartFriendlyTeamDuelAsync</c>), puis tous les autres participants
    /// reçoivent <see cref="DuelStartedPacket"/> une fois ce combat créé (voir Server/Program.cs).
    /// </summary>
    private void HandleDuelResponse(DuelResponsePacket response)
    {
        if (!duelInvites.TryGetPendingForTarget(CharacterId, out var invite))
        {
            return;
        }

        void NotifyAll(string message)
        {
            foreach (var characterId in invite.ChallengerTeamCharacterIds.Concat(invite.TargetTeamCharacterIds).Distinct())
            {
                registry.FindByCharacterId(characterId)?.SendPacket(new ChatMessagePacket { SenderName = "Système", Message = message, Channel = ChatChannel.Global });
            }
        }

        if (!response.Accept)
        {
            duelInvites.RemoveInvite(invite.InviteId);
            NotifyAll($"{CharacterName} a refusé le duel — combat annulé.");
            return;
        }

        invite.AcceptedCharacterIds.Add(CharacterId);
        if (invite.AcceptedCharacterIds.Count < invite.TargetTeamCharacterIds.Count)
        {
            NotifyAll($"{CharacterName} a accepté le duel ({invite.AcceptedCharacterIds.Count}/{invite.TargetTeamCharacterIds.Count}).");
            return;
        }

        duelInvites.RemoveInvite(invite.InviteId);
        registry.FindByCharacterId(invite.ChallengerCharacterId)?.SendPacket(new TeamDuelReadyPacket
        {
            ChallengerTeamCharacterIds = invite.ChallengerTeamCharacterIds,
            TargetTeamCharacterIds = invite.TargetTeamCharacterIds,
        });
    }

    /// <summary>
    /// Voir GDD/demande utilisateur — grande liste de commandes de tchat ("/help, /profile,
    /// /stats, /friend, /whisper, /guild, /party, /kingdom, etc."). Contrairement à
    /// <see cref="HandleChatCommand"/>, ouvertes à tout le monde. Retourne faux si la commande
    /// n'est pas reconnue ici (laisse alors <see cref="HandleChatCommand"/> tenter sa propre
    /// liste, réservée modérateur/admin/fondateur).
    /// </summary>
    private bool TryHandlePublicCommand(AetheriaDbContext db, string command, ChatChannel replyChannel, UserRank rank)
    {
        void Reply(string message) => SendPacket(new ChatMessagePacket { SenderName = "Système", Message = message, Channel = replyChannel });

        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        switch (parts[0].ToLowerInvariant())
        {
            case "/help":
                Reply("Commandes : /profile /stats /achievements /title /friend /whisper (/w) /reply (/r) /guild /party /kingdom /duel /use /report /ping /version /discord" +
                    (rank is UserRank.Moderateur or UserRank.Fondateur || IsAdmin ? " — modération : /ban /mute /unmute /nick /monster-lvl /setiv /give /givemoney /givexp /givemonster /givemount /setlevel /setmoney /setclass /setkingdom /clearinventory /deletemonster /resetlevel /invsee /unban /ipban /unbanip" : "") +
                    (rank == UserRank.Fondateur ? " — fondateur : /givegems /givepalier /globalboost /globalgive /dev" : ""));
                break;

            case "/menu":
                Reply("Utilisez les touches I/M/P/G/V/T/F/U/K/J en jeu, ou /help pour la liste des commandes.");
                break;

            case "/ping":
                Reply("Pong !");
                break;

            case "/version":
                Reply($"{GameInfo.Name} v{GameInfo.Version}");
                break;

            case "/settings":
                Reply("Les paramètres se règlent depuis le launcher (touche F9 en jeu pour la disposition clavier).");
                break;

            // Voir GDD/demande utilisateur — "système de link le compte discord avec le jeu pour
            // sur discord avoir les role des grade automatiquement" : génère un code à usage
            // unique (10 minutes) que le joueur saisit ensuite sur Discord via /link <code> (voir
            // Server/Discord/DiscordGatewayClient.cs), qui lie son compte et synchronise son rôle
            // Discord sur son grade actuel.
            case "/discord":
            {
                var user = db.Users.FirstOrDefault(u => u.Id == UserId);
                if (user is null)
                {
                    Reply("Compte introuvable.");
                    break;
                }

                var code = DiscordLinkService.GenerateLinkCode(user);
                db.SaveChanges();
                Reply($"Code de liaison Discord : {code} (valable 10 minutes). Sur Discord, tape /link {code} pour lier ton compte et recevoir ton rôle de grade.");
                break;
            }

            case "/profile":
            {
                var character = db.Characters.Include(c => c.User).FirstOrDefault(c => c.Id == CharacterId);
                if (character?.User is null)
                {
                    Reply("Profil introuvable.");
                    break;
                }

                var titleText = character.ActiveTitle is { Length: > 0 } t ? $" — {t}" : "";
                Reply($"{character.Name} (Nv.{character.Level}, {character.User.Rank}){titleText} : {(character.ProfileDescription.Length > 0 ? character.ProfileDescription : "(pas de description)")}");
                break;
            }

            case "/stats":
            {
                var character = db.Characters.FirstOrDefault(c => c.Id == CharacterId);
                var stats = db.Statistics.FirstOrDefault(s => s.CharacterId == CharacterId);
                if (character is null)
                {
                    Reply("Personnage introuvable.");
                    break;
                }

                Reply($"Niveau {character.Level} - {character.Gold} or - {stats?.Monsters.MonstersCaptured ?? 0} créature(s) capturée(s) - ELO PvP {stats?.Pvp.CurrentRank ?? 1000} - étage donjon max {stats?.Exploration.DeepestFloorReached ?? 0}");
                break;
            }

            case "/achievements":
            {
                var count = db.Achievements.Count(a => a.UserId == UserId);
                Reply($"{count} succès débloqué(s).");
                break;
            }

            case "/title":
                if (parts.Length < 2)
                {
                    Reply("Usage : /title <nom du titre déjà débloqué, ou 'aucun'>");
                    break;
                }

                SetActiveTitle(db, string.Join(' ', parts[1..]), Reply);
                break;

            case "/friend":
                if (parts.Length < 2)
                {
                    Reply("Usage : /friend add|remove|list [pseudo]");
                    break;
                }

                HandleFriendCommand(db, parts[1..], Reply);
                break;

            case "/whisper":
            case "/w":
                if (parts.Length < 3)
                {
                    Reply("Usage : /whisper <pseudo> <message>");
                    break;
                }

                SendWhisper(parts[1], string.Join(' ', parts[2..]), replyChannel, Reply);
                break;

            case "/reply":
            case "/r":
                if (parts.Length < 2)
                {
                    Reply("Usage : /reply <message>");
                    break;
                }

                if (_lastWhisperFromCharacterName is null)
                {
                    Reply("Personne ne vous a chuchoté récemment.");
                    break;
                }

                SendWhisper(_lastWhisperFromCharacterName, string.Join(' ', parts[1..]), replyChannel, Reply);
                break;

            case "/guild":
                HandleGuildCommand(db, parts.Length > 1 ? parts[1..] : [], replyChannel, Reply);
                break;

            case "/party":
                HandlePartyCommand(db, parts.Length > 1 ? parts[1..] : [], Reply);
                break;

            case "/kingdom":
                HandleKingdomCommand(db, parts.Length > 1 ? parts[1] : "", Reply);
                break;

            // Voir GDD/demande utilisateur — "ajoute des consommables pour booster la luck l'xp
            // la money" : consomme une potion de boost par id d'objet (voir Docs/Items.md).
            case "/use":
                if (parts.Length < 2 || !int.TryParse(parts[1], out var useItemId))
                {
                    Reply("Usage : /use <idObjet>");
                    return true;
                }

                UseConsumable(db, useItemId, Reply);
                break;

            // Voir GDD/demande utilisateur — "ajoute la possibilité de report un joueur" :
            // commande ouverte à tout le monde (pas seulement modération), traitée ici comme
            // /whisper /friend etc. ci-dessus.
            case "/report":
                if (parts.Length < 3)
                {
                    Reply("Usage : /report <pseudo> <raison>");
                    break;
                }

                ReportPlayer(db, parts[1], string.Join(' ', parts[2..]), Reply);
                break;

            default:
                return false;
        }

        return true;
    }

    private void UseConsumable(AetheriaDbContext db, int itemId, Action<string> reply)
    {
        var item = db.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
        {
            reply("Objet introuvable.");
            return;
        }

        var known = new HashSet<string> { "Potion d'expérience", "Potion de fortune", "Potion de chance" };
        if (!known.Contains(item.Name))
        {
            reply($"{item.Name} ne peut pas être utilisé de cette façon.");
            return;
        }

        var stack = db.InventoryItems.FirstOrDefault(i => i.CharacterId == CharacterId && i.ItemId == itemId);
        if (stack is null || stack.Quantity <= 0)
        {
            reply($"Vous n'avez pas de {item.Name}.");
            return;
        }

        var character = db.Characters.FirstOrDefault(c => c.Id == CharacterId);
        if (character is null)
        {
            reply("Personnage introuvable.");
            return;
        }

        switch (item.Name)
        {
            case "Potion d'expérience":
                character.XpBoostExpiresAtUtc = DateTime.UtcNow + TemporaryBoostService.BoostDuration;
                break;
            case "Potion de fortune":
                character.GoldBoostExpiresAtUtc = DateTime.UtcNow + TemporaryBoostService.BoostDuration;
                break;
            case "Potion de chance":
                character.LuckBoostExpiresAtUtc = DateTime.UtcNow + TemporaryBoostService.BoostDuration;
                break;
        }

        stack.Quantity--;
        if (stack.Quantity <= 0)
        {
            db.InventoryItems.Remove(stack);
        }

        db.SaveChanges();
        reply($"{item.Name} utilisé(e) — effet actif pendant {TemporaryBoostService.BoostDuration.TotalMinutes:0} minutes.");
    }

    private void SetActiveTitle(AetheriaDbContext db, string titleName, Action<string> reply)
    {
        var character = db.Characters.FirstOrDefault(c => c.Id == CharacterId);
        if (character is null)
        {
            reply("Personnage introuvable.");
            return;
        }

        if (string.Equals(titleName, "aucun", StringComparison.OrdinalIgnoreCase))
        {
            character.ActiveTitle = null;
            db.SaveChanges();
            reply("Titre retiré.");
            return;
        }

        var owned = db.CharacterTitles.Any(t => t.CharacterId == CharacterId && t.TitleKey == titleName);
        if (!owned)
        {
            reply($"Vous n'avez pas débloqué le titre '{titleName}'.");
            return;
        }

        character.ActiveTitle = titleName;
        db.SaveChanges();
        reply($"Titre actif : {titleName}.");
    }

    private void HandleFriendCommand(AetheriaDbContext db, string[] args, Action<string> reply)
    {
        switch (args[0].ToLowerInvariant())
        {
            case "list":
                var friends = db.Friendships
                    .Where(f => f.Status == FriendshipStatus.Accepted && (f.RequesterCharacterId == CharacterId || f.TargetCharacterId == CharacterId))
                    .ToList();
                if (friends.Count == 0)
                {
                    reply("Aucun ami pour l'instant.");
                    break;
                }

                var friendIds = friends.Select(f => f.RequesterCharacterId == CharacterId ? f.TargetCharacterId : f.RequesterCharacterId).ToList();
                var names = db.Characters.Where(c => friendIds.Contains(c.Id)).Select(c => c.Name).ToList();
                reply($"Amis : {string.Join(", ", names)}");
                break;

            case "add":
                if (args.Length < 2)
                {
                    reply("Usage : /friend add <pseudo>");
                    break;
                }

                var addTarget = db.Characters.FirstOrDefault(c => c.Name == args[1]);
                if (addTarget is null)
                {
                    reply($"Personnage introuvable : {args[1]}");
                    break;
                }

                if (addTarget.Id == CharacterId)
                {
                    reply("Impossible de s'ajouter soi-même.");
                    break;
                }

                var existingFriendship = db.Friendships.FirstOrDefault(f =>
                    (f.RequesterCharacterId == CharacterId && f.TargetCharacterId == addTarget.Id)
                    || (f.RequesterCharacterId == addTarget.Id && f.TargetCharacterId == CharacterId));
                if (existingFriendship is not null)
                {
                    reply($"Une relation existe déjà avec {addTarget.Name}.");
                    break;
                }

                db.Friendships.Add(new FriendshipEntity { Id = Guid.NewGuid(), RequesterCharacterId = CharacterId, TargetCharacterId = addTarget.Id });
                db.SaveChanges();
                reply($"Demande d'ami envoyée à {addTarget.Name}.");
                break;

            case "remove":
                if (args.Length < 2)
                {
                    reply("Usage : /friend remove <pseudo>");
                    break;
                }

                var removeTarget = db.Characters.FirstOrDefault(c => c.Name == args[1]);
                if (removeTarget is null)
                {
                    reply($"Personnage introuvable : {args[1]}");
                    break;
                }

                var toRemove = db.Friendships.Where(f =>
                    (f.RequesterCharacterId == CharacterId && f.TargetCharacterId == removeTarget.Id)
                    || (f.RequesterCharacterId == removeTarget.Id && f.TargetCharacterId == CharacterId)).ToList();
                if (toRemove.Count == 0)
                {
                    reply($"{removeTarget.Name} n'est pas dans votre liste d'amis.");
                    break;
                }

                db.Friendships.RemoveRange(toRemove);
                db.SaveChanges();
                reply($"{removeTarget.Name} retiré de vos amis.");
                break;

            default:
                reply("Usage : /friend add|remove|list [pseudo]");
                break;
        }
    }

    private void SendWhisper(string targetCharacterName, string message, ChatChannel replyChannel, Action<string> reply)
    {
        var target = registry.FindByCharacterName(targetCharacterName);
        if (target is null)
        {
            reply($"{targetCharacterName} n'est pas connecté(e).");
            return;
        }

        var outgoing = new ChatMessagePacket { SenderName = CharacterName, Message = message, Channel = ChatChannel.Prive, Rank = Rank, TargetCharacterName = targetCharacterName };
        target.SendPacket(outgoing);
        target.RecordIncomingWhisper(CharacterName);
        SendPacket(outgoing);
    }

    /// <summary>Voir GDD/demande utilisateur — "ajoute la possibilité de report un joueur" : ouvert à tout le monde (voir TryHandlePublicCommand), consultable par les admins (voir GET /api/admin/reports).</summary>
    private void ReportPlayer(AetheriaDbContext db, string targetCharacterName, string reason, Action<string> reply)
    {
        var target = FindCharacter(db, targetCharacterName, reply);
        if (target is null)
        {
            return;
        }

        if (target.Id == CharacterId)
        {
            reply("Vous ne pouvez pas vous signaler vous-même.");
            return;
        }

        db.Reports.Add(new ReportEntity
        {
            Id = Guid.NewGuid(),
            ReporterCharacterId = CharacterId,
            ReporterCharacterName = CharacterName,
            ReportedCharacterId = target.Id,
            ReportedCharacterName = target.Name,
            Reason = reason.Length > 300 ? reason[..300] : reason,
        });
        db.SaveChanges();
        reply($"{target.Name} a été signalé(e). Merci, un(e) modérateur/administrateur va l'examiner.");
    }

    private void HandleGuildCommand(AetheriaDbContext db, string[] args, ChatChannel replyChannel, Action<string> reply)
    {
        if (args.Length == 0)
        {
            reply("Usage : /guild info|leave|<message>");
            return;
        }

        var membership = db.GuildMembers.Include(m => m.Guild).FirstOrDefault(m => m.CharacterId == CharacterId);

        switch (args[0].ToLowerInvariant())
        {
            case "info":
                reply(membership is null ? "Vous n'êtes dans aucune guilde." : $"Guilde : {membership.Guild?.Name} — {db.GuildMembers.Count(m => m.GuildId == membership.GuildId)} membre(s).");
                break;

            case "leave":
                if (membership is null)
                {
                    reply("Vous n'êtes dans aucune guilde.");
                    break;
                }

                db.GuildMembers.Remove(membership);
                db.SaveChanges();
                reply("Vous avez quitté votre guilde.");
                break;

            default:
                // Voir GDD/demande utilisateur — "/guild <message>" : même diffusion que le canal
                // Guild du tchat (voir HandleChatMessage), déclenchée ici depuis une commande.
                if (membership is null)
                {
                    reply("Vous n'êtes dans aucune guilde.");
                    break;
                }

                var guildMemberIds = db.GuildMembers.Where(m => m.GuildId == membership.GuildId).Select(m => m.CharacterId).ToHashSet();
                var guildOutgoing = new ChatMessagePacket { SenderName = CharacterName, Message = string.Join(' ', args), Channel = ChatChannel.Guild, Rank = Rank };
                foreach (var session in registry.All().Where(s => guildMemberIds.Contains(s.CharacterId)))
                {
                    session.SendPacket(guildOutgoing);
                }

                break;
        }
    }

    private void HandlePartyCommand(AetheriaDbContext db, string[] args, Action<string> reply)
    {
        if (args.Length == 0)
        {
            reply("Usage : /party create|leave|<message>");
            return;
        }

        var membership = db.PartyMembers.FirstOrDefault(m => m.CharacterId == CharacterId);

        switch (args[0].ToLowerInvariant())
        {
            case "create":
                if (membership is not null)
                {
                    reply("Vous êtes déjà dans un groupe.");
                    break;
                }

                var code = Random.Shared.Next(10000, 100000).ToString();
                var party = new PartyEntity { Id = Guid.NewGuid(), LeaderCharacterId = CharacterId, JoinCode = code };
                db.Parties.Add(party);
                db.PartyMembers.Add(new PartyMemberEntity { Id = Guid.NewGuid(), PartyId = party.Id, CharacterId = CharacterId });
                db.SaveChanges();
                reply($"Groupe créé. Code : {code}");
                break;

            case "leave":
                if (membership is null)
                {
                    reply("Vous n'êtes dans aucun groupe.");
                    break;
                }

                var remainingMembers = db.PartyMembers.Where(m => m.PartyId == membership.PartyId && m.CharacterId != CharacterId).OrderBy(m => m.JoinedAtUtc).ToList();
                db.PartyMembers.Remove(membership);

                if (remainingMembers.Count == 0)
                {
                    var partyToDelete = db.Parties.FirstOrDefault(p => p.Id == membership.PartyId);
                    if (partyToDelete is not null)
                    {
                        db.Parties.Remove(partyToDelete);
                    }
                }
                else
                {
                    var partyToUpdate = db.Parties.FirstOrDefault(p => p.Id == membership.PartyId);
                    if (partyToUpdate is not null && partyToUpdate.LeaderCharacterId == CharacterId)
                    {
                        partyToUpdate.LeaderCharacterId = remainingMembers[0].CharacterId;
                    }
                }

                db.SaveChanges();
                reply("Vous avez quitté votre groupe.");
                break;

            default:
                // Voir GDD/demande utilisateur — "/party <message>" : pas de canal de tchat dédié
                // au groupe actuellement (voir ChatChannel — Global/Guild/Prive seulement), donc
                // diffusé en Global aux seuls membres du groupe plutôt que d'ajouter une valeur
                // d'enum pour ce seul usage (voir Docs/README.md pour cette limite assumée).
                if (membership is null)
                {
                    reply("Vous n'êtes dans aucun groupe.");
                    break;
                }

                var partyMemberIds = db.PartyMembers.Where(m => m.PartyId == membership.PartyId).Select(m => m.CharacterId).ToHashSet();
                var partyOutgoing = new ChatMessagePacket { SenderName = $"[GROUPE] {CharacterName}", Message = string.Join(' ', args), Channel = ChatChannel.Global, Rank = Rank };
                foreach (var session in registry.All().Where(s => partyMemberIds.Contains(s.CharacterId)))
                {
                    session.SendPacket(partyOutgoing);
                }

                break;
        }
    }

    private void HandleKingdomCommand(AetheriaDbContext db, string subCommand, Action<string> reply)
    {
        var character = db.Characters.FirstOrDefault(c => c.Id == CharacterId);
        if (character is null)
        {
            reply("Personnage introuvable.");
            return;
        }

        switch (subCommand.ToLowerInvariant())
        {
            case "members":
                var memberCount = db.Characters.Count(c => c.Kingdom == character.Kingdom);
                reply($"{character.Kingdom} compte {memberCount} personnage(s).");
                break;

            case "leaderboard":
                var standings = db.Kingdoms.OrderByDescending(k => k.WarPoints).ToList();
                reply(string.Join(" | ", standings.Select((k, i) => $"{i + 1}. {k.Name} ({k.WarPoints} pts)")));
                break;

            case "info":
            default:
                var kingdom = db.Kingdoms.FirstOrDefault(k => k.Type == character.Kingdom);
                reply(kingdom is null ? "Royaume introuvable." : $"{kingdom.Name} (capitale : {kingdom.CapitalName}) — {kingdom.WarPoints} points de guerre.");
                break;
        }
    }

    /// <summary>
    /// Commandes en jeu réservées aux modérateurs/administrateurs/fondateurs (voir GDD/demande
    /// utilisateur — "commandes réservées au modérateur/admin/fonda : ban de tout son compte,
    /// mute, nick pour renommer un nom d'utilisateur inapproprié"). Les réponses (confirmation ou
    /// erreur) ne sont envoyées qu'à l'expéditeur, jamais diffusées. Cible résolue par nom de
    /// personnage (celui vu dans le tchat) plutôt que par pseudo de compte, plus pratique en jeu.
    /// **Simplification assumée** : un compte banni/mute en jeu n'est pas déconnecté de force si
    /// déjà connecté (voir Docs/README.md) — le bannissement/mute s'applique dès le message
    /// suivant/la prochaine connexion.
    /// </summary>
    private void HandleChatCommand(AetheriaDbContext db, string command, ChatChannel replyChannel, bool isAdmin, UserRank rank)
    {
        void Reply(string message) => SendPacket(new ChatMessagePacket { SenderName = "Système", Message = message, Channel = replyChannel });

        if (!isAdmin && rank is not (UserRank.Moderateur or UserRank.Fondateur))
        {
            Reply("Commande réservée aux modérateurs/administrateurs/fondateurs.");
            return;
        }

        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        switch (parts[0].ToLowerInvariant())
        {
            case "/ban":
                if (parts.Length < 2)
                {
                    Reply("Usage : /ban <pseudo> [raison]");
                    return;
                }

                BanTargetAccount(db, parts[1], parts.Length > 2 ? string.Join(' ', parts[2..]) : "Banni via commande en jeu.", Reply);
                break;

            case "/mute":
                if (parts.Length < 2)
                {
                    Reply("Usage : /mute <pseudo>");
                    return;
                }

                SetTargetMute(db, parts[1], isMuted: true, Reply);
                break;

            case "/unmute":
                if (parts.Length < 2)
                {
                    Reply("Usage : /unmute <pseudo>");
                    return;
                }

                SetTargetMute(db, parts[1], isMuted: false, Reply);
                break;

            case "/nick":
                if (parts.Length < 3)
                {
                    Reply("Usage : /nick <pseudo> <nouveau_pseudo>");
                    return;
                }

                RenameTarget(db, parts[1], parts[2], Reply);
                break;

            // Voir GDD/demande utilisateur — "ajoute les commandes pour les niveaux des montres
            // (/monster-lvl pseudo (n° où est son monstre) lvl)" : cible une créature précise
            // d'un joueur par son numéro d'ordre (voir SetMonsterLevelByIndex), contrairement au
            // panel admin ("NIVEAU MAX EQUIPE") qui agit sur toute la collection d'un coup.
            case "/monster-lvl":
                if (parts.Length < 4 || !int.TryParse(parts[2], out var monsterIndex) || !int.TryParse(parts[3], out var targetLevel))
                {
                    Reply("Usage : /monster-lvl <pseudo> <numero> <niveau>");
                    return;
                }

                SetMonsterLevelByIndex(db, parts[1], monsterIndex, targetLevel, Reply);
                break;

            // Voir GDD/demande utilisateur — "ajoute une commande pour changer les iv" : même
            // ciblage par numéro d'ordre que /monster-lvl ci-dessus.
            case "/setiv":
                if (parts.Length < 5 || !int.TryParse(parts[2], out var setIvMonsterIndex) || !int.TryParse(parts[4], out var setIvValue))
                {
                    Reply("Usage : /setiv <pseudo> <numero> <hp|atk|def|vit|int|res> <valeur 0-31>");
                    return;
                }

                SetMonsterIvByIndex(db, parts[1], setIvMonsterIndex, parts[3], setIvValue, Reply);
                break;

            case "/give":
                if (parts.Length < 4 || !int.TryParse(parts[2], out var giveItemId) || !int.TryParse(parts[3], out var giveQty))
                {
                    Reply("Usage : /give <pseudo> <idObjet> <quantite>");
                    return;
                }

                GiveItem(db, parts[1], giveItemId, giveQty, Reply);
                break;

            case "/givemoney":
            case "/addmoney":
                if (parts.Length < 3 || !long.TryParse(parts[2], out var giveMoneyAmount))
                {
                    Reply($"Usage : {parts[0]} <pseudo> <montant>");
                    return;
                }

                AdjustGold(db, parts[1], giveMoneyAmount, Reply);
                break;

            case "/removemoney":
                if (parts.Length < 3 || !long.TryParse(parts[2], out var removeMoneyAmount))
                {
                    Reply("Usage : /removemoney <pseudo> <montant>");
                    return;
                }

                AdjustGold(db, parts[1], -removeMoneyAmount, Reply);
                break;

            case "/setmoney":
                if (parts.Length < 3 || !long.TryParse(parts[2], out var setMoneyAmount))
                {
                    Reply("Usage : /setmoney <pseudo> <montant>");
                    return;
                }

                SetGold(db, parts[1], setMoneyAmount, Reply);
                break;

            case "/givexp":
                if (parts.Length < 3 || !long.TryParse(parts[2], out var giveXpAmount))
                {
                    Reply("Usage : /givexp <pseudo> <montant>");
                    return;
                }

                GiveCharacterExperience(db, parts[1], giveXpAmount, Reply);
                break;

            case "/givemonster":
                if (parts.Length < 3 || !int.TryParse(parts[2], out var giveMonsterSpeciesId))
                {
                    Reply("Usage : /givemonster <pseudo> <idEspece>");
                    return;
                }

                GiveMonster(db, parts[1], giveMonsterSpeciesId, Reply);
                break;

            case "/setlevel":
                if (parts.Length < 3 || !int.TryParse(parts[2], out var setCharLevel))
                {
                    Reply("Usage : /setlevel <pseudo> <niveau>");
                    return;
                }

                SetCharacterLevel(db, parts[1], setCharLevel, Reply);
                break;

            case "/resetlevel":
                if (parts.Length < 2)
                {
                    Reply("Usage : /resetlevel <pseudo>");
                    return;
                }

                SetCharacterLevel(db, parts[1], 1, Reply);
                break;

            case "/setclass":
                if (parts.Length < 3 || !Enum.TryParse<CharacterClass>(parts[2], true, out var newClass))
                {
                    Reply($"Usage : /setclass <pseudo> <{string.Join('|', Enum.GetNames<CharacterClass>())}>");
                    return;
                }

                SetCharacterField(db, parts[1], c => c.Class = newClass, $"classe {newClass}", Reply);
                break;

            case "/setkingdom":
                if (parts.Length < 3 || !Enum.TryParse<KingdomType>(parts[2], true, out var newKingdom))
                {
                    Reply($"Usage : /setkingdom <pseudo> <{string.Join('|', Enum.GetNames<KingdomType>())}>");
                    return;
                }

                SetCharacterField(db, parts[1], c => c.Kingdom = newKingdom, $"royaume {newKingdom}", Reply);
                break;

            case "/clearinventory":
                if (parts.Length < 2)
                {
                    Reply("Usage : /clearinventory <pseudo>");
                    return;
                }

                ClearInventory(db, parts[1], Reply);
                break;

            case "/deletemonster":
                if (parts.Length < 3 || !int.TryParse(parts[2], out var deleteMonsterIndex))
                {
                    Reply("Usage : /deletemonster <pseudo> <numero>");
                    return;
                }

                DeleteMonsterByIndex(db, parts[1], deleteMonsterIndex, Reply);
                break;

            case "/invsee":
                if (parts.Length < 2)
                {
                    Reply("Usage : /invsee <pseudo>");
                    return;
                }

                InspectInventory(db, parts[1], Reply);
                break;

            case "/unban":
                if (parts.Length < 2)
                {
                    Reply("Usage : /unban <pseudo>");
                    return;
                }

                SetBanned(db, parts[1], false, Reply);
                break;

            case "/ipban":
                if (parts.Length < 2)
                {
                    Reply("Usage : /ipban <pseudo>");
                    return;
                }

                BanCharacterIp(db, parts[1], Reply);
                break;

            case "/unbanip":
                if (parts.Length < 2)
                {
                    Reply("Usage : /unbanip <adresse IP>");
                    return;
                }

                UnbanIp(db, parts[1], Reply);
                break;

            // Voir GDD/demande utilisateur — "shop avec des gems... argent réel" : les gemmes
            // représentent de l'argent réel reçu hors-jeu (aucune passerelle de paiement branchée
            // pour le moment, voir GDD) — réservé au Fondateur seul, un cran au-dessus des autres
            // commandes admin/modérateur (même logique que /dev), pour limiter le risque de
            // crédits frauduleux/erronés de monnaie premium.
            case "/givegems":
                if (rank != UserRank.Fondateur)
                {
                    Reply("Commande réservée au Fondateur.");
                    return;
                }

                if (parts.Length < 3 || !long.TryParse(parts[2], out var gemsAmount))
                {
                    Reply("Usage : /givegems <pseudo> <montant>");
                    return;
                }

                GiveGems(db, parts[1], gemsAmount, Reply);
                break;

            // Voir GDD/demande utilisateur — "ajoute une commande et un champ admin pour donner
            // des palier a un joueur" (paliers du Passe de Niveau) — reserve au Fondateur, meme
            // logique que /givegems ci-dessus (levier economique impactant).
            case "/givepalier":
                if (rank != UserRank.Fondateur)
                {
                    Reply("Commande réservée au Fondateur.");
                    return;
                }

                if (parts.Length < 3 || !int.TryParse(parts[2], out var palierLevels))
                {
                    Reply("Usage : /givepalier <pseudo> <niveaux>");
                    return;
                }

                GiveBattlePassLevels(db, parts[1], palierLevels, Reply);
                break;

            // Voir GDD/demande utilisateur — "ajoute une commande pour give des montures" — reserve
            // au Fondateur, meme logique que /givepalier/givegems ci-dessus.
            case "/givemount":
                if (rank != UserRank.Fondateur)
                {
                    Reply("Commande réservée au Fondateur.");
                    return;
                }

                if (parts.Length < 3)
                {
                    Reply("Usage : /givemount <pseudo> <cleMonture>");
                    return;
                }

                GiveMount(db, parts[1], parts[2], Reply);
                break;

            // Voir GDD/demande utilisateur — "ajoute des commandes admin abuse comme boost de
            // chance pour tout le monde et autre" : applique une potion de boost (voir
            // TemporaryBoostService) à TOUS les personnages actuellement connectés d'un coup —
            // réservé au Fondateur (impact serveur entier, comme /dev/givegems).
            case "/globalboost":
                if (rank != UserRank.Fondateur)
                {
                    Reply("Commande réservée au Fondateur.");
                    return;
                }

                if (parts.Length < 2 || parts[1] is not ("xp" or "money" or "luck"))
                {
                    Reply("Usage : /globalboost <xp|money|luck>");
                    return;
                }

                GlobalBoost(db, parts[1], Reply);
                break;

            // Voir GDD/demande utilisateur — "commandes admin abuse... et autre" : donne un objet à
            // TOUS les personnages connectés d'un coup (variante en masse de /give).
            case "/globalgive":
                if (rank != UserRank.Fondateur)
                {
                    Reply("Commande réservée au Fondateur.");
                    return;
                }

                if (parts.Length < 3 || !int.TryParse(parts[1], out var globalGiveItemId) || !int.TryParse(parts[2], out var globalGiveQty))
                {
                    Reply("Usage : /globalgive <idObjet> <quantite>");
                    return;
                }

                GlobalGive(db, globalGiveItemId, globalGiveQty, Reply);
                break;

            // Voir GDD/demande utilisateur — "réservé au fonda/dev" : un cran au-dessus des autres
            // commandes admin (même logique que /toggle-admin, voir Server/Program.cs), donc
            // revérifié spécifiquement ici plutôt que de se contenter du garde commun en haut de
            // cette méthode.
            case "/dev":
                if (rank != UserRank.Fondateur)
                {
                    Reply("Commande réservée au Fondateur.");
                    return;
                }

                HandleDevCommand(db, parts.Length > 1 ? parts[1..] : [], Reply);
                break;

            default:
                Reply("Commande inconnue. Tapez /help pour la liste des commandes disponibles.");
                break;
        }
    }

    private static void BanTargetAccount(AetheriaDbContext db, string targetCharacterName, string reason, Action<string> reply)
    {
        var target = db.Characters.FirstOrDefault(c => c.Name == targetCharacterName);
        if (target is null)
        {
            reply($"Personnage introuvable : {targetCharacterName}");
            return;
        }

        var user = db.Users.FirstOrDefault(u => u.Id == target.UserId);
        if (user is null)
        {
            reply("Compte introuvable.");
            return;
        }

        user.IsBanned = true;
        user.BanReason = reason;
        db.SaveChanges();
        reply($"{targetCharacterName} a été banni : {reason}");
    }

    private static void SetTargetMute(AetheriaDbContext db, string targetCharacterName, bool isMuted, Action<string> reply)
    {
        var target = db.Characters.FirstOrDefault(c => c.Name == targetCharacterName);
        if (target is null)
        {
            reply($"Personnage introuvable : {targetCharacterName}");
            return;
        }

        var user = db.Users.FirstOrDefault(u => u.Id == target.UserId);
        if (user is null)
        {
            reply("Compte introuvable.");
            return;
        }

        user.IsMuted = isMuted;
        db.SaveChanges();
        reply(isMuted ? $"{targetCharacterName} est maintenant muet(te)." : $"{targetCharacterName} peut de nouveau parler.");
    }

    private void RenameTarget(AetheriaDbContext db, string targetCharacterName, string newName, Action<string> reply)
    {
        var trimmedNewName = newName.Trim();
        if (trimmedNewName.Length < 3)
        {
            reply("Le nouveau nom doit faire au moins 3 caractères.");
            return;
        }

        var target = db.Characters.FirstOrDefault(c => c.Name == targetCharacterName);
        if (target is null)
        {
            reply($"Personnage introuvable : {targetCharacterName}");
            return;
        }

        if (db.Characters.Any(c => c.Name == trimmedNewName))
        {
            reply("Ce nom est déjà utilisé.");
            return;
        }

        target.Name = trimmedNewName;
        db.SaveChanges();
        reply($"{targetCharacterName} a été renommé en {trimmedNewName}.");

        var targetSession = registry.All().FirstOrDefault(s => s.CharacterId == target.Id);
        if (targetSession is not null)
        {
            targetSession.UpdateCharacterName(trimmedNewName);
            registry.BroadcastExcept(Guid.Empty, new PlayerJoinedPacket
            {
                CharacterId = target.Id,
                Name = trimmedNewName,
                PositionX = targetSession.PositionX,
                PositionY = targetSession.PositionY,
                Rank = targetSession.Rank,
                Level = targetSession.Level,
                Title = targetSession.Title,
            });
        }
    }

    /// <summary>
    /// Voir GDD/demande utilisateur — "/monster-lvl pseudo (n° où est son monstre) lvl" : le
    /// numéro correspond à l'ordre d'affichage du panneau Monstres côté client (voir
    /// <c>DrawMonstersPanel</c>), lui-même dans l'ordre de capture — d'où le tri explicite
    /// identique ici, sans quoi l'ordre EF Core par défaut n'est pas garanti stable.
    /// </summary>
    private static void SetMonsterLevelByIndex(AetheriaDbContext db, string targetCharacterName, int monsterIndex, int level, Action<string> reply)
    {
        var target = db.Characters.FirstOrDefault(c => c.Name == targetCharacterName);
        if (target is null)
        {
            reply($"Personnage introuvable : {targetCharacterName}");
            return;
        }

        var monsters = db.Monsters.Where(m => m.OwnerCharacterId == target.Id).OrderBy(m => m.CapturedAtUtc).ToList();
        if (monsterIndex < 1 || monsterIndex > monsters.Count)
        {
            reply($"Numéro invalide : {targetCharacterName} a {monsters.Count} créature(s) (1 à {monsters.Count}).");
            return;
        }

        var monster = monsters[monsterIndex - 1];
        monster.Level = Math.Clamp(level, 1, MonsterProgressionService.MaxLevel);
        monster.Experience = 0;
        db.SaveChanges();
        reply($"{(monster.Nickname.Length > 0 ? monster.Nickname : "Créature")} (#{monsterIndex} de {targetCharacterName}) est maintenant niveau {monster.Level}.");
    }

    /// <summary>Voir GDD/demande utilisateur — "ajoute une commande pour changer les iv" : même ciblage par numéro d'ordre que <see cref="SetMonsterLevelByIndex"/> ci-dessus.</summary>
    private static void SetMonsterIvByIndex(AetheriaDbContext db, string targetCharacterName, int monsterIndex, string statName, int value, Action<string> reply)
    {
        var target = db.Characters.FirstOrDefault(c => c.Name == targetCharacterName);
        if (target is null)
        {
            reply($"Personnage introuvable : {targetCharacterName}");
            return;
        }

        var monsters = db.Monsters.Where(m => m.OwnerCharacterId == target.Id).OrderBy(m => m.CapturedAtUtc).ToList();
        if (monsterIndex < 1 || monsterIndex > monsters.Count)
        {
            reply($"Numéro invalide : {targetCharacterName} a {monsters.Count} créature(s) (1 à {monsters.Count}).");
            return;
        }

        var monster = monsters[monsterIndex - 1];
        var clamped = Math.Clamp(value, 0, MonsterIvRoller.MaxIv);
        var statLabel = statName.ToLowerInvariant() switch
        {
            "hp" => "PV",
            "atk" => "Attaque",
            "def" => "Défense",
            "vit" => "Vitesse",
            "int" => "Intelligence",
            "res" => "Résistance",
            _ => null,
        };

        switch (statName.ToLowerInvariant())
        {
            case "hp": monster.IvHealth = clamped; break;
            case "atk": monster.IvAttack = clamped; break;
            case "def": monster.IvDefense = clamped; break;
            case "vit": monster.IvSpeed = clamped; break;
            case "int": monster.IvIntelligence = clamped; break;
            case "res": monster.IvResistance = clamped; break;
            default:
                reply("Statistique invalide : hp, atk, def, vit, int ou res.");
                return;
        }

        db.SaveChanges();
        reply($"{(monster.Nickname.Length > 0 ? monster.Nickname : "Créature")} (#{monsterIndex} de {targetCharacterName}) : IV {statLabel} = {clamped}.");
    }

    /// <summary>
    /// Voir GDD/demande utilisateur — "je n'arrive pas à me give de monstre" : recherche insensible
    /// à la casse et aux accents (voir TextMatching) plutôt qu'une correspondance exacte, qui
    /// échouait silencieusement pour une saisie manuelle légèrement différente.
    /// </summary>
    private static CharacterEntity? FindCharacter(AetheriaDbContext db, string name, Action<string> reply)
    {
        var target = db.Characters.AsEnumerable().FirstOrDefault(c => TextMatching.NamesMatch(c.Name, name));
        if (target is null)
        {
            reply($"Personnage introuvable : {name}");
        }

        return target;
    }

    private static void GiveItem(AetheriaDbContext db, string targetCharacterName, int itemId, int quantity, Action<string> reply)
    {
        var target = FindCharacter(db, targetCharacterName, reply);
        if (target is null)
        {
            return;
        }

        var item = db.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
        {
            reply("Objet introuvable.");
            return;
        }

        // Voir GDD/demande utilisateur — "limite de stack d'item à 99 par item dans l'inventaire".
        var quantityClamped = Math.Max(1, quantity);
        InventoryStackingService.AddQuantity(db, target.Id, itemId, quantityClamped, item.MaxStackSize);

        db.SaveChanges();
        reply($"{quantityClamped}x {item.Name} donné(s) à {target.Name}.");
    }

    private static void AdjustGold(AetheriaDbContext db, string targetCharacterName, long delta, Action<string> reply)
    {
        var target = FindCharacter(db, targetCharacterName, reply);
        if (target is null)
        {
            return;
        }

        target.Gold = Math.Max(0, target.Gold + delta);
        db.SaveChanges();
        reply($"{target.Name} a maintenant {target.Gold} or.");
    }

    private static void SetGold(AetheriaDbContext db, string targetCharacterName, long amount, Action<string> reply)
    {
        var target = FindCharacter(db, targetCharacterName, reply);
        if (target is null)
        {
            return;
        }

        target.Gold = Math.Max(0, amount);
        db.SaveChanges();
        reply($"{target.Name} a maintenant {target.Gold} or.");
    }

    /// <summary>Voir GDD/demande utilisateur — crédite manuellement des gemmes (monnaie premium, argent réel reçu hors-jeu) sur le COMPTE (pas le personnage) — voir <see cref="UserEntity.Gems"/>.</summary>
    private static void GiveGems(AetheriaDbContext db, string targetCharacterName, long amount, Action<string> reply)
    {
        var target = db.Characters.Include(c => c.User).FirstOrDefault(c => c.Name == targetCharacterName);
        if (target?.User is null)
        {
            reply($"Personnage introuvable : {targetCharacterName}");
            return;
        }

        target.User.Gems = Math.Max(0, target.User.Gems + amount);
        db.SaveChanges();
        reply($"{targetCharacterName} (compte {target.User.Username}) a maintenant {target.User.Gems} gemme(s).");
    }

    /// <summary>Voir GDD/demande utilisateur — "ajoute une commande et un champ admin pour donner des palier a un joueur" (paliers du Passe de Niveau).</summary>
    private static void GiveBattlePassLevels(AetheriaDbContext db, string targetCharacterName, int levels, Action<string> reply)
    {
        var target = FindCharacter(db, targetCharacterName, reply);
        if (target is null)
        {
            return;
        }

        var levelsClamped = Math.Max(1, levels);
        BattlePassService.GrantLevelsAsync(db, target, levelsClamped).GetAwaiter().GetResult();
        db.SaveChanges();
        reply($"{levelsClamped} palier(s) de Passe de Niveau donné(s) à {target.Name} (niveau {target.BattlePassLevel}).");
    }

    /// <summary>Voir GDD/demande utilisateur — "ajoute une commande pour give des montures" : les montures sont liees au compte (voir CollectionEntity/AchievementService), pas au personnage.</summary>
    private static void GiveMount(AetheriaDbContext db, string targetCharacterName, string mountKey, Action<string> reply)
    {
        var target = FindCharacter(db, targetCharacterName, reply);
        if (target is null)
        {
            return;
        }

        var mount = MountCatalog.Find(mountKey);
        if (mount is null)
        {
            reply($"Monture introuvable : {mountKey}");
            return;
        }

        var alreadyOwned = db.Collections.Any(c => c.UserId == target.UserId && c.Category == "Monture" && c.CollectionKey == mount.Key);
        if (alreadyOwned)
        {
            reply($"{target.Name} possède déjà {mount.Name}.");
            return;
        }

        db.Collections.Add(new CollectionEntity { Id = Guid.NewGuid(), UserId = target.UserId, CollectionKey = mount.Key, Category = "Monture" });
        db.SaveChanges();
        reply($"{mount.Name} donnée à {target.Name}.");
    }

    /// <summary>Voir GDD/demande utilisateur — "commandes admin abuse comme boost de chance pour tout le monde".</summary>
    private void GlobalBoost(AetheriaDbContext db, string kind, Action<string> reply)
    {
        var onlineCharacterIds = registry.All().Select(s => s.CharacterId).ToHashSet();
        if (onlineCharacterIds.Count == 0)
        {
            reply("Aucun joueur connecté.");
            return;
        }

        var characters = db.Characters.Where(c => onlineCharacterIds.Contains(c.Id)).ToList();
        var expiresAt = DateTime.UtcNow + TemporaryBoostService.BoostDuration;
        foreach (var character in characters)
        {
            switch (kind)
            {
                case "xp": character.XpBoostExpiresAtUtc = expiresAt; break;
                case "money": character.GoldBoostExpiresAtUtc = expiresAt; break;
                case "luck": character.LuckBoostExpiresAtUtc = expiresAt; break;
            }
        }

        db.SaveChanges();
        reply($"Boost {kind} appliqué à {characters.Count} joueur(s) connecté(s) pour {TemporaryBoostService.BoostDuration.TotalMinutes:0} minutes.");
    }

    /// <summary>Voir GDD/demande utilisateur — "commandes admin abuse... et autre" : variante en masse de /give.</summary>
    private void GlobalGive(AetheriaDbContext db, int itemId, int quantity, Action<string> reply)
    {
        var item = db.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
        {
            reply("Objet introuvable.");
            return;
        }

        var onlineCharacterIds = registry.All().Select(s => s.CharacterId).ToHashSet();
        if (onlineCharacterIds.Count == 0)
        {
            reply("Aucun joueur connecté.");
            return;
        }

        var quantityClamped = Math.Max(1, quantity);
        foreach (var characterId in onlineCharacterIds)
        {
            InventoryStackingService.AddQuantity(db, characterId, itemId, quantityClamped, item.MaxStackSize);
        }

        db.SaveChanges();
        reply($"{quantityClamped}x {item.Name} donné(s) à {onlineCharacterIds.Count} joueur(s) connecté(s).");
    }

    private static void GiveCharacterExperience(AetheriaDbContext db, string targetCharacterName, long amount, Action<string> reply)
    {
        var target = FindCharacter(db, targetCharacterName, reply);
        if (target is null)
        {
            return;
        }

        CharacterProgressionService.GrantExperience(target, amount);
        db.SaveChanges();
        reply($"{target.Name} est maintenant niveau {target.Level}.");
    }

    private static void SetCharacterLevel(AetheriaDbContext db, string targetCharacterName, int level, Action<string> reply)
    {
        var target = FindCharacter(db, targetCharacterName, reply);
        if (target is null)
        {
            return;
        }

        target.Level = Math.Max(1, level);
        target.Experience = 0;
        db.SaveChanges();
        reply($"{target.Name} est maintenant niveau {target.Level}.");
    }

    private static void SetCharacterField(AetheriaDbContext db, string targetCharacterName, Action<CharacterEntity> setter, string description, Action<string> reply)
    {
        var target = FindCharacter(db, targetCharacterName, reply);
        if (target is null)
        {
            return;
        }

        setter(target);
        db.SaveChanges();
        reply($"{target.Name} : {description}.");
    }

    private static void GiveMonster(AetheriaDbContext db, string targetCharacterName, int speciesId, Action<string> reply)
    {
        var target = FindCharacter(db, targetCharacterName, reply);
        if (target is null)
        {
            return;
        }

        // Voir GDD/demande utilisateur — "le don de monstre doit se faire avec l'id pas l'espece".
        var species = db.MonsterSpecies.FirstOrDefault(s => s.Id == speciesId);
        if (species is null)
        {
            reply($"Espèce introuvable : id {speciesId}");
            return;
        }

        var monster = new MonsterEntity { Id = Guid.NewGuid(), OwnerCharacterId = target.Id, SpeciesId = species.Id, Variant = MonsterVariant.Normal, Nickname = species.Name, Level = 1, Nature = MonsterNatureCatalog.RollRandom(Random.Shared) };
        MonsterIvRoller.RollInto(monster, Random.Shared);
        db.Monsters.Add(monster);
        db.SaveChanges();
        reply($"{species.Name} donné à {target.Name}.");
    }

    private static void ClearInventory(AetheriaDbContext db, string targetCharacterName, Action<string> reply)
    {
        var target = FindCharacter(db, targetCharacterName, reply);
        if (target is null)
        {
            return;
        }

        var items = db.InventoryItems.Where(i => i.CharacterId == target.Id).ToList();
        db.InventoryItems.RemoveRange(items);
        db.SaveChanges();
        reply($"Inventaire de {target.Name} vidé ({items.Count} objet(s) retiré(s)).");
    }

    private static void DeleteMonsterByIndex(AetheriaDbContext db, string targetCharacterName, int monsterIndex, Action<string> reply)
    {
        var target = FindCharacter(db, targetCharacterName, reply);
        if (target is null)
        {
            return;
        }

        var monsters = db.Monsters.Where(m => m.OwnerCharacterId == target.Id).OrderBy(m => m.CapturedAtUtc).ToList();
        if (monsterIndex < 1 || monsterIndex > monsters.Count)
        {
            reply($"Numéro invalide : {target.Name} a {monsters.Count} créature(s).");
            return;
        }

        var monster = monsters[monsterIndex - 1];
        db.Monsters.Remove(monster);
        db.SaveChanges();
        reply($"Créature #{monsterIndex} de {target.Name} supprimée.");
    }

    private static void InspectInventory(AetheriaDbContext db, string targetCharacterName, Action<string> reply)
    {
        var target = FindCharacter(db, targetCharacterName, reply);
        if (target is null)
        {
            return;
        }

        var items = db.InventoryItems.Where(i => i.CharacterId == target.Id).Join(db.Items, i => i.ItemId, it => it.Id, (i, it) => $"{it.Name} x{i.Quantity}").ToList();
        reply(items.Count == 0 ? $"{target.Name} n'a aucun objet." : $"Inventaire de {target.Name} : {string.Join(", ", items)}");
    }

    private static void SetBanned(AetheriaDbContext db, string targetCharacterName, bool banned, Action<string> reply)
    {
        var target = db.Characters.Include(c => c.User).FirstOrDefault(c => c.Name == targetCharacterName);
        if (target?.User is null)
        {
            reply($"Personnage introuvable : {targetCharacterName}");
            return;
        }

        target.User.IsBanned = banned;
        if (!banned)
        {
            target.User.BanReason = null;
        }

        db.SaveChanges();
        reply($"{targetCharacterName} est {(banned ? "banni" : "débanni")}.");
    }

    private static void BanCharacterIp(AetheriaDbContext db, string targetCharacterName, Action<string> reply)
    {
        var target = db.Characters.Include(c => c.User).FirstOrDefault(c => c.Name == targetCharacterName);
        if (target?.User is null)
        {
            reply($"Personnage introuvable : {targetCharacterName}");
            return;
        }

        if (target.User.LastKnownIp is not { Length: > 0 } ip)
        {
            reply($"Aucune adresse IP connue pour {targetCharacterName}.");
            return;
        }

        if (!db.BannedIps.Any(b => b.IpAddress == ip))
        {
            db.BannedIps.Add(new BannedIpEntity { Id = Guid.NewGuid(), IpAddress = ip, Reason = $"IP de {targetCharacterName} bannie via commande en jeu." });
            db.SaveChanges();
        }

        reply($"Adresse IP de {targetCharacterName} bannie.");
    }

    private static void UnbanIp(AetheriaDbContext db, string ipAddress, Action<string> reply)
    {
        var bans = db.BannedIps.Where(b => b.IpAddress == ipAddress).ToList();
        if (bans.Count == 0)
        {
            reply($"{ipAddress} n'est pas bannie.");
            return;
        }

        db.BannedIps.RemoveRange(bans);
        db.SaveChanges();
        reply($"{ipAddress} débannie.");
    }

    /// <summary>
    /// Voir GDD/demande utilisateur — namespace "/dev", réservé au Fondateur. Volontairement
    /// restreint par rapport à la liste demandée : pas de commandes destructrices pour le serveur
    /// entier (crash/stopserver/wipeworld/resetserver), pas d'exécution de code arbitraire
    /// (console/execute) — voir le résumé donné au joueur pour le détail de ce qui a été refusé
    /// et pourquoi (risque de sécurité/destruction irréversible plutôt qu'une limite technique).
    /// </summary>
    private void HandleDevCommand(AetheriaDbContext db, string[] args, Action<string> reply)
    {
        if (args.Length == 0)
        {
            reply("Usage : /dev giveall|unlockall <pseudo> ; /dev memory ; /dev gc");
            return;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "giveall":
                if (args.Length < 2)
                {
                    reply("Usage : /dev giveall <pseudo>");
                    return;
                }

                DevGiveAll(db, args[1], reply);
                break;

            case "unlockall":
                if (args.Length < 2)
                {
                    reply("Usage : /dev unlockall <pseudo>");
                    return;
                }

                DevUnlockAll(db, args[1], reply);
                break;

            case "memory":
                reply($"Mémoire gérée : {GC.GetTotalMemory(false) / 1024 / 1024} Mo.");
                break;

            case "gc":
                GC.Collect();
                reply("Garbage collection forcée.");
                break;

            default:
                reply("Commande /dev inconnue ou non disponible.");
                break;
        }
    }

    private static void DevGiveAll(AetheriaDbContext db, string targetCharacterName, Action<string> reply)
    {
        var target = FindCharacter(db, targetCharacterName, reply);
        if (target is null)
        {
            return;
        }

        var items = db.Items.Where(i => i.IsObtainable).ToList();
        foreach (var item in items)
        {
            InventoryStackingService.AddQuantity(db, target.Id, item.Id, 1, item.MaxStackSize);
        }

        db.SaveChanges();
        reply($"{items.Count} objet(s) du catalogue donné(s) à {target.Name}.");
    }

    private static void DevUnlockAll(AetheriaDbContext db, string targetCharacterName, Action<string> reply)
    {
        var target = db.Characters.FirstOrDefault(c => c.Name == targetCharacterName);
        if (target is null)
        {
            reply($"Personnage introuvable : {targetCharacterName}");
            return;
        }

        var unlockedKeys = db.Achievements.Where(a => a.UserId == target.UserId).Select(a => a.AchievementKey).ToHashSet();
        var missingKeys = AchievementCatalog.All.Select(a => a.Key).Where(k => !unlockedKeys.Contains(k)).ToList();
        foreach (var key in missingKeys)
        {
            db.Achievements.Add(new AchievementEntity { Id = Guid.NewGuid(), UserId = target.UserId, AchievementKey = key });
        }

        db.SaveChanges();
        reply($"{missingKeys.Count} succès débloqué(s) pour {targetCharacterName}.");
    }
}
