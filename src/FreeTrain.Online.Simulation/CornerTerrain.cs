// Source: ExAV 2.1.1.12 MountainController, MountainVoxel and World ground methods.
// Original authors, LGPL-2.1-or-later. See doc/web-parity-corner-terrain.md.
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public sealed record CornerTerrainChange(Location Position, int BeforeGround, int AfterGround,
    QuarterCorners Before, QuarterCorners After, long? EntityId, bool Remove, bool LosesForest, bool LosesOwnership);
public sealed record CornerTerrainPlan(Location Position, string Action, string? Reason, ImmutableArray<CornerTerrainChange> Changes)
{
    public bool Legal => Reason == null;
}

public static class CornerTerrain
{
    // Source x-major/y-minor order and NE/SE/SW/NW indices.
    private static readonly (int X, int Y, int Corner)[] touching = [(0,-1,1),(0,0,0),(1,-1,2),(1,0,3)];
    public static CornerTerrainPlan Preview(TerrainWorld world, Location position, string action) => new CornerTerrainDraft(world).Preview(position,action);
    internal static CornerTerrainPlan Preview(CornerTerrainDraft world, Location position, string action)
    {
        CornerTerrainPlan Reject(string reason) => new(position, action, reason, []);
        if (action is not ("raise" or "lower")) return Reject("invalidInput");
        if (!world.Space.ContainsVoxel(position)) return Reject("outside");
        // Preserve the source helper's four outer-corner checks around the 2x2.
        var ring=position;var direction=Heading.North;
        for(int i=0;i<4;i++)
        {
            var d=direction.Left;int corner=d.Index/2;
            int[] fine=[world.FineHeight(ring,corner),world.FineHeight(ring.Move(d),(corner+2)%4),
                world.FineHeight(ring.Move(d.Left),(corner+1)%4),world.FineHeight(ring.Move(d.Right),(corner+3)%4)];
            if(fine.Where(h=>h>=0).Distinct().Count()>1)return Reject("unmatchedCorners");
            ring=ring.Move(direction);direction=direction.Right90;
        }
        var samples = touching.Select(t => (Position: new Location(checked(position.X+t.X), checked(position.Y+t.Y), position.Z), t.Corner)).ToArray();
        var heights = samples.Select(s => world.FineHeight(s.Position, s.Corner)).Where(h => h >= 0).Distinct().ToArray();
        if (heights.Length != 1) return Reject("unmatchedCorners");
        var storage = world.Space.ToStorage(position);
        int ground = world.Cell(storage.H, storage.V).Ground, working = position.Z;
        var baseMountain = world.OccupantAt(position);
        if (baseMountain?.Kind != TerrainOccupantKind.Mountain) baseMountain = null;
        if (action == "raise")
        {
            if (working != ground) return Reject("groundLevel");
        }
        else if (baseMountain != null)
        {
            if (baseMountain.Corners.NE == 0) return Reject("cornerLimit");
        }
        else
        {
            if (ground != working && ground != working-1) return Reject("groundLevel");
            if (working == 0) return Reject("floor");
            working--;
        }
        var changes = ImmutableArray.CreateBuilder<CornerTerrainChange>();
        foreach (var sample in samples)
        {
            var p = sample.Position with { Z = working };
            if (!world.Space.ContainsHorizontal(p))
            {
                if (action == "lower") return Reject("outside");
                continue; // Source permits partial neighborhoods only for raising.
            }
            var s = world.Space.ToStorage(p); var cell = world.Cell(s.H,s.V);
            var occupant = world.OccupantAt(p); var entity = world.At(p).Entity;
            var mountain = occupant?.Kind == TerrainOccupantKind.Mountain ? occupant : null;
            QuarterCorners before = mountain?.Corners ?? default;
            int afterGround = cell.Ground;
            if (action == "raise")
            {
                if (cell.Ground != ground) return Reject("groundLevel");
                if (entity != null && mountain == null) return Reject("occupied");
                if (before[sample.Corner] == 4) return Reject("cornerLimit");
                if (ground == world.Space.Levels-1 && before[sample.Corner] == 3) return Reject("roof");
            }
            else if (mountain == null)
            {
                if (world.At(p with { Z = working+1 }).Status != VoxelStatus.Vacant || entity != null)
                    return Reject("occupied");
                // The desktop guard accepts some invalid cliff selections. Never publish them.
                if (cell.Ground != working+1) return Reject("groundLevel");
                afterGround--; before = new(4,4,4,4);
            }
            if (action == "lower" && before[sample.Corner] == 0) return Reject("cornerLimit");
            int[] offsets = [before.NE,before.SE,before.SW,before.NW];
            offsets[sample.Corner] += action == "raise" ? 1 : -1;
            var after = new QuarterCorners(offsets[0],offsets[1],offsets[2],offsets[3]);
            bool remove = action == "raise" ? after.Saturated : after.Flat;
            if (action == "raise" && remove) { afterGround++; after = default; }
            if(afterGround != cell.Ground && (cell.Surface == TerrainSurface.Terrace ||
                afterGround < 0 || afterGround >= world.Space.Levels)) return Reject("occupied");
            changes.Add(new(p, cell.Ground, afterGround, mountain?.Corners ?? default, after,
                entity?.Id, remove, remove && mountain?.Forest != null, remove && entity?.Owned == true));
        }
        return new(position, action, null, changes.ToImmutable());
    }

    public static TerrainWorld Apply(TerrainWorld world, CornerTerrainPlan plan)
    {
        var draft = new CornerTerrainDraft(world);
        var actual = draft.Preview(plan.Position,plan.Action);
        if (!plan.Legal || !actual.Legal || !plan.Changes.SequenceEqual(actual.Changes))
            throw new ArgumentException("Invalid corner plan.");
        draft.Apply(plan);
        return draft.Finish();
    }
}
