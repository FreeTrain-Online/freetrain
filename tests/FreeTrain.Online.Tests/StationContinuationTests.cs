using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using System.Text.Json.Nodes;
using System.Text;
using Xunit;
using static FreeTrain.Online.Tests.StationBuildingConstructionTests;

namespace FreeTrain.Online.Tests;

public partial class StationYardStopTests
{
    static void ToMinute(MixedSessionState s, long minute)
    {
        while (s.Clock.Time.AbsoluteMinute < minute) s.AdvanceOneMinute();
    }

    static void ContinuationInvariant(MixedSessionState s)
    {
        var pending = s.Clock.Capture().Pending;
        Assert.Equal(pending.Length, pending.Select(p => p.HandlerId).Distinct().Count());
        foreach (var train in s.Fleet!.Trains)
            Assert.Single(pending.Where(p => p.HandlerId == MixedSessionState.TrainHandler(train.Id)));
        foreach (var b in s.World.Stations!.Buildings)
        {
            Assert.Contains(pending, p => p.Registration == b.HourTimer!.Registration && p.Order == b.HourTimer.Order);
            Assert.Contains(pending, p => p.Registration == b.DayTimer!.Registration && p.Order == b.DayTimer.Order);
        }
        Assert.NotEmpty(s.World.Stations.Listeners);
    }

    [Theory]
    [InlineData("service")]
    [InlineData("reverse")]
    [InlineData("turning")]
    [InlineData("midnight")]
    public async Task P6_25_callback_boundaries_reopen_and_checkpoint_match_every_canonical_successor(string mode)
    {
        var initial = Scene();
        initial = Build(initial, at: new(36,13,2));
        initial = ThinPlatformConstruction.Commit(initial,
            ThinPlatformConstruction.Preview(initial.World,new(37,15,2),Heading.East,4,false));
        if (mode != "service") initial = Edit(initial, controller: c => c with { Harbors = [
            mode == "midnight" ? new(StationLocation(initial),FleetHarborKind.Daily,0,false) :
            new(StationLocation(initial),FleetHarborKind.Advanced,0,false,
                [new(-1,-1,-1,-1,-1,mode == "turning" ? FleetRuleAction.Reverse2 : FleetRuleAction.Reverse)])] });
        var start = SaveCodec.Write(initial);
        var uninterrupted = Open(initial);
        var resumedEveryBoundary = Open(initial);
        var end = mode == "midnight" ? 1442 : 600;
        // Discover actual queue boundaries, rather than assume stock timing.
        var cuts = new SortedSet<long> { initial.Clock.Time.AbsoluteMinute };
        while (uninterrupted.Clock.Time.AbsoluteMinute < end)
        {
            var now = uninterrupted.Clock.Time.AbsoluteMinute;
            if (mode == "midnight" && now == 1439)
            {
                var equal = uninterrupted.Clock.Capture().Pending.Where(p => p.DueMinute == 1440).ToArray();
                Assert.Contains(equal,p => p.HandlerId.StartsWith("train/",StringComparison.Ordinal));
                Assert.True(equal.Length >= 5); // two station hours/days and the waiting train
            }
            if (uninterrupted.Clock.Capture().Pending.Any(p => p.DueMinute == now + 1))
            {
                foreach (var cut in new[] { now, now+1, now+2 }) if (cut <= end) cuts.Add(cut);
                resumedEveryBoundary = Open(resumedEveryBoundary);
            }
            uninterrupted.AdvanceOneMinute();
            resumedEveryBoundary.AdvanceOneMinute();
            Assert.Equal(SaveCodec.Write(uninterrupted),SaveCodec.Write(resumedEveryBoundary));
            ContinuationInvariant(uninterrupted);
        }
        var expected = SaveCodec.Write(uninterrupted);
        var directory = Path.Combine(Path.GetTempPath(),"station-continuation-"+Guid.NewGuid());
        try
        {
            foreach (var cut in cuts)
            {
                var live = SaveCodec.ReadWorld(start,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
                ToMinute(live,cut);
                var boundary = SaveCodec.Write(live);
                // Independent writer/reader lifetimes exercise actual atomic disk storage.
                using (var storage = new SaveStorage(directory)) await storage.Write("checkpoint",boundary);
                MixedSessionState resumed;
                using (var storage = new SaveStorage(directory))
                    resumed = SaveCodec.ReadWorld(storage.Read("checkpoint"),TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
                Assert.Equal(boundary,SaveCodec.Write(resumed));
                while (resumed.Clock.Time.AbsoluteMinute < end)
                {
                    live.AdvanceOneMinute(); resumed.AdvanceOneMinute();
                    if (resumed.Clock.Time.AbsoluteMinute <= cut+3)
                        Assert.Equal(SaveCodec.Write(live),SaveCodec.Write(resumed));
                    ContinuationInvariant(resumed);
                }
                Assert.Equal(expected,SaveCodec.Write(resumed));
            }
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory,true); }
        if (mode == "service")
        {
            Assert.Contains(uninterrupted.Construction.Accounts.Transactions,t => t.Operation == "stationUnload" && t.Revenue > 0);
        }
        if (mode == "midnight")
            Assert.Contains(uninterrupted.Construction.Accounts.Transactions,t => t.Operation.StartsWith("station/") && t.AbsoluteMinute == 1440);
        if (Environment.GetEnvironmentVariable("FREETRAIN_STATION_CONTINUATION_OUTPUT") is {} output)
        {
            // Separate process fixtures immediately before boarding/reversal or midnight.
            var boundary = Open(initial); Call(boundary);
            ToMinute(boundary,boundary.Clock.Capture().Pending.Single(p => p.HandlerId.StartsWith("train/",StringComparison.Ordinal)).DueMinute-1);
            var path = output+"."+mode;
            File.WriteAllBytes(path,SaveCodec.Write(boundary));
            ToMinute(boundary,end); File.WriteAllBytes(path+".expected",SaveCodec.Write(boundary));
        }
    }

    static Location StationLocation(MixedSessionState s) => s.World.Stations!.Buildings[0].Location;

    [Theory]
    [InlineData("corrupt")]
    [InlineData("definition")]
    [InlineData("pack")]
    public async Task P6_25_invalid_station_checkpoint_and_named_slot_preserve_recoverable_world(string kind)
    {
        var s=Scene(); Call(s); Call(s);
        var good=SaveCodec.Write(s); var node=JsonNode.Parse(good)!;
        if(kind=="definition") node["mixed"]!["stationGraph"]!["buildings"]![0]!["definition"]!["recipeId"]="missing:p6-25";
        if(kind=="pack") node["requiredPacks"]![0]!["catalogHash"]=new string('0',64);
        var bad=Encoding.UTF8.GetBytes(kind=="corrupt"?"{broken":node.ToJsonString());
        var directory=Path.Combine(Path.GetTempPath(),"station-recovery-"+Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllBytes(Path.Combine(directory,"checkpoint.json"),bad);
            File.WriteAllBytes(Path.Combine(directory,"slot3.json"),bad);
            await using(var server=await NetworkTests.Harness.Start(saveDirectory:directory,canonical:true))
            {
                await server.Bootstrap();
                Assert.False(File.Exists(Path.Combine(directory,"checkpoint.json")));
                Assert.Contains("startupError",await server.Http.GetStringAsync("/api/saves/status"));
                Assert.True((await server.Http.PostAsync("/api/saves/upload?expectedWorldEpoch=1",new System.Net.Http.ByteArrayContent(good))).IsSuccessStatusCode);
                Assert.Equal(good,await server.Http.GetByteArrayAsync("/api/saves/download"));
                Assert.False((await server.Http.PostAsync("/api/saves/slot3/load?expectedWorldEpoch=2",null)).IsSuccessStatusCode);
                Assert.Equal(good,await server.Http.GetByteArrayAsync("/api/saves/download"));
                Assert.Equal(bad,File.ReadAllBytes(Path.Combine(directory,"slot3.json")));
                Assert.True((await server.Http.PostAsync("/api/saves/checkpoint/save?expectedWorldEpoch=2",null)).IsSuccessStatusCode);
            }
            await using(var server=await NetworkTests.Harness.Start(saveDirectory:directory,canonical:true))
            {
                await server.Bootstrap();
                Assert.Equal(good,await server.Http.GetByteArrayAsync("/api/saves/download"));
                Assert.Equal(bad,File.ReadAllBytes(Path.Combine(directory,"slot3.json")));
            }
        }
        finally { Directory.Delete(directory,true); }
    }

    [Fact]
    public async Task P6_25_immediate_build_remove_rebuild_and_rehost_continue_across_each_reopen()
    {
        var live = Scene(); Call(live);
        var location = StationLocation(live);
        var directory=Path.Combine(Path.GetTempPath(),"station-host-checkpoint-"+Guid.NewGuid());
        try
        {
            foreach (var operation in new Func<MixedSessionState,MixedSessionState>[] {
                s => Build(s,at:new(36,13,2)),
                s => { var b=s.World.Stations!.Buildings[0]; return StationBuildingConstruction.Commit(s,
                    StationBuildingConstruction.Preview(s.World,b.Location,null,true,b.Id,b.Revision)); },
                s => Build(s,at:location),
                s => { var p=s.World.Stations!.Platforms[0]; var b=s.World.Stations.Buildings.Single(b=>b.Location==location);
                    return StationHostConnections.Commit(s,StationHostConnections.Preview(s.World,p.Id,p.Revision,b.Id,b.Revision)); } })
            {
                using (var storage=new SaveStorage(directory)) await storage.Write("checkpoint",SaveCodec.Write(live));
                MixedSessionState resumed;
                using (var storage=new SaveStorage(directory)) resumed=SaveCodec.ReadWorld(storage.Read("checkpoint"),TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
                live = operation(live); resumed = operation(resumed);
                Assert.Equal(SaveCodec.Write(live),SaveCodec.Write(resumed));
                using (var storage=new SaveStorage(directory)) await storage.Write("checkpoint",SaveCodec.Write(resumed));
                using (var storage=new SaveStorage(directory)) resumed=SaveCodec.ReadWorld(storage.Read("checkpoint"),TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
                live.AdvanceOneMinute(); resumed.AdvanceOneMinute();
                Assert.Equal(SaveCodec.Write(live),SaveCodec.Write(resumed));
                ContinuationInvariant(resumed);
            }
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory,true); }
    }
}
