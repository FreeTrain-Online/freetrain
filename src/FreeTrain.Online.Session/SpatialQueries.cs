using FreeTrain.Online.Contracts;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Session;

// Session-independent calculation; the caller captures map/epoch/revision under its world lock.
public static class SpatialQueries
{
    public static SpatialPreviewRead Preview(SnapshotMessage snapshot, SpatialQuery query, WorldProfile? selectedProfile = null)
    {
        if (query.ExpectedWorldEpoch < 0) throw new ArgumentException("invalidInput");
        if (query.ExpectedWorldEpoch != snapshot.WorldEpoch) throw new InvalidOperationException("staleEpoch");
        if (query.Position == null || query.Snap is not (0 or 4 or 8) || query.Snap != 0 && query.Anchor == null)
            throw new ArgumentException("invalidInput");
        var point = new Location(query.Position.X, query.Position.Y, query.Position.Z);
        Location? anchor = query.Anchor is {} a ? new(a.X, a.Y, a.Z) : null;
        if (anchor is {} origin && point.Z != origin.Z) throw new ArgumentException("invalidInput");
        if (anchor is {} from) point = query.Snap switch { 4 => point.Align4To(from), 8 => point.Align8To(from), _ => point };
        var profile = selectedProfile ?? WorldProfiles.Resolve(snapshot.Ruleset, snapshot.ContentVersion, snapshot.RequiredPacks)
            ?? throw new ArgumentException("incompatible");
        var map = snapshot.Map;
        if (map.CoordinateSystem != profile.CoordinateSystem) throw new ArgumentException("incompatible");
        var space = new WorldSpace(map.Width, map.Height, map.Levels);
        bool inside;
        ProjectedPoint projected;
        StoragePositionRead? storage = null;
        if (map.CoordinateSystem == "hv")
        {
            inside = space.ContainsVoxel(point);
            projected = space.Project(point);
            var cell = space.ToStorage(point);
            storage = new(cell.H, cell.V, cell.Z);
        }
        else if (map.CoordinateSystem == "prototypeXY")
        {
            // Representation boundary only. Never reinterpret the old X/Y rectangle as H/V.
            inside = point.X >= 0 && point.X < map.Width && point.Y >= 0 && point.Y < map.Height && point.Z >= 0 && point.Z < map.Levels;
            projected = new WorldSpace(1, 1, 1).Project(point); // obsolete view origin is zero
        }
        else throw new ArgumentException("invalidInput");
        int? heading = anchor is {} start && point != start ? start.DirectionTo(point).Index : null;
        int? distance = anchor is {} source ? source.DistanceTo(point) : null;
        return new(snapshot.WorldEpoch, snapshot.Revision, snapshot.Ruleset, snapshot.ContentVersion,
            snapshot.RequiredPacks, new(point.X, point.Y, point.Z), inside, new(projected.A, projected.B), storage, heading, distance);
    }
}
