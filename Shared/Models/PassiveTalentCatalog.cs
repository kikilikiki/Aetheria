namespace Aetheria.Shared.Models;

/// <summary>
/// Compétences passives des créatures (voir GDD/demande utilisateur — "Compétences passives") :
/// un effet automatique, toujours actif en combat, tiré au hasard à la capture/naissance d'une
/// créature (voir CaptureService, BreedingService, StarterService) et conservé pour toujours —
/// contrairement à la capacité spéciale (active, par rôle, voir CombatEngine.ResolveSpecialAbility),
/// c'est propre à CHAQUE créature individuellement. Effets appliqués dans
/// <c>Server/World/Combat/CombatEngine.cs</c> par correspondance de nom (catalogue volontairement
/// petit et fixe plutôt qu'un système de données/scripts).
/// </summary>
public static class PassiveTalentCatalog
{
    public const string Regeneration = "Régénération";
    public const string VolDeVie = "Vol de vie";
    public const string Acharnement = "Acharnement";
    public const string Bouclier = "Bouclier";
    public const string ContreAttaque = "Contre-attaque";

    public static IReadOnlyList<string> All { get; } = [Regeneration, VolDeVie, Acharnement, Bouclier, ContreAttaque];

    public static string RollRandom(Random random) => All[random.Next(All.Count)];

    public static string Describe(string talent) => talent switch
    {
        Regeneration => "Récupère 5% de ses PV max au début de chacun de ses tours.",
        VolDeVie => "Récupère 20% des dégâts infligés en attaquant.",
        Acharnement => "+30% de dégâts infligés quand ses PV passent sous 30%.",
        Bouclier => "Réduit les dégâts subis de 15%.",
        ContreAttaque => "20% de chances de renvoyer la moitié des dégâts subis à l'attaquant.",
        _ => "",
    };
}
