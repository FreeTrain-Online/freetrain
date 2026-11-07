using System.Collections.Immutable;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Server;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public partial class FleetMovementTests
{
    [Fact]
    public void Manager_browser_input_is_source_built_deployed_stock()
    {
        var state=WithFixtureController(FleetLine());OperationsBoundary(state);
        if(Environment.GetEnvironmentVariable("FREETRAIN_CONTROLLER_FLEET_OUTPUT") is {} output)
            File.WriteAllBytes(output,SaveCodec.Write(state));
    }
    static ManageFleetControllerCommand ManagerCreate(string name="Named",FleetControllerKind kind=FleetControllerKind.Timetable)
    {
        var b=RollingStockCatalog.Bundled.FleetProfile.Providers.Single(p=>p.Kind==kind).Binding;
        return new("create",0,0,name,new(b.PackId,b.Version,b.CatalogHash,b.ContributionId),null);
    }
    static FleetControllerRead ManagerController(SessionHost h)=>h.Snapshot().Mixed!.Fleet!.Controllers.Last();
    static ManageFleetControllerCommand ManagerEdit(FleetControllerRead c,string action,string name="",FleetHarborRead[]? harbors=null)
        =>new(action,c.Id,c.Revision,name,null,harbors,action=="configure"?harbors?.FirstOrDefault()?.Location??new(22,20,2):null);
    static AssignFleetControllerCommand ManagerAssign(SessionHost h,long id,string kind,FleetControllerRead controller)
    {
        var f=h.Snapshot().Mixed!.Fleet!;
        var revision=kind=="train"?f.Trains.Single(t=>t.Id==id).Revision:f.Groups.Single(g=>g.Id==id).Revision;
        return new(kind,id,revision,controller.Id,controller.Revision);
    }
    static void ManagerRejected(WireMessage message,string code)=>Assert.Equal(code,Assert.IsType<RejectedMessage>(message).Code);

    [Fact]
    public async Task Manager_named_instances_share_assignments_but_copy_isolates_location_data_and_reopens_exactly()
    {
        using var h=FleetHost();var before=h.Snapshot();
        Assert.IsType<AcceptedMessage>(await FleetSend(h,ManagerCreate("  Shared timetable  ",FleetControllerKind.Timetable)));
        var source=ManagerController(h);Assert.Equal("Shared timetable",source.Name);
        FleetHarborRead[] settings=[new(new(22,20,2),"fixed",17,false),new(new(26,20,2),"daily",480,true)];
        foreach(var setting in settings){Assert.IsType<AcceptedMessage>(await FleetSend(h,ManagerEdit(source,"configure",harbors:[setting])));source=ManagerController(h);}
        foreach(var t in before.Mixed!.Fleet!.Trains)
            Assert.IsType<AcceptedMessage>(await FleetSend(h,ManagerAssign(h,t.Id,"train",source)));
        var save=await FleetSave(h);var f=h.Snapshot().Mixed!.Fleet!;
        Assert.All(f.Trains,t=>Assert.Equal(source.Id,t.ControllerId));
        Assert.IsType<AcceptedMessage>(await FleetSend(h,ManagerEdit(source,"copy")));
        var copy=ManagerController(h);Assert.NotEqual(source.Id,copy.Id);Assert.Equal(0,copy.Revision);Assert.Equal("Shared timetable 1",copy.Name);
        Assert.Equal(source.Harbors,copy.Harbors);
        settings[0]=settings[0] with {Minutes=31};
        Assert.IsType<AcceptedMessage>(await FleetSend(h,ManagerEdit(source,"configure",harbors:[settings[0]])));
        f=h.Snapshot().Mixed!.Fleet!;Assert.Equal(31,f.Controllers.Single(c=>c.Id==source.Id).Harbors[0].Minutes);
        Assert.Equal(17,f.Controllers.Single(c=>c.Id==copy.Id).Harbors[0].Minutes);
        Assert.All(f.Trains,t=>Assert.Equal(source.Id,t.ControllerId));
        source=f.Controllers.Single(c=>c.Id==source.Id);
        Assert.IsType<AcceptedMessage>(await FleetSend(h,ManagerEdit(source,"rename","Renamed")));
        Assert.All(h.Snapshot().Mixed!.Fleet!.Trains,t=>Assert.Equal(source.Id,t.ControllerId));
        var after=h.Snapshot();Assert.Equal(before.Tick,after.Tick);Assert.Equal(before.Cash,after.Cash);
        Assert.Equal(before.Mixed.Queue!.Pending,after.Mixed!.Queue!.Pending);Assert.Equal(before.Mixed.Queue.NextOrder,after.Mixed.Queue.NextOrder);
        var bytes=await FleetSave(h);using var reopened=new SessionHost(SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        Assert.Equal(bytes,await FleetSave(reopened));
        await FleetStep(h,20);await FleetStep(reopened,20);Assert.Equal(await FleetSave(h),await FleetSave(reopened));
        var running=h.Snapshot();Assert.All(running.Mixed!.Fleet!.Trains,t=>Assert.Equal(1,t.StopCallCount));
        Assert.Equal(new long[]{133,480},running.Mixed.Queue!.Pending.Select(a=>a.DueMinute).ToArray());
        Assert.All(running.Mixed.Fleet.Trains,t=>Assert.Equal(0,t.MoveCount));
        Assert.NotEqual(save,bytes);
    }

    [Fact]
    public async Task Manager_referenced_delete_lists_direct_train_and_group_dependencies_and_preserves_builtins()
    {
        using var h=FleetHost();Assert.IsType<AcceptedMessage>(await FleetSend(h,ManagerCreate()));var c=ManagerController(h);
        var f=h.Snapshot().Mixed!.Fleet!;var t=f.Trains[0];var group=f.Groups[0];var simple=f.Controllers.Single(c=>c.Kind=="simple");
        Assert.IsType<AcceptedMessage>(await FleetSend(h,ManagerAssign(h,t.Id,"train",c)));
        var before=await FleetSave(h);ManagerRejected(await FleetSend(h,ManagerEdit(c,"delete")),"controllerReferenced");Assert.Equal(before,await FleetSave(h));
        Assert.IsType<AcceptedMessage>(await FleetSend(h,ManagerAssign(h,t.Id,"train",simple)));
        Assert.IsType<AcceptedMessage>(await FleetSend(h,ManagerAssign(h,group.Id,"group",c)));
        ManagerRejected(await FleetSend(h,ManagerEdit(c,"delete")),"controllerReferenced");
        Assert.IsType<AcceptedMessage>(await FleetSend(h,ManagerAssign(h,group.Id,"group",simple)));
        Assert.IsType<AcceptedMessage>(await FleetSend(h,ManagerEdit(c,"delete")));
        Assert.DoesNotContain(h.Snapshot().Mixed!.Fleet!.Controllers,x=>x.Id==c.Id);
        foreach(var builtin in f.Controllers.Where(c=>c.Kind is "simple" or "delegation"))
            foreach(var action in new[]{"delete","copy","rename","configure"})
                ManagerRejected(await FleetSend(h,ManagerEdit(builtin,action)),"invalidInput");
    }

    [Theory]
    [InlineData("create")][InlineData("rename")][InlineData("copy")][InlineData("configure")][InlineData("delete")][InlineData("train")][InlineData("group")]
    public async Task Manager_every_mutation_fences_role_epoch_stale_and_duplicate_receipts(string action)
    {
        using var h=FleetHost();Assert.IsType<AcceptedMessage>(await FleetSend(h,ManagerCreate(kind:FleetControllerKind.Timetable)));
        var c=ManagerController(h);var f=h.Snapshot().Mixed!.Fleet!;
        CommandPayload payload=action switch {
            "create"=>ManagerCreate(),"rename"=>ManagerEdit(c,action,"New name"),"configure"=>ManagerEdit(c,action,harbors:[]),
            "train"=>ManagerAssign(h,f.Trains[0].Id,action,c),"group"=>ManagerAssign(h,f.RootGroupId,action,c),_=>ManagerEdit(c,action)};
        var observer=new ParticipantContext("observer",ParticipantRole.Observer);h.RegisterParticipant(observer);
        var before=await FleetSave(h);ManagerRejected(await FleetSend(h,payload,observer),"notAuthorized");
        ManagerRejected(await FleetSend(h,payload,epoch:h.WorldEpoch+1),"staleEpoch");Assert.Equal(before,await FleetSave(h));
        var network=new NetworkSession(new(),hostFactory:(_,_)=>h);var peer=network.Connect(FleetNow,FleetOwner)!;
        async Task<WireMessage> send(CommandPayload p){var task=network.Submit(peer,new(Guid.NewGuid().ToString(),h.WorldEpoch,p),FleetNow);network.Tick(FleetNow);return await task;}
        var command=new CommandMessage("manager-retry",h.WorldEpoch,payload);
        var task=network.Submit(peer,command,FleetNow);Assert.Same(task,network.Submit(peer,command,FleetNow));network.Tick(FleetNow);Assert.IsType<AcceptedMessage>(await task);
        var accepted=await FleetSave(h);var retry=network.Submit(peer,command,FleetNow);network.Tick(FleetNow);
        Assert.Equal(await task,await retry);Assert.Equal(accepted,await FleetSave(h));
        if(action!="create"){
            var stale=action=="copy"?ManagerEdit(c,"rename","Changed first"):null;
            if(stale!=null)Assert.IsType<AcceptedMessage>(await send(stale));
            ManagerRejected(await send(payload),"staleRevision");
        }
        else {
            var invalid=(ManageFleetControllerCommand)payload;
            ManagerRejected(await send(invalid with {ExpectedRevision=1}),"invalidInput");
        }
    }

    [Fact]
    public async Task Manager_group_delegation_allows_operator_edits_without_driver_ownership()
    {
        using var h=FleetHost();h.RegisterParticipant(FleetOperator);
        Assert.IsType<AcceptedMessage>(await FleetSend(h,ManagerCreate()));var c=ManagerController(h);
        var f=h.Snapshot().Mixed!.Fleet!;var train=f.Trains[0];var delegation=f.Controllers.Single(c=>c.Kind=="delegation");
        Assert.IsType<AcceptedMessage>(await FleetSend(h,ManagerAssign(h,train.Id,"train",delegation)));
        Assert.IsType<AcceptedMessage>(await FleetSend(h,ManagerAssign(h,f.RootGroupId,"group",c),FleetOperator));
        Assert.IsType<AcceptedMessage>(await FleetSend(h,ManagerEdit(c,"rename","Fresh")));
        Assert.Equal(c.Id,GroupEffective(await FleetSave(h),train.Id));
        Assert.Equal(f.Trains[1].ControllerId,h.Snapshot().Mixed!.Fleet!.Trains[1].ControllerId);
    }

    [Theory]
    [InlineData(479,1)][InlineData(480,1440)][InlineData(481,1439)]
    public void Manager_TAT_basic_location_data_uses_source_daily_equality_and_is_not_a_signal_override(int arrival,int wait)
    {
        var s=TimingSession();var t=s.Fleet!.Trains[0];var p=new Location(22,20,2);
        var c=new FleetController(900,0,"TAT",FleetControllerKind.Timetable,Harbors:[new(p,FleetHarborKind.Daily,480,true)]);
        var call=new FleetHarborCall(c,new FleetHarborReference.Signal(p,100,0),p,0);
        Assert.Equal(wait,FleetControllers.TimetableStop(t,call,new(arrival)).Decision.WaitMinutes);
        Assert.Equal(FleetStopAction.Reverse,FleetControllers.TimetableStop(t,call with {CallCount=1},new(arrival)).Decision.Action);
        Assert.Equal(10,FleetControllers.TimetableStop(t,call with {Harbor=new FleetHarborReference.Signal(new(23,20,2),101,0)},new(arrival)).Decision.WaitMinutes);
    }

    [Fact]
    public async Task Manager_invalid_provider_names_and_location_data_fail_without_mutation()
    {
        using var h=FleetHost();var before=await FleetSave(h);
        foreach(var name in new[]{" ","bad\nname",new string('x',257)})ManagerRejected(await FleetSend(h,ManagerCreate(name)),"invalidInput");
        var create=ManagerCreate();ManagerRejected(await FleetSend(h,create with {Provider=create.Provider! with {CatalogHash=new string('0',64)}}),"operationUnavailable");
        Assert.Equal(before,await FleetSave(h));
        Assert.IsType<AcceptedMessage>(await FleetSend(h,ManagerCreate(kind:FleetControllerKind.Timetable)));var c=ManagerController(h);before=await FleetSave(h);
        foreach(var settings in new FleetHarborRead[][]{
            [new(new(1,2,3),"daily",1440,false)],[new(new(1,2,3),"fixed",-1,false)],
            [new(new(1,2,3),"pass",0,true)],[new(new(1,2,3),"pass",1,false)],
            [new(new(1,2,3),"pass",0,false),new(new(1,2,3),"fixed",10,false)]})
            ManagerRejected(await FleetSend(h,ManagerEdit(c,"configure",harbors:settings)),"invalidInput");
        Assert.Equal(before,await FleetSave(h));
    }
    [Fact]
    public async Task Manager_controller_revision_fences_assignment_and_capacity_rejection_does_not_consume_ids()
    {
        var state=WithFixtureController(FleetLine());var w=state.World;var f=w.Fleet!;var controller=f.Controllers.Last();var next=w.Spatial.NextEntityId;
        var rows=f.Controllers.AddRange(Enumerable.Range(0,FleetGraph.MaximumControllers-f.Controllers.Length)
            .Select(i=>controller with {Id=next+i,Revision=0,Name="Controller "+i}));
        var world=w.Replace(w.Revision,w.Terrain,new(next+FleetGraph.MaximumControllers-f.Controllers.Length,w.Spatial.Entities),w.Voxels,w.Slopes,w.SpeedLimits,
            new(f.Profile,f.RootGroupId,f.Trains,f.Groups,rows));
        using var h=new SessionHost(state.Successor(state.Revision,new(world,state.Construction.Accounts),false),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var c=ManagerController(h);var before=await FleetSave(h);
        ManagerRejected(await FleetSend(h,ManagerCreate()),"invalidInput");ManagerRejected(await FleetSend(h,ManagerEdit(c,"copy")),"invalidInput");
        Assert.Equal(before,await FleetSave(h));
        var assignment=ManagerAssign(h,f.Trains[0].Id,"train",c);
        Assert.IsType<AcceptedMessage>(await FleetSend(h,ManagerEdit(c,"rename","Changed")));
        ManagerRejected(await FleetSend(h,assignment),"staleRevision");
        c=ManagerController(h);Assert.IsType<AcceptedMessage>(await FleetSend(h,ManagerEdit(c,"delete")));
        var allocator=h.Snapshot().Mixed!.NextEntityId;
        Assert.IsType<AcceptedMessage>(await FleetSend(h,ManagerCreate()));
        Assert.Equal(allocator,ManagerController(h).Id);
    }

    [Fact]
    public async Task Manager_one_location_patch_stays_below_network_limit_with_8192_saved_settings_and_preserves_other_entries()
    {
        var state=WithFixtureController(FleetLine());var w=state.World;var f=w.Fleet!;var id=w.Spatial.NextEntityId;
        var provider=f.Profile.Providers.Single(p=>p.Kind==FleetControllerKind.Timetable);
        var harbors=Enumerable.Range(0,8192).Select(i=>new FleetHarborSetting(new(i,20,2),FleetHarborKind.Fixed,10,false)).ToImmutableArray();
        var graph=new FleetGraph(f.Profile,f.RootGroupId,f.Trains,f.Groups,f.Controllers.Add(new(id,0,"Large",provider.Kind,provider.Binding,harbors)));
        var world=w.Replace(w.Revision,w.Terrain,new(id+1,w.Spatial.Entities),w.Voxels,w.Slopes,w.SpeedLimits,graph);
        using var h=new SessionHost(state.Successor(state.Revision,new(world,state.Construction.Accounts),false),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var c=ManagerController(h);var command=ManagerEdit(c,"configure",harbors:[new(new(8191,20,2),"fixed",19,true)]);
        Assert.True(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes<WireMessage>(new CommandMessage("large",h.WorldEpoch,command),WireJson.CreateOptions()).Length<NetworkEndpoints.MaximumMessageBytes);
        Assert.IsType<AcceptedMessage>(await FleetSend(h,command));c=ManagerController(h);
        Assert.Equal(8192,c.Harbors.Length);Assert.Equal(19,c.Harbors[^1].Minutes);Assert.True(c.Harbors[^1].Reverse);
        Assert.All(c.Harbors[..^1],s=>Assert.Equal(10,s.Minutes));
        var before=await FleetSave(h);
        ManagerRejected(await FleetSend(h,ManagerEdit(c,"configure",harbors:[new(new(8192,20,2),"pass",0,false)])),"invalidInput");Assert.Equal(before,await FleetSave(h));
        Assert.IsType<AcceptedMessage>(await FleetSend(h,ManagerEdit(c,"configure",harbors:[]) with {Location=new(8191,20,2)}));
        Assert.Equal(8191,ManagerController(h).Harbors.Length);
    }

}
