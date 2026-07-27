namespace Aetheria.Shared.Network.Packets;

/// <summary>
/// Demande d'entrée dans le monde persistant, envoyée juste après l'établissement de la
/// connexion TCP. <see cref="SessionToken"/> provient de l'API de compte (voir Server/Networking).
/// </summary>
public sealed class EnterWorldRequestPacket : IPacket
{
    public OpCode OpCode => OpCode.EnterWorldRequest;

    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }

    public void Write(BinaryWriter writer)
    {
        writer.Write(SessionToken);
        writer.Write(CharacterId.ToByteArray());
    }

    public static IPacket Read(BinaryReader reader) => new EnterWorldRequestPacket
    {
        SessionToken = reader.ReadString(),
        CharacterId = new Guid(reader.ReadBytes(16)),
    };
}
