using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Server;
using FreeTrain.Online.Simulation;
using Xunit;
namespace FreeTrain.Online.Tests;

public class RiceFieldTests
{
    static readonly Location P=new(14,12,2);
    static readonly CityGroundDefinition[] Definitions=RailwayProfiles.Bundled.City!.Ground.Where(d=>d.Handler=="rice").ToArray();
    static CityCandidate Capture(MixedSessionState s)=>new(s.Construction,s.Clock.Capture(),s.Random.Capture());
    static LandTradePlan Plan(MixedSessionState s,Location? a=null,Location? b=null,int style=0,string action="cover")=>
        StaticGroundConstruction.Preview(Capture(s),a??P,b??P,action,action=="cover"?Definitions[style].Reference:null);
    static MixedSessionState Apply(MixedSessionState s,LandTradePlan p){var n=LandTrading.Commit(s,p);MixedWire.ValidateTransition(s,n);return n;}
    static MixedSessionState Reopen(MixedSessionState s){var bytes=SaveCodec.Write(s);var n=SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;Assert.Equal(bytes,SaveCodec.Write(n));return n;}
    static MixedSessionState Seed(){var s=LandTradingTests.Empty();return StationBuildingConstruction.Commit(s,StationBuildingConstruction.Preview(s.World,new(12,12,2),StationStateTests.Definition.Reference));}

    [Fact]
    public void Both_styles_have_nine_literal_rectangle_tiles_ordered_double_booking_and_real_providers()
    {
        Assert.Equal(2,Definitions.Length);
        for(int style=0;style<2;style++)
        {
            var d=Definitions[style];Assert.Equal(1500000,d.Price);Assert.NotNull(d.Population);
            var s=Seed();var bytes=SaveCodec.Write(s);var p=Plan(s,b:new(16,14,2),style:style);
            Assert.Equal(bytes,SaveCodec.Write(s));Assert.Equal(27000000,p.Amount);
            var n=Apply(s,p);var ground=n.World.City!.State.Ground;Assert.Equal(Enumerable.Range(0,9),ground.Select(g=>g.Variant));
            Assert.All(ground,g=>{Assert.True(g.Owned);Assert.Equal(d.Reference,g.Definition);Assert.Equal(1500000,n.World.Spatial.At(g.Location).Entity!.Value);});
            Assert.Equal(9,n.World.City.State.Producers.Length);Assert.Equal(9,n.World.Stations!.Listeners.Length);
            Assert.Equal(Enumerable.Repeat(1500000L,9).Append(13500000),n.Construction.Accounts.Transactions.Skip(s.Construction.Accounts.Transactions.Length).Select(t=>t.Expenditure));
            Assert.All(n.Construction.Accounts.Transactions.Skip(s.Construction.Accounts.Transactions.Length),t=>Assert.Equal(AccountGenre.Subsidiaries,t.Genre));
            Assert.Equal(JsonSerializer.Serialize(s.Random.Capture()),JsonSerializer.Serialize(n.Random.Capture()));
            var reopened=Reopen(n);Assert.Equal(SaveCodec.Write(Apply(n,Plan(n,b:new(16,14,2),style:1-style))),SaveCodec.Write(Apply(reopened,Plan(reopened,b:new(16,14,2),style:1-style))));
        }
    }
    [Theory]
    [InlineData(14,12,14,12,new[]{0})]
    [InlineData(14,12,14,14,new[]{0,1,2})]
    [InlineData(14,12,16,12,new[]{0,3,6})]
    [InlineData(15,13,14,12,new[]{0,2,6,8})]
    public void Singleton_rows_columns_and_reversed_corners_keep_source_minimum_precedence(int ax,int ay,int bx,int by,int[] variants)
    {
        var s=Seed();Assert.Equal(variants,Apply(s,Plan(s,new(ax,ay,2),new(bx,by,2))).World.City!.State.Ground.Select(g=>g.Variant));
    }
    [Fact]
    public void Blocked_cells_keep_original_rectangle_edges_and_only_successful_inner_charges()
    {
        var s=Seed();var p=Plan(s,new(11,12,2),new(13,12,2));Assert.Equal(new[]{"placeable","protected","placeable"},p.Cells.Select(c=>c.Status));
        Assert.Equal(7500000,p.Amount);var n=Apply(s,p);Assert.Equal(new[]{0,6},n.World.City!.State.Ground.Select(g=>g.Variant));
        Assert.Equal(new long[]{1500000,1500000,4500000},n.Construction.Accounts.Transactions.Skip(s.Construction.Accounts.Transactions.Length).Select(t=>t.Expenditure));
        var all=Apply(s,Plan(s,new(12,12,2),new(12,12,2)));Assert.Empty(all.World.City!.State.Ground);Assert.Equal(1500000,Assert.Single(all.Construction.Accounts.Transactions.Skip(s.Construction.Accounts.Transactions.Length)).Expenditure);Reopen(all);
        var terrain=s.World.Terrain;var hv=terrain.Space.ToStorage(new(15,12,2));
        terrain=new(terrain.Profile,terrain.Name,new(40,32,16),0,terrain.Cells.SetItem(hv.V*40+hv.H,new TerrainCell(3,TerrainSurface.Cut)),new(terrain.Spatial.NextEntityId,terrain.Spatial.Entities),terrain.Occupants);
        s=LandTradingTests.With(s,s.World.City!.State,terrain);p=Plan(s,b:new(16,12,2));Assert.Equal(new[]{"placeable","uneven","placeable"},p.Cells.Select(c=>c.Status));Assert.Equal(7500000,p.Amount);
        Assert.Contains(Plan(s,b:new(0,12,2)).Cells,c=>c.Status=="outside");Assert.Throws<ArgumentException>(()=>Plan(s,b:new(15,12,3)));Assert.Throws<ArgumentException>(()=>Plan(s,b:new(100,100,2)));
        var water=LandTradingTests.Empty(4);Reopen(Apply(water,Plan(water)));
    }
    [Fact]
    public void Replacement_and_both_removal_paths_detach_real_station_memberships_without_extra_fee()
    {
        var s=Seed();s=LandTrading.Commit(s,LandTrading.Preview(Capture(s),P,P,"buy"));
        s=Apply(s,Plan(s,b:new(15,13,2)));Assert.Empty(s.World.City!.State.Properties);var old=s.World.City.State.Ground.Select(g=>g.Id).ToArray();
        s=Apply(s,Plan(s,b:new(15,13,2),style:1));Assert.DoesNotContain(s.World.Stations!.Listeners,l=>old.Contains(l.ProviderId));Assert.Equal(4,s.World.Stations.ListenerMemberships[0].Providers.Length);
        var accounts=s.Construction.Accounts;var (world,kind,reason)=ObjectRemoval.Mixed(s.World,P);Assert.Null(reason);Assert.Equal("groundCover",kind);
        s=s.Successor(s.Revision,new(world!,accounts),s.ConstructionMode);Assert.Equal(3,s.World.Stations!.Listeners.Length);
        s=Apply(s,Plan(s,b:new(15,13,2),action:"removeCover"));Assert.Empty(s.World.City!.State.Ground);Assert.Empty(s.World.City.State.Producers);Assert.Empty(s.World.Stations!.Listeners);Assert.Empty(s.World.Stations.ListenerMemberships[0].Providers);Assert.Equal(accounts,s.Construction.Accounts);Reopen(s);
    }
    [Theory]
    [InlineData(1439)] // midnight
    [InlineData(84959)] // non-leap February 28 -> March 1
    [InlineData(217439)]
    [InlineData(349919)]
    [InlineData(480959)]
    [InlineData(525599)] // year boundary
    public void Calendar_boundaries_preserve_tiles_random_and_accounts_and_exact_uninterrupted_continuation(long minute)
    {
        var empty=LandTradingTests.Empty();var s=CityHeat.Enable(new MixedSessionState(new(MixedWorld.FromTerrain(RailwayProfiles.Bundled,empty.World.Terrain)),empty.Provenance,clock:new(minute,0,[])));
        s=Apply(s,Plan(s,b:new(16,14,2)));var reopened=Reopen(s);var a=Capture(s);var b=Capture(reopened);
        for(int i=0;i<120;i++){a=CityConstruction.AdvanceOneMinute(a);b=CityConstruction.AdvanceOneMinute(b);}
        Assert.Equal(SaveCodec.Write(new MixedSessionState(a.Construction,s.Provenance,s.ConstructionMode,a.Clock,s.Paused,a.Random,s.Revision+120)),SaveCodec.Write(new MixedSessionState(b.Construction,reopened.Provenance,reopened.ConstructionMode,b.Clock,reopened.Paused,b.Random,reopened.Revision+120)));Assert.Equal(s.World.City!.State.Ground,a.Construction.World.City!.State.Ground);
        Assert.Equal(s.Construction.Accounts,a.Construction.Accounts);Assert.Equal(JsonSerializer.Serialize(s.Random.Capture()),JsonSerializer.Serialize(a.Random));Assert.DoesNotContain(a.Clock.Pending,p=>p.HandlerId.Contains("rice",StringComparison.OrdinalIgnoreCase));
        Assert.All(a.Construction.World.Stations!.Listeners,l=>{Assert.Equal(5,l.Provider!.Population.Residents);Assert.Equal(0,l.Provider.Population.Incoming(new(minute+120)));Assert.Equal(0,l.Provider.Population.Outgoing(new(minute+120)));});
    }
    static async Task<byte[]> Save(SessionHost h){var t=h.CaptureSave();h.Pump(0);return await t;}
    static async Task<WireMessage> Send(SessionHost h,CommandPayload payload,string? id=null,ParticipantContext? who=null){var t=h.Enqueue(who??new("owner"),new(id??Guid.NewGuid().ToString(),h.WorldEpoch,payload));h.Pump(0);return await t;}
    static LandQuery Query(SessionHost h){var s=h.Snapshot();var d=Definitions[0].Reference;return new(s.WorldEpoch,s.Mixed!.GeometryRevision,s.Tick,s.ContentVersion,"cover",new(14,12,2),new(15,13,2),new(d.PackId,d.ContributionId,d.RecipeId));}
    [Fact]
    public async Task Review_authority_wrong_cost_stale_replay_and_first_activation_are_atomic()
    {
        using var h=new SessionHost(StationBuildingConstructionTests.Empty(),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);var q=Query(h);var bytes=await Save(h);var p=h.PreviewLand(q);Assert.True(p.Legal);Assert.Equal(12000000,p.Amount);Assert.Equal(bytes,await Save(h));Assert.Null(h.Snapshot().Mixed!.City);
        var observer=new ParticipantContext("viewer",ParticipantRole.Observer);h.RegisterParticipant(observer);
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await Send(h,new TradeLandCommand(q,p.Amount),who:observer)).Code);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,new TradeLandCommand(q,p.Amount-1))).Code);Assert.Equal(bytes,await Save(h));
        var network=new NetworkSession(new(),hostFactory:(_,_)=>h);var now=DateTimeOffset.UtcNow;var c=network.Connect(now,new ParticipantContext("owner"))!;var command=new CommandMessage("rice-replay",h.WorldEpoch,new TradeLandCommand(q,p.Amount));var t=network.Submit(c,command,now);network.Tick(now);var receipt=await t;Assert.IsType<AcceptedMessage>(receipt);bytes=await Save(h);
        Assert.Equal(receipt,await network.Submit(c,command,now));Assert.Equal(bytes,await Save(h));Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,new TradeLandCommand(q,p.Amount),who:c.Participant)).Code);Assert.Equal(bytes,await Save(h));Reopen(SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!);
    }
    [Fact]
    public void Late_allocator_failure_abandons_all_tiles_listeners_and_receipts()
    {
        var s=Seed();var w=new MixedWorld(s.World.Profile,s.World.Terrain,new(long.MaxValue-1,s.World.Spatial.Entities),s.World.Voxels,s.World.Slopes,s.World.Revision,stations:s.World.Stations,city:s.World.City);
        s=new(new(w),s.Provenance,clock:s.Clock.Capture(),random:s.Random.Capture(),revision:s.Revision);var bytes=SaveCodec.Write(s);Assert.Throws<OverflowException>(()=>Plan(s,b:new(15,12,2)));Assert.Equal(bytes,SaveCodec.Write(s));
    }
    [Fact]
    public void Later_source_receipt_overflow_abandons_prior_staged_rice_cells_and_charges()
    {
        var seed=Seed();var s=new MixedSessionState(new(seed.World,new ConstructionAccounts(long.MinValue+3000000,new(1))),seed.Provenance,clock:seed.Clock.Capture(),random:seed.Random.Capture(),revision:seed.Revision);
        var bytes=SaveCodec.Write(s);Assert.Throws<OverflowException>(()=>Plan(s,b:new(15,13,2)));Assert.Equal(bytes,SaveCodec.Write(s));
    }
    [Fact]
    public void Rice_replaces_static_farms_but_preserves_forest_mountains_and_real_rail_neighbors()
    {
        var s=Seed();var farm=s.World.City!.Profile.Ground.First(d=>d.Handler=="land-0d");
        s=Apply(s,StaticGroundConstruction.Preview(Capture(s),P,P,"cover",farm.Reference));var old=s.World.City!.State.Ground[0].Id;
        s=Apply(s,Plan(s));Assert.DoesNotContain(s.World.Stations!.Listeners,l=>l.ProviderId==old);Assert.Single(s.World.Stations.Listeners);
        var rail=NormalRailwayConstruction.PreviewBuild(s.World,new(16,12,2),new(16,14,2));Assert.True(rail.Legal);s=s.Successor(s.Revision,NormalRailwayConstruction.Apply(s.Construction,rail,s.Clock.Time),s.ConstructionMode);
        var voxels=s.World.Voxels;var plan=Plan(s,b:new(16,12,2));Assert.Equal("protected",plan.Cells.Last().Status);s=Apply(s,plan);Assert.Equal(voxels.ToArray(),s.World.Voxels.ToArray());
        var tree=TerrainProfiles.Bundled.Definitions.First(d=>d.TreeDensity>0).Reference;var at=new Location(20,20,2);
        s=Apply(s,ForestConstruction.Preview(Capture(s),at,at,tree));var occupants=s.World.Terrain.Occupants;Assert.NotEmpty(occupants);
        plan=Plan(s,a:at,b:at);Assert.Equal("protected",plan.Cells[0].Status);s=Apply(s,plan);Assert.Equal(occupants,s.World.Terrain.Occupants);Reopen(s);
    }
    [Fact]
    public void Ricefield_browser_seed_and_real_successor_fixtures()
    {
        var s=Seed();var before=MixedWire.Snapshot(s,1);var after=Apply(s,Plan(s,b:new(15,13,2)));var removed=Apply(after,Plan(after,b:new(15,13,2),action:"removeCover"));Reopen(after);Reopen(removed);
        if(Environment.GetEnvironmentVariable("FREETRAIN_RICEFIELD_OUTPUT") is not {} path)return;
        File.WriteAllBytes(path,SaveCodec.Write(s));var options=WireJson.CreateOptions();void Write(string suffix,WireMessage v)=>File.WriteAllText(path+suffix,JsonSerializer.Serialize(v,options));
        Write(".before.json",before);Write(".snapshot.json",MixedWire.Snapshot(after,1));Write(".delta.json",StateDifference.Between(before,MixedWire.Snapshot(after,1)));Write(".removed.json",MixedWire.Snapshot(removed,1));Write(".removed.delta.json",StateDifference.Between(MixedWire.Snapshot(after,1),MixedWire.Snapshot(removed,1)));
    }
}
