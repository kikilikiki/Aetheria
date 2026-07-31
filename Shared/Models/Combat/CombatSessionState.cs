namespace Aetheria.Shared.Models.Combat;

/// <summary>Photographie complète d'un combat en cours, renvoyée après chaque action.</summary>
public sealed record CombatSessionState(
    Guid CombatId,
    int GridWidth,
    int GridHeight,
    IReadOnlyList<CombatantState> Combatants,
    Guid? CurrentTurnCombatantId,
    bool IsFinished,
    int? WinningTeam,
    string? LastMessage,
    /// <summary>Renseigné uniquement à la victoire en PvE (voir GDD — butin de 4 objets) — voir <c>LootSessionState</c>.</summary>
    Guid? LootId = null,
    /// <summary>
    /// Vrai pour un combat déclenché dans un donjon (voir GDD/demande utilisateur — "impossible
    /// de fuir un combat en donjon") : source de vérité pour l'affichage du bouton Fuir côté
    /// client, plutôt que l'état de scène local du client (qui peut rester périmé après être
    /// retourné à l'extérieur — voir <c>Client/Program.cs</c>, <c>interiorIsDungeon</c>).
    /// </summary>
    bool IsDungeonCombat = false,
    /// <summary>
    /// Début du tour courant (UTC) — voir GDD/demande utilisateur : "timer de 10 secondes entre
    /// chaque tour" (<see cref="Aetheria.Shared.GameInfo.CombatTurnTimeoutSeconds"/>). Le client
    /// affiche un compte à rebours à partir de cette valeur ; le serveur fait foi et passe le
    /// tour automatiquement au-delà de ce délai (voir <c>CombatTimeoutScheduler</c>).
    /// </summary>
    DateTime TurnStartedAtUtc = default,
    /// <summary>Voir GDD/demande utilisateur — "pièges, cases destructibles, cases de lave, cases glacées" (voir CombatEngine.ScatterTileEffects) : vide en PvP/Arène.</summary>
    IReadOnlyList<TileEffectEntry>? TileEffects = null,
    /// <summary>Voir GDD/demande utilisateur — "ajoute de la lumière autour des monstre qui lvl up pour faire une petit animation" : identifiants des créatures alliées ayant gagné un niveau lors de la dernière action, pour déclencher une animation client-side (voir DrawCombatPanel).</summary>
    IReadOnlyList<Guid>? LeveledUpMonsterIds = null);
