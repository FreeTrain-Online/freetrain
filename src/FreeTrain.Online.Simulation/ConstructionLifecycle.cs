// ExAV 883cf7d/core/world/structs/ConstructionSite.cs, VoxelImpl.
// Original authors, LGPL-2.1-or-later. Shared by city and railway sites.
namespace FreeTrain.Online.Simulation;

public static class ConstructionLifecycle
{
    // The southwest corner suppresses the draw on EVERY floor.
    public static ConstructionCell Initial(bool southwest, Func<int,int> next) =>
        new(NeedsDoor:!southwest && next(4)==0);
    public static int Delay(Func<int,int> next) => 40+next(80);
    public static bool CanProceed(bool weekend,int hour,int state,int? below,int belowMinimum,int? above,int aboveMinimum) =>
        !weekend && hour is >=9 and <=17 &&
        (below==null || below>=2 && belowMinimum>state && (state!=1 || belowMinimum>=3)) &&
        (above==null || (state!=2 || aboveMinimum>=1) && (state!=3 || aboveMinimum>=3));

    // Decorative ground consumes this turn. It is drawn even for an upper voxel;
    // isGroundLevel only controls presentation in the original source.
    public static ConstructionCell Progress(ConstructionCell cell,Func<int,int> next)
    {
        if(cell.State is <0 or >=4) throw new ArgumentException("Construction voxel is not pending.");
        if(cell.Ground==0 && cell.State==0 && next(2)==0)
            return cell with {Ground=next(4) switch {0=>3,1=>2,2=>1,_=>4+next(3)}};
        return cell with {State=cell.State+1};
    }
}
