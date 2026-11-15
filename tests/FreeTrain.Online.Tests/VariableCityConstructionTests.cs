using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;
namespace FreeTrain.Online.Tests;

public class VariableCityConstructionTests
{
    static readonly Location At=new(14,12,2);
    static CityStructureDefinition Apartment=>RailwayProfiles.Bundled.City!.Structures.Single(d=>d.Reference.ContributionId=="{C7536844-7CD0-49e0-8982-2819F5792B32}");
    static CityCandidate Capture(MixedSessionState s)=>new(s.Construction,s.Clock.Capture(),s.Random.Capture());
    static MixedSessionState Empty()
    {
        var t=new TerrainWorld(TerrainProfiles.Bundled,"Variable city",new(40,32,16),0,Enumerable.Repeat(new TerrainCell(2),40*32),new(1,[]),[]);
        var graph=CityGraph.Empty(RailwayProfiles.Bundled.City!,t.Space);
        graph=new(graph.Profile,graph.State with {Heat=graph.State.Heat with {Q=graph.State.Heat.Q.Select(_=>100f).ToImmutableArray()}});
        var w=new MixedWorld(RailwayProfiles.Bundled,t,new(1,[]),[],[],city:graph);
        var s=new MixedSessionState(new(w),TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,t.Name,new(40,32,16),0)),clock:new(1980,0,[]),random:new RandomStreams(7).Capture());
        return StationBuildingConstructionTests.Build(s,at:new(12,12,2));
    }
    static MixedSessionState Open(byte[] bytes)=>SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
    [Fact]
    public void All_variable_source_definition_heights_review_exact_volume_charge_and_reject_limits_without_effects()
    {
        using var f=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spatial/city-fixtures.json")));
        var s=Empty();var original=SaveCodec.Write(s);var count=0;
        foreach(var row in f.RootElement.GetProperty("groups").GetProperty("definitions").GetProperty("cases").EnumerateArray())
        {
            var input=row.GetProperty("input");var id=input.GetProperty("definition").GetString()!.Split(':')[^1];
            var d=s.World.City!.Profile.Structures.Single(d=>d.Reference.ContributionId==id);if(d.Kind!=CityStructureKind.Variable)continue;
            var h=input.GetProperty("height").GetInt32();var expected=row.GetProperty("expected");
            var p=FixedCityConstruction.Preview(Capture(s),At,d.Reference,selectedHeight:h);
            var after=FixedCityConstruction.Commit(s,p);var site=Assert.Single(after.World.City!.State.Structures);
            Assert.Equal(expected.GetProperty("occupied").GetInt32(),site.Construction.Length);
            Assert.Equal(expected.GetProperty("price").GetInt64(),after.Construction.Accounts.Transactions[^1].Expenditure);
            Assert.Empty(after.World.City.State.Producers);Assert.Empty(after.World.Stations!.Listeners);
            Assert.Equal(h,site.Height);Assert.Equal(site.Id,after.World.Spatial.At(At with {Z=At.Z+h-1}).Entity!.Id);
            Assert.Throws<ArgumentException>(()=>FixedCityConstruction.Preview(Capture(after),At,d.Reference,selectedHeight:h));
            Assert.Throws<ArgumentException>(()=>FixedCityConstruction.Preview(Capture(s),At,d.Reference,selectedHeight:d.MinimumHeight-1));
            Assert.Throws<ArgumentException>(()=>FixedCityConstruction.Preview(Capture(s),At,d.Reference,selectedHeight:d.MaximumHeight+1));
            Assert.Throws<ArgumentException>(()=>FixedCityConstruction.Preview(Capture(s),At with {Z=15},d.Reference,selectedHeight:h));count++;
        }
        Assert.Equal(159,count);Assert.Equal(original,SaveCodec.Write(s));
    }
    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(8)]
    public void Natural_variable_completion_multiplies_after_child_truncation_binds_scales_and_continues_exactly(int height)
    {
        var d=height==8?RailwayProfiles.Bundled.City!.Structures.Single(d=>d.Reference.ContributionId=="{985CAC71-11BD-44b7-917E-D6D455CE0143}"):Apartment;
        var population=height==8?500:120;var area=height==8?4:1;
        var s=Empty();var plan=FixedCityConstruction.Preview(Capture(s),At,d.Reference,selectedHeight:height);
        s=FixedCityConstruction.Commit(s,plan);var restored=Open(SaveCodec.Write(s));
        var site=s.World.City!.State.Structures.Single();
        for(var i=0;i<15000&&s.World.City!.State.Producers.IsEmpty;i++)
        {s.AdvanceOneMinute();restored.AdvanceOneMinute();if(i%1000==0)Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));}
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));
        var owner=Assert.Single(s.World.City!.State.Structures);Assert.Empty(owner.Construction);Assert.NotEqual(site.Id,owner.Id);
        Assert.Equal(d.UnitPrice*height,s.World.Spatial.At(At).Entity!.Value);
        var provider=Assert.Single(s.World.Stations!.Listeners).Provider!;
        Assert.Equal("city-variable",provider.Input);Assert.Equal((area,height),(provider.XYSize,provider.ZSize));
        Assert.Equal(population*height,provider.Population.Residents);
        // Literal ResidentialPopulation holiday 09:00 40%, weekday 80%.
        Assert.Equal(population*40/100*height,provider.Population.Outgoing(new(1980)));
        Assert.Equal(population*80/100*height,provider.Population.Outgoing(new(4860)));
        Assert.Equal(Math.Min(population*40/100*height,provider.LandValue*area*Math.Max(1,height/4)+10),StationCatchment.Demand(s.World.Stations,1,new(1980)));
        var before=SaveCodec.Write(s);var removal=FixedCityConstruction.PreviewRemoval(Capture(s),At with {Z=At.Z+height-1},owner.Id,owner.Revision);
        Assert.Equal(before,SaveCodec.Write(s));s=FixedCityConstruction.Commit(s,removal);
        Assert.Equal(500_000*area,s.Construction.Accounts.Transactions[^1].Expenditure);Assert.Empty(s.World.City!.State.Structures);Assert.Empty(s.World.City.State.Producers);
        Assert.Empty(s.World.Stations!.Listeners);Assert.All(s.World.Stations.ListenerMemberships,m=>Assert.Empty(m.Providers));
        Assert.DoesNotContain(s.Clock.Capture().Pending,a=>a.HandlerId==CityHandlers.Subsidiary(owner.Id));
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(Open(SaveCodec.Write(s))));
    }
    [Fact]
    public void Taller_height_detects_upper_obstacle_and_legacy_variable_uses_vacant_volume_without_ground_or_upfront_charge()
    {
        var s=Empty();var elevated=At with {Z=5};
        var legacy=FixedCityConstruction.Preview(Capture(s),elevated,Apartment.Reference,CityConstructionCaller.LegacyPlayer,2);
        Assert.Equal(s.Construction.Accounts.Transactions.Length,legacy.Candidate.Construction.Accounts.Transactions.Length);
        s=FixedCityConstruction.Commit(s,legacy);var before=SaveCodec.Write(s);
        Assert.NotNull(FixedCityConstruction.Preview(Capture(s),At,Apartment.Reference,selectedHeight:2));
        Assert.Throws<ArgumentException>(()=>FixedCityConstruction.Preview(Capture(s),At,Apartment.Reference,selectedHeight:5));
        Assert.Throws<ArgumentException>(()=>FixedCityConstruction.Preview(Capture(s),elevated,Apartment.Reference,CityConstructionCaller.LegacyPlayer,2));
        Assert.Equal(before,SaveCodec.Write(s));
    }
    [Fact]
    public async Task Selected_height_is_part_of_authoritative_quote_and_observer_cost_and_site_fences()
    {
        using var host=new SessionHost(Empty(),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        async Task<byte[]> Save(){var t=host.CaptureSave();host.Pump(0);return await t;}
        async Task<WireMessage> Send(FixedCityQuery q,long price,ParticipantContext? who=null){var t=host.Enqueue(who??new("owner"),new(Guid.NewGuid().ToString(),host.WorldEpoch,new EditFixedCityCommand(q,price)));host.Pump(0);return await t;}
        var s=host.Snapshot();var d=Apartment.Reference;
        var q=new FixedCityQuery(s.WorldEpoch,s.Mixed!.GeometryRevision,s.Tick,s.ContentVersion,"place",new(14,12,2),new(d.PackId,d.ContributionId,d.RecipeId),0,0,5);
        var original=await Save();var p=host.PreviewFixedCity(q);Assert.True(p.Legal);Assert.Equal(Apartment.UnitPrice*5,p.Cost);Assert.Equal(5,p.Positions.Length);Assert.Equal(original,await Save());
        Assert.False(host.PreviewFixedCity(q with {Height=0}).Legal);Assert.False(host.PreviewFixedCity(q with {Height=6}).Legal);
        var viewer=new ParticipantContext("viewer",ParticipantRole.Observer);host.RegisterParticipant(viewer);
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await Send(q,p.Cost,viewer)).Code);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(q,p.Cost-1)).Code);Assert.Equal(original,await Save());
        Assert.IsType<AcceptedMessage>(await Send(q,p.Cost));var saved=await Save();Assert.Equal(5,host.Snapshot().Mixed!.City!.Structures.Single().Height);
        Assert.False(host.PreviewFixedCity(q with {Height=2}).Legal);Assert.Equal(saved,await Save());Assert.Equal(saved,SaveCodec.Write(Open(saved)));
    }
}
