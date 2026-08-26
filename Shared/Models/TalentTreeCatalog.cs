namespace Aetheria.Shared.Models;

/// <summary>
/// Voir Docs/Idees.md — "Arbre de talents/compétences général" : un nœud accorde un bonus passif
/// en pourcentage sur une statistique (ou sur toutes, pour le nœud de maîtrise final), débloqué
/// avec des points gagnés par montée de niveau (voir <c>MonsterProgressionService.GrantExperience</c>,
/// <c>+1 par niveau</c>). Volontairement un seul arbre partagé par toutes les créatures plutôt
/// qu'un arbre par espèce (impossible à équilibrer à la main pour ~80 espèces) — voir
/// <c>Server/World/MonsterTalentService.cs</c> pour le déblocage.
/// </summary>
public sealed record TalentNode(string Key, string Name, string Description, MonsterStatKind? Stat, float BonusPercent, string[] Requires);

public static class TalentTreeCatalog
{
    public static readonly IReadOnlyList<TalentNode> Nodes =
    [
        new("atk1", "Force I", "+5% Attaque", MonsterStatKind.Attack, 0.05f, []),
        new("atk2", "Force II", "+10% Attaque supplémentaires", MonsterStatKind.Attack, 0.10f, ["atk1"]),
        new("def1", "Cuirasse I", "+5% Défense", MonsterStatKind.Defense, 0.05f, []),
        new("def2", "Cuirasse II", "+10% Défense supplémentaires", MonsterStatKind.Defense, 0.10f, ["def1"]),
        new("hp1", "Vitalité I", "+5% PV maximum", MonsterStatKind.Health, 0.05f, []),
        new("hp2", "Vitalité II", "+10% PV maximum supplémentaires", MonsterStatKind.Health, 0.10f, ["hp1"]),
        new("spd1", "Célérité I", "+5% Vitesse", MonsterStatKind.Speed, 0.05f, []),
        new("spd2", "Célérité II", "+10% Vitesse supplémentaires", MonsterStatKind.Speed, 0.10f, ["spd1"]),
        new("mastery", "Maîtrise", "+5% à toutes les statistiques (nœud final)", null, 0.05f, ["atk2", "def2", "hp2", "spd2"]),
    ];

    public static TalentNode? Find(string key) => Nodes.FirstOrDefault(n => n.Key == key);

    public static HashSet<string> ParseUnlocked(string raw) =>
        raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();

    /// <summary>Somme des bonus (en pourcentage, ex. 0.15 = +15%) des nœuds débloqués s'appliquant à <paramref name="stat"/> — le nœud de maîtrise (<c>Stat == null</c>) s'applique à toutes les statistiques.</summary>
    public static float TotalBonus(string rawUnlockedKeys, MonsterStatKind stat)
    {
        var unlocked = ParseUnlocked(rawUnlockedKeys);
        var total = 0f;
        foreach (var node in Nodes)
        {
            if (unlocked.Contains(node.Key) && (node.Stat is null || node.Stat == stat))
            {
                total += node.BonusPercent;
            }
        }

        return total;
    }
}
