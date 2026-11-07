using FreeTrain.Online.Contracts;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Server;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public partial class FleetMovementTests
{
    static ManageFleetGroupCommand GroupCreate(SessionHost h,string name,long? parent=null)
    {
        var f=h.Snapshot().Mixed!.Fleet!;var p=f.Groups.Single(g=>g.Id==(parent??f.RootGroupId));
        return new("create","group",0,0,name,p.Id,p.Revision);
    }
    static ManageFleetGroupCommand GroupMove(SessionHost h,long id,long parent,string kind="group")
    {
        var f=h.Snapshot().Mixed!.Fleet!;var p=f.Groups.Single(g=>g.Id==parent);
        var revision=kind=="group"?f.Groups.Single(g=>g.Id==id).Revision:f.Trains.Single(t=>t.Id==id).Revision;
        return new("move",kind,id,revision,"",p.Id,p.Revision);
    }
    static ManageFleetGroupCommand GroupRename(SessionHost h,long id,string name)
        =>new("rename","group",id,h.Snapshot().Mixed!.Fleet!.Groups.Single(g=>g.Id==id).Revision,name,null,null);
    static MixedSessionState GroupDecode(byte[] bytes)=>SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
    static long GroupEffective(byte[] bytes,long trainId)=>GroupDecode(bytes).Fleet!.EffectiveController(trainId).Id;

    [Fact]
    public async Task Groups_MAN_A17_nested_moves_ancestor_edits_override_removal_sale_and_continuation()
    {
        using var h=ManualHost();var initial=h.Snapshot();var f=initial.Mixed!.Fleet!;
        var root=f.RootGroupId;var train=f.Trains[0];var neighbor=f.Trains[1];
        var simple=f.Controllers.Single(c=>c.Kind=="simple");var delegation=f.Controllers.Single(c=>c.Kind=="delegation");
        var manual=f.Controllers.Single(c=>c.Kind=="manual");
        Assert.IsType<AcceptedMessage>(await ManualSend(h,GroupCreate(h,"  本線  ")));var a=h.Snapshot().Mixed!.Fleet!.Groups.Last();
        Assert.Equal("本線",a.Name);Assert.Equal(delegation.Id,a.ControllerId);
        Assert.IsType<AcceptedMessage>(await ManualSend(h,GroupCreate(h,"Branch",a.Id)));var b=h.Snapshot().Mixed!.Fleet!.Groups.Last();
        Assert.IsType<AcceptedMessage>(await ManualSend(h,GroupCreate(h,"Other")));var other=h.Snapshot().Mixed!.Fleet!.Groups.Last();
        Assert.IsType<AcceptedMessage>(await ManualSend(h,ManagerAssign(h,a.Id,"group",manual)));
        Assert.IsType<AcceptedMessage>(await ManualSend(h,GroupMove(h,train.Id,b.Id,"train")));
        Assert.Equal(manual.Id,h.Snapshot().Mixed!.Fleet!.Trains[0].ControllerId); // Move retains override.
        Assert.IsType<AcceptedMessage>(await ManualSend(h,ManagerAssign(h,train.Id,"train",delegation)));
        Assert.Equal(manual.Id,GroupEffective(await ManualSave(h),train.Id));
        Assert.IsType<AcceptedMessage>(await ManualSend(h,ManagerAssign(h,a.Id,"group",simple)));
        Assert.Equal(simple.Id,GroupEffective(await ManualSave(h),train.Id));
        Assert.Equal(manual.Id,GroupEffective(await ManualSave(h),neighbor.Id)); // Explicit neighbor is isolated.
        Assert.IsType<AcceptedMessage>(await ManualSend(h,ManagerAssign(h,other.Id,"group",manual)));
        Assert.IsType<AcceptedMessage>(await ManualSend(h,GroupMove(h,b.Id,other.Id)));
        Assert.Equal(manual.Id,GroupEffective(await ManualSave(h),train.Id));
        Assert.IsType<AcceptedMessage>(await ManualSend(h,ManagerAssign(h,train.Id,"train",simple)));
        Assert.IsType<AcceptedMessage>(await ManualSend(h,ManagerAssign(h,other.Id,"group",delegation)));
        Assert.Equal(simple.Id,GroupEffective(await ManualSave(h),train.Id));
        Assert.IsType<AcceptedMessage>(await ManualSend(h,ManagerAssign(h,train.Id,"train",delegation)));
        Assert.Equal(simple.Id,GroupEffective(await ManualSave(h),train.Id)); // Exhausted/root-simple fallback.
        Assert.IsType<AcceptedMessage>(await ManualSend(h,GroupRename(h,b.Id,"支線・Branch")));
        var changed=h.Snapshot();Assert.Equal(initial.Tick,changed.Tick);Assert.Equal(initial.Cash,changed.Cash);
        Assert.Equal(initial.Mixed.Queue!.Pending,changed.Mixed!.Queue!.Pending);Assert.Equal(initial.Mixed.Queue.NextOrder,changed.Mixed.Queue.NextOrder);
        Assert.Equal(initial.Mixed.Voxels,changed.Mixed.Voxels);
        var bytes=await ManualSave(h);using var reopened=new SessionHost(GroupDecode(bytes),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        Assert.Equal(bytes,await ManualSave(reopened));
        await ManualStep(h,20);await ManualStep(reopened,20);Assert.Equal(await ManualSave(h),await ManualSave(reopened));
        var read=h.Snapshot();
        var withdraw=new FleetDeploymentQuery(h.WorldEpoch,read.Revision,read.Tick,ProtocolIdentity.MixedContent,train.Id,"withdraw",null,false);
        Assert.IsType<AcceptedMessage>(await ManualSend(h,new DeployFleetCommand(withdraw)));
        read=h.Snapshot();var sale=new FleetTradeQuery(h.WorldEpoch,read.Revision,read.Tick,ProtocolIdentity.MixedContent,"sell",null,0,0,train.Id);
        Assert.IsType<AcceptedMessage>(await ManualSend(h,new TradeFleetCommand(sale,h.PreviewFleetTrade(sale).Price)));
        f=h.Snapshot().Mixed!.Fleet!;Assert.DoesNotContain(f.Trains,t=>t.Id==train.Id);
        Assert.Empty(f.Groups.Single(g=>g.Id==b.Id).Members);Assert.Contains(f.Groups,g=>g.Id==b.Id);
        bytes=await ManualSave(h);Assert.Equal(bytes,SaveCodec.Write(GroupDecode(bytes)));
    }

    [Theory]
    [InlineData("self")][InlineData("descendant")][InlineData("root")][InlineData("missing")][InlineData("same")]
    [InlineData("delete")][InlineData("blank")][InlineData("control")][InlineData("long")][InlineData("stale-target")][InlineData("stale-parent")]
    public async Task Groups_invalid_operations_preserve_graph_allocator_time_and_queue(string fault)
    {
        using var h=ManualHost();Assert.IsType<AcceptedMessage>(await ManualSend(h,GroupCreate(h,"A")));var a=h.Snapshot().Mixed!.Fleet!.Groups.Last();
        Assert.IsType<AcceptedMessage>(await ManualSend(h,GroupCreate(h,"B",a.Id)));var b=h.Snapshot().Mixed!.Fleet!.Groups.Last();var root=h.Snapshot().Mixed!.Fleet!.RootGroupId;
        var command=fault switch {
            "self"=>GroupMove(h,a.Id,a.Id),"descendant"=>GroupMove(h,a.Id,b.Id),"root"=>GroupMove(h,root,b.Id),
            "same"=>GroupMove(h,b.Id,a.Id),"delete"=>GroupRename(h,a.Id,"") with {Action="delete"},
            "blank"=>GroupCreate(h,"  "),"control"=>GroupCreate(h,"x\u007f"),"long"=>GroupCreate(h,new string('a',257)),
            "stale-target"=>GroupMove(h,b.Id,root) with {ExpectedRevision=99},"stale-parent"=>GroupMove(h,b.Id,root) with {ExpectedParentRevision=99},
            _=>GroupMove(h,b.Id,root) with {ParentId=long.MaxValue}};
        var before=await ManualSave(h);var error=fault is "missing" or "stale-target" or "stale-parent"?"staleRevision":"invalidInput";
        ManagerRejected(await ManualSend(h,command),error);Assert.Equal(before,await ManualSave(h));
    }

    [Theory]
    [InlineData("create")][InlineData("rename")][InlineData("group")][InlineData("train")]
    public async Task Groups_mutations_fence_role_epoch_member_revisions_and_deduplicate_receipts(string action)
    {
        using var h=ManualHost();Assert.IsType<AcceptedMessage>(await ManualSend(h,GroupCreate(h,"A")));var a=h.Snapshot().Mixed!.Fleet!.Groups.Last();
        Assert.IsType<AcceptedMessage>(await ManualSend(h,GroupCreate(h,"B")));var b=h.Snapshot().Mixed!.Fleet!.Groups.Last();
        var command=action switch {"create"=>GroupCreate(h,"C"),"rename"=>GroupRename(h,a.Id,"Named"),"group"=>GroupMove(h,a.Id,b.Id),_=>GroupMove(h,h.Snapshot().Mixed!.Fleet!.Trains[0].Id,a.Id,"train")};
        var observer=new ParticipantContext("observer",ParticipantRole.Observer);h.RegisterParticipant(observer);var before=await ManualSave(h);
        ManagerRejected(await ManualSend(h,command,observer),"notAuthorized");ManagerRejected(await ManualSend(h,command,epoch:h.WorldEpoch+1),"staleEpoch");Assert.Equal(before,await ManualSave(h));
        var network=new NetworkSession(new(),hostFactory:(_,_)=>h);var peer=network.Connect(ManualNow,ManualOwner)!;
        var request=new CommandMessage("group-receipt",h.WorldEpoch,command);var task=network.Submit(peer,request,ManualNow);
        network.Tick(ManualNow);Assert.IsType<AcceptedMessage>(await task);var after=await ManualSave(h);
        Assert.Equal(await task,await network.Submit(peer,request,ManualNow));Assert.Equal(after,await ManualSave(h));
        var retry=network.Submit(peer,new(Guid.NewGuid().ToString(),h.WorldEpoch,command),ManualNow);network.Tick(ManualNow);
        ManagerRejected(await retry,"staleRevision");
    }

    [Fact]
    public async Task Groups_reparenting_respects_inherited_manual_leases_and_resets_changed_owned_input()
    {
        using var h=ManualHost();h.RegisterParticipant(ManualOperator);var f=h.Snapshot().Mixed!.Fleet!;
        var train=f.Trains[0];var manual=f.Controllers.Single(c=>c.Kind=="manual");var delegation=f.Controllers.Single(c=>c.Kind=="delegation");
        Assert.IsType<AcceptedMessage>(await ManualSend(h,GroupCreate(h,"A")));var a=h.Snapshot().Mixed!.Fleet!.Groups.Last();
        Assert.IsType<AcceptedMessage>(await ManualSend(h,GroupCreate(h,"B")));var b=h.Snapshot().Mixed!.Fleet!.Groups.Last();
        Assert.IsType<AcceptedMessage>(await ManualSend(h,ManagerAssign(h,a.Id,"group",manual)));
        Assert.IsType<AcceptedMessage>(await ManualSend(h,GroupCreate(h,"Child",a.Id)));var child=h.Snapshot().Mixed!.Fleet!.Groups.Last();
        Assert.IsType<AcceptedMessage>(await ManualSend(h,GroupMove(h,train.Id,child.Id,"train")));
        Assert.IsType<AcceptedMessage>(await ManualSend(h,ManagerAssign(h,train.Id,"train",delegation)));
        Assert.IsType<AcceptedMessage>(await ManualSend(h,new AcquireControlCommand(train.Id.ToString()),ManualOperator));
        var lease=ManualLease(h,train.Id);Assert.IsType<AcceptedMessage>(await ManualSend(h,ManualKeys(lease,left:true),ManualOperator));
        var before=await ManualSave(h);
        ManagerRejected(await ManualSend(h,GroupMove(h,child.Id,b.Id)),"controlBusy");Assert.Equal(before,await ManualSave(h));
        Assert.IsType<AcceptedMessage>(await ManualSend(h,GroupMove(h,child.Id,b.Id),ManualOperator));Assert.Empty(h.Snapshot().FleetControl!);
        ManagerRejected(await ManualSend(h,ManualKeys(lease,1,left:true),ManualOperator),"leaseRequired");
    }
}
