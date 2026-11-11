using System.Text.Json;
using Xunit;
namespace FreeTrain.Online.Tests;
public class StationCapacityTests
{
    [Theory]
    [InlineData("small",8,4,32)]
    [InlineData("medium",32,16,128)]
    [InlineData("large",128,64,512)]
    [InlineData("extent",128,64,512)]
    public void P6_26_constructed_station_capacity_exchanges_passengers_across_midnight_and_reopens_exactly(string mode,int stations,int trains,int providers)
    {
        var parent=Environment.GetEnvironmentVariable("FREETRAIN_STATION_CAPACITY_FIXTURES");
        var directory=parent==null?Path.Combine(Path.GetTempPath(),"station-capacity-"+Guid.NewGuid()):Path.Combine(parent,mode);
        try
        {
            StationCapacity.Run(mode,directory);
            using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory,"measurement.json")));
            var r=doc.RootElement;
            Assert.Equal(stations,r.GetProperty("stations").GetInt32());
            Assert.Equal(trains,r.GetProperty("trains").GetInt32());
            Assert.Equal(providers,r.GetProperty("providers").GetInt32());
            Assert.Equal(stations*3+trains,r.GetProperty("callbacks").GetInt32());
            Assert.True(long.Parse(r.GetProperty("imports").GetString()!)>0);
            Assert.All(r.GetProperty("waiting").EnumerateArray(),v=>Assert.True(v.GetInt32()>=0));
            Assert.Equal(120,r.GetProperty("ticks").GetArrayLength());
            Assert.Contains(r.GetProperty("ticks").EnumerateArray(),t=>t.GetProperty("midnight").GetBoolean()&&t.GetProperty("due").GetInt32()>=stations*2);
        }
        finally { if(parent==null&&Directory.Exists(directory))Directory.Delete(directory,true); }
    }
}
