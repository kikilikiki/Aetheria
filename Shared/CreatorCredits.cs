namespace Aetheria.Shared;

/// <summary>Fiche affichée en cliquant sur le pseudo d'un créateur du jeu (voir GDD/demande utilisateur — "quand on clique sur un pseudo on a ces informations").</summary>
public sealed record CreatorProfile(string DisplayName, string? Discord, string? Twitch, string? YouTube);

/// <summary>
/// Registre des créateurs du jeu, par pseudo (voir GDD/demande utilisateur — "crée par feelsman
/// ... et quand on clique sur un pseudo on a ces informations"). Volontairement minimal et codé
/// en dur plutôt qu'en base : l'utilisateur a indiqué donner les liens des autres membres de
/// l'équipe plus tard — ajouter une entrée ici suffit, pas de migration nécessaire.
/// </summary>
public static class CreatorCredits
{
    private static readonly Dictionary<string, CreatorProfile> ByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["feelsman"] = new CreatorProfile(
            DisplayName: "feelsman",
            Discord: "https://discord.gg/kRrZQwbG99",
            Twitch: "https://www.twitch.tv/feelsmanvt",
            YouTube: "https://www.youtube.com/@FeelsMan_YT"),
    };

    public static CreatorProfile? Find(string pseudo) => ByName.GetValueOrDefault(pseudo);
}
