using Aetheria.Shared.Enums;

namespace Aetheria.Database.Entities;

/// <summary>
/// Créature possédée par un joueur (table <c>Monsters</c>). <see cref="SpeciesId"/> référence
/// une <see cref="Aetheria.Shared.Models.MonsterSpeciesData"/> du catalogue de contenu
/// (statique, hors base de données — voir MonsterEditor).
/// </summary>
public sealed class MonsterEntity
{
    public Guid Id { get; set; }

    public Guid OwnerCharacterId { get; set; }
    public CharacterEntity? OwnerCharacter { get; set; }

    public int SpeciesId { get; set; }
    public MonsterVariant Variant { get; set; } = MonsterVariant.Normal;

    public string Nickname { get; set; } = string.Empty;
    public int Level { get; set; } = 1;
    public int Experience { get; set; }
    public string Personality { get; set; } = string.Empty;
    public string PassiveTalent { get; set; } = string.Empty;

    // Voir GDD/demande utilisateur — "Talents/capacités passives uniques par monstre (comme les
    // 'natures' Pokémon, influençant les stats)" : tirée une seule fois à la capture/naissance
    // (voir MonsterNatureCatalog.RollRandom), appliquée dans MonsterStatMath.
    public MonsterNature Nature { get; set; } = MonsterNature.Neutre;

    /// <summary>
    /// Emplacement d'équipe (0-3, 4 créatures maximum combattent — voir GDD), <c>null</c> si non
    /// équipée (à la pension). Voir GDD/demande utilisateur — "on doit équiper les monstres au
    /// lieu de juste les mettre avec soi via la pension" : remplace l'ancien booléen
    /// <c>IsInActiveTeam</c> par un vrai numéro d'emplacement, assigné automatiquement au premier
    /// libre par <see cref="Server.World.MonsterCareService.SetEquippedAsync"/>.
    /// </summary>
    public int? EquippedSlot { get; set; }

    // Voir GDD/demande utilisateur — "si les items équipés peuvent donner des avantages à nos
    // monstres (exemple : une épée en fer donne plus de dégâts)" : un objet équipé est retiré de
    // l'inventaire (voir MonsterEquipmentService) tant qu'il reste équipé, et rendu à l'inventaire
    // au déséquipement — pas une simple référence non-exclusive.
    public int? EquippedWeaponItemId { get; set; }
    public int? EquippedArmorItemId { get; set; }
    public int? EquippedAccessoryItemId { get; set; }

    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Voir GDD/demande utilisateur — "Prestige après niveau maximum" : incrémenté par PrestigeService, remet Level/Experience à zéro contre un bonus de statistiques permanent (voir MonsterStatMath).</summary>
    public int PrestigeLevel { get; set; }

    /// <summary>Voir Docs/Idees.md — Arbre de talents : +1 par niveau gagné (voir MonsterProgressionService.GrantExperience), dépensé via MonsterTalentService.</summary>
    public int TalentPoints { get; set; }

    /// <summary>Voir Docs/Idees.md — Arbre de talents : clés de nœuds débloqués (voir TalentTreeCatalog), séparées par des virgules — pas de table de jointure séparée pour un arbre aussi petit (~10 nœuds).</summary>
    public string UnlockedTalentNodeKeys { get; set; } = string.Empty;

    // Voir GDD/demande utilisateur — "ajoute des iv comme sur pokémon" : tirées une seule fois à
    // la capture (voir CaptureService), 0-31, ne changent plus jamais ensuite (même après un
    // prestige — voir PrestigeService) sauf reroll explicite (voir MonsterCareService.RerollIvAsync).
    public int IvHealth { get; set; }
    public int IvAttack { get; set; }
    public int IvDefense { get; set; }
    public int IvSpeed { get; set; }
    public int IvIntelligence { get; set; }
    public int IvResistance { get; set; }

    // Voir GDD/demande utilisateur — "ajoute aussi des ev" : accumulées en combat (voir
    // CombatService.ApplyPveVictoryRewardsAsync), plafonnées à 252 par statistique.
    public int EvHealth { get; set; }
    public int EvAttack { get; set; }
    public int EvDefense { get; set; }
    public int EvSpeed { get; set; }
    public int EvIntelligence { get; set; }
    public int EvResistance { get; set; }

    // Voir GDD/demande utilisateur — "après un prestige ajoute un nouveau champ que on va
    // appelé prest ou a chaque prestige l'un des [...] champs augmentera" : +1 permanent sur une
    // statistique tirée au hasard à chaque prestige (voir PrestigeService), cumulatif, jamais
    // remis à zéro.
    public int PrestHealth { get; set; }
    public int PrestAttack { get; set; }
    public int PrestDefense { get; set; }
    public int PrestSpeed { get; set; }
    public int PrestIntelligence { get; set; }
    public int PrestResistance { get; set; }
}
