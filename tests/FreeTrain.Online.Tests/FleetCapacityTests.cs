using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using Xunit;

namespace FreeTrain.Online.Tests;

public class FleetCapacityTests
{
    [Theory]
    [InlineData("small",8,32)]
    [InlineData("medium",32,256)]
    [InlineData("large",128,1024)]
    [InlineData("empty",128,1024)]
    [InlineData("network",2,16)]
    public void P5_28_legal_budgets_validate_callbacks_and_exact_reopen(string mode,int trains,int cars)
    {
        var parent=Environment.GetEnvironmentVariable("FREETRAIN_FLEET_CAPACITY_FIXTURES");
        var directory=parent==null?Path.Combine(Path.GetTempPath(),"fleet-capacity-"+Guid.NewGuid()):Path.Combine(parent,mode);
        try
        {
            FleetCapacity.Run(mode,directory);
            using var report=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory,"measurement.json")));
            Assert.Equal(trains,report.RootElement.GetProperty("trains").GetInt32());
            Assert.Equal(cars,report.RootElement.GetProperty("cars").GetInt32());
            Assert.Equal(trains,report.RootElement.GetProperty("callbacks").GetInt32());
            Assert.Equal(mode is "empty" or "heavy" or "network"?16:72,report.RootElement.GetProperty("tickMs").GetArrayLength());
            Assert.All(report.RootElement.GetProperty("moves").EnumerateArray(),m=>Assert.True(m.GetInt32()>0));
        }
        finally {if(parent==null)Directory.Delete(directory,true);}
    }

    [Fact]
    public void P5_28_overlong_ring_formations_reject_without_changing_source()
    {
        var directory=Path.Combine(Path.GetTempPath(),"fleet-capacity-"+Guid.NewGuid());
        try
        {
            RailwayCapacity.Run("small",directory);
            var bytes=File.ReadAllBytes(Path.Combine(directory,"world.json"));
            var source=SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
            Assert.Throws<InvalidOperationException>(()=>FleetCapacity.Populate(source,4,19));
            Assert.Equal(bytes,SaveCodec.Write(source));
        }
        finally {Directory.Delete(directory,true);}
    }
}
