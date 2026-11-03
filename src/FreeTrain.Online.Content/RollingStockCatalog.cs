using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Content;

public sealed record StockTemplate(DefinitionReference Reference, string PluginId, string Name, string Author);
public sealed record StockDiagnostic(ContentBinding Binding, string Code, string Detail, string English, string Japanese);

// Explicit portable handlers alongside P2 art. XML class names are compared to
// this allowlist; no assembly, delegate or CLR type is loaded from a pack.
public sealed class RollingStockCatalog
{
    private sealed record Input(TerrainPack Pack, PreparedDefinition Definition, RenderRecipe? Recipe, bool PictureReady);
    private readonly FrozenDictionary<ContentBinding, StockTrain> trains;
    private readonly FrozenDictionary<DefinitionReference, StockCar> cars;
    private static readonly Lazy<RollingStockCatalog> bundled = new(() => Build(PreparedContentCatalog.Bundled));
    public static RollingStockCatalog Bundled => bundled.Value;
    public ImmutableArray<TerrainPack> Packs { get; }
    public ImmutableArray<StockTrain> Trains { get; }
    public ImmutableArray<StockCar> Cars { get; }
    public ImmutableArray<StockTemplate> Templates { get; }
    public ImmutableArray<StockDiagnostic> Diagnostics { get; }
    // Shared immutable state profile. Controller execution/configuration follows
    // in P5-16 onward; these are exact registered provider identities only.
    public FleetProfile FleetProfile { get; }

    public StockTrain ResolveTrain(ContentBinding binding) => trains.TryGetValue(binding, out var train)
        ? train : throw new ArgumentException("Unavailable rolling-stock definition or incompatible pack.");
    public StockCar ResolveCar(DefinitionReference reference) => cars.TryGetValue(reference, out var car)
        ? car : throw new ArgumentException("Unavailable rolling-stock car or recipe.");
    public bool Matches(IEnumerable<TerrainPack> packs) => Packs.OrderBy(p => p.Id, StringComparer.Ordinal)
        .SequenceEqual(packs.OrderBy(p => p.Id, StringComparer.Ordinal));

    public static RollingStockCatalog Build(PreparedContentCatalog catalog, OptionalPluginRegistry? optional = null)
    {
        if (optional != null && catalog.CatalogHash != PreparedContentCatalog.Bundled.CatalogHash)
            throw new ArgumentException("Rolling-stock bundled dependency mismatch.");
        var pack = new TerrainPack(catalog.PackId, catalog.Version, catalog.CatalogHash);
        var packs = new List<TerrainPack> { pack };
        var inputs = catalog.Definitions.Select(d => new Input(pack, d, catalog.FindRenderRecipe(pack.Id, d.Id),
            catalog.FindPicture(pack.Id, d.Id)?.HasPreparedVariants == true)).ToList();
        if (optional != null)
        {
            // OptionalPluginRegistry has already verified dependency hashes, media,
            // identity collisions and URLs. Keep selected packs out of Bundled.
            foreach (var document in optional.Catalogs)
            {
                var descriptor = optional.Content.Packs.Single(p => p.Id == document.GetProperty("packId").GetString());
                var owner = new TerrainPack(descriptor.Id, descriptor.Version, descriptor.CatalogHash);
                packs.Add(owner);
                var recipes = document.GetProperty("renderRecipes").EnumerateArray()
                    .ToDictionary(r => r.GetProperty("id").GetString()!, StringComparer.Ordinal);
                var pictures = document.GetProperty("pictures").EnumerateArray()
                    .Where(p => p.GetProperty("status").GetString() == "prepared-picture")
                    .Select(p => p.GetProperty("id").GetString()!).ToHashSet(StringComparer.Ordinal);
                foreach (var row in document.GetProperty("contributions").EnumerateArray())
                {
                    var id = row.GetProperty("id").GetString()!;
                    var d = new PreparedDefinition(id, row.GetProperty("pluginId").GetString()!, row.GetProperty("type").GetString()!,
                        row.TryGetProperty("declaration", out var declaration) ? declaration.Clone() : row.Clone(),
                        row.TryGetProperty("implementation", out var implementation) ? implementation.GetProperty("name").GetString()! : "");
                    inputs.Add(new(owner, d, recipes.TryGetValue(id, out var r)
                        ? new(id, d.PluginId, r.GetProperty("handler").GetString()!, r.Clone()) : null, pictures.Contains(id)));
                }
            }
        }
        return new(packs, inputs);
    }

    private RollingStockCatalog(IEnumerable<TerrainPack> packs, List<Input> inputs)
    {
        Packs = packs.ToImmutableArray();
        var controllerProviders = inputs.Where(i => i.Definition.Type == "trainController").Select(i =>
        {
            var kind = i.Definition.Implementation switch
            {
                "freetrain.world.rail.manualtc.TrainConrollerContributionImpl" => FleetControllerKind.Manual,
                "freetrain.world.rail.tattc.TATTrainControllerPlugIn" => FleetControllerKind.Timetable,
                _ => (FleetControllerKind?)null
            };
            return kind is {} k ? new FleetControllerProvider(new(i.Pack.Id,i.Pack.Version,i.Pack.CatalogHash,i.Definition.Id),k) : null;
        }).OfType<FleetControllerProvider>().ToImmutableArray();
        var byId = inputs.ToDictionary(i => i.Definition.Id, StringComparer.Ordinal);
        var templateList = new List<StockTemplate>();
        var carMap = new Dictionary<string, StockCar>(StringComparer.Ordinal);
        var trainList = new List<StockTrain>();
        var diagnostics = new Dictionary<string, StockDiagnostic>(StringComparer.Ordinal);
        ContentBinding Binding(Input i) => new(i.Pack.Id, i.Pack.Version, i.Pack.CatalogHash, i.Definition.Id);
        DefinitionReference Reference(Input i) => new(i.Pack.Id, i.Definition.Id, i.Definition.Id);
        void Diagnose(Input i, Exception error)
        {
            diagnostics.TryAdd(i.Definition.Id, new(Binding(i), "stock-unavailable", error.Message,
                "Rolling stock unavailable: invalid or unsupported definition/reference/art.",
                "車両を利用できません。定義・参照・画像が無効、または未対応です。"));
        }
        void Recipe(Input i, string handler, bool frames)
        {
            if (i.Recipe is not {} r || r.Handler != handler || r.Data.GetProperty("status").GetString() != "ready" ||
                frames && (r.Data.GetProperty("frames").GetArrayLength() == 0 ||
                    r.Data.GetProperty("frames").EnumerateArray().Any(f => f.GetProperty("status").GetString() != "ready")))
                throw new ArgumentException("Unavailable stock art: " + i.Definition.Id);
        }
        foreach (var i in inputs.Where(i => i.Definition.Type == "colorMapTrainPicture"))
            try
            {
                if (i.Definition.Implementation != "freetrain.contributions.train.ColoredTrainPictureContribution")
                    throw new ArgumentException("Unsupported color template implementation.");
                Recipe(i, "metadata", false);
                if (!i.PictureReady) throw new ArgumentException("Unprepared color template picture.");
                var declaration = i.Definition.Declaration;
                templateList.Add(new(Reference(i), i.Definition.PluginId, Text(declaration, "name"), Text(declaration, "author")));
            }
            catch (Exception e) when (Invalid(e)) { Diagnose(i, e); }
        StockTemplate Template(string id) => templateList.SingleOrDefault(t => t.Reference.ContributionId == id)
            ?? throw new ArgumentException("Unresolved color template: " + id);

        var visiting = new HashSet<string>(StringComparer.Ordinal);
        StockCar Car(string id)
        {
            if (carMap.TryGetValue(id, out var result)) return result;
            if (!byId.TryGetValue(id, out var i) || i.Definition.Type != "trainCar")
                throw new ArgumentException("Unresolved train car: " + id);
            if (diagnostics.ContainsKey(id)) throw new ArgumentException("Unavailable train car: " + id);
            if (visiting.Count >= 128 || !visiting.Add(id)) throw new ArgumentException("Cyclic/deep car reference: " + id);
            try
            {
                var e = i.Definition.Declaration;
                var handler = i.Definition.Implementation switch
                {
                    "freetrain.contributions.train.SymTrainCarImpl" => StockCarHandler.Symmetric,
                    "freetrain.contributions.train.AsymTrainCarImpl" => StockCarHandler.Asymmetric,
                    "freetrain.contributions.train.ReverseTrainCarImpl" => StockCarHandler.Reverse,
                    "freetrain.contributions.train.ColoredTrainCarImpl" => StockCarHandler.Colored,
                    "freetrain.world.rail.cttrain.ColorTestTrainCar" => StockCarHandler.ColorTest,
                    _ => throw new ArgumentException("Unsupported car implementation: " + i.Definition.Implementation)
                };
                Recipe(i, handler == StockCarHandler.Reverse ? "reverse-train-car" : "train-car", true);
                if (handler == StockCarHandler.Reverse)
                {
                    var core = Car(Attribute(Child(e, "base"), "carRef"));
                    if (i.Recipe!.Data.GetProperty("fields").GetProperty("base").GetString() != core.Reference.ContributionId)
                        throw new ArgumentException("Stock reverse art/reference mismatch.");
                    result = new(Reference(i), handler, core.NormalCapacity, core.SeatedCapacity, core.Reference,
                        core.ColorTemplate, core.Colors);
                }
                else
                {
                    var normal = handler == StockCarHandler.ColorTest ? 0 : Number(e, "capacity");
                    var seated = handler == StockCarHandler.ColorTest ? 0 : Number(e, "seatedcapacity");
                    if (normal < 0) throw new ArgumentException("Negative car capacity.");
                    DefinitionReference? template = null;
                    ImmutableArray<StockColor> colors = [];
                    if (handler == StockCarHandler.ColorTest)
                    {
                        var first = inputs.FirstOrDefault(v => v.Definition.Type == "colorMapTrainPicture")
                            ?? throw new ArgumentException("No color-test template.");
                        template = Template(first.Definition.Id).Reference;
                        colors = [new(255, 0, 0), new(0, 0, 255), new(0, 255, 0), new(255, 255, 0)];
                    }
                    else if (handler == StockCarHandler.Colored)
                    {
                        var map = Child(e, "colorMap");
                        template = Template(Attribute(map, "picture")).Reference;
                        colors = new[] { "base", "line1", "line2", "line3" }.Select(key =>
                        {
                            var rgb = Attribute(map, key).Split(',').Select(v => byte.Parse(v, CultureInfo.InvariantCulture)).ToArray();
                            if (rgb.Length != 3) throw new ArgumentException("Invalid stock RGB.");
                            return new StockColor(rgb[0], rgb[1], rgb[2]);
                        }).ToImmutableArray();
                    }
                    result = new(Reference(i), handler, normal, seated, ColorTemplate: template, Colors: colors);
                }
                var frameRows = i.Recipe!.Data.GetProperty("frames").EnumerateArray().ToArray();
                if (result.ColorTemplate is {} colorTemplate && frameRows.Any(f =>
                    f.GetProperty("pictureId").GetString() != colorTemplate.ContributionId))
                    throw new ArgumentException("Stock color art/template mismatch.");
                if (handler == StockCarHandler.Colored)
                {
                    var colorFields = i.Recipe.Data.GetProperty("fields");
                    if (colorFields.GetProperty("colorTemplate").GetString() != result.ColorTemplate!.ContributionId ||
                        !colorFields.GetProperty("colors").EnumerateArray().Select(c => new StockColor(c[0].GetByte(), c[1].GetByte(), c[2].GetByte()))
                            .SequenceEqual(result.Colors)) throw new ArgumentException("Stock color art/palette mismatch.");
                }
                // Exact 8+4 / 16+8 selectors, including reverse wrappers. Reject
                // missing or duplicated angles rather than allowing fallback art.
                var asymmetric = i.Recipe.Data.GetProperty("fields").GetProperty("asymmetric").GetBoolean();
                foreach (var (key, count) in new[] { ("angle", asymmetric ? 16 : 8), ("slope", asymmetric ? 8 : 4) })
                {
                    var selectors = frameRows.Where(f => f.GetProperty("selector").TryGetProperty(key, out _))
                        .Select(f => f.GetProperty("selector").GetProperty(key).GetInt32()).Order();
                    if (!selectors.SequenceEqual(Enumerable.Range(0, count)))
                        throw new ArgumentException("Incomplete stock frame selectors.");
                }
                // Art and gameplay must agree, including inherited capacities.
                var fields = i.Recipe!.Data.GetProperty("fields");
                if (Number(fields.GetProperty("capacity").GetString()!) != result.NormalCapacity ||
                    Number(fields.GetProperty("seatedcapacity").GetString()!) != result.SeatedCapacity)
                    throw new ArgumentException("Stock art/capacity mismatch.");
                carMap.Add(id, result);
                return result;
            }
            catch (Exception e) when (Invalid(e)) { Diagnose(i, e); throw; }
            finally { visiting.Remove(id); }
        }
        foreach (var i in inputs.Where(i => i.Definition.Type == "trainCar"))
            try { Car(i.Definition.Id); }
            catch (Exception e) when (Invalid(e)) { Diagnose(i, e); }
        foreach (var i in inputs.Where(i => i.Definition.Type == "train"))
            try
            {
                var e = i.Definition.Declaration;
                Recipe(i, "train-composition", false);
                StockCar? head = null, body = null, tail = null;
                var patterns = new List<ImmutableArray<StockCar>>();
                switch (i.Definition.Implementation)
                {
                    case "freetrain.contributions.train.ParamTrainImpl":
                        var comp = Child(e, "composition");
                        StockCar? Part(string tag) => Children(comp, tag).SingleOrDefault() is { ValueKind: JsonValueKind.Object } part
                            ? Car(Attribute(part, "carRef")) : null;
                        head = Part("head"); body = Part("body"); tail = Part("tail");
                        if (body == null) throw new ArgumentException("Missing train body.");
                        break;
                    case "freetrain.contributions.train.PatternTrainImpl":
                        var config = Child(e, "config");
                        var chars = Children(config, "car").ToDictionary(c =>
                        {
                            var symbol = Attribute(c, "char");
                            if (symbol.Length != 1) throw new ArgumentException("Invalid composition character.");
                            return symbol[0];
                        }, c => Car(Attribute(c, "ref")));
                        foreach (var c in Children(config, "composition"))
                        {
                            var symbols = c.GetProperty("text").GetString()!.Where(ch => !char.IsWhiteSpace(ch)).ToArray();
                            if (symbols.Length is < 1 or > StockAdmission.MaximumFormationCars)
                                throw new ArgumentException("Unsupported pattern length.");
                            patterns.Add(symbols.Select(ch => chars.TryGetValue(ch, out var car) ? car
                                : throw new ArgumentException("Undefined composition character.")).ToImmutableArray());
                        }
                        break;
                    default: throw new ArgumentException("Unsupported train implementation: " + i.Definition.Implementation);
                }
                var speed = Text(e, "speed").ToLowerInvariant();
                var minutes = speed switch { "slow" => 4, "medium" => 3, "fast" => 2, "superb" => 1, _ => Number(speed) };
                var range = OptionalText(e, "triprange")?.ToLowerInvariant() switch { "middle" => 1, "long" => 2, _ => 0 };
                trainList.Add(new(Binding(i), new(Text(e, "company"), Text(e, "type"), Text(e, "name"), Text(e, "author"), Text(e, "description")),
                    Number(e, "price"), minutes, Number(e, "fare"), Number(e, "amenity"), range, head, body, tail, patterns));
            }
            catch (Exception e) when (Invalid(e)) { Diagnose(i, e); }
        Trains = trainList.ToImmutableArray(); Cars = carMap.Values.ToImmutableArray(); Templates = templateList.ToImmutableArray();
        Diagnostics = diagnostics.Values.ToImmutableArray();
        trains = Trains.ToFrozenDictionary(t => t.Binding); cars = Cars.ToFrozenDictionary(c => c.Reference);
        FleetProfile = new(Packs,Trains,controllerProviders);
    }

    private static bool Invalid(Exception e) => e is ArgumentException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException;
    private static IEnumerable<JsonElement> Children(JsonElement e, string tag) => e.GetProperty("children").EnumerateArray().Where(c => c.GetProperty("tag").GetString() == tag);
    private static JsonElement Child(JsonElement e, string tag) => Children(e, tag).Single();
    private static string Attribute(JsonElement e, string key) => e.GetProperty("attributes").GetProperty(key).GetString()
        ?? throw new ArgumentException("Missing stock attribute: " + key);
    private static string Text(JsonElement e, string tag) => Child(e, tag).GetProperty("text").GetString()
        ?? throw new ArgumentException("Missing stock text: " + tag);
    private static string? OptionalText(JsonElement e, string tag) => Children(e, tag).SingleOrDefault() is { ValueKind: JsonValueKind.Object } c ? c.GetProperty("text").GetString() : null;
    private static int Number(JsonElement e, string tag) => Number(Text(e, tag));
    private static int Number(string value) => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
}
