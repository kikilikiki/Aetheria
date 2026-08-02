using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models;

/// <summary>
/// Créature effectivement possédée par un joueur : une instance capturée d'une
/// <see cref="MonsterSpeciesData"/>, avec sa propre progression.
/// </summary>
public sealed class MonsterInstanceData
{
    public required Guid Id { get; init; }
    public required int SpeciesId { get; init; }
    public required Guid OwnerCharacterId { get; init; }

    public MonsterVariant Variant { get; set; } = MonsterVariant.Normal;
    public string Nickname { get; set; } = string.Empty;
    public int Level { get; set; } = 1;
    public int Experience { get; set; }
    public string Personality { get; set; } = string.Empty;
    public string PassiveTalent { get; set; } = string.Empty;

    /// <summary>Voir GDD/demande utilisateur — "Talents/capacités passives uniques par monstre (comme les 'natures' Pokémon, influençant les stats)" : tirée une seule fois à la capture.</summary>
    public MonsterNature Nature { get; set; } = MonsterNature.Neutre;

    /// <summary>Emplacement d'équipe (0-3), <c>null</c> si non équipée/à la pension — voir <c>MonsterEntity.EquippedSlot</c>.</summary>
    public int? EquippedSlot { get; set; }

    // Voir GDD/demande utilisateur — équipement donnant des bonus de stats en combat (voir
    // MonsterEquipmentService/CombatService.BuildTeamCombatantsAsync).
    public int? EquippedWeaponItemId { get; set; }
    public string? EquippedWeaponName { get; set; }
    public int? EquippedArmorItemId { get; set; }
    public string? EquippedArmorName { get; set; }
    public int? EquippedAccessoryItemId { get; set; }
    public string? EquippedAccessoryName { get; set; }

    public DateTime CapturedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>Voir GDD/demande utilisateur — "Prestige après niveau maximum".</summary>
    public int PrestigeLevel { get; set; }

    /// <summary>Voir GDD/demande utilisateur — "ajoute des iv comme sur pokémon" et "fait en sorte que dans le detail du pokemon on puisse les voir" : tirées une seule fois à la capture, 0-31.</summary>
    public int IvHealth { get; set; }
    public int IvAttack { get; set; }
    public int IvDefense { get; set; }
    public int IvSpeed { get; set; }
    public int IvIntelligence { get; set; }
    public int IvResistance { get; set; }

    /// <summary>Voir GDD/demande utilisateur — "ajoute aussi des ev" : accumulées en combat, plafonnées à 252 par statistique.</summary>
    public int EvHealth { get; set; }
    public int EvAttack { get; set; }
    public int EvDefense { get; set; }
    public int EvSpeed { get; set; }
    public int EvIntelligence { get; set; }
    public int EvResistance { get; set; }

    /// <summary>Voir GDD/demande utilisateur — "après un prestige ajoute un nouveau champ que on va appelé prest" : +1 permanent sur une statistique tirée au hasard à chaque prestige.</summary>
    public int PrestHealth { get; set; }
    public int PrestAttack { get; set; }
    public int PrestDefense { get; set; }
    public int PrestSpeed { get; set; }
    public int PrestIntelligence { get; set; }
    public int PrestResistance { get; set; }
}
