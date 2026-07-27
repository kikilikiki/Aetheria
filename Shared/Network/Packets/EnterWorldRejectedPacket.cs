namespace Aetheria.Shared.Network.Packets;

/// <summary>Refus d'entrée dans le monde (token invalide/expiré, personnage inconnu, ...).</summary>
public sealed class EnterWorldRejectedPacket : IPacket
{
    public OpCode OpCode => OpCode.EnterWorldRejected;

    public required string Reason { get; init; }

    public void Write(BinaryWriter writer) => writer.Write(Reason);

    public static IPacket Read(BinaryReader reader) => new EnterWorldRejectedPacket
    {
        Reason = reader.ReadString(),
    };
}
