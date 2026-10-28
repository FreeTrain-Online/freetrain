using System.Globalization;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Content;

// Exact terrain state/save/transport dispatch; distinct from the reference-world profile.
// Only the reviewed bundled set is registered. Optional packs need their own handlers.
public static class TerrainProfiles
{
    private static readonly Lazy<TerrainProfile> bundled = new(() => Build(PreparedContentCatalog.Bundled));
    public static TerrainProfile Bundled => bundled.Value;
    public static TerrainProfile? Resolve(string ruleset, string contentVersion, IEnumerable<TerrainPack> packs) =>
        Bundled.Matches(ruleset, contentVersion, packs) ? Bundled : null;

    private static TerrainProfile Build(PreparedContentCatalog catalog)
    {
        var ground = new[] { "spring", "winter", "water", "underground" }.Select(kind =>
        {
            var id = $"system:ground/{kind}";
            RequireReady(catalog.FindRenderRecipe(catalog.PackId, id), "source-geometry");
            return new DefinitionReference(catalog.PackId, id, id);
        }).ToArray();
        // MountainVoxel is source color/quarter geometry, not a bitmap contribution.
        // This explicit runtime identity must never be sent to the prepared recipe lookup.
        var definitions = new List<TerrainDefinition> { new(new(catalog.PackId, TerrainProfile.MountainId,
            TerrainProfile.MountainGeometry), TerrainOccupantKind.Mountain) };
        foreach (var recipe in catalog.RenderRecipes.Where(r => r.Handler is "forest" or "terrace" or "creek"))
        {
            RequireReady(recipe, recipe.Handler);
            var kind = recipe.Handler == "terrace" ? TerrainOccupantKind.Terrace :
                recipe.Handler == "creek" ? TerrainOccupantKind.Creek : TerrainOccupantKind.Mountain;
            var fields = recipe.Data.GetProperty("fields");
            int Number(string name) => int.Parse(fields.GetProperty(name).GetString()!, CultureInfo.InvariantCulture);
            definitions.Add(new(new(catalog.PackId, recipe.Id, recipe.Id), kind,
                recipe.Handler == "forest" ? Number("count") : 0,
                recipe.Handler == "forest" ? Number("density") : 0,
                recipe.Handler == "forest" ? Number("price") : 0));
        }
        return new([new(catalog.PackId, catalog.Version, catalog.CatalogHash)], ground, definitions);
    }
    private static void RequireReady(RenderRecipe? recipe, string handler)
    {
        if (recipe is null || recipe.Handler != handler || recipe.Data.GetProperty("status").GetString() != "ready" ||
            !recipe.Data.GetProperty("frames").EnumerateArray().Any(f => f.GetProperty("status").GetString() == "ready") ||
            recipe.Data.GetProperty("frames").EnumerateArray().Any(f => f.GetProperty("status").GetString() is not ("ready" or "invisible")))
            throw new ArgumentException("Unavailable prepared terrain recipe.");
    }
}
