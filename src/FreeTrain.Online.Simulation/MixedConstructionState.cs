namespace FreeTrain.Online.Simulation;

// Detached publication unit for P4 tools. P4-05 owns session FIFO, clock/random,
// mode, authorization, save/wire and dispatch. A caller publishes only this result.
public sealed class MixedConstructionState
{
    public MixedWorld World { get; }
    public ConstructionAccounts Accounts { get; }
    public MixedConstructionState(MixedWorld world, ConstructionAccounts? accounts = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        Accounts = accounts ?? new(); World = world;
        if (!Accounts.Transactions.IsEmpty && Accounts.Transactions[^1].WorldRevision > world.Revision)
            throw new ArgumentException("Booking is newer than geometry.");
    }

    // Candidate geometry and every ordered spend validate before anything can be
    // published. Rejection preserves terrain, railway, allocator AND accounts.
    // No construction-mode argument: authoring does not waive source charges.
    public MixedConstructionState Replace(long expectedRevision, TerrainWorld terrain, SpatialState spatial,
        IEnumerable<RailwayVoxel> voxels, IEnumerable<SlopeOwnership> slopes,
        ConstructionBooking booking, GameTime time, string operation, SpeedLimits? speedLimits = null, FleetGraph? fleet = null,
        StationGraph? stations = null)
    {
        var candidate = World.Replace(expectedRevision,terrain,spatial,voxels,slopes,speedLimits,fleet,stations);
        var accounts = Accounts.Book(booking,candidate.Revision,time,operation);
        return new(candidate,accounts);
    }
}
