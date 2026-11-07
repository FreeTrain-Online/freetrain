using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Session;

public sealed partial class SessionHost
{
    private string? ConfigureFleetColor(ConfigureFleetColorCommand command)
    {
        try {
            var state=mixedState;
            if(state==null && terrainState is {} terrain)
                state=RailwayState(WorldEpoch,terrain.World.Revision,TerrainProfile.ContentVersion);
            if(state==null)return "operationUnavailable";
            var next=state.World.Spatial.NextEntityId;
            var fleet=state.Fleet;
            if(fleet==null) {
                if(state.World.Profile.FleetProfile is not {} profile)return "operationUnavailable";
                fleet=FleetGraph.Empty(profile,next);next=checked(next+3);
            }
            if((fleet.ColorTest?.Revision??0)!=command.ExpectedRevision)return "staleRevision";
            var revision=fleet.ColorTest==null?1:checked(fleet.ColorTest.Revision+1);
            if(command.Configuration.Revision!=revision)return "invalidInput";
            var graph=new FleetGraph(fleet.Profile,fleet.RootGroupId,fleet.Trains,fleet.Groups,fleet.Controllers,FleetWire.Color(command.Configuration));
            return PublishControllers(state,graph,next);
        } catch(Exception e) when(e is ArgumentException or InvalidOperationException or OverflowException or InvalidDataException){return "invalidInput";}
    }
}
