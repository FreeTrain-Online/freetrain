using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using Xunit;

public class PreparedContentTests
{
    private static JsonNode Catalog()
    {
        using var stream = typeof(PreparedContentCatalog).Assembly.GetManifestResourceStream("BundledCatalog")!;
        return JsonNode.Parse(stream)!;
    }
    private static PreparedContentCatalog Read(JsonNode node)
    {
        var bytes = Encoding.UTF8.GetBytes(node.ToJsonString());
        return PreparedContentCatalog.Read(bytes, Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    [Fact]
    public void Bundled_registry_preserves_exact_original_ids_and_is_not_a_playable_profile()
    {
        var catalog = PreparedContentCatalog.Bundled;
        Assert.Equal("exav-stable-bundled", catalog.PackId);
        Assert.Equal(448, catalog.Definitions.Length); Assert.Equal(159, catalog.Pictures.Length);
        Assert.Equal(159, catalog.Pictures.Count(p => p.HasPreparedVariants));
        Assert.Equal(813, catalog.RenderRecipes.Length);
        Assert.All(catalog.Definitions, d => Assert.Contains(catalog.RenderRecipes, r => r.Id == d.Id));
        Assert.Equal(54, catalog.PluginOrder.Length); Assert.False(catalog.GameplayAvailable);
        var generated = catalog.FindDefinition(catalog.PackId, "null-contrib-0:0");
        Assert.NotNull(generated); Assert.Equal("generatedStructure", generated.Type);
        Assert.Null(catalog.FindDefinition(catalog.PackId.ToUpperInvariant(), generated.Id));
        Assert.Null(catalog.FindDefinition(catalog.PackId, generated.Id.ToUpperInvariant()));
        Assert.Null(catalog.FindDefinition("optional-pack", generated.Id));
        Assert.Null(catalog.FindDefinition(catalog.PackId, "not-present"));
        Assert.Null(WorldProfiles.Resolve(ProtocolIdentity.ReferenceRuleset, ProtocolIdentity.ReferenceContent,
            [new(catalog.PackId, catalog.Version, catalog.CatalogHash)]));
    }

    [Fact]
    public void Catalog_hash_and_owned_immutable_data_survive_disposed_json_and_multiple_reads()
    {
        var node = Catalog(); var catalog = Read(node);
        var original = catalog.Definitions[0].Declaration.GetRawText();
        node["contributions"]![0]!["declaration"]!["text"] = "changed";
        Assert.Equal(original, catalog.Definitions[0].Declaration.GetRawText());
        Assert.Throws<ArgumentException>(() => PreparedContentCatalog.Read(Encoding.UTF8.GetBytes(node.ToJsonString()), catalog.CatalogHash));
        Assert.Same(PreparedContentCatalog.Bundled, PreparedContentCatalog.Bundled);
    }

    [Theory]
    [InlineData("duplicate-definition")][InlineData("duplicate-picture")][InlineData("unknown-pack")]
    [InlineData("unknown-source")][InlineData("broken-dependency")][InlineData("broken-order")]
    [InlineData("missing-selection")][InlineData("runtime-enabled")]
    [InlineData("missing-recipe")][InlineData("missing-render-picture")][InlineData("missing-render-surface")]
    public void Malformed_registry_never_publishes_partial_or_fallback_content(string mutation)
    {
        var node = Catalog();
        switch (mutation)
        {
            case "duplicate-definition": node["contributions"]!.AsArray().Add(node["contributions"]![0]!.DeepClone()); break;
            case "duplicate-picture": node["pictures"]!.AsArray().Add(node["pictures"]![0]!.DeepClone()); break;
            case "unknown-pack": node["packId"] = "optional-pack"; break;
            case "unknown-source": node["source"]!["revision"] = "unreviewed"; break;
            case "broken-dependency": node["plugins"]![0]!["dependencies"]!.AsArray().Add("missing"); break;
            case "broken-order": node["pluginOrder"]![1] = node["pluginOrder"]![0]!.GetValue<string>(); break;
            case "missing-selection": node["selections"]![0]!["target"] = "missing"; break;
            case "missing-recipe": node["renderRecipes"]!.AsArray().RemoveAt(0); break;
            case "missing-render-picture": node["renderRecipes"]!.AsArray().First(r => r!["frames"]!.AsArray().Count > 0)!["frames"]![0]!["pictureId"] = "missing"; break;
            case "missing-render-surface": node["surfaceSets"]!.AsObject().Clear(); break;
            case "runtime-enabled": node["contributions"]![0]!["status"] = "implemented"; break;
        }
        Assert.Throws<ArgumentException>(() => Read(node));
        Assert.Equal(448, PreparedContentCatalog.Bundled.Definitions.Length);
    }
}
