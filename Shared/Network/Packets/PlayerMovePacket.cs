namespace Aetheria.Shared.Network.Packets;

/// <summary>Demande de déplacement du client vers une case cible de la grille.</summary>
public sealed class PlayerMovePacket : IPacket
{
    public OpCode OpCode => OpCode.PlayerMove;

    public required int TargetX { get; init; }
    public required int TargetY { get; init; }

    public void Write(BinaryWriter writer)
    {
        writer.Write(TargetX);
        writer.Write(TargetY);
    }

    public static IPacket Read(BinaryReader reader) => new PlayerMovePacket
    {
        TargetX = reader.ReadInt32(),
        TargetY = reader.ReadInt32(),
    };
}
