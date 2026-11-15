using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Session;

public sealed partial class SessionHost
{
    private MixedSessionState FixedCityState(FixedCityQuery q)
    {
        if(q.Position == null || q.ExpectedWorldEpoch<1 || q.ExpectedGeometryRevision<0 || q.ExpectedMinute<0 ||
            q.OwnerId<0 || q.ExpectedOwnerRevision<0 || q.Action is not ("place" or "remove") ||
            (q.Action=="place" ? q.Definition==null || q.OwnerId!=0 || q.ExpectedOwnerRevision!=0 : q.Definition!=null || q.OwnerId==0))
            throw new ArgumentException("invalidInput");
        var state=RailwayState(q.ExpectedWorldEpoch,q.ExpectedGeometryRevision,q.ExpectedContentVersion);
        ValidateQuoteMinute(q.ExpectedMinute);
        return CityHeat.Enable(state);
    }
    private static FixedCityPlan PlanFixedCity(MixedSessionState s,FixedCityQuery q)
    {
        var p=new Location(q.Position.X,q.Position.Y,q.Position.Z);
        var capture=new CityCandidate(s.Construction,s.Clock.Capture(),s.Random.Capture());
        if(q.Action=="remove")
        {
            var owner=s.World.City!.State.Structures.SingleOrDefault(o=>o.Id==q.OwnerId);
            if(owner==null || owner.Revision!=q.ExpectedOwnerRevision) throw new InvalidOperationException("staleRevision");
            return FixedCityConstruction.PreviewRemoval(capture,p,q.OwnerId,q.ExpectedOwnerRevision);
        }
        var d=q.Definition!;
        return FixedCityConstruction.Preview(capture,p,new(d.PackId,d.ContributionId,d.RecipeId));
    }
    private static long FixedCityCost(FixedCityPlan plan) => plan.Candidate.Construction.Accounts.Transactions.Skip(plan.Source.Construction.Accounts.Transactions.Length).Sum(t=>t.Expenditure);
    public FixedCityPreviewRead PreviewFixedCity(FixedCityQuery q)
    {
        lock(gate)
        {
            return BuildQuote<FixedCityQuery, FixedCityPreviewRead>(q, () =>
            {
                var state=FixedCityState(q);
                try
                {
                    var plan=PlanFixedCity(state,q);
                    var after=FixedCityConstruction.Commit(state,plan);
                    MixedWire.ValidateTransition(state,after);
                    var owner=q.Action=="remove" ? state.World.City!.Structure(q.OwnerId) : after.World.City!.State.Structures.Last();
                    var d=state.World.City!.Profile.Structure(owner.Definition);
                    var positions=new Cube(owner.Location,d.SizeX,d.SizeY,owner.Height).Locations().Select(p=>new PositionRead(p.X,p.Y,p.Z)).ToArray();
                    return new(q,true,null,FixedCityCost(plan),positions,[]);
                }
                catch(Exception ex) when(ex is ArgumentException or OverflowException or InvalidDataException)
                { return new(q,false,"invalidPlacement",0,[],[]); }
            }, q.ExpectedMinute);
        }
    }
    private string? CommitFixedCity(EditFixedCityCommand command)
    {
        try
        {
            using var review = ReviewQuote(command.Query, () => PreviewFixedCity(command.Query));
            var state=FixedCityState(command.Query);
            var plan=PlanFixedCity(state,command.Query);
            if(FixedCityCost(plan)!=command.ExpectedCost) return "staleRevision";
            var after=FixedCityConstruction.Commit(state,plan);
            MixedWire.ValidateTransition(state,after);
            mixedState=after;terrainState=null;
            ConsumeQuote(command.Query); return null;
        }
        catch(InvalidOperationException ex) when(ex.Message is "staleEpoch" or "staleRevision" or "operationUnavailable") {return ex.Message;}
        catch(Exception ex) when(ex is ArgumentException or OverflowException or InvalidDataException or InvalidOperationException) {return "invalidPlacement";}
    }
}
