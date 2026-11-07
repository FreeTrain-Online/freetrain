using System.Collections.Immutable;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Session;

public sealed partial class SessionHost
{
    private string? EditFleetRule(EditFleetRuleCommand command)
    {
        try {
            var state=mixedState;if(state?.Fleet is not {} f)return "operationUnavailable";
            var selected=f.Controllers.SingleOrDefault(c=>c.Id==command.ControllerId);
            if(selected==null || selected.Revision!=command.ExpectedRevision)return "staleRevision";
            if(selected.Kind!=FleetControllerKind.Timetable || command.Location==null)return "invalidInput";
            var key=new Location(command.Location.X,command.Location.Y,command.Location.Z);
            var append=command.Operation=="append";
            if(append ? command.SettingKind!="harbor" || command.Rule!=null || command.Index!=-1 || command.Offset!=0 || command.RulesBase64==null : command.RulesBase64!=null)
                return "invalidInput";
            var rule=command.Rule==null?null:FleetWire.Rule(command.Rule);
            FleetController changed;
            if(command.SettingKind=="junction") {
                if(command.ExpectedGeometryRevision!=state.World.Revision)return "staleRevision";
                if(state.World.At(key)?.Traffic?.Rail is not {} rail || state.World.Profile.Resolve(rail.Definition).Handler!=RailwayHandler.Junction)return "invalidInput";
                var rows=selected.Junctions.IsDefault?[]:selected.Junctions;
                var setting=rows.SingleOrDefault(j=>j.Location==key);if(setting==null)return "invalidInput";
                changed=selected with {Junctions=rows.Replace(setting,setting with {
                    Rules=FleetTimetable.Edit(setting.Rules,true,command.Operation,command.Index,command.Offset,rule)})};
            } else if(command.SettingKind=="harbor" && command.ExpectedGeometryRevision==null) {
                var rows=selected.Harbors.IsDefault?[]:selected.Harbors;
                var setting=rows.SingleOrDefault(h=>h.Location==key);
                if(setting?.Kind!=FleetHarborKind.Advanced)return "invalidInput";
                changed=selected with {Harbors=rows.Replace(setting,setting with {
                    Rules=append?FleetTimetable.Append(setting.Rules,command.RulesBase64!):FleetTimetable.Edit(setting.Rules,false,command.Operation,command.Index,command.Offset,rule)})};
            } else return "invalidInput";
            changed=changed with {Revision=checked(selected.Revision+1)};
            return PublishControllers(state,new(f.Profile,f.RootGroupId,f.Trains,f.Groups,f.Controllers.Replace(selected,changed),f.ColorTest),state.World.Spatial.NextEntityId);
        } catch(Exception e) when(e is ArgumentException or InvalidOperationException or OverflowException or InvalidDataException or FormatException){return "invalidInput";}
    }
    // All controller edits enter the same FIFO/receipt/epoch boundary as train
    // commands. Drafts carry member revisions, independent of the moving clock.
    private string? PublishControllers(MixedSessionState state, FleetGraph graph, long nextId)
    {
        var w=state.World;
        var world=w.Replace(w.Revision,w.Terrain,new(nextId,w.Spatial.Entities),w.Voxels,w.Slopes,w.SpeedLimits,graph);
        var successor=state.Successor(state.Revision,new(world,state.Construction.Accounts),state.ConstructionMode);
        MixedWire.ValidateTransition(state,successor);
        mixedState=successor;terrainState=null;
        return null;
    }
    private string? ManageFleetController(ManageFleetControllerCommand command)
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
            var junctionEdit=command.Action=="cleanJunctions" || command.Action=="configure" && command.Junctions!=null;
            if(junctionEdit) {
                if(command.ExpectedGeometryRevision!=state.World.Revision)return "staleRevision";
            } else if(command.Junctions!=null || command.ExpectedGeometryRevision!=null)return "invalidInput";
            bool IsJunction(Location p)=>state.World.At(p)?.Traffic?.Rail is {} r &&
                state.World.Profile.Resolve(r.Definition).Handler==RailwayHandler.Junction;
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
                            Junctions=(selected.Junctions.IsDefault?[]:selected.Junctions).Select(j=>j with {Rules=j.Rules.IsDefault?default:j.Rules.Select(r=>r with {}).ToImmutableArray()}).ToImmutableArray(),
                            Harbors=(selected.Harbors.IsDefault?[]:selected.Harbors).Select(h=>h with {Rules=h.Rules.IsDefault?default:h.Rules.Select(r=>r with {}).ToImmutableArray()}).ToImmutableArray()});
                        nextId=checked(nextId+1);break;
                    case "cleanJunctions" when command.Name=="" && command.Harbors==null && command.Junctions==null && command.Location==null && selected.Kind==FleetControllerKind.Timetable:
                        rows=rows.Replace(selected,selected with {
                            Junctions=(selected.Junctions.IsDefault?[]:selected.Junctions).Where(j=>IsJunction(j.Location)).ToImmutableArray(),
                            Revision=checked(selected.Revision+1)});break;
                    case "configure" when command.Name=="" && command.Harbors==null && command.Junctions is {} junctionSettings && command.Location is {} junctionLocation && selected.Kind==FleetControllerKind.Timetable:
                        if(junctionSettings.Length>1 || junctionSettings.Any(j=>j==null || j.Location!=junctionLocation || j.Rules!=null || j.DefaultRoute is not ("straight" or "curve")))return "invalidInput";
                        var junctionKey=new Location(junctionLocation.X,junctionLocation.Y,junctionLocation.Z);
                        var junctions=selected.Junctions.IsDefault?ImmutableArray<FleetJunctionSetting>.Empty:selected.Junctions;
                        var oldJunction=junctions.SingleOrDefault(j=>j.Location==junctionKey);
                        if(junctionSettings.Length==0) {if(oldJunction!=null)junctions=junctions.Remove(oldJunction);}
                        else {
                            if(!IsJunction(junctionKey))return "invalidInput";
                            var replacement=new FleetJunctionSetting(junctionKey,junctionSettings[0].DefaultRoute=="curve"?FleetJunctionRoute.Curve:FleetJunctionRoute.Straight,oldJunction?.Rules??default);
                            junctions=oldJunction==null?junctions.Add(replacement):junctions.Replace(oldJunction,replacement);
                        }
                        rows=rows.Replace(selected,selected with {Junctions=junctions,Revision=checked(selected.Revision+1)});break;
                    case "configure" when command.Name=="" && command.Junctions==null && command.Harbors is {} settings && command.Location is {} location && selected.Kind==FleetControllerKind.Timetable:
                        // One location per command keeps edits below the existing
                        // 16 KiB network boundary even at 8192 stored settings.
                        if(settings.Length>1 || settings.Any(h=>h==null || h.Location!=location || h.Rules!=null || h.Kind is not ("pass" or "fixed" or "daily" or "advanced")))return "invalidInput";
                        var key=new Location(location.X,location.Y,location.Z);
                        var harbors=selected.Harbors.IsDefault?ImmutableArray<FleetHarborSetting>.Empty:selected.Harbors;
                        var prior=harbors.SingleOrDefault(h=>h.Location==key);
                        if(settings.Length==0){if(prior!=null)harbors=harbors.Remove(prior);}
                        else {
                            var h=settings[0];var replacement=new FleetHarborSetting(key,
                                h.Kind switch {"pass"=>FleetHarborKind.Pass,"fixed"=>FleetHarborKind.Fixed,"daily"=>FleetHarborKind.Daily,_=>FleetHarborKind.Advanced},h.Minutes,h.Reverse,
                                h.Kind=="advanced"?prior?.Rules??default:default);
                            harbors=prior==null?harbors.Add(replacement):harbors.Replace(prior,replacement);
                        }
                        rows=rows.Replace(selected,selected with {Harbors=harbors,Revision=checked(selected.Revision+1)});break;
                    case "delete" when command.Name=="" && command.Harbors==null:
                        if(f.Trains.Any(t=>t.ControllerId==selected.Id)||f.Groups.Any(g=>g.ControllerId==selected.Id))return "controllerReferenced";
                        rows=rows.Remove(selected);break;
                    default:return "invalidInput";
                }
            }
            return PublishControllers(state,new(f.Profile,f.RootGroupId,f.Trains,f.Groups,rows,f.ColorTest),nextId);
        }
        catch(Exception e) when(e is ArgumentException or InvalidOperationException or OverflowException or InvalidDataException){return "invalidInput";}
    }
    private string? AssignFleetController(AssignFleetControllerCommand command)
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
                trains=trains.Replace(train,train with {ControllerId=controller.Id,Revision=checked(train.Revision+1)});
            }
            else if(command.TargetKind=="group")
            {
                var group=f.Groups.SingleOrDefault(g=>g.Id==command.TargetId);
                if(group==null||group.Revision!=command.ExpectedRevision)return "staleRevision";
                groups=groups.Replace(group,group with {ControllerId=controller.Id,Revision=checked(group.Revision+1)});
            }
            else return "invalidInput";
            var error=PublishControllers(state,new(f.Profile,f.RootGroupId,trains,groups,f.Controllers,f.ColorTest),state.World.Spatial.NextEntityId);
            return error;
        }
        catch(Exception e) when(e is ArgumentException or InvalidOperationException or OverflowException or InvalidDataException){return "invalidInput";}
    }
}
