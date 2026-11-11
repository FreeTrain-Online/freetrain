using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Server;
using FreeTrain.Online.Simulation;

// P6-26: gameplay-built stations/platforms/stock; explicit P7 controlled supply.
static class StationCapacity
{
    public static void Run(string mode, string directory)
    {
        if (mode is not ("small" or "medium" or "large" or "extent")) throw new ArgumentException("Unknown station capacity scene.");
        RailwayCapacity.Run(mode == "extent" ? "empty" : mode, Path.Combine(directory,"railway"));
        var railBytes = File.ReadAllBytes(Path.Combine(directory,"railway/world.json"));
        var baseline = SaveCodec.ReadWorld(railBytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
        var world = baseline.World.EnableFleet(RollingStockCatalog.Bundled.FleetProfile);
        var s = new MixedSessionState(new(world,baseline.Construction.Accounts),baseline.Provenance,clock:new(1437,0,[]),revision:world.Revision);
        int rings = mode switch { "small" => 4, "medium" => 16, _ => 64 };
        var definition = RailwayProfiles.Bundled.Stations!.Definitions.First(d=>d.SizeX==1&&d.SizeY==1&&d.Height==1);
        var stock = RollingStockCatalog.Bundled.Trains.First(t=>t.PermittedLengths.Contains(3)&&t.Create(3).PackedCapacity>t.Create(3).NormalCapacity);
        var space = s.World.Terrain.Space;
        int columns = mode == "extent" ? 8 : space.HorizontalSize/16;
        var starts = new List<Location>(); var providers = new List<long>();
        var buildMs = new List<double>(); var attachMs = new List<double>();
        for (int n=0;n<rings;n++)
        {
            var o=space.ToLocation(new(16*(n%columns),16+32*(n/columns),2));
            Location P(int x,int y)=>new(o.X+x,o.Y+y,2);
            foreach(var p in new[]{P(4,0),P(11,15)})
            {
                var watch=Stopwatch.StartNew();
                var plan=StationBuildingConstruction.Preview(s.World,p,definition.Reference);
                if(!plan.Legal)throw new Exception("Station: "+plan.Reason);
                s=StationBuildingConstruction.Commit(s,plan);buildMs.Add(watch.Elapsed.TotalMilliseconds);
            }
            foreach(var p in new[]{P(5,2),P(5,13)})
            {
                var plan=ThinPlatformConstruction.Preview(s.World,p,Heading.East,4,n%2==0);
                if(!plan.Legal)throw new Exception("Platform: "+plan.Reason);
                s=ThinPlatformConstruction.Commit(s,plan);
            }
            // Four individual rail owners on each side carry explicitly controlled
            // populations. They are real spatial owners, not city producer claims.
            foreach(var y in new[]{2,13})foreach(var x in new[]{4,9,10,11})
            {
                long id=s.World.At(P(x,y))!.OwnerId;var watch=Stopwatch.StartNew();
                world=StationCatchment.Attach(s.World,id,new(new("ConstantPopulation",2500),10000));
                s=s.Successor(s.Revision,new(world,s.Construction.Accounts),false);
                attachMs.Add(watch.Elapsed.TotalMilliseconds);providers.Add(id);
            }
            starts.Add(P(5,2));
        }
        s=FleetTrading.Commit(s,FleetTrading.QuotePurchase(s,stock.Binding,3,rings));
        for(int n=0;n<rings;n++)s=FleetDeployment.Commit(s,FleetDeployment.PreviewPlacement(s,s.Fleet!.Trains[n].Id,starts[n]));
        Validate(s,rings);
        var bytes=SaveCodec.Write(s);File.WriteAllBytes(Path.Combine(directory,"world.json"),bytes);
        var reopenWatch=Stopwatch.StartNew();var reopened=Open(bytes);var reopenMs=reopenWatch.Elapsed.TotalMilliseconds;
        if(!bytes.SequenceEqual(SaveCodec.Write(reopened)))throw new Exception("Initial reopen differs.");
        var churn=new List<double>();var previewMs=new List<double>();var editMs=new List<double>();
        for(int i=0;i<30;i++)
        {
            var watch=Stopwatch.StartNew();var detached=StationCatchment.Detach(s.World,providers[i%providers.Count]);
            var attached=StationCatchment.Attach(detached,providers[i%providers.Count],s.World.Stations!.Listeners.Single(l=>l.ProviderId==providers[i%providers.Count]).Provider!);
            churn.Add(watch.Elapsed.TotalMilliseconds);
            if(attached.Stations!.Listeners.Length!=providers.Count)throw new Exception("Churn lost provider.");
            var b=s.World.Stations!.Buildings[i%s.World.Stations.Buildings.Length];watch.Restart();
            var removal=StationBuildingConstruction.Preview(s.World,b.Location,null,true,b.Id,b.Revision);
            previewMs.Add(watch.Elapsed.TotalMilliseconds);if(!removal.Legal)throw new Exception(removal.Reason);
            watch.Restart();var removed=StationBuildingConstruction.Commit(s,removal);
            var replacement=StationBuildingConstruction.Preview(removed.World,b.Location,definition.Reference);
            if(!replacement.Legal)throw new Exception(replacement.Reason);
            var rebuilt=StationBuildingConstruction.Commit(removed,replacement);editMs.Add(watch.Elapsed.TotalMilliseconds);
            MixedWire.ValidateAdmission(rebuilt);
        }
        var ticks=new List<object>();long departures=0;
        for(int i=0;i<120;i++)
        {
            long minute=s.Clock.Time.AbsoluteMinute+1;var due=s.Clock.Capture().Pending.Where(p=>p.DueMinute==minute).ToArray();
            var watch=Stopwatch.StartNew();s.AdvanceOneMinute();var ms=watch.Elapsed.TotalMilliseconds;
            departures+=s.DepartureBells.Length;
            ticks.Add(new{minute,ms,hour=minute%60==0,midnight=minute%1440==0,due=due.Length});
            reopened.AdvanceOneMinute();Validate(s,rings);
            if(i==2||i==59)reopened=Open(SaveCodec.Write(reopened));
            if(!SaveCodec.Write(s).SequenceEqual(SaveCodec.Write(reopened)))throw new Exception("Continuation differs at "+minute);
        }
        var graph=s.World.Stations!;
        if(departures==0||!s.Construction.Accounts.Transactions.Any(t=>t.Operation=="stationUnload"&&t.Revenue>0))throw new Exception("No real passenger service.");
        File.WriteAllBytes(Path.Combine(directory,"world.json.expected"),SaveCodec.Write(s));
        var initial=Open(bytes);
        var report=new{mode,rings,trains=rings,cars=rings*3,stations=2*rings,platforms=2*rings,providers=providers.Count,
            controlledPopulationPerProvider=2500,scope="Controlled rail-owner population bindings; P7 city providers pending",
            dimensions=new{h=space.HorizontalSize,v=space.VerticalSize,z=space.Levels},
            saveSha256=Hash(bytes),railwaySha256=Hash(railBytes),contentSha256=Hash(File.ReadAllBytes("src/FreeTrain.Online.Content/bundled-v2.json")),
            fixtureSha256=Hash(File.ReadAllBytes(File.Exists("doc/parity/station-fixtures.json")?"doc/parity/station-fixtures.json":Path.Combine(AppContext.BaseDirectory,"Spatial/station-fixtures.json"))),definition=definition.Reference,stock=stock.Binding,
            carRecipes=initial.Fleet!.Trains[0].Cars.Select(c=>c.Definition),saveBytes=bytes.Length,
            packetBytes=StateTransfer.Serialize(MixedWire.Snapshot(initial,1)).Length,reopenMs,buildMs,attachMs,churnMs=churn,previewMs,editMs,ticks,
            callbacks=initial.Clock.Capture().Pending.Length,departures,
            waiting=graph.Buildings.Select(b=>StationCatchment.Waiting(graph,b.Id,s.Clock.Time)),
            exports=graph.Buildings.Sum(b=>(long)b.Exports.Today),imports=graph.Buildings.Sum(b=>(long)b.Imports.Today),
            transactions=s.Construction.Accounts.Transactions.Length,managedRetainedBytes=GC.GetTotalMemory(true)};
        File.WriteAllText(Path.Combine(directory,"measurement.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions(WireJson.CreateOptions()){WriteIndented=true})+"\n");
        Console.WriteLine($"Station {mode}: {rings*2} stations, {rings} trains, {providers.Count} controlled providers; exact 120-minute continuation.");
    }
    static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    static MixedSessionState Open(byte[] bytes)=>SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
    static void Validate(MixedSessionState s,int rings)
    {
        MixedWire.ValidateAdmission(s);
        var pending=s.Clock.Capture().Pending;
        if(pending.Length!=rings*7||pending.Select(p=>p.HandlerId).Distinct().Count()!=pending.Length)throw new Exception("Callback identities/count differ.");
        if(s.World.Stations!.Platforms.Any(p=>p.HostId==null))throw new Exception("Unhosted platform.");
        if(s.World.Voxels.Count(v=>v.Traffic?.Car!=null)!=rings*3)throw new Exception("Lost train occupancy.");
        foreach(var t in s.Fleet!.Trains)
            if(pending.Count(p=>p.HandlerId==MixedSessionState.TrainHandler(t.Id))!=1)throw new Exception("Train timer differs.");
    }
}
