using System.Collections.Immutable;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Persistence;

public static class CityWire
{
    public const int Format = 1;
    private static PositionRead P(Location p) => new(p.X,p.Y,p.Z);
    private static Location P(PositionRead p) => new(p.X,p.Y,p.Z);
    private static DefinitionRead D(DefinitionReference d) => new(d.PackId,d.ContributionId,d.RecipeId);
    private static DefinitionReference D(DefinitionRead d) => new(d.PackId,d.ContributionId,d.RecipeId);
    private static MixedActionRead? T(ScheduledAction? t) => t == null ? null : new(t.Registration,t.Order,t.DueMinute,t.HandlerId,t.Interval);
    private static ScheduledAction? T(MixedActionRead? t) => t == null ? null : new(t.Registration,t.Order,t.DueMinute,t.HandlerId,t.Interval);
    private static ConstructionCellRead C(ConstructionCell c) => new(c.State,c.Ground,c.NeedsDoor);
    private static ConstructionCell C(ConstructionCellRead c) => new(c.State,c.Ground,c.NeedsDoor);
    private static string Tag<T>(T value) where T : struct, Enum => value.ToString().ToLowerInvariant();
    private static T Value<T>(string value) where T : struct, Enum => Enum.GetValues<T>().Single(v=>Tag(v)==value);
    private static CityPropertyRead M(CityProperty v) => new(v.Id, v.Revision, P(v.Location));
    private static CityProperty M(CityPropertyRead v) => new(v.Id, v.Revision, P(v.Location));
    private static CityFenceHostRead M(CityFenceHost v) => new(v.Id, v.Revision, P(v.Location));
    private static CityFenceHost M(CityFenceHostRead v) => new(v.Id, v.Revision, P(v.Location));
    private static CityGroundRead M(CityGround v) => new(v.Id, v.Revision, D(v.Definition), P(v.Location), v.Owned, v.Variant);
    private static CityGround M(CityGroundRead v) => new(v.Id, v.Revision, D(v.Definition), P(v.Location), v.Owned, v.Variant);
    private static CityHalfRead M(CityHalf v) => new(D(v.Definition), Tag(v.Side), v.Direction, v.Color);
    private static CityHalf M(CityHalfRead v) => new(D(v.Definition), Value<HalfSide>(v.Side), v.Direction, v.Color);
    private static CitySubsidiaryRead M(CitySubsidiary v) => new(v.MarketPrice, v.TotalLandValue, T(v.Callback));
    private static CitySubsidiary M(CitySubsidiaryRead v) => new(v.MarketPrice, v.TotalLandValue, T(v.Callback));
    private static CityConstructionVoxelRead M(CityConstructionVoxel v) => new(P(v.Offset), C(v.Cell), T(v.Callback));
    private static CityConstructionVoxel M(CityConstructionVoxelRead v) => new(P(v.Offset), C(v.Cell), T(v.Callback));
    private static CityStructureRead M(CityStructure v) => new(v.Id, v.Revision, D(v.Definition), P(v.Location), v.Height, v.Owned, v.Subsidiary == null ? null : M(v.Subsidiary), v.Halves.Select(M).ToArray(), v.Construction.Select(M).ToArray(), Tag(v.Caller));
    private static CityStructure M(CityStructureRead v) => new(v.Id, v.Revision, D(v.Definition), P(v.Location), v.Height, v.Owned, v.Subsidiary == null ? null : M(v.Subsidiary), v.Halves.Select(M).ToImmutableArray(), v.Construction.Select(M).ToImmutableArray(), Value<CityConstructionCaller>(v.Caller));
    private static CityProducerRead M(CityProducer v) => new(v.OwnerId, D(v.InitialDefinition), v.InitialHeight);
    private static CityProducer M(CityProducerRead v) => new(v.OwnerId, D(v.InitialDefinition), v.InitialHeight);
    private static CityFenceRead M(CityFence v) => new(v.HostOwnerId, P(v.Location), v.Side, D(v.Definition));
    private static CityFence M(CityFenceRead v) => new(v.HostOwnerId, P(v.Location), v.Side, D(v.Definition));
    private static CitySelectedPlanRead M(CitySelectedPlan v) => new(D(v.Definition), P(v.Site), v.SizeX, v.SizeY, v.Height, v.Value, v.FinalPrice);
    private static CitySelectedPlan M(CitySelectedPlanRead v) => new(D(v.Definition), P(v.Site), v.SizeX, v.SizeY, v.Height, v.Value, v.FinalPrice);
    private static CitySearchPlanRead M(CitySearchPlan v) => new(v.StationId, Tag(v.Phase), v.Strength, v.Budget, v.Work.ToArray(), v.Scanning == null ? null : P(v.Scanning.Value), v.Selected == null ? null : M(v.Selected));
    private static CitySearchPlan M(CitySearchPlanRead v) => new(v.StationId, Value<CitySearchPhase>(v.Phase), v.Strength, v.Budget, v.Work.ToImmutableArray(), v.Scanning == null ? null : P(v.Scanning), v.Selected == null ? null : M(v.Selected));
    private static CityLandCacheRead M(CityLandCache v) => new(P(v.Corner), v.SizeX, v.SizeY, v.LandValue, v.EntityValue);
    private static CityLandCache M(CityLandCacheRead v) => new(P(v.Corner), v.SizeX, v.SizeY, v.LandValue, v.EntityValue);
    private static CityHeatRead M(CityHeatState v) => new(v.HorizontalSize, v.VerticalSize, v.Q.ToArray(), v.Back.ToArray(), v.Rho.ToArray());
    private static CityHeatState M(CityHeatRead v) => new(v.HorizontalSize, v.VerticalSize, v.Q.ToImmutableArray(), v.Back.ToImmutableArray(), v.Rho.ToImmutableArray());
    private static CitySettingsRead M(CityDevelopmentSettings v) => new(v.StrengthDiffusion, v.PopulationPower, v.PopulationBase, v.PopulationScale, v.LandPriceScale, v.ReplacementFactor, v.MaximumPricePower, v.Alpha, v.Diffusion, v.UpdateFrequencyHours, v.LandValuePower, v.BareConductivity);
    private static CityDevelopmentSettings M(CitySettingsRead v) => new(v.StrengthDiffusion, v.PopulationPower, v.PopulationBase, v.PopulationScale, v.LandPriceScale, v.ReplacementFactor, v.MaximumPricePower, v.Alpha, v.Diffusion, v.UpdateFrequencyHours, v.LandValuePower, v.BareConductivity);
    public static CityRead? Read(CityGraph? graph)
    {
        if (graph == null) return null;
        var s=graph.State;
        var data=new CityRead(Format,s.Properties.Select(M).ToArray(), s.Structures.Select(M).ToArray(), s.Ground.Select(M).ToArray(), s.Producers.Select(M).ToArray(), s.Fences.Select(M).ToArray(), M(s.Heat), M(s.Settings), s.Plans.Select(M).ToArray(), s.LandCache.Select(M).ToArray(), s.FenceHosts.Select(M).ToArray(), T(s.HeatCallback), T(s.DevelopmentCallback));
        ValidateLimits(data);
        return data;
    }
    public static CityGraph? Restore(CityRead? data, CityProfile? profile)
    {
        if(data == null) return null;
        if(profile == null) throw new InvalidDataException("incompatibleSave");
        ValidateLimits(data);
        return new(profile,new(data.Properties.Select(M).ToImmutableArray(), data.Structures.Select(M).ToImmutableArray(), data.Ground.Select(M).ToImmutableArray(), data.Producers.Select(M).ToImmutableArray(), data.Fences.Select(M).ToImmutableArray(), M(data.Heat), M(data.Settings), data.Plans.Select(M).ToImmutableArray(), data.LandCache.Select(M).ToImmutableArray(), data.FenceHosts.Select(M).ToImmutableArray(), T(data.HeatCallback), T(data.DevelopmentCallback)));
    }
    public static IEnumerable<MixedActionRead> Timers(CityRead? c) => c == null ? [] :
        c.Structures.SelectMany(s=>s.Construction.Select(v=>v.Callback).Append(s.Subsidiary?.Callback))
            .Concat(new[]{c.HeatCallback,c.DevelopmentCallback}).OfType<MixedActionRead>();
    public static void ValidateLimits(CityRead c)
    {
        const int max=ProtocolIdentity.MaximumRailwayVoxels;
        if(c.Format!=Format || c.Properties.Length+(long)c.Structures.Length+c.Ground.Length+c.FenceHosts.Length>max ||
            c.Producers.Length>max || c.Fences.Length>4L*max || c.Plans.Length>max || c.LandCache.Length>max ||
            c.Structures.Any(s=>s.Halves.Length>2 || s.Construction.Length>max) ||
            c.Structures.Sum(s=>(long)s.Construction.Length)>max || c.Plans.Any(p=>p.Work.Length!=4) ||
            c.Heat.HorizontalSize<1 || c.Heat.VerticalSize<1 || c.Heat.HorizontalSize>WorldCreationProfile.MaxSide || c.Heat.VerticalSize>WorldCreationProfile.MaxSide ||
            c.Heat.Q.Length!=(long)(c.Heat.HorizontalSize+2)*(c.Heat.VerticalSize+2) ||
            c.Heat.Back.Length!=c.Heat.Q.Length || c.Heat.Rho.Length!=c.Heat.Q.Length)
            throw new ArgumentException("City wire limit/format.");
    }
}
