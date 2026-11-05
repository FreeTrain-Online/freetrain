using System.Globalization;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Session;

public sealed partial class SessionHost
{
    // Transport identity, lease generation and held input are host-local only.
    private sealed record FleetLease(ParticipantContext Participant,string Id,DateTimeOffset Expires,
        long ControllerId,long ControllerRevision,long Sequence = -1,DateTimeOffset? InputAt = null,FleetManualInput Input = default);
    private readonly Dictionary<long,FleetLease> fleetLeases = new();
    private static bool FleetId(string value,out long id) => long.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out id)
        && id>0 && value==id.ToString(CultureInfo.InvariantCulture);
    private FleetTrain? ControlledTrain(string value) => FleetId(value,out var id) ? mixedState?.Fleet?.Trains.FirstOrDefault(t=>t.Id==id) : null;
    private void MaintainFleetControl()
    {
        foreach(var (id,lease) in fleetLeases.ToArray())
        {
            var train=mixedState?.Fleet?.Trains.FirstOrDefault(t=>t.Id==id);
            var controller=train==null?null:mixedState!.Fleet!.EffectiveController(id);
            if(train==null || lostParticipants.Contains(lease.Participant) || boundaryTime>=lease.Expires ||
                controller!.Id!=lease.ControllerId || controller.Revision!=lease.ControllerRevision)
            { fleetLeases.Remove(id); mixedState?.ReviseSession(); }
            else if(resetInput || resetParticipants.Contains(lease.Participant) ||
                lease.InputAt is {} at && boundaryTime-at>=InputExpiry)
                fleetLeases[id]=lease with {Input=default,InputAt=null};
        }
        lostParticipants.Clear();resetParticipants.Clear();resetInput=false;
    }
    private string? AcquireFleet(ParticipantContext participant,string value)
    {
        if(ControlledTrain(value) is not {} train)return "invalidInput";
        if(fleetLeases.TryGetValue(train.Id,out var lease) && lease.Participant!=participant)return "controlBusy";
        var controller=mixedState!.Fleet!.EffectiveController(train.Id);
        // Every explicit acquisition/renewal starts a fresh generation. Delayed
        // keys from an earlier acquisition cannot revive input after a reset.
        fleetLeases[train.Id]=new(participant,(++nextLease).ToString(CultureInfo.InvariantCulture),
            boundaryTime+ControlLeaseDuration,controller.Id,controller.Revision);
        mixedState.ReviseSession();return null;
    }
    private string? RequireFleetLease(ParticipantContext participant,string value,string token,out FleetTrain? train,out FleetLease? lease)
    {
        train=ControlledTrain(value);lease=null;
        if(train==null || !fleetLeases.TryGetValue(train.Id,out lease) || lease.Participant!=participant || lease.Id!=token)
            return "leaseRequired";
        var controller=mixedState!.Fleet!.EffectiveController(train.Id);
        return controller.Id==lease.ControllerId && controller.Revision==lease.ControllerRevision ? null : "leaseRequired";
    }
    private string? ReleaseFleet(ParticipantContext participant,ReleaseControlCommand command)
    {
        var error=RequireFleetLease(participant,command.TrainId,command.LeaseId,out var train,out _);
        if(error!=null)return error;
        fleetLeases.Remove(train!.Id);mixedState!.ReviseSession();return null;
    }
    private string? InputFleet(ParticipantContext participant,ControllerInputCommand command)
    {
        var error=RequireFleetLease(participant,command.TrainId,command.LeaseId,out var train,out var lease);
        if(error!=null)return error;
        if(command.InputSequence<0 || command.InputSequence<=lease!.Sequence ||
            mixedState!.Fleet!.EffectiveController(train!.Id).Kind!=FleetControllerKind.Manual ||
            train.State==TrainOperationalState.Unplaced)return "invalidInput";
        fleetLeases[train.Id]=lease with {Sequence=command.InputSequence,Expires=boundaryTime+ControlLeaseDuration,
            InputAt=command.Reset?null:boundaryTime,Input=command.Reset?default:
                new(command.Pass,command.Depart,command.Reverse,command.Left,command.Right)};
        mixedState.ReviseSession();return null;
    }
    private string? SetFleetController(ParticipantContext participant,SetControllerCommand command)
    {
        var error=RequireFleetLease(participant,command.TrainId,command.LeaseId,out var train,out var lease);
        if(error!=null)return error;
        if(train!.Revision!=command.ExpectedRevision)return "staleRevision";
        if(command.Controller is not ("automatic" or "manual"))return "invalidInput";
        try
        {
            var state=mixedState!;var w=state.World;var fleet=w.Fleet!;
            var target=command.Controller=="automatic" ? fleet.Simple : fleet.Controllers.FirstOrDefault(c=>c.Kind==FleetControllerKind.Manual);
            var nextId=w.Spatial.NextEntityId;
            if(target==null)
            {
                var provider=fleet.Profile.Providers.FirstOrDefault(p=>p.Kind==FleetControllerKind.Manual);
                if(provider==null)return "operationUnavailable";
                target=new(nextId,0,"Manual",FleetControllerKind.Manual,provider.Binding);nextId=checked(nextId+1);
            }
            var graph=new FleetGraph(fleet.Profile,fleet.RootGroupId,fleet.Trains.Select(t=>t.Id==train.Id
                ?t with {ControllerId=target.Id,Revision=checked(t.Revision+1)}:t),fleet.Groups,
                fleet.Controllers.Any(c=>c.Id==target.Id)?fleet.Controllers:fleet.Controllers.Add(target));
            var world=w.Replace(w.Revision,w.Terrain,new(nextId,w.Spatial.Entities),w.Voxels,w.Slopes,w.SpeedLimits,graph);
            var after=state.Successor(state.Revision,new(world,state.Construction.Accounts),state.ConstructionMode);
            MixedWire.ValidateTransition(state,after);mixedState=after;
            fleetLeases[train.Id]=lease! with {ControllerId=target.Id,ControllerRevision=target.Revision,
                Expires=boundaryTime+ControlLeaseDuration,Input=default,InputAt=null};
            return null;
        }
        catch(Exception e) when(e is ArgumentException or InvalidOperationException or OverflowException or InvalidDataException){return "invalidInput";}
    }
    private FleetControlRead[] ReadFleetControl() => fleetLeases.OrderBy(p=>p.Key)
        .Select(p=>new FleetControlRead(p.Key,new(p.Value.Participant.ParticipantId,p.Value.Id,p.Value.Expires.ToUnixTimeMilliseconds()))).ToArray();
}
