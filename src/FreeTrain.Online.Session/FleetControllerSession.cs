using System.Collections.Immutable;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Session;

public sealed partial class SessionHost
{
    // All controller edits enter the same FIFO/receipt/epoch boundary as train
    // commands. Drafts carry member revisions, independent of the moving clock.
    private string? PublishControllers(ParticipantContext participant, MixedSessionState state, FleetGraph graph, long nextId)
    {
        var affected=(state.Fleet?.Trains ?? []).Where(t=> {
            var before=state.Fleet!.EffectiveController(t.Id);var after=graph.EffectiveController(t.Id);
            return before.Id!=after.Id || before.Revision!=after.Revision;
        }).Select(t=>t.Id).ToArray();
        if(affected.Any(id=>fleetLeases.TryGetValue(id,out var lease) && lease.Participant!=participant))return "controlBusy";
        var w=state.World;
        var world=w.Replace(w.Revision,w.Terrain,new(nextId,w.Spatial.Entities),w.Voxels,w.Slopes,w.SpeedLimits,graph);
        var successor=state.Successor(state.Revision,new(world,state.Construction.Accounts),state.ConstructionMode);
        MixedWire.ValidateTransition(state,successor);
        mixedState=successor;terrainState=null;
        foreach(var id in affected)fleetLeases.Remove(id); // No held intent survives reassignment/configuration.
        return null;
    }
    private string? ManageFleetController(ParticipantContext participant, ManageFleetControllerCommand command)
    {
        try
        {
            var state=mixedState;
            if(state==null && command.Action=="create" && terrainState is {} terrain)
                state=RailwayState(WorldEpoch,terrain.World.Revision,TerrainProfile.ContentVersion);
            if(state==null)return "operationUnavailable";
            var nextId=state.World.Spatial.NextEntityId;
            var f=state.Fleet;
            if(f==null)
            {
                if(command.Action!="create" || state.World.Profile.FleetProfile is not {} profile)return "operationUnavailable";
                f=FleetGraph.Empty(profile,nextId);nextId=checked(nextId+3);
            }
            var rows=f.Controllers;
            if(command.Name==null)return "invalidInput";
            if(command.Action=="create")
            {
                if(command.ControllerId!=0 || command.ExpectedRevision!=0 || command.Harbors!=null || command.Location!=null || command.Provider is not {} p)
                    return "invalidInput";
                var binding=new ContentBinding(p.PackId,p.Version,p.CatalogHash,p.ContributionId);
                var provider=f.Profile.Providers.SingleOrDefault(p=>p.Binding==binding);
                if(provider==null)return "operationUnavailable";
                rows=rows.Add(new(nextId,0,command.Name.Trim(),provider.Kind,binding));nextId=checked(nextId+1);
            }
            else
            {
                var selected=rows.SingleOrDefault(c=>c.Id==command.ControllerId);
                if(selected==null)return "staleRevision";
                if(selected.Revision!=command.ExpectedRevision)return "staleRevision";
                if(selected.Kind is FleetControllerKind.Simple or FleetControllerKind.Delegation || command.Provider!=null || command.Action!="configure" && command.Location!=null)
                    return "invalidInput";
                switch(command.Action)
                {
                    case "rename" when command.Harbors==null:
                        rows=rows.Replace(selected,selected with {Name=command.Name.Trim(),Revision=checked(selected.Revision+1)});break;
                    case "copy" when command.Harbors==null && command.Name=="":
                        // Source chooses the first unused numeric suffix. Immutable
                        // typed records replace BinaryFormatter's executable clone.
                        var suffix=1;string name;
                        do {var end=" "+suffix++;name=selected.Name[..Math.Min(selected.Name.Length,256-end.Length)]+end;}
                        while(rows.Any(c=>c.Name==name));
                        rows=rows.Add(selected with {Id=nextId,Revision=0,Name=name,
                            Harbors=(selected.Harbors.IsDefault?[]:selected.Harbors).Select(h=>h with {}).ToImmutableArray()});
                        nextId=checked(nextId+1);break;
                    case "configure" when command.Name=="" && command.Harbors is {} settings && command.Location is {} location && selected.Kind==FleetControllerKind.Timetable:
                        // One location per command keeps edits below the existing
                        // 16 KiB network boundary even at 8192 stored settings.
                        if(settings.Length>1 || settings.Any(h=>h==null || h.Location!=location || h.Kind is not ("pass" or "fixed" or "daily")))return "invalidInput";
                        var key=new Location(location.X,location.Y,location.Z);
                        var harbors=selected.Harbors.IsDefault?ImmutableArray<FleetHarborSetting>.Empty:selected.Harbors;
                        var prior=harbors.SingleOrDefault(h=>h.Location==key);
                        if(settings.Length==0){if(prior!=null)harbors=harbors.Remove(prior);}
                        else {
                            var h=settings[0];var replacement=new FleetHarborSetting(key,
                                h.Kind switch {"pass"=>FleetHarborKind.Pass,"fixed"=>FleetHarborKind.Fixed,_=>FleetHarborKind.Daily},h.Minutes,h.Reverse);
                            harbors=prior==null?harbors.Add(replacement):harbors.Replace(prior,replacement);
                        }
                        rows=rows.Replace(selected,selected with {Harbors=harbors,Revision=checked(selected.Revision+1)});break;
                    case "delete" when command.Name=="" && command.Harbors==null:
                        if(f.Trains.Any(t=>t.ControllerId==selected.Id)||f.Groups.Any(g=>g.ControllerId==selected.Id))return "controllerReferenced";
                        rows=rows.Remove(selected);break;
                    default:return "invalidInput";
                }
            }
            return PublishControllers(participant,state,new(f.Profile,f.RootGroupId,f.Trains,f.Groups,rows),nextId);
        }
        catch(Exception e) when(e is ArgumentException or InvalidOperationException or OverflowException or InvalidDataException){return "invalidInput";}
    }
    private string? AssignFleetController(ParticipantContext participant, AssignFleetControllerCommand command)
    {
        try
        {
            var state=mixedState;if(state?.Fleet is not {} f)return "operationUnavailable";
            var controller=f.Controllers.SingleOrDefault(c=>c.Id==command.ControllerId);
            if(controller==null||controller.Revision!=command.ExpectedControllerRevision)return "staleRevision";
            var trains=f.Trains;var groups=f.Groups;
            if(command.TargetKind=="train")
            {
                var train=f.Trains.SingleOrDefault(t=>t.Id==command.TargetId);
                if(train==null||train.Revision!=command.ExpectedRevision)return "staleRevision";
                // Even an identical assignment cannot bypass another driver's lease.
                if(fleetLeases.TryGetValue(train.Id,out var lease)&&lease.Participant!=participant)return "controlBusy";
                trains=trains.Replace(train,train with {ControllerId=controller.Id,Revision=checked(train.Revision+1)});
            }
            else if(command.TargetKind=="group")
            {
                var group=f.Groups.SingleOrDefault(g=>g.Id==command.TargetId);
                if(group==null||group.Revision!=command.ExpectedRevision)return "staleRevision";
                groups=groups.Replace(group,group with {ControllerId=controller.Id,Revision=checked(group.Revision+1)});
            }
            else return "invalidInput";
            var error=PublishControllers(participant,state,new(f.Profile,f.RootGroupId,trains,groups,f.Controllers),state.World.Spatial.NextEntityId);
            if(error==null && command.TargetKind=="train")fleetLeases.Remove(command.TargetId);
            return error;
        }
        catch(Exception e) when(e is ArgumentException or InvalidOperationException or OverflowException or InvalidDataException){return "invalidInput";}
    }
}
