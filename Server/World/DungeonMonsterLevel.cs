using Aetheria.Database.Entities;
using Aetheria.Shared.Models;

namespace Aetheria.Server.World;

/// <summary>
/// Voir GDD/demande utilisateur — CORRECTION : "le niveau requis pour aller en donjon c'est le
/// niveau des monstres, pas celui du personnage". Niveau des monstres rencontrés à un étage donné,
/// interpolé linéairement entre <see cref="DungeonEntity.MinLevel"/> (étage 1) et
/// <see cref="DungeonEntity.MaxMonsterLevel"/> (dernier étage, voir DungeonProgression.MaxFloor).
/// </summary>
public static class DungeonMonsterLevel
{
    public static int ForFloor(DungeonEntity dungeon, int floorNumber)
    {
        var minLevel = Math.Max(1, dungeon.MinLevel);
        var maxLevel = Math.Max(minLevel, dungeon.MaxMonsterLevel);
        if (DungeonProgression.MaxFloor <= 1)
        {
            return minLevel;
        }

        var progress = Math.Clamp(floorNumber - 1, 0, DungeonProgression.MaxFloor - 1);
        var level = minLevel + (maxLevel - minLevel) * progress / (DungeonProgression.MaxFloor - 1);
        return Math.Clamp(level, 1, MonsterProgressionService.MaxLevel);
    }
}
