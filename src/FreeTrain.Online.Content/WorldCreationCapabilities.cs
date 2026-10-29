using System.Collections.Immutable;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Content;

public static class WorldCreationCapabilities
{
    public sealed record ForestChoice(ContentBinding Binding, string Species, string Variant);
    private static readonly (string Species, string Id)[] species =
    [
        ("cedar", "{FBD0CD06-016A-4037-AB15-02040218CBAA}"),
        ("pine", "{4B732903-B207-4693-9CF7-19AED4E42604}"),
        ("camphor", "{CE18E96E-336B-4C8A-80E6-F149DAEFD6E4}"),
        ("maple", "{88868086-3B45-46B5-A3F1-8D0E735DBD1C}"),
        ("ginkgo", "{77961EC1-0406-42C7-A5F5-ADEC3555400E}"),
        ("beech", "{DF43FB52-E197-451D-B4A3-1114BBFF69D9}")
    ];
    // Reviewed bundled handlers only. Optional inventory cannot grant permission.
    public static ImmutableArray<ForestChoice> ForestChoices()
    {
        var profile = TerrainProfiles.Bundled; var pack = profile.Packs.Single();
        return species.SelectMany(s => new[] { (Suffix: "", Variant: "natural"), (Suffix: "-m", Variant: "mountain"), (Suffix: "-g", Variant: "grass") }
            .Select(v =>
            {
                string id = s.Id + v.Suffix;
                var definition = profile.Resolve(new(pack.Id, id, id));
                if (definition.TreeSpriteCount < 1 || definition.TreeDensity < 1) throw new ArgumentException("Unavailable forest handler.");
                return new ForestChoice(new(pack.Id, pack.Version, pack.CatalogHash, id), s.Species, v.Variant);
            })).ToImmutableArray();
    }
    public static CreationContent GeneratedReady()
    {
        var terrain = EmptyReady();
        return terrain with { Capabilities = terrain.Capabilities.AddRange(ForestChoices()
            .Select(c => new CreationCapability(c.Binding, CreationCapabilityKind.Forest, true, true))) };
    }
    public static CreationContent EmptyReady()
    {
        var profile = TerrainProfiles.Bundled; // Requires all ground recipes to be executable.
        var pack = profile.Packs.Single();
        return new(pack.CatalogHash, [new(new(pack.Id, pack.Version, pack.CatalogHash,
            profile.GroundRecipes[0].ContributionId), CreationCapabilityKind.Terrain, true, true)]);
    }
    // Inventory prepared bindings only. These must not become selectable just because
    // art exists. GeneratedReady separately registers the reviewed bundled handlers.
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
