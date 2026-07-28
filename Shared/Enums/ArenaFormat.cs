namespace Aetheria.Shared.Enums;

/// <summary>
/// Format d'arène classée (voir <c>Docs/GameDesign.md</c> — PvP en équipes). Détermine le nombre
/// de joueurs humains par équipe et, pour chacun, le nombre de créatures qu'il engage en plus de
/// son propre personnage — voir <c>Server/World/Combat/ArenaFormatRules</c>.
/// </summary>
public enum ArenaFormat
{
    OneVOne,
    TwoVTwo,
    ThreeVThree,
    FourVFour,
}
