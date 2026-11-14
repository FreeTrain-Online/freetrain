using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public sealed class MixedSuccessorValidationTests
{
    private static readonly Location Position = new(8, 8, 2);

    private static MixedWorld World(int pattern = 1, long nextId = 2, long revision = 0, TerrainWorld? terrain = null)
    {
        var profile = RailwayProfiles.Bundled;
        terrain ??= new TerrainWorld(TerrainProfiles.Bundled, "Successor validation", new(32, 16, 8), 0,
            Enumerable.Repeat(new TerrainCell(2), 32 * 16), new(1, []), []);
        var rail = profile.Bindings.Single(b => b.Handler == RailwayHandler.Normal && b.Variant == pattern);
        var owner = new SpatialEntity(1, 0, profile.TrafficOwner, Position,
            new([new(new(0, 0, 0), true)]), true, false, 0);
        return new(profile, terrain, new(nextId, [owner]),
            [new(Position, 1, RailwayVoxelKind.Traffic, new(new(rail.Reference, pattern)))], [], revision);
    }

    private static MixedSessionState Session(MixedWorld world, ConstructionAccounts? accounts = null) =>
        new(new(world, accounts), TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,
            world.Terrain.Name, new(32, 16, 8), 0)), clock: new(100, 0, []), revision: world.Revision);

    private static MixedWorld Replace(MixedWorld world) => world.Replace(world.Revision, world.Terrain,
        world.Spatial.Capture(), world.Voxels, world.Slopes, world.SpeedLimits);

    private static MixedWorld Copy(MixedWorld world) => new(world.Profile, world.Terrain,
        world.Spatial.Capture(), world.Voxels, world.Slopes, world.Revision, world.SpeedLimits);

    [Fact]
    public void Certified_and_independently_constructed_valid_successors_publish_the_same_state()
    {
        var session = Session(World());
        var certified = Replace(session.World);
        var reused = session.Successor(session.Revision, new(certified, session.Construction.Accounts), false);
        var validated = session.Successor(session.Revision, new(Copy(certified), session.Construction.Accounts), false);
        Assert.Same(certified, reused.World);
        Assert.Equal(SaveCodec.Write(reused), SaveCodec.Write(validated));
    }

    [Fact]
    public void Reconstructing_a_certified_successor_does_not_certify_changed_unversioned_payloads()
    {
        var session = Session(World());
        var certified = Replace(session.World);
        var changed = World(pattern: 4).Voxels;
        var forged = new MixedWorld(certified.Profile, certified.Terrain, certified.Spatial.Capture(),
            changed, certified.Slopes, certified.Revision, certified.SpeedLimits);
        Assert.Throws<ArgumentException>(() => session.Successor(session.Revision,
            new(forged, session.Construction.Accounts), false));
    }

    [Theory]
    [InlineData("payload")]
    [InlineData("allocator")]
    [InlineData("revision")]
    public void A_certificate_for_another_predecessor_cannot_authorize_invalid_successors(string fault)
    {
        var session = Session(World(nextId: fault == "allocator" ? 3 : 2,
            revision: fault == "revision" ? 1 : 0));
        // Equal revisions are insufficient: this replacement validated against
        // another world's payloads and allocator, even when they share IDs.
        var candidate = Replace(World(pattern: fault == "payload" ? 4 : 1, terrain: session.World.Terrain));
        Assert.Throws<ArgumentException>(() => session.Successor(session.Revision,
            new(candidate, session.Construction.Accounts), false));
    }

    [Theory]
    [InlineData("initial-cash")]
    [InlineData("settings")]
    [InlineData("missing-prefix")]
    [InlineData("changed-prefix")]
    [InlineData("booking-time")]
    [InlineData("booking-revision")]
    public void Certified_worlds_still_require_valid_account_successors(string fault)
    {
        var accounts = new ConstructionAccounts().Book(ConstructionBooking.Spend(10), 1, new(99), "before");
        var session = Session(World(revision: 2), accounts);
        var candidate = Replace(session.World);
        var after = fault switch
        {
            "initial-cash" => new ConstructionAccounts(accounts.InitialCash + 1),
            "settings" => new ConstructionAccounts(settings: new(1)),
            "missing-prefix" => new ConstructionAccounts(),
            "changed-prefix" => ConstructionAccounts.Restore(accounts.Capture() with {
                Transactions = [accounts.Transactions[0] with { Operation = "changed" }] }),
            "booking-time" => accounts.Book(ConstructionBooking.Spend(1), candidate.Revision, new(101), "after"),
            _ => accounts.Book(ConstructionBooking.Spend(1), session.World.Revision, new(100), "after")
        };
        Assert.Throws<ArgumentException>(() => session.Successor(session.Revision, new(candidate, after), false));
    }

    [Theory]
    [InlineData("minute")]
    [InlineData("queue")]
    public void Certified_worlds_still_require_valid_clock_and_callback_boundaries(string fault)
    {
        var session = Session(World());
        var candidate = Replace(session.World);
        var clock = fault == "minute" ? new ClockState(101, 0, []) :
            new ClockState(100, 1, [new(0, 0, 101, "unknown", 0)]);
        Assert.Throws<ArgumentException>(() => session.Successor(session.Revision,
            new(candidate, session.Construction.Accounts), false, clock: clock));
    }
}

public partial class FleetMovementTests
{
    [Fact]
    public void Certified_train_callbacks_match_uncertified_callbacks_through_save_and_continuation()
    {
        var initial = BuildRing();
        var certified = Restore(SaveCodec.Write(initial));
        var callbacks = 0;
        TrainCallbackResult Uncertified(MixedSessionState minute, long id)
        {
            var result = FleetMovement.Tick(minute, id);
            callbacks++;
            var w = result.Candidate.World;
            var copy = new MixedWorld(w.Profile, w.Terrain, w.Spatial.Capture(), w.Voxels, w.Slopes,
                w.Revision, w.SpeedLimits, w.Fleet, w.Stations, w.City);
            return result with { Candidate = new(copy, result.Candidate.Accounts) };
        }
        var uncertified = Session(initial.World, initial.Clock.Capture(), initial.Construction.Accounts, Uncertified);
        for (var i = 0; i < 16; i++)
        {
            certified.AdvanceOneMinute();
            uncertified.AdvanceOneMinute();
            Assert.Equal(SaveCodec.Write(certified), SaveCodec.Write(uncertified));
            if (i == 7) certified = Restore(SaveCodec.Write(certified));
        }
        Assert.True(callbacks > 0);
        Assert.True(certified.Fleet!.Trains[0].MoveCount > 0);
    }

    [Theory]
    [InlineData("continuation")]
    [InlineData("booking")]
    public void Invalid_callback_metadata_rolls_back_an_already_certified_train_move(string fault)
    {
        var initial = BuildRing();
        var callbacks = 0;
        TrainCallbackResult Invalid(MixedSessionState minute, long id)
        {
            var result = FleetMovement.Tick(minute, id);
            callbacks++;
            Assert.NotSame(minute.World, result.Candidate.World);
            minute.Random["construction"].Next(100);
            if (fault == "continuation") return result with { NextDelay = 0 };
            var accounts = result.Candidate.Accounts.Book(ConstructionBooking.Spend(1),
                result.Candidate.World.Revision, new(minute.Clock.Time.AbsoluteMinute + 1), "invalid-time");
            return result with { Candidate = new(result.Candidate.World, accounts) };
        }
        var session = Session(initial.World, initial.Clock.Capture(), initial.Construction.Accounts, Invalid);
        // Deployment's native timer is two minutes; fail exactly on the move.
        while (session.Clock.Time.AbsoluteMinute + 1 < session.Clock.Capture().Pending.Min(a => a.DueMinute))
            session.AdvanceOneMinute();
        var before = SaveCodec.Write(session);
        var world = session.World;
        Assert.Throws<ArgumentException>(session.AdvanceOneMinute);
        Assert.Equal(1, callbacks);
        Assert.Same(world, session.World);
        Assert.Equal(before, SaveCodec.Write(session));
        Assert.Empty(session.PassengerEffects);
        Assert.Empty(session.DepartureBells);
    }
}
