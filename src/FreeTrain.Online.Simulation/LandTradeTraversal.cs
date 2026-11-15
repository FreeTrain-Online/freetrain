// ExAV 883cf7d LandPropertyController; original authors, LGPL-2.1-or-later.
// Shared source traversal, independent of geometry realization. Keeping this
// small engine separate allows the pinned numeric vectors to test the SAME
// deduplication and ordered bookings used by the live rectangle tool.
namespace FreeTrain.Online.Simulation;

public sealed record LandTradeOwner(long Id,bool Owned,bool Reclaimable,long Value,bool Property=false);
public sealed class LandTradeTraversal(bool sell)
{
    public HashSet<long> Removed { get; } = [];
    public List<AccountCharge> Charges { get; } = [];
    public List<long> Revenues { get; } = [];
    public long Amount { get; private set; }
    public string Visit(long value,LandTradeOwner? owner,Func<long,ConstructionBooking> reclaim)
    {
        if(value<0 || owner?.Value<0) throw new ArgumentException("Invalid land trade value.");
        if(owner!=null && Removed.Contains(owner.Id)) owner=null;
        if(sell)
        {
            if(owner?.Property!=true)return "notProperty";
            Removed.Add(owner.Id);Revenues.Add(value);Amount=checked(Amount+value);return "sellable";
        }
        if(owner is {Owned:false,Reclaimable:true})
        {
            var removal=reclaim(owner.Id);
            Charges.Add(new(AccountGenre.Subsidiaries,owner.Value));Charges.AddRange(removal.Charges);
            Amount=checked(Amount+checked(owner.Value+removal.Total));Removed.Add(owner.Id);owner=null;
        }
        if(owner!=null)return owner.Owned?"owned":"protected";
        Charges.Add(new(AccountGenre.Subsidiaries,value));Amount=checked(Amount+value);return "buyable";
    }
}
