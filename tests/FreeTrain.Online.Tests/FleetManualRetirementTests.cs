using FreeTrain.Online.Contracts;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public partial class FleetMovementTests
{
    [Fact]
    public async Task Canonical_manual_commands_are_unavailable_and_do_not_mutate_the_world()
    {
        using var h=FleetHost();var f=h.Snapshot().Mixed!.Fleet!;var train=f.Trains[0];
        var before=await FleetSave(h);
        CommandPayload[] commands=[new AcquireControlCommand(train.Id.ToString()),
            new ReleaseControlCommand(train.Id.ToString(),"old-lease"),
            new SetControllerCommand(train.Id.ToString(),train.Revision,"manual","old-lease"),
            new ControllerInputCommand(train.Id.ToString(),1,true,true,true,true,true,false,"old-lease"),
            ManagerCreate() with {Provider=ManagerCreate().Provider! with {ContributionId="{5567FE11-FC2C-4a1d-836F-722EF9DC957C}"}}];
        foreach(var command in commands)
        {
            ManagerRejected(await FleetSend(h,command),"operationUnavailable");
            Assert.Equal(before,await FleetSave(h));
        }
        Assert.DoesNotContain("fleetControl",System.Text.Json.JsonSerializer.Serialize<WireMessage>(h.Snapshot(),WireJson.CreateOptions()));
    }

    [Fact]
    public void Saved_manual_controller_records_are_rejected_without_conversion()
    {
        var bytes=SaveCodec.Write(WithFixtureController(FleetLine()));
        var save=System.Text.Json.Nodes.JsonNode.Parse(bytes)!;
        var controller=save["mixed"]!["fleet"]!["controllers"]!.AsArray().Last()!;
        controller["kind"]="manual";
        controller["provider"]!["contributionId"]="{5567FE11-FC2C-4a1d-836F-722EF9DC957C}";
        Assert.Throws<InvalidDataException>(()=>SaveCodec.ReadWorld(System.Text.Encoding.UTF8.GetBytes(save.ToJsonString()),TerrainProfiles.Resolve,RailwayProfiles.Resolve));
        Assert.All(RollingStockCatalog.Bundled.FleetProfile.Providers,p=>Assert.Equal(FleetControllerKind.Timetable,p.Kind));
    }
}
