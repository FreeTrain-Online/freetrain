namespace FreeTrain.Online.Contracts;

// P7 city continuation: data only; no delegates or derived indexes.
public sealed record CityPropertyRead(long Id, long Revision, PositionRead Location);
public sealed record CityFenceHostRead(long Id, long Revision, PositionRead Location);
public sealed record CityGroundRead(long Id, long Revision, DefinitionRead Definition, PositionRead Location, bool Owned, int Variant);
public sealed record CityHalfRead(DefinitionRead Definition, string Side, int Direction, int Color);
public sealed record CitySubsidiaryRead(long MarketPrice, long TotalLandValue, MixedActionRead? Callback);
public sealed record CityConstructionVoxelRead(PositionRead Offset, ConstructionCellRead Cell, MixedActionRead? Callback);
public sealed record CityStructureRead(long Id, long Revision, DefinitionRead Definition, PositionRead Location, int Height, bool Owned, CitySubsidiaryRead? Subsidiary, CityHalfRead[] Halves, CityConstructionVoxelRead[] Construction, string Caller);
public sealed record CityProducerRead(long OwnerId, DefinitionRead InitialDefinition, int InitialHeight);
public sealed record CityFenceRead(long HostOwnerId, PositionRead Location, int Side, DefinitionRead Definition);
public sealed record CitySelectedPlanRead(DefinitionRead Definition, PositionRead Site, int SizeX, int SizeY, int Height, long Value, long FinalPrice);
public sealed record CitySearchPlanRead(long StationId, string Phase, double Strength, int Budget, int[] Work, PositionRead? Scanning, CitySelectedPlanRead? Selected);
public sealed record CityLandCacheRead(PositionRead Corner, int SizeX, int SizeY, long LandValue, long EntityValue);
public sealed record CityHeatRead(int HorizontalSize, int VerticalSize, float[] Q, float[] Back, float[] Rho);
public sealed record CitySettingsRead(double StrengthDiffusion, double PopulationPower, int PopulationBase, double PopulationScale, double LandPriceScale, double ReplacementFactor, double MaximumPricePower, float Alpha, float Diffusion, int UpdateFrequencyHours, double LandValuePower, float BareConductivity);
public sealed record CityRead(int Format, CityPropertyRead[] Properties, CityStructureRead[] Structures, CityGroundRead[] Ground, CityProducerRead[] Producers, CityFenceRead[] Fences, CityHeatRead Heat, CitySettingsRead Settings, CitySearchPlanRead[] Plans, CityLandCacheRead[] LandCache, CityFenceHostRead[] FenceHosts, MixedActionRead? HeatCallback, MixedActionRead? DevelopmentCallback);

public sealed record CityQuery(long ExpectedWorldEpoch, long ExpectedRevision, long ExpectedMinute,
    string ExpectedContentVersion, long OwnerId, long ExpectedOwnerRevision);
public sealed record CityPreviewRead(CityQuery Query, bool CanCancel, string? Reason, long Cost, CityStructureRead Structure);
public sealed record CancelCityConstructionCommand(CityQuery Query) : CommandPayload;
