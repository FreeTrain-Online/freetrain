using System.Runtime.CompilerServices;
using System.Text.Json;
using FreeTrain.Online.Contracts;

namespace FreeTrain.Online.Server;

// Queue entries share their encoding across the admission check and socket
// workers. Weak ownership releases bytes when the bounded queues drain.
public static class OutboundWire
{
    public const int MaximumBytes = 16 * 1024 * 1024;
    private sealed record Envelope(byte[] Bytes);
    private static readonly ConditionalWeakTable<WireMessage, Envelope> envelopes = new();
    public static ReadOnlyMemory<byte> Serialize(WireMessage message)
    {
        if (message is JoinMessage or SnapshotMessage)
            throw new ArgumentException("Full states use the coherent state-transfer writer.", nameof(message));
        return envelopes.GetValue(message, Encode).Bytes;
    }
    private static Envelope Encode(WireMessage message)
    {
        using var stream = new BoundedStream();
        JsonSerializer.Serialize<WireMessage>(stream, message, NetworkSession.Json);
        return new(stream.ToArray());
    }
    private sealed class BoundedStream : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (Length + count > MaximumBytes) throw new InvalidDataException("Outbound message exceeds the delta limit.");
            base.Write(buffer, offset, count);
        }
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (Length + buffer.Length > MaximumBytes) throw new InvalidDataException("Outbound message exceeds the delta limit.");
            base.Write(buffer);
        }
    }
}
