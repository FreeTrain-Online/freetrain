using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Server;
using FreeTrain.Online.Simulation;

// P5-28: reuse P4's gameplay-built geometry, then purchase and deploy real stock.
static class FleetCapacity
{
    public static MixedSessionState Populate(MixedSessionState source,int rings,int length,bool network=false)
    {
        // Explicit composition with head/body/tail, not trusted car components.
        var stock=RollingStockCatalog.Bundled.Trains.Single(t=>t.Binding.ContributionId=="{3983B298-ADB1-4905-94E5-03B7AAE5A221}");
        var s=source;
        for(int bought=0;bought<rings*2;)
        {
            int quantity=Math.Min(StockAdmission.MaximumQuantity,rings*2-bought);
            s=FleetTrading.Commit(s,FleetTrading.QuotePurchase(s,stock.Binding,length,quantity));bought+=quantity;
        }
        var space=s.World.Terrain.Space;
        int columns=space.HorizontalSize==1500?8:space.HorizontalSize/16;
        for(int n=0;n<rings-(network?1:0);n++)
        {
            var o=space.ToLocation(new(16*(n%columns),16+32*(n/columns),2));
            // Opposite cardinal sides, eighteen track cells apart. The second source dir1
            // choice is reversed to follow the first. Never infer a pose from the rail count.
            foreach(var (offset,index) in new[]{(new Location(4,2,2),2*n),(new Location(11,13,2),2*n+1)})
            {
                var p=new Location(o.X+offset.X,o.Y+offset.Y,2);
                s=FleetDeployment.Commit(s,FleetDeployment.PreviewPlacement(s,s.Fleet!.Trains[index].Id,p,reverse:index%2==1));
            }
        }
        if(network)
        {
            var o=space.ToLocation(space.HorizontalSize==1500?new(64,260,2):new(32,100,2));
            foreach(var (x,index) in new[]{(32,rings*2-2),(46,rings*2-1)})
                s=FleetDeployment.Commit(s,FleetDeployment.PreviewPlacement(s,s.Fleet!.Trains[index].Id,new(o.X+x,o.Y+4,3)));
        }
        Validate(s,rings*2,length);return s;
    }
    public static void Validate(MixedSessionState s,int trains,int length)
    {
        var fleet=s.Fleet!;
        if(fleet.Trains.Length!=trains||fleet.Trains.Any(t=>t.Cars.Length!=length||t.State==TrainOperationalState.Unplaced))
            throw new Exception("Incomplete deployed fleet.");
        var cars=fleet.Trains.SelectMany(t=>t.Cars.Select(c=>(t,c))).ToArray();
        var inside=cars.Select(a=>(a.t,a.c,p:((FleetCarState.Inside)a.c.State).Location)).ToArray();
        if(inside.Select(a=>a.p).Distinct().Count()!=trains*length||s.World.Voxels.Count(v=>v.Traffic?.Car!=null)!=trains*length)
            throw new Exception("Overlapping/missing occupied car cells.");
        foreach(var (t,c,p) in inside)
            if(s.World.At(p)?.Traffic?.Car!=new CarComponent(c.Definition,t.Id,c.Id))throw new Exception("Occupancy mismatch.");
        var pending=s.Clock.Capture().Pending;
        if(pending.Length!=trains||fleet.Trains.Any(t=>pending.Count(a=>a.HandlerId==MixedSessionState.TrainHandler(t.Id)&&a.Interval==0)!=1))
            throw new Exception("Callback mismatch.");
        MixedWire.ValidateAdmission(s);
    }
    public static void Run(string mode,string directory)
    {
        RailwayCapacity.Run(mode,Path.Combine(directory,"railway"));
        var p4=File.ReadAllBytes(Path.Combine(directory,"railway/world.json"));
        var source=SaveCodec.ReadWorld(p4,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
        int rings=mode switch{"small"=>4,"medium"=>16,"network"=>1,"large" or "empty" or "heavy"=>64,_=>throw new ArgumentException("Unsupported fleet scene.")};
        int length=mode=="small"?4:8;
        bool network=mode is "empty" or "heavy" or "network";int minutes=network?16:72;
        var watch=Stopwatch.StartNew();var s=Populate(source,rings,length,network);var populateMs=watch.Elapsed.TotalMilliseconds;
        var bytes=SaveCodec.Write(s);File.WriteAllBytes(Path.Combine(directory,"world.json"),bytes);
        watch.Restart();var restored=SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;var reopenMs=watch.Elapsed.TotalMilliseconds;
        if(!bytes.SequenceEqual(SaveCodec.Write(restored)))throw new Exception("Nonexact fleet round trip.");
        var ticks=new List<double>();
        // Ring profiles complete a circuit at two minutes/cell. Network profiles
        // sample sixteen minutes. Validate every boundary and exact continuation.
        for(int minute=0;minute<minutes;minute++)
        {
            watch.Restart();s.AdvanceOneMinute();ticks.Add(watch.Elapsed.TotalMilliseconds);
            restored.AdvanceOneMinute();Validate(s,rings*2,length);
        }
        if(s.Fleet!.Trains.Take(network?rings*2-2:rings*2).Any(t=>t.MoveCount!=minutes/2)||
            s.Fleet.Trains.Any(t=>t.MoveCount==0)||!SaveCodec.Write(s).SequenceEqual(SaveCodec.Write(restored)))
            throw new Exception("Fleet callback progress or exact reopened continuation mismatch.");
        File.WriteAllBytes(Path.Combine(directory,"world.json.expected"),SaveCodec.Write(s));
        var initial=SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
        var packet=StateTransfer.Serialize(MixedWire.Snapshot(initial,1));
        var report=new{mode,constructedRings=mode=="network"?0:rings,occupiedRings=network?rings-1:rings,trains=rings*2,cars=rings*2*length,length,populateMs,reopenMs,tickMs=ticks,
            saveBytes=bytes.Length,packetBytes=packet.Length,saveSha256=Convert.ToHexStringLower(SHA256.HashData(bytes)),
            railwaySha256=Convert.ToHexStringLower(SHA256.HashData(p4)),terrainEntities=s.World.Terrain.Occupants.Length,
            railwayVoxels=s.World.Voxels.Length,callbacks=initial.Clock.Capture().Pending.Length,minutes,ringTrains=network?rings*2-2:rings*2,networkTrains=network?2:0,moves=s.Fleet!.Trains.Select(t=>t.MoveCount),
            dimensions=new{h=s.World.Terrain.Space.HorizontalSize,v=s.World.Terrain.Space.VerticalSize,z=s.World.Terrain.Space.Levels},
            trainsBound=initial.Fleet!.Trains.Select(t=>new{t.Id,t.Definition,cars=t.Cars.Select(c=>new{c.Id,c.Definition,c.OriginalIndex,c.State})}),
            managedRetainedBytes=GC.GetTotalMemory(true)};
        File.WriteAllText(Path.Combine(directory,"measurement.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions(WireJson.CreateOptions()){WriteIndented=true})+"\n");
        Console.WriteLine($"Fleet {mode}: {rings*2} trains / {rings*2*length} cars; {minutes} minutes; exact continuation.");
    }
}
