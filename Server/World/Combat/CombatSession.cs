using Aetheria.Shared.Enums;

namespace Aetheria.Server.World.Combat;

/// <summary>État complet (serveur, mutable) d'un combat en cours, conservé en mémoire pour sa durée de vie.</summary>
public sealed class CombatSession
{
    public const int GridWidth = 7;
    public const int GridHeight = 7;

    public required Guid Id { get; init; }

    /// <summary>Mode PvE (contre un monstre sauvage) ou PvP (contre un autre joueur) — change les règles applicables (capture, etc.).</summary>
    public required bool IsPvp { get; init; }

    /// <summary>Vrai pour un match d'arène classée (2+ joueurs par équipe, voir <c>ArenaQueueService</c>) — change le calcul de fin de combat (ELO par équipe plutôt que le défi 1v1 direct).</summary>
    public bool IsArenaMatch { get; init; }

    /// <summary>Vrai pour un combat déclenché dans un donjon (voir GDD/demande utilisateur — "on ne peut pas fuir un combat en donjon, on peut en dehors") : bloque <see cref="Aetheria.Shared.Enums.CombatActionType.Flee"/>.</summary>
    public bool IsDungeonCombat { get; init; }

    /// <summary>Vrai pour un combat déclenché dans un donjon mythique (voir GDD/demande utilisateur — "contenu end-game... reliques uniques") : voir <c>CombatService.ApplyPveVictoryRewardsAsync</c>, récompense garantie à la victoire.</summary>
    public bool IsMythic { get; init; }

    /// <summary>Donjon d'origine (voir GDD/demande utilisateur — "les dongon normal on est 3 vie") : <c>null</c> hors combat de donjon.</summary>
    public int? DungeonId { get; init; }

    /// <summary>Voir Docs/Idees.md — table de butin dédiée aux matériaux de boss : type de salle affrontée (Monstre/MiniBoss/Boss/BossLegendaire), <c>null</c> hors combat de donjon. Renseigné par <c>CombatService.StartFromDungeonAsync</c>.</summary>
    public DungeonEncounterType? RoomEncounterType { get; set; }

    /// <summary>Vrai si le mode hardcore a été choisi pour ce combat de donjon (voir <c>StartDungeonCombatRequest.HardcoreRequested</c>) — le hardcore/mythique ne consomme pas de vie (voir <c>ApplyPveVictoryRewardsAsync</c>).</summary>
    public bool IsHardcoreCombat { get; init; }

    /// <summary>Vrai pour un combat contre le boss mondial (voir GDD/demande utilisateur — "fait que sa soit un vrai combat") : voir <c>CombatService.StartWorldBossEncounterAsync</c>/<c>ApplyWorldBossResultAsync</c>, dégâts appliqués au total de PV partagé même en cas de défaite/fuite.</summary>
    public bool IsWorldBossCombat { get; init; }

    /// <summary><c>null</c> hors combat de boss mondial. Voir <see cref="IsWorldBossCombat"/>.</summary>
    public Guid? WorldBossId { get; init; }

    /// <summary>PV réels du boss mondial au tout début de cette instance (voir <see cref="IsWorldBossCombat"/>) : sert de référence pour calculer les dégâts effectivement infligés pendant CE combat (<c>WorldBossStartingHealth - PV du combattant boss à la fin</c>), le combattant lui-même gardant son <c>MaxHealth</c> réel pour un affichage correct de la barre de vie.</summary>
    public int? WorldBossStartingHealth { get; init; }

    /// <summary>
    /// Groupe à l'origine de ce combat PvE (voir GDD/demande utilisateur — "en groupe, les 2
    /// doivent voir le même combat, pas deux combats séparés") : un membre du groupe qui engage
    /// un combat alors qu'un combat de ce groupe est déjà en cours y est ajouté directement
    /// plutôt que d'en démarrer un second isolé. <c>null</c> hors groupe ou en PvP/Arène.
    /// </summary>
    public Guid? PartyId { get; init; }

    /// <summary>Compte propriétaire de chaque équipe jouable (les monstres sauvages, contrôlés par l'IA, n'y figurent pas).</summary>
    public Dictionary<int, Guid> TeamOwnerUserId { get; init; } = [];

    /// <summary>Personnage représentant chaque équipe jouable (pour attribuer capture/récompenses/statistiques).</summary>
    public Dictionary<int, Guid> TeamCharacterId { get; init; } = [];

    public List<Combatant> Combatants { get; set; } = [];
    public int TurnIndex { get; set; }
    public bool IsFinished { get; set; }
    public int? WinningTeam { get; set; }
    public string? LastMessage { get; set; }

    /// <summary>Renseigné à chaque changement de tour (voir <c>CombatEngine.Initialize</c>/<c>AdvanceTurn</c>) : sert de référence à <see cref="Aetheria.Server.World.CombatService.AutoPassIfTimedOutAsync"/> (voir GDD/demande utilisateur — "timer de 10 secondes entre chaque tour").</summary>
    public DateTime TurnStartedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Butin de victoire PvE (voir <c>LootSessionState</c>), renseigné une seule fois sur la
    /// session elle-même plutôt que retourné seulement à l'appelant qui a porté le coup final —
    /// sinon un coéquipier qui sonde l'état après coup (voir GDD/demande utilisateur — "bloqué
    /// contre la cible, ça ne fonctionne pas") ne verrait jamais ce butin.
    /// </summary>
    public Guid? LootId { get; set; }

    /// <summary>Voir GDD/demande utilisateur — "ajoute de la lumière autour des monstre qui lvl up pour faire une petit animation" : identifiants des créatures alliées ayant gagné un niveau à la dernière action (voir CombatService.ApplyPveVictoryRewardsAsync), remis à zéro implicitement à chaque nouvel état renvoyé (une seule victoire par combat de toute façon).</summary>
    public List<Guid> LeveledUpMonsterIds { get; init; } = [];

    /// <summary>
    /// Renseigné dès que le combat se termine (voir <see cref="CombatSessionStore"/>) : la
    /// session n'est plus retirée immédiatement du store pour que tout coéquipier qui sonde
    /// encore l'état (voir GDD/demande utilisateur — combat de groupe désynchronisé) la voie
    /// bien terminée au lieu d'un combat introuvable, avant d'être purgée après un court délai.
    /// </summary>
    public DateTime? FinishedAtUtc { get; set; }

    public Combatant? CurrentCombatant => Combatants.Count == 0 ? null : Combatants[TurnIndex % Combatants.Count];

    /// <summary>
    /// Voir GDD/demande utilisateur — "cases destructibles, cases de lave, cases glacées, pièges
    /// posés sur les cases" : effets de terrain tirés au hasard par <c>CombatEngine.Initialize</c>,
    /// jamais sur une case de départ d'un combattant. Vide (grille neutre) hors PvE — voir
    /// Initialize.
    /// </summary>
    public Dictionary<(int X, int Y), TileEffect> TileEffects { get; init; } = [];

    /// <summary>PV restants d'une case Destructible (voir TileEffects) — retirée de TileEffects une fois à 0 (voir CombatEngine.ResolveAttack).</summary>
    public Dictionary<(int X, int Y), int> DestructibleHealth { get; init; } = [];

    /// <summary>
    /// Voir GDD/demande utilisateur — "Combo entre monstres", "Combo entre joueurs", "Ultime
    /// d'équipe" (voir CombatEngine.ApplyComboBonus) : cible de la chaîne de combo en cours et
    /// combattants qui l'ont déjà frappée sans interruption — réinitialisée dès qu'une cible
    /// différente est attaquée.
    /// </summary>
    public Guid? ComboTargetId { get; set; }

    public HashSet<Guid> ComboAttackerIds { get; init; } = [];
}
