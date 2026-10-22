using System.Collections.Immutable;
using FreeTrain.Online.Simulation;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using Xunit;

public class ClockTests
{
    // Independent source-calendar vectors: no DateTime or production calendar helpers.
    [Theory]
    [InlineData(0,1,1,1,0,0,0,Season.Winter,DayNight.Night)]
    [InlineData(130080,1,4,1,6,8,0,Season.Spring,DayNight.Day)]
    [InlineData(84959,1,2,28,2,23,59,Season.Winter,DayNight.Night)]
    [InlineData(84960,1,3,1,3,0,0,Season.Spring,DayNight.Night)]
    [InlineData(218880,1,6,2,5,0,0,Season.Summer,DayNight.Night)]
    [InlineData(349920,1,9,1,5,0,0,Season.Autumn,DayNight.Night)]
    [InlineData(480960,1,12,1,5,0,0,Season.Winter,DayNight.Night)]
    [InlineData(525599,1,12,31,0,23,59,Season.Winter,DayNight.Night)]
    [InlineData(525600,2,1,1,1,0,0,Season.Winter,DayNight.Night)]
    [InlineData(1661760,4,3,1,6,0,0,Season.Spring,DayNight.Night)]
    [InlineData(359,1,1,1,0,5,59,Season.Winter,DayNight.Night)]
    [InlineData(360,1,1,1,0,6,0,Season.Winter,DayNight.Day)]
    [InlineData(1079,1,1,1,0,17,59,Season.Winter,DayNight.Day)]
    [InlineData(1080,1,1,1,0,18,0,Season.Winter,DayNight.Night)]
    public void Fixed_calendar_vectors(long minute,int year,int month,int day,int weekday,int hour,int minutes,Season season,DayNight light)
    {
        var time=new GameTime(minute);
        Assert.Equal((year,month,day,weekday,hour,minutes,season,light),
            (time.YearNumber,time.Month,time.DayOfMonth,time.DayOfWeek,time.HourOfDay,time.Minute,time.Season,time.DayOrNight));
        Assert.Equal(weekday is 0 or 6,time.IsWeekend);
        Assert.Equal(minute-130080,time.ElapsedMinutes);
    }
    [Fact]
    public void All_source_holidays_and_vacation_edges_are_exact_and_repeat_without_leap_days()
    {
        var holidays=new[]{1,2,3,15,42,80,119,123,124,125,201,258,266,283,307,327,357,364,365};
        // Inclusive day-of-year ranges, independently counted from the source month predicates.
        var vacations=new[]{(1,5),(84,95),(206,243),(359,365)};
        for(var day=1;day<=365;day++)foreach(var year in new[]{0,3})
        {
            var time=new GameTime((year*365L+day-1)*1440);
            Assert.Equal(holidays.Contains(day),time.IsHoliday);
            Assert.Equal(vacations.Any(r=>day>=r.Item1&&day<=r.Item2),time.IsVacation);
        }
        Assert.Equal(1440,new GameTime(1440).MinutesUntilTomorrow);
        Assert.Equal(1,new GameTime(1439).MinutesUntilTomorrow);
        Assert.Throws<ArgumentOutOfRangeException>(()=>new GameTime(-1));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new GameTime(GameTime.MaximumMinute).AddMinutes(1));
    }
    [Fact]
    public void Daylight_precedes_fifo_due_actions_then_detached_end_of_turn_batch()
    {
        var trace=new List<string>();
        Clock clock=null!;
        clock=new(359,new Dictionary<string,Action>{
            ["a"]=()=>{trace.Add("a");clock.Schedule("later",1);clock.AtEndOfTurn(()=>trace.Add("from-a"));},
            ["b"]=()=>trace.Add("b"),["later"]=()=>trace.Add("later")});
        clock.DayNightChanged+=()=>trace.Add("daylight");
        clock.Schedule("b",2);clock.Schedule("a",1);clock.Schedule("b",1);
        clock.AtEndOfTurn(()=>{trace.Add("eot");clock.AtEndOfTurn(()=>trace.Add("next-eot"));});
        clock.Advance();Assert.Equal(new[]{"daylight","a","b","eot","from-a"},trace);
        trace.Clear();clock.Advance();Assert.Equal(new[]{"b","later","next-eot"},trace);
        trace.Clear();clock.Advance();Assert.Empty(trace);
    }
    [Fact]
    public void Removal_during_callback_never_drops_neighbor_and_self_cancel_stops_repetition()
    {
        var trace=new List<string>();long repeated=0,other=0;
        Clock clock=null!;
        clock=new(0,new Dictionary<string,Action>{
            ["self"]=()=>{trace.Add("self");clock.Unregister(repeated);clock.Unregister(other);},
            ["neighbor"]=()=>trace.Add("neighbor"),["removed"]=()=>trace.Add("removed")});
        repeated=clock.ScheduleRepeated("self",1,2);clock.Schedule("neighbor",1);other=clock.Schedule("removed",1);
        clock.Advance();clock.Advance();clock.Advance();Assert.Equal(new[]{"self","neighbor"},trace);Assert.Empty(clock.Capture().Pending);
    }
    [Fact]
    public void Named_one_shot_removal_and_repeated_cookies_have_distinct_identity()
    {
        var calls=0;var clock=new Clock(0,new Dictionary<string,Action>{{"a",()=>calls++}});
        clock.Schedule("a",1);clock.Schedule("a",2);var repeated=clock.ScheduleRepeated("a",1);
        clock.UnregisterOneShots("a");clock.Advance();clock.Advance();Assert.Equal(2,calls);
        clock.Unregister(repeated);clock.Advance();Assert.Equal(2,calls);
    }
    [Fact]
    public void Repeat_reschedules_after_callback_registrations_and_restore_keeps_tie_order()
    {
        Clock first=null!;var trace=new List<string>();
        first=new(0,new Dictionary<string,Action>{["repeat"]=()=>{trace.Add("repeat");first.Schedule("one",2);},["one"]=()=>trace.Add("one")});
        first.ScheduleRepeated("repeat",1,2);first.Schedule("one",3);first.Advance();
        var state=first.Capture();Assert.Equal(new long[]{1,2,3},state.Pending.Select(x=>x.Order));
        Assert.Equal(new[]{"one","one","repeat"},state.Pending.Select(x=>x.HandlerId));
        // Serialized named actions are rebound to a new world, never old delegates.
        state=System.Text.Json.JsonSerializer.Deserialize<ClockState>(System.Text.Json.JsonSerializer.Serialize(state,WireJson.CreateOptions()),WireJson.CreateOptions())!;
        Clock second=null!;var continued=new List<string>();
        second=Clock.Restore(state,new Dictionary<string,Action>{["repeat"]=()=>{continued.Add("repeat");second.Schedule("one",2);},["one"]=()=>continued.Add("one")});
        trace.Clear();for(var i=0;i<4;i++){first.Advance();second.Advance();}
        Assert.Equal(trace,continued);Assert.Equal(first.Capture().Pending.ToArray(),second.Capture().Pending.ToArray());
        Assert.Equal(first.NextOrder,second.NextOrder);Assert.Equal(first.Time,second.Time);
    }
    [Fact]
    public void Invalid_scheduling_restore_overflow_and_reentry_fail_without_cross_world_state()
    {
        Clock clock=null!;
        clock=new(0,new Dictionary<string,Action>{{"a",()=>Assert.Throws<InvalidOperationException>(()=>clock.Advance())}});
        Assert.Throws<ArgumentOutOfRangeException>(()=>clock.Schedule("a",0));
        Assert.Throws<ArgumentOutOfRangeException>(()=>clock.Schedule("a",-1));
        Assert.Throws<ArgumentOutOfRangeException>(()=>clock.Schedule("a",1,-1));
        Assert.Throws<ArgumentOutOfRangeException>(()=>clock.ScheduleRepeated("a",0));
        Assert.Throws<ArgumentOutOfRangeException>(()=>clock.ScheduleRepeated("a",1,-1));
        Assert.Throws<ArgumentException>(()=>clock.Schedule("missing",1));
        clock.Schedule("a",1);clock.Advance();
        var state=new ClockState(0,1,[new(0,0,1,"a",0)]);
        Assert.Throws<ArgumentException>(()=>Clock.Restore(state));
        var bindings=new Dictionary<string,Action>{{"a",()=>{}}};
        foreach(var action in new[]{new ScheduledAction(0,0,0,"a",0),new(0,1,1,"a",0),new(0,0,1,"a",-1)})
            Assert.Throws<ArgumentException>(()=>Clock.Restore(state with{Pending=[action]},bindings));
        Assert.Throws<ArgumentException>(()=>Clock.Restore(state with{Pending=[state.Pending[0],state.Pending[0]]},bindings));
        var saturated=Clock.Restore(new(0,long.MaxValue,[]),bindings);
        Assert.Throws<OverflowException>(()=>saturated.Schedule("a",1));Assert.Empty(saturated.Capture().Pending);
        var max=new Clock(GameTime.MaximumMinute);Assert.Throws<ArgumentOutOfRangeException>(()=>max.Advance());
        Assert.Equal(GameTime.MaximumMinute,max.Time.AbsoluteMinute);
    }
    [Fact]
    public async Task Sessions_have_independent_time_roles_pause_bounds_and_canonical_save_continuation()
    {
        var a=new SessionHost(SpatialReferenceScenario.Create(),"owner");var b=new SessionHost(SpatialReferenceScenario.Create(),"owner");
        async Task<WireMessage> Send(CommandPayload payload){var task=a.Enqueue(new("owner"),new(Guid.NewGuid().ToString(),a.WorldEpoch,payload));a.Pump();return await task;}
        await Send(new StepCommand());Assert.Equal(130081,a.Snapshot().Tick);Assert.Equal(130080,b.Snapshot().Tick);
        await Send(new RunCommand());Assert.IsType<RejectedMessage>(await Send(new StepCommand()));
        Assert.Equal(256,a.Pump(256).AdvancedTicks);Assert.Equal(0,b.Pump(256).AdvancedTicks);
        a.RequestInputReset(false);a.Pump();Assert.False(a.Snapshot().Paused);
        a.RequestInputReset(true);a.Pump();Assert.True(a.Snapshot().Paused);
        var bytesTask=a.CaptureSave();a.Pump();var bytes=await bytesTask;
        var restored=SaveCodec.Read(bytes);Assert.True(restored.Paused);Assert.Equal(a.Snapshot().Tick,restored.Clock.Time.AbsoluteMinute);
        var load=b.LoadSave(bytes);b.Pump();await load;Assert.Equal(a.Snapshot().Tick,b.Snapshot().Tick);
        var observer=new ParticipantContext("observer",ParticipantRole.Observer);a.RegisterParticipant(observer);
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await a.Enqueue(observer,new("o",1,new RunCommand()))).Code);
        // Explicit steps, including Pump(0), consume the same hard per-pump bound.
        for(var i=0;i<256;i++)_ = a.Enqueue(new("owner"),new("s"+i,1,new StepCommand()));
        Assert.Equal(64,a.Pump(256).AdvancedTicks);
        Assert.Equal(64,a.Pump(0).AdvancedTicks);
    }
    [Fact]
    public void Current_save_clock_validation_is_atomic()
    {
        var modern=SaveCodec.Write(SpatialReferenceScenario.Create());
        Assert.Equal(modern,SaveCodec.Write(SaveCodec.Read(modern)));
        foreach(var mutation in new[]{"negative","tooLarge","number","unbound","duplicate","missing","null","order"})
        {
            var node=System.Text.Json.Nodes.JsonNode.Parse(modern)!;
            switch(mutation)
            {
                case "negative":node["clock"]!["absoluteMinute"]="-1";break;
                case "tooLarge":node["clock"]!["absoluteMinute"]=(GameTime.MaximumMinute+1).ToString();break;
                case "number":node["clock"]!["absoluteMinute"]=130080;break;
                case "unbound":node["clock"]!["nextOrder"]="1";node["clock"]!["pending"]=System.Text.Json.Nodes.JsonNode.Parse("[{\"registration\":\"0\",\"order\":\"0\",\"dueMinute\":\"130081\",\"handlerId\":\"unknown\",\"interval\":\"0\"}]");break;
                case "duplicate":node["clock"]!["extra"]=0;break;
                case "missing":node["clock"]!.AsObject().Remove("pending");break;
                case "null":node["clock"]=null;break;
                case "order":node["clock"]!["nextOrder"]="-1";break;
            }
            Assert.Throws<InvalidDataException>(()=>SaveCodec.Read(System.Text.Encoding.UTF8.GetBytes(node.ToJsonString())));
        }
    }
    [Fact]
    public void End_of_turn_is_transient_and_failed_callbacks_require_recovery_without_replay()
    {
        Clock clock=null!;var calls=0;
        clock=new(0,new Dictionary<string,Action>{["bad"]=()=>throw new InvalidOperationException("handler"),["good"]=()=>calls++});
        clock.Schedule("bad",1);clock.Schedule("good",1);
        Assert.Throws<InvalidOperationException>(()=>clock.Advance());
        Assert.Equal(1,clock.Time.AbsoluteMinute);
        Assert.Throws<InvalidOperationException>(()=>clock.Capture());
        Assert.Throws<InvalidOperationException>(()=>clock.Advance());
        // Failed minute is not replayed. Caller must discard/recover a failed world.
        var restored=Clock.Restore(new(1,0,[]));
        restored.AtEndOfTurn(()=>calls++);
        var copy=Clock.Restore(restored.Capture());copy.Advance();Assert.Equal(0,calls);
    }

    [Fact]
    public async Task Maximum_calendar_boundary_stays_paused_and_rejects_further_progression()
    {
        var world=SpatialReferenceScenario.Create();world.RestoreTime(new(GameTime.MaximumMinute,0,[]),true);
        var host=new SessionHost(world,"owner");
        foreach(var payload in new CommandPayload[]{new StepCommand(),new RunCommand()})
        {
            var result=host.Enqueue(new("owner"),new(Guid.NewGuid().ToString(),1,payload));
            Assert.Equal(0,host.Pump(256).AdvancedTicks);
            Assert.Equal("invalidInput",Assert.IsType<RejectedMessage>(await result).Code);
        }
        Assert.True(host.Snapshot().Paused);Assert.Equal(GameTime.MaximumMinute,host.Snapshot().Tick);
        Assert.Equal(0,host.Snapshot().Revision);
        Assert.Equal((int.MaxValue,12,31,23,59),(world.Clock.Time.YearNumber,world.Clock.Time.Month,world.Clock.Time.DayOfMonth,world.Clock.Time.HourOfDay,world.Clock.Time.Minute));
    }

}
