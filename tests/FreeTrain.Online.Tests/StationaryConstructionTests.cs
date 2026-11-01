using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public class StationaryConstructionTests
{
    static Location P => GarageConstructionTests.Start;
    internal static MixedSessionState Empty(long minute=3420)
    {
        var w=GarageConstructionTests.World();
        var r=new EmptyWorldRequest(WorldCreationProfile.Id,w.Terrain.Name,new(40,32,8),0);
        return new(new(w),TerrainProvenance.From(r),clock:new(minute,0,[]),revision:w.Revision);
    }
    static MixedSessionState Start(MixedSessionState s,string type) => StationaryConstruction.Commit(s,StationaryConstruction.Preview(s.World,P,type));
    static MixedSessionState Open(byte[] bytes) => SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
    static string[] Types => RailwayProfiles.Bundled.Bindings.Where(b=>b.Handler==RailwayHandler.Stationary).Select(b=>b.Reference.ContributionId).ToArray();
    [Fact]
    public void Every_source_contribution_builds_through_a_site_then_books_completion_and_subsidiaries_demolition()
    {
        using var ledger=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spatial/railway-accessories.json")));
        var contributions=ledger.RootElement.GetProperty("contributions").EnumerateArray().Where(c=>c.GetProperty("type").GetString()=="railStationary").ToDictionary(c=>c.GetProperty("id").GetString()!);
        Assert.Equal(54,Types.Length);Assert.Equal(Types.Order(),contributions.Keys.Order());
        foreach(var id in Types)
        {
            var s=Empty(); var bytes=SaveCodec.Write(s);
            var quote=StationaryConstruction.Preview(s.World,P,id);
            Assert.True(quote.Legal,quote.Reason); Assert.Equal(bytes,SaveCodec.Write(s)); Assert.Empty(quote.Booking.Charges);
            var type=s.World.Profile.Bindings.Single(b=>b.Reference.ContributionId==id).Stationary!;
            var source=contributions[id];
            Assert.Equal(source.GetProperty("footprint")[0].GetInt32(),type.SizeX);Assert.Equal(source.GetProperty("footprint")[1].GetInt32(),type.SizeY);Assert.Equal(source.GetProperty("footprint")[2].GetInt32(),type.Height);
            Assert.Equal(source.GetProperty("completionPrice").GetInt64(),type.Price);
            s=Start(s,id);
            Assert.Equal(type.Height,s.World.Voxels.Length); Assert.Empty(s.Construction.Accounts.Transactions);
            Assert.All(s.World.Spatial.Entities,e=>Assert.False(e.Owned));
            Assert.All(s.World.Spatial.Entities.SelectMany(e=>e.Footprint.Members),m=>Assert.False(m.Transparent));
            Assert.Equal(type.Height,s.Clock.Capture().Pending.Length);
            Assert.All(s.Clock.Capture().Pending,a=>Assert.InRange(a.DueMinute-s.Clock.Time.AbsoluteMinute,40,119));
            Assert.All(s.World.Voxels,v=>Assert.False(v.Construction!.NeedsDoor)); // All bundled footprints are 1x1.
            var siteOwner=s.World.Voxels[0].OwnerId;
            var max=0;while(s.World.Voxels[0].Construction!=null&&max++<2000)s.AdvanceOneMinute();
            Assert.True(max<2000,id); Assert.Empty(s.Clock.Capture().Pending);
            Assert.NotEqual(siteOwner,s.World.Voxels[0].OwnerId);
            Assert.All(s.World.Spatial.Entities,e=>{Assert.True(e.Owned);Assert.False(e.SilentlyReclaimable);Assert.Equal(0,e.Value);});
            Assert.All(s.World.Spatial.Entities.SelectMany(e=>e.Footprint.Members),m=>Assert.True(m.Transparent));
            var charge=Assert.Single(s.Construction.Accounts.Transactions);Assert.Equal(type.Price,charge.Expenditure);Assert.Equal(AccountGenre.RailService,charge.Genre);
            Assert.Equal("stationaryCompletion",charge.Operation);
            var saved=SaveCodec.Write(s);Assert.Equal(saved,SaveCodec.Write(Open(saved)));
            var remove=StationaryConstruction.Preview(s.World,P with{Z=P.Z+type.Height-1},"different selected type",true);
            Assert.True(remove.Legal); Assert.Equal(500_000,remove.Booking.Total);
            s=StationaryConstruction.Commit(s,remove);
            Assert.Empty(s.World.Voxels);Assert.Empty(s.World.Spatial.Entities);Assert.Empty(s.Clock.Capture().Pending);
            Assert.Equal(AccountGenre.Subsidiaries,s.Construction.Accounts.Transactions[^1].Genre);
            Assert.Equal(500_000,s.Construction.Accounts.Transactions[^1].Expenditure);
        }
    }
    [Fact]
    public void In_progress_save_resumes_identical_callbacks_progress_random_and_accounting()
    {
        var id=RailwayProfiles.Bundled.Bindings.First(b=>b.Stationary?.Height==2).Reference.ContributionId;
        var s=Start(Empty(),id);
        for(var i=0;i<175;i++)s.AdvanceOneMinute();
        Assert.Contains(s.World.Voxels,v=>v.Construction?.State>0);
        var bytes=SaveCodec.Write(s);var loaded=Open(bytes);Assert.Equal(bytes,SaveCodec.Write(loaded));
        for(var i=0;i<900;i++)
        {
            s.AdvanceOneMinute();loaded.AdvanceOneMinute();
            if(i%80==0)Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(loaded));
        }
        Assert.All(s.World.Voxels,v=>Assert.Null(v.Construction));Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(loaded));
    }
    [Fact]
    public void Cancellation_and_bulldozer_cancel_all_callbacks_without_completion_spend_or_extra_draws()
    {
        var s=Start(Empty(),Types[0]);var random=JsonSerializer.Serialize(s.Random.Capture());
        var plan=StationaryConstruction.Preview(s.World,P,Types[^1],true);s=StationaryConstruction.Commit(s,plan);
        Assert.Empty(s.Clock.Capture().Pending);Assert.Empty(s.Construction.Accounts.Transactions);
        for(var i=0;i<500;i++)s.AdvanceOneMinute();
        Assert.Equal(random,JsonSerializer.Serialize(s.Random.Capture()));Assert.Empty(s.World.Voxels);
        s=Start(s,Types[0]);
        var (candidate,kind,reason)=ObjectRemoval.Mixed(s.World,P);Assert.Null(reason);Assert.NotNull(candidate);
        s=s.Successor(s.Revision,new(candidate!,s.Construction.Accounts),s.ConstructionMode);
        Assert.Empty(s.Clock.Capture().Pending);Assert.Empty(s.World.Spatial.Entities);
    }
    [Fact]
    public void Weekends_nights_and_floor_gates_preserve_source_work_hours()
    {
        // Sunday 10:00 and Monday 08:00 never advance a site before 09:00.
        foreach(var minute in new[]{600L,1800L,2520L})
        {
            var s=Start(Empty(minute),Types[0]);var random=JsonSerializer.Serialize(s.Random.Capture());
            for(var i=0;i<120;i++)s.AdvanceOneMinute();
            Assert.All(s.World.Voxels,v=>Assert.Equal(0,v.Construction!.State));
            Assert.NotEqual(random,JsonSerializer.Serialize(s.Random.Capture())); // retry delays still consume the construction stream
        }
        var tall=RailwayProfiles.Bundled.Bindings.First(b=>b.Stationary?.Height==2).Reference.ContributionId;
        var t=Start(Empty(),tall);
        while(t.World.Voxels[0].Construction!=null)
        {
            var before=t.World; t.AdvanceOneMinute();
            var lower=t.World.At(P)?.Construction;var upper=t.World.At(P with{Z=P.Z+1})?.Construction;
            if(lower!=null&&upper!=null){Assert.True(lower.State>=upper.State);if(upper.State>0)Assert.True(lower.State>=2);if(lower.State==4)Assert.True(upper.State>=3);}
        }
    }
    [Fact]
    public void Legality_checks_upper_occupancy_ground_borders_and_all_site_owner_fields()
    {
        var s=Empty();var tall=RailwayProfiles.Bundled.Bindings.First(b=>b.Stationary?.Height==2).Reference.ContributionId;
        Assert.Equal("stationaryGround",StationaryConstruction.Preview(s.World,P with{Z=P.Z+1},tall).Reason);
        var rail=TunnelConstructionTests.Add(s.World,P with{Z=P.Z+1});
        Assert.Equal("obstacle",StationaryConstruction.Preview(rail,P,tall).Reason);
        Assert.Equal("outside",StationaryConstruction.Preview(s.World,new(int.MaxValue,0,0),tall).Reason);
        Assert.False(StationaryConstruction.Preview(s.World,P,"unknown").Legal);
        s=Start(s,tall);var bytes=SaveCodec.Write(s);
        Assert.Equal("obstacle",StationaryConstruction.Preview(s.World,P,Types[0]).Reason);
        var node=System.Text.Json.Nodes.JsonNode.Parse(bytes)!;
        node["clock"]!["pending"]!.AsArray().RemoveAt(0);
        Assert.Throws<InvalidDataException>(()=>Open(System.Text.Encoding.UTF8.GetBytes(node.ToJsonString())));
        node=System.Text.Json.Nodes.JsonNode.Parse(bytes)!;node["mixed"]!["voxels"]![0]!["construction"]!["state"]=5;
        Assert.Throws<InvalidDataException>(()=>Open(System.Text.Encoding.UTF8.GetBytes(node.ToJsonString())));
        Assert.Equal(bytes,SaveCodec.Write(s));
    }
    [Fact]
    public void Worlds_and_previews_do_not_share_construction_random_or_callbacks()
    {
        var a=Empty();var b=Empty();var bytes=SaveCodec.Write(b);
        a=Start(a,Types[0]);for(var i=0;i<200;i++)a.AdvanceOneMinute();
        Assert.Equal(bytes,SaveCodec.Write(b));
        var quote=StationaryConstruction.Preview(b.World,P,Types[0]);Assert.True(quote.Legal);
        Assert.Equal(bytes,SaveCodec.Write(b));
    }
    [Fact]
    public void Reclaimable_creek_loss_is_explicit_and_cancel_does_not_restore_or_charge_it()
    {
        var original=Empty();var terrain=original.World.Terrain;
        var definition=terrain.Profile.Definitions.First(b=>b.Kind==TerrainOccupantKind.Creek).Reference;
        var id=terrain.Spatial.NextEntityId;
        var occupied=new TerrainWorld(terrain.Profile,terrain.Name,new(40,32,8),terrain.WaterLevel,terrain.Cells,
            new(id+1,[new(id,0,definition,P,new([new(new(0,0,0),true)]),false,true,0)]),[new(id,TerrainOccupantKind.Creek,new(0,0,0,0))]);
        var source=new MixedSessionState(new(MixedWorld.FromTerrain(original.World.Profile,occupied)),original.Provenance,clock:original.Clock.Capture());
        var before=SaveCodec.Write(source);var plan=StationaryConstruction.Preview(source.World,P,Types[0]);
        Assert.True(plan.Legal,plan.Reason);Assert.Equal(id,Assert.Single(plan.Reclaimed).Entity.Id);Assert.Empty(plan.Booking.Charges);
        Assert.Equal(before,SaveCodec.Write(source));var after=StationaryConstruction.Commit(source,plan);
        Assert.Empty(after.World.Terrain.Occupants);Assert.Equal(id+1,after.World.At(P)!.OwnerId);
        after=StationaryConstruction.Commit(after,StationaryConstruction.Preview(after.World,P,Types[0],true));
        Assert.Empty(after.Construction.Accounts.Transactions);Assert.Empty(after.World.Spatial.Entities);
        Assert.Empty(after.Clock.Capture().Pending);Assert.Empty(after.World.Terrain.Occupants);
    }
    [Fact]
    public void Pending_completion_reserves_journal_capacity_against_other_construction()
    {
        var source=Empty();var w=source.World.Replace(0,source.World.Terrain,source.World.Spatial.Capture(),[],[]);
        var accounts=new ConstructionAccounts().Book(new(Enumerable.Repeat(new AccountCharge(AccountGenre.RailService,0),8191)),1,source.Clock.Time,"full");
        var s=new MixedSessionState(new(w,accounts),source.Provenance,clock:source.Clock.Capture(),revision:1);
        s=Start(s,Types[0]);Assert.Equal(8191,s.Construction.Accounts.Transactions.Length);
        using var h=new SessionHost(s,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);var state=h.Snapshot();
        var q=new RailwayQuery(state.WorldEpoch,state.Mixed!.GeometryRevision,state.ContentVersion,new(16,10,2),new(17,10,2),"lay","source");
        Assert.Equal("accountCapacityLimit",h.PreviewRailway(q).Reason);
        var bytes=SaveCodec.Write(s);var node=System.Text.Json.Nodes.JsonNode.Parse(bytes)!;
        node["clock"]!["pending"]![0]!["dueMinute"]=(s.Clock.Time.AbsoluteMinute+120).ToString();
        Assert.Throws<InvalidDataException>(()=>Open(System.Text.Encoding.UTF8.GetBytes(node.ToJsonString())));
        for(var i=0;i<1000&&s.World.At(P)!.Construction!=null;i++)s.AdvanceOneMinute();
        Assert.Equal(8192,s.Construction.Accounts.Transactions.Length);Assert.Null(s.World.At(P)!.Construction);
    }

}
