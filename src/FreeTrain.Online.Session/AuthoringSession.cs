using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Session;
public sealed partial class SessionHost
{
    private string? SetAuthoringMode(ParticipantContext participant,SetAuthoringModeCommand command)
    {
        if(participant.Role!=ParticipantRole.Owner)return "notAuthorized";
        var state=terrainState;if(state==null)return "operationUnavailable";
        if(command.ExpectedGeometryRevision!=state.World.Revision)return "staleRevision";
        if(command.Enabled==state.ConstructionMode)return null;
        try
        {
            // Increment geometry even without a shape change: entry/exit fences all
            // participants' captured tool previews and in-flight confirmations.
            var candidate=new CornerTerrainDraft(state.World).Finish();
            SaveCodec.Write(new TerrainSessionState(candidate,state.Provenance,command.Enabled,
                state.Clock.Capture(),state.Paused,state.Random.Capture(),checked(state.Revision+1)));
            state.Replace(state.Revision,candidate,command.Enabled);return null;
        }
        catch(Exception ex) when(ex is ArgumentException or OverflowException or InvalidDataException){return "invalidPlacement";}
    }
    private (AuthoringTerrainPlan Plan,RandomStreamsState Random) PlanAuthoring(CornerTerrainQuery query)
    {
        var state=terrainState??throw new InvalidOperationException("operationUnavailable");
        if(query.ExpectedWorldEpoch!=WorldEpoch)throw new InvalidOperationException("staleEpoch");
        if(query.ExpectedGeometryRevision!=state.World.Revision)throw new InvalidOperationException("staleRevision");
        if(!state.ConstructionMode||state.Revision==long.MaxValue)throw new InvalidOperationException("operationUnavailable");
        if(query.Position==null||query.Rectangle?.End==null||query.Rectangle.Soft||query.OffsetX!=null||query.OffsetY!=null||query.Stroke?.Length>0)
            throw new ArgumentException("invalidInput");
        var random=RandomStreams.Restore(state.Random.Capture());
        var p=query.Position;var e=query.Rectangle.End;var f=query.Forest;
        var plan=AuthoringTerrain.Plan(state.World,new(p.X,p.Y,p.Z),new(e.X,e.Y,e.Z),query.Action,
            f==null?null:new(f.PackId,f.ContributionId,f.RecipeId),random);
        return(plan,random.Capture());
    }
    private CornerTerrainPreviewRead PreviewAuthoring(CornerTerrainQuery query)
    {
        var (plan,_)=PlanAuthoring(query);var legal=plan.Sites.Any(p=>p.Legal);
        return new(WorldEpoch,terrainState!.World.Revision,query.Position,query.Action,legal,legal?null:plan.Sites[0].Reason,0,
            plan.Sites.SelectMany(p=>p.Changes).Select(c=>new CornerTerrainChangeRead(new(c.Position.X,c.Position.Y,c.Position.Z),c.BeforeGround,c.AfterGround,
                c.Before.NE,c.Before.SE,c.Before.SW,c.Before.NW,c.After.NE,c.After.SE,c.After.SW,c.After.NW,c.Remove,c.LosesForest,c.LosesOwnership)).ToArray(),
            plan.Sites.Where(p=>!p.Legal).Select(p=>new CornerTerrainSkipRead(new(p.Position.X,p.Position.Y,p.Position.Z),p.Reason!)).ToArray(),
            plan.Decorations.Select(d=>new AuthoringDecorationRead(new(d.Position.X,d.Position.Y,d.Position.Z),d.Forest is {} f?
                new(new(f.Definition.PackId,f.Definition.ContributionId,f.Definition.RecipeId),f.Trees.Select(t=>new TerrainTreeRead(t.X,t.Y,t.Sprite)).ToArray()):null,d.CreekConnections)).ToArray());
    }
    private string? CommitAuthoring(CornerTerrainQuery query)
    {
        try
        {
            var (plan,random)=PlanAuthoring(query);if(!plan.Sites.Any(p=>p.Legal))return "invalidPlacement";
            var candidate=plan.Draft.Finish();var state=terrainState!;
            SaveCodec.Write(new TerrainSessionState(candidate,state.Provenance,state.ConstructionMode,
                state.Clock.Capture(),state.Paused,random,checked(state.Revision+1)));
            state.Replace(state.Revision,candidate,state.ConstructionMode,random);return null;
        }
        catch(InvalidOperationException ex) when(ex.Message is "staleEpoch" or "staleRevision" or "operationUnavailable"){return ex.Message;}
        catch(Exception ex) when(ex is ArgumentException or OverflowException or InvalidDataException){return "invalidPlacement";}
    }
}
