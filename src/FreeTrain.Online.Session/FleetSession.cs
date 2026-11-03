using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Session;

public sealed partial class SessionHost
{
    private MixedSessionState FleetState(FleetTradeQuery query)
    {
        if(query.ExpectedWorldEpoch!=WorldEpoch)throw new InvalidOperationException("staleEpoch");
        if(query.ExpectedRevision!=CanonicalRevision || query.ExpectedMinute!=CanonicalMinute)
            throw new InvalidOperationException("staleRevision");
        return RailwayState(query.ExpectedWorldEpoch,mixedState?.World.Revision??terrainState?.World.Revision??-1,query.ExpectedContentVersion);
    }
    private static FleetTradePlan PlanTrade(MixedSessionState state,FleetTradeQuery query)
    {
        if(query.Action=="buy" && query.Definition is {} d && query.TrainId==0)
            return FleetTrading.QuotePurchase(state,new(d.PackId,d.Version,d.CatalogHash,d.ContributionId),query.Length,query.Quantity);
        if(query.Action=="sell" && query.Definition==null && query.Length==0 && query.Quantity==0 &&
            state.Fleet?.Train(query.TrainId).State==TrainOperationalState.Unplaced)
            return FleetTrading.QuoteSale(state,query.TrainId);
        throw new ArgumentException("invalidInput");
    }
    public FleetTradePreviewRead PreviewFleetTrade(FleetTradeQuery query)
    {
        lock(gate)
        {
            var state=FleetState(query);var plan=PlanTrade(state,query);
            MixedWire.ValidateTransition(state,FleetTrading.Commit(state,plan));
            return new(query,plan.Price,plan.NormalCapacity,plan.SeatedCapacity,plan.PackedCapacity);
        }
    }
    private string? CommitFleetTrade(TradeFleetCommand command)
    {
        try
        {
            var state=FleetState(command.Query);var plan=PlanTrade(state,command.Query);
            if(plan.Price!=command.ExpectedPrice)return "staleRevision";
            var after=FleetTrading.Commit(state,plan);MixedWire.ValidateTransition(state,after);
            mixedState=after;terrainState=null;return null;
        }
        catch(InvalidOperationException e) when(e.Message is "staleEpoch" or "staleRevision" or "operationUnavailable"){return e.Message;}
        catch(Exception e) when(e is ArgumentException or OverflowException or InvalidOperationException or InvalidDataException){return "invalidInput";}
    }
    private string? RenameFleet(RenameFleetCommand command)
    {
        try
        {
            var state=mixedState;if(state?.Fleet is not {} fleet)return "operationUnavailable";
            var train=fleet.Train(command.TrainId);
            if(train.Revision!=command.ExpectedRevision)return "staleRevision";
            var name=command.Name.Trim();
            if(string.IsNullOrWhiteSpace(name)||name.Length>256||name.Any(char.IsControl))return "invalidInput";
            var graph=new FleetGraph(fleet.Profile,fleet.RootGroupId,fleet.Trains.Select(t=>t.Id==train.Id
                ? t with {Name=name,Revision=checked(t.Revision+1)}:t),fleet.Groups,fleet.Controllers);
            var w=state.World;var world=w.Replace(w.Revision,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,w.SpeedLimits,graph);
            var after=state.Successor(state.Revision,new(world,state.Construction.Accounts),state.ConstructionMode);
            MixedWire.ValidateTransition(state,after);mixedState=after;return null;
        }
        catch(Exception e) when(e is ArgumentException or OverflowException or InvalidOperationException or InvalidDataException){return "invalidInput";}
    }
}
