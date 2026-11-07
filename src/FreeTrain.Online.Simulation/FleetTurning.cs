// ExAV 883cf7d: AdvancedStationHandler.checkTurn (foobarbazer / riorio).
// LGPL-2.1-or-later. TRAIN-D006: ordered effects in a detached callback;
// invalid re-placement aborts the callback, never partially changing occupancy.
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public static class FleetTurning
{
    public static FleetStopResult CheckTurn(MixedWorld world,FleetTrain train,int forwarding,GameTime? time=null)
    {
        if(forwarding is not (-1 or -1000))return new(train,-1);
        if(train.Head.State is not FleetCarState.Inside head)return new(train,forwarding);

        train=FleetReversal.Reverse(world,train,time);
        FleetCarState.Inside? tail=null;
        // Source traverses from the captured old head via its new predecessors.
        foreach(var car in train.Cars.Reverse())
        {
            if(car.State is not FleetCarState.Inside inside || world.At(inside.Location)?.Traffic?.Rail is not {} rail)
                return new(train,forwarding);
            var ports=RailwayTopology.Ports(rail.Pattern).Order().ToArray();
            if(ports.Length!=2 || ports[1]-ports[0]!=4)return new(train,forwarding);
            tail=inside;
        }
        bool toLower;
        if(train.Cars.Length==1)
        {
            var direction=tail!.Heading; // guide on a straight rail after the first reverse
            if(!direction.IsCardinal)return new(train,-1000); // even for reverse2
            toLower=direction.Index<=2;
        }
        else
        {
            var dx=tail!.Location.X-head.Location.X;var dy=tail.Location.Y-head.Location.Y;
            if(dx==dy)return new(train,forwarding);
            toLower=dy<0 || dx>0;
        }
        var position=toLower?head.Location:tail!.Location;
        // Remove cars only: no train unregister/register and no timer changes.
        train=train with {Cars=train.Cars.Select(c=>c with {State=new FleetCarState.Unplaced()}).ToImmutableArray()};
        if(toLower)train=FleetReversal.Reverse(world,train,time);
        var cars=train.Cars.ToBuilder();var visited=new HashSet<Location>();Heading? heading=null;
        for(var n=cars.Count-1;n>=0;n--)
        {
            var traffic=world.At(position)?.Traffic;
            if(!world.Terrain.Space.ContainsVoxel(position) || traffic?.Rail is not {} rail ||
                traffic.Car is {} occupant && occupant.TrainId!=train.Id || !visited.Add(position))
                throw new ArgumentException("Invalid turning re-placement.");
            heading ??=new Heading(RailwayTopology.Ports(rail.Pattern).Min()); // rr.dir1
            cars[n]=cars[n] with {State=new FleetCarState.Inside(position,heading.Value)};
            var handler=world.Profile.Resolve(rail.Definition).Handler;
            if(n>0 && handler==RailwayHandler.Junction && RailwayTopology.HasPort(rail.Pattern,heading.Value) &&
                (RailwayTopology.HasPort(rail.Pattern,heading.Value.Left) || RailwayTopology.HasPort(rail.Pattern,heading.Value.Right)))
                throw new ArgumentException("Turning follower has an unplaced predecessor.");
            var direction=n==0 ? FleetRailGuidance.GuideHead(world,world.Fleet!,train,position,rail,heading.Value,time) :
                FleetRailGuidance.Guide(handler,rail.Pattern,heading.Value);
            position=position.Move(direction);heading=direction; // source deliberately omits zdiff
        }
        train=train with {Cars=cars.ToImmutable()};
        if(toLower)train=FleetReversal.Reverse(world,train,time);
        return new(train,forwarding);
    }
}
