using Aetheria.Shared.Enums;

namespace Aetheria.Database.Entities;

/// <summary>Personnage jouable (table <c>Characters</c>), appartenant à un <see cref="UserEntity"/>.</summary>
public sealed class CharacterEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public UserEntity? User { get; set; }

    public required string Name { get; set; }
    public CharacterClass Class { get; set; }
    public KingdomType Kingdom { get; set; }

    public int Level { get; set; } = 1;
    public long Experience { get; set; }
    public long Gold { get; set; }

    /// <summary>Voir Docs/Idees.md — "PvP sauvage" : réputation militaire, gagnée en combattant en zone à risque (voir WildPvpQueueService), détermine le grade militaire affiché (voir MilitaryRankCatalog). Ne baisse jamais (pas de perte sur défaite), comme BestRank pour les titres PvP.</summary>
    public int MilitaryReputation { get; set; }

    // Apparence (voir GDD — création de personnage en jeu) : indices dans de petites palettes
    // fixes côté Client plutôt que des couleurs libres, pour rester cohérent avec le rendu par
    // quads colorés du moteur (pas de sprite/texture de personnage — voir Docs/README.md).
    public int SkinColorIndex { get; set; }
    public int HairStyleIndex { get; set; }
    public int HairColorIndex { get; set; }
    public int ClothesColorIndex { get; set; }
    public int AccessoryIndex { get; set; }

    // Voir GDD/demande utilisateur — "restaurer la position du joueur en quittant/revenant" :
    // dernière position connue sur la carte du monde, relue par PlayerSession.HandleEnterWorld
    // (0,0 par défaut pour un personnage qui n'a encore jamais été sauvegardé — capitale de
    // départ, comportement inchangé) et réécrite à la déconnexion (voir PlayerSession.Run).
    public int LastPositionX { get; set; }
    public int LastPositionY { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Voir GDD/demande utilisateur — "un endroit pour modifier son profil (description, item que
    // l'on veut montrer, titre, grade)" : le grade affiché est UserEntity.Rank (lecture seule,
    // pas dupliqué ici) ; ces trois champs sont les seuls réellement éditables par le joueur.
    public string ProfileDescription { get; set; } = string.Empty;
    public int? ShowcaseItemId { get; set; }

    /// <summary>Voir GDD/demande utilisateur — "des titres que l'on peut obtenir en pvp dans des classements" : doit correspondre à un <see cref="CharacterTitleEntity.TitleKey"/> déjà possédé (voir ProfileService.UpdateAsync), sinon ignoré.</summary>
    public string? ActiveTitle { get; set; }

    /// <summary>Voir GDD/demande utilisateur — "Collections : montures, ailes" : doit correspondre à une entrée possédée du compte (voir CollectionEntity/MountCatalog/WingCatalog, ProfileService.UpdateAsync), sinon ignoré. Purement déclaratif — aucun rendu visuel de monture/ailes dans le moteur (voir Docs/README.md).</summary>
    public string? ActiveMountKey { get; set; }
    public string? ActiveWingKey { get; set; }

    // Voir GDD/demande utilisateur — "ajoute des consommables pour booster la luck l'xp la money"
    // : chaque potion pose une expiration plutôt qu'un compteur, voir TemporaryBoostService.
    public DateTime? XpBoostExpiresAtUtc { get; set; }
    public DateTime? GoldBoostExpiresAtUtc { get; set; }
    public DateTime? LuckBoostExpiresAtUtc { get; set; }

    // Voir GDD/demande utilisateur — "un pass de niveaux de joueur ou chaque xp que tu gagne est
    // ajouté dedans" : progression parallèle alimentée par la même XP que Level/Experience
    // ci-dessus (voir BattlePassService.GrantExperienceAsync, appelé aux mêmes points que
    // CharacterProgressionService.GrantExperience), avec ses propres récompenses par palier.
    public long BattlePassXp { get; set; }
    public int BattlePassLevel { get; set; } = 1;

    /// <summary>Voir GDD/demande utilisateur — "si il paie le pass premium alors il auront accès à des trucs plus exclusif" : palier payant en gemmes (voir BattlePassService.PremiumCostGems), pas de vraie passerelle de paiement réel (même limite que PremiumService).</summary>
    public bool BattlePassHasPremium { get; set; }

    /// <summary>Dernier palier pour lequel la récompense premium a déjà été distribuée — sert au rattrapage rétroactif si le pass premium est acheté après avoir déjà progressé (voir BattlePassService.PurchasePremiumAsync).</summary>
    public int BattlePassLastPremiumRewardLevel { get; set; }

    /// <summary>Voir Docs/Idees.md — suivi "tutoriel déjà vu" : mis à `true` à la fermeture du tutoriel (F1, voir Client/Program.cs UpdateTutorial), déclenche son affichage automatique une seule fois juste après la création de personnage tant que c'est encore `false`.</summary>
    public bool HasSeenTutorial { get; set; }

    public List<MonsterEntity> Monsters { get; set; } = new();
    public List<InventoryItemEntity> InventoryItems { get; set; } = new();
    public StatisticsEntity? Statistics { get; set; }
    public List<CharacterTitleEntity> Titles { get; set; } = new();

    // Voir retour utilisateur — "la couveuse doit ajouter un temps et une validation avant de le
    // faire (fait pareil pour la fusion) plus le monstre que l'on obtient apres fusion/reproduction
    // plus sa prendra de temps" : un seul slot en attente à la fois (voir FusionService), la
    // créature résultante est déjà déterminée au lancement (voir StartAsync) pour que la durée
    // reflète sa force réelle plutôt que d'être devinée à l'avance.
    public Guid? PendingFusionSurvivorId { get; set; }
    public Guid? PendingFusionConsumedId { get; set; }
    public DateTime? PendingFusionCompletesAtUtc { get; set; }

    /// <summary>Voir retour utilisateur — "ajoute un cooldown si une personne a reproduit (pas qu'ils se reproduisent direct)" : espèce/variante/passif du bébé tirés au lancement (voir BreedingService.StartAsync), pas à la récupération — la durée d'attente en dépend.</summary>
    public Guid? PendingBreedParentId1 { get; set; }
    public Guid? PendingBreedParentId2 { get; set; }
    public int? PendingBreedOffspringSpeciesId { get; set; }
    public MonsterVariant? PendingBreedOffspringVariant { get; set; }
    public string? PendingBreedOffspringPassiveTalent { get; set; }
    public DateTime? PendingBreedCompletesAtUtc { get; set; }
    public DateTime? NextBreedAllowedAtUtc { get; set; }
}
