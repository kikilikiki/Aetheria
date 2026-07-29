namespace Aetheria.Shared.Network.Packets;

/// <summary>
/// Envoyé au DÉFIEUR quand l'adversaire accepte (voir <see cref="DuelResponsePacket"/>) — son
/// client déclenche alors <c>POST /api/pvp/challenge</c> lui-même (il connaît déjà sa propre
/// équipe ; récupère celle de l'adversaire via l'endpoint public des créatures d'un personnage).
/// </summary>
public sealed class DuelAcceptedPacket : IPacket
{
    public OpCode OpCode => OpCode.DuelAccepted;

    public required Guid OpponentCharacterId { get; init; }
    public required string OpponentCharacterName { get; init; }

    public void Write(BinaryWriter writer)
    {
        writer.Write(OpponentCharacterId.ToString());
        writer.Write(OpponentCharacterName);
    }

    public static IPacket Read(BinaryReader reader) => new DuelAcceptedPacket
    {
        OpponentCharacterId = Guid.Parse(reader.ReadString()),
        OpponentCharacterName = reader.ReadString(),
    };
}
