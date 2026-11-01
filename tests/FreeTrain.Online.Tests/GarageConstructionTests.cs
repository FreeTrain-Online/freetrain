using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Simulation;
using Xunit;
namespace FreeTrain.Online.Tests;

public class GarageConstructionTests
{
    static RailwayProfile Profile=>RailwayProfiles.Bundled;
    internal static Location Start=>new(14,10,2);
    internal static MixedWorld World()=>TunnelConstructionTests.World([]);
    static RailComponent Rail(int pattern=4,bool garage=false)=>new(garage?Profile.Bindings.Single(b=>b.Handler==RailwayHandler.Garage).Reference:Profile.Bindings.Single(b=>b.Handler==RailwayHandler.Normal&&b.Variant==pattern).Reference,pattern,garage&&pattern==10?1:0);
    static MixedWorld Add(MixedWorld w,Location p,RailComponent rail,bool car=false)
    {
        var id=w.Spatial.NextEntityId;
        var def=new DefinitionReference(Profile.Terrain.Packs[0].Id,"test:car","test:car");
        var profile=car&&!w.Profile.Bindings.Any(b=>b.Handler==RailwayHandler.Car)?new RailwayProfile(w.Profile.Terrain,w.Profile.Bindings.Add(new(def,RailwayHandler.Car))):w.Profile;
        return new(profile,w.Terrain,new(id+1,w.Spatial.Entities.Add(new(id,0,profile.TrafficOwner,p,new([new(new(0,0,0),true)]),true,false,0))),
            w.Voxels.Add(new(p,id,RailwayVoxelKind.Traffic,new(rail,Car:car?new(def,1,1):null))),w.Slopes,w.Revision);
    }
    static MixedWorld Apply(MixedWorld w,RailwayConstructionPlan p)=>GarageRailwayConstruction.Apply(new(w),p,new(0)).World;
    [Fact]
    public void Independent_literal_vectors_cover_replacement_cumulative_billing_gaps_and_cars()
    {
        using var f=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spatial/railway-fixtures.json")));
        var count=0;
        foreach(var row in f.RootElement.GetProperty("groups").EnumerateObject().SelectMany(g=>g.Value.GetProperty("cases").EnumerateArray()).Where(c=>c.GetProperty("id").GetString()!.StartsWith("GARAGE-")))
        {
            var input=row.GetProperty("input");var expected=row.GetProperty("expected");
            var d=input.GetProperty("direction").GetInt32();if(d%2!=0)continue;
            var data=input.GetProperty("cells").EnumerateArray().ToArray();var z=input.GetProperty("z").GetInt32();var(dx,dy)=new Heading(d).Offset;
            Location P(int n)=>new(Start.X+n*dx,Start.Y+n*dy,z);
            var w=World();
            var terrainCells=w.Terrain.Cells.ToBuilder();
            for(var i=0;i<data.Length;i++){var s=w.Terrain.Space.ToStorage(P(i));terrainCells[s.V*40+s.H]=new(data[i].GetProperty("ground").GetInt32(),TerrainSurface.Cut);}
            var terrain=new TerrainWorld(Profile.Terrain,"Garage",new(40,32,8),input.GetProperty("water").GetInt32(),terrainCells,new(1,[]),[]);
            w=MixedWorld.FromTerrain(Profile,terrain);
            for(var i=0;i<data.Length;i++)
            {
                var c=data[i];var kind=c.GetProperty("kind").GetString();if(kind=="empty")continue;
                var pattern=Enumerable.Range(0,28).First(p=>RailwayTopology.Ports(p).Order().SequenceEqual(c.GetProperty("edges").EnumerateArray().Select(e=>e.GetInt32()).Order()));
                w=Add(w,P(i),Rail(pattern,kind=="garage"),c.TryGetProperty("car",out var car)&&car.GetBoolean());
            }
            var plan=GarageRailwayConstruction.Preview(w,P(0),P(data.Length-1),input.GetProperty("action").GetString()=="remove");
            var legal=expected.GetProperty("outcome").GetString()=="apply";Assert.Equal(legal,plan.Legal);
            Assert.Equal(expected.GetProperty("spend").GetInt64(),plan.Booking.Total);
            if(legal)
            {
                Assert.Equal(expected.GetProperty("charges").EnumerateArray().Select(e=>e.GetInt64()),plan.Booking.Charges.Select(c=>c.Amount));
                var result=Apply(w,plan);Assert.Same(w.Terrain,result.Terrain);Assert.Equal(w.Spatial.NextEntityId+data.Count(c=>c.GetProperty("kind").GetString()=="empty"&&input.GetProperty("action").GetString()=="build"),result.Spatial.NextEntityId);
                foreach(var i in expected.GetProperty("changed").EnumerateArray().Select(e=>e.GetInt32()))Assert.Equal(input.GetProperty("action").GetString()=="build"?RailwayHandler.Garage:RailwayHandler.Normal,result.Profile.Resolve(result.At(P(i))!.Traffic!.Rail!.Definition).Handler);
            }
            count++;
        }
        Assert.True(count>=6);
    }
    [Theory]
    [InlineData(0,10,1)][InlineData(2,4,0)][InlineData(4,10,1)][InlineData(6,4,0)]
    public void Axes_lengths_ground_checks_snapping_and_normal_restoration(int direction,int pattern,int art)
    {
        foreach(var length in new[]{1,2,7})
        {
            var w=World();var(dx,dy)=new Heading(direction).Offset;var end=new Location(Start.X+length*dx,Start.Y+length*dy,2);
            w=Add(w,Start,Rail(pattern));var p=GarageRailwayConstruction.Preview(w,Start,end);Assert.True(p.Legal,p.Reason);
            Assert.Equal((length+1)*6_500_000,p.Booking.Total);w=Apply(w,p);
            Assert.All(w.Voxels,v=>{Assert.Equal(pattern,v.Traffic!.Rail!.Pattern);Assert.Equal(art,v.Traffic.Rail.Variant);});
            var removal=GarageRailwayConstruction.Preview(w,Start,end,true);Assert.Equal(200_000L*length*(length+1)/2,removal.Booking.Total);w=Apply(w,removal);
            Assert.Equal(RailwayHandler.Garage,w.Profile.Resolve(w.At(end)!.Traffic!.Rail!.Definition).Handler);
            w=Apply(w,GarageRailwayConstruction.Preview(w,end,Start,true));Assert.All(w.Voxels,v=>Assert.Equal(RailwayHandler.Normal,w.Profile.Resolve(v.Traffic!.Rail!.Definition).Handler));
        }
        Assert.Equal("garageGroundRequired",GarageRailwayConstruction.Preview(World(),Start with{Z=3},Start with{X=16,Z=3}).Reason);
        Assert.Equal("garageGroundRequired",GarageRailwayConstruction.Preview(World(),Start with{Z=1},Start with{X=16,Z=1}).Reason);
        Assert.Equal("samePoint",GarageRailwayConstruction.Preview(World(),Start,Start).Reason);
        Assert.Equal("routeWorkLimit",GarageRailwayConstruction.Preview(World(),Start,new(int.MaxValue,10,2)).Reason);
        Assert.Equal("outside",GarageRailwayConstruction.Preview(World(),Start,Start with{X=500}).Reason);
        Assert.Equal(new Location(17,10,2),GarageRailwayConstruction.Preview(World(),Start,new(17,11,7)).Topology.To);
        Assert.Equal("cannotAttach",GarageRailwayConstruction.Preview(Add(World(),Start,Rail(pattern==4?10:4)),Start,new(Start.X+(direction==2?1:direction==6?-1:0),Start.Y+(direction==4?1:direction==0?-1:0),2)).Reason);
    }
    [Fact]
    public void Prepared_plan_rejects_substitution_and_stale_predecessors()
    {
        var w=World();var end=Start with{X=16};var p=GarageRailwayConstruction.Preview(w,Start,end);var state=new MixedConstructionState(w);
        Assert.Throws<InvalidOperationException>(()=>GarageRailwayConstruction.Apply(state,NormalRailwayConstruction.PreviewBuild(w,Start,end),new(0)));
        var next=GarageRailwayConstruction.Apply(state,p,new(0));Assert.Throws<InvalidOperationException>(()=>GarageRailwayConstruction.Apply(next,p,new(0)));
    }
    [Fact]
    public void Cumulative_removal_reserves_every_journal_entry_before_atomic_publication()
    {
        var w=World();var end=Start with{X=16};w=Apply(w,GarageRailwayConstruction.Preview(w,Start,end));
        var accounts=new ConstructionAccounts().Book(new ConstructionBooking(Enumerable.Repeat(new AccountCharge(AccountGenre.RailService,0),8191)),w.Revision,new(0),"fixture");
        var state=new MixedConstructionState(w,accounts);var p=GarageRailwayConstruction.Preview(w,Start,end,true);
        Assert.Equal(2,p.Booking.Charges.Length);
        Assert.Equal("accountCapacityLimit",Assert.Throws<InvalidOperationException>(()=>GarageRailwayConstruction.Apply(state,p,new(0))).Message);
        Assert.Equal(8191,state.Accounts.Transactions.Length);Assert.All(w.Voxels,v=>Assert.Equal(RailwayHandler.Garage,w.Profile.Resolve(v.Traffic!.Rail!.Definition).Handler));
    }

    [Fact]
    public void Wrong_axis_and_gaps_retain_zero_spends_and_protected_car_roundtrips_exactly()
    {
        var w=Add(World(),Start,Rail(10,true));var end=Start with{X=16};
        var p=GarageRailwayConstruction.Preview(w,Start,end,true);Assert.True(p.Legal);
        Assert.Equal(new long[]{0,0},p.Booking.Charges.Select(c=>c.Amount));
        var next=GarageRailwayConstruction.Apply(new(w),p,new(0));Assert.True(w.Voxels.SequenceEqual(next.World.Voxels));
        Assert.Equal(2,next.Accounts.Transactions.Length);
        w=Add(World(),Start,Rail(4,true),true);w=Add(w,Start with{X=15},Rail(4,true));
        var provenance=TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,w.Terrain.Name,new(40,32,8),0));
        var before=new MixedSessionState(new(w),provenance);
        var bytes=FreeTrain.Online.Persistence.SaveCodec.Write(before);
        var reopened=FreeTrain.Online.Persistence.SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,(_,_,_)=>w.Profile).Mixed!;
        var a=GarageRailwayConstruction.Apply(before.Construction,GarageRailwayConstruction.Preview(w,Start,end,true),before.Clock.Time);
        var b=GarageRailwayConstruction.Apply(reopened.Construction,GarageRailwayConstruction.Preview(reopened.World,Start,end,true),reopened.Clock.Time);
        Assert.NotNull(a.World.At(Start)!.Traffic!.Car);Assert.Equal(RailwayHandler.Garage,w.Profile.Resolve(a.World.At(Start)!.Traffic!.Rail!.Definition).Handler);
        Assert.Equal(600_000,a.Accounts.Transactions.Sum(t=>t.Expenditure));
        Assert.Equal(FreeTrain.Online.Persistence.SaveCodec.Write(before.Successor(before.Revision,a,before.ConstructionMode)),
            FreeTrain.Online.Persistence.SaveCodec.Write(reopened.Successor(reopened.Revision,b,reopened.ConstructionMode)));
    }

}
