using System.Text.Json;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Persistence;

// Save 3 extends the canonical graph with source-minute clock state. Save 2 upgrades explicitly.
public static class SaveCodec
{
    public const int MaximumBytes = 1024 * 1024;
    public const int Version = 3;
    public sealed record MapData(int Width, int Height, int Levels, string CoordinateSystem);
    public sealed record RailData(int X, int Y, int Z, int Pattern);
    public sealed record SpatialDocument(int SaveVersion, string Ruleset, string ContentVersion,
        RequiredPackRead[] RequiredPacks, MapData Map, RailData[] Rails);
    public sealed record Document(int SaveVersion, string Ruleset, string ContentVersion,
        RequiredPackRead[] RequiredPacks, MapData Map, RailData[] Rails, ClockState Clock, bool Paused);
    private static readonly JsonSerializerOptions Json = new(WireJson.CreateOptions()) { PropertyNameCaseInsensitive = false };

    public static byte[] Write(ReferenceWorld world) => JsonSerializer.SerializeToUtf8Bytes(new Document(Version,
        ProtocolIdentity.ReferenceRuleset, ProtocolIdentity.ReferenceContent, ProtocolIdentity.ReferencePacks,
        new(world.Space.HorizontalSize, world.Space.VerticalSize, world.Space.Levels, "hv"),
        world.Rails.OrderBy(r => r.Position.X).ThenBy(r => r.Position.Y).ThenBy(r => r.Position.Z)
            .Select(r => new RailData(r.Position.X,r.Position.Y,r.Position.Z,r.Pattern)).ToArray(), world.Clock.Capture(), world.Paused), Json);

    public static ReferenceWorld Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("saveTooLarge");
        try
        {
            using var parsed = JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
            var root = parsed.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("saveVersion", out var version)
                || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number <= 0)
                throw new InvalidDataException("invalidSave");
            if (number is not (2 or Version) || !root.TryGetProperty("ruleset", out var ruleset)
                || ruleset.ValueKind != JsonValueKind.String
                || ruleset.GetString() != (number == 2 ? "reference-spatial-1" : ProtocolIdentity.ReferenceRuleset))
                throw new InvalidDataException("incompatibleSave");
            CheckUniqueProperties(root);
            Document data;
            if (number == 2)
            {
                var spatial = JsonSerializer.Deserialize<SpatialDocument>(bytes, Json) ?? throw new JsonException();
                data = new(Version, ProtocolIdentity.ReferenceRuleset, spatial.ContentVersion, spatial.RequiredPacks,
                    spatial.Map, spatial.Rails, new(GameTime.StartMinute, 0, []), true);
            }
            else data = JsonSerializer.Deserialize<Document>(bytes, Json) ?? throw new JsonException();
            if (data.ContentVersion != ProtocolIdentity.ReferenceContent || data.RequiredPacks.Length != 1
                || data.RequiredPacks[0] != ProtocolIdentity.ReferencePacks[0] || data.Map.CoordinateSystem != "hv")
                throw new InvalidDataException("incompatibleSave");
            if (data.Rails.Any(r => r == null)) throw new JsonException();
            var world = new ReferenceWorld(data.Map.Width, data.Map.Height, data.Map.Levels,
                data.Rails.Select(r => new ReferenceRail(new(r.X,r.Y,r.Z),r.Pattern)));
            world.RestoreTime(data.Clock, data.Paused);
            return world;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or OverflowException)
        { throw new InvalidDataException("invalidSave", ex); }
    }
    private static void CheckUniqueProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            { if (!names.Add(property.Name)) throw new JsonException("Duplicate property."); CheckUniqueProperties(property.Value); }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) CheckUniqueProperties(item);
    }
}
