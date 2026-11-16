using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
namespace FreeTrain.Online.Session;

public sealed partial class SessionHost
{
    private MixedSessionState LandState(LandQuery q)
    {
        if(q.Anchor==null || q.End==null || q.ExpectedWorldEpoch<1 || q.ExpectedGeometryRevision<0 || q.ExpectedMinute<0 ||
            q.Action is not ("buy" or "sell" or "inspect" or "cover" or "removeCover") ||
            (q.Action=="cover" ? q.Definition==null : q.Definition!=null)) throw new ArgumentException("invalidInput");
        var state=RailwayState(q.ExpectedWorldEpoch,q.ExpectedGeometryRevision,q.ExpectedContentVersion);
        ValidateQuoteMinute(q.ExpectedMinute);
        return CityHeat.Enable(state); // private candidate only; inspection never activates shared state
    }
    private static LandTradePlan PlanLand(MixedSessionState state,LandQuery q) => q.Action is "cover" or "removeCover" ? StaticGroundConstruction.Preview(
        new(state.Construction,state.Clock.Capture(),state.Random.Capture()),new(q.Anchor.X,q.Anchor.Y,q.Anchor.Z),new(q.End.X,q.End.Y,q.End.Z),q.Action,
        q.Definition is {} d ? new(d.PackId,d.ContributionId,d.RecipeId):null) : LandTrading.Preview(
        new(state.Construction,state.Clock.Capture(),state.Random.Capture()),new(q.Anchor.X,q.Anchor.Y,q.Anchor.Z),new(q.End.X,q.End.Y,q.End.Z),q.Action);
    public LandPreviewRead PreviewLand(LandQuery q)
    {
        lock(gate)
        {
            return BuildQuote<LandQuery,LandPreviewRead>(q,()=>
            {
                var state=LandState(q);
                try
                {
                    var plan=PlanLand(state,q);
                    if(q.Action=="cover" || q.Action!="inspect" && plan.Cells.Any(c=>c.Status is "buyable" or "sellable" or "placeable" or "removable")) MixedWire.ValidateTransition(state,LandTrading.Commit(state,plan));
                    var legal=q.Action is "inspect" or "cover" || plan.Cells.Any(c=>c.Status is "buyable" or "sellable" or "placeable" or "removable");
                    PositionRead Position(Location p)=>new(p.X,p.Y,p.Z);
                    return new(q,legal,legal?null:"noEligibleCells",plan.Amount,
                        plan.Cells.Select(c=>new LandCellRead(Position(c.Position),c.Status,c.Value,c.OwnerId,c.Definition is {} d?new(d.PackId,d.ContributionId,d.RecipeId):null)).ToArray(),
                        plan.Reclaimed.Select(r=>new LandReclamationRead(r.Owner.Id,Position(r.Owner.Anchor),r.Owner.Value,r.RemovalCost)).ToArray(),
                        plan.Cells.Select(c=>c.Position).Concat(plan.Reclaimed.SelectMany(r=>r.Owner.Footprint.At(r.Owner.Anchor).Select(c=>c.Position)))
                            .Distinct().Select(Position).ToArray());
                }
                catch(Exception ex) when(ex is ArgumentException or OverflowException or InvalidDataException)
                {return new(q,false,"invalidPlacement",0,[],[],[]);}
            },q.ExpectedMinute);
        }
    }
    private string? CommitLand(TradeLandCommand command)
    {
        try
        {
            if(command.Query.Action=="inspect") return "invalidInput";
            using var review=ReviewQuote(command.Query,()=>PreviewLand(command.Query));
            var state=LandState(command.Query);var plan=PlanLand(state,command.Query);
            if(command.Query.Action!="cover" && !plan.Cells.Any(c=>c.Status is "buyable" or "sellable" or "placeable" or "removable")) return "invalidPlacement";
            if(plan.Amount!=command.ExpectedAmount) return "staleRevision";
            var after=LandTrading.Commit(state,plan);MixedWire.ValidateTransition(state,after);
            mixedState=after;terrainState=null;ConsumeQuote(command.Query);return null;
        }
        catch(InvalidOperationException ex) when(ex.Message is "staleEpoch" or "staleRevision" or "operationUnavailable") {return ex.Message;}
        catch(Exception ex) when(ex is ArgumentException or OverflowException or InvalidDataException or InvalidOperationException) {return "invalidPlacement";}
    }
}
