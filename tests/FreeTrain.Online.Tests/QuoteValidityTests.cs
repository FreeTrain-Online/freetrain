using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public class QuoteValidityTests
{
    static SessionHost Host()=>new(StationBuildingConstructionTests.Empty(),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
    static async Task<WireMessage> Send(SessionHost h,CommandPayload payload)
    {var task=h.Enqueue(new("owner"),new(Guid.NewGuid().ToString(),h.WorldEpoch,payload));h.Pump(0);return await task;}
    static async Task<byte[]> Save(SessionHost h){var task=h.CaptureSave();h.Pump(0);return await task;}
    static async Task Advance(SessionHost h,int minutes)
    {for(var i=0;i<minutes;i++)Assert.IsType<AcceptedMessage>(await Send(h,new StepCommand()));}
    static FleetTradeQuery TrainQuery(SessionHost h)
    {
        var s=h.Snapshot();var b=RollingStockCatalog.Bundled.Trains[0].Binding;
        return new(s.WorldEpoch,s.Revision,s.Tick,s.ContentVersion,"buy",new(b.PackId,b.Version,b.CatalogHash,b.ContributionId),3,1,0);
    }
    static RailwayQuery RailQuery(SessionHost h,int y=10)
    {
        var s=h.Snapshot();return new(s.WorldEpoch,s.Mixed!.GeometryRevision,s.ContentVersion,new(10,y,2),new(14,y,2),"lay","source",ExpectedMinute:s.Tick);
    }
    static CommandPayload Quote(SessionHost h,string kind)
    {
        var s=h.Snapshot();var epoch=s.WorldEpoch;var revision=s.Mixed!.GeometryRevision;var minute=s.Tick;var content=s.ContentVersion;
        switch(kind)
        {
            case "train":var trade=TrainQuery(h);return new TradeFleetCommand(trade,h.PreviewFleetTrade(trade).Price);
            case "rail":var rail=RailQuery(h);Assert.True(h.PreviewRailway(rail).Legal);return new EditRailwayCommand(rail);
            case "slope":var slope=new SlopeQuery(epoch,revision,content,new(14,10,2),2,"lay",minute);Assert.True(h.PreviewSlope(slope).Legal);return new EditSlopeCommand(slope);
            case "terrain":var terrain=new CornerTerrainQuery(epoch,revision,new(10,10,2),"raise",ExpectedMinute:minute);Assert.True(h.PreviewCorner(terrain).Legal);return new EditCornerCommand(terrain);
            case "station":
                var d=StationStateTests.Definition.Reference;
                var station=new StationBuildingQuery(epoch,revision,minute,content,"place",new(10,10,2),new(d.PackId,d.ContributionId,d.RecipeId),0,0);
                var stationPreview=h.PreviewStationBuilding(station);Assert.True(stationPreview.Legal,stationPreview.Reason);return new EditStationBuildingCommand(station,stationPreview.Cost);
            case "building":
                var building=new FixedCityQuery(epoch,revision,minute,content,"place",new(10,10,2),new("exav-stable-bundled","{27D6D0C0-2B1A-4494-9257-14C208168075}","{27D6D0C0-2B1A-4494-9257-14C208168075}"),0,0);
                var buildingPreview=h.PreviewFixedCity(building);Assert.True(buildingPreview.Legal,buildingPreview.Reason);return new EditFixedCityCommand(building,buildingPreview.Cost);
            case "thin":
                var thin=new ThinPlatformQuery(epoch,revision,minute,content,"place",new(10,10,2),2,4,true,0,0);
                var thinPreview=h.PreviewThinPlatform(thin);Assert.True(thinPreview.Legal,thinPreview.Reason);return new EditThinPlatformCommand(thin,thinPreview.Cost);
            case "wide":
                var wide=new WidePlatformQuery(epoch,revision,minute,content,"place",new(10,10,2),2,4,false,0,0);
                var widePreview=h.PreviewWidePlatform(wide);Assert.True(widePreview.Legal,widePreview.Reason);return new EditWidePlatformCommand(wide,widePreview.Cost);
            case "passage":
                var passage=new PassageQuery(epoch,revision,minute,content,"place",new(10,10,3),new(14,10,3),"concrete");
                var passagePreview=h.PreviewPassage(passage);Assert.True(passagePreview.Legal,passagePreview.Reason);return new EditPassageCommand(passage,passagePreview.Cost);
            default:throw new ArgumentException(kind);
        }
    }

    [Theory]
    [InlineData("train")][InlineData("rail")][InlineData("slope")][InlineData("terrain")]
    [InlineData("station")][InlineData("building")][InlineData("thin")][InlineData("wide")][InlineData("passage")]
    public async Task Issued_quotes_survive_unrelated_geometry_and_a_half_day_of_simulation(string kind)
    {
        using var h=Host();var command=Quote(h,kind);
        // A different player action must not expire an unaffected quote either.
        var other=RailQuery(h,18);Assert.True(h.PreviewRailway(other).Legal);
        Assert.IsType<AcceptedMessage>(await Send(h,new EditRailwayCommand(other)));
        await Advance(h,720);
        var result=await Send(h,command);
        Assert.True(result is AcceptedMessage,$"{kind}: {result}");
    }
    [Theory]
    [InlineData("train")][InlineData("rail")][InlineData("slope")][InlineData("terrain")]
    [InlineData("station")][InlineData("building")][InlineData("thin")][InlineData("wide")][InlineData("passage")]
    public async Task Quotes_expire_after_720_minutes_without_mutating_the_world(string kind)
    {
        using var h=Host();var command=Quote(h,kind);await Advance(h,721);var before=await Save(h);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,command)).Code);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,command)).Code);
        Assert.Equal(before,await Save(h));
        Assert.IsType<AcceptedMessage>(await Send(h,Quote(h,kind)));
    }
    [Fact]
    public async Task Changed_reviewed_route_rejects_even_when_the_new_route_is_legal_and_a_quote_cannot_be_replayed()
    {
        using var h=Host();var command=Quote(h,"rail");
        Assert.IsType<AcceptedMessage>(await Send(h,command));var before=await Save(h);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,command)).Code);
        Assert.Equal(before,await Save(h));
        var removalQuery=new RemovalQuery(h.WorldEpoch,h.Snapshot().Mixed!.GeometryRevision,h.Snapshot().ContentVersion,new(10,10,2),0,0,new(14,10,2),h.Snapshot().Tick);
        var reviewed=h.PreviewRemoval(removalQuery);Assert.True(reviewed.Legal);
        var endpoint=h.Snapshot().Entities!.Single(e=>e.Anchor==new PositionRead(10,10,2));
        var removeEndpoint=new RemovalQuery(h.WorldEpoch,h.Snapshot().Mixed!.GeometryRevision,h.Snapshot().ContentVersion,endpoint.Anchor,endpoint.Id,endpoint.Revision,ExpectedMinute:h.Snapshot().Tick);
        Assert.True(h.PreviewRemoval(removeEndpoint).Legal);
        Assert.IsType<AcceptedMessage>(await Send(h,new RemoveObjectCommand(removeEndpoint)));
        var replacement=RailQuery(h) with {To=new(11,10,2)};
        Assert.True(h.PreviewRailway(replacement).Legal);
        Assert.IsType<AcceptedMessage>(await Send(h,new EditRailwayCommand(replacement)));
        var currentRemoval=h.PreviewRemoval(removalQuery with {ExpectedGeometryRevision=h.Snapshot().Mixed!.GeometryRevision});
        Assert.Equal(reviewed.Positions.OrderBy(p=>p.X),currentRemoval.Positions.OrderBy(p=>p.X));
        before=await Save(h);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,new RemoveObjectCommand(removalQuery))).Code);
        Assert.Equal(before,await Save(h));
    }
    [Fact]
    public void Preview_rejects_future_and_expired_clock_values_and_future_revisions()
    {
        using var h=Host();var q=TrainQuery(h);
        Assert.Throws<InvalidOperationException>(()=>h.PreviewFleetTrade(q with {ExpectedMinute=q.ExpectedMinute+1}));
        Assert.Throws<InvalidOperationException>(()=>h.PreviewFleetTrade(q with {ExpectedRevision=q.ExpectedRevision+1}));
        Assert.Throws<InvalidOperationException>(()=>h.PreviewRailway(RailQuery(h) with {ExpectedMinute=h.Snapshot().Tick+1}));
    }
}
