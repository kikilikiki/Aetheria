namespace Aetheria.Shared.Network.Packets;

/// <summary>Envoyé à tous les participants sauf celui qui a démarré le combat (voir <see cref="TeamDuelReadyPacket"/>) — leur client n'a que l'ID du combat à récupérer via <c>GET /api/combat/{id}</c>, comme un appairage d'arène.</summary>
public sealed class DuelStartedPacket : IPacket
{
    public OpCode OpCode => OpCode.DuelStarted;

    public required Guid CombatId { get; init; }

    public void Write(BinaryWriter writer) => writer.Write(CombatId.ToString());

    public static IPacket Read(BinaryReader reader) => new DuelStartedPacket { CombatId = Guid.Parse(reader.ReadString()) };
}
