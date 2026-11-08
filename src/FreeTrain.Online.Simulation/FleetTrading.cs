// ExAV 883cf7d: TrainTradingDialog.buttonOK_Click, Train.sell/remove and
// AccountManager.spend/earn. Original authors, LGPL-2.1-or-later.
using System.Collections.Immutable;
using System.Globalization;

namespace FreeTrain.Online.Simulation;

// A quote owns its detached, fully validated successor. Discarding a quote is
// cancellation; it consumes no live IDs, cash, random draws or clock callbacks.
public sealed class FleetTradePlan
{
    internal MixedSessionState Source { get; }
    internal MixedConstructionState Before { get; }
    internal MixedConstructionState Candidate { get; }
    internal long ExpectedRevision { get; }
    internal ClockState Clock { get; }
    public StockQuote? Purchase { get; }
    public bool IsSale => Purchase == null;
    public long Price { get; }
    public long NormalCapacity { get; }
    public long SeatedCapacity { get; }
    public long PackedCapacity { get; }
    public ImmutableArray<long> TrainIds { get; }

    internal FleetTradePlan(MixedSessionState source, MixedConstructionState candidate,
        ImmutableArray<long> trainIds, long price, StockQuote? purchase = null)
    {
        Source=source; Before=source.Construction; ExpectedRevision=source.Revision; Clock=source.Clock.Capture();
        Candidate=candidate; TrainIds=trainIds; Price=price; Purchase=purchase;
        if (purchase != null)
        {
            NormalCapacity=checked((long)purchase.Formation.NormalCapacity*purchase.Quantity);
            SeatedCapacity=checked((long)purchase.Formation.SeatedCapacity*purchase.Quantity);
            PackedCapacity=checked((long)purchase.Formation.PackedCapacity*purchase.Quantity);
        }
        // Includes callback cancellation, accounting prefix and queue capacity.
        source.Successor(ExpectedRevision,candidate,source.ConstructionMode);
    }
}

public static class FleetTrading
{
    public static FleetTradePlan QuotePurchase(MixedSessionState session, ContentBinding definition, int length, int quantity)
    {
        var w=session.World;
        if (w.Fleet == null)
        {
            var profile=w.Profile.FleetProfile ?? throw new InvalidOperationException("operationUnavailable");
            var initial=FleetGraph.Empty(profile,w.Spatial.NextEntityId);
            var initialized=new MixedWorld(w.Profile,w.Terrain,new(checked(w.Spatial.NextEntityId+3),w.Spatial.Entities),
                w.Voxels,w.Slopes,w.Revision,w.SpeedLimits,initial,w.Stations);
            var detached=new MixedSessionState(new(initialized,session.Construction.Accounts),session.Provenance,
                session.ConstructionMode,session.Clock.Capture(),session.Paused,session.Random.Capture(),session.Revision,
                trainCallback:session.TrainDispatcher);
            var firstQuote=QuotePurchase(detached,definition,length,quantity);
            var candidate=firstQuote.Candidate.World;
            // These members are first published together: every new identity starts at revision zero.
            var firstGraph=new FleetGraph(profile,initial.RootGroupId,candidate.Fleet!.Trains,
                candidate.Fleet.Groups.Select(g=>g with {Revision=0}),candidate.Fleet.Controllers);
            var firstWorld=new MixedWorld(w.Profile,w.Terrain,candidate.Spatial.Capture(),candidate.Voxels,candidate.Slopes,
                candidate.Revision,candidate.SpeedLimits,firstGraph,candidate.Stations);
            return Plan(session,firstWorld,firstQuote.Candidate.Accounts,firstQuote.TrainIds,firstQuote.Price,firstQuote.Purchase);
        }
        var f=w.Fleet;
        var quote=f.Profile.Resolve(definition).Quote(length,quantity,f.Trains.Length,f.Trains.Sum(t=>t.Cars.Length));
        var trains=f.Trains.ToBuilder(); var ids=ImmutableArray.CreateBuilder<long>(quantity);
        var next=w.Spatial.NextEntityId;
        for (var n=0;n<quantity;n++)
        {
            var id=next; next=checked(next+1); ids.Add(id);
            var cars=ImmutableArray.CreateBuilder<FleetCar>(length);
            for (var i=0;i<length;i++)
            {
                cars.Add(new(next,0,i,quote.Formation.Cars[i].Reference,new FleetCarState.Unplaced()));
                next=checked(next+1);
            }
            // Source TR names were process-global. Use the world-local stable ID
            // so save/restart and independent worlds cannot reuse a live identity.
            trains.Add(new(id,0,"TR"+id.ToString(CultureInfo.InvariantCulture),definition,f.RootGroupId,
                f.Simple.Id,cars.MoveToImmutable(),PurchaseMinute:session.Clock.Time.AbsoluteMinute));
        }
        var trainIds=ids.MoveToImmutable();
        var groups=f.Groups.Select(g=>g.Id==f.RootGroupId
            ? g with {Revision=checked(g.Revision+1),Members=g.Members.AddRange(trainIds)} : g);
        var graph=new FleetGraph(f.Profile,f.RootGroupId,trains,groups,f.Controllers,f.ColorTest);
        var world=w.Replace(w.Revision,w.Terrain,new(next,w.Spatial.Entities),w.Voxels,w.Slopes,w.SpeedLimits,graph);
        var accounts=session.Construction.Accounts.Book(ConstructionBooking.Spend(quote.Price),world.Revision,session.Clock.Time,"trainPurchase");
        return Plan(session,world,accounts,trainIds,quote.Price,quote);
    }

    // Source domain supports placed sale; the later browser workflow must still
    // require withdrawal before offering sale, as TrainControllerDialog does.
    public static FleetTradePlan QuoteSale(MixedSessionState session, long trainId)
    {
        var w=session.World;
        var f=w.Fleet ?? throw new InvalidOperationException("Fleet is not initialized.");
        var train=f.Train(trainId);
        var price=f.Profile.Resolve(train.Definition).ResaleValue(train.Cars.Length,train.PurchaseMinute,session.Clock.Time.AbsoluteMinute);
        var graph=new FleetGraph(f.Profile,f.RootGroupId,f.Trains.Where(t=>t.Id!=trainId),
            f.Groups.Select(g=>g.Id==train.GroupId
                ? g with {Revision=checked(g.Revision+1),Members=g.Members.Remove(trainId)} : g),f.Controllers,f.ColorTest);
        var voxels=w.Voxels.Select(v=>v.Traffic?.Car?.TrainId==trainId
            ? v with {Traffic=v.Traffic with {Car=null}} : v).ToImmutableArray();
        var changed=voxels.Where(v=>v!=w.At(v.Position)).Select(v=>v.OwnerId).ToHashSet();
        var spatial=new SpatialState(w.Spatial.NextEntityId,w.Spatial.Entities
            .Select(e=>changed.Contains(e.Id) ? e with {Revision=checked(e.Revision+1)} : e).ToImmutableArray());
        var world=w.Replace(w.Revision,w.Terrain,spatial,voxels,w.Slopes,w.SpeedLimits,graph);
        // An absent purchase date means NO earn call, even a zero receipt.
        var accounts=train.PurchaseMinute==null ? session.Construction.Accounts :
            session.Construction.Accounts.Earn(price,world.Revision,session.Clock.Time,"trainSale");
        return Plan(session,world,accounts,[trainId],price);
    }

    private static FleetTradePlan Plan(MixedSessionState session, MixedWorld world, ConstructionAccounts accounts,
        ImmutableArray<long> trainIds, long price, StockQuote? purchase = null)
    {
        if (accounts.Transactions.Length>NormalRailwayConstruction.MaximumAccountTransactions)
            throw new ArgumentException("Account transaction capacity exceeded.");
        return new(session,new(world,accounts),trainIds,price,purchase);
    }

    public static MixedSessionState Commit(MixedSessionState session, FleetTradePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var clock=session.Clock.Capture();
        if (!ReferenceEquals(session,plan.Source) || !ReferenceEquals(session.Construction,plan.Before) ||
            session.Revision!=plan.ExpectedRevision || clock.AbsoluteMinute!=plan.Clock.AbsoluteMinute ||
            clock.NextOrder!=plan.Clock.NextOrder || !clock.Pending.SequenceEqual(plan.Clock.Pending))
            throw new InvalidOperationException("staleRevision");
        return session.Successor(plan.ExpectedRevision,plan.Candidate,session.ConstructionMode);
    }
}
