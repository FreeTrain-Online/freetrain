// ExAV 883cf7d/trunk/core/world/subsidiaries/{Parameters,SubsidiaryCompany}.cs.
// Original authors, LGPL-2.1-or-later. P7 inputs, not P8 market/report dispatch.
namespace FreeTrain.Online.Simulation;

public static class CityValuation
{
    public static long Profit(long structurePrice,long landPrice)
    {
        if(structurePrice<0 || landPrice<0) throw new ArgumentException("Invalid city valuation inputs.");
        var t=checked(structurePrice+1);
        var maximum=0.01*t*Math.Log(t);
        var prime=0.01+0.01*Math.Log(t);
        var profit=structurePrice<landPrice ? prime*2*(landPrice-structurePrice)+maximum :
            prime/2*(landPrice-structurePrice)+maximum;
        return checked((long)profit);
    }
    public static CitySubsidiary Reprice(CitySubsidiary previous,long structurePrice,long landPrice) =>
        previous with {TotalLandValue=landPrice,MarketPrice=checked(previous.MarketPrice+structurePrice+landPrice+checked(10*Profit(structurePrice,landPrice)))};
}

public static class CityOwnership
{
    public static long[] Placement(MixedWorld world,Location anchor,CityStructureDefinition definition,int height)
    {
        var footprint=definition.Footprint(height);
        foreach(var p in new Cube(anchor,definition.SizeX,definition.SizeY,1).Locations())
            if(world.Terrain.At(p).Relation!=GroundRelation.Ground) throw new ArgumentException("City placement is not on level ground.");
        return Reclaimable(world,anchor,footprint);
    }
    // Pure source player-mode legality. Invisible upper cells participate. A
    // reclaimable owner is returned once, for later atomic teardown/booking.
    public static long[] Reclaimable(MixedWorld world,Location anchor,Footprint footprint)
    {
        var owners=new HashSet<long>();
        foreach(var (p,_) in footprint.At(anchor))
        {
            var cell=world.Spatial.At(p);
            if(!cell.Reusable) throw new ArgumentException("City footprint is outside or protected.");
            if(cell.Entity is {} e) owners.Add(e.Id);
        }
        return owners.Order().ToArray();
    }
}
