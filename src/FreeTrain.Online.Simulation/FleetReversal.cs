// ExAV 883cf7d: Train.reverse, ReverseCarState and calcReturnPoint/Matrix.
// Original authors; LGPL-2.1-or-later.
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public static class FleetReversal
{
    // Visit in current head-to-tail order before swapping. A follower's guide
    // uses its predecessor's position, including a predecessor already Outside.
    // Callers normalize revisions once after their complete operation.
    public static FleetTrain Reverse(MixedWorld world, FleetTrain train)
    {
        var fleet=world.Fleet ?? throw new ArgumentException("Fleet is not initialized.");
        var cars=train.Cars.ToBuilder();
        for(var n=0;n<cars.Count;n++)
        {
            var state=cars[n].State;
            if(state is FleetCarState.Inside inside)
            {
                var rail=world.At(inside.Location)!.Traffic!.Rail!;
                var go=n==0 ? FleetRailGuidance.GuideHead(world,fleet,train,inside.Location,rail,inside.Heading) :
                    FleetRailGuidance.Guide(world.Profile.Resolve(rail.Definition).Handler,rail.Pattern,inside.Heading,
                        predecessor:((FleetCarState.Placed)cars[n-1].State).Location,location:inside.Location);
                state=inside with {Heading=go.Opposite};
            }
            else if(state is FleetCarState.Outside outside && ReturnReference(world,outside) is {} reference)
                state=new FleetCarState.Outside(reference.Location.Move(reference.Heading.Opposite),
                    reference.Heading.Opposite,100-outside.Countdown);
            cars[n]=cars[n] with {State=state};
        }
        cars.Reverse();
        return train with {Cars=cars.ToImmutable(),IsReversed=!train.IsReversed};
    }

    // Connectivity alone selects the FIRST source candidate. Occupancy is not
    // part of this lookup. Integer division truncates the doubled matrices.
    internal static FleetCarState.Inside? ReturnReference(MixedWorld world,FleetCarState.Outside state)
    {
        var (x,y)=state.Heading.Offset;
        (int X,int Y)[] offsets=state.Heading.IsCardinal
            ? [(y,-x),(-y,x),(y-2*x,-x-2*y),(-y-2*x,x-2*y),(-x,-y)]
            : [((-x+y)/2,(-x-y)/2),((-x-y)/2,(x-y)/2),
               ((-3*x+y)/2,(-x-3*y)/2),((-3*x-y)/2,(x-3*y)/2),(-x,-y)];
        foreach(var (dx,dy) in offsets)
        {
            var p=state.Location with {X=checked(state.Location.X+dx),Y=checked(state.Location.Y+dy)};
            if(world.At(p)?.Traffic?.Rail is {} rail && RailwayTopology.HasPort(rail.Pattern,state.Heading))
                return new(p,state.Heading.Opposite);
        }
        return null;
    }

    internal static FleetTrain NormalizeCars(FleetTrain before,FleetTrain after)
    {
        var original=before.Cars.ToDictionary(c=>c.Id);
        return after with {Cars=after.Cars.Select(c=>c.State==original[c.Id].State ? original[c.Id] :
            c with {Revision=checked(original[c.Id].Revision+1)}).ToImmutableArray()};
    }
}
