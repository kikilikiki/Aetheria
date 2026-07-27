namespace Aetheria.Shared.Network.Packets;

/// <summary>Message de discussion, envoyé par le client ou diffusé par le serveur.</summary>
public sealed class ChatMessagePacket : IPacket
{
    public OpCode OpCode => OpCode.ChatMessage;

    public required string SenderName { get; init; }
    public required string Message { get; init; }

    public void Write(BinaryWriter writer)
    {
        writer.Write(SenderName);
        writer.Write(Message);
    }

    public static IPacket Read(BinaryReader reader) => new ChatMessagePacket
    {
        SenderName = reader.ReadString(),
        Message = reader.ReadString(),
    };
}
