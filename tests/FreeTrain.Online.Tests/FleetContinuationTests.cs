using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public partial class FleetMovementTests
{
    // Reopen at EVERY minute, including quiet minutes immediately either side
    // of a callback. Compare the complete canonical save, not just train poses.
    [Theory]
    [InlineData("equal-time",100,116)]
    [InlineData("collision",100,112)]
    [InlineData("wait-departure",100,122)]
    [InlineData("turning",100,122)]
    [InlineData("outside-exchange",199,205)]
    [InlineData("outside-return",199,207)]
    public void P5_27_every_minute_reopen_preserves_exact_continuation(string scenario,int start,int end)
    {
        MixedSessionState s;
        if(scenario.StartsWith("outside"))s=OutsideSession(scenario=="outside-exchange"?51:0,true);
        else if(scenario=="equal-time")s=BuildRing(quantity:2);
        else
        {
            s=OperationsLine(signal:scenario is "wait-departure" or "turning");
            s=Place(s,0,new(22,20,2));
            if(scenario=="collision")s=Place(s,1,new(23,20,2),true);
            else s=Timetableize(s,[scenario=="turning"
                ?new(new(22,20,2),FleetHarborKind.Advanced,0,false,[Rule(FleetRuleAction.Reverse2)])
                :new(new(22,20,2),FleetHarborKind.Fixed,7,true)]);
        }
        Assert.Equal(start,s.Clock.Time.AbsoluteMinute);
        var initial=SaveCodec.Write(s);var uninterrupted=Restore(initial);AdvanceTo(uninterrupted,end);
        var expected=SaveCodec.Write(uninterrupted);
        for(var cut=start;cut<=end;cut++)
        {
            var live=Restore(initial);AdvanceTo(live,cut);
            var boundary=SaveCodec.Write(live);var loaded=Restore(boundary);
            Assert.Equal(boundary,SaveCodec.Write(loaded));
            while(loaded.Clock.Time.AbsoluteMinute<end)
            {
                live.AdvanceOneMinute();loaded.AdvanceOneMinute();
                Assert.Equal(SaveCodec.Write(live),SaveCodec.Write(loaded));OperationsBoundary(loaded);
            }
            Assert.Equal(expected,SaveCodec.Write(loaded));
        }
        // Export a gameplay-built two-train ring and its exact continued oracle
        // for the HTTP/browser/process restart test; no trusted fixture car count.
        if(scenario=="equal-time" && Environment.GetEnvironmentVariable("FREETRAIN_FLEET_CONTINUATION_OUTPUT") is {} output)
        {
            File.WriteAllBytes(output,initial);File.WriteAllBytes(output+".expected",expected);
            Assert.All(uninterrupted.Fleet!.Trains,t=>Assert.Equal(8,t.MoveCount));
            Assert.Equal(2,uninterrupted.Clock.Capture().Pending.Length);
        }
    }

    [Fact]
    public void P5_27_placement_withdrawal_and_reversal_match_on_both_sides_of_load()
    {
        var live=OperationsLine(length:3,quantity:1);var loaded=Restore(SaveCodec.Write(live));
        MixedSessionState Deploy(MixedSessionState s)=>Place(s,0,new(20,20,2));
        MixedSessionState Reverse(MixedSessionState s)=>FleetDeployment.Commit(s,FleetDeployment.PreviewReversal(s,s.Fleet!.Trains[0].Id));
        MixedSessionState Withdraw(MixedSessionState s)=>FleetDeployment.Commit(s,FleetDeployment.PreviewWithdrawal(s,s.Fleet!.Trains[0].Id));
        foreach(var operation in new Func<MixedSessionState,MixedSessionState>[]{Deploy,Reverse,Withdraw,Deploy})
        {
            live=operation(live);loaded=operation(loaded);
            Assert.Equal(SaveCodec.Write(live),SaveCodec.Write(loaded));OperationsBoundary(loaded);
            loaded=Restore(SaveCodec.Write(loaded));
            AdvanceTo(live,live.Clock.Time.AbsoluteMinute+2);AdvanceTo(loaded,loaded.Clock.Time.AbsoluteMinute+2);
            Assert.Equal(SaveCodec.Write(live),SaveCodec.Write(loaded));
        }
    }

    [Theory]
    [InlineData(-1,45_000_000L)][InlineData(0,30_000_000L)][InlineData(1,30_000_000L)]
    public void P5_27_resale_anniversary_keeps_purchase_minute_and_exact_receipt(int offset,long price)
    {
        var purchased=Purchase(Empty(),3);var id=purchased.Fleet!.Trains[0].Id;
        var minute=100+365L*24*60+offset;
        var live=Session(purchased.World,new(minute,0,[]),purchased.Construction.Accounts);
        var loaded=Restore(SaveCodec.Write(live));
        Assert.Equal(100,loaded.Fleet!.Train(id).PurchaseMinute);
        foreach(var state in new[]{live,loaded})Assert.Equal(price,FleetTrading.QuoteSale(state,id).Price);
        live=FleetTrading.Commit(live,FleetTrading.QuoteSale(live,id));
        loaded=FleetTrading.Commit(loaded,FleetTrading.QuoteSale(loaded,id));
        Assert.Equal(SaveCodec.Write(live),SaveCodec.Write(loaded));
        Assert.Equal(price,loaded.Construction.Accounts.Transactions[^1].Revenue);
        Assert.Empty(loaded.Clock.Capture().Pending);
    }

    [Fact]
    public async Task P5_27_shared_controller_and_group_edits_continue_identically_after_each_reopen()
    {
        using var live=FleetHost();
        foreach(var edit in new Func<SessionHost,CommandPayload>[] {
            h=>GroupCreate(h,"回復 / Recovery"),
            h=>GroupMove(h,h.Snapshot().Mixed!.Fleet!.Trains[0].Id,h.Snapshot().Mixed!.Fleet!.Groups.Last().Id,"train"),
            h=>ManagerAssign(h,h.Snapshot().Mixed!.Fleet!.Groups.Last().Id,"group",h.Snapshot().Mixed!.Fleet!.Controllers.Single(c=>c.Kind=="timetable")),
            h=>ManagerAssign(h,h.Snapshot().Mixed!.Fleet!.Trains[0].Id,"train",h.Snapshot().Mixed!.Fleet!.Controllers.Single(c=>c.Kind=="delegation")),
            h=>GroupRename(h,h.Snapshot().Mixed!.Fleet!.Groups.Last().Id,"本線 / Main") })
        {
            using var loaded=new SessionHost(Restore(await FleetSave(live)),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
            Assert.IsType<AcceptedMessage>(await FleetSend(live,edit(live)));
            Assert.IsType<AcceptedMessage>(await FleetSend(loaded,edit(loaded)));
            Assert.Equal(await FleetSave(live),await FleetSave(loaded));
            await FleetStep(live,2);await FleetStep(loaded,2);
            Assert.Equal(await FleetSave(live),await FleetSave(loaded));
        }
    }
}

public class FleetStorageRecoveryTests
{
    [Fact]
    public async Task Interrupted_temporary_write_and_failed_rename_preserve_prior_slot_and_checkpoint()
    {
        var directory=Path.Combine(Path.GetTempPath(),"fleet-recovery-"+Guid.NewGuid());
        var bytes=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Saves/fleet-v10.json"));
        try
        {
            using(var storage=new SaveStorage(directory))
            {
                await storage.Write("slot2",bytes);await storage.Write("checkpoint",bytes);
            }
            // A killed writer can leave a prefix at its unique temporary path.
            var orphan=Path.Combine(directory,".save-interrupted.tmp");File.WriteAllBytes(orphan,bytes[..100]);
            using(var storage=new SaveStorage(directory))
            {
                Assert.Equal(bytes,storage.Read("checkpoint"));Assert.Equal(bytes,storage.Read("slot2"));
                // A directory at the destination forces rename to fail after
                // the complete temporary write, portably on Windows and Unix.
                Directory.CreateDirectory(Path.Combine(directory,"slot3.json"));
                await Assert.ThrowsAnyAsync<IOException>(()=>storage.Write("slot3",bytes));
                Assert.Equal(bytes,storage.Read("checkpoint"));Assert.Equal(bytes,storage.Read("slot2"));
                Assert.Equal(new[]{orphan},Directory.GetFiles(directory,"*.tmp"));
                var loaded=SaveCodec.ReadWorld(storage.Read("checkpoint"),TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
                Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(bytes),
                    System.Text.Json.Nodes.JsonNode.Parse(SaveCodec.Write(loaded))));
            }
        }
        finally {Directory.Delete(directory,true);}
    }
}
