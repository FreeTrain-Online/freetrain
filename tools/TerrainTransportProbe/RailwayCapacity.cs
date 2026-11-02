using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Server;
using FreeTrain.Online.Simulation;

// P4-only instantiation of the P0-06 recipes. Every rail/support is produced by
// a gameplay handler. No train, platform, population or native-runtime credit.
static class RailwayCapacity
{
    public static void Run(string mode,string directory)
    {
        Directory.CreateDirectory(directory);
        var (h,v,z,rings)=mode switch {"small"=>(64,64,16,4),"medium"=>(128,128,32,16),
            "large"=>(256,256,64,64),"empty" or "heavy"=>(1500,1090,45,64),"limit" or "voxel-edge"=>(1500,1090,45,227),"route"=>(2048,830,45,0),"journal"=>(64,64,16,0),
            _=>throw new ArgumentException("Expected small, medium, large, empty, heavy, limit, voxel-edge, route or journal.")};
        var watch=Stopwatch.StartNew();
        var request=new EmptyWorldRequest(WorldCreationProfile.Id,"P4 capacity "+mode,new(h,v,z),0);
        var baseState=mode=="heavy"?TerrainCapacityScenario.Create(h,v,z,8):null;
        var cells=baseState?.World.Cells??Enumerable.Repeat(new TerrainCell(2,TerrainSurface.Cut),h*v).ToImmutableArray();
        var space=new WorldSpace(h,v,z);
        // Heavy scene reserves the same flat module area before constructing.
        // All other terrace/forest resources remain, with IDs and realized trees.
        bool Reserved(Location p){var s=space.ToStorage(p);return s.H<144&&s.V<300;}
        var kept=baseState?.World.Spatial.Entities.Where(e=>!Reserved(e.Anchor)).ToImmutableArray()??[];
        var ids=kept.Select(e=>e.Id).ToHashSet();
        if(baseState!=null){var b=cells.ToBuilder();for(int row=0;row<300;row++)for(int col=0;col<144;col++)b[row*h+col]=new(2,TerrainSurface.Cut);cells=b.ToImmutable();}
        var terrain=new TerrainWorld(TerrainProfiles.Bundled,request.Name,request.Dimensions,0,cells,
            new(baseState?.World.Spatial.NextEntityId??1,kept),baseState?.World.Occupants.Where(o=>ids.Contains(o.EntityId))??[]);
        if(mode is "empty" or "heavy")
        {
            var o=space.ToLocation(new(64,260,2));var next=terrain.Spatial.NextEntityId;
            var mountain=terrain.Profile.Definitions.Single(d=>d.Kind==TerrainOccupantKind.Mountain&&d.TreeSpriteCount==0).Reference;
            var anchors=new[]{new Location(o.X+11,o.Y+12,2),new Location(o.X+11,o.Y+20,2)};
            var extra=anchors.Select((p,i)=>new SpatialEntity(next+i,0,mountain,p,new([new(new(0,0,0),false)]),true,false,0)).ToImmutableArray();
            terrain=new(terrain.Profile,terrain.Name,request.Dimensions,0,cells,new(next+2,terrain.Spatial.Entities.AddRange(extra)),
                terrain.Occupants.Concat(extra.Select(e=>new TerrainOccupant(e.Id,TerrainOccupantKind.Mountain,new(0,1,2,3)))));
        }
        var state=new MixedConstructionState(MixedWorld.FromTerrain(RailwayProfiles.Bundled,terrain),new());
        var samples=new List<object>();
        void Rail(string type,Location from,Location to)
        {
            var t=Stopwatch.StartNew();
            var plan=type switch {"bridge"=>BridgeRailwayConstruction.Preview(state.World,from,to),
                "girder"=>GirderRailwayConstruction.Preview(state.World,from,to),"tunnel"=>TunnelRailwayConstruction.Preview(state.World,from,to),
                "garage"=>GarageRailwayConstruction.Preview(state.World,from,to),
                "signal"=>SignalRailwayConstruction.Preview(state.World,from,SignalRailwayConstruction.ThreeAspect,2),
                _=>NormalRailwayConstruction.PreviewBuild(state.World,from,to)};
            if(!plan.Legal)throw new InvalidOperationException($"{type} {from} {to}: {plan.Reason}");
            var previewMs=t.Elapsed.TotalMilliseconds;t.Restart();
            state=type switch {"bridge"=>BridgeRailwayConstruction.Apply(state,plan,new(0)),
                "girder"=>GirderRailwayConstruction.Apply(state,plan,new(0)),"tunnel"=>TunnelRailwayConstruction.Apply(state,plan,new(0)),
                "garage"=>GarageRailwayConstruction.Apply(state,plan,new(0)),"signal"=>SignalRailwayConstruction.Apply(state,plan,new(0)),
                _=>NormalRailwayConstruction.Apply(state,plan,new(0))};
            samples.Add(new{type,from,to,previewMs,applyMs=t.Elapsed.TotalMilliseconds});
        }
        int[][] vertices=[[4,2],[11,2],[13,4],[13,11],[11,13],[4,13],[2,11],[2,4]];
        var ringCells=new HashSet<Location>();
        for(int n=0;n<rings;n++)
        {
            int columns=h==1500?8:h/16;var origin=space.ToLocation(new(16*(n%columns),16+32*(n/columns),2));
            Location P(int i)=>new(origin.X+vertices[i%8][0],origin.Y+vertices[i%8][1],2);
            for(int i=0;i<8;i++){Rail("normal",P(i),P(i+1));foreach(var p in RailwayTopology.Route(P(i),P(i+1)))ringCells.Add(p);}
        }
        if(ringCells.Count!=36*rings)throw new Exception("Ring count/overlap mismatch.");
        // Literal ring adjacency is independent of production pattern generation.
        foreach(var p in ringCells)
        {
            var rail=state.World.At(p)!.Traffic!.Rail!;
            foreach(var d in RailwayTopology.Ports(rail.Pattern))
                if(!ringCells.Contains(p.Move(new(d)))||!RailwayTopology.HasPort(state.World.At(p.Move(new(d)))!.Traffic!.Rail!.Pattern,new((d+4)%8)))
                    throw new Exception("Nonreciprocal ring port.");
        }
        Location? networkOrigin=null;
        if(mode is "empty" or "heavy")
        {
            var o=space.ToLocation(new(64,260,2));networkOrigin=o;
            Location P(int x,int y,int level=2)=>new(o.X+x,o.Y+y,level);
            var t=Stopwatch.StartNew();var slope=SlopeRailwayConstruction.PreviewBuild(state.World,P(24,4),Heading.East);
            if(!slope.Legal)throw new Exception(slope.Reason);var previewMs=t.Elapsed.TotalMilliseconds;t.Restart();
            state=SlopeRailwayConstruction.Apply(state,slope,new(0));samples.Add(new{type="slope",previewMs,applyMs=t.Elapsed.TotalMilliseconds});
            Rail("normal",P(4,4),P(24,4));
            Rail("normal",P(27,4,3),P(32,4,3));Rail("bridge",P(32,4,3),P(40,4,3));
            Rail("girder",P(40,4,3),P(48,4,3));Rail("normal",P(48,4,3),P(54,4,3));
            Rail("tunnel",P(11,12),P(11,20));Rail("normal",P(9,4),P(11,6));Rail("normal",P(11,6),P(11,12));
            Rail("normal",P(18,4),P(20,6));Rail("normal",P(20,6),P(20,10));Rail("garage",P(20,10),P(20,16));Rail("signal",P(6,4),P(6,4));
        }
        string? overflowReason=null;
        if(mode is "limit" or "voxel-edge")
        {
            var p=space.ToLocation(new(500,500,2));int last=mode=="limit"?19:18;
            Rail("normal",p,p with {X=p.X+last});
            if(state.World.Voxels.Length!=(mode=="limit"?8192:8191))throw new Exception("Railway capacity boundary mismatch.");
            if(mode=="limit")
            {
                var overflow=NormalRailwayConstruction.PreviewBuild(state.World,p with {X=p.X+19},p with {X=p.X+20});
                overflowReason=overflow.Reason;
                if(overflow.Legal||overflowReason!="railwayCapacityLimit")throw new Exception("Railway overflow did not reject.");
            }
        }
        if(mode=="journal")
        {
            var p=space.ToLocation(new(16,32,2));
            // Source charged no-ops create real durable accounting entries.
            // Leave one slot for the browser to fill through a real extension.
            for(int i=0;i<8191;i++)Rail("normal",p,p with {X=p.X+1});
            if(state.Accounts.Transactions.Length!=8191||state.World.Voxels.Length!=2)throw new Exception("Journal capacity boundary mismatch.");
        }
        if(mode=="route")
        {
            var p=space.ToLocation(new(0,400,2));Rail("normal",p,p with {X=p.X+2047,Y=p.Y+2047});
            if(state.World.Voxels.Length!=2048)throw new Exception("Long route count mismatch.");
        }
        var (components,openPorts)=ValidateGraph(state.World,rings,networkOrigin!=null,mode is "limit" or "voxel-edge" or "route" or "journal");
        var constructedMs=watch.Elapsed.TotalMilliseconds;
        var session=new MixedSessionState(state,TerrainProvenance.From(request),revision:state.World.Revision);
        MixedWire.ValidateAdmission(session);
        watch.Restart();var bytes=SaveCodec.Write(session);var saveMs=watch.Elapsed.TotalMilliseconds;
        File.WriteAllBytes(Path.Combine(directory,"world.json"),bytes);
        watch.Restart();var restored=SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;var readMs=watch.Elapsed.TotalMilliseconds;
        if(!bytes.SequenceEqual(SaveCodec.Write(restored)))throw new Exception("Nonexact mixed round trip.");
        var snapshot=MixedWire.Snapshot(session,1);watch.Restart();var packet=StateTransfer.Serialize(snapshot);var packetMs=watch.Elapsed.TotalMilliseconds;
        File.WriteAllBytes(Path.Combine(directory,"packet.json"),packet);
        var content=File.ReadAllBytes("src/FreeTrain.Online.Content/bundled-v2.json");
        var report=new{mode,h,v,z,rings,ringCells=ringCells.Count,networkOrigin,components,openPorts,overflowReason,
            railwayVoxels=state.World.Voxels.Length,railwayOwners=MixedWire.RailwayEntities(state.World).Length,
            terrainEntities=state.World.Terrain.Occupants.Length,transactions=state.Accounts.Transactions.Length,
            constructedMs,saveMs,readMs,packetMs,saveBytes=bytes.Length,packetBytes=packet.Length,chunks=StateTransfer.Chunks(snapshot,packet).Count(),
            saveSha256=Convert.ToHexStringLower(SHA256.HashData(bytes)),contentSha256=Convert.ToHexStringLower(SHA256.HashData(content)),
            managedRetainedBytes=GC.GetTotalMemory(true),samples,
            bindings=state.World.Voxels.Select(v=>v.Traffic?.Rail?.Definition??v.Definition).Where(d=>d!=null).Distinct(),
            limits=new{railwayVoxels=8192,accountTransactions=8192,routeCells=NormalRailway.MaximumRouteCells}};
        File.WriteAllText(Path.Combine(directory,"measurement.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions(WireJson.CreateOptions()){WriteIndented=true})+"\n");
        Console.WriteLine(JsonSerializer.Serialize(new{mode,report.railwayVoxels,report.transactions,saveMs,readMs}));
    }
    static (int Components,int OpenPorts) ValidateGraph(MixedWorld world,int rings,bool network,bool limit)
    {
        var rails=world.Voxels.Where(v=>v.Traffic?.Rail!=null).ToDictionary(v=>v.Position);
        var remaining=rails.Keys.ToHashSet();int components=0,openPorts=0;
        IEnumerable<Location> Neighbors(Location p)
        {
            var r=rails[p].Traffic!.Rail!;
            foreach(var d in RailwayTopology.Ports(r.Pattern))
            {
                var heading=new Heading(d);var q=p.Move(heading);
                if(r.Pattern>=28)q=q with {Z=q.Z+SlopeRailwayConstruction.ZDiff(r.Pattern,heading)};
                if(!rails.TryGetValue(q,out var next)){openPorts++;continue;}
                var back=next.Traffic!.Rail!;var backPosition=q.Move(heading.Opposite);
                if(back.Pattern>=28)backPosition=backPosition with {Z=backPosition.Z+SlopeRailwayConstruction.ZDiff(back.Pattern,heading.Opposite)};
                if(!RailwayTopology.HasPort(back.Pattern,heading.Opposite)||backPosition!=p)throw new Exception("Nonreciprocal network edge.");
                yield return q;
            }
        }
        while(remaining.Count>0)
        {
            components++;var queue=new Queue<Location>();var first=remaining.First();remaining.Remove(first);queue.Enqueue(first);
            while(queue.TryDequeue(out var p))foreach(var q in Neighbors(p))if(remaining.Remove(q))queue.Enqueue(q);
        }
        if(components!=rings+(!network?0:1)+(limit?1:0)||openPorts!=(limit?2:!network?0:4))throw new Exception($"Network graph: {components} components, {openPorts} open ports.");
        return (components,openPorts);
    }
    public static void Verify(string directory)
    {
        var bytes=File.ReadAllBytes(Path.Combine(directory,"world.json"));
        var state=SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
        using var report=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"measurement.json")));
        var r=report.RootElement;var mode=r.GetProperty("mode").GetString();
        var graph=ValidateGraph(state.World,r.GetProperty("rings").GetInt32(),mode is "empty" or "heavy",mode is "limit" or "voxel-edge" or "route" or "journal");
        if(!bytes.SequenceEqual(SaveCodec.Write(state)))throw new Exception("Nonexact capacity save.");
        var hash=Convert.ToHexStringLower(SHA256.HashData(bytes));
        if(hash!=r.GetProperty("saveSha256").GetString())throw new Exception("Fixture drift.");
        File.WriteAllText(Path.Combine(directory,"validation.json"),JsonSerializer.Serialize(new{mode,graph.Components,graph.OpenPorts,sha256=hash,exact=true})+"\n");
    }

}
