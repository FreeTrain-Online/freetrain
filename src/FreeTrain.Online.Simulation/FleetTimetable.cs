// ExAV 883cf7d: TimeMask, AdvancedStationHandler, Junction and their forms.
// Original authors; LGPL-2.1-or-later. TRAIN-D005 restores the actual day on edit.
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public enum FleetRuleAction { Pass, Stop, Go, Reverse, Forwarding, ReverseForwarding, Straight, Curve, Reverse2, RForwarding2 }
public sealed record FleetTimetableRule(int Month, int Day, int DayOfWeek, int Hour, int Minutes, FleetRuleAction Action)
{
    public bool Matches(GameTime time) => (Month==-1 || Month==time.Month) && (Day==-1 || Day==time.DayOfMonth) &&
        (DayOfWeek==-1 || DayOfWeek==time.DayOfWeek) && (Hour==-1 || Hour==time.HourOfDay) && (Minutes==-1 || Minutes==time.Minute);
    public bool SameMask(FleetTimetableRule other) => this with {Action=other.Action} == other;
}

public static class FleetTimetable
{
    public const int MaximumLocationRules=1024, MaximumControllerRules=8192;
    public static bool EqualSettings(FleetController a,FleetController b)
    {
        var aj=a.Junctions.IsDefault?[]:a.Junctions;var bj=b.Junctions.IsDefault?[]:b.Junctions;
        var ah=a.Harbors.IsDefault?[]:a.Harbors;var bh=b.Harbors.IsDefault?[]:b.Harbors;
        bool Equal(ImmutableArray<FleetTimetableRule> x,ImmutableArray<FleetTimetableRule> y)=>(x.IsDefault?[]:x).SequenceEqual(y.IsDefault?[]:y);
        return aj.Length==bj.Length && ah.Length==bh.Length &&
            aj.Zip(bj).All(p=>p.First with {Rules=p.Second.Rules}==p.Second && Equal(p.First.Rules,p.Second.Rules)) &&
            ah.Zip(bh).All(p=>p.First with {Rules=p.Second.Rules}==p.Second && Equal(p.First.Rules,p.Second.Rules));
    }
    public static bool ValidRule(FleetTimetableRule r,bool junction) => r!=null &&
        (r.Month==-1 || r.Month is >=1 and <=12) && (r.Day==-1 || r.Day is >=1 and <=31) &&
        r.DayOfWeek is >=-1 and <=6 && r.Hour is >=-1 and <=23 &&
        (junction ? r.Minutes==-1 && r.Action is FleetRuleAction.Straight or FleetRuleAction.Curve :
            (r.Minutes==-1 || r.Minutes is >=0 and <=50 && r.Minutes%10==0) &&
            r.Action is FleetRuleAction.Pass or FleetRuleAction.Stop or FleetRuleAction.Go or FleetRuleAction.Reverse or FleetRuleAction.Forwarding or FleetRuleAction.ReverseForwarding or FleetRuleAction.Reverse2 or FleetRuleAction.RForwarding2);
    public static bool ValidRules(ImmutableArray<FleetTimetableRule> rules,bool junction) =>
        rules.IsDefault || rules.Length<=MaximumLocationRules && rules.All(r=>ValidRule(r,junction));

    // Add checks equal station masks, regardless of action. Replace/copy (and
    // P5-23 validated file append) intentionally bypass that source UI check.
    public static ImmutableArray<FleetTimetableRule> Edit(ImmutableArray<FleetTimetableRule> rules,bool junction,
        string operation,int index,int offset,FleetTimetableRule? rule)
    {
        if(rules.IsDefault)rules=[];
        if(rule!=null && !ValidRule(rule,junction))throw new ArgumentException("Invalid timetable rule.");
        var selected=index>=0 && index<rules.Length;
        var result=operation switch {
            "add" when rule!=null && index==-1 && offset==0 && (junction || !rules.Any(r=>r.SameMask(rule))) => rules.Add(rule),
            "replace" when rule!=null && selected && offset==0 => rules.SetItem(index,rule),
            "copy" when rule==null && selected && offset==0 => rules.Add(rules[index] with {}),
            "move" when rule==null && selected && offset is -1 or 1 && index+offset>=0 && index+offset<rules.Length => rules.RemoveAt(index).Insert(index+offset,rules[index]),
            "remove" when rule==null && selected && offset==0 => rules.RemoveAt(index),
            "clear" when rule==null && index==-1 && offset==0 => [],
            _ => throw new ArgumentException("Invalid timetable edit.")
        };
        if(result.Length>MaximumLocationRules)throw new ArgumentException("Timetable rule capacity exceeded.");
        return result;
    }

    // Validated file append bypasses only Add's duplicate-mask guard. Rule
    // domain/capacity and controller graph validation still apply atomically.
    public static ImmutableArray<FleetTimetableRule> Append(ImmutableArray<FleetTimetableRule> rules, string encoded)
    {
        if(encoded==null || encoded.Length is <8 or >8192 || encoded.Length%8!=0 ||
            encoded.Any(c=>!char.IsAsciiLetterOrDigit(c) && c is not ('+' or '/')))
            throw new ArgumentException("Invalid append encoding.");
        var bytes=Convert.FromBase64String(encoded);
        FleetRuleAction[] actions=[FleetRuleAction.Pass,FleetRuleAction.Stop,FleetRuleAction.Go,FleetRuleAction.Reverse,
            FleetRuleAction.Forwarding,FleetRuleAction.ReverseForwarding,FleetRuleAction.Reverse2,FleetRuleAction.RForwarding2];
        if(rules.IsDefault)rules=[];
        if(bytes.Length%6!=0 || rules.Length+bytes.Length/6>MaximumLocationRules)throw new ArgumentException("Timetable rule capacity exceeded.");
        var result=rules.ToBuilder();
        for(var i=0;i<bytes.Length;i+=6) {
            if(bytes[i+5]>=actions.Length)throw new ArgumentException("Invalid append action.");
            var r=new FleetTimetableRule((sbyte)bytes[i],(sbyte)bytes[i+1],(sbyte)bytes[i+2],(sbyte)bytes[i+3],(sbyte)bytes[i+4],actions[bytes[i+5]]);
            if(!ValidRule(r,false))throw new ArgumentException("Invalid append rule.");
            result.Add(r);
        }
        return result.ToImmutable();
    }

    private static FleetRuleAction Select(ImmutableArray<FleetTimetableRule> rules,int callCount,GameTime time)
    {
        foreach(var rule in rules.IsDefault?[]:rules)
        {
            if(!rule.Matches(time))continue;
            if(callCount==0) {
                if(rule.Action is FleetRuleAction.Pass or FleetRuleAction.Stop)return rule.Action;
            } else if(rule.Action!=FleetRuleAction.Pass)return rule.Action;
        }
        return FleetRuleAction.Stop;
    }

    private static FleetStopDecision Decision(FleetRuleAction action,int callCount,GameTime time) => action switch {
        FleetRuleAction.Pass or FleetRuleAction.Go => FleetStopDecision.Proceed,
        FleetRuleAction.Reverse => FleetStopDecision.Reverse,
        FleetRuleAction.Forwarding => FleetStopDecision.Forwarding,
        FleetRuleAction.ReverseForwarding => FleetStopDecision.ReverseForwarding,
        FleetRuleAction.Stop => FleetStopDecision.Wait(callCount==0?10:10-time.Minute%10),
        _ => throw new ArgumentException("Turning requires the train and world.")
    };
    public static FleetStopDecision Stop(ImmutableArray<FleetTimetableRule> rules,int callCount,GameTime time)
        => Decision(Select(rules,callCount,time),callCount,time);

    public static FleetStopResult Stop(MixedWorld world,FleetTrain train,ImmutableArray<FleetTimetableRule> rules,int callCount,GameTime time)
    {
        var action=Select(rules,callCount,time);
        return action is FleetRuleAction.Reverse2 or FleetRuleAction.RForwarding2
            ? FleetTurning.CheckTurn(world,train,action==FleetRuleAction.Reverse2?-1:-1000,time)
            : new(train,Decision(action,callCount,time));
    }
}
