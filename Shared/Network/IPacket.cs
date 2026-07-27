namespace Aetheria.Shared.Network;

/// <summary>
/// Un message échangeable sur la connexion de jeu. Chaque implémentation sait s'écrire
/// elle-même ; sa lecture se fait via une méthode statique <c>Read(BinaryReader)</c>
/// enregistrée dans <see cref="PacketRegistry"/>.
/// </summary>
public interface IPacket
{
    OpCode OpCode { get; }

    void Write(BinaryWriter writer);
}
