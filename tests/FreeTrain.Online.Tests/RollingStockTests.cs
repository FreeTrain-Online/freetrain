using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FreeTrain.Online.Content;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public class RollingStockTests
{
    const string Ft1 = "{F3604C61-D0D5-4c9c-ADD6-405610FA6250}";
    const string FtCar = "{9EE438DE-B766-4b37-A474-9CE6EE73FE75}";
    const string Raicho = "{3983B298-ADB1-4905-94E5-03B7AAE5A221}";
    const string Komachi = "{F4D54B1E-534E-47b0-9EA7-76CE075423E4}";
    static StockTrain Train(string id) => RollingStockCatalog.Bundled.Trains.Single(t => t.Binding.ContributionId == id);
    static JsonNode Catalog()
    {
        using var stream = typeof(PreparedContentCatalog).Assembly.GetManifestResourceStream("BundledCatalog")!;
        return JsonNode.Parse(stream)!;
    }
    static RollingStockCatalog Read(JsonNode node)
    {
        var bytes = Encoding.UTF8.GetBytes(node.ToJsonString());
        return RollingStockCatalog.Build(PreparedContentCatalog.Read(bytes, Convert.ToHexStringLower(SHA256.HashData(bytes))));
    }
    static JsonNode Definition(JsonNode c, string id) => c["contributions"]!.AsArray().Single(d => d!["id"]!.GetValue<string>() == id)!;
    static JsonNode Child(JsonNode n, string tag) => n["children"]!.AsArray().Single(c => c!["tag"]!.GetValue<string>() == tag)!;
    static JsonNode Field(JsonNode c, string id, string tag) => Child(Definition(c, id)["declaration"]!, tag);
    static JsonNode Node(string tag, string text = "", params (string, string)[] attributes) => JsonSerializer.SerializeToNode(new
    {
        tag, text, attributes = attributes.ToDictionary(a => a.Item1, a => a.Item2), children = Array.Empty<object>()
    })!;

    [Fact]
    public void Every_bundled_stock_definition_has_exact_binding_and_every_p5_crop_resolves_a_real_car()
    {
        var registry = RollingStockCatalog.Bundled;
        Assert.Empty(registry.Diagnostics);
        Assert.Equal(6, registry.Trains.Length); Assert.Equal(8, registry.Cars.Length); Assert.Equal(7, registry.Templates.Length);
        var catalog = PreparedContentCatalog.Bundled;
        Assert.Equal(catalog.Definitions.Where(d => d.Type is "train" or "trainCar" or "colorMapTrainPicture").Select(d => d.Id).Order(),
            registry.Trains.Select(t => t.Binding.ContributionId).Concat(registry.Cars.Select(c => c.Reference.ContributionId))
                .Concat(registry.Templates.Select(t => t.Reference.ContributionId)).Order());
        using var fixtures = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Spatial/train-fixtures.json")));
        foreach (var row in fixtures.RootElement.GetProperty("groups").GetProperty("rendering").GetProperty("cases").EnumerateArray())
        {
            var id = row.GetProperty("input").GetProperty("recipe").GetString()!;
            Assert.Equal(id, registry.ResolveCar(new(catalog.PackId, id, id)).Reference.RecipeId);
        }
        Assert.True(registry.Matches([new(catalog.PackId, catalog.Version, catalog.CatalogHash)]));
        Assert.False(registry.Matches([new(catalog.PackId, catalog.Version, new string('0', 64))]));
        Assert.Throws<ArgumentException>(() => registry.ResolveTrain(Train(Ft1).Binding with { CatalogHash = new string('0', 64) }));
        Assert.Throws<ArgumentException>(() => registry.ResolveTrain(Train(Ft1).Binding with { Version = "1" }));
        Assert.Throws<ArgumentException>(() => registry.ResolveCar(new(catalog.PackId, "test-car", "test-car")));
        Assert.Throws<ArgumentException>(() => registry.ResolveCar(registry.Cars[0].Reference with { RecipeId = Ft1 }));
        Assert.DoesNotContain(RailwayProfiles.Bundled.Bindings, b => b.Handler == RailwayHandler.Car);
    }

    [Theory]
    [InlineData(Ft1, 30_000_000, 2, 400, 100, 0, 660, 330, 990, 71)]
    [InlineData("{2C6F6C72-FA4B-4941-84C1-57553C8A5C2A}", 28_000_000, 3, 400, 100, 0, 660, 330, 990, 76)]
    [InlineData("{F7134C8E-6B63-4780-AF16-90D33131CD07}", 25_000_000, 4, 400, 100, 0, 660, 330, 990, 85)]
    [InlineData(Raicho, 50_000_000, 2, 750, 120, 2, 340, 340, 340, 42)]
    [InlineData(Komachi, 70_000_000, 1, 1000, 140, 2, 304, 304, 304, 30)]
    [InlineData("{719F0922-EA74-409C-B829-C4950201C226}", 100_000_000, 3, 400, 100, 0, 0, 0, 0, 21)]
    public void Literal_source_economics_and_capacities_bind_all_six_families(string id, int price, int speed,
        int fare, int amenity, int range, int normal, int seated, int packed, int longestSafe)
    {
        var train = Train(id); var f = train.Create(5);
        Assert.Equal(price, train.PricePerCar); Assert.Equal(speed, train.MinutesPerVoxel);
        Assert.Equal(fare, train.Fare); Assert.Equal(amenity, train.Amenity); Assert.Equal(range, train.TripRange);
        Assert.Equal(price * 5, f.Price); Assert.Equal(normal, f.NormalCapacity);
        Assert.Equal(seated, f.SeatedCapacity); Assert.Equal(packed, f.PackedCapacity);
        Assert.Equal(longestSafe, train.PermittedLengths[^1]); Assert.Empty(train.DeclaredLengths);
        Assert.Throws<OverflowException>(() => train.Create(longestSafe + 1));
        Assert.Equal((long)price * 5 * 100, train.Quote(5, 100).Price);
    }

    [Theory]
    [InlineData(Raicho, 65, 70)] [InlineData(Komachi, 56, 64)]
    public void One_car_tail_overwrites_head_and_mixed_order_is_immutable(string id, int endCapacity, int bodyCapacity)
    {
        var train = Train(id);
        var one = train.Create(1); Assert.Equal(id + "-t", one.Cars[0].Reference.ContributionId);
        Assert.Equal(endCapacity, one.NormalCapacity); Assert.Equal(endCapacity, one.SeatedCapacity);
        var f = train.Create(3);
        Assert.Equal(new[] { id + "-h", id + "-m", id + "-t" }, f.Cars.Select(c => c.Reference.ContributionId));
        Assert.Equal(2 * endCapacity + bodyCapacity, f.NormalCapacity);
        Assert.Equal(f.Cars[0].Reference, f.Cars[2].BaseCar);
        var changed = f.Cars.SetItem(0, f.Cars[1]);
        Assert.NotEqual(changed, train.Create(3).Cars); Assert.Equal(f.Cars, train.Create(3).Cars.ToArray());
    }

    [Fact]
    public void Color_test_is_real_zero_capacity_content_with_source_initial_template_and_colors()
    {
        var car = Train("{719F0922-EA74-409C-B829-C4950201C226}").Create(1).Cars.Single();
        Assert.Equal(StockCarHandler.ColorTest, car.Handler); Assert.Equal(0, car.NormalCapacity);
        Assert.Equal("{243D9F53-67FB-432E-B985-3B9B696D8CE0}-1", car.ColorTemplate!.ContributionId);
        Assert.Equal(new[] { new StockColor(255,0,0), new(0,0,255), new(0,255,0), new(255,255,0) }, car.Colors);
    }

    [Theory]
    [InlineData("0", false, 0)] [InlineData("-1", false, 0)] [InlineData("oops", false, 0)]
    [InlineData("1", true, 1)] [InlineData("5", true, 5)] [InlineData("2147483647", true, int.MaxValue)]
    public void Numeric_native_speed_is_positive_int32_without_clamping(string speed, bool valid, int expected)
    {
        var c = Catalog(); Field(c, Ft1, "speed")["text"] = speed;
        var r = Read(c);
        if (valid) Assert.Equal(expected, r.Trains.Single(t => t.Binding.ContributionId == Ft1).MinutesPerVoxel);
        else Assert.Contains(r.Diagnostics, d => d.Binding.ContributionId == Ft1 && d.English.Length > 0 && d.Japanese.Length > 0);
        Assert.Equal(valid ? 6 : 5, r.Trains.Length);
    }

    [Theory]
    [InlineData("missing-car")] [InlineData("wrong-car-type")] [InlineData("reverse-cycle")]
    [InlineData("capacity-mismatch")] [InlineData("negative-capacity")] [InlineData("unported-class")]
    [InlineData("bad-frame")] [InlineData("missing-angle")] [InlineData("template-art")]
    public void Invalid_reference_numeric_or_art_closure_never_enters_gameplay(string mutation)
    {
        var c = Catalog();
        var recipe = c["renderRecipes"]!.AsArray().Single(r => r!["id"]!.GetValue<string>() == FtCar)!;
        switch (mutation)
        {
            case "missing-car": Child(Field(c, Ft1, "composition"), "body")["attributes"]!["carRef"] = "absent"; break;
            case "wrong-car-type": Child(Field(c, Ft1, "composition"), "body")["attributes"]!["carRef"] = Raicho; break;
            case "reverse-cycle": Field(c, Raicho + "-t", "base")["attributes"]!["carRef"] = Raicho + "-t"; break;
            case "capacity-mismatch": Field(c, FtCar, "capacity")["text"] = "133"; break;
            case "negative-capacity": Field(c, FtCar, "capacity")["text"] = "-1"; break;
            case "unported-class": Definition(c, FtCar)["implementation"]!["name"] = "untrusted.Class"; break;
            case "bad-frame": recipe["status"] = "source-diagnostic"; break;
            case "missing-angle": recipe["frames"]!.AsArray().RemoveAt(0); break;
            case "template-art": c["pictures"]!.AsArray().Single(p => p!["id"]!.GetValue<string>() == "{243D9F53-67FB-432E-B985-3B9B696D8CE0}-1")!["status"] = "unprepared"; break;
        }
        var r = Read(c);
        Assert.NotEmpty(r.Diagnostics);
        Assert.True(r.Trains.Length < 6);
        Assert.All(r.Diagnostics, d => Assert.DoesNotContain(r.Trains, t => t.Binding == d.Binding));
        Assert.Equal(6, RollingStockCatalog.Bundled.Trains.Length);
    }

    static JsonNode PatternCatalog()
    {
        var c = Catalog(); var d = Definition(c, Ft1);
        d["implementation"]!["name"] = "freetrain.contributions.train.PatternTrainImpl";
        Field(c, Ft1, "price")["text"] = "1000";
        var e = d["declaration"]!; var children = e["children"]!.AsArray(); children.Remove(Child(e, "composition"));
        var config = Node("config"); var items = config["children"]!.AsArray();
        items.Add(Node("car", "", ("char", "A"), ("ref", Raicho + "-h")));
        items.Add(Node("car", "", ("char", "B"), ("ref", FtCar)));
        items.Add(Node("composition", " A B\nB A ")); items.Add(Node("composition", "B"));
        children.Add(config); return c;
    }

    [Fact]
    public void Explicit_patterns_accept_only_declared_lengths_with_mixed_capacities()
    {
        var r = Read(PatternCatalog()); Assert.Empty(r.Diagnostics);
        var t = r.Trains.Single(t => t.Binding.ContributionId == Ft1);
        Assert.False(t.IsParameterized); Assert.Equal(new[] { 1, 4 }, t.DeclaredLengths);
        var f = t.Create(4);
        Assert.Equal(new[] { Raicho + "-h", FtCar, FtCar, Raicho + "-h" }, f.Cars.Select(c => c.Reference.ContributionId));
        Assert.Equal(394, f.NormalCapacity); Assert.Equal(262, f.SeatedCapacity); Assert.Equal(526, f.PackedCapacity);
        Assert.Throws<ArgumentException>(() => t.Create(2));
    }

    [Theory]
    [InlineData("duplicate-length")] [InlineData("unknown-symbol")] [InlineData("too-long")]
    public void Invalid_patterns_are_diagnosed_as_whole_definitions(string problem)
    {
        var c = PatternCatalog(); var config = Field(c, Ft1, "config");
        if (problem == "duplicate-length") config["children"]!.AsArray().Add(Node("composition", "AAAA"));
        else if (problem == "unknown-symbol") config["children"]![0]!["attributes"]!["char"] = "C";
        if (problem == "too-long") config["children"]!.AsArray().Add(Node("composition", new string('A', 101)));
        Assert.Contains(Read(c).Diagnostics, d => d.Binding.ContributionId == Ft1);
    }

    [Fact]
    public void Source_seated_fallback_clamp_and_checked_aggregate_width_are_retained()
    {
        var c = Catalog(); Field(c, FtCar, "seatedcapacity")["text"] = "-1";
        c["renderRecipes"]!.AsArray().Single(r => r!["id"]!.GetValue<string>() == FtCar)!["fields"]!["seatedcapacity"] = "-1";
        Field(c, Ft1, "amenity")["text"] = "999";
        var t = Read(c).Trains.Single(t => t.Binding.ContributionId == Ft1);
        Assert.Equal(200, t.Amenity); Assert.Equal(264, t.Create(2).SeatedCapacity); Assert.Equal(264, t.Create(2).PackedCapacity);
        var car = new StockCar(new("synthetic", "car", "car"), StockCarHandler.Symmetric, int.MaxValue, 0);
        Assert.Throws<ArgumentException>(() => new StockTrain(new("synthetic", "1", "hash", "train"), new("C", "T", "N", "A", "D"),
            1, 1, 1, 1, 0, null, car, null)); // even one packed car overflows
    }

    [Fact]
    public void Registration_admission_bounds_quantity_request_and_cumulative_fleet_without_overflow()
    {
        StockAdmission.Validate(100, 10); StockAdmission.Validate(1, 1, 127, 1023);
        var train = Train(Ft1); Assert.Equal(1000, train.Quote(10, 100).CarCount);
        Assert.Equal(1024, train.Quote(16, 64).CarCount);
        foreach (var (length, quantity, trains, cars) in new[] { (0,1,0,0), (101,1,0,0), (1,0,0,0), (1,101,0,0),
            (100,11,0,0), (1,1,128,0), (1,1,0,1024), (1,1,-1,0), (int.MaxValue,int.MaxValue,0,0) })
            Assert.Throws<ArgumentException>(() => train.Quote(length, quantity, trains, cars));
    }

    [Fact]
    public void Pure_resale_valuation_consumes_independent_p5_anniversary_and_null_date_literals()
    {
        var car = new StockCar(new("synthetic", "car", "car"), StockCarHandler.Symmetric, 0, 0);
        var train = new StockTrain(new("synthetic", "1", "hash", "train"), new("C", "T", "N", "A", "D"),
            1_000_001, 1, 1, 100, 0, null, car, null);
        using var fixtures = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Spatial/train-fixtures.json")));
        foreach (var row in fixtures.RootElement.GetProperty("groups").GetProperty("sales").GetProperty("cases").EnumerateArray())
        {
            var days = row.GetProperty("input").GetProperty("ageDays");
            var now = days.ValueKind == JsonValueKind.Null ? 0 : days.GetInt64() * 24 * 60;
            Assert.Equal(row.GetProperty("expected").GetProperty("revenue").GetInt64(),
                train.ResaleValue(1, days.ValueKind == JsonValueKind.Null ? null : 0, now));
        }
        Assert.Throws<ArgumentException>(() => train.ResaleValue(1, 1, 0));
    }
}
