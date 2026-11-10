using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Server;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public partial class StationYardStopTests
{
    static readonly DateTimeOffset BellNow = new(2026,11,10,12,0,0,TimeSpan.Zero);
    static WireMessage[] ReadBells(NetworkSession.Peer peer)
    { var result=new List<WireMessage>(); while(peer.Output.Reader.TryRead(out var m))result.Add(m); return result.ToArray(); }
    static (NetworkSession Network,SessionHost Host,NetworkSession.Peer Owner,NetworkSession.Peer Peer) BellNetwork(MixedSessionState state)
    {
        var host=new SessionHost(state,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var network=new NetworkSession(new(){OutputCapacity=128},hostFactory:(_,_)=>host);
        var owner=network.Connect(BellNow,new("owner"))!;
        var peer=network.Connect(BellNow,new("peer",ParticipantRole.Observer))!;
        return (network,host,owner,peer);
    }
    static async Task<(WireMessage Result,WireMessage[] Owner,WireMessage[] Peer)> BellStep(
        NetworkSession network,NetworkSession.Peer owner,NetworkSession.Peer peer,string id)
    {
        var task=network.Submit(owner,new(id,network.Bootstrap().WorldEpoch,new StepCommand()),BellNow);
        network.Tick(BellNow); var result=await task; network.Reply(owner,result);
        return (result,ReadBells(owner),ReadBells(peer));
    }
    [Theory]
    [InlineData(-1000,1,true)][InlineData(-999,1,true)][InlineData(0,1,true)]
    [InlineData(0,0,true)][InlineData(10,1,true)][InlineData(10,0,true)][InlineData(0,1,false)]
    public async Task Committed_departures_only_publish_after_state_with_selected_bell_to_two_peers(int result,int count,bool connected)
    {
        var state=Scene(count:count,host:connected);
        var platform=state.World.Stations!.Platforms[0];var station=Station(state);
        var selected=state.World.Stations.Profile.Bells.Last().Reference;
        state=PlatformProperties.Apply(state,platform.Id,platform.Revision,platform.Name,selected,
            platform.HostId,connected?station.Revision:0,connected?station.Revision:0);
        state=Bind(state,(_,t,_)=>new(t,result));
        var (network,host,owner,peer)=BellNetwork(state);using var dispose=host;
        var baseline=Assert.IsType<JoinMessage>(Assert.Single(ReadBells(owner))).Snapshot.EffectsBaseline!;ReadBells(peer);
        (WireMessage Result,WireMessage[] Owner,WireMessage[] Peer) step=(new RejectedMessage("initial","unused",new()),[],[]);
        var due=state.Clock.Capture().Pending.Single(t=>t.HandlerId.StartsWith("train/",StringComparison.Ordinal)).DueMinute;
        for(var i=0;host.Snapshot().Tick<due;i++)step=await BellStep(network,owner,peer,"bell-"+i);
        Assert.IsType<AcceptedMessage>(step.Result);
        if(connected && count>0 && result<=0)
        {
            var bell=Assert.Single(step.Owner.OfType<DepartureBellMessage>());
            Assert.Equal(bell,Assert.Single(step.Peer.OfType<DepartureBellMessage>()));
            Assert.Equal(baseline.LiveEpoch,bell.LiveEpoch);Assert.True(bell.PublicationSequence>baseline.PublicationSequence);
            Assert.Equal(0,bell.EffectOrdinal);Assert.Equal(platform.Id,bell.PlatformId);
            Assert.Equal(selected.ContributionId,bell.Bell.ContributionId);Assert.Equal(new(27,15,2),bell.Location);
            Assert.True(Array.FindIndex(step.Owner,m=>m is DeltaMessage)<Array.IndexOf(step.Owner,bell));
            if(result is -999 or -1000)Assert.Equal(0,host.Snapshot().Mixed!.Fleet!.Trains[0].Passengers);
            var retry=new CommandMessage("bell-"+(due-state.Clock.Time.AbsoluteMinute-1),host.WorldEpoch,new StepCommand());
            var duplicate=network.Submit(owner,retry,BellNow);Assert.Same(step.Result,await duplicate);
            network.Reply(owner,await duplicate);network.Tick(BellNow);
            Assert.IsType<AcceptedMessage>(Assert.Single(ReadBells(owner)));Assert.Empty(ReadBells(peer));
            network.Disconnect(peer,BellNow);
            var rejoined=network.Connect(BellNow,new("peer",ParticipantRole.Observer))!;
            var join=Assert.IsType<JoinMessage>(Assert.Single(ReadBells(rejoined)));
            Assert.Equal(bell.PublicationSequence,join.Snapshot.EffectsBaseline!.PublicationSequence);
            network.Resync(owner);Assert.Single(ReadBells(owner));network.Tick(BellNow);
            Assert.Empty(ReadBells(owner));Assert.Empty(ReadBells(rejoined));
        }
        else {Assert.Empty(step.Owner.OfType<DepartureBellMessage>());Assert.Empty(step.Peer.OfType<DepartureBellMessage>());}
    }
    [Fact]
    public async Task Two_real_yards_depart_in_one_publication_with_distinct_ordered_identities()
    {
        var state=Scene(count:1);
        state=StationBuildingConstructionTests.Build(state,at:new(23,28,2));
        var route=NormalRailwayConstruction.PreviewBuild(state.World,new(16,30,2),new(44,30,2));Assert.True(route.Legal,route.Reason);
        state=state.Successor(state.Revision,NormalRailwayConstruction.Apply(state.Construction,route,state.Clock.Time),false);
        state=ThinPlatformConstruction.Commit(state,ThinPlatformConstruction.Preview(state.World,new(25,30,2),Heading.East,4,true));
        var stock=RollingStockCatalog.Bundled.Trains.First(t=>t.PermittedLengths.Contains(3));
        state=FleetTrading.Commit(state,FleetTrading.QuotePurchase(state,stock.Binding,3,1));
        state=FleetDeployment.Commit(state,FleetDeployment.PreviewPlacement(state,state.Fleet!.Trains[^1].Id,new(25,30,2),false));
        state=Bind(Edit(state,t=>t with {StopCallCount=1}),(_,t,_)=>new(t,0));
        var (network,host,owner,peer)=BellNetwork(state);using var dispose=host;ReadBells(owner);ReadBells(peer);
        var due=state.Clock.Capture().Pending.First(t=>t.HandlerId.StartsWith("train/",StringComparison.Ordinal)).DueMinute;
        WireMessage[] published=[];
        for(var i=0;host.Snapshot().Tick<due;i++)published=(await BellStep(network,owner,peer,"pair-"+i)).Owner;
        var bells=published.OfType<DepartureBellMessage>().ToArray();Assert.Equal(2,bells.Length);
        Assert.Equal(bells[0].PublicationSequence,bells[1].PublicationSequence);Assert.Equal(bells[0].LiveEpoch,bells[1].LiveEpoch);
        Assert.Equal(new[]{0,1},bells.Select(b=>b.EffectOrdinal));Assert.Equal(2,bells.Select(b=>b.TrainId).Distinct().Count());
        Assert.Equal(2,bells.Select(b=>b.PlatformId).Distinct().Count());
    }
    [Fact]
    public void Generate_real_selected_bell_departure_fixtures()
    {
        var state=Scene(count:1);var platform=state.World.Stations!.Platforms[0];var station=Station(state);
        state=PlatformProperties.Apply(state,platform.Id,platform.Revision,platform.Name,state.World.Stations.Profile.Bells.Last().Reference,
            platform.HostId,station.Revision,station.Revision);
        state=Edit(state,controller:c=>c with {Kind=FleetControllerKind.Timetable,Harbors=[new(station.Location,FleetHarborKind.Advanced,0,false,
            [new(-1,-1,-1,-1,-1,FleetRuleAction.Forwarding)])]});
        var seed=SaveCodec.Write(state);Call(state);Assert.Single(state.DepartureBells);Assert.Equal(0,state.Fleet!.Trains[0].Passengers);
        if(Environment.GetEnvironmentVariable("FREETRAIN_BELL_FLEET_OUTPUT") is {} path)
        {
            File.WriteAllBytes(path,seed);File.WriteAllBytes(path+".departed.json",SaveCodec.Write(state));
            var rejected=Scene(count:1);
            rejected=new(new(rejected.World,new ConstructionAccounts(long.MinValue)),rejected.Provenance,
                clock:rejected.Clock.Capture(),revision:rejected.Revision);
            File.WriteAllBytes(path+".rejected.json",SaveCodec.Write(rejected));
        }
    }
    [Fact]
    public async Task Rejected_minute_and_ambiguous_receipt_retry_never_emit_bells_and_reopen_changes_epoch()
    {
        var state=Scene(count:1);var due=state.Clock.Capture().Pending.Single(t=>t.HandlerId.StartsWith("train/",StringComparison.Ordinal)).DueMinute;
        while(state.Clock.Time.AbsoluteMinute<due-1)state.AdvanceOneMinute();
        state=new(state.Construction,state.Provenance,clock:state.Clock.Capture(),revision:state.Revision,
            trainCallback:(s,id)=>{var candidate=FleetMovement.Tick(s,id);Assert.Single(candidate.DepartureBells);throw new OverflowException("after staged departure");});
        var (network,host,owner,peer)=BellNetwork(state);using var dispose=host;
        var baseline=Assert.IsType<JoinMessage>(Assert.Single(ReadBells(owner))).Snapshot.EffectsBaseline!;ReadBells(peer);
        var command=new CommandMessage("failed-departure",host.WorldEpoch,new StepCommand());
        var task=network.Submit(owner,command,BellNow);network.Tick(BellNow);
        Assert.Equal("operationUnavailable",Assert.IsType<RejectedMessage>(await task).Code);
        Assert.Empty(ReadBells(owner));Assert.Empty(ReadBells(peer));Assert.Equal(baseline,host.EffectsBaseline());
        Assert.Same(task,network.Submit(owner,command,BellNow));network.Tick(BellNow);
        Assert.Empty(ReadBells(owner));Assert.Empty(ReadBells(peer));
        var saved=SaveCodec.Write(state);var load=host.LoadSave(saved);network.Tick(BellNow.AddMilliseconds(100));await load;
        var replacement=Assert.IsType<SnapshotMessage>(Assert.Single(ReadBells(owner)));
        Assert.NotEqual(baseline.LiveEpoch,replacement.EffectsBaseline!.LiveEpoch);
        Assert.Equal(0,replacement.EffectsBaseline.PublicationSequence);Assert.Empty(host.TakeDepartureBells());
        Assert.Equal(saved,SaveCodec.Write(SaveCodec.ReadWorld(saved,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!));
    }
}
