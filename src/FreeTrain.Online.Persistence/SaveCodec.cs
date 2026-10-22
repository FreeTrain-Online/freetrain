using System.Text.Json;
using System.Text.Json.Serialization;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Persistence;

// Only the current save format is supported during parity implementation.
public static class SaveCodec
{
    public const int MaximumBytes = 1024 * 1024;
    public const int Version = 4;
    public sealed record MapData(int Width, int Height, int Levels, string CoordinateSystem);
    public sealed record RailData(int X, int Y, int Z, int Pattern);
    public sealed record Document(int SaveVersion, string Ruleset, string ContentVersion,
        RequiredPackRead[] RequiredPacks, MapData Map, RailData[] Rails, ClockState Clock, bool Paused, RandomStreamsState Random);
    private static readonly JsonSerializerOptions Json = new(WireJson.CreateOptions())
        { PropertyNameCaseInsensitive = false, NumberHandling = JsonNumberHandling.Strict };

    public static byte[] Write(ReferenceWorld world) => JsonSerializer.SerializeToUtf8Bytes(new Document(Version,
        ProtocolIdentity.ReferenceRuleset, ProtocolIdentity.ReferenceContent, ProtocolIdentity.ReferencePacks,
        new(world.Space.HorizontalSize, world.Space.VerticalSize, world.Space.Levels, "hv"),
        world.Rails.OrderBy(r => r.Position.X).ThenBy(r => r.Position.Y).ThenBy(r => r.Position.Z)
            .Select(r => new RailData(r.Position.X,r.Position.Y,r.Position.Z,r.Pattern)).ToArray(), world.Clock.Capture(), world.Paused, world.Random.Capture()), Json);

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
            if (number != Version || !root.TryGetProperty("ruleset", out var ruleset)
                || ruleset.ValueKind != JsonValueKind.String
                || ruleset.GetString() != ProtocolIdentity.ReferenceRuleset)
                throw new InvalidDataException("incompatibleSave");
            CheckUniqueProperties(root);
            var data = JsonSerializer.Deserialize<Document>(bytes, Json) ?? throw new JsonException();
            if (data.ContentVersion != ProtocolIdentity.ReferenceContent || data.RequiredPacks.Length != 1
                || data.RequiredPacks[0] != ProtocolIdentity.ReferencePacks[0] || data.Map.CoordinateSystem != "hv")
                throw new InvalidDataException("incompatibleSave");
            if (data.Rails.Any(r => r == null)) throw new JsonException();
            var world = new ReferenceWorld(data.Map.Width, data.Map.Height, data.Map.Levels,
                data.Rails.Select(r => new ReferenceRail(new(r.X,r.Y,r.Z),r.Pattern)));
            world.RestoreTime(data.Clock, data.Paused);
            if (data.Random.Version != RandomStreams.Version || data.Random.Algorithm != GameRandom.Algorithm)
                throw new InvalidDataException("incompatibleSave");
            world.RestoreRandom(data.Random);
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
