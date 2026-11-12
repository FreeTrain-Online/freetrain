using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Session;

public sealed partial class SessionHost
{
    private MixedSessionState CityState(CityQuery query)
    {
        if(query.ExpectedWorldEpoch<1 || query.ExpectedRevision<0 || query.ExpectedMinute<0 || query.OwnerId<1 || query.ExpectedOwnerRevision<0)
            throw new ArgumentException("invalidInput");
        if(query.ExpectedWorldEpoch!=WorldEpoch) throw new InvalidOperationException("staleEpoch");
        var state=mixedState ?? throw new InvalidOperationException("operationUnavailable");
        if(state.World.City==null) throw new InvalidOperationException("operationUnavailable");
        if(query.ExpectedContentVersion!=ProtocolIdentity.MixedContent) throw new ArgumentException("invalidInput");
        if(state.Revision!=query.ExpectedRevision || state.Clock.Time.AbsoluteMinute!=query.ExpectedMinute)
            throw new InvalidOperationException("staleRevision");
        var owner=state.World.City.State.Structures.SingleOrDefault(s=>s.Id==query.OwnerId);
        if(owner==null || owner.Revision!=query.ExpectedOwnerRevision) throw new InvalidOperationException("staleRevision");
        return state;
    }
    // Read-only inspection/preview. Family placement and completed demolition are
    // admitted by their later source-specific lifecycle slices.
    public CityPreviewRead PreviewCity(CityQuery query)
    {
        lock(gate)
        {
            var state=CityState(query);
            var structure=state.World.City!.Structure(query.OwnerId);
            var canCancel=!structure.Construction.IsEmpty;
            if(canCancel) MixedWire.ValidateTransition(state,CancelCityCandidate(state,query.OwnerId));
            return new(query,canCancel,canCancel ? null : "operationUnavailable",0,
                CityWire.Read(state.World.City)!.Structures.Single(s=>s.Id==query.OwnerId));
        }
    }
    private static MixedSessionState CancelCityCandidate(MixedSessionState state,long ownerId)
    {
        var after=CityConstruction.Remove(new(state.Construction,state.Clock.Capture(),state.Random.Capture()),ownerId);
        return state.Successor(state.Revision,after.Construction,state.ConstructionMode,after.Random,after.Clock);
    }
    private string? CancelCityConstruction(CancelCityConstructionCommand command)
    {
        try
        {
            var before=CityState(command.Query);
            if(before.World.City!.Structure(command.Query.OwnerId).Construction.IsEmpty) return "operationUnavailable";
            var after=CancelCityCandidate(before,command.Query.OwnerId);
            MixedWire.ValidateTransition(before,after);
            mixedState=after;
            return null;
        }
        catch(InvalidOperationException ex) when(ex.Message is "staleEpoch" or "staleRevision" or "operationUnavailable") {return ex.Message;}
        catch(Exception ex) when(ex is ArgumentException or OverflowException or InvalidDataException or InvalidOperationException) {return "invalidInput";}
    }
}
