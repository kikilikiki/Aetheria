using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models;

/// <summary>Statistique visée par une nature (voir MonsterNatureCatalog) — sert uniquement à choisir quel multiplicateur appliquer, pas de lien avec StatBlock.</summary>
public enum MonsterStatKind { Health, Attack, Defense, Speed, Intelligence, Resistance }

public static class MonsterNatureCatalog
{
    public static IReadOnlyList<MonsterNature> All { get; } =
        [MonsterNature.Neutre, MonsterNature.Fonceur, MonsterNature.Rocailleux, MonsterNature.Fulgurant, MonsterNature.Reflechi, MonsterNature.Endurant, MonsterNature.Robuste];

    public static MonsterNature RollRandom(Random random) => All[random.Next(All.Count)];

    private static readonly Dictionary<MonsterNature, (MonsterStatKind Boosted, MonsterStatKind Lowered)> Effects = new()
    {
        [MonsterNature.Fonceur] = (MonsterStatKind.Attack, MonsterStatKind.Defense),
        [MonsterNature.Rocailleux] = (MonsterStatKind.Defense, MonsterStatKind.Speed),
        [MonsterNature.Fulgurant] = (MonsterStatKind.Speed, MonsterStatKind.Intelligence),
        [MonsterNature.Reflechi] = (MonsterStatKind.Intelligence, MonsterStatKind.Resistance),
        [MonsterNature.Endurant] = (MonsterStatKind.Resistance, MonsterStatKind.Health),
        [MonsterNature.Robuste] = (MonsterStatKind.Health, MonsterStatKind.Attack),
    };

    public static float Multiplier(MonsterNature nature, MonsterStatKind stat)
    {
        if (nature == MonsterNature.Neutre || !Effects.TryGetValue(nature, out var effect))
        {
            return 1f;
        }

        if (effect.Boosted == stat) return 1.1f;
        if (effect.Lowered == stat) return 0.9f;
        return 1f;
    }

    public static string DisplayName(MonsterNature nature) => nature switch
    {
        MonsterNature.Fonceur => "Fonceur (+Attaque / -Defense)",
        MonsterNature.Rocailleux => "Rocailleux (+Defense / -Vitesse)",
        MonsterNature.Fulgurant => "Fulgurant (+Vitesse / -Intelligence)",
        MonsterNature.Reflechi => "Reflechi (+Intelligence / -Resistance)",
        MonsterNature.Endurant => "Endurant (+Resistance / -PV)",
        MonsterNature.Robuste => "Robuste (+PV / -Attaque)",
        _ => "Neutre",
    };
}
