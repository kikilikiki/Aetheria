using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models;

/// <summary>Voir Docs/Idees.md — un message de l'historique de tchat persisté, renvoyé par <c>GET /api/chat/history</c>.</summary>
public sealed class ChatHistoryMessage
{
    public required string SenderName { get; init; }
    public required UserRank SenderRank { get; init; }
    public required string Message { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}
