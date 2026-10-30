using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Simulation;
using Xunit;

public class WorldCreationTests
{
    private static readonly string Hash = new('a', 64);
    private static readonly ContentBinding Terrain = new("test-pack", "1", Hash, "terrain");
    private static readonly ContentBinding Tree = new("test-pack", "1", Hash, "tree");
    private static readonly CreationContent Content = new(Hash, [new(Terrain, CreationCapabilityKind.Terrain, true, true),
        new(Tree, CreationCapabilityKind.Forest, true, true)]);
    private static readonly CreationFence Fence = new("session", "participant", "connection", 1, 4, Hash);
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    private static HeightmapInput Image => new(Hash, "image/png", 1024, 1, 1, 1);

    [Fact]
    public void Defaults_are_explicit_stored_dimensions_and_requests_roundtrip_without_aliasing()
    {
        var empty = WorldCreationProfile.Empty("  名前  ");
        Assert.Equal("ab06507b54ca7d3d55f229173aa6ee5d03e313e56355895f8a8bbb4c6d841198",
            WorldCreationProfile.Fingerprint(WorldCreationProfile.Empty()));
        Assert.Equal(new GenerationBudget(1260, 7560, 2550784, 3225600, 70576), WorldCreationProfile.Estimate(empty));
        Assert.Equal(new(30, 42, 6), empty.Dimensions); Assert.Equal(1, empty.WaterLevel);
        Assert.Equal("  名前  ", empty.Name);
        Assert.Equal(new(64, 64, 16), WorldCreationProfile.Size(WorldSizePreset.Compact));
        Assert.Equal(new(128, 128, 32), WorldCreationProfile.Size(WorldSizePreset.Standard));
        var generated = WorldCreationProfile.Generated();
        Assert.Equal(new(64, 64, 16), generated.Dimensions); Assert.Equal(4, generated.WaterLevel);
        Assert.Equal(new(ReliefKind.Hills, 50, 24, 50), generated.Options.Relief);
        foreach (var request in new WorldCreationRequest[] { empty, generated, WorldCreationProfile.Heightmap(Image) })
        {
            Assert.Empty(WorldCreationProfile.Validate(request));
            var json = JsonSerializer.Serialize(request);
            var copy = JsonSerializer.Deserialize<WorldCreationRequest>(json)!;
            Assert.Equal(json, JsonSerializer.Serialize(copy));
            Assert.Equal(WorldCreationProfile.Fingerprint(request), WorldCreationProfile.Fingerprint(copy));
            Assert.NotEqual(WorldCreationProfile.Fingerprint(request), WorldCreationProfile.Fingerprint(request with { Name = "different" }));
        }
        var forest = generated with { Options = generated.Options with { Forests = new(25, 50, [Tree]) } };
        Assert.NotEqual(WorldCreationProfile.Fingerprint(generated), WorldCreationProfile.Fingerprint(forest));
        Assert.Empty(Content.Validate(forest));
    }

    [Theory]
    [InlineData(1, 1, 1, 0, true)]
    [InlineData(128, 128, 32, 1, true)]
    [InlineData(128, 128, 64, 1, true)]
    [InlineData(256, 256, 64, 1, true)]
    [InlineData(1500, 1090, 45, 1, true)]
    [InlineData(2048, 2048, 16, 1, false)]
    [InlineData(0, 2, 4, 0, false)]
    [InlineData(-1, 2, 4, 0, false)]
    [InlineData(2049, 2, 4, 0, false)]
    [InlineData(int.MaxValue, int.MaxValue, int.MaxValue, 0, false)]
    [InlineData(2, 2, 65, 0, false)]
    [InlineData(2, 2, 4, -1, false)]
    [InlineData(2, 2, 4, 4, false)]
    public void Size_water_overflow_and_work_limits_are_checked_before_allocation(int h, int v, int z, int water, bool valid)
    {
        var r = WorldCreationProfile.Empty() with { Dimensions = new(h, v, z), WaterLevel = water };
        Assert.Equal(valid, WorldCreationProfile.Validate(r).IsEmpty);
        if (!valid) Assert.Throws<ArgumentException>(() => WorldCreationProfile.Estimate(r));
    }

    [Theory]
    [InlineData("tiny")][InlineData("headroom")][InlineData("coast-missing")][InlineData("inland-sides")]
    [InlineData("island-sides")][InlineData("coverage")][InlineData("river-direction")]
    [InlineData("lakes")][InlineData("space")][InlineData("forest-missing")]
    [InlineData("forest-duplicate")][InlineData("forest-default")][InlineData("relief")][InlineData("null-options")]
    [InlineData("extreme-features")][InlineData("null-relief")][InlineData("enum")][InlineData("start-area")][InlineData("river-width")]
    public void Contradictory_generated_options_fail_without_clamping(string mutation)
    {
        var r = WorldCreationProfile.Generated(); var o = r.Options;
        r = mutation switch
        {
            "extreme-features" => r with { Options = o with { Lakes = new(int.MaxValue, int.MaxValue), Rivers = new((RiverDirection)int.MaxValue, int.MaxValue, int.MaxValue) } },
            "tiny" => r with { Dimensions = new(15, 64, 16) },
            "headroom" => r with { WaterLevel = 13 },
            "coast-missing" => r with { Options = o with { Coast = new(CoastKind.Sides, CoastSides.None, 25) } },
            "inland-sides" => r with { Options = o with { Coast = new(CoastKind.Inland, CoastSides.North, 0) } },
            "island-sides" => r with { Options = o with { Coast = new(CoastKind.Island, CoastSides.East, 30) } },
            "coverage" => r with { Options = o with { Coast = new(CoastKind.Inland, CoastSides.None, 20) } },
            "river-direction" => r with { Options = o with { Rivers = new((RiverDirection)999, 1, 30) } },
            "lakes" => r with { Options = o with { Lakes = new(0, 5) } },
            "space" => r with { Options = o with { Lakes = new(8, 20) } },
            "forest-missing" => r with { Options = o with { Forests = new(25, 50, []) } },
            "forest-duplicate" => r with { Options = o with { Forests = new(25, 50, [Tree, Tree]) } },
            "forest-default" => r with { Options = o with { Forests = new(0, 50, default) } },
            "relief" => r with { Options = o with { Relief = new(ReliefKind.Plains, -1, 1, 101) } },
            "null-options" => r with { Options = null! },
            "null-relief" => r with { Options = o with { Relief = null! } },
            "enum" => r with { Options = o with { Coast = new((CoastKind)999, (CoastSides)16, 10) } },
            "start-area" => r with { Options = o with { StartingArea = 2000 } },
            "river-width" => r with { Options = o with { Rivers = new(RiverDirection.NorthSouth, 9, 30) } },
            _ => throw new ArgumentException()
        };
        Assert.NotEmpty(WorldCreationProfile.Validate(r));
        Assert.Throws<ArgumentException>(() => WorldCreationProfile.Fingerprint(r));
    }

    [Fact]
    public void All_compass_sides_and_inland_channel_choices_are_representable()
    {
        var r = WorldCreationProfile.Generated();
        foreach (var sides in Enumerable.Range(1, 15).Select(n => (CoastSides)n))
            Assert.Empty(WorldCreationProfile.Validate(r with { Options = r.Options with { Coast = new(CoastKind.Sides, sides, 25) } }));
        Assert.Empty(WorldCreationProfile.Validate(r with { Options = r.Options with { Rivers = new(RiverDirection.NorthSouth, 1, 50) } }));
        Assert.Empty(WorldCreationProfile.Validate(r with { Options = r.Options with { Lakes = new(1, 5), Rivers = new(RiverDirection.EastWest, 1, 50) } }));
        Assert.Empty(WorldCreationProfile.Validate(r with { Options = r.Options with { Coast = new(CoastKind.Island, CoastSides.None, 30), Rivers = new(RiverDirection.NorthSouth, 1, 50) } }));
    }

    [Theory]
    [InlineData("digest")][InlineData("bytes")][InlineData("pixels")][InlineData("overflow")]
    [InlineData("animation")][InlineData("alpha")][InlineData("format")][InlineData("null")][InlineData("provider")]
    public void Image_metadata_does_not_authorize_unbounded_decode(string mutation)
    {
        var image = mutation switch
        {
            "digest" => Image with { Sha256 = "bad" }, "bytes" => Image with { EncodedBytes = 8_388_609 },
            "pixels" => Image with { Width = 4096, Height = 4096 },
            "overflow" => Image with { Width = int.MaxValue, Height = int.MaxValue },
            "animation" => Image with { Frames = 2 }, "alpha" => Image with { HasTransparency = true }, "format" => Image with { MediaType = "image/svg+xml" },
            "null" => null!, "provider" => Image with { Provider = Tree with { CatalogHash = "bad" } },
            _ => throw new ArgumentException()
        };
        Assert.NotEmpty(WorldCreationProfile.Validate(WorldCreationProfile.Heightmap(image)));
        Assert.Empty(WorldCreationProfile.Validate(WorldCreationProfile.Heightmap(Image with { Width = 2048, Height = 2048, EncodedBytes = 8_388_608 }) with { Dimensions = new(32,32,16) }));
    }

    [Fact]
    public void Names_and_malformed_requests_are_bounded()
    {
        Assert.NotEmpty(WorldCreationProfile.Validate(null));
        Assert.NotEmpty(WorldCreationProfile.Validate(WorldCreationProfile.Empty() with { Dimensions = null! }));
        Assert.NotEmpty(WorldCreationProfile.Validate(WorldCreationProfile.Empty() with { Profile = "future" }));
        foreach (var name in new[] { null, new string('a', 129), "line\nbreak" })
            Assert.NotEmpty(WorldCreationProfile.Validate(WorldCreationProfile.Empty() with { Name = name! }));
        Assert.Empty(WorldCreationProfile.Validate(WorldCreationProfile.Empty("")));
    }

    // Literal oracles computed independently with Python hashlib + int.from_bytes.
    [Theory]
    [InlineData(0, new[] { 293479539, 1988742517, 2046890731, 531666846, 1184918920, 1794192551, 1459198540, 800645966 })]
    [InlineData(int.MinValue, new[] { 767482897, 290019996, 2127281518, 1817083203, 343434129, 1478173813, 1699632393, 60921996 })]
    [InlineData(int.MaxValue, new[] { 1514756221, 1249359101, 1085012439, 2065558181, 1406882327, 1485986840, 1909550539, 534661802 })]
    public void Generation_seeds_are_frozen_and_independent_of_gameplay(int seed, int[] expected)
    {
        var gameplay = new RandomStreams(seed);
        var before = JsonSerializer.Serialize(gameplay.Capture());
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            Assert.Equal(expected, Enum.GetValues<GenerationStage>().Select(s => WorldCreationProfile.StageSeed(seed, s)));
            Assert.Equal(expected, Enum.GetValues<GenerationStage>().Select(s => WorldCreationProfile.StageSeed(seed, s)));
        }
        finally { CultureInfo.CurrentCulture = culture; }
        Assert.Equal(before, JsonSerializer.Serialize(gameplay.Capture()));
        Assert.Equal(1918789643, WorldCreationProfile.StageSeed(0, GenerationStage.Relief, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => WorldCreationProfile.StageSeed(seed, GenerationStage.Relief, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => WorldCreationProfile.StageSeed(seed, (GenerationStage)999));
    }

    [Fact]
    public void Prepared_art_is_not_runtime_permission_and_missing_pack_hashes_are_exact()
    {
        var bundled = WorldCreationCapabilities.Inspect(PreparedContentCatalog.Bundled);
        Assert.Equal(18, bundled.Capabilities.Length);
        Assert.All(bundled.Capabilities, c => { Assert.True(c.Prepared); Assert.False(c.RuntimeReady); });
        Assert.Contains(bundled.Validate(WorldCreationProfile.Empty()), i => i.Code == "terrain-unavailable");
        var r = WorldCreationProfile.Generated();
        r = r with { Options = r.Options with { Forests = new(25, 50, [Tree with { CatalogHash = new('b', 64) }]) } };
        Assert.Contains(Content.Validate(r), i => i.Code == "content-missing");
        Assert.Throws<ArgumentException>(() => WorldCreationJob.Create(Guid.NewGuid(), WorldCreationProfile.Empty(), Fence with { CatalogSetHash = bundled.CatalogSetHash }, bundled, Now));
        Assert.NotEmpty((Content with { Capabilities = default }).Validate(r));
        Assert.NotEmpty((Content with { Capabilities = [Content.Capabilities[0], Content.Capabilities[0]] }).Validate(r));
        var provider = WorldCreationProfile.Heightmap(Image with { Provider = Tree });
        Assert.Contains(Content.Validate(provider), i => i.Code == "content-missing");
    }

    [Fact]
    public void Work_meter_bounds_total_work_and_honors_cancellation_before_spending()
    {
        var meter = new GenerationWork(WorldCreationProfile.Empty() with { Dimensions = new(1, 1, 1), WaterLevel = 0 });
        Assert.Equal(1280, meter.Remaining);
        meter.Spend(1279);
        Assert.Throws<InvalidOperationException>(() => meter.Spend(2));
        Assert.Equal(1, meter.Remaining);
        meter.Spend(1);
        Assert.Throws<InvalidOperationException>(() => meter.Spend(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => meter.Spend(4097));
        using var cancellation = new CancellationTokenSource();
        var cancelled = new GenerationWork(WorldCreationProfile.Generated(), cancellation.Token);
        var before = cancelled.Remaining;
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => cancelled.Spend(1));
        Assert.Equal(before, cancelled.Remaining);
    }

    private static WorldCreationJob Job() => WorldCreationJob.Create(Guid.NewGuid(), WorldCreationProfile.Generated(), Fence, Content, Now);
    [Fact]
    public void Exact_candidate_commit_is_owner_fenced_single_use_and_preserves_original_record()
    {
        var queued = Job(); var running = queued.Start(Now, Fence).Report(30, Now, Fence);
        var ready = running.Complete(running.RequestHash, Hash, 1024, Now, Fence);
        Assert.Equal(CreationJobStatus.Queued, queued.Status);
        Assert.Equal(100, ready.Progress);
        Assert.Throws<UnauthorizedAccessException>(() => ready.Commit(ready.RequestHash, Hash, false, Now, Fence));
        Assert.Throws<ArgumentException>(() => ready.Commit(ready.RequestHash, new('b', 64), true, Now, Fence));
        Assert.Throws<ArgumentException>(() => ready.Commit(new('b', 64), Hash, true, Now, Fence));
        var committed = ready.Commit(ready.RequestHash, Hash, true, Now, Fence);
        Assert.Equal(CreationJobStatus.Committed, committed.Status);
        Assert.Throws<InvalidOperationException>(() => committed.Commit(committed.RequestHash, Hash, true, Now, Fence));
        Assert.Throws<InvalidOperationException>(() => committed.Cancel(Now, Fence));
        Assert.Throws<ArgumentOutOfRangeException>(() => running.Report(29, Now, Fence));
        Assert.Throws<ArgumentOutOfRangeException>(() => running.Report(100, Now, Fence));
        Assert.Throws<ArgumentException>(() => running.Complete("wrong-request", Hash, 1, Now, Fence));
        Assert.Throws<ArgumentException>(() => running.Complete(running.RequestHash, Hash, long.MaxValue, Now, Fence));
    }

    [Fact]
    public void Cancellation_failures_expiry_and_changed_fences_reject_late_results()
    {
        var job = Job(); var running = job.Start(Now, Fence);
        foreach (var stale in new[] { Fence with { Epoch = 2 }, Fence with { Revision = 5 }, Fence with { SessionId = "other" },
            Fence with { ParticipantId = "other" }, Fence with { ConnectionId = "new" }, Fence with { CatalogSetHash = new('b', 64) } })
            Assert.Throws<InvalidOperationException>(() => running.Complete(running.RequestHash, Hash, 1, Now, stale));
        foreach (var terminal in new[] { job.Cancel(Now, Fence), running.Cancel(Now, Fence), running.Fail("work-limit", Now, Fence), running.Expire(Now.AddSeconds(600)) })
            Assert.Throws<InvalidOperationException>(() => terminal.Complete(terminal.RequestHash, Hash, 1, Now, Fence));
        Assert.Throws<InvalidOperationException>(() => running.Complete(running.RequestHash, Hash, 1, Now.AddSeconds(600), Fence));
        Assert.Throws<InvalidOperationException>(() => running.Expire(Now));
        var ready = running.Complete(running.RequestHash, Hash, 1, Now, Fence);
        Assert.Null(ready.Cancel(Now, Fence).CandidateHash);
        Assert.Null(ready.Expire(Now.AddSeconds(600)).CandidateHash);
    }
}
