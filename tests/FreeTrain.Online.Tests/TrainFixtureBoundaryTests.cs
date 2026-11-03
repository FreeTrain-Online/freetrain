using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

// P5-02 literals exercise existing P1/P3/P4 interfaces only. These are not
// movement/controller tests; those consumers belong to their later P5 slices.
public class TrainFixtureBoundaryTests
{
    static JsonDocument Fixtures() => JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spatial/train-fixtures.json")));
    static IEnumerable<JsonElement> Cases(JsonDocument f,string name) => f.RootElement.GetProperty("groups").GetProperty(name).GetProperty("cases").EnumerateArray();
    static int Int(JsonElement e,string key) => e.GetProperty(key).GetInt32();
    static Location Point(JsonElement e) => new(e[0].GetInt32(),e[1].GetInt32(),e[2].GetInt32());

    [Fact]
    public void All_surface_and_underground_ramp_shifts_consume_literal_current_rail_zdiff_and_reciprocity()
    {
        using var f=Fixtures();var count=0;
        foreach(var c in Cases(f,"ramps")) {
            var i=c.GetProperty("input");var h=new Heading(Int(i,"heading"));var expected=c.GetProperty("expected").GetProperty("steps");
            var prior=Point(i.GetProperty("cars")[0].GetProperty("location"));
            for(var n=0;n<3;n++) {
                var p=i.GetProperty("patterns")[n].GetInt32();
                var next=prior.Move(h);next=next with { Z=next.Z+SlopeRailwayConstruction.ZDiff(p,h) };
                Assert.True(RailwayTopology.HasPort(i.GetProperty("patterns")[n+1].GetInt32(),h.Opposite));
                Assert.Equal(Point(expected[n].GetProperty("cars")[0].GetProperty("location")),next);
                Assert.Equal(Point(expected[n].GetProperty("cars")[1].GetProperty("location")),prior);
                prior=next;
            }
            count++;
        }
        Assert.Equal(16,count);
    }

    [Fact]
    public void Signal_delegation_uses_the_literal_heading_cases_and_shared_harbor_boundary()
    {
        using var f=Fixtures();var count=0;
        foreach(var c in Cases(f,"signals")) {
            var i=c.GetProperty("input");var e=c.GetProperty("expected");var trains=i.GetProperty("trains");
            for(var n=0;n<trains.GetArrayLength();n++) {
                var delegates=SignalRailwayConstruction.DelegatesStopTime(Int(i,"direction"),new(Int(trains[n],"heading")));
                Assert.Equal(e.GetProperty("waits")[n].GetInt32(),delegates ? Int(trains[n],"wait") : 0);
            }
            count++;
        }
        Assert.Equal(9,count);
    }

    [Fact]
    public void Eight_visible_world_exits_match_source_border_before_destination_track_lookup()
    {
        using var f=Fixtures();var count=0;
        foreach(var c in Cases(f,"exits")) {
            var i=c.GetProperty("input");var size=i.GetProperty("size");var e=c.GetProperty("expected");
            var dimensions=new WorldDimensions(size[0].GetInt32(),size[1].GetInt32(),size[2].GetInt32());
            var world=new TerrainWorld(TerrainProfiles.Bundled,"Exit",dimensions,0,
                Enumerable.Repeat(new TerrainCell(2),dimensions.H*dimensions.V),new(1,[]),[]);
            var car=i.GetProperty("car");var before=Point(car.GetProperty("location"));
            var after=before.Move(new(Int(car,"heading")));
            Assert.False(world.IsTrainBorder(before));Assert.True(world.IsTrainBorder(after));
            Assert.Equal(Point(e.GetProperty("car").GetProperty("location")),after);
            var storage=world.Space.ToStorage(after);var literal=e.GetProperty("storage");
            Assert.Equal(new StoragePosition(literal[0].GetInt32(),literal[1].GetInt32(),literal[2].GetInt32()),storage);count++;
        }
        Assert.Equal(8,count);
    }

    [Fact]
    public void Clock_consumes_literal_equal_time_retry_and_executing_cancellation_order()
    {
        using var f=Fixtures();
        foreach(var c in Cases(f,"queue")) {
            var i=c.GetProperty("input");var e=c.GetProperty("expected");var calls=new List<string>();Clock clock=null!;
            var executing=i.GetProperty("executing").EnumerateArray().ToArray();
            var pending=i.GetProperty("pending").EnumerateArray().ToArray();
            var handlers=executing.Concat(pending).Select(r=>r.GetProperty("handler").GetString()!).Distinct()
                .ToDictionary(id=>id,id=>(Action)(()=>calls.Add(id)));
            foreach(var row in executing) {
                var captured=row;var id=row.GetProperty("handler").GetString()!;
                handlers[id]=()=>{calls.Add(id);if(captured.TryGetProperty("cancel",out var cancel))clock.UnregisterOneShots(cancel.GetString()!);
                    clock.Schedule(id,Int(captured,"delay"));};
            }
            ScheduledAction Entry(JsonElement r) => new(Int(r,"order"),Int(r,"order"),Int(r,"due"),r.GetProperty("handler").GetString()!,0);
            clock=Clock.Restore(new(Int(i,"now")-1,Int(i,"nextOrder"),executing.Concat(pending).Select(Entry).ToImmutableArray()),handlers);
            clock.Advance();var state=clock.Capture();
            Assert.Equal(e.GetProperty("calls").EnumerateArray().Select(r=>r.GetString()),calls);
            Assert.Equal(e.GetProperty("pending").EnumerateArray().Select(Entry),state.Pending);
            Assert.Equal(Int(e,"nextOrder"),state.NextOrder);
        }
    }
}
