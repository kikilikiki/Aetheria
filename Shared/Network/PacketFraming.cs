using System.Text;

namespace Aetheria.Shared.Network;

/// <summary>
/// Sérialise/désérialise des <see cref="IPacket"/> sur un flux TCP en utilisant un
/// framing longueur-préfixée : [ int32 longueur ][ byte opcode ][ payload ].
/// </summary>
public static class PacketFraming
{
    public static void WritePacket(Stream stream, IPacket packet)
    {
        using var payloadStream = new MemoryStream();
        using (var payloadWriter = new BinaryWriter(payloadStream, Encoding.UTF8, leaveOpen: true))
        {
            payloadWriter.Write((byte)packet.OpCode);
            packet.Write(payloadWriter);
        }

        var payload = payloadStream.ToArray();

        using var frameWriter = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        frameWriter.Write(payload.Length);
        frameWriter.Write(payload);
        frameWriter.Flush();
    }

    /// <summary>Lit un packet complet depuis <paramref name="stream"/>, ou <c>null</c> si le flux est fermé.</summary>
    public static IPacket? ReadPacket(Stream stream)
    {
        Span<byte> lengthBuffer = stackalloc byte[4];
        if (!ReadExact(stream, lengthBuffer))
        {
            return null;
        }

        var length = BitConverter.ToInt32(lengthBuffer);
        var payload = new byte[length];
        if (!ReadExact(stream, payload))
        {
            return null;
        }

        using var payloadStream = new MemoryStream(payload);
        using var reader = new BinaryReader(payloadStream, Encoding.UTF8);
        var opCode = (OpCode)reader.ReadByte();
        return PacketRegistry.Read(opCode, reader);
    }

    private static bool ReadExact(Stream stream, Span<byte> buffer)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = stream.Read(buffer[totalRead..]);
            if (read == 0)
            {
                return false;
            }

            totalRead += read;
        }

        return true;
    }
}
