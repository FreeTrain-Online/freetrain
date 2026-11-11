// Canonical detached contracts from ExAV 883cf7d: LandPropertyVoxel, LandVoxel,
// PThreeDimStructure, VarHeightBuilding, HVStructure, ULV and SearchPlan.
// Original authors, LGPL-2.1-or-later. Detached lifecycle: CityConstruction.
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;

namespace FreeTrain.Online.Simulation;

public enum HalfSide { Back, Fore }
public static class CityHandlers
{
    public const string Heat = "city/heat";
    public const string Development = "city/development";
    public static string Subsidiary(long id) => "city/subsidiary/"+id.ToString(CultureInfo.InvariantCulture);
    public static string Construction(long id,Location offset) => string.Create(CultureInfo.InvariantCulture,
        $"city/construction/{id}/{offset.X}/{offset.Y}/{offset.Z}");
}
public sealed record CityHalf(DefinitionReference Definition, HalfSide Side, int Direction, int Color = 0);
public sealed record CityProperty(long Id, long Revision, Location Location);
public sealed record CityFenceHost(long Id, long Revision, Location Location);
public sealed record CityGround(long Id, long Revision, DefinitionReference Definition, Location Location,
    bool Owned, int Variant = 0);
// The subsidiary shares the structure's stable identity. P8 owns market lists,
// accounting/history and events; P7 retains the actual cumulative valuation inputs.
public sealed record CitySubsidiary(long MarketPrice, long TotalLandValue, ScheduledAction? Callback = null);
public sealed record CityConstructionVoxel(Location Offset, ConstructionCell Cell, ScheduledAction? Callback);
public enum CityConstructionCaller { LegacyPlayer, CatalogPlayer, Automated }
public sealed record CityStructure(long Id, long Revision, DefinitionReference Definition, Location Location,
    int Height, bool Owned, CitySubsidiary? Subsidiary,
    ImmutableArray<CityHalf> Halves, ImmutableArray<CityConstructionVoxel> Construction,
    CityConstructionCaller Caller = CityConstructionCaller.LegacyPlayer);
// Retain the initial half's population binding even after that half is removed.
// Listener edges remain exclusively in StationGraph; no station population copy.
public sealed record CityProducer(long OwnerId, DefinitionReference InitialDefinition, int InitialHeight);
// Voxel.setFence decorates one actual voxel, including Traffic/structures/terrain.
// A vacant site uses a real DummyVoxel spatial owner, not a parallel fence grid.
public sealed record CityFence(long HostOwnerId, Location Location, int Side, DefinitionReference Definition);
public enum CitySearchPhase { Start, SelectStruct1, SelectStruct2, FitPlace, Build, End }
public sealed record CitySelectedPlan(DefinitionReference Definition, Location Site, int SizeX, int SizeY,
    int Height, long Value, long FinalPrice);
public sealed record CitySearchPlan(long StationId, CitySearchPhase Phase, double Strength, int Budget,
    ImmutableArray<int> Work, Location? Scanning, CitySelectedPlan? Selected);
// A non-null ULV is observably stale in the source: its sampled values ARE state.
// Null results retry; dictionary indexes and sorted candidate pools are rebuildable.
public sealed record CityLandCache(Location Corner, int SizeX, int SizeY, long LandValue, long EntityValue);
public sealed record CityDevelopmentSettings(double StrengthDiffusion = 0.2, double PopulationPower = 1,
    int PopulationBase = 10, double PopulationScale = 0.1, double LandPriceScale = 1,
    double ReplacementFactor = 2.5, double MaximumPricePower = 0.2,
    float Alpha = 0.240f, float Diffusion = 0.99f, int UpdateFrequencyHours = 10,
    double LandValuePower = (double)0.3f, float BareConductivity = 0.80f)
{
    public void Validate()
    {
        if(new[]{StrengthDiffusion,PopulationPower,PopulationScale,LandPriceScale,ReplacementFactor,MaximumPricePower,
                LandValuePower,(double)Alpha,Diffusion,BareConductivity}.Any(v=>!double.IsFinite(v)) ||
            UpdateFrequencyHours<1 || MaximumPricePower==0 || !double.IsFinite(1/MaximumPricePower))
            throw new ArgumentException("Invalid city settings state.");
        // Reachable world/content-specific powers/products/Next bounds require
        // P7-28's full admission validation. No finite value gets blanket runtime
        // approval from this representation check, nor an invented balance cap.
    }
}
public sealed record CityHeatState(int HorizontalSize, int VerticalSize, ImmutableArray<float> Q,
    ImmutableArray<float> Back, ImmutableArray<float> Rho)
{
    // H-major, V-minor includes both border rows/columns. Never recreate retained
    // borders or the back buffer from current terrain during capture/restore.
    public int Index(int h,int v)
    {
        if(h<0 || h>HorizontalSize+1 || v<0 || v>VerticalSize+1) throw new ArgumentOutOfRangeException(nameof(h));
        return checked(h*(VerticalSize+2)+v);
    }
    public void Validate(WorldSpace space)
    {
        var n=checked((space.HorizontalSize+2)*(space.VerticalSize+2));
        if(HorizontalSize!=space.HorizontalSize || VerticalSize!=space.VerticalSize ||
            Q.IsDefault || Back.IsDefault || Rho.IsDefault || Q.Length!=n || Back.Length!=n || Rho.Length!=n ||
            Q.Any(v=>!float.IsFinite(v)) || Back.Any(v=>!float.IsFinite(v)) || Rho.Any(v=>!float.IsFinite(v)||v<0))
            throw new ArgumentException("Invalid city heat buffers.");
    }
    public static CityHeatState Empty(WorldSpace space, float bare = 0.80f)
    {
        var n=checked((space.HorizontalSize+2)*(space.VerticalSize+2));
        var rho=new float[n];
        for(var h=1;h<=space.HorizontalSize;h++) for(var v=1;v<=space.VerticalSize;v++) rho[h*(space.VerticalSize+2)+v]=bare;
        var result=new CityHeatState(space.HorizontalSize,space.VerticalSize,new float[n].ToImmutableArray(),new float[n].ToImmutableArray(),rho.ToImmutableArray());
        result.Validate(space); return result;
    }
}
public sealed record CityState(ImmutableArray<CityProperty> Properties, ImmutableArray<CityStructure> Structures,
    ImmutableArray<CityGround> Ground, ImmutableArray<CityProducer> Producers, ImmutableArray<CityFence> Fences,
    CityHeatState Heat, CityDevelopmentSettings Settings, ImmutableArray<CitySearchPlan> Plans,
    ImmutableArray<CityLandCache> LandCache, ImmutableArray<CityFenceHost> FenceHosts,
    ScheduledAction? HeatCallback = null, ScheduledAction? DevelopmentCallback = null);

public sealed class CityGraph
{
    public CityProfile Profile { get; }
    public CityState State { get; }
    private readonly FrozenDictionary<long, CityStructure> structures;
    private readonly FrozenDictionary<long, CityGround> ground;
    private readonly FrozenSet<long> properties;
    private readonly FrozenDictionary<long,CityFenceHost> fenceHosts;
    public ImmutableArray<(long Id,long Revision)> Identities { get; }
    public CityGraph(CityProfile profile, CityState state)
    {
        ArgumentNullException.ThrowIfNull(profile); ArgumentNullException.ThrowIfNull(state);
        Profile=profile; State=state;
        if(state.Properties.IsDefault || state.Structures.IsDefault || state.Ground.IsDefault || state.Producers.IsDefault ||
            state.Fences.IsDefault || state.Plans.IsDefault || state.LandCache.IsDefault || state.FenceHosts.IsDefault || state.Heat==null || state.Settings==null ||
            state.Properties.Any(p=>p==null) || state.Structures.Any(s=>s==null) || state.Ground.Any(g=>g==null) || state.FenceHosts.Any(f=>f==null))
            throw new ArgumentException("Incomplete city graph.");
        state.Settings.Validate();
        Identities=state.Properties.Select(p=>(p.Id,p.Revision)).Concat(state.Structures.Select(s=>(s.Id,s.Revision)))
            .Concat(state.Ground.Select(g=>(g.Id,g.Revision))).Concat(state.FenceHosts.Select(f=>(f.Id,f.Revision))).ToImmutableArray();
        if(Identities.Length>SpatialIndex.MaximumOccupiedCells || Identities.Any(i=>i.Id<1||i.Revision<0) ||
            Identities.Select(i=>i.Id).Distinct().Count()!=Identities.Length)
            throw new ArgumentException("Invalid city identity graph.");
        structures=state.Structures.ToFrozenDictionary(s=>s.Id); ground=state.Ground.ToFrozenDictionary(g=>g.Id);
        properties=state.Properties.Select(p=>p.Id).ToFrozenSet();
        fenceHosts=state.FenceHosts.ToFrozenDictionary(f=>f.Id);
        foreach(var s in state.Structures) ValidateStructure(s);
        foreach(var g in state.Ground)
        {
            var d=profile.GroundDefinition(g.Definition);
            if(d.Handler is not ("land-0d" or "rice" or "vinyl") || g.Variant<0 ||
                d.Handler=="land-0d" && g.Variant!=0 || d.Handler=="rice" && g.Variant>8 || d.Handler=="vinyl" && g.Variant>2)
                throw new ArgumentException("Invalid realized city ground.");
        }
        if(state.Producers.Any(p=>p==null || !IsProducer(p)) || state.Producers.Select(p=>p.OwnerId).Distinct().Count()!=state.Producers.Length)
            throw new ArgumentException("Invalid city producer reference/lifecycle.");
        // Every population-bearing completed owner has exactly one producer.
        foreach(var s in state.Structures)
            if(s.Construction.IsEmpty && HasPopulation(s) != state.Producers.Any(p=>p.OwnerId==s.Id))
                throw new ArgumentException("Missing/unexpected completed city producer.");
        foreach(var g in state.Ground)
            if((profile.GroundDefinition(g.Definition).Population!=null) != state.Producers.Any(p=>p.OwnerId==g.Id))
                throw new ArgumentException("Missing/unexpected ground producer.");
        if(state.Fences.Any(f=>f==null || f.HostOwnerId<1 || f.Side is <0 or >3 ||
                profile.GroundDefinition(f.Definition).Handler!="fence" ||
                profile.Bindings.Single(b=>b.Reference==f.Definition).Implementation.EndsWith("FenceRemover",StringComparison.Ordinal)) ||
            state.Fences.Select(f=>(f.Location,f.Side)).Distinct().Count()!=state.Fences.Length)
            throw new ArgumentException("Invalid fence decoration.");
        foreach(var p in state.Plans)
        {
            if(p==null || p.StationId<1 || !Enum.IsDefined(p.Phase) || !double.IsFinite(p.Strength) || p.Budget<0 ||
                p.Work.IsDefault || p.Work.Length!=4 || p.Work.Any(i=>i<0) ||
                p.Phase is CitySearchPhase.SelectStruct1 or CitySearchPhase.SelectStruct2 or CitySearchPhase.FitPlace or CitySearchPhase.Build && p.Scanning==null ||
                p.Phase is CitySearchPhase.FitPlace or CitySearchPhase.Build && p.Selected==null)
                throw new ArgumentException("Invalid city search continuation.");
            if(p.Selected is {} plan)
            {
                if(plan.SizeX<1 || plan.SizeY<1 || plan.Height<1 || plan.Value<0 || plan.FinalPrice<0)
                    throw new ArgumentException("Invalid city selected plan.");
                if(profile.Structures.Any(d=>d.Reference==plan.Definition))
                {
                    var d=profile.Structure(plan.Definition); d.Price(plan.Height);
                    if(d.ComputerCannotBuild || d.Kind==CityStructureKind.Half || plan.SizeX!=d.SizeX || plan.SizeY!=d.SizeY ||
                        plan.Value!=d.Price(plan.Height) || plan.FinalPrice!=plan.Value)
                        throw new ArgumentException("Invalid selected structure definition.");
                }
                else
                {
                    var d=profile.GroundDefinition(plan.Definition);
                    if(d.ComputerCannotBuild || plan.Height!=1 || plan.Value!=checked(d.Price*4) ||
                        plan.FinalPrice!=checked(d.Price*plan.SizeX*plan.SizeY))
                        throw new ArgumentException("Invalid selected ground definition.");
                }
            }
        }
        if(state.Plans.Select(p=>p.StationId).Distinct().Count()!=state.Plans.Length ||
            state.LandCache.Any(c=>c==null || c.SizeX<1 || c.SizeY<1 || c.LandValue<0 || c.EntityValue<0) ||
            state.LandCache.Select(c=>(c.Corner,c.SizeX,c.SizeY)).Distinct().Count()!=state.LandCache.Length)
            throw new ArgumentException("Invalid city search/cache graph.");
        foreach(var a in Callbacks) ValidateCallback(a);
        if(state.HeatCallback is {} heat && (heat.HandlerId!=CityHandlers.Heat || heat.Interval<=0) ||
            state.DevelopmentCallback is {} development && (development.HandlerId!=CityHandlers.Development || development.Interval<=0))
            throw new ArgumentException("Invalid city repeated timer binding.");
        if(Callbacks.Length>Clock.MaximumPending || Callbacks.Select(a=>a.HandlerId).Distinct().Count()!=Callbacks.Length ||
            Callbacks.Select(a=>a.Registration).Distinct().Count()!=Callbacks.Length || Callbacks.Select(a=>a.Order).Distinct().Count()!=Callbacks.Length)
            throw new ArgumentException("Duplicate city callback handles.");
    }
    private bool HasPopulation(CityStructure s) => Profile.Structure(s.Definition).Population!=null;
    private bool IsProducer(CityProducer p)
    {
        if(structures.TryGetValue(p.OwnerId,out var s))
        {
            var d=Profile.Structure(p.InitialDefinition);
            return s.Construction.IsEmpty && d.Population!=null && p.InitialHeight>=d.MinimumHeight && p.InitialHeight<=d.MaximumHeight &&
                p.InitialDefinition==s.Definition && p.InitialHeight==s.Height;
        }
        return ground.TryGetValue(p.OwnerId,out var g) && p.InitialDefinition==g.Definition && p.InitialHeight==1 &&
            Profile.GroundDefinition(p.InitialDefinition).Population!=null;
    }
    private void ValidateStructure(CityStructure s)
    {
        var d=Profile.Structure(s.Definition); d.Price(s.Height);
        if(!Enum.IsDefined(s.Caller) || s.Halves.IsDefault || s.Construction.IsDefault || (d.Kind==CityStructureKind.Half) != !s.Halves.IsEmpty ||
            s.Halves.Length>2 || s.Subsidiary is {} sub && (sub.MarketPrice<0 || sub.TotalLandValue<0))
            throw new ArgumentException("Invalid city structure payload.");
        if(!s.Halves.IsEmpty)
        {
            if(!s.Owned || s.Halves.Any(h=>h==null || !Enum.IsDefined(h.Side) || h.Direction is <0 or >7 || h.Direction%2!=0 || h.Color<0 ||
                    Profile.Structure(h.Definition).Kind!=CityStructureKind.Half ||
                    !Profile.Structure(h.Definition).HalfVariants.Contains(new(h.Direction,(int)h.Side,h.Color))) ||
                s.Halves.Select(h=>h.Side).Distinct().Count()!=s.Halves.Length || s.Halves.Select(h=>h.Direction%4).Distinct().Count()!=1 ||
                !s.Construction.IsEmpty)
                throw new ArgumentException("Invalid half sharing axis/side.");
        }
        if(!s.Construction.IsEmpty)
        {
            var offsets=d.Footprint(s.Height).Members.Select(m=>m.Offset).ToHashSet();
            if(s.Owned || s.Subsidiary!=null || s.Construction.Length!=offsets.Count ||
                s.Construction.Any(c=>c==null || c.Cell==null || c.Cell.State is <0 or >4 || c.Cell.Ground is <0 or >6 ||
                    c.Cell.NeedsDoor && c.Offset.X==0 && c.Offset.Y==d.SizeY-1 ||
                    !offsets.Remove(c.Offset) || (c.Cell.State<4)!=(c.Callback!=null) || c.Callback is {} a && a.Interval!=0) ||
                offsets.Count!=0 || s.Construction.All(c=>c.Cell.State==4))
                throw new ArgumentException("Invalid city construction cells/ownership.");
        }
        else if(s.Subsidiary==null) throw new ArgumentException("Completed city structure requires subsidiary inputs.");
        if(s.Construction.Any(c=>c.Callback is {} a && a.HandlerId!=CityHandlers.Construction(s.Id,c.Offset)) ||
            s.Subsidiary?.Callback is {} subCallback && (subCallback.HandlerId!=CityHandlers.Subsidiary(s.Id) || subCallback.Interval!=0))
            throw new ArgumentException("Invalid city owner callback binding.");
    }
    public bool IsSpatialOwner(long id) => structures.ContainsKey(id)||ground.ContainsKey(id)||properties.Contains(id)||fenceHosts.ContainsKey(id);
    public CityStructure Structure(long id) => structures.TryGetValue(id,out var s) ? s : throw new ArgumentException("Unknown city structure.");
    public Population ProducerPopulation(long ownerId)
    {
        var p=State.Producers.Single(p=>p.OwnerId==ownerId);
        return structures.ContainsKey(ownerId) ? Profile.Structure(p.InitialDefinition).PopulationAt(p.InitialHeight)! :
            Profile.GroundDefinition(p.InitialDefinition).Population!;
    }
    public long StructurePrice(CityStructure s) => s.Halves.IsEmpty ? Profile.Structure(s.Definition).Price(s.Height) :
        s.Halves.Aggregate(0L,(sum,h)=>checked(sum+Profile.Structure(h.Definition).UnitPrice));
    public SpatialEntity SpatialOwner(long id)
    {
        if(structures.TryGetValue(id,out var s))
        {
            var d=Profile.Structure(s.Definition);
            var value=s.Construction.IsEmpty ? d.Kind==CityStructureKind.Variable ? StructurePrice(s) : s.Subsidiary!.MarketPrice : 0;
            return new(id,s.Revision,s.Definition,s.Location,d.Footprint(s.Height),s.Owned,false,value);
        }
        if(ground.TryGetValue(id,out var g)) return new(id,g.Revision,g.Definition,g.Location,Footprint.Box(new(new(0,0,0),1,1,1),true),
            g.Owned,true,Profile.GroundDefinition(g.Definition).Price);
        if(fenceHosts.TryGetValue(id,out var f)) return new(id,f.Revision,Profile.FenceHostDefinition,f.Location,
            Footprint.Box(new(new(0,0,0),1,1,1),true),true,true,0);
        var p=State.Properties.Single(p=>p.Id==id);
        return new(id,p.Revision,Profile.PropertyDefinition,p.Location,Footprint.Box(new(new(0,0,0),1,1,1)),true,true,0);
    }
    public ImmutableArray<ScheduledAction> Callbacks => State.Structures.SelectMany(s=>s.Construction.Where(c=>c.Callback!=null).Select(c=>c.Callback!))
        .Concat(State.Structures.Where(s=>s.Subsidiary?.Callback!=null).Select(s=>s.Subsidiary!.Callback!))
        .Concat(new[]{State.HeatCallback,State.DevelopmentCallback}.OfType<ScheduledAction>()).ToImmutableArray();
    private static void ValidateCallback(ScheduledAction a)
    {
        if(a.Registration<0 || a.Order<a.Registration || a.DueMinute<0 || a.DueMinute>GameTime.MaximumMinute || a.Interval<0 ||
            a.Interval>GameTime.MaximumMinute || string.IsNullOrWhiteSpace(a.HandlerId)) throw new ArgumentException("Invalid city callback.");
    }
    internal void ValidateWorld(MixedWorld world)
    {
        if(world.Profile.City!=Profile) throw new ArgumentException("City profile mismatch.");
        State.Heat.Validate(world.Terrain.Space);
        var entities=world.Spatial.Entities.ToDictionary(e=>e.Id);
        var reserved=(world.Stations?.Identities.Select(i=>i.Id) ?? []).Concat(world.Fleet?.Identities.Select(i=>i.Id) ?? [])
            .Concat(world.Terrain.Spatial.Entities.Select(e=>e.Id)).ToHashSet();
        foreach(var (id,_) in Identities)
        {
            var expected=SpatialOwner(id);
            if(id>=world.Spatial.NextEntityId || reserved.Contains(id) || !entities.TryGetValue(id,out var e) ||
                e with {Footprint=expected.Footprint}!=expected || !e.Footprint.Members.ToHashSet().SetEquals(expected.Footprint.Members) ||
                e.Footprint.At(e.Anchor).Any(c=>world.At(c.Position)!=null)) throw new ArgumentException("Invalid city spatial owner.");
        }
        foreach(var f in State.Fences)
            if(world.Spatial.At(f.Location).Entity?.Id!=f.HostOwnerId) throw new ArgumentException("Dangling fence voxel host.");
        foreach(var p in State.Plans)
        {
            if(world.Stations==null || !world.Stations.Buildings.Any(b=>b.Id==p.StationId) ||
                p.Scanning is {} scan && !world.Terrain.Space.ContainsVoxel(scan)) throw new ArgumentException("Dangling city search station/site.");
            if(p.Selected is {} plan && new Cube(plan.Site,plan.SizeX,plan.SizeY,plan.Height).Locations().Any(l=>!world.Terrain.Space.ContainsVoxel(l)))
                throw new ArgumentException("Selected city plan outside world.");
        }
        foreach(var c in State.LandCache)
            if(new Cube(c.Corner,c.SizeX,c.SizeY,1).Locations().Any(l=>!world.Terrain.Space.ContainsVoxel(l)))
                throw new ArgumentException("City cache outside world.");
        // P7-05 extends StationProvider.Input and codecs. Until then city producer
        // descriptors cannot masquerade as P6 controlled listener payloads.
        if(world.Stations?.Listeners.Any(l=>IsSpatialOwner(l.ProviderId))==true)
            throw new ArgumentException("City station listener publication requires P7-05 producer boundary.");
    }
    internal void ValidateSuccessor(CityGraph previous,long allocator)
    {
        if(Profile!=previous.Profile) throw new ArgumentException("City profile changed.");
        var old=previous.Identities.ToDictionary(i=>i.Id,i=>i.Revision);
        foreach(var (id,revision) in Identities)
        {
            if(!old.TryGetValue(id,out var rev))
            { if(id<allocator || revision!=0) throw new ArgumentException("Reused city identity."); }
            else if(revision!=rev && revision!=checked(rev+1)) throw new ArgumentException("Invalid city revision.");
            else if(previous.structures.ContainsKey(id)!=structures.ContainsKey(id) || previous.ground.ContainsKey(id)!=ground.ContainsKey(id) ||
                previous.properties.Contains(id)!=properties.Contains(id) || previous.fenceHosts.ContainsKey(id)!=fenceHosts.ContainsKey(id) ||
                previous.SpatialOwner(id).Definition!=SpatialOwner(id).Definition || previous.SpatialOwner(id).Anchor!=SpatialOwner(id).Anchor ||
                structures.TryGetValue(id,out var s) && (s.Height!=previous.structures[id].Height || s.Caller!=previous.structures[id].Caller))
                throw new ArgumentException("City owner kind cannot change.");
            else if(revision==rev && !SamePayload(previous,id)) throw new ArgumentException("Changed city payload requires revision.");
            if(old.ContainsKey(id) && previous.State.Producers.SingleOrDefault(p=>p.OwnerId==id) is {} p &&
                State.Producers.SingleOrDefault(p=>p.OwnerId==id)!=p)
                throw new ArgumentException("Initial city population snapshot cannot change.");
        }
    }
    private bool SamePayload(CityGraph before,long id)
    {
        var a=before.structures.GetValueOrDefault(id);var b=structures.GetValueOrDefault(id);
        return (a==null ? b==null : b!=null && a with {Halves=b.Halves,Construction=b.Construction}==b &&
                a.Halves.SequenceEqual(b.Halves) && a.Construction.SequenceEqual(b.Construction)) &&
            before.State.Properties.SingleOrDefault(p=>p.Id==id)==State.Properties.SingleOrDefault(p=>p.Id==id) &&
            before.ground.GetValueOrDefault(id)==ground.GetValueOrDefault(id) &&
            before.fenceHosts.GetValueOrDefault(id)==fenceHosts.GetValueOrDefault(id) &&
            before.State.Producers.SingleOrDefault(p=>p.OwnerId==id)==State.Producers.SingleOrDefault(p=>p.OwnerId==id) &&
            SameFences(before,id);
    }
    internal bool SameFences(CityGraph? before,long id) =>
        (before?.State.Fences ?? []).Where(f=>f.HostOwnerId==id).ToHashSet().SetEquals(State.Fences.Where(f=>f.HostOwnerId==id));
    public static CityGraph Empty(CityProfile profile,WorldSpace space) => new(profile,new([],[],[],[],[],CityHeatState.Empty(space),new(),[],[],[]));
}

// Detached continuation envelope: use the existing world-local queue/random bank,
// never independent static RNGs or a second scheduler. CityConstruction dispatches sites.
public sealed class CityCandidate
{
    public MixedConstructionState Construction { get; }
    public ClockState Clock { get; }
    public RandomStreamsState Random { get; }
    public CityCandidate(MixedConstructionState construction,ClockState clock,RandomStreamsState random)
    {
        var graph=construction.World.City ?? throw new ArgumentException("Missing city graph.");
        var callbacks=graph.Callbacks;
        var railwaySites=construction.World.Voxels.Where(v=>v.Construction!=null).Select(v=>v.OwnerId).Distinct().Count();
        if(construction.Accounts.Transactions.Length+railwaySites>NormalRailwayConstruction.MaximumAccountTransactions ||
            callbacks.Where(a=>a.HandlerId.StartsWith("city/construction/",StringComparison.Ordinal))
                .Any(a=>a.DueMinute-clock.AbsoluteMinute>119))
            throw new ArgumentException("City account/schedule capacity exceeded.");
        if(callbacks.Any(a=>!clock.Pending.Contains(a))) throw new ArgumentException("Missing city callback registration.");
        var railway=StationaryConstruction.Bindings(construction.World).Keys.ToHashSet(StringComparer.Ordinal);
        if(clock.Pending.Any(a=>railway.Contains(a.HandlerId) && (a.Interval!=0 || a.DueMinute-clock.AbsoluteMinute>119)))
            throw new ArgumentException("Invalid railway construction schedule in city candidate.");
        var trains=(construction.World.Fleet?.Trains ?? []).Where(t=>t.State!=TrainOperationalState.Unplaced).Select(t=>MixedSessionState.TrainHandler(t.Id));
        var station=StationLifecycle.Timers(construction.World.Stations);
        var expected=railway.Concat(trains).Concat(station.Select(a=>a.HandlerId)).Concat(callbacks.Select(a=>a.HandlerId)).ToArray();
        if(expected.Distinct(StringComparer.Ordinal).Count()!=expected.Length || clock.Pending.IsDefault ||
            clock.Pending.Length!=expected.Length || clock.Pending.Select(a=>a.HandlerId).Distinct(StringComparer.Ordinal).Count()!=expected.Length)
            throw new ArgumentException("Invalid detached city queue membership.");
        var bindings=expected.ToDictionary(id=>id,_=>(Action)(()=>{}),StringComparer.Ordinal);
        Simulation.Clock.Restore(clock,bindings); RandomStreams.Restore(random);
        StationLifecycle.Validate(construction.World.Stations,clock);
        if(construction.Accounts.Transactions.Any(t=>t.AbsoluteMinute>clock.AbsoluteMinute) ||
            construction.World.Fleet?.Trains.Any(t=>t.PurchaseMinute>clock.AbsoluteMinute)==true)
            throw new ArgumentException("City booking is newer than clock.");
        Construction=construction; Clock=clock; Random=random;
    }
}
