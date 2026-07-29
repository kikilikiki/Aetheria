using System.ComponentModel.DataAnnotations.Schema;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;

namespace Aetheria.Database.Entities;

/// <summary>
/// Catalogue des objets du jeu (table <c>Items</c>) — contenu géré par les designers via
/// l'AdminPanel/MapEditor plutôt que codé en dur, pour pouvoir ajouter des objets sans recompiler.
/// </summary>
public sealed class ItemEntity
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string Description { get; set; } = string.Empty;
    public ItemType ItemType { get; set; }
    public Rarity Rarity { get; set; } = Rarity.Commun;

    // Colonnes scalaires plutôt qu'un ComplexProperty : le fournisseur EF Core InMemory (utilisé
    // en dev sans PostgreSQL) plante sur les complex types dans certaines requêtes (bug connu).
    public int StatBonusHealth { get; set; }
    public int StatBonusAttack { get; set; }
    public int StatBonusDefense { get; set; }
    public int StatBonusSpeed { get; set; }
    public int StatBonusIntelligence { get; set; }
    public int StatBonusResistance { get; set; }

    [NotMapped]
    public StatBlock StatBonus
    {
        get => new(StatBonusHealth, StatBonusAttack, StatBonusDefense, StatBonusSpeed, StatBonusIntelligence, StatBonusResistance);
        set
        {
            StatBonusHealth = value.Health;
            StatBonusAttack = value.Attack;
            StatBonusDefense = value.Defense;
            StatBonusSpeed = value.Speed;
            StatBonusIntelligence = value.Intelligence;
            StatBonusResistance = value.Resistance;
        }
    }

    public bool IsStackable { get; set; } = true;
    public int MaxStackSize { get; set; } = 99;

    /// <summary>Prix en or à la Boutique (voir GDD) ; 0 = non vendu en boutique.</summary>
    public int Price { get; set; }

    /// <summary>
    /// Voir GDD/demande utilisateur — "ajoute des objets que l'on ne peut pas obtenir ni
    /// fabriquer" : exclu du tirage de butin de combat (voir <c>LootService</c>) même si son prix
    /// est à 0 (le prix à 0 exclut déjà de la Boutique, mais pas du butin aléatoire). Reste
    /// distribuable manuellement via le panel admin ("DONNER UN OBJET"), qui ignore ce champ.
    /// </summary>
    public bool IsObtainable { get; set; } = true;
}
