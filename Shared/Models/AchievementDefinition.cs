using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models;

/// <summary>Définition statique d'un succès. Le déblocage effectif est suivi côté serveur par clé.</summary>
public sealed record AchievementDefinition(
    string Key,
    string Name,
    string Description,
    AchievementCategory Category,
    /// <summary>Voir GDD/demande utilisateur — "Succès cachés" : nom/description à masquer côté Client tant qu'il n'est pas débloqué (voir GetUnlockedKeysAsync).</summary>
    bool IsHidden = false);
