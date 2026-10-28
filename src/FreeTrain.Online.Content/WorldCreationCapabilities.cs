using System.Collections.Immutable;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Content;

public static class WorldCreationCapabilities
{
    // Inventory prepared bindings only. These must not become selectable just because
    // art exists: P3 state/render/save handlers have not shipped yet.
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
