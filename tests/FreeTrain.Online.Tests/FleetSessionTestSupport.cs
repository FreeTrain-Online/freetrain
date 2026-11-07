using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public partial class FleetMovementTests
{
    static MixedSessionState WithFixtureController(MixedSessionState s)
    {
        var w=s.World;var f=w.Fleet!;var id=w.Spatial.NextEntityId;
        var provider=f.Profile.Providers.Single(p=>p.Kind==FleetControllerKind.Timetable);
        var graph=new FleetGraph(f.Profile,f.RootGroupId,f.Trains.Select(t=>t with {ControllerId=id,Revision=t.Revision+1}),
            f.Groups,f.Controllers.Add(new(id,0,"Shared controller",provider.Kind,provider.Binding)));
        var world=w.Replace(w.Revision,w.Terrain,new(id+1,w.Spatial.Entities),w.Voxels,w.Slopes,w.SpeedLimits,graph);
        return s.Successor(s.Revision,new(world,s.Construction.Accounts),s.ConstructionMode);
    }
    static readonly DateTimeOffset FleetNow=new(2026,11,5,12,0,0,TimeSpan.Zero);
    static readonly ParticipantContext FleetOwner=new("owner"),FleetOperator=new("operator",ParticipantRole.Operator,"operator-connection");
    static SessionHost FleetHost()
    {
        var s=FleetLine();
        return new(WithFixtureController(s),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
    }
    static MixedSessionState FleetLine()
    {
        var s=OperationsLine(quantity:2,signal:true);
        var p=SignalRailwayConstruction.Preview(s.World,new(26,20,2),SignalRailwayConstruction.ThreeAspect,2);
        Assert.True(p.Legal,p.Reason);s=s.Successor(s.Revision,SignalRailwayConstruction.Apply(s.Construction,p,s.Clock.Time),false);
        s=Place(s,0,new(22,20,2));return Place(s,1,new(26,20,2));
    }
    static async Task<WireMessage> FleetSend(SessionHost h,CommandPayload payload,ParticipantContext? who=null,int ms=0,long? epoch=null)
    {
        var task=h.Enqueue(who??FleetOwner,new(Guid.NewGuid().ToString(),epoch??h.WorldEpoch,payload));
        h.Pump(0,FleetNow.AddMilliseconds(ms));return await task;
    }
    static async Task FleetStep(SessionHost h,int minutes,int ms=0)
    { for(var n=0;n<minutes;n++)Assert.IsType<AcceptedMessage>(await FleetSend(h,new StepCommand(),ms:ms)); }
    static async Task<byte[]> FleetSave(SessionHost h)
    {var save=h.CaptureSave();h.Pump(0,FleetNow);return await save;}

}
