using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Server;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public partial class FleetMovementTests
{
    // Independent literal source byte/action mapping; do not derive it from
    // the production decoder. All-wildcard Go = ff ff ff ff ff 02.
    const string WildcardGo="//////8C";
    static EditFleetRuleCommand AppendCommand(SessionHost h,string encoded=WildcardGo)
        =>new(ManagerController(h).Id,ManagerController(h).Revision,new(22,20,2),"harbor","append",-1,0,null,null,encoded);

    [Theory]
    [InlineData(0,FleetRuleAction.Pass)][InlineData(1,FleetRuleAction.Stop)]
    [InlineData(2,FleetRuleAction.Go)][InlineData(3,FleetRuleAction.Reverse)]
    [InlineData(4,FleetRuleAction.Forwarding)][InlineData(5,FleetRuleAction.ReverseForwarding)]
    [InlineData(6,FleetRuleAction.Reverse2)][InlineData(7,FleetRuleAction.RForwarding2)]
    public void Rule_file_append_decodes_source_ordinals_without_confusing_junction_actions(byte code,FleetRuleAction action)
    {
        var encoded=Convert.ToBase64String([4,23,5,8,0,code]);
        var r=Assert.Single(FleetTimetable.Append([],encoded));Assert.Equal(new(4,23,5,8,0,action),r);
    }

    [Fact]
    public async Task Rule_file_append_preserves_existing_order_bypasses_only_duplicate_guard_and_continues_after_save()
    {
        using var h=new SessionHost(Timetableize(FleetLine(),[new(new(22,20,2),FleetHarborKind.Advanced,0,false,[Rule(FleetRuleAction.Stop)])]),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var initial=h.Snapshot();var c=ManagerController(h);
        if(Environment.GetEnvironmentVariable("FREETRAIN_RULE_FILE_FLEET_OUTPUT") is {} output)File.WriteAllBytes(output,await FleetSave(h));
        // Duplicate mask with different action is intentional on read, unlike Add.
        Assert.IsType<AcceptedMessage>(await FleetSend(h,AppendCommand(h,WildcardGo+"//////8G")));
        var after=ManagerController(h);Assert.Equal(c.Revision+1,after.Revision);
        Assert.Equal(new[]{"stop","go","reverse2"},after.Harbors[0].Rules!.Select(r=>r.Action));
        using var fixture=Fixtures();var scenario=fixture.RootElement.GetProperty("scenarioBindings").GetProperty("EXAV-SCN-009").GetProperty("expected");
        Assert.Equal(scenario.GetProperty("readRuleOrder").EnumerateArray().Select(x=>x.GetString()),
            after.Harbors[0].Rules!.Select(r=>r.Action switch {"stop"=>"A","go"=>"B","reverse2"=>"C",_=>"unexpected"}));
        Assert.Equal("BinaryFormatter",scenario.GetProperty("diagramEncoding").GetString());Assert.False(scenario.GetProperty("soapReadActive").GetBoolean());
        var read=h.Snapshot();Assert.Equal(initial.Tick,read.Tick);Assert.Equal(initial.Cash,read.Cash);
        Assert.Equal(JsonSerializer.Serialize(initial.Mixed!.Queue),JsonSerializer.Serialize(read.Mixed!.Queue));
        var saved=await FleetSave(h);var restored=Restore(saved);Assert.Equal(saved,SaveCodec.Write(restored));
        AdvanceTo(restored,112);Assert.Equal(0,restored.Fleet!.Trains[0].MoveCount);Assert.Equal(2,restored.Fleet.Trains[0].StopCallCount);
        Assert.Equal("stop",c.Harbors[0].Rules![0].Action);
        Assert.IsType<AcceptedMessage>(await FleetSend(h,new EditFleetRuleCommand(after.Id,after.Revision,new(22,20,2),"harbor","replace",1,0,new(-1,-1,-1,-1,-1,"pass"))));
        Assert.Equal("go",after.Harbors[0].Rules![1].Action);
    }

    [Theory]
    [InlineData("")][InlineData("AAAA")][InlineData("%%%%%%==")][InlineData("//////8I")]
    [InlineData("AP////8C")][InlineData("/////wEC")][InlineData("//////8C ")]
    public async Task Rule_file_malformed_append_is_atomic(string encoded)
    {
        using var h=new SessionHost(Timetableize(FleetLine(),[new(new(22,20,2),FleetHarborKind.Advanced,0,false)]),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var before=await FleetSave(h);ManagerRejected(await FleetSend(h,AppendCommand(h,encoded.Length==0?encoded:WildcardGo+encoded)),"invalidInput");
        Assert.Equal(before,await FleetSave(h));
    }

    [Fact]
    public async Task Rule_file_append_rechecks_epoch_role_revision_context_and_receipts()
    {
        using var h=new SessionHost(Timetableize(FleetLine(),[new(new(22,20,2),FleetHarborKind.Advanced,0,false)]),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var command=AppendCommand(h);var observer=new ParticipantContext("viewer",ParticipantRole.Observer);h.RegisterParticipant(observer);
        var before=await FleetSave(h);ManagerRejected(await FleetSend(h,command,observer),"notAuthorized");
        ManagerRejected(await FleetSend(h,command,epoch:h.WorldEpoch+1),"staleEpoch");
        foreach(var invalid in new[]{command with {SettingKind="junction"},command with {Index=0},command with {Offset=1},command with {Rule=new(-1,-1,-1,-1,-1,"go")},command with {Operation="add"},command with {RulesBase64=null},command with {ExpectedGeometryRevision=0},command with {Location=new(0,0,0)}})
            ManagerRejected(await FleetSend(h,invalid),"invalidInput");
        Assert.Equal(before,await FleetSave(h));
        h.RegisterParticipant(FleetOperator);var network=new NetworkSession(new(),hostFactory:(_,_)=>h);var peer=network.Connect(FleetNow,FleetOperator)!;
        var message=new CommandMessage("file-append",h.WorldEpoch,command);var pending=network.Submit(peer,message,FleetNow);network.Tick(FleetNow);Assert.IsType<AcceptedMessage>(await pending);
        before=await FleetSave(h);var replay=network.Submit(peer,message,FleetNow);network.Tick(FleetNow);Assert.Equal(await pending,await replay);Assert.Equal(before,await FleetSave(h));
        ManagerRejected(await FleetSend(h,command),"staleRevision");Assert.Equal(before,await FleetSave(h));
        Assert.Single(ManagerController(h).Harbors[0].Rules!);
    }

    [Fact]
    public async Task Rule_file_full_location_fits_transport_and_controller_capacity_rejects_complete_append()
    {
        var encoded=string.Concat(Enumerable.Repeat(WildcardGo,1024));
        using var h=new SessionHost(Timetableize(FleetLine(),[new(new(22,20,2),FleetHarborKind.Advanced,0,false)]),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var command=AppendCommand(h,encoded);Assert.True(JsonSerializer.SerializeToUtf8Bytes(new CommandMessage("max",h.WorldEpoch,command)).Length<16384);
        Assert.IsType<AcceptedMessage>(await FleetSend(h,command));Assert.Equal(1024,ManagerController(h).Harbors[0].Rules!.Length);
        var before=await FleetSave(h);ManagerRejected(await FleetSend(h,AppendCommand(h)),"invalidInput");Assert.Equal(before,await FleetSave(h));
        Assert.Throws<ArgumentException>(()=>FleetTimetable.Append([],encoded+WildcardGo));
        var full=Enumerable.Repeat(Rule(),1024).ToImmutableArray();
        var settings=Enumerable.Range(0,8).Select(x=>new FleetHarborSetting(new(x,20,2),FleetHarborKind.Advanced,0,false,full)).Append(new(new(22,20,2),FleetHarborKind.Advanced,0,false)).ToImmutableArray();
        using var capacity=new SessionHost(Timetableize(FleetLine(),settings),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        before=await FleetSave(capacity);ManagerRejected(await FleetSend(capacity,AppendCommand(capacity)),"invalidInput");Assert.Equal(before,await FleetSave(capacity));
    }
}
