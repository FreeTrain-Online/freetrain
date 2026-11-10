// Stable ExAV 883cf7d YardRailRoad.getStopTimeSpan, LGPL-2.1-or-later.
namespace FreeTrain.Online.Simulation;

public static class StationStopGeometry
{
    public static int? Index(int platformLength, int trainLength, Heading platformDirection, Heading incoming) =>
        incoming == platformDirection ? (platformLength + trainLength) / 2 - 1 :
        incoming == platformDirection.Opposite ? (platformLength - trainLength) / 2 : null;
}
