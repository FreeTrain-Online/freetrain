using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;
namespace FreeTrain.Online.Tests;

public class FixedCitySessionTests
{
    static async Task<byte[]> Save(SessionHost h){var task=h.CaptureSave();h.Pump(0);return await task;}
    static async Task<WireMessage> Send(SessionHost h,CommandPayload p,ParticipantContext? who=null)
    {var t=h.Enqueue(who??new("owner"),new(Guid.NewGuid().ToString(),h.WorldEpoch,p));h.Pump(0);return await t;}
    static FixedCityQuery Query(SessionHost h,bool remove=false)
    {
        var s=h.Snapshot();var b=s.Mixed?.City?.Structures.FirstOrDefault();
        return new(s.WorldEpoch,s.Mixed?.GeometryRevision??s.Terrain!.GeometryRevision,s.Tick,s.ContentVersion,remove?"remove":"place",
            new(14,12,2),remove?null:new("exav-stable-bundled","{27D6D0C0-2B1A-4494-9257-14C208168075}","{27D6D0C0-2B1A-4494-9257-14C208168075}"),remove?b!.Id:0,remove?b!.Revision:0);
    }
    [Fact]
    public async Task Completed_fixed_city_demolition_reviews_the_actual_charge_and_detaches_the_real_station_provider()
    {
        var state=CityHeat.Enable(StationBuildingConstructionTests.Build(StationBuildingConstructionTests.Empty(now:1980),at:new(12,12,2)));
        var d=state.World.City!.Profile.Structures.Single(d=>d.Reference.ContributionId=="{27D6D0C0-2B1A-4494-9257-14C208168075}");
        state=FixedCityConstruction.Commit(state,FixedCityConstruction.Preview(new(state.Construction,state.Clock.Capture(),state.Random.Capture()),new(14,12,2),d.Reference));
        for(var i=0;i<15000&&state.World.City!.State.Producers.IsEmpty;i++)state.AdvanceOneMinute();
        Assert.Single(state.World.City!.State.Producers);Assert.Single(state.World.Stations!.Listeners);
        using var h=new SessionHost(state,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var q=Query(h,true);var p=h.PreviewFixedCity(q);Assert.True(p.Legal);Assert.Equal(500_000,p.Cost);
        var saved=await Save(h);Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,new EditFixedCityCommand(q,p.Cost+1))).Code);Assert.Equal(saved,await Save(h));
        Assert.IsType<AcceptedMessage>(await Send(h,new EditFixedCityCommand(q,p.Cost)));
        Assert.Empty(h.Snapshot().Mixed!.City!.Producers);Assert.Empty(h.Snapshot().Mixed!.StationGraph!.Listeners);
        Assert.All(h.Snapshot().Mixed!.StationGraph!.ListenerMemberships,m=>Assert.Empty(m.Providers));
    }
    [Fact]
    public async Task First_train_purchase_after_city_construction_preserves_city_stations_and_pending_callbacks()
    {
        var state=CityHeat.Enable(StationBuildingConstructionTests.Build(StationBuildingConstructionTests.Empty(now:1980),at:new(12,12,2)));
        using var h=new SessionHost(state,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var building=Query(h);var preview=h.PreviewFixedCity(building);
        Assert.True(preview.Legal,preview.Reason);
        Assert.IsType<AcceptedMessage>(await Send(h,new EditFixedCityCommand(building,preview.Cost)));
        var before=h.Snapshot();var saved=await Save(h);
        Assert.Null(before.Mixed!.Fleet);Assert.NotEmpty(before.Mixed.City!.Structures);
        var b=RollingStockCatalog.Bundled.Trains[0].Binding;
        var query=new FleetTradeQuery(before.WorldEpoch,before.Revision,before.Tick,before.ContentVersion,"buy",
            new(b.PackId,b.Version,b.CatalogHash,b.ContributionId),3,1,0);
        var quote=h.PreviewFleetTrade(query);
        Assert.Equal(saved,await Save(h));
        Assert.IsType<AcceptedMessage>(await Send(h,new TradeFleetCommand(query,quote.Price)));
        var after=h.Snapshot();
        var json=WireJson.CreateOptions();
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before.Mixed.City,json),System.Text.Json.JsonSerializer.Serialize(after.Mixed!.City,json));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before.Mixed.StationGraph,json),System.Text.Json.JsonSerializer.Serialize(after.Mixed.StationGraph,json));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before.Mixed.Queue,json),System.Text.Json.JsonSerializer.Serialize(after.Mixed.Queue,json));
        Assert.Single(after.Mixed.Fleet!.Trains);Assert.Equal(before.Tick,after.Tick);
        var purchased=await Save(h);
        var reopened=SaveCodec.ReadWorld(purchased,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
        Assert.Equal(purchased,SaveCodec.Write(reopened));
        using var restored=new SessionHost(reopened,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        Assert.IsType<AcceptedMessage>(await Send(h,new StepCommand()));
        Assert.IsType<AcceptedMessage>(await Send(restored,new StepCommand()));
        Assert.Equal(await Save(h),await Save(restored));
    }
    [Fact]
    public void Playable_city_browser_seed_is_empty_source_clock_without_producers_or_completion_shortcut()
    {
        var s=StationBuildingConstructionTests.Empty(now:1980);
        Assert.Empty(s.World.Spatial.Entities);Assert.Null(s.World.City);
        var bytes=SaveCodec.Write(s);
        Assert.Equal(bytes,SaveCodec.Write(SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!));
        if(Environment.GetEnvironmentVariable("FREETRAIN_PLAYABLE_CITY_OUTPUT") is {} path) File.WriteAllBytes(path,bytes);
    }
    [Fact]
    public async Task Fixed_city_preview_activation_cost_roles_minute_and_removal_are_atomic_and_reopen_exactly()
    {
        using var h=new SessionHost(StationBuildingConstructionTests.Empty(),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var original=await Save(h);var q=Query(h);var p=h.PreviewFixedCity(q);
        Assert.True(p.Legal,p.Reason);Assert.Equal(11_000_000,p.Cost);Assert.Single(p.Positions);Assert.Equal(original,await Save(h));
        var observer=new ParticipantContext("viewer",ParticipantRole.Observer);h.RegisterParticipant(observer);
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await Send(h,new EditFixedCityCommand(q,p.Cost),observer)).Code);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,new EditFixedCityCommand(q,p.Cost+1))).Code);
        Assert.Equal(original,await Save(h));
        Assert.IsType<AcceptedMessage>(await Send(h,new EditFixedCityCommand(q,p.Cost)));
        var saved=await Save(h);Assert.NotEmpty(h.Snapshot().Mixed!.City!.Structures[0].Construction);
        Assert.Equal(saved,SaveCodec.Write(SaveCodec.ReadWorld(saved,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!));
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,new EditFixedCityCommand(q,p.Cost))).Code);
        var blocked=h.PreviewFixedCity(Query(h));Assert.False(blocked.Legal);Assert.Equal(saved,await Save(h));
        q=Query(h,true);p=h.PreviewFixedCity(q);Assert.True(p.Legal);Assert.Equal(0,p.Cost);
        Assert.IsType<AcceptedMessage>(await Send(h,new StepCommand()));saved=await Save(h);
        Assert.IsType<AcceptedMessage>(await Send(h,new EditFixedCityCommand(q,p.Cost)));
        Assert.Empty(h.Snapshot().Mixed!.City!.Structures);Assert.DoesNotContain(h.Snapshot().Mixed!.Queue!.Pending,t=>t.HandlerId.StartsWith("city/construction/"));
    }
}
