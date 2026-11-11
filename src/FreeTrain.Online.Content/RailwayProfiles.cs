using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Content;

// Exact mixed state/save/transport dispatch. Art readiness grants no tool handler.
public static class RailwayProfiles
{
    private static readonly Lazy<RailwayProfile> bundled = new(() => Build(PreparedContentCatalog.Bundled, TerrainProfiles.Bundled));
    public static RailwayProfile Bundled => bundled.Value;
    public static RailwayProfile? Resolve(string ruleset, string contentVersion, IEnumerable<TerrainPack> packs) =>
        ruleset == RailwayProfile.Ruleset && contentVersion == RailwayProfile.ContentVersion &&
        Bundled.Terrain.Matches(TerrainProfile.Ruleset,TerrainProfile.ContentVersion,packs) ? Bundled : null;
    public static RailwayProfile Build(PreparedContentCatalog catalog, TerrainProfile terrain)
    {
        if (!terrain.Packs.Contains(new TerrainPack(catalog.PackId, catalog.Version, catalog.CatalogHash)))
            throw new ArgumentException("Railway/terrain catalog mismatch.");
        var bindings = new List<RailwayBinding>();
        void Add(string id, RailwayHandler handler, int variant = -1, string recipeHandler = "source-geometry")
        {
            var recipe = catalog.FindRenderRecipe(catalog.PackId, id);
            if (recipe == null || recipe.Handler != recipeHandler || recipe.Data.GetProperty("status").GetString() != "ready" ||
                recipe.Data.GetProperty("frames").GetArrayLength() == 0 ||
                recipe.Data.GetProperty("frames").EnumerateArray().Any(f => f.GetProperty("status").GetString() is not ("ready" or "invisible")))
                throw new ArgumentException("Unavailable railway state recipe.");
            bindings.Add(new(new(catalog.PackId, id, id), handler, variant));
        }
        for (var i = 0; i < 60; i++) Add($"system:pattern/{i}",
            i < 12 ? RailwayHandler.Normal : i < 28 ? RailwayHandler.Junction : RailwayHandler.Slope, i);
        Add("system:pier", RailwayHandler.Pier, 0);
        Add("system:pier/slopeNE", RailwayHandler.Pier, 1);
        for (var d = 0; d < 8; d += 2)
            for (var i = 2; i < 4; i++)
            {
                var sprite = d + i - 2;
                var pattern = d / 2 * 4 + i;
                Add($"system:pattern/slope-support-{pattern}", RailwayHandler.SlopeSupport, sprite);
                Add($"system:pattern/underground-slope-support-{pattern}", RailwayHandler.SlopeWall, sprite);
            }
        Add("{874B8BFD-1EF4-4828-9C5A-D0EA1049D28D}", RailwayHandler.Bridge, recipeHandler: "special-rail");
        for (var i = 0; i < 4; i++)
            bindings.Add(new(new(catalog.PackId,$"system:bridge-pier/{i}","{874B8BFD-1EF4-4828-9C5A-D0EA1049D28D}"),RailwayHandler.Pier,i+2));
        Add("{7FD493FF-D6A7-4a13-A471-FB737641D8A0}", RailwayHandler.Girder, recipeHandler: "special-rail");
        Add("{3E66FC7A-2B58-4182-8A8E-2D5A01BE78E5}", RailwayHandler.Tunnel, recipeHandler: "special-rail");
        Add("{93481807-D998-46d7-B1BE-CB2B5D086D67}", RailwayHandler.Garage, recipeHandler: "special-rail");
        foreach (var recipe in catalog.RenderRecipes.Where(r => r.Handler is "electric-pole" or "signal"))
        {
            var expectedType = recipe.Handler == "signal" ? "railSignal" : "electricPole";
            if (catalog.FindDefinition(catalog.PackId, recipe.Id)?.Type != expectedType)
                throw new ArgumentException("Railway contribution type mismatch.");
            Add(recipe.Id, recipe.Handler == "signal" ? RailwayHandler.Signal : RailwayHandler.Pole, recipeHandler: recipe.Handler);
        }
        foreach (var recipe in catalog.RenderRecipes.Where(r => r.Id.StartsWith("system:construction/", StringComparison.Ordinal)))
            Add(recipe.Id, RailwayHandler.ConstructionArt);
        foreach (var definition in catalog.Definitions.Where(d => d.Type == "railStationary"))
        {
            Add(definition.Id, RailwayHandler.Stationary, recipeHandler: "fixed-3d");
            var fields = catalog.FindRenderRecipe(catalog.PackId, definition.Id)!.Data.GetProperty("fields");
            var size = fields.GetProperty("size");
            bindings[^1] = bindings[^1] with { Stationary = new(size[0].GetInt32(), size[1].GetInt32(),
                int.Parse(fields.GetProperty("height").GetString()!, System.Globalization.CultureInfo.InvariantCulture),
                long.Parse(fields.GetProperty("price").GetString()!, System.Globalization.CultureInfo.InvariantCulture)) };
        }
        var stock = catalog == PreparedContentCatalog.Bundled ? RollingStockCatalog.Bundled : RollingStockCatalog.Build(catalog);
        return new(terrain, bindings, stock.FleetProfile, StationProfiles.Build(catalog,terrain),CityProfiles.Build(catalog,terrain));
    }
}
