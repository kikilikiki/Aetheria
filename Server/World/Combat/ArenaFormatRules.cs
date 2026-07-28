using Aetheria.Shared.Enums;

namespace Aetheria.Server.World.Combat;

/// <summary>
/// Règles de composition d'équipe par format d'arène (voir GDD — "1v1 = 4 unités par joueur,
/// 2v2 = 2 unités par joueur, 3v3 asymétrique, 4v4 = 1 unité par joueur"). "Unité" désigne ici les
/// créatures engagées en plus du personnage lui-même, qui est toujours présent en tant que
/// combattant séparé (voir <c>CombatService.BuildArenaTeamCombatantsAsync</c>) — cohérent avec le
/// mode Solo existant (personnage + jusqu'à 4 créatures).
/// </summary>
public static class ArenaFormatRules
{
    public static int PlayersPerTeam(ArenaFormat format) => format switch
    {
        ArenaFormat.OneVOne => 1,
        ArenaFormat.TwoVTwo => 2,
        ArenaFormat.ThreeVThree => 3,
        ArenaFormat.FourVFour => 4,
        _ => 1,
    };

    /// <summary>
    /// Nombre de créatures que peut engager le joueur d'indice <paramref name="playerIndexInTeam"/>
    /// (0-indexé, dans l'ordre d'arrivée en file d'attente) au sein de son équipe. Le 3v3 est
    /// volontairement asymétrique (2/1/1, voir GDD) plutôt qu'un partage égal impossible sur un
    /// total de 4 unités pour 3 joueurs.
    /// </summary>
    public static int UnitsForPlayerIndex(ArenaFormat format, int playerIndexInTeam) => format switch
    {
        ArenaFormat.OneVOne => 4,
        ArenaFormat.TwoVTwo => 2,
        ArenaFormat.ThreeVThree => playerIndexInTeam == 0 ? 2 : 1,
        ArenaFormat.FourVFour => 1,
        _ => 1,
    };
}
