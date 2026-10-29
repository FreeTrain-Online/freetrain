using System.Security.Cryptography;
using System.Text.Json;
using System.Runtime.CompilerServices;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;

namespace FreeTrain.Online.Server;

// One transfer is written to a socket at a time. Follow-up state remains in its
// existing bounded output queue; slow consumers disconnect and re-bootstrap.
public static class StateTransfer
{
    public const int InlineBytes = 256 * 1024;
    private static readonly ConditionalWeakTable<WireMessage, byte[]> payloads = new();
    public static byte[] Serialize(WireMessage message)
        => payloads.GetValue(message, SerializeCore);
    private static byte[] SerializeCore(WireMessage message)
    {
        var snapshot = message switch { JoinMessage j => j.Snapshot, SnapshotMessage s => s, _ => null };
        PackedTerrainRead? packed = null;
        if (snapshot?.Terrain is not null)
        {
            packed = PackedTerrain.Capture(snapshot);
            var header = snapshot with { Terrain = null, Entities = [] };
            message = message is JoinMessage join ? join with { Snapshot = header } : header;
        }
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new StateTransferPayload(message, packed), NetworkSession.Json);
        if (bytes.Length > ProtocolIdentity.MaximumStateBytes) throw new InvalidDataException("State transfer limit.");
        return bytes;
    }
    public static IEnumerable<StateChunkMessage> Chunks(WireMessage message, byte[]? serialized = null)
    {
        var bytes = serialized ?? Serialize(message);
        var snapshot = message switch { JoinMessage j => j.Snapshot, SnapshotMessage s => s, _ => throw new ArgumentException("Expected state.") };
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes)); var id = Guid.NewGuid().ToString("N");
        var count = (bytes.Length + ProtocolIdentity.StateChunkBytes - 1) / ProtocolIdentity.StateChunkBytes;
        for (var index = 0; index < count; index++)
        {
            var offset = index * ProtocolIdentity.StateChunkBytes;
            yield return new(id, snapshot.WorldEpoch, snapshot.Revision, index, count, bytes.Length, hash,
                bytes.AsSpan(offset, Math.Min(ProtocolIdentity.StateChunkBytes, bytes.Length - offset)).ToArray());
        }
    }
}
