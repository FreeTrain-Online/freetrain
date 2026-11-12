// ExAV 883cf7d ConstructionSite, MultiSelectorController, Commercial and
// VarHeightBuilding. Original authors, LGPL-2.1-or-later.
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

// Trusted domain handoff for existing railway/train/station and later city timers.
// All work runs on detached clock/random instances; only the returned envelope is
// publishable. A missing consumer fails the entire minute, never drops an action.
public delegate MixedConstructionState CityScheduledCallback(MixedConstructionState state,
    Clock clock, RandomStreams random, string handler);

// Missing later-phase consumers are recoverable authority failures. The host
// pauses/rejects the minute while keeping cancellation and load available.
public sealed class CityConsumerUnavailableException(string consumer) : InvalidOperationException("Missing city consumer: "+consumer);

public static class CityConstruction
{
    public static ConstructionBooking StartBooking(CityStructureDefinition definition,int height,CityConstructionCaller caller)
    {
        if(!Enum.IsDefined(caller) || definition.Kind==CityStructureKind.Half) throw new ArgumentException("Invalid timed construction caller/type.");
        var price=definition.Price(height);
        return caller==CityConstructionCaller.CatalogPlayer ? ConstructionBooking.Spend(price,AccountGenre.Subsidiaries) : ConstructionBooking.None;
    }
    public static ConstructionBooking RemovalBooking(CityStructureDefinition definition,bool site) => site ? ConstructionBooking.None :
        // Preserve source Int32 arithmetic; reject overflow rather than publish a
        // wrapped negative charge. Half structures have a separate P7-11 path.
        definition.Kind==CityStructureKind.Half ? throw new ArgumentException("Half removal requires its own lifecycle.") :
        ConstructionBooking.Spend(checked(500_000*definition.SizeX*definition.SizeY),AccountGenre.Subsidiaries);

    private static MixedConstructionState Replace(MixedConstructionState before,CityState state,long nextId,
        ConstructionBooking booking,GameTime time,string operation)
    {
        var world=before.World;
        var graph=new CityGraph(world.City!.Profile,state);
        var entities=world.Spatial.Entities.Where(e=>!world.City.IsSpatialOwner(e.Id))
            .Concat(graph.Identities.Select(i=>graph.SpatialOwner(i.Id))).OrderBy(e=>e.Id).ToImmutableArray();
        return before.Replace(world.Revision,world.Terrain,new(nextId,entities),world.Voxels,world.Slopes,
            booking,time,operation,city:graph);
    }
    private static ScheduledAction Schedule(Clock clock,string handler,long delay)
    {
        var due=checked(clock.Time.AbsoluteMinute+delay);
        var registration=clock.Schedule(handler,delay);
        return new(registration,registration,due,handler,0);
    }
    // Placement/reclamation policies remain with the family/caller (P7-07/10/27).
    // This reusable primitive requires their already cleared, level footprint.
    public static CityCandidate Start(CityCandidate before,Location location,DefinitionReference definition,int height,CityConstructionCaller caller)
    {
        var world=before.Construction.World;
        var d=world.City!.Profile.Structure(definition);
        var booking=StartBooking(d,height,caller);
        if(CityOwnership.Placement(world,location,d,height).Length!=0)
            throw new ArgumentException("Construction requires atomic caller reclamation first.");
        var volume=checked(d.SizeX*d.SizeY*height);
        if(volume>Clock.MaximumPending-before.Clock.Pending.Length)
            throw new ArgumentException("City construction queue capacity exceeded.");
        var railwaySites=world.Voxels.Where(v=>v.Construction!=null).Select(v=>v.OwnerId).Distinct().Count();
        if(booking.Charges.Length>NormalRailwayConstruction.MaximumAccountTransactions-before.Construction.Accounts.Transactions.Length-railwaySites)
            throw new ArgumentException("City construction account capacity exceeded.");
        var id=world.Spatial.NextEntityId;
        var nextId=checked(id+1);
        var random=RandomStreams.Restore(before.Random);
        var pending=before.Clock.Pending.ToBuilder();var order=before.Clock.NextOrder;
        var cells=ImmutableArray.CreateBuilder<CityConstructionVoxel>();
        for(var z=0;z<height;z++) for(var x=0;x<d.SizeX;x++) for(var y=0;y<d.SizeY;y++)
        {
            var offset=new Location(x,y,z);
            var cell=ConstructionLifecycle.Initial(x==0 && y==d.SizeY-1,random["construction"].Next);
            var due=checked(before.Clock.AbsoluteMinute+ConstructionLifecycle.Delay(random["construction"].Next));
            var action=new ScheduledAction(order,order,due,CityHandlers.Construction(id,offset),0);
            order=checked(order+1);pending.Add(action);cells.Add(new(offset,cell,action));
        }
        var site=new CityStructure(id,0,definition,location,height,false,null,[],cells.ToImmutable(),caller);
        var state=world.City.State with {Structures=world.City.State.Structures.Add(site)};
        var after=Replace(before.Construction,state,nextId,booking,new(before.Clock.AbsoluteMinute),"cityConstructionStart");
        return new(after,before.Clock with {NextOrder=order,Pending=pending.OrderBy(a=>a.DueMinute).ThenBy(a=>a.Order).ToImmutableArray()},random.Capture());
    }
    public static CityCandidate Remove(CityCandidate before,long ownerId)
    {
        var world=before.Construction.World;var graph=world.City!;var s=graph.Structure(ownerId);
        var booking=RemovalBooking(graph.Profile.Structure(s.Definition),!s.Construction.IsEmpty);
        var registrations=s.Construction.Where(c=>c.Callback!=null).Select(c=>c.Callback!.Registration)
            .Concat(s.Subsidiary?.Callback is {} a ? [a.Registration] : Array.Empty<long>()).ToHashSet();
        var state=graph.State with {Structures=graph.State.Structures.Remove(s),
            Producers=graph.State.Producers.Where(p=>p.OwnerId!=ownerId).ToImmutableArray(),
            Fences=graph.State.Fences.Where(f=>f.HostOwnerId!=ownerId).ToImmutableArray()};
        var after=Replace(before.Construction,state,world.Spatial.NextEntityId,booking,new(before.Clock.AbsoluteMinute),"cityConstructionRemoval");
        return new(after,before.Clock with {Pending=before.Clock.Pending.Where(a=>!registrations.Contains(a.Registration)).ToImmutableArray()},before.Random);
    }
    // totalLandValue is the source family's actual valuation input, supplied by
    // P7-06/07/10. No constant/default valuation is substituted here.
    public static CityCandidate AdvanceOneMinute(CityCandidate before,Func<MixedWorld,CityStructure,long> totalLandValue,
        CityScheduledCallback? other = null)
    {
        ArgumentNullException.ThrowIfNull(totalLandValue);
        var working=before.Construction;
        var random=RandomStreams.Restore(before.Random);
        Clock clock=null!;
        var bindings=before.Clock.Pending.ToDictionary(a=>a.HandlerId,a=>(Action)(()=>
        {
            var graph=working.World.City!;
            var site=graph.State.Structures.SingleOrDefault(s=>s.Construction.Any(c=>c.Callback?.HandlerId==a.HandlerId));
            if(site==null)
                working=other?.Invoke(working,clock,random,a.HandlerId) ?? throw new InvalidOperationException("Missing city timer consumer: "+a.HandlerId);
            else working=Tick(working,site,a.HandlerId,clock,random,totalLandValue);
        }),StringComparer.Ordinal);
        // New handlers (subsidiary completion) need bindings before scheduling.
        // Clock bindings are immutable; install potential completion identities too.
        var next=working.World.Spatial.NextEntityId;
        var count=working.World.City!.State.Structures.Count(s=>!s.Construction.IsEmpty);
        for(var i=0;i<count;i++)
        {
            if(i>long.MaxValue-next) break; // Tick rejects actual allocator exhaustion atomically.
            var handler=CityHandlers.Subsidiary(next+i);
            bindings.TryAdd(handler,()=>throw new InvalidOperationException("New subsidiary cannot run in its construction minute."));
        }
        clock=Clock.Restore(before.Clock,bindings);
        clock.Advance();
        return new(working,clock.Capture(),random.Capture());
    }
    internal static MixedConstructionState Tick(MixedConstructionState before,CityStructure site,string handler,
        Clock clock,RandomStreams random,Func<MixedWorld,CityStructure,long> totalLandValue)
    {
        var graph=before.World.City!;
        var voxel=site.Construction.Single(c=>c.Callback?.HandlerId==handler);
        var p=voxel.Offset;
        var below=site.Construction.SingleOrDefault(c=>c.Offset==p with {Z=p.Z-1});
        var above=site.Construction.SingleOrDefault(c=>c.Offset==p with {Z=p.Z+1});
        int Floor(int z)=>site.Construction.Where(c=>c.Offset.Z==z).Min(c=>c.Cell.State);
        var allowed=ConstructionLifecycle.CanProceed(clock.Time.IsWeekend,clock.Time.HourOfDay,voxel.Cell.State,
            below?.Cell.State,below==null?0:Floor(below.Offset.Z),above?.Cell.State,above==null?0:Floor(above.Offset.Z));
        var cell=allowed ? ConstructionLifecycle.Progress(voxel.Cell,random["construction"].Next) : voxel.Cell;
        var action=cell.State<4 ? Schedule(clock,handler,ConstructionLifecycle.Delay(random["construction"].Next)) : null;
        var updated=site with {Revision=checked(site.Revision+1),Construction=site.Construction.Replace(voxel,new(p,cell,action))};
        var state=graph.State;
        var nextId=before.World.Spatial.NextEntityId;
        if(updated.Construction.All(c=>c.Cell.State==4))
        {
            // Remove site, create a fresh owner, then attach population at source
            // constructor point. Station listener edges are the P7-05/07 handoff.
            var id=nextId;nextId=checked(nextId+1);
            var d=graph.Profile.Structure(site.Definition);
            updated=updated with {Id=id,Revision=0,Owned=site.Caller!=CityConstructionCaller.Automated,Construction=[]};
            var producers=state.Producers;
            CityProducer? producer=d.Population==null ? null : new(id,d.Reference,site.Height);
            if(d.Kind==CityStructureKind.Fixed && producer!=null) producers=producers.Add(producer);
            var land=totalLandValue(before.World,updated);
            var subsidiary=CityValuation.Reprice(new(0,0),d.Price(site.Height),land);
            var delay=checked((long)(random["time-length"].NextDouble()*20160+20160));
            updated=updated with {Subsidiary=subsidiary with {Callback=Schedule(clock,CityHandlers.Subsidiary(id),delay)}};
            if(d.Kind==CityStructureKind.Variable && producer!=null) producers=producers.Add(producer);
            state=state with {Producers=producers,Fences=state.Fences.Where(f=>f.HostOwnerId!=site.Id).ToImmutableArray()};
        }
        state=state with {Structures=state.Structures.Replace(site,updated)};
        return Replace(before,state,nextId,ConstructionBooking.None,clock.Time,
            updated.Construction.IsEmpty?"cityConstructionCompletion":"cityConstructionProgress");
    }
}
