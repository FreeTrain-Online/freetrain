using System.Text.Json;
using FreeTrain.Online.Contracts;

namespace FreeTrain.Online.Persistence;

// Shared by admission and the live publisher; the bytes admitted are the bytes sent.
public static class StateDifference
{
    private static readonly JsonSerializerOptions Json = WireJson.CreateOptions();
    public static DeltaMessage Between(SnapshotMessage previous, SnapshotMessage next)
    {
        if (TerrainWire.TryDifference(previous,next,out var terrainDelta)) return terrainDelta with { PlaybackRate = next.PlaybackRate };
        if (previous.WorldEpoch != next.WorldEpoch || previous.Map != next.Map || previous.Ruleset != next.Ruleset ||
            previous.ContentVersion != next.ContentVersion || !previous.RequiredPacks.OrderBy(p => p.Id).SequenceEqual(next.RequiredPacks.OrderBy(p => p.Id)) ||
            previous.Terrain?.Name != next.Terrain?.Name || previous.Terrain?.Provenance != next.Terrain?.Provenance)
            throw new ArgumentException("World replacement requires a snapshot.");
        static T[] Changed<T, TKey>(T[] before, T[] after, Func<T, TKey> key) where TKey : notnull
        {
            if (ReferenceEquals(before, after)) return [];
            var old = before.ToDictionary(key);
            return after.Where(x => !old.TryGetValue(key(x), out var value) ||
                !ReferenceEquals(value,x) && JsonSerializer.Serialize(value, Json) != JsonSerializer.Serialize(x, Json)).ToArray();
        }
        return new(next.WorldEpoch, previous.Revision, next.Revision, next.Tick, next.Paused, next.Cash,
            Changed(previous.Trains, next.Trains, x => x.Id), Changed(previous.Stations, next.Stations, x => x.Id),
            Changed(previous.Buildings, next.Buildings, x => x.Id), Changed(previous.Tracks, next.Tracks, x => x.Position),
            new(previous.Trains.Select(x => x.Id).Except(next.Trains.Select(x => x.Id)).ToArray(),
                previous.Stations.Select(x => x.Id).Except(next.Stations.Select(x => x.Id)).ToArray(),
                previous.Buildings.Select(x => x.Id).Except(next.Buildings.Select(x => x.Id)).ToArray(),
                previous.Tracks.Select(x => x.Position).Except(next.Tracks.Select(x => x.Position)).ToArray(),
                next.Entities == null ? null : ReferenceEquals(previous.Entities, next.Entities) ? [] : (previous.Entities ?? []).Select(e => e.Id).Except(next.Entities.Select(e => e.Id)).ToArray()),
            next.Ruleset, next.ContentVersion, next.RequiredPacks,
            next.Entities == null ? null : Changed(previous.Entities ?? [], next.Entities, e => e.Id),
            next.Terrain is not {} terrain ? null : new(terrain.WaterLevel, terrain.ConstructionMode, terrain.GeometryRevision,
                terrain.NextEntityId, ReferenceEquals(previous.Terrain?.Cells, terrain.Cells) ? [] : terrain.Cells.Select((c, i) => (c, i))
                    .Where(row => previous.Terrain == null || previous.Terrain.Cells[row.i] != row.c)
                    .Select(row => new TerrainCellChangeRead(row.i, row.c.Ground, row.c.Surface)).ToArray(),
                Changed(previous.Terrain?.Occupants ?? [], terrain.Occupants, o => o.EntityId)), next.Mixed, next.PlaybackRate);
    }
}
