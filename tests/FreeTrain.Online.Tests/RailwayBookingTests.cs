using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public class RailwayBookingTests
{
    static JsonDocument Fixtures() => JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spatial/railway-fixtures.json")));
    static IEnumerable<JsonElement> Cases(JsonDocument f, string group) => f.RootElement.GetProperty("groups").GetProperty(group).GetProperty("cases").EnumerateArray();
    static int Int(JsonElement j, string key) => j.GetProperty(key).GetInt32();
    static long Long(JsonElement j, string key) => j.GetProperty(key).GetInt64();
    static string? Str(JsonElement j, string key) => j.GetProperty(key).GetString();
    static bool Car(JsonElement j) => j.TryGetProperty("car",out var value) && value.GetBoolean();
    static bool Applies(JsonElement j) => Str(j.GetProperty("expected"),"outcome") == "apply";
    static void Quote(ConstructionBooking booking, long expected)
    {
        Assert.Equal(expected,booking.Total);
        var accounts = new ConstructionAccounts(); var before = accounts.Capture();
        var after = accounts.Book(booking,1,new(GameTime.StartMinute),"literal-test");
        Assert.Equal(before,accounts.Capture());
        Assert.Equal(booking.Charges.Select(c => c.Amount),after.Transactions.Select(t => t.Expenditure));
        Assert.All(after.Transactions,t => Assert.Equal(AccountGenre.RailService,t.Genre));
    }

    [Fact]
    public void Literal_normal_build_costs_include_reclamation_and_unchanged_endpoint_attachment()
    {
        using var f = Fixtures(); var count = 0;
        foreach (var row in Cases(f,"routes").Where(Applies))
        {
            var input = row.GetProperty("input"); var z = input.GetProperty("from")[2].GetInt32();
            Quote(RailwayBooking.NormalBuild(input.GetProperty("cells").EnumerateArray().Select(c =>
                new RailConstructionCharge(z,Int(c,"ground"),c.TryGetProperty("reclaimedValue",out var v) ? v.GetInt64() : 0))),
                Long(row.GetProperty("expected"),"spend"));
            count++;
        }
        Assert.True(count >= 10);
    }

    [Fact]
    public void Literal_normal_removal_counts_noop_rails_but_excludes_cars()
    {
        using var f = Fixtures();
        foreach (var row in Cases(f,"normalRemoval").Where(Applies))
        {
            var i = row.GetProperty("input");
            var count = i.GetProperty("cells").EnumerateArray().Count(c => c.TryGetProperty("patternId",out _) && !Car(c));
            Quote(RailwayBooking.NormalRemoval(count,Int(i,"z"),Int(i,"water")),Long(row.GetProperty("expected"),"spend"));
        }
    }

    [Fact]
    public void Literal_cardinal_slopes_use_water_instead_of_local_ground_and_charge_once()
    {
        using var f = Fixtures(); var count = 0;
        foreach (var row in Cases(f,"slopes").Where(Applies))
        {
            var i = row.GetProperty("input"); var e = row.GetProperty("expected");
            var z = i.GetProperty("base")[2].GetInt32(); var water = Int(i,"water");
            Quote(RailwayBooking.SlopeBuild(z,water),Long(e,"build"));
            Quote(RailwayBooking.SlopeRemoval(z,water),Long(e,"remove")); count++;
        }
        Assert.Equal(15,count);
        foreach (var row in Cases(f,"slopeRemoval").Where(Applies))
        {
            // Ownership resolution is P4-10; price takes the resolved source base.
            var e = row.GetProperty("expected");
            Quote(RailwayBooking.SlopeRemoval(e.GetProperty("base")[2].GetInt32(),Int(row.GetProperty("input"),"water")),Long(e,"spend"));
        }
    }

    [Fact]
    public void Literal_special_booking_preserves_endpoint_skip_underwater_and_repeated_subtotal_rules()
    {
        using var f = Fixtures(); var count = 0;
        foreach (var row in Cases(f,"specialRails").Where(Applies))
        {
            var i = row.GetProperty("input"); var e = row.GetProperty("expected");
            var cells = i.GetProperty("cells").EnumerateArray().ToArray();
            var op = Str(i,"operation"); var remove = Str(i,"action") == "remove";
            var z = Int(i,"z"); var direction = Int(i,"direction");
            bool Matches(JsonElement c) => Str(c,"kind") == op && c.GetProperty("edges").EnumerateArray().Any(v => v.GetInt32() == direction);
            var booking = op switch {
                "bridge" or "girder" => RailwayBooking.Elevated(cells.Select(c => new RailConstructionCharge(z,Int(c,"ground"))),
                    z,Int(cells[0],"ground"),Int(i,"water"),remove),
                "tunnel" when !remove => RailwayBooking.TunnelBuild(cells.Count(c => Str(c,"kind") is "empty" or "mountain")),
                "tunnel" => RailwayBooking.TunnelRemoval(cells.SkipLast(1).Count(c => Matches(c) && !Car(c))),
                "garage" when !remove => RailwayBooking.GarageBuild(cells.Length),
                "garage" => RailwayBooking.GarageRemoval(cells.SkipLast(1).Select(Matches)),
                _ => throw new Exception("Uncovered operation.") };
            Quote(booking,Long(e,"spend"));
            Assert.Equal(e.GetProperty("charges").EnumerateArray().Select(v => v.GetInt64()),booking.Charges.Select(c => c.Amount));
            count++;
        }
        Assert.True(count >= 25);
    }

    [Fact]
    public void Literal_stationary_genres_and_zero_booking_interfaces_remain_distinct()
    {
        using var f = Fixtures(); var count = 0;
        foreach (var row in Cases(f,"components"))
        {
            var i = row.GetProperty("input"); var e = row.GetProperty("expected");
            if (Str(i,"kind") == "stationary-booking")
            {
                var complete = Str(i,"action") == "complete";
                var booking = complete ? RailwayBooking.StationaryCompletion(Long(i,"price")) :
                    RailwayBooking.StationaryRemoval(i.GetProperty("size")[0].GetInt64(),i.GetProperty("size")[1].GetInt64());
                Assert.Equal(Long(e,"spend"),booking.Total);
                Assert.Equal(complete ? AccountGenre.RailService : AccountGenre.Subsidiaries,booking.Charges.Single().Genre);
                count++;
            }
            else if (Str(i,"kind") is "signal" or "pole" or "site-cancel" or "special-attach")
                Assert.Equal(Long(e,"spend"),ConstructionBooking.None.Total);
        }
        Assert.Equal(3,count);
        Assert.Empty(ConstructionBooking.None.Charges);
        Assert.Single(ConstructionBooking.Spend(0).Charges);
        Assert.Equal("{AC30BB0B-044C-4784-A83D-FCB1F60B3CF2}",AccountGenre.RailService.SourceId());
        Assert.Equal("{2A666F1A-9F40-4F67-98F4-DEAC1E55296D}",AccountGenre.Subsidiaries.SourceId());
        Assert.Equal("{CC00A6D1-D078-4D3C-AFB2-EDC6CB9F4CB3}",AccountGenre.RoadService.SourceId());
        Assert.Equal("{C0A9ABA5-801F-4AA6-93EA-6FF563C2B407}",AccountGenre.Others.SourceId());
    }

    [Fact]
    public void Literal_widened_products_and_Int64_overflow_boundary()
    {
        using var f = Fixtures();
        foreach (var row in Cases(f,"boundaries").Where(r => Str(r.GetProperty("input"),"kind") == "product"))
        {
            var i = row.GetProperty("input"); var e = row.GetProperty("expected");
            long Product() => RailwayBooking.Product(Long(i,"unit"),Long(i,"multiplier"),Long(i,"count"));
            if (Str(e,"outcome") == "apply") Assert.Equal(Long(e,"amount"),Product());
            else Assert.Throws<OverflowException>(() => Product());
        }
        Assert.Equal(0,RailwayBooking.Product(long.MaxValue,long.MaxValue,0));
        Assert.Throws<OverflowException>(() => RailwayBooking.NormalBuild([new(2,2,long.MaxValue)]));
        Assert.Throws<OverflowException>(() => RailwayBooking.NormalRemoval(long.MaxValue,2,0));
        Assert.Throws<OverflowException>(() => RailwayBooking.TunnelBuild(long.MaxValue));
        Assert.Throws<OverflowException>(() => RailwayBooking.TunnelRemoval(long.MaxValue));
        Assert.Throws<OverflowException>(() => RailwayBooking.GarageBuild(long.MaxValue));
        Assert.Throws<OverflowException>(() => RailwayBooking.StationaryRemoval(long.MaxValue,2));
        Assert.Throws<OverflowException>(() => new ConstructionBooking([new(AccountGenre.RailService,long.MaxValue),new(AccountGenre.RailService,1)]));
        Assert.Throws<ArgumentException>(() => RailwayBooking.NormalBuild([new(2,2,-1)]));
        Assert.Throws<ArgumentException>(() => ConstructionBooking.Spend(-1));
        Assert.Throws<ArgumentException>(() => ConstructionBooking.Spend(1,(AccountGenre)999));
        // Coordinate differences widen BEFORE subtraction, including slope baseZ+1.
        Assert.Equal(51_539_607_540_000_000,RailwayBooking.NormalBuild([new(int.MinValue,int.MaxValue)]).Total);
        Assert.Equal(34_359_738_376_000_000,RailwayBooking.SlopeRemoval(int.MaxValue,int.MinValue).Total);
    }

    [Fact]
    public void Literal_cash_defaults_zero_and_multiple_rescues_have_no_affordability_gate()
    {
        Assert.Equal(15_000_000_000,new ConstructionAccounts().Cash);
        Assert.Equal(10_000_000_000,new ConstructionAccounts().Settings.BankruptcyIncrement);
        using var f = Fixtures();
        foreach (var row in Cases(f,"boundaries").Where(r => Str(r.GetProperty("input"),"kind") == "cash"))
        {
            var i = row.GetProperty("input"); var e = row.GetProperty("expected");
            ConstructionAccounts Book() => new ConstructionAccounts(Long(i,"cash"),new(Long(i,"increment")))
                .Book(ConstructionBooking.Spend(Long(i,"spend")),1,new(GameTime.StartMinute),"literal-cash");
            if (Str(e,"outcome") == "reject")
            {
                if (Long(i,"increment") <= 0) Assert.Throws<ArgumentException>(() => Book());
                else Assert.Throws<OverflowException>(() => Book());
            }
            else
            {
                var after = Book(); Assert.Equal(Long(e,"cash"),after.Cash); Assert.Equal(Long(e,"topUps"),after.TopUps);
                Assert.Equal(Long(i,"spend"),after.Transactions.Single().Expenditure);
            }
        }
        var huge = new ConstructionAccounts(0,new(1)).Book(ConstructionBooking.Spend(long.MaxValue),1,new(0),"large-rescue");
        Assert.Equal(0,huge.Cash); Assert.Equal(long.MaxValue,huge.TopUps);
        Assert.Throws<OverflowException>(() => huge.Book(ConstructionBooking.Spend(1),2,new(0),"counter-overflow"));
        Assert.Throws<OverflowException>(() => new ConstructionAccounts(-1,new(1)).Book(ConstructionBooking.Spend(long.MaxValue),1,new(0),"count-overflow"));
        var nearlyFull = new ConstructionAccounts(0,new(1)).Book(ConstructionBooking.Spend(long.MaxValue-1),1,new(0),"prepare");
        var before = nearlyFull.Capture();
        Assert.Throws<OverflowException>(() => nearlyFull.Book(new([new(AccountGenre.RailService,1),new(AccountGenre.RailService,1)]),2,new(0),"late-failure"));
        Assert.Equal(before,nearlyFull.Capture());
    }

    [Fact]
    public void Ordered_garage_rescues_and_no_spend_calls_survive_durable_state_roundtrip()
    {
        var empty = new ConstructionAccounts(0,new(300_000));
        var accounts = empty.Book(RailwayBooking.GarageRemoval([true,false,true]),1,new(GameTime.StartMinute),"garage.remove");
        Assert.Equal(new long[] { 200_000,200_000,400_000 },accounts.Transactions.Select(t => t.Expenditure));
        Assert.Equal(new long[] { 1,1,1 },accounts.Transactions.Select(t => t.TopUps));
        Assert.Equal(new long[] { 100_000,200_000,100_000 },accounts.Transactions.Select(t => t.CashAfter));
        var state = accounts.Capture();
        var restored = ConstructionAccounts.Restore(JsonSerializer.Deserialize<ConstructionAccountState>(JsonSerializer.Serialize(state))!);
        Assert.Equal(JsonSerializer.Serialize(state),JsonSerializer.Serialize(restored.Capture()));
        var next = restored.Book(ConstructionBooking.Spend(100_001,AccountGenre.Subsidiaries),3,new(GameTime.StartMinute+1440),"stationary.remove");
        Assert.Equal(299_999,next.Cash); Assert.Equal(4,next.TopUps); Assert.Equal(4,next.Transactions[^1].Sequence);
        Assert.Equal(5,next.NextSequence); Assert.Equal(AccountGenre.Subsidiaries,next.Transactions[^1].Genre);
        var none = next.Book(ConstructionBooking.None,4,new(GameTime.StartMinute+1440),"signal.place");
        Assert.Equal(next.Capture(),none.Capture());
        var zero = none.Book(ConstructionBooking.Spend(0),5,new(GameTime.StartMinute+1440),"bridge.zero");
        Assert.Equal(5,zero.Transactions.Length); Assert.Equal(next.Cash,zero.Cash);
        Assert.Empty(empty.Transactions);
    }

    [Fact]
    public void Tampered_or_out_of_order_history_and_invalid_settings_reject_before_restore()
    {
        var accounts = new ConstructionAccounts(0).Book(RailwayBooking.GarageRemoval([true,true]),1,new(100),"garage.remove");
        var state = accounts.Capture(); var first = state.Transactions[0]; var last = state.Transactions[^1];
        foreach (var bad in new[] { first with { Sequence = 2 }, first with { TopUps = 0 }, first with { CashAfter = 7 },
            first with { Expenditure = -1 },first with { Genre = (AccountGenre)999 },first with { AbsoluteMinute = -1 },
            first with { Operation = "" },first with { WorldRevision = 0 } })
            Assert.ThrowsAny<ArgumentException>(() => ConstructionAccounts.Restore(state with { Transactions = state.Transactions.SetItem(0,bad) }));
        foreach (var bad in new[] { last with { AbsoluteMinute = 99 },last with { AbsoluteMinute = 101 },last with { Operation = "other" } })
            Assert.Throws<ArgumentException>(() => ConstructionAccounts.Restore(state with { Transactions = state.Transactions.SetItem(1,bad) }));
        Assert.Throws<ArgumentException>(() => ConstructionAccounts.Restore(state with { Settings = new(1) }));
        Assert.Throws<ArgumentException>(() => ConstructionAccounts.Restore(state with { Transactions = default }));
        Assert.Throws<ArgumentException>(() => new ConstructionAccounts(0,new(0)));
        Assert.Throws<ArgumentException>(() => accounts.Book(ConstructionBooking.Spend(1),1,new(100),"replay"));
        Assert.Throws<ArgumentException>(() => accounts.Book(ConstructionBooking.Spend(1),2,new(99),"past"));
        Assert.Equal(state,accounts.Capture());
    }

    static Location P => new(8,8,2);
    static TerrainWorld Terrain() => new(TerrainProfiles.Bundled,"Booking",new(32,16,8),0,
        Enumerable.Repeat(new TerrainCell(2),512),new(1,[]),[]);
    static (SpatialEntity Entity, RailwayVoxel Voxel) Track(long id)
    {
        var profile = RailwayProfiles.Bundled;
        var rail = new RailComponent(new(PreparedContentCatalog.BundledPackId,"system:pattern/4","system:pattern/4"),4);
        return (new(id,0,profile.TrafficOwner,P,new([new(new(0,0,0),true)]),true,false,0),
            new(P,id,RailwayVoxelKind.Traffic,new(rail)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Geometry_terrain_allocator_and_accounts_form_one_publication_without_authoring_exemption(bool authoring)
    {
        var terrain = Terrain();
        var session = new TerrainSessionState(terrain,TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,
            terrain.Name,new(32,16,8),0)),constructionMode:authoring);
        var sessionBefore = SaveCodec.Write(session);
        var state = new MixedConstructionState(MixedWorld.FromTerrain(RailwayProfiles.Bundled,session.World),new(0));
        var edited = CornerTerrain.Apply(terrain,CornerTerrain.Preview(terrain,new(14,14,2),"raise"));
        var id = edited.Spatial.NextEntityId; var (entity,voxel) = Track(id);
        var booking = RailwayBooking.NormalBuild([new(2,2)]);
        var after = state.Replace(0,edited,new(id+1,edited.Spatial.Entities.Add(entity)),[voxel],[],booking,new(GameTime.StartMinute),"normal.build");
        Assert.Same(edited,after.World.Terrain); Assert.Equal(voxel,after.World.At(P));
        Assert.Equal(id+1,after.World.Spatial.NextEntityId); Assert.Equal(1,after.World.Revision);
        Assert.Equal(9_994_000_000,after.Accounts.Cash); Assert.Equal(1,after.Accounts.TopUps);
        Assert.Equal(6_000_000,after.Accounts.Transactions.Single().Expenditure);
        Assert.Equal(1,after.Accounts.Transactions.Single().WorldRevision);
        Assert.Same(terrain,state.World.Terrain); Assert.Empty(state.World.Voxels); Assert.Equal(1,state.World.Spatial.NextEntityId);
        Assert.Empty(state.Accounts.Transactions); Assert.Equal(0,state.Accounts.Cash);
        Assert.Equal(sessionBefore,SaveCodec.Write(session));
        Assert.Throws<ArgumentException>(() => new MixedConstructionState(state.World,after.Accounts));
    }

    [Fact]
    public void Late_account_failure_stale_geometry_and_invalid_graph_preserve_every_original_subset()
    {
        var terrain = Terrain(); var (entity,voxel) = Track(1);
        var state = new MixedConstructionState(MixedWorld.FromTerrain(RailwayProfiles.Bundled,terrain),new(long.MinValue));
        var before = JsonSerializer.Serialize(state.Accounts.Capture());
        var charge = ConstructionBooking.Spend(1); var spatial = new SpatialState(2,[entity]);
        Assert.Throws<OverflowException>(() => state.Replace(0,terrain,spatial,[voxel],[],charge,new(0),"overflow"));
        Assert.Throws<InvalidOperationException>(() => state.Replace(1,terrain,spatial,[voxel],[],charge,new(0),"stale"));
        Assert.Throws<ArgumentException>(() => state.Replace(0,terrain,spatial,[],[],charge,new(0),"partial"));
        Assert.Throws<ArgumentException>(() => state.Replace(0,terrain,spatial,[voxel],[],charge,new(0),""));
        var edited = CornerTerrain.Apply(terrain,CornerTerrain.Preview(terrain,new(14,14,2),"raise"));
        var id = edited.Spatial.NextEntityId; var (newEntity,newVoxel) = Track(id);
        Assert.Throws<OverflowException>(() => state.Replace(0,edited,new(id+1,edited.Spatial.Entities.Add(newEntity)),
            [newVoxel],[],charge,new(0),"late-terrain-overflow"));
        Assert.Equal(before,JsonSerializer.Serialize(state.Accounts.Capture()));
        Assert.Same(terrain,state.World.Terrain); Assert.Empty(state.World.Spatial.Entities);
        Assert.Empty(state.World.Voxels); Assert.Equal(1,state.World.Spatial.NextEntityId); Assert.Equal(0,state.World.Revision);
    }

    [Fact]
    public void Charged_noop_and_free_geometry_changes_are_both_real_atomic_operations()
    {
        var state = new MixedConstructionState(MixedWorld.FromTerrain(RailwayProfiles.Bundled,Terrain()));
        var world = state.World;
        var charged = state.Replace(0,world.Terrain,world.Spatial.Capture(),[],[],RailwayBooking.NormalRemoval(1,2,0),new(0),"normal.remove-noop");
        Assert.Empty(charged.World.Voxels); Assert.Equal(14_994_000_000,charged.Accounts.Cash);
        var (entity,voxel) = Track(1);
        var zero = charged.Replace(1,world.Terrain,new(2,[entity]),[voxel],[],ConstructionBooking.Spend(0),new(0),"bridge.zero");
        Assert.Equal(2,zero.World.Revision); Assert.Equal(voxel,zero.World.At(P)); Assert.Equal(charged.Accounts.Cash,zero.Accounts.Cash);
        Assert.Equal(2,zero.Accounts.Transactions.Length); Assert.Equal(0,zero.Accounts.Transactions[^1].Expenditure);
        var noCall = zero.Replace(2,world.Terrain,zero.World.Spatial.Capture(),[voxel],[],ConstructionBooking.None,new(0),"pole.place");
        Assert.Equal(3,noCall.World.Revision); Assert.Equal(zero.Accounts.Capture(),noCall.Accounts.Capture());
    }
}
