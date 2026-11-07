// ExAV 883cf7d: TimeMask, AdvancedStationHandler, Junction and their forms.
// Original authors; LGPL-2.1-or-later. TRAIN-D005 restores the actual day on edit.
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

// TURNING actions reverse2/rforwarding2 require P5-21's handler-side geometry.
// They are deliberately not admitted as aliases for ordinary reversal.
public enum FleetRuleAction { Pass, Stop, Go, Reverse, Forwarding, ReverseForwarding, Straight, Curve }
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
            r.Action is FleetRuleAction.Pass or FleetRuleAction.Stop or FleetRuleAction.Go or FleetRuleAction.Reverse or FleetRuleAction.Forwarding or FleetRuleAction.ReverseForwarding);
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

    public static FleetStopDecision Stop(ImmutableArray<FleetTimetableRule> rules,int callCount,GameTime time)
    {
        foreach(var rule in rules.IsDefault?[]:rules)
        {
            if(!rule.Matches(time))continue;
            if(callCount==0) {
                if(rule.Action==FleetRuleAction.Pass)return FleetStopDecision.Proceed;
                if(rule.Action==FleetRuleAction.Stop)return FleetStopDecision.Wait(10);
            } else switch(rule.Action) {
                case FleetRuleAction.Go:return FleetStopDecision.Proceed;
                case FleetRuleAction.Reverse:return FleetStopDecision.Reverse;
                case FleetRuleAction.Forwarding:return FleetStopDecision.Forwarding;
                case FleetRuleAction.ReverseForwarding:return FleetStopDecision.ReverseForwarding;
                case FleetRuleAction.Stop:return FleetStopDecision.Wait(10-time.Minute%10);
            }
        }
        return FleetStopDecision.Wait(callCount==0?10:10-time.Minute%10);
    }
}
