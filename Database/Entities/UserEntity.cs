using Aetheria.Shared.Enums;

namespace Aetheria.Database.Entities;

/// <summary>Compte joueur (table <c>Users</c>). Un compte possède plusieurs <see cref="CharacterEntity"/>.</summary>
public sealed class UserEntity
{
    public Guid Id { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }

    /// <summary>Hash BCrypt du mot de passe — jamais le mot de passe en clair (voir Server/Networking).</summary>
    public required string PasswordHash { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsBanned { get; set; }
    public string? BanReason { get; set; }

    /// <summary>Compte administrateur (voir AdminPanel) — peut supprimer/modifier les comptes joueurs.</summary>
    public bool IsAdmin { get; set; }

    /// <summary>Grade communautaire affiché dans le tchat/la liste des joueurs en ligne (voir GDD — assignable par un administrateur).</summary>
    public UserRank Rank { get; set; } = UserRank.Joueur;

    /// <summary>Muet (voir GDD/demande utilisateur — "mute pour ne pas qu'il parle dans le tchat") : les messages de tchat envoyés sont silencieusement refusés côté serveur.</summary>
    public bool IsMuted { get; set; }

    /// <summary>Dernière IP connue (renseignée à la connexion, voir GDD/demande utilisateur — "ban ip") — sert de cible à un bannissement IP.</summary>
    public string? LastKnownIp { get; set; }

    /// <summary>
    /// Monnaie premium (voir GDD/demande utilisateur — "shop avec des gems, si les personnes
    /// payent avec de l'argent réel on peut obtenir des gems"). Créditée pour le moment
    /// uniquement manuellement par le Fondateur (voir <c>/givegems</c>, PlayerSession) ou par
    /// conversion de pièces (voir <see cref="PremiumService.GoldPerGemBlock"/>) — aucune vraie
    /// passerelle de paiement n'est encore branchée (voir GDD, "bloque la page pour le moment").
    /// </summary>
    public long Gems { get; set; }

    /// <summary>Palier de grade payant possédé (0 = aucun, 1 à 3 — voir <see cref="PremiumService"/>) : bonus cosmétique + petit boost XP/or. Le Fondateur a toujours le palier maximum sans dépenser de gemmes.</summary>
    public int PremiumGradeTier { get; set; }

    /// <summary>
    /// Ancien palier de pass "emplacement de personnage", séparé du grade. Voir GDD/demande
    /// utilisateur — "les grades coûtent [...] +1/+2/+5 emplacements personnage" : les
    /// emplacements de personnage font désormais partie du grade lui-même
    /// (<see cref="PremiumGradeTier"/>, voir <see cref="PremiumService.MaxCharacters"/>). Colonne
    /// conservée pour éviter une migration destructrice, mais plus lue par aucune logique.
    /// </summary>
    public int CharacterSlotTier { get; set; }

    /// <summary>
    /// Suppression douce : le compte est masqué (connexion refusée) mais conservé en base pour
    /// permettre une restauration réelle par un administrateur (voir <c>Docs/GameDesign.md</c> —
    /// "restaurer un compte").
    /// </summary>
    public bool IsDeleted { get; set; }

    public List<CharacterEntity> Characters { get; set; } = new();
}
