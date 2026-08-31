namespace Aetheria.Shared.Network.Packets;

/// <summary>Type d'effet admin diffusé à tous les joueurs (voir GDD/demande utilisateur — "panel admin abuse").</summary>
public enum AdminEffectKind : byte
{
    /// <summary>Message affiché en grand en haut de l'écran de tous les joueurs quelques secondes.</summary>
    Broadcast = 0,

    /// <summary>Transforme l'apparence de tous les joueurs en panneau pendant <see cref="AdminEffectPacket.DurationSeconds"/>.</summary>
    SignMode = 1,

    /// <summary>
    /// Voir demande utilisateur — "message entre modos pour toutes les actions" et "message à la
    /// personne qui a reçu un monstre/une modification, que lui et les modos voient" : bannière
    /// secondaire, discrète, affichée uniquement aux destinataires choisis par le serveur (le
    /// serveur restreint l'audience via WorldSessionRegistry.SendToStaff, pas le Client).
    /// </summary>
    StaffNotice = 2,
}

/// <summary>
/// Diffusé par le serveur à tous les joueurs connectés suite à une action du panel admin en jeu
/// (voir GDD/demande utilisateur — "ils peuvent afficher un message en haut de l'écran en gros à
/// tous les joueurs [...] transformer le skin de tout les joueurs en panneau [...] pendant 5min").
/// </summary>
public sealed class AdminEffectPacket : IPacket
{
    public OpCode OpCode => OpCode.AdminEffect;

    public AdminEffectKind Kind { get; init; }
    public string Message { get; init; } = string.Empty;
    public int DurationSeconds { get; init; }

    public void Write(BinaryWriter writer)
    {
        writer.Write((byte)Kind);
        writer.Write(Message);
        writer.Write(DurationSeconds);
    }

    public static IPacket Read(BinaryReader reader) => new AdminEffectPacket
    {
        Kind = (AdminEffectKind)reader.ReadByte(),
        Message = reader.ReadString(),
        DurationSeconds = reader.ReadInt32(),
    };
}
