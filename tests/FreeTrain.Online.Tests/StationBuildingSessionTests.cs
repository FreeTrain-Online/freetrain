using System.Collections.Immutable;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;
using static FreeTrain.Online.Tests.StationStateTests;
using static FreeTrain.Online.Tests.StationBuildingConstructionTests;

namespace FreeTrain.Online.Tests;

public class StationBuildingSessionTests
{
    static SessionHost Host(MixedSessionState? s=null) => new(s ?? Empty(),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
    static async Task<byte[]> Save(SessionHost h){var task=h.CaptureSave();h.Pump(0);return await task;}
    static async Task<WireMessage> Send(SessionHost h,CommandPayload payload,ParticipantContext? who=null,long? epoch=null)
    {var task=h.Enqueue(who ?? new("owner"),new(Guid.NewGuid().ToString(),epoch ?? h.WorldEpoch,payload));h.Pump(0);return await task;}
    static StationBuildingQuery Query(SessionHost h, bool removal=false)
    {
        var s=h.Snapshot();var b=s.Mixed?.StationGraph?.Buildings.FirstOrDefault();
        return new(s.WorldEpoch,s.Mixed?.GeometryRevision ?? s.Terrain!.GeometryRevision,s.Tick,s.ContentVersion,
            removal ? "remove" : "place",new(Base.X,Base.Y,Base.Z),removal ? null : new(Definition.Reference.PackId,Definition.Reference.ContributionId,Definition.Reference.RecipeId),
            removal ? b!.Id : 0,removal ? b!.Revision : 0);
    }
    [Fact]
    public async Task Building_names_are_revisioned_with_spatial_owner_and_preserve_timers_accounts_and_exact_reopen()
    {
        using var h=Host(Build(Empty()));
        var before=h.Snapshot();var b=before.Mixed!.StationGraph!.Buildings[0];
        var op=new ParticipantContext("operator",ParticipantRole.Operator);var observer=new ParticipantContext("observer",ParticipantRole.Observer);
        h.RegisterParticipant(op);h.RegisterParticipant(observer);
        var command=new RenameStationCommand(b.Id.ToString(),b.Revision," 新中央駅 / Central ");
        var bytes=await Save(h);
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await Send(h,command,observer)).Code);
        foreach(var name in new[]{" ","bad\0name",new string('a',129)})
            Assert.Equal("invalidInput",Assert.IsType<RejectedMessage>(await Send(h,command with {Name=name},op)).Code);
        Assert.Equal(bytes,await Save(h));
        Assert.IsType<AcceptedMessage>(await Send(h,command,op));
        var after=h.Snapshot();var renamed=after.Mixed!.StationGraph!.Buildings[0];
        Assert.Equal("新中央駅 / Central",renamed.Name);Assert.Equal(b.Revision+1,renamed.Revision);
        Assert.Equal(renamed.Revision,after.Entities!.Single(e=>e.Id==b.Id).Revision);
        Assert.Equal(b.HourTimer,renamed.HourTimer);Assert.Equal(b.DayTimer,renamed.DayTimer);
        Assert.Equal(before.Mixed.Accounts.Transactions,after.Mixed.Accounts.Transactions);Assert.Equal(before.Cash,after.Cash);
        bytes=await Save(h);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,command)).Code);
        Assert.Equal(bytes,await Save(h));
        Assert.Equal(bytes,SaveCodec.Write(SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!));
        var q=Query(h,true);var quote=h.PreviewStationBuilding(q);
        Assert.IsType<AcceptedMessage>(await Send(h,new EditStationBuildingCommand(q,quote.Cost)));
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,command with {ExpectedRevision=renamed.Revision})).Code);
    }
    [Fact]
    public async Task Preview_is_pure_operator_commits_observer_rejects_and_stale_confirmation_preserves_exact_save()
    {
        using var h=Host();var original=await Save(h);var query=Query(h);var preview=h.PreviewStationBuilding(query);
        Assert.True(preview.Legal,preview.Reason);Assert.Equal(Definition.Price,preview.Cost);Assert.Equal(original,await Save(h));
        var observer=new ParticipantContext("observer",ParticipantRole.Observer);var op=new ParticipantContext("operator",ParticipantRole.Operator);
        h.RegisterParticipant(observer);h.RegisterParticipant(op);
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await Send(h,new EditStationBuildingCommand(query,preview.Cost),observer)).Code);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,new EditStationBuildingCommand(query,preview.Cost+1))).Code);
        Assert.Equal(original,await Save(h));
        Assert.IsType<AcceptedMessage>(await Send(h,new EditStationBuildingCommand(query,preview.Cost),op));
        var bytes=await Save(h);Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,new EditStationBuildingCommand(query,preview.Cost))).Code);
        Assert.Equal(bytes,await Save(h));using var reopened=Host(SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!);
        Assert.Equal(bytes,await Save(reopened));Assert.Equal(h.Snapshot().Mixed!.StationGraph!.Buildings[0].Id,reopened.Snapshot().Mixed!.StationGraph!.Buildings[0].Id);
        var removal=Query(h,true);var quote=h.PreviewStationBuilding(removal);Assert.True(quote.Legal);
        Assert.IsType<AcceptedMessage>(await Send(h,new EditStationBuildingCommand(removal,quote.Cost)));
        Assert.Empty(h.Snapshot().Mixed!.StationGraph!.Buildings);Assert.Equal(2,h.Snapshot().Mixed!.Accounts.Transactions.Length);
        var removed=await Save(h);Assert.Equal(removed,SaveCodec.Write(SaveCodec.ReadWorld(removed,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!));
    }
    [Fact]
    public async Task Clock_content_epoch_target_and_occupied_fences_do_not_partially_publish()
    {
        using var h=Host();var query=Query(h);var quote=h.PreviewStationBuilding(query);
        Assert.IsType<AcceptedMessage>(await Send(h,new StepCommand()));var afterStep=await Save(h);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,new EditStationBuildingCommand(query,quote.Cost))).Code);
        Assert.Equal(afterStep,await Save(h));query=Query(h);
        Assert.Equal("staleEpoch",Assert.IsType<RejectedMessage>(await Send(h,new EditStationBuildingCommand(query with {ExpectedWorldEpoch=query.ExpectedWorldEpoch+1},quote.Cost))).Code);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,new EditStationBuildingCommand(query with {ExpectedContentVersion="unavailable"},quote.Cost))).Code);
        Assert.IsType<AcceptedMessage>(await Send(h,new EditStationBuildingCommand(query,quote.Cost)));
        var built=await Save(h);Assert.False(h.PreviewStationBuilding(Query(h)).Legal);
        Assert.Equal("invalidPlacement",Assert.IsType<RejectedMessage>(await Send(h,new EditStationBuildingCommand(Query(h),quote.Cost))).Code);
        var remove=Query(h,true);var cost=h.PreviewStationBuilding(remove).Cost;
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,new EditStationBuildingCommand(remove with {ExpectedStationRevision=1},cost))).Code);
        Assert.Equal("invalidPlacement",Assert.IsType<RejectedMessage>(await Send(h,new EditStationBuildingCommand(remove with {StationId=999},cost))).Code);
        Assert.Equal(built,await Save(h));
    }
    [Fact]
    public async Task Terrain_promotion_is_detached_until_commit_and_uses_exact_station_content()
    {
        var terrain=Terrain();var state=new TerrainSessionState(terrain,TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,terrain.Name,new(64,24,8),0)));
        using var h=new SessionHost(state,"owner",TerrainProfiles.Resolve,railwayResolver:RailwayProfiles.Resolve);
        var before=await Save(h);var q=Query(h);var p=h.PreviewStationBuilding(q);Assert.True(p.Legal,p.Reason);Assert.Equal(before,await Save(h));
        Assert.Null(h.Snapshot().Mixed);Assert.IsType<AcceptedMessage>(await Send(h,new EditStationBuildingCommand(q,p.Cost)));
        Assert.Equal(Definition.Reference.ContributionId,h.Snapshot().Mixed!.StationGraph!.Buildings[0].Definition.ContributionId);
        Assert.Equal(2,h.Snapshot().Mixed!.Queue!.Pending.Length);
    }
    [Fact]
    public async Task Timer_allocator_overflow_rejects_in_preview_and_commit_before_geometry_or_booking_publication()
    {
        using var h=Host(Empty(clock:new(517,long.MaxValue,[])));var before=await Save(h);var q=Query(h);
        Assert.Equal("constructionOverflow",h.PreviewStationBuilding(q).Reason);
        Assert.Equal("invalidPlacement",Assert.IsType<RejectedMessage>(await Send(h,new EditStationBuildingCommand(q,Definition.Price))).Code);
        Assert.Equal(before,await Save(h));
    }
    [Fact]
    public async Task Account_capacity_and_late_cash_overflow_reject_before_any_station_timers_or_geometry_publish()
    {
        var empty=Empty();
        var exhausted=ConstructionAccounts.Restore(new(ConstructionAccounts.DefaultInitialCash,new(),
            Enumerable.Range(1,ProtocolIdentity.MaximumAccountTransactions).Select(n=>new AccountTransaction(n,n,517,"existing",AccountGenre.RailService,0,0,ConstructionAccounts.DefaultInitialCash)).ToImmutableArray()));
        var w=new MixedWorld(empty.World.Profile,empty.World.Terrain,empty.World.Spatial.Capture(),[],[],revision:ProtocolIdentity.MaximumAccountTransactions);
        foreach (var accounts in new[]{exhausted,new ConstructionAccounts(long.MinValue)})
        {
            var state=new MixedSessionState(new(w,accounts),empty.Provenance,clock:new(517,0,[]),revision:w.Revision);
            using var h=Host(state);var before=await Save(h);var q=Query(h);
            Assert.Equal(ReferenceEquals(accounts,exhausted) ? "accountCapacityLimit" : "constructionOverflow",h.PreviewStationBuilding(q).Reason);
            Assert.Equal("invalidPlacement",Assert.IsType<RejectedMessage>(await Send(h,new EditStationBuildingCommand(q,Definition.Price))).Code);
            Assert.Equal(before,await Save(h));
        }
    }

}
