using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Content;

public static class SpatialReferenceScenario
{
    // Independent octagon: four unit steps per heading, 45-degree turns only.
    // No prototype route, entities, financial values or clock state are reused.
    public static ReferenceWorld Create()
    {
        var positions = new List<Location>();
        var position = new Location(27, 24, 0);
        foreach (var direction in new[] { 2,3,4,5,6,7,0,1 })
            for (var step = 0; step < 4; step++)
            { positions.Add(position); position = position.Move(new(direction)); }
        return new(32,64,8, positions.Select((p,i) => new ReferenceRail(p,
            NormalRailPatterns.Find(p.DirectionTo(positions[(i+positions.Count-1)%positions.Count]).Index,
                p.DirectionTo(positions[(i+1)%positions.Count]).Index))));
    }
}
