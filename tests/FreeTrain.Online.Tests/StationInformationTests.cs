using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public partial class StationYardStopTests
{
    static SessionHost InformationHost(MixedSessionState s) => new(s,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
    static StationInformationQuery InformationQuery(SessionHost host, bool platform=true, bool train=true)
    {
        var s=host.Snapshot();var g=s.Mixed!.StationGraph!;
        return new(s.WorldEpoch,s.Revision,s.Tick,g.Buildings.SingleOrDefault()?.Id ?? 0,
            platform?g.Platforms[0].Id:0,train?s.Mixed.Fleet!.Trains[0].Id:0);
    }
    static async Task<byte[]> InformationSave(SessionHost h) { var task=h.CaptureSave();h.Pump(0);return await task; }

    [Theory,MemberData(nameof(Positions))]
    public async Task Station_information_uses_independent_source_stop_indices_and_is_read_only(JsonElement row)
    {
        var i=row.GetProperty("input");var e=row.GetProperty("expected");
        var s=Scene(Int(i,"trainLength"),Int(i,"index"),i.GetProperty("sameDirection").GetBoolean());
        using var host=InformationHost(s);var before=await InformationSave(host);
        var result=host.StationInformation(InformationQuery(host));
        Assert.Equal(Int(e,"stopPosition"),result.StopIndex);
        Assert.Equal(result.StopIndex>=0 && result.StopIndex<4 ? "wait" : "length",result.Reason);
        Assert.Equal(10000,result.Demand);Assert.Equal(10000,result.Waiting);
        Assert.Equal(0,result.AverageBoarded);Assert.Equal(0,result.AverageAlighted);
        Assert.Equal(before,await InformationSave(host));
        using var reopened=InformationHost(Open(s));Assert.Equal(result,reopened.StationInformation(InformationQuery(reopened)));
    }
    [Theory]
    [InlineData("pass",0,"pass")]
    [InlineData("forwarding",0,"wait")]
    [InlineData("forwarding",1,"forwarding")]
    [InlineData("reverseforwarding",1,"forwarding")]
    [InlineData("reverse2",1,"ready")]
    [InlineData("rforwarding2",1,"forwarding")]
    [InlineData("stop",1,"wait")]
    public async Task Station_information_checks_current_effective_TAT_call_without_committing_exchange_or_TURNING(string action,int count,string reason)
    {
        var s=Scene(count:count);var b=Station(s);
        s=Edit(s,controller:c=>c with {Harbors=[new(b.Location,FleetHarborKind.Advanced,0,false,
            [new(-1,-1,-1,-1,-1,Enum.Parse<FleetRuleAction>(action,true))])]});
        using var host=InformationHost(s);var before=await InformationSave(host);
        var result=host.StationInformation(InformationQuery(host));
        Assert.Equal(reason,result.Reason);Assert.Equal(s.Fleet!.EffectiveController(s.Fleet.Trains[0].Id).Id,result.ControllerId);
        Assert.Equal(before,await InformationSave(host));Assert.Empty(s.PassengerEffects);Assert.Empty(s.DepartureBells);
    }
    [Fact]
    public void Station_information_distinguishes_disconnection_zero_supply_wrong_cell_and_away()
    {
        var s=Scene(index:1);using(var h=InformationHost(s))Assert.Equal("position",h.StationInformation(InformationQuery(h)).Reason);
        s=Scene();var provider=s.World.Stations!.Listeners[0];
        s=s.Successor(s.Revision,new(StationCatchment.Update(s.World,provider.ProviderId,provider.Provider! with {Population=new("ConstantPopulation",0)}),s.Construction.Accounts),false);
        using(var h=InformationHost(s)) {var r=h.StationInformation(InformationQuery(h));Assert.Equal("wait",r.Reason);Assert.Equal(0,r.Demand);Assert.Equal(0,r.Waiting);}
        s=Scene(host:false);using(var h=InformationHost(s)) {
            var r=h.StationInformation(InformationQuery(h) with {StationId=0});Assert.Equal("disconnected",r.Reason);Assert.Null(r.Demand);
        }
        s=Scene();s=FleetDeployment.Commit(s,FleetDeployment.PreviewWithdrawal(s,s.Fleet!.Trains[0].Id));
        using(var h=InformationHost(s))Assert.Equal("unplaced",h.StationInformation(InformationQuery(h)).Reason);
        s=FleetDeployment.Commit(s,FleetDeployment.PreviewPlacement(s,s.Fleet!.Trains[0].Id,new(32,15,2),false));
        using(var h=InformationHost(s))Assert.Equal("away",h.StationInformation(InformationQuery(h)).Reason);
    }
    [Fact]
    public async Task Station_information_fences_epoch_revision_minute_and_identities_and_does_not_cache_after_exchange()
    {
        var s=Scene();using var h=InformationHost(s);var q=InformationQuery(h);var before=await InformationSave(h);
        foreach(var stale in new[]{q with {ExpectedWorldEpoch=q.ExpectedWorldEpoch+1},q with {ExpectedRevision=q.ExpectedRevision+1},
            q with {ExpectedMinute=q.ExpectedMinute+1},q with {StationId=99999},q with {PlatformId=99999},q with {TrainId=99999},q with {StationId=0}})
            Assert.Throws<InvalidOperationException>(()=>h.StationInformation(stale));
        Assert.Throws<ArgumentException>(()=>h.StationInformation(q with {TrainId=-1}));
        Assert.Equal("noPlatform",h.StationInformation(q with {PlatformId=0}).Reason);
        Assert.Equal("noTrain",h.StationInformation(q with {TrainId=0}).Reason);
        Assert.Equal(before,await InformationSave(h));
        Call(s);Call(s);using var changed=InformationHost(s);var result=changed.StationInformation(InformationQuery(changed));
        Assert.True(result.Waiting<10000);Assert.True(result.AverageBoarded>0);
        using var resumed=InformationHost(Open(s));Assert.Equal(result,resumed.StationInformation(InformationQuery(resumed)));
    }
    [Fact]
    public void Station_information_midnight_browser_fixture_is_reached_by_real_callbacks_and_reopens_exactly()
    {
        var s=Scene();Call(s);Call(s);
        while(s.Clock.Time.AbsoluteMinute<1439)s.AdvanceOneMinute();
        var before=SaveCodec.Write(s);
        if(Environment.GetEnvironmentVariable("FREETRAIN_STATION_INFORMATION_OUTPUT") is {} path)File.WriteAllBytes(path,before);
        Assert.Equal(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"PassengerService","station-information-midnight-v11.json")),before);
        var reopened=Open(s);s.AdvanceOneMinute();reopened.AdvanceOneMinute();
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(reopened));
        Assert.Equal(0,Station(s).Exports.Today);Assert.True(Station(s).Exports.Yesterday>0);
        using var h=InformationHost(s);using var r=InformationHost(reopened);
        Assert.Equal(h.StationInformation(InformationQuery(h)),r.StationInformation(InformationQuery(r)));
    }

    [Fact]
    public async Task Station_information_latest_observes_current_state_after_the_requested_minute_without_mutation()
    {
        var s=Scene();using var h=InformationHost(s);var q=InformationQuery(h);
        var task=h.Enqueue(new("owner"),new CommandMessage("advance-information",h.WorldEpoch,new StepCommand()));h.Pump(0);
        Assert.IsType<AcceptedMessage>(await task);
        Assert.Throws<InvalidOperationException>(()=>h.StationInformation(q));
        var before=await InformationSave(h);var r=h.StationInformation(q with {Latest=true});
        Assert.Equal(q.ExpectedMinute+1,r.ObservedMinute);Assert.True(r.ObservedRevision>q.ExpectedRevision);
        Assert.Equal(before,await InformationSave(h));
    }

}
