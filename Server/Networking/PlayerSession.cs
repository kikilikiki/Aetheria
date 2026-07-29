using System.Net.Sockets;
using Aetheria.Database.Context;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Network;
using Aetheria.Shared.Network.Packets;
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

    /// <summary>Mis à jour immédiatement par la commande <c>/nick</c> (voir <see cref="HandleChatCommand"/>) — sans attendre une reconnexion.</summary>
    public void UpdateCharacterName(string newName) => CharacterName = newName;

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
        UserId = userId;
        var user = db.Users.FirstOrDefault(u => u.Id == userId);
        Rank = user?.Rank ?? UserRank.Joueur;
        IsAdmin = user?.IsAdmin ?? false;

        // Position de départ : la capitale du royaume choisi. Le placement réel dans le
        // monde persistant (royaumes/donjons) arrive avec les systèmes de jeu (Phase G).
        PositionX = 0;
        PositionY = 0;

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
        });

        logger.LogInformation("{CharacterName} est entré dans le monde.", character.Name);
    }

    private void HandlePlayerMove(PlayerMovePacket move)
    {
        if (!HasEnteredWorld)
        {
            return;
        }

        // Pas encore de validation de portée/obstacles côté serveur (voir GDD — Phase G pour un
        // monde partagé complet) : la position envoyée par le client est acceptée telle quelle,
        // puis diffusée à tous — y compris à l'émetteur, pour rester la seule source de vérité
        // sur sa propre position affichée (comportement autoritaire déjà en place côté Client).
        PositionX = move.TargetX;
        PositionY = move.TargetY;

        var update = new PlayerPositionUpdatePacket { CharacterId = CharacterId, PositionX = PositionX, PositionY = PositionY };
        SendPacket(update);
        registry.BroadcastExcept(CharacterId, update);
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
        var self = db.Users.Where(u => u.Id == UserId).Select(u => new { u.IsAdmin, u.Rank, u.IsMuted }).FirstOrDefault();
        if (self is null)
        {
            return;
        }

        if (trimmed.StartsWith("/duel", StringComparison.OrdinalIgnoreCase))
        {
            // Voir GDD/demande utilisateur — "ajouter les demandes en duel pour le pvp" :
            // commande ouverte à tout le monde (contrairement à HandleChatCommand, réservée
            // aux modérateurs/admin/fondateur), donc traitée à part, avant ce filtrage.
            HandleDuelCommand(trimmed, chat.Channel);
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
    /// Voir GDD/demande utilisateur — "ajouter les demandes en duel pour le pvp" : ouvert à tout
    /// le monde (contrairement à <see cref="HandleChatCommand"/>). Le combat n'est pas démarré
    /// ici — juste l'invitation ; voir <see cref="HandleDuelResponse"/> pour la suite une fois
    /// acceptée/refusée.
    /// </summary>
    private void HandleDuelCommand(string command, ChatChannel replyChannel)
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

        duelInvites.SendInvite(CharacterId, CharacterName, target.CharacterId);
        target.SendPacket(new DuelInvitePacket { FromCharacterName = CharacterName });
        Reply($"Demande de duel envoyée à {target.CharacterName}.");
    }

    /// <summary>
    /// Réponse du joueur défié (voir <see cref="HandleDuelCommand"/>) : si accepté, notifie le
    /// défieur (voir <see cref="DuelAcceptedPacket"/>) dont le client démarre lui-même le combat
    /// via <c>POST /api/pvp/challenge</c> (déjà entièrement implémenté côté HTTP — voir
    /// CombatService.StartPvpAsync), puis reçoit à son tour <see cref="DuelStartedPacket"/>
    /// une fois ce combat créé (voir l'endpoint dans Server/Program.cs).
    /// </summary>
    private void HandleDuelResponse(DuelResponsePacket response)
    {
        if (!duelInvites.TryConsume(CharacterId, out var challengerId, out var challengerName))
        {
            return;
        }

        var challengerSession = registry.FindByCharacterId(challengerId);
        if (!response.Accept)
        {
            challengerSession?.SendPacket(new ChatMessagePacket { SenderName = "Système", Message = $"{CharacterName} a refusé le duel.", Channel = ChatChannel.Global });
            return;
        }

        challengerSession?.SendPacket(new DuelAcceptedPacket { OpponentCharacterId = CharacterId, OpponentCharacterName = CharacterName });
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

            default:
                Reply("Commande inconnue. Commandes disponibles : /ban, /mute, /unmute, /nick.");
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
            });
        }
    }
}
