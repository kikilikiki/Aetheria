using Aetheria.Database.Entities;

namespace Aetheria.Server.World;

/// <summary>
/// Voir GDD/demande utilisateur — "ajoute des iv comme sur pokémon" et "ajoute un random iv" :
/// tirage 0-31 (échelle Pokémon) par statistique, réutilisé à la fois à la capture (voir
/// <see cref="CaptureService"/>) et au reroll (voir <see cref="MonsterCareService.RerollIvAsync"/>).
/// </summary>
public static class MonsterIvRoller
{
    public const int MaxIv = 31;

    public static void RollInto(MonsterEntity monster, Random random)
    {
        monster.IvHealth = random.Next(0, MaxIv + 1);
        monster.IvAttack = random.Next(0, MaxIv + 1);
        monster.IvDefense = random.Next(0, MaxIv + 1);
        monster.IvSpeed = random.Next(0, MaxIv + 1);
        monster.IvIntelligence = random.Next(0, MaxIv + 1);
        monster.IvResistance = random.Next(0, MaxIv + 1);
    }
}
