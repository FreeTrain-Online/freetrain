using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;
using static FreeTrain.Online.Tests.StationStateTests;
using static FreeTrain.Online.Tests.StationBuildingConstructionTests;

namespace FreeTrain.Online.Tests;

public class PlatformPropertiesTests
{
    static MixedSessionState Setup(bool wide=false)
    {
        var s=Build(Build(Empty()),at:new(16,10,2));
        var plan=wide ? WidePlatformConstruction.Preview(s.World,new(12,12,2),new(2),4,false) :
            ThinPlatformConstruction.Preview(s.World,new(12,12,2),new(2),4,false);
        Assert.True(plan.Legal,plan.Reason);
        return wide ? WidePlatformConstruction.Commit(s,plan) : ThinPlatformConstruction.Commit(s,plan);
    }
    static SessionHost Host(MixedSessionState s)=>new(s,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
    static async Task<byte[]> Save(SessionHost h){var t=h.CaptureSave();h.Pump(0);return await t;}
    static async Task<WireMessage> Send(SessionHost h,CommandPayload p,ParticipantContext? who=null,long? epoch=null)
    {var t=h.Enqueue(who??new("owner"),new(Guid.NewGuid().ToString(),epoch??h.WorldEpoch,p));h.Pump(0);return await t;}
    static EditPlatformPropertiesCommand Command(SessionHost h,string name="Independent / 独立ホーム",int bell=1,int host=1)
    {
        var s=h.Snapshot();var g=s.Mixed!.StationGraph!;var p=g.Platforms[0];
        var q=new PlatformPropertiesQuery(s.WorldEpoch,s.Mixed.GeometryRevision,s.ContentVersion,p.Id,p.Revision);
        var c=host<0?null:h.PlatformProperties(q).Candidates[host];var b=Profile.Bells[bell].Reference;
        return new(q,name,new(b.PackId,b.ContributionId,b.RecipeId),c?.Id,c?.Revision??0,c?.StationRevision??0);
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task Atomic_properties_reparent_once_preserve_service_and_save_exact_bilingual_name_and_bell(bool wide)
    {
        var initial=Setup(wide);using var h=Host(initial);var before=h.Snapshot();var bytes=await Save(h);
        var command=Command(h);Assert.Equal(bytes,await Save(h));
        Assert.Equal(2,h.PlatformProperties(command.Query).Candidates.Length);
        Assert.IsType<AcceptedMessage>(await Send(h,command));
        var after=h.Snapshot();var p=after.Mixed!.StationGraph!.Platforms[0];
        Assert.Equal(command.Name,p.Name);Assert.Equal(command.Bell,p.Bell);Assert.Equal(command.HostId,p.HostId);Assert.Equal(1,p.Revision);
        Assert.Equal(before.Mixed!.StationGraph!.Buildings.Select(b=>b.Name),after.Mixed.StationGraph.Buildings.Select(b=>b.Name));
        Assert.Equal(before.Mixed.Queue!.NextOrder,after.Mixed.Queue!.NextOrder);Assert.Equal(before.Mixed.Queue.Pending,after.Mixed.Queue.Pending);Assert.Equal(before.Cash,after.Cash);
        Assert.Equal(before.Mixed.Accounts.Transactions,after.Mixed.Accounts.Transactions);
        Assert.Equal(JsonSerializer.Serialize(initial.Random.Capture()),JsonSerializer.Serialize(SaveCodec.ReadWorld(await Save(h),TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!.Random.Capture()));
        if(wide)Assert.Equal(p.Revision,after.Entities!.Single(e=>e.Id==p.Id).Revision);
        var saved=await Save(h);Assert.Equal(saved,SaveCodec.Write(SaveCodec.ReadWorld(saved,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!));
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,command)).Code);Assert.Equal(saved,await Save(h));
        // Exact same properties do not consume graph revisions or disconnect children.
        var same=Command(h);Assert.IsType<AcceptedMessage>(await Send(h,same));Assert.Equal(saved,await Save(h));
        Assert.IsType<AcceptedMessage>(await Send(h,Command(h,host:-1)));
        Assert.Null(h.Snapshot().Mixed!.StationGraph!.Platforms[0].HostId);
    }
    [Fact]
    public async Task Roles_invalid_names_bells_self_unknown_hosts_and_every_revision_fence_preserve_exact_save()
    {
        using var h=Host(Setup());var bytes=await Save(h);var command=Command(h);
        var observer=new ParticipantContext("observer",ParticipantRole.Observer);h.RegisterParticipant(observer);
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await Send(h,command,observer)).Code);
        foreach(var c in new[]{command with {Name=" "},command with {Name="bad\0name"},command with {Name=new string('a',129)},
            command with {Bell=command.Bell with {RecipeId="missing"}},command with {HostId=null,ExpectedHostRevision=1},
            command with {HostId=command.Query.PlatformId},command with {HostId=999},command with {ExpectedHostRevision=999},command with {ExpectedStationRevision=999},
            command with {Query=command.Query with {ExpectedPlatformRevision=999}},command with {Query=command.Query with {ExpectedGeometryRevision=999}},
            command with {Query=command.Query with {ExpectedContentVersion="missing"}},command with {Query=command.Query with {ExpectedWorldEpoch=999}}})
        {Assert.IsType<RejectedMessage>(await Send(h,c));Assert.Equal(bytes,await Save(h));}
        Assert.Equal("staleEpoch",Assert.IsType<RejectedMessage>(await Send(h,command,epoch:h.WorldEpoch+1)).Code);
        var op=new ParticipantContext("operator",ParticipantRole.Operator);h.RegisterParticipant(op);
        Assert.IsType<AcceptedMessage>(await Send(h,command,op));
    }
    [Fact]
    public async Task Parent_rename_and_removal_invalidate_candidate_drafts_without_retargeting_or_losing_names()
    {
        using var h=Host(Setup());var command=Command(h);var host=h.Snapshot().Mixed!.StationGraph!.Buildings.Single(b=>b.Id==command.HostId);
        Assert.IsType<AcceptedMessage>(await Send(h,new RenameStationCommand(host.Id.ToString(),host.Revision,"Renamed station")));
        var bytes=await Save(h);Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,command)).Code);Assert.Equal(bytes,await Save(h));
        command=Command(h);host=h.Snapshot().Mixed!.StationGraph!.Buildings.Single(b=>b.Id==command.HostId);
        var s=h.Snapshot();var q=new StationBuildingQuery(s.WorldEpoch,s.Mixed!.GeometryRevision,s.Tick,s.ContentVersion,"remove",host.Location,null,host.Id,host.Revision);
        Assert.IsType<AcceptedMessage>(await Send(h,new EditStationBuildingCommand(q,h.PreviewStationBuilding(q).Cost)));
        bytes=await Save(h);Assert.IsType<RejectedMessage>(await Send(h,command));Assert.Equal(bytes,await Save(h));
    }
    [Fact]
    public void A_combined_metadata_and_host_edit_recursively_disconnects_children_and_same_host_keeps_them()
    {
        var s=Setup(true);s=ThinPlatformConstruction.Commit(s,ThinPlatformConstruction.Preview(s.World,new(12,16,2),new(2),4,false));
        var g=s.World.Stations!;var parent=g.Platforms[0];var child=g.Platforms[1];
        s=StationHostConnections.Commit(s,StationHostConnections.Preview(s.World,child.Id,child.Revision,parent.Id,parent.Revision));
        g=s.World.Stations!;parent=g.Platform(parent.Id);var host=g.Building(parent.HostId!.Value);
        var named=FreeTrain.Online.Simulation.PlatformProperties.Apply(s,parent.Id,parent.Revision,"Named parent",parent.Bell,host.Id,host.Revision,host.Revision);
        Assert.Equal(parent.Children,named.World.Stations!.Platform(parent.Id).Children);
        g=named.World.Stations;parent=g.Platform(parent.Id);host=g.Buildings.Single(b=>b.Id!=host.Id);
        var changed=FreeTrain.Online.Simulation.PlatformProperties.Apply(named,parent.Id,parent.Revision,"Changed parent",Profile.Bells[2].Reference,host.Id,host.Revision,host.Revision);
        MixedWire.ValidateTransition(named,changed);
        Assert.Null(changed.World.Stations!.Platform(child.Id).HostId);Assert.Empty(changed.World.Stations.Platform(parent.Id).Children);
        Assert.Equal(parent.Revision+1,changed.World.Stations.Platform(parent.Id).Revision);
        Assert.Equal(named.Clock.Capture().Pending.ToArray(),changed.Clock.Capture().Pending.ToArray());
    }
}
