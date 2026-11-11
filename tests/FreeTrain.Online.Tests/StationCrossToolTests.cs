using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;
using static FreeTrain.Online.Tests.StationBuildingConstructionTests;

namespace FreeTrain.Online.Tests;

public partial class FleetMovementTests
{
    [Theory]
    [InlineData(0)][InlineData(2)][InlineData(4)][InlineData(6)]
    public void Actual_slope_rails_reject_platform_conversion_without_mutating_supports(int heading)
    {
        var s=StationBuildingConstructionTests.Empty();
        var plan=SlopeRailwayConstruction.PreviewBuild(s.World,Base,new(heading));
        Assert.True(plan.Legal,plan.Reason);
        var built=SlopeRailwayConstruction.Apply(s.Construction,plan,s.Clock.Time);
        s=s.Successor(s.Revision,built,false);var bytes=SaveCodec.Write(s);
        foreach(var rail in plan.Rails)
        {
            Assert.Equal("incompatiblePlatformRail",ThinPlatformConstruction.Preview(s.World,rail.Position,new(heading),1,false).Reason);
            Assert.Equal(bytes,SaveCodec.Write(s));
        }
    }

    [Fact]
    public void Platform_passage_pole_and_speed_edits_retain_each_others_components_and_timers()
    {
        var s=StationBuildingConstructionTests.Empty();
        s=ThinPlatformConstruction.Commit(s,ThinPlatformConstruction.Preview(s.World,Base,Heading.East,4,true));
        var p=s.World.Stations!.Platforms[0];var rail=s.World.At(Base)!.Traffic!.Rail;
        var timers=s.Clock.Capture();var accounts=s.Construction.Accounts;
        var pole=PoleRailwayConstruction.Preview(s.World,Base,PoleRailwayConstruction.Double1);Assert.True(pole.Legal,pole.Reason);
        s=s.Successor(s.Revision,PoleRailwayConstruction.Apply(s.Construction,pole,s.Clock.Time),false);
        var speed=SpeedLimitConstruction.Preview(s.World,Base,Base.Move(Heading.East),"low");Assert.True(speed.Legal,speed.Reason);
        s=s.Successor(s.Revision,SpeedLimitConstruction.Apply(s.Construction,speed,s.Clock.Time),false);
        var passage=PassageConstruction.Preview(s.World,Base.Move(new(0)),Base.Move(new(4)),PassageMaterial.Green);Assert.True(passage.Legal,passage.Reason);
        s=PassageConstruction.Commit(s,passage);
        Assert.Equal(rail,s.World.At(Base)!.Traffic!.Rail);Assert.NotNull(s.World.At(Base)!.Traffic!.Accessory);
        Assert.True(s.World.SpeedLimits.Contains(Base));
        Assert.Equal(timers.AbsoluteMinute,s.Clock.Time.AbsoluteMinute);Assert.Equal(timers.NextOrder,s.Clock.Capture().NextOrder);
        Assert.Equal(timers.Pending.ToArray(),s.Clock.Capture().Pending.ToArray());Assert.Equal(accounts,s.Construction.Accounts);
        p=s.World.Stations!.Platform(p.Id);
        s=ThinPlatformConstruction.Commit(s,ThinPlatformConstruction.Preview(s.World,Base,Heading.East,4,true,true,p.Id,p.Revision));
        Assert.Equal(rail,s.World.At(Base)!.Traffic!.Rail);Assert.NotNull(s.World.At(Base)!.Traffic!.Accessory);
        Assert.True(s.World.SpeedLimits.Contains(Base));Assert.Empty(s.Clock.Capture().Pending);
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(SaveCodec.ReadWorld(SaveCodec.Write(s),TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!));
    }

    [Theory]
    [InlineData("normal")][InlineData("bridge")][InlineData("girder")]
    [InlineData("garage")][InlineData("tunnel")][InlineData("signal")]
    public void Station_platforms_use_actual_constructed_rail_and_car_predicates(string infrastructure)
    {
        // CrossToolLine purchases/deploys a real consist on legally built rail.
        var s=CrossToolLine(infrastructure);var z=infrastructure is "bridge" or "girder"?3:2;
        var bytes=SaveCodec.Write(s);var occupied=new Location(22,20,z);
        Assert.Equal("occupied",ThinPlatformConstruction.Preview(s.World,occupied,Heading.East,1,false).Reason);
        Assert.Equal(bytes,SaveCodec.Write(s));
        // Check a clear actual special rail, including the signal itself after withdrawal.
        s=FleetDeployment.Commit(s,FleetDeployment.PreviewWithdrawal(s,s.Fleet!.Trains[0].Id));
        var withdrawn=SaveCodec.Write(s);
        var at=new Location(infrastructure=="signal"?22:26,20,z);
        var plan=ThinPlatformConstruction.Preview(s.World,at,Heading.East,1,false);
        Assert.Equal(infrastructure=="normal",plan.Legal);
        if(infrastructure!="normal")Assert.Equal("incompatiblePlatformRail",plan.Reason);
        else
        {
            var after=ThinPlatformConstruction.Commit(s,plan);
            Assert.Equal("platformDependency",NormalRailwayConstruction.PreviewRemoval(after.World,at,at.Move(Heading.East)).Reason);
            Assert.Equal("platformDependency",ObjectRemoval.Mixed(after.World,at).Reason);
            Assert.Equal(s.World.At(at)!.Traffic!.Rail,after.World.At(at)!.Traffic!.Rail);
            MixedWire.ValidateTransition(s,after);
        }
        Assert.Equal(withdrawn,SaveCodec.Write(s));
    }
}
