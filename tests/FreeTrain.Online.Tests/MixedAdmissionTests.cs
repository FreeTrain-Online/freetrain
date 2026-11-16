using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Server;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public sealed class MixedAdmissionTests
{
    private static readonly JsonSerializerOptions Json = WireJson.CreateOptions();
    private static MixedRead FreshRead(MixedSessionState state) => MixedWire.Read(state.Construction) with {
        Queue = state.Fleet == null && state.World.Stations == null && state.World.City == null
            ? null : FleetWire.Queue(state.Clock.Capture())
    };
    private static void Check(MixedSessionState state)
    {
        var expected = JsonSerializer.SerializeToUtf8Bytes(FreshRead(state), Json).LongLength;
        var sizes = MixedWire.AdmissionBytes(state);
        Assert.Equal(expected, sizes.Mixed);
        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(MixedWire.RailwayEntities(state.World), Json).LongLength, sizes.Railway);
        MixedWire.ValidateAdmission(state);
    }

    private static MixedSessionState Extent()
    {
        var request = new EmptyWorldRequest(WorldCreationProfile.Id,"Extent heat admission",new(1500,1090,45),2);
        var terrain = TerrainWorld.Empty(request,TerrainProfiles.Bundled);
        return new(new(MixedWorld.FromTerrain(RailwayProfiles.Bundled,terrain)),TerrainProvenance.From(request));
    }

    [Fact]
    public void Extent_heat_activation_deposit_and_diffusion_fit_saves_snapshots_and_deltas()
    {
        var before = Extent();
        var active = CityHeat.Enable(before);
        Check(active);
        Assert.True(MixedWire.AdmissionBytes(active).Mixed > 8 * 1024 * 1024);
        CheckTransition(before,active);

        var location = active.World.Terrain.Space.ToLocation(new(10,10,2));
        var deposited = active.Successor(active.Revision,
            new(CityHeat.Deposit(active.World,location,4),active.Construction.Accounts),false);
        CheckTransition(active,deposited);

        // Move to the saved timer boundary without simulating 599 empty minutes.
        var timer = deposited.World.City!.State.HeatCallback!;
        var clock = deposited.Clock.Capture() with { AbsoluteMinute = timer.DueMinute-1 };
        var state = new MixedSessionState(deposited.Construction,deposited.Provenance,
            clock:clock,random:deposited.Random.Capture(),revision:deposited.Revision);
        var bytes = SaveCodec.Write(state);
        var reopened = SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
        Assert.Equal(bytes,SaveCodec.Write(reopened));
        var previous = state.Freeze();
        state.AdvanceOneMinute();reopened.AdvanceOneMinute();
        Assert.Equal(timer.DueMinute,state.Clock.Time.AbsoluteMinute);
        Assert.Equal(timer.DueMinute+timer.Interval,state.World.City!.State.HeatCallback!.DueMinute);
        Assert.NotEqual(previous.World.City!.State.Heat.Q,state.World.City.State.Heat.Q);
        Check(state);
        CheckTransition(previous,state);
        Assert.Equal(SaveCodec.Write(state),SaveCodec.Write(reopened));
        var packet = StateTransfer.Serialize(MixedWire.Snapshot(state,1));
        Assert.True(packet.Length <= ProtocolIdentity.MaximumStateBytes);
        Assert.Equal(packet.Length,StateTransfer.Chunks(MixedWire.Snapshot(state,1),packet).Sum(c=>c.Data.Length));

        static void CheckTransition(MixedSessionState from,MixedSessionState to)
        {
            MixedWire.ValidateTransition(from,to);
            var delta = StateDifference.Between(MixedWire.Snapshot(from,1),MixedWire.Snapshot(to,1));
            Assert.True(OutboundWire.Serialize(delta).Length <= OutboundWire.MaximumBytes);
        }
    }

    [Theory]
    [InlineData("small")]
    [InlineData("large")]
    public void Section_counts_match_full_encoding_across_heat_and_construction_callbacks(string scene)
    {
        var state = BackendPerformanceFixture.Open(scene);
        for (var i = 0; i < 12; i++)
        {
            Check(state);
            state.AdvanceOneMinute();
        }
        Check(state);
    }

    [Fact]
    public void Queue_changes_without_revision_or_world_change_are_counted_afresh()
    {
        var state = BackendPerformanceFixture.Open("small");
        Check(state);
        var revision = state.Revision;
        var before = MixedWire.AdmissionBytes(state);
        var registration = state.Clock.Schedule(state.Clock.Capture().Pending[0].HandlerId, 100);
        Assert.Equal(revision, state.Revision);
        Assert.True(MixedWire.AdmissionBytes(state).Mixed > before.Mixed);
        Check(state);
        state.Clock.Unregister(registration);
        Check(state); // NextOrder still changed.
    }

    [Fact]
    public void Accounts_and_heat_with_equal_revisions_use_their_own_identities()
    {
        var state = BackendPerformanceFixture.Open("small");
        Check(state);
        var accountState = state.Construction.Accounts.Capture();
        var accounts = ConstructionAccounts.Restore(accountState with {
            Transactions = accountState.Transactions.Select(t => t.WorldRevision == accountState.Transactions[0].WorldRevision
                ? t with { Operation = "quote \" \\ å雪😀" } : t).ToImmutableArray()
        });
        var changed = new MixedSessionState(new(state.World, accounts), state.Provenance,
            clock: state.Clock.Capture(), random: state.Random.Capture(), revision: state.Revision);
        Check(changed);
        Assert.NotEqual(MixedWire.AdmissionBytes(state).Mixed, MixedWire.AdmissionBytes(changed).Mixed);

        var world = state.World;
        var city = world.City!;
        var heat = city.State.Heat;
        heat = heat with { Q = heat.Q.SetItem(0, -0f).SetItem(1, float.Epsilon).SetItem(2, float.MaxValue) };
        city = new(city.Profile, city.State with { Heat = heat });
        world = new(world.Profile, world.Terrain, world.Spatial.Capture(), world.Voxels, world.Slopes,
            world.Revision, world.SpeedLimits, world.Fleet, world.Stations, city);
        changed = new(new(world, accounts), state.Provenance, clock: state.Clock.Capture(),
            random: state.Random.Capture(), revision: state.Revision);
        Check(changed);
        Check(state);
    }

    [Fact]
    public void Public_wire_array_mutation_does_not_certify_admission_sizes()
    {
        var state = BackendPerformanceFixture.Open("small");
        var read = MixedWire.Read(state);
        read.Voxels[0] = read.Voxels[0] with { Sprite = int.MaxValue };
        read.Accounts.Transactions[0] = read.Accounts.Transactions[0] with { Operation = new('x', 128) };
        Check(state); // First fill occurs AFTER public projections were modified.
        read.Accounts.Transactions[0] = read.Accounts.Transactions[0] with { Operation = "changed again" };
        Check(state);
    }

    [Fact]
    public void Worlds_without_optional_sections_match_full_encoding()
    {
        var state = SaveCodec.ReadWorld(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Saves/mixed-v11.json")),
            FreeTrain.Online.Content.TerrainProfiles.Resolve,FreeTrain.Online.Content.RailwayProfiles.Resolve).Mixed!;
        Assert.Null(state.World.City);
        Assert.Null(FreshRead(state).Queue);
        Check(state);
    }

    [Fact]
    public void Oversized_heat_is_rejected_after_a_smaller_heat_was_cached()
    {
        var state = CityHeat.Enable(Extent());
        Check(state);
        var world = state.World;
        var city = world.City!;
        var heat = city.State.Heat;
        var full = Enumerable.Repeat(float.MaxValue,heat.Q.Length).ToImmutableArray();
        heat = heat with { Q = full, Back = full, Rho = full };
        city = new(city.Profile,city.State with { Heat = heat });
        world = new(world.Profile,world.Terrain,world.Spatial.Capture(),world.Voxels,world.Slopes,
            world.Revision,world.SpeedLimits,world.Fleet,world.Stations,city);
        var changed = new MixedSessionState(new(world,state.Construction.Accounts),state.Provenance,
            clock: state.Clock.Capture(),random: state.Random.Capture(),revision: state.Revision);
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(FreshRead(changed),Json).Length > MixedWire.MaximumMixedBytes);
        Assert.Equal("saveTooLarge",Assert.Throws<InvalidDataException>(() => MixedWire.ValidateAdmission(changed)).Message);
        Check(state);
    }

    [Fact]
    public void Mixed_limit_accepts_exactly_sixteen_MiB_and_rejects_one_byte_more()
    {
        const int maximum = 16 * 1024 * 1024;
        Assert.Equal(maximum,MixedWire.MaximumMixedBytes);
        var state = CityHeat.Enable(Extent());
        var original = state.World;
        var city = original.City!;
        var heat = city.State.Heat;
        var accounts = state.Construction.Accounts.Book(ConstructionBooking.Spend(0),1,new(0),"x").Capture();
        MixedSessionState With(float[] values, int padding)
        {
            var length = heat.Q.Length;
            var changedHeat = heat with {
                Q = values.AsSpan(0,length).ToArray().ToImmutableArray(),
                Back = values.AsSpan(length,length).ToArray().ToImmutableArray(),
                Rho = values.AsSpan(2*length,length).ToArray().ToImmutableArray()
            };
            var changedCity = new CityGraph(city.Profile,city.State with { Heat = changedHeat });
            var world = new MixedWorld(original.Profile,original.Terrain,original.Spatial.Capture(),original.Voxels,
                original.Slopes,1,original.SpeedLimits,original.Fleet,original.Stations,changedCity);
            var changedAccounts = ConstructionAccounts.Restore(accounts with {
                Transactions = accounts.Transactions.Select(t => t with { Operation = new string('x',1+padding) }).ToImmutableArray()
            });
            return new(new(world,changedAccounts),state.Provenance,clock: state.Clock.Capture(),
                random: state.Random.Capture(),revision: 1);
        }
        var values = new float[3*heat.Q.Length];
        var baseline = JsonSerializer.SerializeToUtf8Bytes(FreshRead(With(values,0)),Json).Length;
        var step = JsonSerializer.SerializeToUtf8Bytes(float.MaxValue,Json).Length - 1;
        var count = (maximum-baseline)/step - 1;
        Array.Fill(values,float.MaxValue,0,count);
        var padding = maximum-baseline-count*step;
        foreach (var offset in new[] { -1, 0, 1 })
        {
            var candidate = With(values,padding+offset);
            Assert.Equal(maximum+offset,JsonSerializer.SerializeToUtf8Bytes(FreshRead(candidate),Json).Length);
            if (offset <= 0) Check(candidate);
            else Assert.Equal("saveTooLarge",Assert.Throws<InvalidDataException>(() => MixedWire.ValidateAdmission(candidate)).Message);
        }
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("\"\\\n å雪😀")]
    public void Counting_stream_enforces_exact_inclusive_boundaries(string value)
    {
        var length = JsonSerializer.SerializeToUtf8Bytes(value, Json).LongLength;
        Assert.Equal(length, JsonByteCount.Count(value, Json, length));
        Assert.Equal("saveTooLarge", Assert.Throws<InvalidDataException>(() =>
            JsonByteCount.Count(value, Json, length - 1)).Message);
    }

    [Fact]
    public void Counting_stream_preserves_polymorphic_delta_envelope()
    {
        var state = BackendPerformanceFixture.Open("small");
        var before = MixedWire.Snapshot(state, 1);
        state.AdvanceOneMinute();
        WireMessage delta = StateDifference.Between(before, MixedWire.Snapshot(state, 1));
        var length = JsonSerializer.SerializeToUtf8Bytes(delta, Json).LongLength;
        Assert.Equal(length, JsonByteCount.Count(delta, Json, length));
        Assert.Throws<InvalidDataException>(() => JsonByteCount.Count(delta, Json, length - 1));
        Assert.Equal(length, JsonByteCount.Count(delta, Json, 16 * 1024 * 1024));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (MixedSessionState Current, List<WeakReference> Previous) FillCaches()
    {
        var state = BackendPerformanceFixture.Open("small");
        var previous = new List<WeakReference>();
        for (var i = 0; i < 16; i++)
        {
            MixedWire.ValidateAdmission(state);
            previous.Add(new(state.World));
            previous.Add(new(state.Construction.Accounts));
            previous.Add(new(state.World.City!.State.Heat));
            state.AdvanceOneMinute();
        }
        previous.RemoveAll(reference => ReferenceEquals(reference.Target, state.World) ||
            ReferenceEquals(reference.Target, state.Construction.Accounts) ||
            ReferenceEquals(reference.Target, state.World.City!.State.Heat));
        return (state, previous);
    }

    [Fact]
    public void Admission_caches_do_not_retain_obsolete_worlds_accounts_or_heat()
    {
        var (current, previous) = FillCaches();
        Assert.NotEmpty(previous);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.All(previous, reference => Assert.False(reference.IsAlive));
        GC.KeepAlive(current);
    }
}
