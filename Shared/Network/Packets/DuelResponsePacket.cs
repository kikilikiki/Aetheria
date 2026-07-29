namespace Aetheria.Shared.Network.Packets;

/// <summary>Envoyé par le client défié en réponse à un <see cref="DuelInvitePacket"/> — voir GDD/demande utilisateur "demandes en duel pour le pvp".</summary>
public sealed class DuelResponsePacket : IPacket
{
    public OpCode OpCode => OpCode.DuelResponse;

    public bool Accept { get; init; }

    public void Write(BinaryWriter writer) => writer.Write(Accept);

    public static IPacket Read(BinaryReader reader) => new DuelResponsePacket { Accept = reader.ReadBoolean() };
}
