using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Server;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;
using Xunit.Abstractions;

namespace FreeTrain.Online.Tests;

[CollectionDefinition("Backend performance", DisableParallelization = true)]
public sealed class BackendPerformanceCollection;

[Collection("Backend performance")]
[Trait("Category", "BackendPerformance")]
public sealed class BackendPerformanceTests(ITestOutputHelper output)
{
    // The comparison runner overlays this harness onto older base commits too.
    // Bind once outside measurement, preserving their original socket encoding.
    private static readonly Func<WireMessage, ReadOnlyMemory<byte>> Encode =
        typeof(NetworkSession).Assembly.GetType("FreeTrain.Online.Server.OutboundWire")?
            .GetMethod("Serialize")?.CreateDelegate<Func<WireMessage, ReadOnlyMemory<byte>>>()
        ?? (message => JsonSerializer.SerializeToUtf8Bytes<WireMessage>(message, NetworkSession.Json));
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
    private sealed record ExpectedState(int Operations, string InitialSha256, string FinalSha256);
    private sealed record Expectations(int SchemaVersion, string CatalogHash, Dictionary<string, ExpectedState> Cases);
    private sealed record Measurement(string Name, int Operations, long AllocatedBytes, double ElapsedMs,
        string InitialSha256, string FinalSha256, Dictionary<string, long> Work)
    {
        public long BytesPerOperation => AllocatedBytes / Operations;
        public double MsPerOperation => ElapsedMs / Operations;
    }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static byte[] Save(MixedSessionState state) => SaveCodec.Write(state);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (MixedSessionState Current, List<WeakReference<MixedWorld>> Previous) AdvanceForRetentionCheck()
    {
        var state = BackendPerformanceFixture.Open("small");
        var previous = new List<WeakReference<MixedWorld>>();
        for (var i = 0; i < 32; i++)
        {
            var world = state.World;
            state.AdvanceOneMinute();
            if (!ReferenceEquals(world, state.World)) previous.Add(new(world));
        }
        return (state, previous);
    }

    [Fact]
    public void Live_successor_does_not_retain_historical_worlds()
    {
        // The no-inline helper removes predecessor locals from the stack before
        // collection. Keep the CURRENT session alive to catch history chains in
        // future validation certificates, indexes and caches.
        var (current, previous) = AdvanceForRetentionCheck();
        Assert.NotEmpty(previous);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.All(previous, reference => Assert.False(reference.TryGetTarget(out _),
            "The live session retains an obsolete mixed world."));
        GC.KeepAlive(current);
    }

    private static (long Bytes, double Ms) Measure(int count, Action operation)
    {
        // Every measured operation is synchronous. This excludes allocation by
        // xUnit, other threads and background GC, unlike process-wide counters.
        var start = Stopwatch.GetTimestamp();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < count; i++) operation();
        var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        return (bytes, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
    }

    private void ReportAndCheckState(Measurement result)
    {
        // Performance numbers are informational. Only deterministic behavior
        // determines pass/fail; write measurements even when a state check fails.
        var text = JsonSerializer.Serialize(result, Json);
        output.WriteLine(text);
        if (Environment.GetEnvironmentVariable("FREETRAIN_BACKEND_PERFORMANCE_OUTPUT") is { } directory)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, result.Name + ".json"), text + "\n");
        }
        var path = Path.Combine(AppContext.BaseDirectory, "Performance", "backend-expectations.json");
        var expectations = JsonSerializer.Deserialize<Expectations>(File.ReadAllText(path), Json)!;
        Assert.Equal(2, expectations.SchemaVersion);
        var catalogHash = PreparedContentCatalog.Bundled.CatalogHash;
        Assert.True(string.Equals(expectations.CatalogHash, catalogHash, StringComparison.Ordinal),
            $"Backend performance expectations target a different catalog. Expected {expectations.CatalogHash}; " +
            $"loaded {catalogHash}. Review the content change and follow the refresh procedure in " +
            "tests/fixtures/performance/README.md before updating the pinned save hashes.");
        var expected = expectations.Cases[result.Name];
        Assert.Equal(expected.Operations, result.Operations);
        Assert.Equal(expected.InitialSha256, result.InitialSha256);
        Assert.Equal(expected.FinalSha256, result.FinalSha256);
    }

    [Theory]
    [InlineData("small", 64, 1024)]
    [InlineData("large", 64, 55179)]
    [InlineData("midnight", 4, 1024)]
    public void Mixed_minutes_report_performance_and_preserve_pinned_continuation(string scene, int minutes, int owners)
    {
        var state = BackendPerformanceFixture.Open(scene);
        Assert.Equal(owners, state.World.Terrain.Spatial.Entities.Length);
        Assert.Equal(2, state.Fleet!.Trains.Length);
        Assert.Equal(2, state.World.Stations!.Buildings.Length);
        Assert.Equal(6, state.World.City!.State.Structures.Length);
        Assert.All(state.World.City.State.Structures, s => Assert.NotEmpty(s.Construction));
        var initial = Save(state);
        var initialMinute = state.Clock.Time.AbsoluteMinute;
        var initialMoves = state.Fleet.Trains.Sum(t => t.MoveCount);
        var heat = state.World.City.State.Heat;
        var pending = state.Clock.Capture().Pending;
        // Warm the actual branches on a detached copy of the SAME starting phase.
        var warmup = state.Freeze();
        for (var i = 0; i < 4; i++) warmup.AdvanceOneMinute();
        var measured = Measure(minutes, state.AdvanceOneMinute);
        var measuredMoves = state.Fleet!.Trains.Sum(t => t.MoveCount) - initialMoves;
        var final = Save(state);
        Assert.Equal(initialMinute + minutes, state.Clock.Time.AbsoluteMinute);
        if (scene == "midnight")
            Assert.All(state.Fleet!.Trains, t => Assert.True(t.StopCallCount > 0));
        else
        {
            Assert.All(state.Fleet!.Trains, t => Assert.True(t.MoveCount > 0));
            Assert.True(measuredMoves > 0);
        }
        Assert.NotSame(heat, state.World.City!.State.Heat);
        Assert.Equal(initialMinute + 602, state.World.City.State.HeatCallback!.DueMinute);
        Assert.Equal(16f, heat.Q[heat.Index(8, 8)]); // immutable predecessor remains intact
        var stationHours = pending.Where(a => a.HandlerId.StartsWith("station/", StringComparison.Ordinal) && a.Interval == 60);
        Assert.All(stationHours, before => Assert.Contains(state.Clock.Capture().Pending,
            after => after.HandlerId == before.HandlerId && after.DueMinute > before.DueMinute));
        if (scene == "midnight")
            Assert.All(pending.Where(a => a.HandlerId.StartsWith("station/", StringComparison.Ordinal) && a.Interval == 1440),
                before => Assert.Contains(state.Clock.Capture().Pending,
                    after => after.HandlerId == before.HandlerId && after.DueMinute == before.DueMinute + 1440));
        else
            Assert.Contains(pending.Where(a => a.HandlerId.StartsWith("city/construction/", StringComparison.Ordinal)),
                before => !state.Clock.Capture().Pending.Contains(before));

        // This checks reopening as well as the pinned pre-optimization hash.
        var reopened = SaveCodec.ReadWorld(final, TerrainProfiles.Resolve, RailwayProfiles.Resolve).Mixed!;
        Assert.True(final.AsSpan().SequenceEqual(Save(reopened)), "Exact save round trip changed.");
        for (var i = 0; i < 4; i++) { state.AdvanceOneMinute(); reopened.AdvanceOneMinute(); }
        Assert.True(Save(state).AsSpan().SequenceEqual(Save(reopened)), "Reopened continuation diverged.");
        ReportAndCheckState(new("minutes-" + scene, minutes, measured.Bytes, measured.Ms, Hash(initial), Hash(final), new() {
            ["naturalOwners"] = owners, ["minutes"] = minutes,
            ["trainMoves"] = measuredMoves }));
    }

    [Theory]
    [InlineData("small")]
    [InlineData("large")]
    public void Unchanged_world_replacement_reports_performance_and_preserves_payloads(string scene)
    {
        var state = BackendPerformanceFixture.Open(scene);
        var initial = Save(state);
        var world = state.World;
        MixedWorld? candidate = null;
        void Replace() => candidate = world.Replace(world.Revision, world.Terrain, world.Spatial.Capture(),
            world.Voxels, world.Slopes, world.SpeedLimits, world.Fleet, world.Stations, world.City);
        var replacements = scene == "small" ? 32 : 3;
        for (var i = 0; i < (scene == "small" ? 8 : 1); i++) Replace(); // warmup excluded
        var measured = Measure(replacements, Replace);
        Assert.Equal(world.Revision + 1, candidate!.Revision);
        Assert.Same(world.Terrain, candidate.Terrain);
        Assert.Same(world.City, candidate.City);
        Assert.True(world.Spatial.Entities.SequenceEqual(candidate.Spatial.Entities));
        var successor = new MixedSessionState(new(candidate, state.Construction.Accounts), state.Provenance,
            clock: state.Clock.Capture(), random: state.Random.Capture(), revision: state.Revision + 1);
        Assert.True(initial.AsSpan().SequenceEqual(Save(state)), "Replacement mutated the predecessor.");
        ReportAndCheckState(new("replace-" + scene, replacements, measured.Bytes, measured.Ms, Hash(initial), Hash(Save(successor)),
            new() { ["naturalOwners"] = world.Terrain.Spatial.Entities.Length }));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(4, false)]
    [InlineData(1, true)]
    [InlineData(4, true)]
    public async Task Publication_reports_performance_and_preserves_peer_state(int peerCount, bool running)
    {
        var state = BackendPerformanceFixture.Open("large");
        var initial = Save(state);
        using var host = new SessionHost(state, "owner", TerrainProfiles.Resolve, RailwayProfiles.Resolve);
        var session = new NetworkSession(new(), hostFactory: (_, _) => host);
        var peers = Enumerable.Range(0, peerCount).Select(i => session.Connect(Now, new("perf-" + i))!).ToArray();
        var received = new int[peerCount];
        var revisions = new long[peerCount];
        for (var i = 0; i < peers.Length; i++)
            revisions[i] = Assert.IsType<JoinMessage>(await peers[i].Output.Reader.ReadAsync()).Snapshot.Revision;
        var heatReferences = 0;
        var fullHeat = 0;
        var invalidReferences = 0;
        var invalidFullHeat = 0;
        var invalidBases = 0;
        var heatLength = state.World.City!.State.Heat.Q.Length;
        long wireBytes = 0, maxReferenceBytes = 0;
        var now = Now;
        void Tick()
        {
            now += TimeSpan.FromMilliseconds(50);
            session.Tick(now);
            for (var i = 0; i < peers.Length; i++)
                while (peers[i].Output.Reader.TryRead(out var message))
                {
                    // Same encoding used by the socket worker, with no network
                    // scheduling or wall-clock sleep inside the regression test.
                    var encoded = Encode(message);
                    wireBytes += encoded.Length;
                    if (message is not DeltaMessage delta) continue;
                    received[i]++;
                    if (delta.BaseRevision != revisions[i]) invalidBases++;
                    revisions[i] = delta.Revision;
                    if (delta.CityHeatBaseRevision != null)
                    {
                        heatReferences++;
                        var heat = delta.Mixed!.City!.Heat;
                        if (heat.Q.Length != 0 || heat.Back.Length != 0 || heat.Rho.Length != 0) invalidReferences++;
                        maxReferenceBytes = Math.Max(maxReferenceBytes, encoded.Length);
                    }
                    else if (delta.Mixed?.City is { } city)
                    {
                        fullHeat++;
                        if (city.Heat.Q.Length != heatLength || city.Heat.Back.Length != heatLength ||
                            city.Heat.Rho.Length != heatLength) invalidFullHeat++;
                    }
                }
        }
        // Warm snapshots and queue polling without advancing the starting phase.
        // The run command below also warms delta encoding outside measurement.
        for (var i = 0; i < 32; i++) Tick();
        if (running)
        {
            var command = session.Submit(peers[0], new("run", host.WorldEpoch, new RunCommand()), now);
            Tick();
            Assert.IsType<AcceptedMessage>(await command);
        }
        // A run command may commit between publication slots. Preserve each
        // peer's last RECEIVED revision, rather than the latest host revision.
        Array.Clear(received);
        heatReferences = fullHeat = invalidReferences = invalidFullHeat = invalidBases = 0;
        wireBytes = maxReferenceBytes = 0;
        var ticks = running ? 17 : 128; // Finish on a publication slot in both cases.
        var measured = Measure(ticks, Tick);
        foreach (var peer in peers) Assert.False(peer.Stop.IsCancellationRequested);
        var snapshot = host.Snapshot();
        if (running)
        {
            Assert.All(received, count => Assert.True(count > 0));
            Assert.All(received, count => Assert.Equal(received[0], count));
            Assert.All(revisions, revision => Assert.Equal(snapshot.Revision, revision));
            Assert.Equal(peerCount, fullHeat); // diffusion must be sent once to every peer
            Assert.Equal(0, invalidReferences);
            Assert.Equal(0, invalidFullHeat);
            Assert.Equal(0, invalidBases);
            Assert.True(heatReferences >= peerCount);
            Assert.True(maxReferenceBytes > 0);
        }
        else
        {
            Assert.All(received, count => Assert.Equal(0, count));
            Assert.Equal(0, wireBytes);
        }
        var capture = host.CaptureSave(); host.Pump(0); var final = await capture;
        ReportAndCheckState(new($"{(running ? "publish" : "paused")}-{peerCount}-peer{(peerCount == 1 ? "" : "s")}",
            ticks, measured.Bytes, measured.Ms, Hash(initial), Hash(final), new() {
                ["peers"] = peerCount, ["deltas"] = received.Sum(), ["heatReferences"] = heatReferences,
                ["fullHeat"] = fullHeat, ["wireBytes"] = wireBytes, ["maxReferenceBytes"] = maxReferenceBytes }));
    }
}
