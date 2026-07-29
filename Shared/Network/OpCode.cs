namespace Aetheria.Shared.Network;

/// <summary>
/// Identifiant de type de packet échangé sur la connexion TCP de jeu (Client ↔ Server).
/// Le compte (inscription/connexion) transite par une API HTTP séparée — voir
/// <c>Docs/README.md</c> — ces packets ne concernent que la session de jeu une fois connecté.
/// </summary>
public enum OpCode : byte
{
    Ping = 1,
    Pong = 2,

    EnterWorldRequest = 10,
    EnterWorldAccepted = 11,
    EnterWorldRejected = 12,

    PlayerMove = 20,
    PlayerPositionUpdate = 21,
    PlayerJoined = 22,
    PlayerLeft = 23,

    ChatMessage = 30,

    AdminEffect = 40,

    // Voir GDD/demande utilisateur — "ajouter les demandes en duel pour le pvp".
    DuelInvite = 50,
    DuelResponse = 51,
    DuelAccepted = 52,
    DuelStarted = 53,
}
