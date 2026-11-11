using System.Collections.Immutable;
using System.Text.Json;
using System.Runtime.CompilerServices;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Persistence;

public static class MixedWire
{
    private sealed record Publication(MixedRead Mixed, SpatialEntityRead[] Railway, SpatialEntityRead[] Combined);
    private static readonly ConditionalWeakTable<MixedConstructionState, Publication> publications = new();
    private static Publication Published(MixedSessionState state) => publications.GetValue(state.Construction,_ =>
    {
        var railway = RailwayEntities(state.World);
        var terrain = TerrainWire.Snapshot(state.CaptureTerrain(),1);
        return new(Read(state.Construction),railway,terrain.Entities!.Concat(railway).OrderBy(e => e.Id).ToArray());
    });
    private static PositionRead P(Location p)=>new(p.X,p.Y,p.Z);
    private static DefinitionRead? Read(DefinitionReference? d) => d == null ? null : new(d.PackId,d.ContributionId,d.RecipeId);
    private static DefinitionReference? Restore(DefinitionRead? d) => d == null ? null : new(d.PackId,d.ContributionId,d.RecipeId);
    public static MixedRead Read(MixedConstructionState state)
    {
        var w = state.World; var a = state.Accounts;
        if (w.City != null) throw new ArgumentException("Detached city state requires the P7-05 codec.");
        if (w.Voxels.Any(v => v.Traffic?.Rail?.ControllerId != null)) throw new ArgumentException("Reserved signal controller must be null (TRAIN-D010).");
        if (w.Voxels.Length > ProtocolIdentity.MaximumRailwayVoxels || a.Transactions.Length > ProtocolIdentity.MaximumAccountTransactions)
            throw new InvalidDataException("saveTooLarge");
        return new(w.Revision,w.Spatial.NextEntityId,w.Voxels.OrderBy(v => v.OwnerId).ThenBy(v => v.Position.X).ThenBy(v => v.Position.Y).ThenBy(v => v.Position.Z).Select(v =>
            new RailwayVoxelRead(new(v.Position.X,v.Position.Y,v.Position.Z),v.OwnerId,v.Kind.ToString().ToLowerInvariant(),
                v.Traffic is not {} t ? null : new(t.Rail is not {} r ? null : new(Read(r.Definition)!,r.Pattern,r.Variant,
                    r.TerrainBackup is {} b ? new(b.NE,b.SE,b.SW,b.NW) : null,r.Direction,r.ControllerId),
                    t.Road is {} road ? new(Read(road.Definition)!,road.Edges) : null,
                    t.Accessory is {} accessory ? new(Read(accessory.Definition)!,accessory.Direction) : null,
                    t.Car is {} car ? new(Read(car.Definition)!,car.TrainId,car.CarId) : null,Read(t.Platform)),
                Read(v.Definition),v.Sprite,v.DrawGroundBelow,v.DrawGroundAbove,v.SupportedOwnerId,v.Construction is {} site ? new(site.State,site.Ground,site.NeedsDoor) : null)).ToArray(),
            w.Slopes.OrderBy(s => s.EntityId).Select(s => new SlopeOwnerRead(s.EntityId,s.Climb.Index)).ToArray(),
            new(a.InitialCash,a.Settings.BankruptcyIncrement,a.Transactions.Select(t => new AccountTransactionRead(t.Sequence,
                t.WorldRevision,t.AbsoluteMinute,t.Operation,t.Genre.SourceId(),t.Expenditure,t.TopUps,t.CashAfter,t.Revenue)).ToArray()),
            w.SpeedLimits.Locations.Any() ? new(w.SpeedLimits.Low.Select(P).ToArray(),w.SpeedLimits.Medium.Select(P).ToArray(),w.SpeedLimits.Fast.Select(P).ToArray()) : null, FleetWire.Read(w.Fleet), StationGraph: StationWire.Read(w.Stations));
    }
    public static IReadOnlyDictionary<string, Action> ConstructionBindings(MixedRead data) => data.Voxels
        .Where(v => v.Construction is {State: < 4}).ToDictionary(v => StationaryConstruction.Handler(new(
            new(v.Position.X,v.Position.Y,v.Position.Z),v.OwnerId,RailwayVoxelKind.Stationary)), _ => (Action)(() => {}))
        .Concat((data.Fleet?.Trains ?? []).Where(t => t.State != "unplaced")
            .Select(t => new KeyValuePair<string, Action>(MixedSessionState.TrainHandler(t.Id), () => {})))
        .Concat(StationWire.Timers(data.StationGraph).Select(t => new KeyValuePair<string, Action>(t.HandlerId, () => {})))
        .ToDictionary();
    public static IReadOnlyDictionary<Location, QuarterCorners> TunnelBackups(MixedRead data, RailwayProfile profile) =>
        data.Voxels.Where(v => v.Traffic?.Rail is {} r && profile.Resolve(Restore(r.Definition)!).Handler == RailwayHandler.Tunnel)
            .ToDictionary(v => new Location(v.Position.X,v.Position.Y,v.Position.Z), v => {
                var b=v.Traffic!.Rail!.TerrainBackup ?? throw new ArgumentException("Missing tunnel backup.");
                return new QuarterCorners(b.NE,b.SE,b.SW,b.NW);
            });
    public static MixedConstructionState Restore(MixedRead data, RailwayProfile profile, TerrainWorld terrain, SpatialEntityRead[] railwayEntities)
    {
        if (data.Voxels.Length > ProtocolIdentity.MaximumRailwayVoxels || data.Slopes.Length > ProtocolIdentity.MaximumRailwayVoxels / 8 ||
            data.Accounts.Transactions.Length > ProtocolIdentity.MaximumAccountTransactions || railwayEntities.Length > ProtocolIdentity.MaximumRailwayVoxels)
            throw new ArgumentException("Mixed state limit.");
        if (data.Voxels.Any(v => v.Traffic?.Rail?.ControllerId != null)) throw new ArgumentException("Reserved signal controller must be null (TRAIN-D010).");
        var voxels = data.Voxels.Select(v => new RailwayVoxel(new(v.Position.X,v.Position.Y,v.Position.Z),v.OwnerId,
            Enum.GetValues<RailwayVoxelKind>().Single(k => k.ToString().ToLowerInvariant() == v.Kind),
            v.Traffic is not {} t ? null : new(t.Rail is {} r ? new(Restore(r.Definition)!,r.Pattern,r.Variant,
                r.TerrainBackup is {} b ? new QuarterCorners(b.NE,b.SE,b.SW,b.NW) : null,r.Direction,r.ControllerId) : null,
                t.Road is {} road ? new(Restore(road.Definition)!,road.Edges) : null,
                t.Accessory is {} accessory ? new(Restore(accessory.Definition)!,accessory.Direction) : null,
                t.Car is {} car ? new(Restore(car.Definition)!,car.TrainId,car.CarId) : null,Restore(t.Platform)),
            Restore(v.Definition),v.Sprite,v.DrawGroundBelow,v.DrawGroundAbove,v.SupportedOwnerId,v.Construction is {} site ? new(site.State,site.Ground,site.NeedsDoor) : null));
        static ImmutableArray<Location> Locations(PositionRead[] list) => list.Length<=ProtocolIdentity.MaximumRailwayVoxels ? list.Select(p=>new Location(p.X,p.Y,p.Z)).ToImmutableArray() : throw new ArgumentException("Speed-limit list limit.");
        var spatial = TerrainWire.Spatial(data.NextEntityId,railwayEntities);
        var world = new MixedWorld(profile,terrain,spatial with { Entities = terrain.Spatial.Entities.AddRange(spatial.Entities) },voxels,
            data.Slopes.Select(s => new SlopeOwnership(s.EntityId,new(s.Climb))),data.GeometryRevision,
            data.SpeedLimits is {} limits ? new SpeedLimits(Locations(limits.Low),Locations(limits.Medium),Locations(limits.Fast)) : null,
            FleetWire.Restore(data.Fleet, profile.FleetProfile), StationWire.Restore(data.StationGraph, profile.Stations));
        var accounts = ConstructionAccounts.Restore(new(data.Accounts.InitialCash,new(data.Accounts.BankruptcyIncrement),
            data.Accounts.Transactions.Select(t => new AccountTransaction(t.Sequence,t.WorldRevision,t.AbsoluteMinute,t.Operation,
                Enum.GetValues<AccountGenre>().Single(g => g.SourceId() == t.Genre),t.Expenditure,t.TopUps,t.CashAfter,t.Revenue)).ToImmutableArray()));
        return new(world,accounts);
    }
    public static MixedRead Read(MixedSessionState state)
    {
        var data = Published(state).Mixed;
        var clock = state.Clock.Capture();
        return data with { Queue = state.Fleet == null && state.World.Stations == null ? null : FleetWire.Queue(clock) };
    }
    public static SpatialEntityRead[] RailwayEntities(MixedWorld world)
    {
        if (world.City != null) throw new ArgumentException("Detached city state requires the P7-05 codec.");
        var terrainIds = world.Terrain.Spatial.Entities.Select(e => e.Id).ToHashSet();
        return world.Spatial.Entities.Where(e => !terrainIds.Contains(e.Id)).Select(TerrainWire.Entity).ToArray();
    }
    public static SnapshotMessage Snapshot(MixedSessionState state, long epoch)
    {
        var terrain = TerrainWire.Snapshot(state.CaptureTerrain(),epoch);
        return terrain with { Ruleset = ProtocolIdentity.MixedRuleset, ContentVersion = ProtocolIdentity.MixedContent,
            Cash = state.Construction.Accounts.Cash, Entities = Published(state).Combined, Mixed = Read(state) };
    }
    // Bound complete save/snapshot bytes; successor delta bytes are checked below.
    public static void ValidateAdmission(MixedSessionState state)
    {
        var mixed = Read(state);
        var mixedBytes = JsonSerializer.SerializeToUtf8Bytes(mixed,WireJson.CreateOptions()).LongLength;
        if (mixedBytes > 8 * 1024 * 1024)
            throw new InvalidDataException("saveTooLarge");
        if (SaveCodec.TerrainSaveUpperBound(state.CaptureTerrain()) +
            JsonSerializer.SerializeToUtf8Bytes(RailwayEntities(state.World),WireJson.CreateOptions()).LongLength +
            mixedBytes + 4096 > ProtocolIdentity.MaximumStateBytes)
            throw new InvalidDataException("saveTooLarge");
    }
    public static void ValidateTransition(MixedSessionState before, MixedSessionState after)
    {
        ValidateAdmission(after);
        var delta = StateDifference.Between(Snapshot(before,1),Snapshot(after,1));
        if (JsonSerializer.SerializeToUtf8Bytes<WireMessage>(delta,WireJson.CreateOptions()).Length > 16 * 1024 * 1024)
            throw new InvalidDataException("saveTooLarge");
    }

}
