using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FreeTrain.Online.Content;
using FreeTrain.Online.Server;
using FreeTrain.Online.Simulation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class OptionalPluginTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "freetrain-optional-" + Guid.NewGuid().ToString("N"));
    public OptionalPluginTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, true);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private string Pack(string id = "synthetic-one", string? dependency = null, string? definition = null)
    {
        var directory = Path.Combine(root, id); Directory.CreateDirectory(directory);
        var catalog = new
        {
            schemaVersion = 2, packId = id, version = "1", status = "prepared; not selected by a world profile", source = new { revision = "synthetic" },
            plugins = new[] { new { id, dependencies = new[] { dependency ?? "system" } } },
            factories = Array.Empty<object>(),
            contributions = new[] { new { id = definition ?? id + "-picture", pluginId = id, type = "picture", status = "prepared; gameplay-unavailable" } },
            renderRecipes = new[] { new { id = definition ?? id + "-picture", pluginId = id, handler = "metadata", frames = Array.Empty<object>() } },
            pictures = Array.Empty<object>(), assets = Array.Empty<object>(), surfaceSets = new { }, masks = new { },
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(catalog, Json); File.WriteAllBytes(Path.Combine(directory, "catalog.json"), bytes);
        var notice = Encoding.UTF8.GetBytes("Original synthetic content, freely usable.\n"); File.WriteAllBytes(Path.Combine(directory, "notice.txt"), notice);
        var prefix = $"/api/content/{id}/{new string('a', 64)}/";
        var manifest = new
        {
            schemaVersion = 1, id, version = "1", sourceRevision = "synthetic", profile = "synthetic-v2", contentKey = new string('a', 64),
            dependencies = new[] { new PluginDependency(PreparedContentCatalog.BundledPackId, PreparedContentCatalog.Bundled.Version, PreparedContentCatalog.Bundled.CatalogHash) },
            credits = new PluginCredits("ja", new() { ["en"] = new("Fixture credits", "Fixture", "Original fixture"), ["ja"] = new("謝辞", "検証", "検証用") }),
            notices = new[] { new PluginNotice("notice.txt", prefix + "notice.txt", Hash(notice)) },
            files = new Dictionary<string, PluginFile> { ["catalog.json"] = new(Hash(bytes), bytes.Length), ["notice.txt"] = new(Hash(notice), notice.Length) },
        };
        File.WriteAllBytes(Path.Combine(directory, "prepared-pack.json"), JsonSerializer.SerializeToUtf8Bytes(manifest, Json));
        return directory;
    }

    [Fact]
    public void Repeated_directory_options_preserve_every_path_and_leave_other_server_flags()
    {
        var (directories, remaining) = PluginStartup.ParseArguments(["--urls", "http://localhost:5080", "--plugin-dir", root, "--plugin-dir=" + Path.Combine(root, "two")]);
        Assert.Equal(new[] { root, Path.Combine(root, "two") }, directories); Assert.Equal(new[] { "--urls", "http://localhost:5080" }, remaining);
        Assert.Throws<ArgumentException>(() => PluginStartup.ParseArguments(["--plugin-dir"]));
        Assert.Throws<ArgumentException>(() => PluginStartup.ParseArguments(["--plugin-dir= "]));
        Assert.Throws<ArgumentException>(() => PluginStartup.ParseArguments(["--plugin-dir", root, "--plugin-dir", root]));
    }

    [Fact]
    public void Multiple_packs_resolve_dependencies_independently_of_argument_order_and_never_expose_local_paths()
    {
        var one = Pack(); var two = Pack("synthetic-two", "synthetic-one");
        var registry = OptionalPluginRegistry.Load([two, one]);
        Assert.Equal(registry.Content.SetHash, OptionalPluginRegistry.Load([one, two]).Content.SetHash);
        Assert.Equal(2, registry.Content.Packs.Length);
        Assert.DoesNotContain(root, JsonSerializer.Serialize(registry.Content));
        Assert.True(registry.Compatible(registry.Content.SetHash)); Assert.False(registry.Compatible(null));
        Assert.False(registry.Compatible(new string('0', 64)));
        Assert.True(OptionalPluginRegistry.Empty.Compatible(null)); Assert.Empty(OptionalPluginRegistry.Empty.Content.Packs);
        var descriptor = registry.Content.Packs[0];
        Assert.Equal(descriptor.CatalogHash, Hash(registry.ReadFile(descriptor.CatalogUrl)!));
        Assert.Null(registry.ReadFile(descriptor.CatalogUrl + "/../prepared-pack.json"));
        File.WriteAllText(Path.Combine(one, "catalog.json"), "tampered");
        Assert.Throws<InvalidDataException>(() => registry.ReadFile(descriptor.CatalogUrl));
    }

    [Fact]
    public void Optional_stock_uses_only_selected_verified_packs_and_never_leaks_into_bundled_registry()
    {
        var directory = Pack();
        var catalogPath = Path.Combine(directory, "catalog.json");
        var catalog = JsonNode.Parse(File.ReadAllBytes(catalogPath))!;
        using var source = typeof(PreparedContentCatalog).Assembly.GetManifestResourceStream("BundledCatalog")!;
        var bundled = JsonNode.Parse(source)!;
        const string ft1 = "{F3604C61-D0D5-4c9c-ADD6-405610FA6250}";
        var definition = bundled["contributions"]!.AsArray().Single(d => d!["id"]!.GetValue<string>() == ft1)!.DeepClone();
        var recipe = bundled["renderRecipes"]!.AsArray().Single(d => d!["id"]!.GetValue<string>() == ft1)!.DeepClone();
        definition["id"] = "optional-train"; recipe["id"] = "optional-train";
        definition["pluginId"] = "synthetic-one"; recipe["pluginId"] = "synthetic-one";
        definition["declaration"]!["attributes"]!["id"] = "optional-train";
        catalog["contributions"] = new JsonArray(definition); catalog["renderRecipes"] = new JsonArray(recipe);
        var bytes = Encoding.UTF8.GetBytes(catalog.ToJsonString()); File.WriteAllBytes(catalogPath, bytes);
        var manifestPath = Path.Combine(directory, "prepared-pack.json");
        var manifest = JsonNode.Parse(File.ReadAllBytes(manifestPath))!;
        manifest["files"]!["catalog.json"] = JsonSerializer.SerializeToNode(new PluginFile(Hash(bytes), bytes.Length), Json);
        File.WriteAllText(manifestPath, manifest.ToJsonString());

        var optional = OptionalPluginRegistry.Load([directory]);
        var stocks = optional.RollingStock;
        Assert.Empty(stocks.Diagnostics); Assert.Equal(7, stocks.Trains.Length);
        var train = stocks.Trains.Single(t => t.Binding.ContributionId == "optional-train");
        Assert.Equal("synthetic-one", train.Binding.PackId); Assert.Equal(264, train.Create(2).NormalCapacity);
        Assert.Equal(PreparedContentCatalog.BundledPackId, train.Create(2).Cars[0].Reference.PackId);
        Assert.Same(stocks, optional.RollingStock);
        Assert.Throws<ArgumentException>(() => RollingStockCatalog.Bundled.ResolveTrain(train.Binding));
        Assert.Throws<ArgumentException>(() => stocks.ResolveTrain(train.Binding with { CatalogHash = new string('0', 64) }));
        Assert.False(stocks.Matches(RollingStockCatalog.Bundled.Packs));
        Assert.Equal(6, OptionalPluginRegistry.Empty.RollingStock.Trains.Length);
        Assert.Equal(6, RollingStockCatalog.Bundled.Trains.Length);
    }

    [Theory]
    [InlineData("missing")][InlineData("cycle")][InlineData("collision")][InlineData("duplicate-pack")]
    [InlineData("hash")][InlineData("traversal")][InlineData("credits")][InlineData("base-version")]
    public void Invalid_set_never_publishes_partial_content(string mutation)
    {
        var one = Pack(dependency: mutation == "missing" ? "missing" : mutation == "cycle" ? "synthetic-two" : null);
        var two = Pack("synthetic-two", mutation == "cycle" ? "synthetic-one" : null, mutation == "collision" ? "synthetic-one-picture" : null);
        var path = Path.Combine(one, "prepared-pack.json"); var node = JsonNode.Parse(File.ReadAllBytes(path))!;
        if (mutation == "hash") File.WriteAllText(Path.Combine(one, "catalog.json"), "tampered");
        if (mutation == "traversal") node["files"]!["../outside"] = JsonSerializer.SerializeToNode(new PluginFile(new string('0', 64), 1), Json);
        if (mutation == "credits") node["credits"]!["locales"]!.AsObject().Remove("ja");
        if (mutation == "base-version") node["dependencies"]![0]!["version"] = "bad";
        File.WriteAllText(path, node.ToJsonString());
        Assert.Throws<InvalidDataException>(() => OptionalPluginRegistry.Load(mutation == "duplicate-pack" ? [one, one] : [one, two]));
        Assert.Empty(OptionalPluginRegistry.Empty.Content.Packs);
    }

    [Fact]
    public async Task Server_serves_only_selected_verified_files_and_fences_websocket_join_by_complete_set_hash()
    {
        var registry = OptionalPluginRegistry.Load([Pack()]);
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(registry); builder.Services.AddSingleton(new NetworkOptions());
        builder.Services.AddSingleton(TestWorlds.SmallStartup());
        builder.Services.AddSingleton<NetworkSession>(); builder.Services.AddHostedService<SessionDriver>();
        await using var app = builder.Build(); app.MapNetwork(); app.MapPluginContent(); await app.StartAsync();
        using var handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
        using var http = new HttpClient(handler) { BaseAddress = new(app.Urls.Single()) };
        var bootstrap = await http.GetAsync("/api/bootstrap?protocolVersion=10&contentVersion=terrain-content-1");
        Assert.Equal(HttpStatusCode.OK, bootstrap.StatusCode);
        Assert.Equal(registry.Content.SetHash, bootstrap.Headers.GetValues("X-FreeTrain-Content-Set").Single());
        var descriptor = registry.Content.Packs.Single();
        Assert.Equal(descriptor.CatalogHash, Hash(await http.GetByteArrayAsync(descriptor.CatalogUrl)));
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync(descriptor.CatalogUrl.Replace("catalog.json", "prepared-pack.json"))).StatusCode);
        const string query = "/api/session?protocolVersion=10&contentVersion=terrain-content-1";
        using var rejected = new ClientWebSocket(); rejected.Options.Cookies = handler.CookieContainer;
        var socketBase = http.BaseAddress!.ToString().Replace("http:", "ws:");
        await Assert.ThrowsAsync<WebSocketException>(() => rejected.ConnectAsync(new(new Uri(socketBase), query), CancellationToken.None));
        using var accepted = new ClientWebSocket(); accepted.Options.Cookies = handler.CookieContainer;
        await accepted.ConnectAsync(new(new Uri(socketBase), query + "&packSetHash=" + registry.Content.SetHash), CancellationToken.None);
        Assert.Equal(WebSocketState.Open, accepted.State); accepted.Abort(); await app.StopAsync();
    }
}
