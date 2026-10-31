using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Session;

public sealed partial class SessionHost
{
    private MixedSessionState SlopeState(SlopeQuery q)
    {
        if (q.Position == null || q.Climb is not (0 or 2 or 4 or 6) || q.Action is not ("lay" or "remove"))
            throw new ArgumentException("invalidInput");
        return RailwayState(q.ExpectedWorldEpoch,q.ExpectedGeometryRevision,q.ExpectedContentVersion);
    }
    private static SlopeRailwayConstructionPlan PlanSlope(MixedSessionState s, SlopeQuery q) => q.Action == "lay"
        ? SlopeRailwayConstruction.PreviewBuild(s.World,new(q.Position.X,q.Position.Y,q.Position.Z),new(q.Climb))
        : SlopeRailwayConstruction.PreviewRemoval(s.World,new(q.Position.X,q.Position.Y,q.Position.Z),new(q.Climb));

    public SlopePreviewRead PreviewSlope(SlopeQuery query)
    {
        lock (gate)
        {
            var state=SlopeState(query); var plan=PlanSlope(state,query);
            var reason=plan.Reason ?? (state.Revision == long.MaxValue || state.World.Revision == long.MaxValue
                ? "constructionOverflow" : state.Construction.Accounts.Transactions.Length >= NormalRailwayConstruction.MaximumAccountTransactions
                ? "accountCapacityLimit" : null);
            static PositionRead P(Location p) => new(p.X,p.Y,p.Z);
            var members=plan.Members.OrderBy(v=>v.Position.X).ThenBy(v=>v.Position.Y).ThenBy(v=>v.Position.Z).Select(v=>new SlopeMemberRead(P(v.Position),v.Kind.ToString().ToLowerInvariant(),
                v.Traffic?.Rail?.Pattern,v.DrawGroundBelow,v.DrawGroundAbove)).ToArray();
            // Even a rejected build shows its complete intended occupied footprint.
            // No candidate IDs or client-supplied prices enter the command path.
            if (members.Length == 0 && query.Action == "lay")
            {
                try
                {
                    var (dx,dy)=plan.Climb.Offset;
                    members=Enumerable.Range(0,4).SelectMany(i=>new[]{
                        new SlopeMemberRead(new(checked(plan.Base.X+i*dx),checked(plan.Base.Y+i*dy),checked(plan.Base.Z+i/2)),"traffic",null,true,true),
                        new SlopeMemberRead(new(checked(plan.Base.X+i*dx),checked(plan.Base.Y+i*dy),checked(plan.Base.Z+1-i/2)),i<2?"slopefiller":"slopesupport",null,true,true)
                    }).ToArray();
                }
                catch (OverflowException) { members=[]; }
            }
            return new(query,reason == null,reason,reason == null?plan.Booking.Total:0,P(plan.Base),plan.Climb.Index,members,
                plan.Rails.Select(r=>new SlopeRailPositionRead(r.Level,P(r.Position),r.Pattern,r.ZDiff,r.ZAngle.Index)).ToArray(),
                plan.ElectedPiers.Select(p=>P(p.Position)).ToArray(),
                plan.RemovedPiers.Select(v=>new RailwayLossRead("pier",P(v.Position),v.OwnerId,v.SupportedOwnerId)).ToArray());
        }
    }
    private string? CommitSlope(SlopeQuery query)
    {
        try
        {
            var before=SlopeState(query); var plan=PlanSlope(before,query);
            if (!plan.Legal) return "invalidPlacement";
            var construction=SlopeRailwayConstruction.Apply(before.Construction,plan,before.Clock.Time);
            var after=before.Successor(before.Revision,construction,before.ConstructionMode);
            MixedWire.ValidateTransition(before,after);
            mixedState=after; terrainState=null;
            return null;
        }
        catch (InvalidOperationException ex) when (ex.Message is "staleEpoch" or "staleRevision" or "operationUnavailable") { return ex.Message; }
        catch (Exception ex) when (ex is ArgumentException or OverflowException or InvalidDataException or InvalidOperationException)
        { return "invalidPlacement"; }
    }
}
