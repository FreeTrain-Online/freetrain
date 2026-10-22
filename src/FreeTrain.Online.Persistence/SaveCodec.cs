using System.Text.Json;

namespace FreeTrain.Online.Persistence;

// No prototype save codec is registered. Canonical-world persistence is a later slice.
public static class SaveCodec
{
    public const int MaximumBytes = 1024 * 1024;

    public static void RejectUnsupported(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("saveTooLarge");
        try
        {
            using var document = JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("saveVersion", out var version)
                || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number <= 0)
                throw new InvalidDataException("invalidSave");
            // Do not deserialize a retired world, remap it, or accept a version-only relabel.
            throw new InvalidDataException("incompatibleSave");
        }
        catch (JsonException ex) { throw new InvalidDataException("invalidSave", ex); }
    }
}
