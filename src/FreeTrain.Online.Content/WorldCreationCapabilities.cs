using System.Collections.Immutable;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Content;

public static class WorldCreationCapabilities
{
    public static CreationContent EmptyReady()
    {
        var profile = TerrainProfiles.Bundled; // Requires all ground recipes to be executable.
        var pack = profile.Packs.Single();
        return new(pack.CatalogHash, [new(new(pack.Id, pack.Version, pack.CatalogHash,
            profile.GroundRecipes[0].ContributionId), CreationCapabilityKind.Terrain, true, true)]);
    }
    // Inventory prepared bindings only. These must not become selectable just because
    // art exists: live P3 render/save/worker integration is not ready yet.
    public static CreationContent Inspect(PreparedContentCatalog catalog)
    {
        var capabilities = catalog.RenderRecipes.Where(r => r.Handler == "forest")
            .Select(r => new CreationCapability(new(catalog.PackId, catalog.Version, catalog.CatalogHash, r.Id),
                CreationCapabilityKind.Forest,
                r.Data.GetProperty("frames").EnumerateArray().Any(f => f.GetProperty("status").GetString() == "ready") &&
                r.Data.GetProperty("frames").EnumerateArray().All(f => f.GetProperty("status").GetString() is "ready" or "invisible"),
                RuntimeReady: false)).ToImmutableArray();
        // Base catalog alone uses its exact hash. A future installed-set builder must
        // hash the sorted pack/version/hash tuple list and include reviewed providers.
        return new(catalog.CatalogHash, capabilities);
    }
}
