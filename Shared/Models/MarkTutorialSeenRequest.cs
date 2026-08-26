namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/characters/{characterId}/mark-tutorial-seen</c> — voir Docs/Idees.md, suivi "tutoriel déjà vu".</summary>
public sealed class MarkTutorialSeenRequest
{
    public required string SessionToken { get; init; }
}
