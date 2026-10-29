using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Content;

public sealed record FixedHeightmap(ContentBinding Binding, string Name, string Author, string Description,
    WorldDimensions Dimensions, int WaterLevel, string ImageUrl, string Sha256, long Bytes);

public static class FixedHeightmaps
{
    // Only the explicit prepared terrainOnlyNewGame adapter can register these.
    // Source images are never the color-keyed day/night sprite derivatives.
    public static ImmutableArray<FixedHeightmap> Read(OptionalPluginRegistry registry)
    {
        var maps = ImmutableArray.CreateBuilder<FixedHeightmap>();
        foreach (var catalog in registry.Catalogs)
        {
            if (!catalog.TryGetProperty("fixedHeightmaps", out var rows)) continue;
            string pack = catalog.GetProperty("packId").GetString()!;
            var descriptor = registry.Content.Packs.Single(p => p.Id == pack);
            foreach (var row in rows.EnumerateArray())
            {
                var id = row.GetProperty("id").GetString()!;
                var definition = catalog.GetProperty("contributions").EnumerateArray().Single(d => d.GetProperty("id").GetString() == id);
                if (definition.GetProperty("type").GetString() != "terrainOnlyNewGame" ||
                    definition.GetProperty("implementation").GetProperty("name").GetString() != "freetrain.tools.terrainloader.TerrainOnlyNewGameContribution")
                    throw new InvalidDataException("Unreviewed fixed heightmap handler.");
                var dimensions = row.GetProperty("dimensions").Deserialize<WorldDimensions>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                var image = row.GetProperty("image"); var url = image.GetProperty("url").GetString()!;
                if (!url.StartsWith(descriptor.CatalogUrl[..^"catalog.json".Length], StringComparison.Ordinal) ||
                    !WorldCreationProfile.IsHash(image.GetProperty("sha256").GetString())) throw new InvalidDataException("Unbound fixed heightmap.");
                maps.Add(new(new(pack, descriptor.Version, descriptor.CatalogHash, id), row.GetProperty("name").GetString()!,
                    row.GetProperty("author").GetString()!, row.GetProperty("description").GetString()!, dimensions,
                    row.GetProperty("waterLevel").GetInt32(), url, image.GetProperty("sha256").GetString()!, image.GetProperty("bytes").GetInt64()));
            }
        }
        return maps.ToImmutable();
    }
    public static CreationContent Ready(OptionalPluginRegistry registry)
    {
        var terrain = WorldCreationCapabilities.EmptyReady(); var maps = Read(registry);
        return new(registry.Content.SetHash, terrain.Capabilities.AddRange(maps.Select(m => new CreationCapability(m.Binding, CreationCapabilityKind.HeightmapProvider, true, true))));
    }
}
