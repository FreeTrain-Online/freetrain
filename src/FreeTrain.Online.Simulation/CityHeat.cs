// ExAV 883cf7d/trunk/core/world/development/LandValue.cs; LGPL-2.1-or-later.
// Keep Single operations separate: regrouping or fused arithmetic changes saves.
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public static class CityHeat
{
    public const float RoadConductivity = 0.999f;
    public static int Value(CityHeatState heat, CityDevelopmentSettings settings, int h, int v)
    {
        // CITY-D001 (doc/web-parity-city-contract.md): ExAV reads [h+1,v+1]
        // but deposits and conductivity updates use [h,v]. This appears to be
        // an indexing defect, not a documented gameplay rule; preserve it for
        // compatibility. Any correction must review all three accesses, retained
        // borders and odd/even neighbor geometry together, not just this getter.
        var value = Math.Pow(heat.Q[heat.Index(checked(h+1),checked(v+1))],settings.LandValuePower);
        if (!double.IsFinite(value) || value < 0) throw new ArgumentException("Invalid land-value power result.");
        return checked(checked((int)value)*10); // truncate BEFORE multiplying
    }
    public static int Value(MixedWorld world, Location location)
    {
        var graph=world.City ?? throw new CityConsumerUnavailableException("land-value");
        var p=world.Terrain.Space.ToStorage(location);
        return Value(graph.State.Heat,graph.State.Settings,p.H,p.V);
    }
    public static long TotalLandValue(MixedWorld world, CityStructure structure)
    {
        var d=world.City!.Profile.Structure(structure.Definition);
        var center=d.Kind==CityStructureKind.Half ? structure.Location :
            new Location(checked(structure.Location.X+d.SizeX/2),checked(structure.Location.Y+d.SizeY/2),structure.Location.Z);
        var value=Value(world,center);
        return d.Kind==CityStructureKind.Half ? value : checked(checked(value*d.SizeX)*d.SizeY);
    }
    public static CityHeatState Deposit(CityHeatState heat, CityDevelopmentSettings settings, int h, int v, float amount)
    {
        settings.Validate();
        if (!float.IsFinite(amount)) throw new ArgumentException("Invalid heat deposit.");
        // Intentionally preserve ExAV's unshifted deposit under CITY-D001.
        // Value reads the next H/V cell; edge deposits can enter padding that
        // diffusion never updates. See the compatibility explanation in Value.
        var index=heat.Index(h,v);
        var deposit=amount*settings.UpdateFrequencyHours;
        deposit=deposit/4;
        var value=heat.Q[index]+deposit;
        if (!float.IsFinite(deposit) || !float.IsFinite(value)) throw new ArgumentException("Heat deposit overflow.");
        return heat with {Q=heat.Q.SetItem(index,value)};
    }
    public static MixedWorld Deposit(MixedWorld world, Location location, float amount)
    {
        var graph=world.City ?? throw new CityConsumerUnavailableException("land-value");
        var p=world.Terrain.Space.ToStorage(location);
        return Replace(world,graph.State with {Heat=Deposit(graph.State.Heat,graph.State.Settings,p.H,p.V,amount)});
    }
    public static CityHeatState Diffuse(CityHeatState heat, CityDevelopmentSettings settings)
    {
        settings.Validate();
        var back=heat.Q; var rho=heat.Rho; var q=heat.Back.ToBuilder();
        for(var h=heat.HorizontalSize;h>0;h--) for(var v=heat.VerticalSize;v>0;v--)
        {
            var i=heat.Index(h,v);var t=back[i];var side=v%2==0 ? h+1 : h-1;
            var n1=heat.Index(h,v-1);var n2=heat.Index(h,v+1);
            var n3=heat.Index(side,v-1);var n4=heat.Index(side,v+1);
            float tr;
            if(settings.BareConductivity<rho[i])
            {
                var dt=Math.Max(0,back[n1]-t);tr=dt*dt;
                dt=Math.Max(0,back[n2]-t);tr=tr+dt*dt;
                dt=Math.Max(0,back[n3]-t);tr=tr+dt*dt;
                dt=Math.Max(0,back[n4]-t);tr=tr+dt*dt;
                var root=(float)Math.Sqrt(tr*settings.Alpha);
                t=back[i]*RoadConductivity+root;
            }
            else
            {
                tr=(back[n1]-t)*rho[n1];
                tr=tr+(back[n2]-t)*rho[n2];
                tr=tr+(back[n3]-t)*rho[n3];
                tr=tr+(back[n4]-t)*rho[n4];
                var change=tr*settings.Alpha;change=change*rho[i];
                t=back[i]*settings.Diffusion+change;
            }
            // Reject non-finite intermediates even when the final clamp could hide them.
            if(!float.IsFinite(tr)||!float.IsFinite(t)) throw new ArgumentException("Heat diffusion overflow.");
            q[i]=t<0 ? 0 : t;
        }
        return heat with {Q=q.ToImmutable(),Back=back};
    }
    public static MixedConstructionState Tick(MixedConstructionState before, Clock clock)
    {
        var graph=before.World.City!;var state=graph.State;
        var timer=state.HeatCallback ?? throw new ArgumentException("Missing heat timer.");
        if(timer.DueMinute!=clock.Time.AbsoluteMinute) throw new ArgumentException("Heat timer is not due.");
        var next=timer with {Order=clock.NextOrder,DueMinute=checked(timer.DueMinute+timer.Interval)};
        return new(Replace(before.World,state with {Heat=Diffuse(state.Heat,state.Settings),HeatCallback=next}),before.Accounts);
    }
    // Explicit first activation; restoration never calls this or rescans terrain.
    public static MixedSessionState Enable(MixedSessionState before)
    {
        if(before.World.City!=null) return before;
        var world=before.World;
        var profile=world.Profile.City ?? throw new CityConsumerUnavailableException("land-value");
        var state=CityGraph.Empty(profile,world.Terrain.Space).State;
        var clock=before.Clock.Capture();var interval=checked(60L*state.Settings.UpdateFrequencyHours);
        var timer=new ScheduledAction(clock.NextOrder,clock.NextOrder,checked(clock.AbsoluteMinute+interval),CityHandlers.Heat,interval);
        var candidate=world.Replace(world.Revision,world.Terrain,world.Spatial.Capture(),world.Voxels,world.Slopes,city:new(profile,state with {HeatCallback=timer}));
        return before.Successor(before.Revision,new(candidate,before.Construction.Accounts),before.ConstructionMode,
            clock:clock with {NextOrder=checked(clock.NextOrder+1),Pending=clock.Pending.Add(timer).OrderBy(a=>a.DueMinute).ThenBy(a=>a.Order).ToImmutableArray()});
    }
    private static MixedWorld Replace(MixedWorld world, CityState state) => world.Replace(world.Revision,world.Terrain,
        world.Spatial.Capture(),world.Voxels,world.Slopes,city:new(world.City!.Profile,state));

    public static float Conductivity(MixedWorld world, Location location)
    {
        RoadComponent? road=null;var mountain=false;
        for(var z=0;z<world.Terrain.Space.Levels;z++)
        {
            var p=location with {Z=z};
            // CITY-D003: the highest Traffic voxel wins, even with a null road.
            // An elevated rail-only Traffic therefore masks a road below it.
            // Source intent is undocumented; preserve the overwrite rather than
            // silently changing the rule to "highest non-null road".
            if(world.At(p)?.Traffic is {} traffic) road=traffic.Road;
            if(world.Terrain.OccupantAt(p)?.Kind==TerrainOccupantKind.Mountain) mountain=true;
        }
        var bare=world.City!.State.Settings.BareConductivity;
        if(road!=null)
        {
            var d=world.City.Profile.Road(road.Definition) ?? throw new ArgumentException("Missing road conductivity binding.");
            return d.Style is "street" or "highway" or "railballast" ? d.Sidewalk=="pavement" ? RoadConductivity : (RoadConductivity+bare)/2 : bare;
        }
        var hv=world.Terrain.Space.ToStorage(location);
        return world.Terrain.Cell(hv.H,hv.V).Ground<world.Terrain.WaterLevel ? 0.03f : mountain ? 0.4f : bare;
    }
    // CITY-D003 preserves source notification gaps as well as scan precedence:
    // voxel assignment/removal and Road construction notify, but Traffic setters,
    // road detach while Traffic remains, outlook/tree/corner changes and water
    // alone do not. Stale rho can therefore persist; do not add a rescan as a
    // cleanup without explicitly revising the compatibility decision.
    internal static CityGraph NotifyChanges(MixedWorld before, MixedWorld after)
    {
        var graph=after.City!;var locations=new HashSet<Location>();
        var old=before.Spatial.Entities.SelectMany(e=>e.Footprint.At(e.Anchor).Select(c=>(c.Position,e.Id))).ToDictionary(c=>c.Position,c=>c.Id);
        var current=after.Spatial.Entities.SelectMany(e=>e.Footprint.At(e.Anchor).Select(c=>(c.Position,e.Id))).ToDictionary(c=>c.Position,c=>c.Id);
        foreach(var p in old.Keys.Concat(current.Keys))
            if(old.GetValueOrDefault(p)!=current.GetValueOrDefault(p)) locations.Add(p with {Z=0});
        foreach(var voxel in after.Voxels)
            if(voxel.Traffic?.Road is {} road && before.At(voxel.Position)?.Traffic?.Road?.Definition!=road.Definition)
                locations.Add(voxel.Position with {Z=0});
        if(locations.Count==0) return graph;
        var rho=graph.State.Heat.Rho.ToBuilder();
        foreach(var p in locations)
        {
            var hv=after.Terrain.Space.ToStorage(p);
            var value=Conductivity(after,p);
            if(!float.IsFinite(value)||value<0) throw new ArgumentException("Invalid heat conductivity.");
            // CITY-D001: keep conductivity aligned with deposits at [h,v],
            // despite Value's [h+1,v+1] read. See Value before changing either.
            rho[graph.State.Heat.Index(hv.H,hv.V)]=value;
        }
        return new(graph.Profile,graph.State with {Heat=graph.State.Heat with {Rho=rho.ToImmutable()}});
    }
}
