using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Simulation;
using FreeTrain.Online.Persistence;
using Xunit;

namespace FreeTrain.Online.Tests;

public class TunnelConstructionTests
{
    internal static RailwayProfile Profile => RailwayProfiles.Bundled;
    internal static Location Start => new(14,10,2);
    internal static MixedWorld World(IEnumerable<Location>? mountains=null,QuarterCorners? corners=null,int water=0,bool natural=false)
    {
        var cells=Enumerable.Repeat(new TerrainCell(2,natural?TerrainSurface.Natural:TerrainSurface.Cut),1280).ToArray();
        var space=new WorldSpace(40,32,8);
        var points=natural ? Enumerable.Range(0,1280).Select(i=>space.ToLocation(new(i%40,i/40,2))) : mountains??[Start];
        var entities=new List<SpatialEntity>();var occupants=new List<TerrainOccupant>();long id=1;
        var definition=Profile.Terrain.Definitions.Single(b=>b.Kind==TerrainOccupantKind.Mountain&&b.TreeSpriteCount==0).Reference;
        foreach(var p in points)
        {
            var c=corners??new QuarterCorners(0,1,2,3);
            entities.Add(new(id,0,definition,p,new([new(new(0,0,0),c.Flat)]),true,false,0));
            occupants.Add(new(id,TerrainOccupantKind.Mountain,c));id++;
        }
        var terrain=new TerrainWorld(Profile.Terrain,"Tunnel",new(40,32,8),water,cells,new(id,entities.ToImmutableArray()),occupants);
        return MixedWorld.FromTerrain(Profile,terrain);
    }
    internal static MixedWorld Add(MixedWorld w,Location p,int pattern=4,bool tunnel=false,bool car=false,DefinitionReference? platform=null)
    {
        var id=w.Spatial.NextEntityId;
        var carDef=Profile.TrafficOwner with{ContributionId="test:car"};
        var profile=car&&!w.Profile.Bindings.Any(b=>b.Handler==RailwayHandler.Car)?new RailwayProfile(w.Profile.Terrain,w.Profile.Bindings.Add(new(carDef,RailwayHandler.Car))):w.Profile;
        var rail=new RailComponent(tunnel?Profile.Bindings.Single(b=>b.Handler==RailwayHandler.Tunnel).Reference:
            Profile.Bindings.Single(b=>b.Variant==pattern&&b.Handler==(pattern<12?RailwayHandler.Normal:RailwayHandler.Junction)).Reference,
            pattern,TerrainBackup:tunnel?new QuarterCorners(0,1,2,3):null);
        return new(profile,w.Terrain,new(id+1,w.Spatial.Entities.Add(new(id,0,Profile.TrafficOwner,p,new([new(new(0,0,0),true)]),true,false,0))),
            w.Voxels.Add(new(p,id,RailwayVoxelKind.Traffic,new(rail,Car:car?new(carDef,1,1):null,Platform:platform))),w.Slopes,w.Revision);
    }
    internal static MixedWorld Apply(MixedWorld w,RailwayConstructionPlan p) => TunnelRailwayConstruction.Apply(new(w),p,new(0)).World;

    [Fact]
    public void Independent_literal_tunnel_vectors_match_new_cell_booking_protection_and_half_open_corners()
    {
        using var fixtures=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spatial/railway-fixtures.json")));
        var rows=fixtures.RootElement.GetProperty("groups").EnumerateObject().SelectMany(g=>g.Value.GetProperty("cases").EnumerateArray()).Where(c=>c.GetProperty("id").GetString()!.StartsWith("TUNNEL-"));
        var count=0;
        foreach(var row in rows)
        {
            var input=row.GetProperty("input");var expected=row.GetProperty("expected");
            var data=input.GetProperty("cells").EnumerateArray().ToArray();var direction=input.GetProperty("direction").GetInt32();var (dx,dy)=new Heading(direction).Offset;
            Location P(int n)=>new(Start.X+n*dx,Start.Y+n*dy,2);
            var w=World(data.Select((c,i)=>(c,i)).Where(x=>x.c.GetProperty("kind").GetString()=="mountain").Select(x=>P(x.i)),water:input.GetProperty("water").GetInt32());
            for(var i=0;i<data.Length;i++)
            {
                var c=data[i];var kind=c.GetProperty("kind").GetString();if(kind is "mountain" or "empty")continue;
                w=Add(w,P(i),tunnel:kind=="tunnel",car:c.TryGetProperty("car",out var car)&&car.GetBoolean());
            }
            var plan=TunnelRailwayConstruction.Preview(w,P(0),P(data.Length-1),input.GetProperty("action").GetString()=="remove");
            var legal=expected.GetProperty("outcome").GetString()=="apply";
            Assert.Equal(legal,plan.Legal);Assert.Equal(expected.GetProperty("spend").GetInt64(),plan.Booking.Total);
            if(legal)
            {
                Assert.Equal(expected.GetProperty("changed").EnumerateArray().Select(c=>c.GetInt32()),plan.Topology.Cells.Select((c,i)=>(c,i)).Where(x=>x.c.Changed).Select(x=>x.i));
                var after=Apply(w,plan);Assert.Empty(plan.ElectedPiers);Assert.Empty(plan.RemovedPiers);
                foreach(var r in expected.GetProperty("restored").EnumerateArray())
                {
                    var p=P(r.GetProperty("cell").GetInt32());var occupant=after.Terrain.OccupantAt(p)!;
                    Assert.Equal(new QuarterCorners(0,1,2,3),occupant.Corners);Assert.Null(occupant.Forest);Assert.False(after.Spatial.At(p).Entity!.Owned);
                }
            }
            count++;
        }
        Assert.Equal(9,count);
    }
    [Theory]
    [InlineData(0,10,1)][InlineData(2,4,0)][InlineData(4,10,1)][InlineData(6,4,0)]
    public void Both_route_orders_and_axes_restore_all_corners_and_keep_ordinary_gap_rail(int direction,int pattern,int sprite)
    {
        var d=new Heading(direction);var end=Start.Move(d).Move(d);var middle=Start.Move(d);
        var w=World([Start,end]);var original=w.Terrain;var plan=TunnelRailwayConstruction.Preview(w,Start,end);
        Assert.True(plan.Legal);Assert.Equal(36_000_000,plan.Booking.Total);Assert.Equal(2,plan.Reclaimed.Length);
        w=Apply(w,plan);Assert.True(original.Cells.SequenceEqual(w.Terrain.Cells));Assert.Equal(2,w.Terrain.TunnelBackups.Count);
        Assert.Equal(pattern,w.At(middle)!.Traffic!.Rail!.Pattern);Assert.Equal(RailwayHandler.Normal,w.Profile.Resolve(w.At(middle)!.Traffic!.Rail!.Definition).Handler);
        foreach(var p in new[]{Start,end}){Assert.Equal(sprite,w.At(p)!.Traffic!.Rail!.Variant);Assert.Equal(new QuarterCorners(0,1,2,3),w.At(p)!.Traffic!.Rail!.TerrainBackup);}
        var removal=TunnelRailwayConstruction.Preview(w,Start,end,true);Assert.Equal(4_000_000,removal.Booking.Total);w=Apply(w,removal);
        Assert.Null(w.At(Start));Assert.NotNull(w.At(end));Assert.NotNull(w.At(middle));
        w=Apply(w,TunnelRailwayConstruction.Preview(w,end,Start,true));
        Assert.Single(w.Voxels);Assert.Empty(w.Terrain.TunnelBackups);Assert.True(original.Cells.SequenceEqual(w.Terrain.Cells));
        foreach(var p in new[]{Start,end})Assert.Equal(original.OccupantAt(p)!.Corners,w.Terrain.OccupantAt(p)!.Corners);
    }
    [Fact]
    public void Natural_surface_boundaries_survive_build_and_partial_restoration_without_reclassifying_ground()
    {
        var w=World(corners:new(1,1,1,1),natural:true);var end=Start with{X=18};
        var before=w.Terrain.Cells;w=Apply(w,TunnelRailwayConstruction.Preview(w,Start,end));
        Assert.True(before.SequenceEqual(w.Terrain.Cells));Assert.Equal(8,w.Terrain.FineHeight(Start,0));
        w=Apply(w,TunnelRailwayConstruction.Preview(w,Start,end,true));Assert.True(before.SequenceEqual(w.Terrain.Cells));Assert.Single(w.Terrain.TunnelBackups);
    }
    [Theory]
    [InlineData(4,true)][InlineData(10,false)][InlineData(14,true)][InlineData(0,false)]
    public void Compatible_existing_rail_is_unchanged_and_free_while_other_ports_reject(int pattern,bool legal)
    {
        var w=Add(World(),Start with{X=16},pattern);var old=w.At(Start with{X=16});
        var p=TunnelRailwayConstruction.Preview(w,Start,Start with{X=16});Assert.Equal(legal,p.Legal);
        if(legal){Assert.Equal(24_000_000,p.Booking.Total);Assert.Equal(old,Apply(w,p).At(old!.Position));}
    }
    [Fact]
    public void Rejections_and_wrong_subtype_removal_do_not_mutate_and_stale_plans_cannot_apply()
    {
        var w=World();var end=Start with{X=16};
        Assert.Equal("tunnelMountainRequired",TunnelRailwayConstruction.Preview(World([]),Start,end).Reason);
        Assert.Equal("underwater",TunnelRailwayConstruction.Preview(World(water:3),Start,end).Reason);
        Assert.True(TunnelRailwayConstruction.Preview(World(water:2),Start,end).Legal);
        Assert.Equal("carOccupied",TunnelRailwayConstruction.Preview(Add(w,end,car:true),Start,end).Reason);
        Assert.Equal("samePoint",TunnelRailwayConstruction.Preview(w,Start,Start).Reason);
        Assert.Equal("outside",TunnelRailwayConstruction.Preview(w,new(-500,0,2),new(-498,0,2)).Reason);
        Assert.Equal("routeWorkLimit",TunnelRailwayConstruction.Preview(w,Start,new(int.MaxValue,10,2)).Reason);
        var plan=TunnelRailwayConstruction.Preview(w,Start,end);var next=Apply(w,plan);
        Assert.Throws<InvalidOperationException>(()=>Apply(next,plan));Assert.NotNull(w.Terrain.OccupantAt(Start));Assert.Empty(w.Voxels);
        var skip=TunnelRailwayConstruction.Preview(Add(World([]),Start),Start,end,true);
        Assert.True(skip.Legal);Assert.Equal(0,skip.Booking.Total);Assert.Empty(skip.Topology.Cells.Where(c=>c.Changed));
    }
    [Fact]
    public void Replaced_forest_and_ownership_are_lost_but_unrelated_forest_terrace_and_corners_survive()
    {
        var other=new Location(22,12,2);var terrace=new Location(24,12,1);
        var w=World([Start,other]);var terrain=w.Terrain;
        var forest=Profile.Terrain.Definitions.First(d=>d.TreeSpriteCount>0 && d.TreeDensity>0);
        var occupants=terrain.Occupants.Select(o=>o with{Forest=new(forest.Reference,[new(3,4,0)])}).ToList();
        var entities=terrain.Spatial.Entities.Select(e=>e with{Value=forest.Price}).ToList();
        var id=terrain.Spatial.NextEntityId;
        var terraceDef=Profile.Terrain.Definitions.First(d=>d.Kind==TerrainOccupantKind.Terrace).Reference;
        entities.Add(new(id,0,terraceDef,terrace,new([new(new(0,0,0),false)]),false,false,0));
        occupants.Add(new(id,TerrainOccupantKind.Terrace,new(1,2,3,4)));
        var backing=Start with{Z=1};
        entities.Add(new(id+1,0,terraceDef,backing,new([new(new(0,0,0),false)]),false,false,0));
        occupants.Add(new(id+1,TerrainOccupantKind.Terrace,new(4,3,2,1)));
        var grid=terrain.Cells.ToBuilder();var t=terrain.Space.ToStorage(terrace);grid[t.V*40+t.H]=new(2,TerrainSurface.Terrace);var b=terrain.Space.ToStorage(Start);grid[b.V*40+b.H]=new(2,TerrainSurface.Terrace);
        terrain=new(Profile.Terrain,terrain.Name,new(40,32,8),0,grid,new(id+2,entities.ToImmutableArray()),occupants);
        w=MixedWorld.FromTerrain(Profile,terrain);
        var plan=TunnelRailwayConstruction.Preview(w,Start,Start with{X=16});
        Assert.NotNull(Assert.Single(plan.Reclaimed).Occupant.Forest);Assert.Equal(36_000_000,plan.Booking.Total);
        w=Apply(w,plan);
        var request=new EmptyWorldRequest(WorldCreationProfile.Id,terrain.Name,new(40,32,8),0);
        var session=new MixedSessionState(new(w),TerrainProvenance.From(request),revision:w.Revision);
        var bytes=SaveCodec.Write(session);
        w=SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!.World;
        Assert.Equal(bytes,SaveCodec.Write(SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!));
        w=Apply(w,TunnelRailwayConstruction.Preview(w,Start,Start with{X=16},true));
        Assert.Null(w.Terrain.OccupantAt(Start)!.Forest);Assert.False(w.Spatial.At(Start).Entity!.Owned);
        Assert.Equal(terrain.OccupantAt(other)!.Corners,w.Terrain.OccupantAt(other)!.Corners);Assert.Equal(terrain.OccupantAt(other)!.Forest!.Trees.ToArray(),w.Terrain.OccupantAt(other)!.Forest!.Trees.ToArray());Assert.Equal(terrain.Spatial.At(other).Entity!.Id,w.Spatial.At(other).Entity!.Id);Assert.True(w.Spatial.At(other).Entity!.Owned);
        Assert.Equal(terrain.OccupantAt(backing),w.Terrain.OccupantAt(backing));Assert.Equal(terrain.OccupantAt(terrace),w.Terrain.OccupantAt(terrace));Assert.True(terrain.Cells.SequenceEqual(w.Terrain.Cells));
    }

    [Fact]
    public void Generated_sloped_hill_accepts_same_level_surface_portals_without_terraces_and_restores_after_reopen()
    {
        var request=WorldCreationProfile.Generated(0,"Sloped tunnel portals");
        request=request with{Dimensions=new(40,40,16),Options=request.Options with{Relief=WorldCreationProfile.Relief(ReliefKind.Hills),StartingArea=0}};
        var terrain=ReliefTerrain.Create(request,Profile.Terrain);
        var anchors=terrain.Spatial.Entities.Where(e=>terrain.OccupantAt(e.Anchor)?.Kind==TerrainOccupantKind.Mountain).Select(e=>e.Anchor).ToArray();
        // Choose opposing sloped surface cells at one integer level, with higher
        // natural ground between them. No artificial cuts or terraces are added.
        var routes=anchors.GroupBy(p=>(p.Y,p.Z)).Where(g=>g.Count()>1)
            .Select(g=>(From:g.MinBy(p=>p.X),To:g.MaxBy(p=>p.X)))
            .Where(r=>r.To.X-r.From.X>2 && Enumerable.Range(r.From.X+1,r.To.X-r.From.X-1).Any(x=>{
                var q=terrain.Space.ToStorage(new(x,r.From.Y,r.From.Z));return terrain.Cell(q.H,q.V).Ground>r.From.Z;
            })).ToArray();
        Assert.NotEmpty(routes);var (from,to)=routes[0];
        Assert.False(terrain.OccupantAt(from)!.Corners.Flat);Assert.False(terrain.OccupantAt(to)!.Corners.Flat);
        Assert.DoesNotContain(terrain.Cells,c=>c.Surface!=TerrainSurface.Natural);
        var w=MixedWorld.FromTerrain(Profile,terrain);var plan=TunnelRailwayConstruction.Preview(w,from,to);
        Assert.True(plan.Legal,plan.Reason);w=Apply(w,plan);
        Assert.Equal(RailwayHandler.Tunnel,Profile.Resolve(w.At(from)!.Traffic!.Rail!.Definition).Handler);
        Assert.Equal(RailwayHandler.Tunnel,Profile.Resolve(w.At(to)!.Traffic!.Rail!.Definition).Handler);
        Assert.Contains(w.Voxels,v=>{
            var p=terrain.Space.ToStorage(v.Position);
            return terrain.Cell(p.H,p.V).Ground>v.Position.Z && Profile.Resolve(v.Traffic!.Rail!.Definition).Handler==RailwayHandler.Normal;
        });
        var bytes=SaveCodec.Write(new MixedSessionState(new(w),TerrainProvenance.From(request),revision:w.Revision));
        w=SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!.World;
        w=Apply(w,TunnelRailwayConstruction.Preview(w,from,to,true));
        w=Apply(w,TunnelRailwayConstruction.Preview(w,to,from,true));
        Assert.Equal(terrain.OccupantAt(from)!.Corners,w.Terrain.OccupantAt(from)!.Corners);
        Assert.Equal(terrain.OccupantAt(to)!.Corners,w.Terrain.OccupantAt(to)!.Corners);
        Assert.True(terrain.Cells.SequenceEqual(w.Terrain.Cells));
    }

}
