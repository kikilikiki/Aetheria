using System.ComponentModel.DataAnnotations.Schema;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;

namespace Aetheria.Database.Entities;

/// <summary>
/// Catalogue des espèces de créatures (la "fiche" du bestiaire) — contenu géré par les
/// designers via le MonsterEditor plutôt que codé en dur, à l'image des <see cref="ItemEntity"/>.
/// Les créatures possédées par les joueurs (<see cref="MonsterEntity"/>) référencent une espèce
/// par <c>SpeciesId</c> sans clé étrangère stricte, pour ne pas lier le contenu de jeu aux
/// données de compte (voir <c>Docs/GameDesign.md</c> — section Monstres).
/// </summary>
public sealed class MonsterSpeciesEntity
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public Element Element { get; set; } = Element.Neutre;

    /// <summary>Rôle de combat (voir GDD/demande utilisateur — "ajoute des type (soigneur, guerrier, archer etc) aux monstres").</summary>
    public MonsterType Type { get; set; } = MonsterType.Guerrier;
    public Rarity BaseRarity { get; set; } = Rarity.Commun;
    public string Habitat { get; set; } = string.Empty;
    public string Lore { get; set; } = string.Empty;

    // Colonnes scalaires plutôt qu'un ComplexProperty : le fournisseur EF Core InMemory (utilisé
    // en dev sans PostgreSQL) plante sur les complex types dans certaines requêtes (bug connu).
    public int BaseHealth { get; set; }
    public int BaseAttack { get; set; }
    public int BaseDefense { get; set; }
    public int BaseSpeed { get; set; }
    public int BaseIntelligence { get; set; }
    public int BaseResistance { get; set; }

    [NotMapped]
    public StatBlock BaseStats
    {
        get => new(BaseHealth, BaseAttack, BaseDefense, BaseSpeed, BaseIntelligence, BaseResistance);
        set
        {
            BaseHealth = value.Health;
            BaseAttack = value.Attack;
            BaseDefense = value.Defense;
            BaseSpeed = value.Speed;
            BaseIntelligence = value.Intelligence;
            BaseResistance = value.Resistance;
        }
    }

    public int? EvolvesIntoSpeciesId { get; set; }
    public int EvolutionLevel { get; set; }
}
