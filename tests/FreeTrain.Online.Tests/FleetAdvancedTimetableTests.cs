using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Server;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public partial class FleetMovementTests
{
    static FleetTimetableRule Rule(FleetRuleAction action=FleetRuleAction.Go,int month=-1,int day=-1,int weekday=-1,int hour=-1,int minute=-1)
        =>new(month,day,weekday,hour,minute,action);

    [Fact]
    public void Advanced_consumes_all_17_independent_P5_02_mask_and_ordered_rule_fixtures()
    {
        using var fixtures=Fixtures();var count=0;
        FleetTimetableRule Mask(JsonElement m,FleetRuleAction action=FleetRuleAction.Go)=>new(Int(m,"month"),Int(m,"day"),Int(m,"dayOfWeek"),Int(m,"hour"),Int(m,"minutes"),action);
        foreach(var row in Cases(fixtures,"timetables")) {
            var input=row.GetProperty("input");var kind=input.GetProperty("kind").GetString();if(kind=="daily")continue;
            var clock=input.GetProperty("clock");
            // Literal year5 November3 Tuesday08:00 in the source 365-day calendar.
            var time=new GameTime(2543520+Int(clock,"minutes"));
            Assert.Equal(Int(clock,"month"),time.Month);Assert.Equal(Int(clock,"day"),time.DayOfMonth);
            Assert.Equal(Int(clock,"dayOfWeek"),time.DayOfWeek);Assert.Equal(Int(clock,"hour"),time.HourOfDay);
            if(kind=="mask")Assert.Equal(row.GetProperty("expected").GetProperty("matches").GetBoolean(),Mask(input.GetProperty("mask")).Matches(time));
            else {
                var rules=input.GetProperty("rules").EnumerateArray().Select(r=>Mask(r.GetProperty("mask"),
                    r.GetProperty("action").GetString()=="rforwarding"?FleetRuleAction.ReverseForwarding:Enum.Parse<FleetRuleAction>(r.GetProperty("action").GetString()!,true))).ToImmutableArray();
                Assert.Equal(Int(row.GetProperty("expected"),"wait"),FleetTimetable.Stop(rules,Int(input,"callCount"),time).SourceMinutes);
            }
            count++;
        }
        Assert.Equal(17,count);
    }

    [Theory]
    [InlineData(0,1,1,0,0,0,true)]
    [InlineData(44639,1,31,2,23,59,true)]
    [InlineData(44640,2,1,3,0,0,true)]
    [InlineData(84960,3,1,3,0,0,true)]
    [InlineData(525599,12,31,0,23,59,true)]
    [InlineData(525600,1,1,1,0,0,true)]
    [InlineData(44640,1,-1,-1,-1,-1,false)]
    [InlineData(44640,-1,31,-1,-1,-1,false)]
    [InlineData(44640,-1,-1,2,-1,-1,false)]
    [InlineData(44640,-1,-1,-1,23,-1,false)]
    [InlineData(44640,-1,-1,-1,-1,50,false)]
    [InlineData(44640,2,31,-1,-1,-1,false)]
    public void Advanced_masks_match_all_five_source_calendar_fields_conjunctively(long minute,int month,int day,int weekday,int hour,int minutes,bool match)
    {
        Assert.Equal(match,Rule(month:month,day:day,weekday:weekday,hour:hour,minute:minutes).Matches(new(minute)));
        Assert.True(Rule().Matches(new(minute)));
    }

    [Theory]
    [InlineData(FleetRuleAction.Pass,0,0)][InlineData(FleetRuleAction.Stop,0,10)]
    [InlineData(FleetRuleAction.Go,0,10)][InlineData(FleetRuleAction.Reverse,0,10)]
    [InlineData(FleetRuleAction.Forwarding,0,10)][InlineData(FleetRuleAction.ReverseForwarding,0,10)]
    [InlineData(FleetRuleAction.Pass,1,7)][InlineData(FleetRuleAction.Stop,1,7)]
    [InlineData(FleetRuleAction.Go,1,0)][InlineData(FleetRuleAction.Reverse,1,-1)]
    [InlineData(FleetRuleAction.Forwarding,1,-999)][InlineData(FleetRuleAction.ReverseForwarding,1,-1000)]
    public void Advanced_action_selection_distinguishes_arrival_from_later_calls(FleetRuleAction action,int count,int expected)
        =>Assert.Equal(expected,FleetTimetable.Stop([Rule(action)],count,new(203)).SourceMinutes);

    [Fact]
    public void Advanced_stored_order_overlap_and_no_match_polling_have_literal_results()
    {
        Assert.Equal(0,FleetTimetable.Stop([Rule(FleetRuleAction.Go),Rule(FleetRuleAction.Pass),Rule(FleetRuleAction.Stop)],0,new(203)).SourceMinutes);
        Assert.Equal(10,FleetTimetable.Stop([Rule(FleetRuleAction.Stop),Rule(FleetRuleAction.Pass)],0,new(203)).SourceMinutes);
        Assert.Equal(7,FleetTimetable.Stop([Rule(FleetRuleAction.Pass),Rule(FleetRuleAction.Stop),Rule()],1,new(203)).SourceMinutes);
        Assert.Equal(0,FleetTimetable.Stop([Rule(),Rule(FleetRuleAction.Stop)],1,new(203)).SourceMinutes);
        foreach(var (minute,wait) in new[]{(180,10),(189,1),(190,10),(239,1),(240,10)}) {
            Assert.Equal(wait,FleetTimetable.Stop([],7,new(minute)).SourceMinutes);
            Assert.Equal(wait,FleetTimetable.Stop([Rule(month:2)],7,new(minute)).SourceMinutes);
            Assert.Equal(10,FleetTimetable.Stop([],0,new(minute)).SourceMinutes);
        }
    }

    [Theory]
    [InlineData(FleetRuleAction.Go,false,false,23)]
    [InlineData(FleetRuleAction.Reverse,true,false,21)]
    [InlineData(FleetRuleAction.Forwarding,false,true,23)]
    [InlineData(FleetRuleAction.ReverseForwarding,true,true,21)]
    public void Advanced_actual_signal_actions_and_queue_continue_identically_after_save(FleetRuleAction action,bool reversed,bool forwarding,int x)
    {
        var s=Timetableize(FleetLine(),[new(new(22,20,2),FleetHarborKind.Advanced,0,false,[Rule(action)]),
            new(new(26,20,2),FleetHarborKind.Advanced,0,false,[Rule(action)])]);
        AdvanceTo(s,102);Assert.All(s.Fleet!.Trains,t=>{Assert.Equal(1,t.StopCallCount);Assert.Equal(0,t.MoveCount);});
        Assert.All(s.Clock.Capture().Pending,a=>Assert.Equal(112,a.DueMinute));
        var restored=Restore(SaveCodec.Write(s));AdvanceTo(s,112);AdvanceTo(restored,112);
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));
        var t=s.Fleet!.Trains[0];Assert.Equal(reversed,t.IsReversed);Assert.Equal(forwarding,t.Forwarding);
        Assert.Equal(0,t.StopCallCount);Assert.Equal(1,t.MoveCount);Assert.Equal(x,((FleetCarState.Inside)t.Head.State).Location.X);AssertOccupancy(s);
        AdvanceTo(s,114);AdvanceTo(restored,114);Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));
        Assert.False(s.Fleet.Trains[0].Forwarding);Assert.Equal(2,s.Clock.Capture().Pending.Length);
    }

    [Fact]
    public void Advanced_signal_pass_and_polling_do_not_use_next_match()
    {
        var s=Timetableize(FleetLine(),[new(new(22,20,2),FleetHarborKind.Advanced,0,false,[Rule(FleetRuleAction.Pass)]),
            new(new(26,20,2),FleetHarborKind.Advanced,0,false,[Rule(minute:0)])]);
        AdvanceTo(s,102);Assert.Equal(1,s.Fleet!.Trains[0].MoveCount);Assert.Equal(1,s.Fleet.Trains[1].StopCallCount);
        AdvanceTo(s,112);Assert.Equal(2,s.Fleet.Trains[1].StopCallCount);
        Assert.Equal(120,s.Clock.Capture().Pending.Single(a=>a.HandlerId==MixedSessionState.TrainHandler(s.Fleet.Trains[1].Id)).DueMinute);
        var reopened=Restore(SaveCodec.Write(s));AdvanceTo(s,120);AdvanceTo(reopened,120);
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(reopened));Assert.Equal(1,s.Fleet.Trains[1].MoveCount);
    }

    [Theory]
    [InlineData(1,true)][InlineData(2,false)]
    public void Advanced_junction_uses_current_clock_for_real_head_and_follower(int month,bool curve)
    {
        var s=OperationsLine(length:2,quantity:1);var p=new Location(22,20,2);
        var branch=NormalRailwayConstruction.PreviewBuild(s.World,p,new(19,23,2));
        s=s.Successor(s.Revision,NormalRailwayConstruction.Apply(s.Construction,branch,s.Clock.Time),false);
        s=Timetableize(Place(s,0,new(23,20,2),true),junctions:[new(p,FleetJunctionRoute.Straight,[Rule(FleetRuleAction.Curve,month:month),Rule(FleetRuleAction.Straight)])]);
        var restored=Restore(SaveCodec.Write(s));AdvanceTo(s,104);AdvanceTo(restored,104);
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));
        Assert.Equal(new FleetCarState.Inside(new(21,curve?21:20,2),new(curve?5:6)),s.Fleet!.Trains[0].Head.State);
        Assert.Equal(new FleetCarState.Inside(p,new(6)),s.Fleet.Trains[0].Cars[1].State);AssertOccupancy(s);
    }

    [Fact]
    public async Task Advanced_rule_commands_preserve_duplicate_path_order_copies_and_atomic_fences()
    {
        using var h=new SessionHost(Timetableize(FleetLine(),[new(new(22,20,2),FleetHarborKind.Advanced,0,false)]),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var id=ManagerController(h).Id;var p=new PositionRead(22,20,2);
        EditFleetRuleCommand Edit(string op,int index=-1,FleetRuleRead? rule=null,int offset=0)=>new(id,ManagerController(h).Revision,p,"harbor",op,index,offset,rule);
        var r=new FleetRuleRead(4,23,-1,8,0,"go");
        var add=Edit("add",rule:r);Assert.True(JsonSerializer.SerializeToUtf8Bytes(add).Length<1024);
        var observer=new ParticipantContext("observer",ParticipantRole.Observer);h.RegisterParticipant(observer);
        var before=await FleetSave(h);ManagerRejected(await FleetSend(h,add,observer),"notAuthorized");
        ManagerRejected(await FleetSend(h,add,epoch:h.WorldEpoch+1),"staleEpoch");Assert.Equal(before,await FleetSave(h));
        Assert.IsType<AcceptedMessage>(await FleetSend(h,add));
        before=await FleetSave(h);ManagerRejected(await FleetSend(h,add),"staleRevision");
        ManagerRejected(await FleetSend(h,Edit("add",rule:r with {Action="stop"})),"invalidInput");Assert.Equal(before,await FleetSave(h));
        Assert.IsType<AcceptedMessage>(await FleetSend(h,Edit("copy",0)));
        Assert.IsType<AcceptedMessage>(await FleetSend(h,Edit("replace",0,r with {Action="pass"})));
        Assert.Equal(new[]{"pass","go"},ManagerController(h).Harbors[0].Rules!.Select(a=>a.Action));
        Assert.Equal(23,ManagerController(h).Harbors[0].Rules![0].Day);
        Assert.IsType<AcceptedMessage>(await FleetSend(h,Edit("move",1,offset:-1)));
        Assert.Equal(new[]{"go","pass"},ManagerController(h).Harbors[0].Rules!.Select(a=>a.Action));
        var original=ManagerController(h);Assert.IsType<AcceptedMessage>(await FleetSend(h,ManagerEdit(original,"copy")));
        var copy=ManagerController(h);var originalRules=original.Harbors[0].Rules!;
        Assert.IsType<AcceptedMessage>(await FleetSend(h,new EditFleetRuleCommand(copy.Id,copy.Revision,p,"harbor","clear",-1,0,null)));
        Assert.Null(ManagerController(h).Harbors[0].Rules);
        Assert.Equal(originalRules,h.Snapshot().Mixed!.Fleet!.Controllers.Single(c=>c.Id==id).Harbors[0].Rules);
        var bytes=await FleetSave(h);Assert.Equal(bytes,SaveCodec.Write(SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!));
        Assert.All(h.Snapshot().Mixed!.Fleet!.Trains,t=>Assert.Equal(0,t.MoveCount));
    }

    [Fact]
    public async Task Advanced_junction_edits_require_geometry_preserve_defaults_and_allow_duplicate_masks()
    {
        var s=OperationsLine(quantity:1);var p=new Location(22,20,2);
        var branch=NormalRailwayConstruction.PreviewBuild(s.World,p,new(19,23,2));
        s=s.Successor(s.Revision,NormalRailwayConstruction.Apply(s.Construction,branch,s.Clock.Time),false);
        s=Timetableize(s,junctions:[new(p,FleetJunctionRoute.Curve)]);
        using var h=new SessionHost(s,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var pos=new PositionRead(p.X,p.Y,p.Z);var geometry=h.Snapshot().Mixed!.GeometryRevision;
        EditFleetRuleCommand Edit(string op,int index=-1,FleetRuleRead? rule=null,int offset=0)=>new(ManagerController(h).Id,ManagerController(h).Revision,pos,"junction",op,index,offset,rule,h.Snapshot().Mixed!.GeometryRevision);
        var r=new FleetRuleRead(-1,-1,-1,1,-1,"straight");
        var before=await FleetSave(h);ManagerRejected(await FleetSend(h,Edit("add",rule:r) with {ExpectedGeometryRevision=geometry+1}),"staleRevision");
        ManagerRejected(await FleetSend(h,Edit("add",rule:r with {Minutes=0})),"invalidInput");
        ManagerRejected(await FleetSend(h,Edit("add",rule:r with {Action="go"})),"invalidInput");Assert.Equal(before,await FleetSave(h));
        Assert.IsType<AcceptedMessage>(await FleetSend(h,Edit("add",rule:r)));
        Assert.IsType<AcceptedMessage>(await FleetSend(h,Edit("add",rule:r with {Action="curve"})));
        var c=ManagerController(h);Assert.Equal(new[]{"straight","curve"},c.Junctions![0].Rules!.Select(r=>r.Action));
        // Changing a default route must not replace/drop the ordered rule list.
        Assert.IsType<AcceptedMessage>(await FleetSend(h,new ManageFleetControllerCommand("configure",c.Id,c.Revision,"",null,null,pos,[new(pos,"straight")],h.Snapshot().Mixed!.GeometryRevision)));
        Assert.Equal(c.Junctions[0].Rules,ManagerController(h).Junctions![0].Rules);
        Assert.IsType<AcceptedMessage>(await FleetSend(h,Edit("move",1,offset:-1)));
        Assert.Equal("curve",ManagerController(h).Junctions![0].Rules![0].Action);
        Assert.IsType<AcceptedMessage>(await FleetSend(h,Edit("remove",1)));
        Assert.IsType<AcceptedMessage>(await FleetSend(h,Edit("clear")));
        Assert.Null(ManagerController(h).Junctions![0].Rules);Assert.Equal("straight",ManagerController(h).Junctions![0].DefaultRoute);
    }

    [Fact]
    public async Task Advanced_operator_rule_edits_do_not_replay_or_partially_apply_rules()
    {
        using var h=new SessionHost(Timetableize(FleetLine(),[new(new(22,20,2),FleetHarborKind.Advanced,0,false)]),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        h.RegisterParticipant(FleetOperator);var train=h.Snapshot().Mixed!.Fleet!.Trains[0];
        var c=ManagerController(h);var edit=new EditFleetRuleCommand(c.Id,c.Revision,new(22,20,2),"harbor","add",-1,0,new(-1,-1,-1,-1,-1,"go"));
        var network=new NetworkSession(new(),hostFactory:(_,_)=>h);var peer=network.Connect(FleetNow,FleetOperator)!;
        var message=new CommandMessage("rule-retry",h.WorldEpoch,edit);var pending=network.Submit(peer,message,FleetNow);
        Assert.Same(pending,network.Submit(peer,message,FleetNow));network.Tick(FleetNow);Assert.IsType<AcceptedMessage>(await pending);
        Assert.Single(ManagerController(h).Harbors[0].Rules!);
        var before=await FleetSave(h);var replay=network.Submit(peer,message,FleetNow);network.Tick(FleetNow);Assert.Equal(await pending,await replay);Assert.Equal(before,await FleetSave(h));
    }

    [Fact]
    public async Task Advanced_rule_capacity_is_bounded_and_unchanged_member_revisions_reject_nested_changes()
    {
        var rules=Enumerable.Range(0,1024).Select(_=>Rule()).ToImmutableArray();
        Assert.Throws<ArgumentException>(()=>FleetTimetable.Edit(rules,false,"copy",0,0,null));
        var settings=Enumerable.Range(0,8).Select(x=>new FleetHarborSetting(new(x,20,2),FleetHarborKind.Advanced,0,false,rules)).ToImmutableArray();
        var s=Timetableize(FleetLine(),settings);var f=s.Fleet!;var c=f.Controllers.Last();
        FleetGraph Graph(FleetController replacement)=>new(f.Profile,f.RootGroupId,f.Trains,f.Groups,f.Controllers.Replace(c,replacement));
        Assert.Throws<ArgumentException>(()=>Graph(c with {Harbors=settings.Add(new(new(8,20,2),FleetHarborKind.Advanced,0,false,[Rule()]))}));
        Assert.Throws<ArgumentException>(()=>s.World.Replace(s.World.Revision,s.World.Terrain,s.World.Spatial.Capture(),s.World.Voxels,s.World.Slopes,s.World.SpeedLimits,
            Graph(c with {Harbors=settings.SetItem(0,settings[0] with {Rules=rules.SetItem(0,Rule(FleetRuleAction.Stop))})})));
        var bytes=SaveCodec.Write(s);Assert.Equal(bytes,SaveCodec.Write(Restore(bytes)));
        using var h=new SessionHost(s,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var edit=new EditFleetRuleCommand(c.Id,c.Revision,new(0,20,2),"harbor","remove",0,0,null);
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(edit).Length<1024);
        Assert.IsType<AcceptedMessage>(await FleetSend(h,edit));Assert.Equal(1023,ManagerController(h).Harbors[0].Rules!.Length);
        Assert.Equal(1024,c.Harbors[0].Rules.Length);
    }

    [Theory]
    [InlineData("minute")][InlineData("month")][InlineData("day")][InlineData("action")][InlineData("null")][InlineData("capacity")][InlineData("basic")]
    public void Advanced_malformed_rule_saves_fail_before_reveal(string defect)
    {
        var s=Timetableize(FleetLine(),[new(new(22,20,2),FleetHarborKind.Advanced,0,false,[Rule()])]);
        var doc=JsonNode.Parse(SaveCodec.Write(s))!;var setting=doc["mixed"]!["fleet"]!["controllers"]!.AsArray().Last()!["harbors"]![0]!;
        var rules=setting["rules"]!.AsArray();
        switch(defect) {
            case "minute":rules[0]!["minutes"]=1;break;
            case "month":rules[0]!["month"]=0;break;
            case "day":rules[0]!["day"]=32;break;
            case "action":rules[0]!["action"]="unknown";break;
            case "null":rules[0]=null;break;
            case "capacity":while(rules.Count<=1024)rules.Add(rules[0]!.DeepClone());break;
            case "basic":setting["kind"]="fixed";break;
        }
        Assert.Throws<InvalidDataException>(()=>SaveCodec.ReadWorld(Encoding.UTF8.GetBytes(doc.ToJsonString()),TerrainProfiles.Resolve,RailwayProfiles.Resolve));
    }
}
