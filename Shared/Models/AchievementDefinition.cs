using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models;

/// <summary>Définition statique d'un succès. Le déblocage effectif est suivi côté serveur par clé.</summary>
public sealed record AchievementDefinition(string Key, string Name, string Description, AchievementCategory Category);
