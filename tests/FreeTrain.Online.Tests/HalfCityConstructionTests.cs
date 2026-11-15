using System.Text.Json;
using System.Collections.Immutable;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;
namespace FreeTrain.Online.Tests;

public class HalfCityConstructionTests
{
    static CityStructureDefinition[] Definitions=>RailwayProfiles.Bundled.City!.Structures.Where(d=>d.Kind==CityStructureKind.Half).ToArray();
    static Location P=>new(14,12,2);
    static CityCandidate Capture(MixedSessionState s)=>new(s.Construction,s.Clock.Capture(),s.Random.Capture());
    static MixedSessionState Empty()=>CityHeat.Enable(StationBuildingConstructionTests.Build(StationBuildingConstructionTests.Empty(now:1980),at:new(12,12,2)));
    static MixedSessionState Place(MixedSessionState s,CityStructureDefinition d,HalfSide side=HalfSide.Back,int direction=0,Location? p=null,int color=0)=>
        FixedCityConstruction.Commit(s,HalfCityConstruction.Preview(Capture(s),p??P,new(d.Reference,side,direction,color)));
    static MixedSessionState Remove(MixedSessionState s,HalfSide? side=null)
    {var b=Assert.Single(s.World.City!.State.Structures);return FixedCityConstruction.Commit(s,HalfCityConstruction.PreviewRemoval(Capture(s),P,b.Id,b.Revision,side));}
    static void Reopen(MixedSessionState s)
    {var bytes=SaveCodec.Write(s);var reopened=SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;Assert.Equal(bytes,SaveCodec.Write(reopened));s.AdvanceOneMinute();reopened.AdvanceOneMinute();Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(reopened));}
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Both_creation_orders_keep_initial_listener_price_snapshot_timer_and_identity_through_partial_removal(bool reverse)
    {
        var a=Definitions[reverse?7:0];var b=Definitions[reverse?0:7];var s=Empty();var original=SaveCodec.Write(s);
        var plan=HalfCityConstruction.Preview(Capture(s),P,new(a.Reference,HalfSide.Back,0));
        Assert.Equal(original,SaveCodec.Write(s));s=FixedCityConstruction.Commit(s,plan);
        var first=Assert.Single(s.World.City!.State.Structures);var listener=Assert.Single(s.World.Stations!.Listeners);
        Assert.Equal(2_500_000,s.Construction.Accounts.Transactions.Last().Expenditure);Assert.Empty(first.Construction);Assert.True(first.Owned);
        Assert.Equal("city-half",listener.Provider!.Input);Assert.Equal((1,1),(listener.Provider.XYSize,listener.Provider.ZSize));
        Assert.Equal(a.Population!.Residents,listener.Provider.Population.Residents);
        var random=JsonSerializer.Serialize(s.Random.Capture());var pending=JsonSerializer.Serialize(s.Clock.Capture());var bookings=s.Construction.Accounts.Transactions;
        s=Place(s,b,HalfSide.Fore,4);var pair=Assert.Single(s.World.City!.State.Structures);
        Assert.Equal(first.Id,pair.Id);Assert.Equal(first.Revision+1,pair.Revision);Assert.Equal(first.Subsidiary,pair.Subsidiary);
        Assert.Equal(bookings,s.Construction.Accounts.Transactions);Assert.Equal(5_000_000,s.World.City.StructurePrice(pair));
        Assert.Equal(listener,Assert.Single(s.World.Stations!.Listeners));Assert.Equal(random,JsonSerializer.Serialize(s.Random.Capture()));Assert.Equal(pending,JsonSerializer.Serialize(s.Clock.Capture()));
        Assert.Single(s.World.City.SpatialOwner(first.Id).Footprint.Members);
        s=Remove(s,HalfSide.Back);var remaining=Assert.Single(s.World.City!.State.Structures);Assert.Equal(b.Reference,Assert.Single(remaining.Halves).Definition);
        Assert.Equal(a.Reference,remaining.Definition);Assert.Equal(a.Reference,Assert.Single(s.World.City.State.Producers).InitialDefinition);
        Assert.Equal(first.Subsidiary,remaining.Subsidiary);Assert.Equal(listener,Assert.Single(s.World.Stations!.Listeners));
        Reopen(s);s=Remove(s,HalfSide.Fore);Assert.Empty(s.World.City!.State.Structures);Assert.Empty(s.World.City.State.Producers);
        Assert.Empty(s.World.Stations!.Listeners);Assert.Empty(Assert.Single(s.World.Stations.ListenerMemberships).Providers);
        Assert.Equal(300_000,s.Construction.Accounts.Transactions.Last().Expenditure);
        Assert.DoesNotContain(s.Clock.Capture().Pending,a=>a.HandlerId==CityHandlers.Subsidiary(first.Id));Reopen(s);
    }
    [Fact]
    public void Every_bundled_half_direction_side_has_real_construction_and_whole_demolition_with_saved_continuation()
    {
        Assert.Equal(8,Definitions.Length);
        Assert.All(Definitions,d=>Assert.Equal(5_000_000,d.PricePerArea));
        foreach(var d in Definitions)foreach(var variant in d.HalfVariants)
        {
            var before=Empty();var s=Place(before,d,(HalfSide)variant.Side,variant.Direction,color:variant.Color);
            MixedWire.ValidateTransition(before,s);var b=Assert.Single(s.World.City!.State.Structures);
            Assert.Equal(new CityHalf(d.Reference,(HalfSide)variant.Side,variant.Direction,variant.Color),Assert.Single(b.Halves));
            Assert.Equal(2_500_000,s.Construction.Accounts.Transactions.Last().Expenditure);Reopen(s);
            var removed=Remove(s);MixedWire.ValidateTransition(s,removed);Assert.Equal(300_000,removed.Construction.Accounts.Transactions.Last().Expenditure);Reopen(removed);
        }
    }
    [Fact]
    public void Duplicate_sides_cross_axes_invalid_selectors_protected_occupancy_and_outside_reject_atomically()
    {
        var s=Place(Empty(),Definitions[0]);var bytes=SaveCodec.Write(s);
        foreach(var (side,dir,color) in new[]{(HalfSide.Back,0,0),(HalfSide.Fore,2,0),(HalfSide.Fore,1,0),(HalfSide.Fore,4,99)})
            Assert.Throws<ArgumentException>(()=>Place(s,Definitions[1],side,dir,color:color));
        Assert.Throws<ArgumentException>(()=>Place(s,Definitions[0],p:new(12,12,2))); // actual station
        Assert.Throws<ArgumentException>(()=>Place(s,Definitions[0],p:P with {Z=1}));
        Assert.Throws<ArgumentException>(()=>Place(s,Definitions[0],p:new(-100,0,2)));
        Assert.Equal(bytes,SaveCodec.Write(s));
        // Storage reserves only the base, so a real upper neighbor remains legal.
        var elevated=Place(s,Definitions[1],p:P with {Z=3});Assert.Equal(2,elevated.World.City!.State.Structures.Length);Reopen(elevated);
        var pair=Place(s,Definitions[1],HalfSide.Fore,4);Assert.Throws<ArgumentException>(()=>Place(pair,Definitions[2],HalfSide.Fore,4));
        var removed=Remove(pair,HalfSide.Back);Assert.Throws<ArgumentException>(()=>Remove(removed,HalfSide.Back));
        var rebuilt=Place(removed,Definitions[2],HalfSide.Back,4);Assert.Equal(s.World.City!.State.Producers,rebuilt.World.City!.State.Producers);
    }
    [Fact]
    public void Road_and_rail_neighbors_are_preserved_and_cannot_be_reclaimed_as_half_sites()
    {
        var s=Empty();var w=s.World;var next=w.Spatial.NextEntityId;
        var road=P with {X=P.X+1};var rail=P with {Y=P.Y+1};
        var positions=new[]{road,rail};
        var entities=positions.Select((p,i)=>new SpatialEntity(next+i,0,w.Profile.TrafficOwner,p,Footprint.Box(new(new(0,0,0),1,1,1),true),true,false,0));
        var voxels=new[]{new RailwayVoxel(road,next,RailwayVoxelKind.Traffic,new(Road:new(w.Profile.City!.Roads[0].Reference,17))),
            new RailwayVoxel(rail,next+1,RailwayVoxelKind.Traffic,new(Rail:new(new(w.Profile.City.Packs[0].Id,"system:pattern/4","system:pattern/4"),4)))};
        w=new(w.Profile,w.Terrain,new(next+2,w.Spatial.Entities.Concat(entities).ToImmutableArray()),voxels,[],w.Revision,stations:w.Stations,city:w.City);
        s=new(new(w,s.Construction.Accounts),s.Provenance,clock:s.Clock.Capture(),random:s.Random.Capture(),revision:s.Revision);
        var built=Place(s,Definitions[0]);Assert.Equal(voxels,built.World.Voxels);Reopen(built);
        Assert.Throws<ArgumentException>(()=>Place(built,Definitions[1],p:road));Assert.Throws<ArgumentException>(()=>Place(built,Definitions[1],p:rail));
        var bytes=SaveCodec.Write(built);
        var invalidClock=new CityCandidate(built.Construction,built.Clock.Capture() with {NextOrder=long.MaxValue},built.Random.Capture());
        Assert.Throws<OverflowException>(()=>HalfCityConstruction.Preview(invalidClock,P with {X=P.X+2},new(Definitions[0].Reference,HalfSide.Back,0)));
        Assert.Equal(bytes,SaveCodec.Write(built));
    }
    [Fact]
    public async Task Shared_content_change_requires_renewed_placement_review_even_when_owner_and_price_are_unchanged()
    {
        using var host=new SessionHost(Empty(),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        async Task<byte[]> Save(){var task=host.CaptureSave();host.Pump(0);return await task;}
        async Task<WireMessage> Send(FixedCityQuery q,long cost){var task=host.Enqueue(new("owner"),new(Guid.NewGuid().ToString(),host.WorldEpoch,new EditFixedCityCommand(q,cost)));host.Pump(0);return await task;}
        FixedCityQuery Query(string side,int definition=0,bool remove=false){
            var s=host.Snapshot();var b=s.Mixed!.City!.Structures.FirstOrDefault();var d=Definitions[definition].Reference;
            return new(s.WorldEpoch,s.Mixed.GeometryRevision,s.Tick,s.ContentVersion,remove?"remove":"place",new(P.X,P.Y,P.Z),remove?null:new(d.PackId,d.ContributionId,d.RecipeId),remove?b!.Id:0,remove?b!.Revision:0,Half:new(side,0,0));
        }
        async Task Apply(string side,int definition=0,bool remove=false){var q=Query(side,definition,remove);var p=host.PreviewFixedCity(q);Assert.True(p.Legal);Assert.IsType<AcceptedMessage>(await Send(q,p.Cost));}
        await Apply("back");await Apply("fore",1);await Apply("back",remove:true);
        var original=Query("back");Assert.Equal(0,host.PreviewFixedCity(original).Cost);
        await Apply("back",2);await Apply("fore",remove:true);await Apply("fore",3);await Apply("back",remove:true);
        var bytes=await Save();var rejected=Assert.IsType<RejectedMessage>(await Send(original,0));Assert.Equal("staleRevision",rejected.Code);Assert.Equal(bytes,await Save());
    }
    [Fact]
    public async Task Half_authority_quotes_roles_costs_stale_reviews_and_selected_side_removal_are_atomic()
    {
        using var h=new SessionHost(Empty(),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        async Task<byte[]> Save(){var task=h.CaptureSave();h.Pump(0);return await task;}
        async Task<WireMessage> Send(CommandPayload p,ParticipantContext? participant=null){var task=h.Enqueue(participant??new("owner"),new(Guid.NewGuid().ToString(),h.WorldEpoch,p));h.Pump(0);return await task;}
        FixedCityQuery Query(string action="place",string side="back"){
            var s=h.Snapshot();var b=s.Mixed!.City!.Structures.FirstOrDefault();var d=Definitions[0].Reference;
            return new(s.WorldEpoch,s.Mixed.GeometryRevision,s.Tick,s.ContentVersion,action,new(P.X,P.Y,P.Z),action=="place"?new(d.PackId,d.ContributionId,d.RecipeId):null,action=="remove"?b!.Id:0,action=="remove"?b!.Revision:0,Half:new(side,0,0));
        }
        var bytes=await Save();var q=Query();var p=h.PreviewFixedCity(q);Assert.True(p.Legal);Assert.Equal(2_500_000,p.Cost);Assert.Single(p.Positions);Assert.Equal(bytes,await Save());
        var observer=new ParticipantContext("observer",ParticipantRole.Observer);h.RegisterParticipant(observer);
        Assert.IsType<RejectedMessage>(await Send(new EditFixedCityCommand(q,p.Cost),observer));
        Assert.IsType<RejectedMessage>(await Send(new EditFixedCityCommand(q,0)));Assert.Equal(bytes,await Save());
        Assert.IsType<AcceptedMessage>(await Send(new EditFixedCityCommand(q,p.Cost)));
        Assert.IsType<RejectedMessage>(await Send(new EditFixedCityCommand(q,p.Cost)));
        Assert.False(h.PreviewFixedCity(Query()).Legal);Assert.False(h.PreviewFixedCity(Query() with {Half=null}).Legal);
        q=Query(side:"fore");p=h.PreviewFixedCity(q);Assert.True(p.Legal);Assert.Equal(0,p.Cost);Assert.IsType<AcceptedMessage>(await Send(new EditFixedCityCommand(q,p.Cost)));
        q=Query("remove");p=h.PreviewFixedCity(q);Assert.Equal(0,p.Cost);Assert.IsType<AcceptedMessage>(await Send(new EditFixedCityCommand(q,p.Cost)));
        q=Query("remove","fore");p=h.PreviewFixedCity(q);Assert.Equal(300_000,p.Cost);Assert.IsType<AcceptedMessage>(await Send(new EditFixedCityCommand(q,p.Cost)));Assert.Empty(h.Snapshot().Mixed!.City!.Structures);
    }
}
