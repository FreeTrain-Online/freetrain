using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Content;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation.Prototype;
using Xunit;

public class SpatialIntegrationTests
{
    public static IEnumerable<object[]> Vectors()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Spatial/spatial-vectors.json")));
        return document.RootElement.GetProperty("cases").EnumerateArray().Select(row => new object[] { row.GetRawText() }).ToArray();
    }

    [Theory, MemberData(nameof(Vectors))]
    public void Shared_source_vectors_match_authoritative_spatial_query(string json)
    {
        using var vector = JsonDocument.Parse(json);
        var row = vector.RootElement;
        var options = WireJson.CreateOptions();
        var map = row.GetProperty("map").Deserialize<MapRead>(options)!;
        // A geometry-only source fixture, not a playable canonical scenario or installed content pack.
        var profile = map.CoordinateSystem == "hv" ? WorldProfiles.Reference : WorldProfiles.Prototype;
        var snapshot = new SnapshotMessage(1, 0, 0, true, 0, map, [], [], [], [], profile.Ruleset, profile.ContentVersion, profile.Packs.ToArray());
        var query = row.GetProperty("query").Deserialize<SpatialQuery>(options)!;
        var actual = JsonSerializer.SerializeToNode(SpatialQueries.Preview(snapshot, query), options)!;
        foreach (var field in row.GetProperty("expected").EnumerateObject())
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(field.Value.GetRawText()), actual[field.Name]), row.GetProperty("id") + ": " + field.Name);
    }

    private static SessionHost Host() => new(ReferenceScenario.Create(), "owner", [() => new AutomaticController()]);

    [Fact]
    public void Preview_does_not_mutate_spend_schedule_or_share_session_state()
    {
        var first = Host(); var second = Host();
        var before = JsonSerializer.Serialize(first.ReadState());
        var preview = first.PreviewSpatial(new(1, new(12, 25, 1), new(10, 20, 1), 8));
        Assert.Equal(new PositionRead(12, 22, 1), preview.Position);
        Assert.Equal(3, preview.Heading); Assert.True(preview.Inside);
        Assert.Equal(before, JsonSerializer.Serialize(first.ReadState()));
        Assert.Equal(before, JsonSerializer.Serialize(second.ReadState()));
        Assert.Equal(0, first.Pump().AppliedCommands);
        Assert.Throws<ArgumentException>(() => first.PreviewSpatial(new(-1, new(12, 25, 1), null, 0)));
    }

    [Theory]
    [InlineData("stale")]
    [InlineData("overflow")]
    [InlineData("level")]
    [InlineData("mode")]
    [InlineData("anchor")]
    public void Unsupported_queries_fail_without_changing_the_world(string problem)
    {
        var host = Host(); var before = JsonSerializer.Serialize(host.ReadState());
        var query = new SpatialQuery(1, new(12, 25, 1), new(10, 20, 1), 8);
        query = problem switch
        {
            "stale" => query with { ExpectedWorldEpoch = 2 },
            "overflow" => query with { Position = new(int.MaxValue, 0, 1) },
            "level" => query with { Anchor = new(10, 20, 0) },
            "mode" => query with { Snap = 5 },
            _ => query with { Anchor = null }
        };
        var expectedType = problem switch
        {
            "stale" => typeof(InvalidOperationException),
            "overflow" => typeof(OverflowException),
            _ => typeof(ArgumentException)
        };
        Assert.Throws(expectedType, () => host.PreviewSpatial(query));
        Assert.Equal(before, JsonSerializer.Serialize(host.ReadState()));
    }

    [Fact]
    public void Prototype_identity_catalog_hash_and_car_headings_are_explicit()
    {
        var snapshot = Host().Snapshot();
        Assert.Equal(ProtocolIdentity.PrototypeRuleset, snapshot.Ruleset);
        Assert.Equal("prototypeXY", snapshot.Map.CoordinateSystem);
        Assert.Equal(2, snapshot.Trains[0].Heading);
        Assert.Equal([2, 2, 2], snapshot.Trains[0].CarHeadings);
        var catalog = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Spatial/sprites.json"));
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(catalog)), Assert.Single(snapshot.RequiredPacks).CatalogHash);
        var profile = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Spatial/prototype-profile.json")))!;
        var actual = JsonSerializer.SerializeToNode(snapshot, WireJson.CreateOptions())!;
        foreach (var key in new[] { "ruleset", "contentVersion", "requiredPacks" }) Assert.True(JsonNode.DeepEquals(profile[key], actual[key]));
    }
}
