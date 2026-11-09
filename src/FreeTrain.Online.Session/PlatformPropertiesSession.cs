using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Session;

public sealed partial class SessionHost
{
    private MixedSessionState PlatformPropertiesState(PlatformPropertiesQuery query)
    {
        if (query.ExpectedWorldEpoch < 0 || query.ExpectedGeometryRevision < 0 || query.PlatformId <= 0 || query.ExpectedPlatformRevision < 0)
            throw new ArgumentException("invalidInput");
        var state = RailwayState(query.ExpectedWorldEpoch,query.ExpectedGeometryRevision,query.ExpectedContentVersion);
        if (state.World.Stations?.Platforms.SingleOrDefault(p=>p.Id==query.PlatformId) is not {} platform ||
            platform.Revision != query.ExpectedPlatformRevision) throw new InvalidOperationException("staleRevision");
        return state;
    }
    public PlatformPropertiesRead PlatformProperties(PlatformPropertiesQuery query)
    {
        lock (gate)
        {
            var state = PlatformPropertiesState(query);
            static PositionRead P(Location p) => new(p.X,p.Y,p.Z);
            return new(query,StationHostConnections.Candidates(state.World,state.World.Stations!.Platform(query.PlatformId))
                .Select(c=>new PlatformHostCandidateRead(c.Id,c.Revision,P(c.Location),c.IsStation,c.StationId,c.StationRevision,P(c.StationLocation))).ToArray());
        }
    }
    private string? CommitPlatformProperties(EditPlatformPropertiesCommand command)
    {
        try
        {
            var state = PlatformPropertiesState(command.Query);
            if (command.Name == null || command.Bell == null) return "invalidInput";
            var after = FreeTrain.Online.Simulation.PlatformProperties.Apply(state,command.Query.PlatformId,command.Query.ExpectedPlatformRevision,
                command.Name,new(command.Bell.PackId,command.Bell.ContributionId,command.Bell.RecipeId),
                command.HostId,command.ExpectedHostRevision,command.ExpectedStationRevision);
            MixedWire.ValidateTransition(state,after);
            mixedState = after;
            return null;
        }
        catch (InvalidOperationException ex) when (ex.Message is "staleEpoch" or "staleRevision" or "operationUnavailable") { return ex.Message; }
        catch (Exception ex) when (ex is ArgumentException or OverflowException or InvalidDataException or InvalidOperationException) { return "invalidInput"; }
    }
}
