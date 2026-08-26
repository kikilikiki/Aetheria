using Aetheria.Shared.Enums;

namespace Aetheria.Server.World.Combat;

/// <summary>
/// Représentation interne (mutable) d'un combattant pendant la résolution d'un combat.
/// N'est jamais exposée telle quelle au client — voir <see cref="Aetheria.Shared.Models.Combat.CombatantState"/>.
/// </summary>
public sealed class Combatant
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required int Team { get; init; }
    public int X { get; set; }
    public int Y { get; set; }
    public required int MaxHealth { get; init; }
    public int CurrentHealth { get; set; }
    public required int Attack { get; init; }
    public required int Defense { get; init; }
    public required int Speed { get; init; }
    public required int MovementRange { get; init; }
    public required int AttackRange { get; init; }
    public bool IsPlayerControlled { get; init; }

    /// <summary>Rôle de combat (voir GDD — types de monstres) : détermine la capacité spéciale et la couleur affichée côté client.</summary>
    public MonsterType Type { get; init; } = MonsterType.Guerrier;

    /// <summary>Élément (voir GDD — avantages/faiblesses de type) : Neutre pour le personnage joueur.</summary>
    public Element Element { get; init; } = Element.Neutre;

    /// <summary>Voir GDD/demande utilisateur — variantes de créature (voir MonsterVariantCatalog) : bonus de statistiques déjà appliqué à Attack/Defense/Speed/MaxHealth ci-dessus, conservée ici pour l'affichage et pour être reportée sur la créature capturée (voir ResolveCaptureAsync).</summary>
    public MonsterVariant Variant { get; init; } = MonsterVariant.Normal;

    /// <summary>Voir GDD/demande utilisateur — "Compétences passives" (voir PassiveTalentCatalog, effets appliqués dans CombatEngine) : vide pour un combattant sans passif (monstres sauvages).</summary>
    public string PassiveTalent { get; init; } = string.Empty;

    /// <summary>Voir GDD/demande utilisateur — "Competences ultimes debloquees au niveau max" : niveau réel de la créature (1 pour un monstre sauvage, sans notion de niveau propre), voir CombatEngine.ResolveSpecialAbility.</summary>
    public int Level { get; init; } = 1;

    /// <summary>
    /// Espèce d'origine (monstre sauvage ou créature possédée), <c>null</c> pour un combattant
    /// sans espèce. Utilisé par <c>ResolveCaptureAsync</c> pour retrouver l'espèce de façon
    /// fiable — <see cref="Name"/> seul ne suffit plus depuis que plusieurs ennemis sauvages
    /// partagent la même espèce avec un nom numéroté ("Braisillon 1", "Braisillon 2", ...).
    /// </summary>
    public int? SpeciesId { get; init; }

    /// <summary>
    /// Joueur propriétaire de ce combattant précis — <c>null</c> pour les monstres sauvages
    /// contrôlés par l'IA. Utilisé pour l'autorisation d'action (voir
    /// <c>CombatService.SubmitActionAsync</c>) : contrairement à <see cref="Team"/>, permet à
    /// plusieurs joueurs de partager la même équipe (arènes 2v2/3v3/4v4, voir GDD).
    /// </summary>
    public Guid? OwnerUserId { get; init; }
    public Guid? OwnerCharacterId { get; init; }

    public bool IsAlive => CurrentHealth > 0;

    /// <summary>
    /// Nombre de tours (propres à ce combattant) restants avant de pouvoir réutiliser sa capacité
    /// spéciale (voir GDD/demande utilisateur — "ajoute un cooldown pour le spécial") : décrémenté
    /// à chaque fois que c'est de nouveau son tour (<see cref="CombatEngine.AdvanceTurn"/>).
    /// </summary>
    public int SpecialAbilityCooldownRemaining { get; set; }

    /// <summary>
    /// Voir GDD/demande utilisateur — "il doit y avoir l'attaque spéciale ... en plus de
    /// l'attaque ultime si le monstre est lvl max" : cooldown séparé de
    /// <see cref="SpecialAbilityCooldownRemaining"/>, pour que l'Ultime (voir
    /// <c>CombatEngine.ResolveUltimateAbility</c>) reste une action à part entière plutôt qu'un
    /// simple remplacement de la capacité spéciale normale au niveau max.
    /// </summary>
    public int UltimateAbilityCooldownRemaining { get; set; }

    /// <summary>
    /// Voir Docs/Idees.md — capacité spéciale du Support : bonus de dégâts en attente, consommé
    /// (remis à 0) par la prochaine <see cref="CombatEngine.ResolveAttack"/> de ce combattant,
    /// quel qu'en soit l'auteur du buff. 0 = aucun bonus actif.
    /// </summary>
    public int NextAttackBonusAmount { get; set; }
}
