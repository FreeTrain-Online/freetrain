using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Server;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

public class RandomTests
{
    [Theory]
    [InlineData(0, new[] {1559595546,1755192844,1649316166,1198642031,442452829,1200195957})]
    [InlineData(1, new[] {534011718,237820880,1002897798,1657007234,1412011072,929393559})]
    [InlineData(-1, new[] {534011718,237820880,1002897798,1657007234,1412011072,929393559})]
    [InlineData(int.MaxValue, new[] {1559595546,1755192844,1649316172,1198642031,442452829,1200195955})]
    [InlineData(int.MinValue, new[] {1559595546,1755192844,1649316172,1198642031,442452829,1200195955})]
    public void Independent_signed_Int32_initialization_vectors(int seed, int[] expected)
    {
        var random = new GameRandom(seed);
        Assert.Equal(expected, Enumerable.Range(0, expected.Length).Select(_ => random.Next()));
    }

    [Theory]
    [InlineData(0)][InlineData(1)][InlineData(-2147483648)][InlineData(2147483647)][InlineData(161803399)]
    public void Frozen_operations_match_seeded_runtime_reference_over_table_wraps(int seed)
    {
        // Supplementary comparison; the literal vectors above do not depend on this oracle.
        var actual = new GameRandom(seed); var expected = new Random(seed);
        for (var i = 0; i < 200; i++)
        {
            Assert.Equal(expected.Next(), actual.Next());
            Assert.Equal(expected.Next(0), actual.Next(0));
            Assert.Equal(expected.Next(17), actual.Next(17));
            Assert.Equal(expected.Next(-9, -9), actual.Next(-9, -9));
            Assert.Equal(expected.Next(-99, 41), actual.Next(-99, 41));
            Assert.Equal(expected.Next(int.MinValue, int.MaxValue), actual.Next(int.MinValue, int.MaxValue));
            Assert.Equal(expected.NextDouble(), actual.NextDouble());
        }
    }

    [Fact]
    public void Scaling_and_large_range_consumption_have_literal_expectations()
    {
        var random = new GameRandom(0);
        Assert.Equal(0.7262432699679598, random.NextDouble());
        Assert.Equal(8, random.Next(10));
        Assert.Equal(53, random.Next(-100,100));
        Assert.Equal(7, random.Next(7,7)); // Still draws.
        Assert.Equal(0, random.Next(0)); // Still draws.
        Assert.Equal(1200195957, random.Next());
        var large = new GameRandom(0);
        Assert.Equal(-1559595548, large.Next(int.MinValue, int.MaxValue)); // Exactly two draws.
        Assert.Equal(1649316166, large.Next());
        var before = random.Capture();
        Assert.Throws<ArgumentOutOfRangeException>(() => random.Next(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => random.Next(2,1));
        Assert.Equal(before.Index, random.Capture().Index);
        Assert.Equal(before.Values.ToArray(), random.Capture().Values.ToArray());
    }

    [Fact]
    public void Versioned_seed_derivation_has_independent_SHA256_vectors()
    {
        int[] expected = [243598805,860615389,1126851233,1665008976,1721242361,
            482792135,58605309,547054292,1691091256,1900910394];
        Assert.Equal(expected, RandomStreams.Ids.Select(id => RandomStreams.DeriveSeed(0,id)));
        Assert.Equal(10, expected.Distinct().Count());
        Assert.Throws<ArgumentException>(() => RandomStreams.DeriveSeed(0,"Development"));
    }

    [Fact]
    public void Capture_restore_owns_its_arrays_and_preserves_every_stream_over_multiple_wraps()
    {
        var bank = new RandomStreams(-42);
        foreach (var (id,i) in RandomStreams.Ids.Select((id,i) => (id,i)))
            for (var j = 0; j < i * 19; j++) bank[id].Next();
        var state = bank.Capture();
        var json = JsonSerializer.SerializeToUtf8Bytes(state, WireJson.CreateOptions());
        var restored = RandomStreams.Restore(JsonSerializer.Deserialize<RandomStreamsState>(json,WireJson.CreateOptions())!);
        foreach (var id in RandomStreams.Ids)
            for (var i = 0; i < 150; i++) Assert.Equal(bank[id].Next(),restored[id].Next());
        Assert.Equal(json, JsonSerializer.SerializeToUtf8Bytes(state, WireJson.CreateOptions()));
        var copy = RandomStreams.Restore(state);
        copy["development"].Next();
        Assert.Equal(json, JsonSerializer.SerializeToUtf8Bytes(state, WireJson.CreateOptions()));
    }

    [Fact]
    public void Interleaving_streams_and_sessions_never_consumes_another_generator()
    {
        var worldA = SpatialReferenceScenario.Create(); var worldB = SpatialReferenceScenario.Create();
        var a = new SessionHost(worldA,"owner"); var b = new SessionHost(worldB,"owner");
        var untouched = SaveCodec.Write(worldB);
        var expected = new RandomStreams();
        for (var i = 0; i < 200; i++)
        {
            worldA.Random["construction"].NextDouble();
            worldA.Random["stock-economy"].Next();
            Assert.Equal(expected["development"].Next(),worldA.Random["development"].Next());
        }
        Assert.Equal(untouched, SaveCodec.Write(worldB));
        Assert.Equal(a.Snapshot().Tick,b.Snapshot().Tick);
        var before = worldA.Random.Capture();
        worldA.AdvanceOneMinute(); // No gameplay random consumers are registered yet.
        Assert.Equal(JsonSerializer.Serialize(before),JsonSerializer.Serialize(worldA.Random.Capture()));
        Assert.Throws<ArgumentException>(() => worldA.Random["missing"].Next());
    }

    [Fact]
    public void Creek_endpoint_seed_is_repeatable_symmetric_and_independent_of_world_streams()
    {
        var forward = RandomStreams.ForCreek(1,2,3,4);
        var reverse = RandomStreams.ForCreek(3,4,1,2);
        Assert.Equal(new GameRandom(262).Next(),forward.Next());
        reverse.Next();
        for (var i=0;i<100;i++) Assert.Equal(forward.Next(2),reverse.Next(2));
        Assert.Equal(new GameRandom(unchecked((int.MaxValue*128+int.MinValue) ^ (int.MinValue*128+int.MaxValue))).Next(),
            RandomStreams.ForCreek(int.MaxValue,int.MinValue,int.MinValue,int.MaxValue).Next());
    }

    [Fact]
    public void Pending_scheduler_actions_rebind_to_restored_random_streams_in_FIFO_order()
    {
        var bank = new RandomStreams(73); var trace = new List<int>();
        var clock = new Clock(0,new Dictionary<string,Action> {
            ["development"] = () => trace.Add(bank["development"].Next(100)),
            ["economy"] = () => trace.Add(bank["stock-economy"].Next(100)) });
        clock.ScheduleRepeated("development",2); clock.Schedule("economy",2);
        clock.Advance();
        var copied = RandomStreams.Restore(bank.Capture()); var nextTrace = new List<int>();
        var nextClock = Clock.Restore(clock.Capture(),new Dictionary<string,Action> {
            ["development"] = () => nextTrace.Add(copied["development"].Next(100)),
            ["economy"] = () => nextTrace.Add(copied["stock-economy"].Next(100)) });
        for (var i=0;i<80;i++) { clock.Advance(); nextClock.Advance(); }
        Assert.Equal(trace,nextTrace);
        Assert.Equal(JsonSerializer.Serialize(bank.Capture()),JsonSerializer.Serialize(copied.Capture()));
    }

    [Fact]
    public async Task Save_replacement_fences_the_old_epoch_and_preserves_consumed_streams()
    {
        var world=SpatialReferenceScenario.Create();
        foreach(var id in RandomStreams.Ids) for(var i=0;i<83;i++)world.Random[id].Next();
        world.AdvanceOneMinute();world.SetPaused(false);
        var host=new SessionHost(world,"owner");
        var capture=host.CaptureSave();host.Pump();var bytes=await capture;
        var expected=SaveCodec.Read(bytes);
        var load=host.LoadSave(bytes,1);host.Pump();Assert.Equal(2,(await load).WorldEpoch);
        var copy=host.CaptureSave(2);host.Pump();Assert.Equal(bytes,await copy);
        var stale=host.LoadSave(bytes,1);host.Pump();await Assert.ThrowsAsync<InvalidOperationException>(()=>stale);
        var restored=SaveCodec.Read(bytes);
        foreach(var id in RandomStreams.Ids)for(var i=0;i<200;i++)Assert.Equal(expected.Random[id].Next(),restored.Random[id].Next());
    }

    [Theory]
    [InlineData("algorithm")][InlineData("version")][InlineData("missing")][InlineData("null")]
    [InlineData("duplicate")][InlineData("unknown")][InlineData("index")][InlineData("short")]
    [InlineData("negative")][InlineData("maximum")][InlineData("zero")][InlineData("extra")]
    [InlineData("missingState")][InlineData("nullState")][InlineData("seed")][InlineData("missingStream")]
    [InlineData("stringIndex")][InlineData("stringValue")][InlineData("stringVersion")]
    public async Task Invalid_random_state_preserves_current_world_epoch_and_save(string mutation)
    {
        var host=SessionComposition.CreateReferenceHost();
        var before=host.CaptureSave();host.Pump();var original=await before;
        var node=JsonNode.Parse(original)!;var random=node["random"]!;
        var state=random["streams"]![0]!["state"]!;
        switch(mutation)
        {
            case "algorithm":random["algorithm"]="future";break;
            case "version":random["version"]=2;break;
            case "missing":node.AsObject().Remove("random");break;
            case "null":node["random"]=null;break;
            case "duplicate":random["streams"]![1]!["id"]=random["streams"]![0]!["id"]!.GetValue<string>();break;
            case "unknown":random["streams"]![0]!["id"]="unknown";break;
            case "index":state["index"]=55;break;
            case "short":state["values"]!.AsArray().RemoveAt(0);break;
            case "negative":state["values"]![0]=-1;break;
            case "maximum":state["values"]![0]=int.MaxValue;break;
            case "zero":state["values"]=new JsonArray(Enumerable.Repeat(0,55).Select(v=>(JsonNode?)JsonValue.Create(v)).ToArray());break;
            case "extra":state["extra"]=0;break;
            case "missingState":random["streams"]![0]!.AsObject().Remove("state");break;
            case "nullState":random["streams"]![0]!["state"]=null;break;
            case "seed":random["seed"]="0";break;
            case "stringIndex":state["index"]="0";break;
            case "stringValue":state["values"]![0]="0";break;
            case "stringVersion":random["version"]="1";break;
            case "missingStream":random["streams"]!.AsArray().RemoveAt(0);break;
        }
        var load=host.LoadSave(Encoding.UTF8.GetBytes(node.ToJsonString()),1);host.Pump();
        Assert.Equal(mutation is "algorithm" or "version" ? "incompatibleSave" : "invalidSave",
            (await Assert.ThrowsAsync<InvalidDataException>(()=>load)).Message);
        Assert.Equal(1,host.WorldEpoch);
        var after=host.CaptureSave();host.Pump();Assert.Equal(original,await after);
    }

    [Fact]
    public async Task Actual_checkpoint_shutdown_and_restart_preserve_consumed_random_streams()
    {
        var directory=Path.Combine(Path.GetTempPath(),"freetrain-random-"+Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var world=SpatialReferenceScenario.Create();
            foreach(var (id,i) in RandomStreams.Ids.Select((id,i)=>(id,i)))
                for(var j=0;j<(i+1)*61;j++)world.Random[id].Next();
            var bytes=SaveCodec.Write(world);
            await using(var server=await NetworkTests.Harness.Start(saveDirectory:directory,canonical:true))
            {
                await server.Bootstrap();
                Assert.True((await server.Http.PostAsync("/api/saves/upload?expectedWorldEpoch=1",new ByteArrayContent(bytes))).IsSuccessStatusCode);
                Assert.Equal(bytes,await server.Http.GetByteArrayAsync("/api/saves/download"));
            }
            Assert.Equal(bytes,File.ReadAllBytes(Path.Combine(directory,"checkpoint.json")));
            await using(var restarted=await NetworkTests.Harness.Start(saveDirectory:directory,canonical:true))
            {
                await restarted.Bootstrap();
                var loaded=await restarted.Http.GetByteArrayAsync("/api/saves/download");
                Assert.Equal(bytes,loaded);
                var restored=SaveCodec.Read(loaded);
                foreach(var id in RandomStreams.Ids)
                    for(var i=0;i<100;i++)Assert.Equal(world.Random[id].Next(),restored.Random[id].Next());
            }
        }
        finally { Directory.Delete(directory,true); }
    }
}
