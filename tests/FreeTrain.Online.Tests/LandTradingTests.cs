using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Server;
using FreeTrain.Online.Simulation;
using Xunit;
namespace FreeTrain.Online.Tests;

public class LandTradingTests
{
    static readonly Location P=new(14,12,2);
    static CityCandidate Capture(MixedSessionState s)=>new(s.Construction,s.Clock.Capture(),s.Random.Capture());
    public static MixedSessionState Empty(int water=0)
    {
        var t=new TerrainWorld(TerrainProfiles.Bundled,"Land trading",new(40,32,16),water,
            Enumerable.Repeat(new TerrainCell(2),40*32),new(1,[]),[]);
        var s=new MixedSessionState(new(MixedWorld.FromTerrain(RailwayProfiles.Bundled,t)),
            TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,t.Name,new(40,32,16),water)),clock:new(1980,0,[]));
        s=CityHeat.Enable(s);
        return With(s,s.World.City!.State with {Heat=s.World.City.State.Heat with {Q=s.World.City.State.Heat.Q.Select(_=>100f).ToImmutableArray()}});
    }
    public static MixedSessionState With(MixedSessionState s,CityState state,TerrainWorld? terrain=null)
    {
        var w=s.World;var graph=new CityGraph(w.City!.Profile,state);
        var entities=w.Spatial.Entities.Where(e=>!w.City.IsSpatialOwner(e.Id)).Concat(graph.Identities.Select(i=>graph.SpatialOwner(i.Id))).ToImmutableArray();
        var next=Math.Max(w.Spatial.NextEntityId,entities.Select(e=>e.Id).DefaultIfEmpty().Max()+1);
        return new(new(new MixedWorld(w.Profile,terrain??w.Terrain,new(next,entities),w.Voxels,w.Slopes,w.Revision,w.SpeedLimits,w.Fleet,w.Stations,graph),s.Construction.Accounts),
            s.Provenance,clock:s.Clock.Capture(),random:s.Random.Capture(),revision:s.Revision);
    }
    static LandTradePlan Plan(MixedSessionState s,string action="buy",Location? a=null,Location? b=null)=>LandTrading.Preview(Capture(s),a??P,b??a??P,action);
    static MixedSessionState Trade(MixedSessionState s,string action="buy",Location? a=null,Location? b=null)=>LandTrading.Commit(s,Plan(s,action,a,b));
    static void Exact(MixedSessionState s){var bytes=SaveCodec.Write(s);Assert.Equal(bytes,SaveCodec.Write(SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!));}
    public static MixedSessionState Fixture()
    {
        var s=Empty();var g=s.World.City!.Profile.Ground.First(d=>d.Handler=="land-0d"&&d.Population!=null);
        s=With(s,s.World.City.State with {Ground=[new(1,0,g.Reference,P,false),new(2,0,g.Reference,new(15,12,2),true)],
            Producers=[new(1,g.Reference,1),new(2,g.Reference,1)],Properties=[new(3,0,new(14,13,2))]});
        return s;
    }
    [Fact]
    public void Six_independent_source_trade_vectors_include_shared_owner_dedup_nested_fees_and_current_sale()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Spatial/city-fixtures.json")));
        var count=0;
        foreach(var row in doc.RootElement.GetProperty("groups").GetProperty("trade").GetProperty("cases").EnumerateArray())
        {
            var input=row.GetProperty("input");var expected=row.GetProperty("expected");var sell=input.GetProperty("operation").GetString()=="sell";
            var engine=new LandTradeTraversal(sell);var result=new List<string?>();var properties=0L;
            foreach(var cell in input.GetProperty("cells").EnumerateArray())
            {
                var name=cell.GetProperty("entity").GetString();LandTradeOwner? owner=null;
                if(name=="property")owner=new(100+properties++,true,true,0,true);
                else if(name!=null){var e=input.GetProperty("entities").GetProperty(name);owner=new(name[0],e.GetProperty("owned").GetBoolean(),e.GetProperty("reclaimable").GetBoolean(),e.GetProperty("value").GetInt64());}
                var status=engine.Visit(cell.GetProperty("land").GetInt64(),owner,id=>new(input.GetProperty("entities").GetProperty(((char)id).ToString()).GetProperty("removeFees").EnumerateArray().Select(v=>new AccountCharge(AccountGenre.Subsidiaries,v.GetInt64()))));
                result.Add(status=="buyable"?"property":status=="sellable"?null:name);
            }
            Assert.Equal(expected.GetProperty("entities").EnumerateArray().Select(v=>v.GetString()),result);
            var receipts=sell?engine.Revenues:engine.Charges.Select(c=>c.Amount);
            Assert.Equal(expected.GetProperty("receipts").EnumerateArray().Select(v=>v[2].GetInt64()),receipts);
            Assert.Equal(expected.GetProperty("authoritativeQuote").GetInt64(),sell?-engine.Amount:engine.Amount);count++;
        }
        Assert.Equal(6,count);
    }
    [Fact]
    public void Source_X_outer_Y_inner_buys_reclaimable_cells_skips_owned_and_quotes_all_receipts_without_mutation()
    {
        var s=Fixture();var bytes=SaveCodec.Write(s);var p=Plan(s,b:new(15,13,2));
        Assert.Equal(bytes,SaveCodec.Write(s));Assert.Equal(new[]{"buyable","owned","owned","buyable"},p.Cells.Select(c=>c.Status));
        var loss=Assert.Single(p.Reclaimed);var expected=loss.Owner.Value+60;Assert.Equal(expected,p.Amount);
        var after=LandTrading.Commit(s,p);Assert.Equal(s.World.Revision+1,after.World.Revision);
        Assert.Equal(new[]{loss.Owner.Value,30L,30L},after.Construction.Accounts.Transactions.Select(t=>t.Expenditure));
        Assert.All(after.Construction.Accounts.Transactions,t=>{Assert.Equal(AccountGenre.Subsidiaries,t.Genre);Assert.Equal(after.World.Revision,t.WorldRevision);});
        Assert.Equal(new[]{new Location(14,13,2),P,new Location(15,13,2)},after.World.City!.State.Properties.Select(o=>o.Location));
        Assert.Single(after.World.City.State.Ground);Assert.Single(after.World.City.State.Producers);Exact(after);
        Assert.Throws<InvalidOperationException>(()=>LandTrading.Commit(after,p));
    }
    [Fact]
    public void Sale_uses_live_value_and_one_revenue_receipt_per_property_in_source_order()
    {
        var s=Trade(Empty(),b:new(15,13,2));Assert.Equal(120,s.Construction.Accounts.Transactions.Sum(t=>t.Expenditure));
        s=With(s,s.World.City!.State with {Heat=s.World.City.State.Heat with {Q=s.World.City.State.Heat.Q.Select(_=>1000f).ToImmutableArray()}});
        var p=Plan(s,"sell",b:new(15,13,2));Assert.Equal(280,p.Amount); // trunc(pow(1000,.3f))*10 = 70 per cell
        var next=LandTrading.Commit(s,p);Assert.Empty(next.World.City!.State.Properties);
        Assert.Equal(new long[]{70,70,70,70},next.Construction.Accounts.Transactions.Skip(4).Select(t=>t.Revenue));
        Assert.Equal(s.Construction.Accounts.Cash+280,next.Construction.Accounts.Cash);Exact(next);
        Assert.Equal(JsonSerializer.Serialize(s.Random.Capture()),JsonSerializer.Serialize(next.Random.Capture()));
        Assert.Equal(JsonSerializer.Serialize(s.Clock.Capture()),JsonSerializer.Serialize(next.Clock.Capture()));
    }
    [Theory]
    [InlineData(0)] [InlineData(4)]
    public void First_corner_ground_later_columns_hold_level_water_is_buyable_and_outside_cells_are_skipped(int water)
    {
        var s=Empty(water);var hv=s.World.Terrain.Space.ToStorage(P with {X=15});
        var cells=s.World.Terrain.Cells.SetItem(hv.V*s.World.Terrain.Space.HorizontalSize+hv.H,new TerrainCell(3,TerrainSurface.Cut));
        var terrain=new TerrainWorld(s.World.Terrain.Profile,s.World.Terrain.Name,new(40,32,16),water,cells,new(1,[]),[]);
        s=With(s,s.World.City!.State,terrain);
        var p=Plan(s,b:new(15,12,2));Assert.Equal(new[]{"buyable","underground"},p.Cells.Select(c=>c.Status));
        Assert.Single(LandTrading.Commit(s,p).World.City!.State.Properties);
        Assert.Throws<ArgumentException>(()=>Plan(s,a:P with {Z=3}));
        Assert.Throws<ArgumentException>(()=>Plan(s,b:new(15,12,3)));
        Assert.Throws<ArgumentException>(()=>Plan(s,b:new(int.MaxValue,12,2)));
        Assert.Equal(30,Plan(s).Amount);Assert.NotEmpty(Plan(s,b:new(0,12,2)).Cells.Where(c=>c.Status=="outside"));
    }
    [Fact]
    public void Multi_cell_building_is_protected_and_never_partially_reclaimed_or_sold()
    {
        var s=Empty();var d=s.World.City!.Profile.Structures.First(d=>d.Kind==CityStructureKind.Fixed&&d.SizeX>1&&d.SizeY>1&&d.Population==null);
        s=With(s,s.World.City.State with {Structures=[new(1,0,d.Reference,P,d.MinimumHeight,true,new(123,0),[],[])]});
        var bytes=SaveCodec.Write(s);var p=Plan(s,b:new(P.X+d.SizeX,P.Y+d.SizeY-1,P.Z));
        Assert.Empty(p.Reclaimed);Assert.Equal(d.SizeX*d.SizeY,p.Cells.Count(c=>c.Status=="owned"));
        var after=LandTrading.Commit(s,p);Assert.Single(after.World.City!.State.Structures);Assert.Equal(d.SizeY,after.World.City.State.Properties.Length);
        Assert.DoesNotContain(Plan(s,"sell",b:new(P.X+d.SizeX,P.Y+d.SizeY-1,P.Z)).Cells,c=>c.Status=="sellable");Assert.Equal(bytes,SaveCodec.Write(s));
    }
    [Fact]
    public void Actual_creek_and_ground_provider_reclamation_remove_fences_and_both_listener_edges_atomically()
    {
        var s=Fixture();s=StationBuildingConstruction.Commit(s,StationBuildingConstruction.Preview(s.World,new(12,12,2),StationStateTests.Definition.Reference));
        var attached=StationCatchment.Attach(s.World,1,s.World.City!.Provider(1,30));
        s=new(new(attached,s.Construction.Accounts),s.Provenance,clock:s.Clock.Capture(),random:s.Random.Capture(),revision:attached.Revision);
        Assert.Contains(s.World.Stations!.Listeners,l=>l.ProviderId==1);
        var fence=s.World.City!.Profile.Ground.First(d=>d.Handler=="fence"&&!s.World.City.Profile.Bindings.Single(b=>b.Reference==d.Reference).Implementation.EndsWith("FenceRemover",StringComparison.Ordinal));
        s=With(s,s.World.City.State with {Fences=[new(1,P,0,fence.Reference)]});
        var next=Trade(s);Assert.DoesNotContain(next.World.Stations!.Listeners,l=>l.ProviderId==1);
        Assert.All(next.World.Stations.ListenerMemberships,m=>Assert.DoesNotContain(1L,m.Providers));Assert.Empty(next.World.City!.State.Fences);Exact(next);
        s=Empty();var draft=AuthoringTerrain.Plan(s.World.Terrain,P,P with {X=15},"placeCreek",null,s.Random,s.World.Spatial.NextEntityId,s.World.Spatial);
        var t=draft.Draft.Finish();var w=new MixedWorld(s.World.Profile,t,t.Spatial.Capture(),[],[],t.Revision,city:s.World.City);
        s=new(new(w),s.Provenance,clock:s.Clock.Capture(),random:s.Random.Capture(),revision:w.Revision);
        var p=Plan(s);Assert.Single(p.Reclaimed);next=LandTrading.Commit(s,p);Assert.Null(next.World.Terrain.OccupantAt(P));Assert.NotNull(next.World.Terrain.OccupantAt(P with {X=15}));Exact(next);
    }
    static async Task<byte[]> Save(SessionHost h){var t=h.CaptureSave();h.Pump(0);return await t;}
    static async Task<WireMessage> Send(SessionHost h,CommandPayload payload,string? id=null,ParticipantContext? who=null)
    {var t=h.Enqueue(who??new("owner"),new(id??Guid.NewGuid().ToString(),h.WorldEpoch,payload));h.Pump(0);return await t;}
    static LandQuery Query(SessionHost h,string action="buy",Location? p=null)
    {var s=h.Snapshot();var a=p??P;return new(s.WorldEpoch,s.Mixed!.GeometryRevision,s.Tick,s.ContentVersion,action,new(a.X,a.Y,a.Z),new(a.X,a.Y,a.Z));}
    [Fact]
    public async Task Land_authority_issued_quotes_receipt_retry_noop_inspection_and_exact_restart()
    {
        using var h=new SessionHost(Empty(),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var original=await Save(h);var q=Query(h);var p=h.PreviewLand(q);Assert.True(p.Legal);Assert.Equal(30,p.Amount);Assert.Equal(original,await Save(h));
        var observer=new ParticipantContext("viewer",ParticipantRole.Observer);h.RegisterParticipant(observer);
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await Send(h,new TradeLandCommand(q,p.Amount),who:observer)).Code);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,new TradeLandCommand(q,31))).Code);Assert.Equal(original,await Save(h));
        var network=new NetworkSession(new(),hostFactory:(_,_)=>h);var now=DateTimeOffset.UtcNow;var connection=network.Connect(now,new ParticipantContext("owner"))!;
        var command=new CommandMessage("land-retry",h.WorldEpoch,new TradeLandCommand(q,p.Amount));
        var task=network.Submit(connection,command,now);network.Tick(now);var receipt=await task;Assert.IsType<AcceptedMessage>(receipt);var saved=await Save(h);
        Assert.Equal(receipt,await network.Submit(connection,command,now));Assert.Equal(saved,await Save(h));
        var owned=h.PreviewLand(Query(h));Assert.False(owned.Legal);Assert.Equal("owned",owned.Cells[0].Status);
        var inspect=h.PreviewLand(Query(h,"inspect"));Assert.True(inspect.Legal);Assert.Equal(30,inspect.Cells[0].Value);Assert.Equal(saved,await Save(h));
        Assert.Equal("invalidInput",Assert.IsType<RejectedMessage>(await Send(h,new TradeLandCommand(inspect.Query,0),who:connection.Participant)).Code);
        var reopened=SaveCodec.ReadWorld(saved,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;Assert.Equal(saved,SaveCodec.Write(reopened));
        using var peer=new SessionHost(reopened,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        q=Query(peer,"sell");p=peer.PreviewLand(q);Assert.Equal(30,p.Amount);Assert.IsType<AcceptedMessage>(await Send(peer,new TradeLandCommand(q,p.Amount)));Assert.Empty(peer.Snapshot().Mixed!.City!.Properties);
    }
    [Fact]
    public async Task Relevant_heat_change_requires_renewed_review_while_unrelated_clock_change_keeps_quote()
    {
        var s=Empty();var city=s.World.City!;var hv=s.World.Terrain.Space.ToStorage(P);var heat=city.State.Heat;
        // One nonzero sample crosses a truncation boundary at the next callback: value30 ->20.
        heat=heat with {Q=heat.Q.Select(_=>0f).ToImmutableArray().SetItem(heat.Index(hv.H+1,hv.V+1),100f)};
        var timer=city.State.HeatCallback! with {DueMinute=s.Clock.Time.AbsoluteMinute+2};
        var state=city.State with {Heat=heat,HeatCallback=timer};var graph=new CityGraph(city.Profile,state);
        var w=new MixedWorld(s.World.Profile,s.World.Terrain,s.World.Spatial.Capture(),[],[],s.World.Revision,city:graph);
        s=new(new(w),s.Provenance,clock:s.Clock.Capture() with {Pending=[timer]},random:s.Random.Capture(),revision:s.Revision);
        using var h=new SessionHost(s,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var q=Query(h);var p=h.PreviewLand(q);Assert.Equal(30,p.Amount);
        Assert.IsType<AcceptedMessage>(await Send(h,new StepCommand()));Assert.IsType<AcceptedMessage>(await Send(h,new TradeLandCommand(q,p.Amount)));
        q=Query(h,"sell");p=h.PreviewLand(q);Assert.Equal(30,p.Amount);
        if(Environment.GetEnvironmentVariable("FREETRAIN_LAND_OUTPUT") is {} changing) File.WriteAllBytes(changing+".changing",await Save(h));
        Assert.IsType<AcceptedMessage>(await Send(h,new StepCommand()));var saved=await Save(h);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,new TradeLandCommand(q,p.Amount))).Code);Assert.Equal(saved,await Save(h));
        p=h.PreviewLand(Query(h,"sell"));Assert.Equal(20,p.Amount);Assert.IsType<AcceptedMessage>(await Send(h,new TradeLandCommand(p.Query,p.Amount)));
    }
    [Fact]
    public void Late_overflow_rejects_entire_rectangle_and_preserves_random_queue_cash_and_owner_graph()
    {
        var s=Empty();s=With(s,s.World.City!.State with {Heat=s.World.City.State.Heat with {Q=s.World.City.State.Heat.Q.Select(_=>float.MaxValue).ToImmutableArray()}});
        var bytes=SaveCodec.Write(s);Assert.Throws<OverflowException>(()=>Plan(s,b:new(15,13,2)));Assert.Equal(bytes,SaveCodec.Write(s));
        var t=Empty();var world=new MixedWorld(t.World.Profile,t.World.Terrain,new(long.MaxValue,[]),[],[],t.World.Revision,city:t.World.City);
        t=new(new(world),t.Provenance,clock:t.Clock.Capture(),random:t.Random.Capture(),revision:t.Revision);bytes=SaveCodec.Write(t);
        Assert.Throws<OverflowException>(()=>Plan(t,b:new(15,13,2)));Assert.Equal(bytes,SaveCodec.Write(t));
    }
    [Fact]
    public async Task First_activation_inspection_and_cancelled_preview_do_not_change_railway_only_save()
    {
        using var h=new SessionHost(StationBuildingConstructionTests.Empty(),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var original=await Save(h);Assert.Null(h.Snapshot().Mixed!.City);
        var inspect=h.PreviewLand(Query(h,"inspect"));Assert.True(inspect.Legal);Assert.Equal(0,inspect.Cells[0].Value);
        var p=h.PreviewLand(Query(h));Assert.True(p.Legal);Assert.Equal(0,p.Amount);Assert.Equal(original,await Save(h));
        Assert.IsType<AcceptedMessage>(await Send(h,new TradeLandCommand(p.Query,p.Amount)));
        Assert.Single(h.Snapshot().Mixed!.City!.Properties);Assert.Single(h.Snapshot().Mixed!.Queue!.Pending,a=>a.HandlerId==CityHandlers.Heat);
    }
    [Fact]
    public void Second_cell_value_and_second_sale_receipt_overflow_abandon_every_staged_change()
    {
        var s=Empty();var heat=s.World.City!.State.Heat;var hv=s.World.Terrain.Space.ToStorage(P with {X=15});
        s=With(s,s.World.City.State with {Heat=heat with {Q=heat.Q.SetItem(heat.Index(hv.H+1,hv.V+1),float.MaxValue)}});
        var original=SaveCodec.Write(s);Assert.Throws<OverflowException>(()=>Plan(s,b:P with {X=15}));Assert.Equal(original,SaveCodec.Write(s));
        s=Trade(Empty(),b:P with {X=15});
        s=new(new(s.World,new ConstructionAccounts(long.MaxValue-30)),s.Provenance,clock:s.Clock.Capture(),random:s.Random.Capture(),revision:s.Revision);
        original=SaveCodec.Write(s);Assert.Throws<OverflowException>(()=>Plan(s,"sell",b:P with {X=15}));Assert.Equal(original,SaveCodec.Write(s));
    }
    [Fact]
    public void Land_browser_fixture_and_transport_use_actual_canonical_trade_successor()
    {
        var s=Fixture();var before=MixedWire.Snapshot(s,1);var after=Trade(s,b:new(15,13,2));Exact(after);
        if(Environment.GetEnvironmentVariable("FREETRAIN_LAND_OUTPUT") is {} path)
        {
            var options=WireJson.CreateOptions();File.WriteAllBytes(path,SaveCodec.Write(s));
            File.WriteAllText(path+".before.json",JsonSerializer.Serialize<WireMessage>(before,options));
            File.WriteAllText(path+".snapshot.json",JsonSerializer.Serialize<WireMessage>(MixedWire.Snapshot(after,1),options));
            File.WriteAllText(path+".delta.json",JsonSerializer.Serialize<WireMessage>(StateDifference.Between(before,MixedWire.Snapshot(after,1)),options));
        }
    }
}
