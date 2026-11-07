using FreeTrain.Online.Contracts;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Session;

public sealed partial class SessionHost
{
    private string? ManageFleetGroup(ManageFleetGroupCommand command)
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
            var groups=f.Groups;var trains=f.Trains;
            if(command.Name==null || command.TargetKind is not ("group" or "train"))return "invalidInput";
            var target=groups.SingleOrDefault(g=>g.Id==command.TargetId);
            if(command.Action=="rename")
            {
                if(command.TargetKind!="group" || command.ParentId!=null || command.ExpectedParentRevision!=null)return "invalidInput";
                if(target==null || target.Revision!=command.ExpectedRevision)return "staleRevision";
                groups=groups.Replace(target,target with {Name=command.Name.Trim(),Revision=checked(target.Revision+1)});
            }
            else if(command.Action is "create" or "move")
            {
                // The first group can be created before any train/controller exists.
                // Parent zero selects the freshly initialized root only in that case.
                var parent=groups.SingleOrDefault(g=>g.Id==command.ParentId || state.Fleet==null && command.ParentId==0 && g.Id==f.RootGroupId);
                if(parent==null || parent.Revision!=command.ExpectedParentRevision)return "staleRevision";
                if(command.Action=="create")
                {
                    if(command.TargetKind!="group" || command.TargetId!=0 || command.ExpectedRevision!=0)return "invalidInput";
                    // Source's interactive constructor assigns registered delegation.
                    var delegation=f.Controllers.Single(c=>c.Kind==FleetControllerKind.Delegation);
                    groups=groups.Add(new(nextId,0,command.Name.Trim(),parent.Id,delegation.Id,[]));
                    groups=groups.Replace(parent,parent with {Members=parent.Members.Add(nextId),Revision=state.Fleet==null?0:checked(parent.Revision+1)});
                    nextId=checked(nextId+1);
                }
                else
                {
                    if(command.Name!="")return "invalidInput";
                    long previousId;
                    if(command.TargetKind=="group")
                    {
                        if(target==null || target.Revision!=command.ExpectedRevision)return "staleRevision";
                        if(target.Id==f.RootGroupId || target.Id==parent.Id)return "invalidInput";
                        // Validate ancestry before changing reciprocal membership.
                        for(FleetGroup? ancestor=parent;ancestor!=null;ancestor=ancestor.ParentId is {} p?groups.Single(g=>g.Id==p):null)
                            if(ancestor.Id==target.Id)return "invalidInput";
                        previousId=target.ParentId!.Value;
                        groups=groups.Replace(target,target with {ParentId=parent.Id,Revision=checked(target.Revision+1)});
                    }
                    else
                    {
                        var train=trains.SingleOrDefault(t=>t.Id==command.TargetId);
                        if(train==null || train.Revision!=command.ExpectedRevision)return "staleRevision";
                        previousId=train.GroupId;
                        trains=trains.Replace(train,train with {GroupId=parent.Id,Revision=checked(train.Revision+1)});
                    }
                    if(previousId==parent.Id)return "invalidInput";
                    var previous=groups.Single(g=>g.Id==previousId);
                    groups=groups.Replace(previous,previous with {Members=previous.Members.Remove(command.TargetId),Revision=checked(previous.Revision+1)});
                    groups=groups.Replace(parent,parent with {Members=parent.Members.Add(command.TargetId),Revision=checked(parent.Revision+1)});
                }
            }
            else return "invalidInput"; // Source groups have no deletion action.
            return PublishControllers(state,new(f.Profile,f.RootGroupId,trains,groups,f.Controllers,f.ColorTest),nextId);
        }
        catch(Exception e) when(e is ArgumentException or InvalidOperationException or OverflowException or InvalidDataException){return "invalidInput";}
    }
}
