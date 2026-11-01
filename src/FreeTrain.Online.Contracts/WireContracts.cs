using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FreeTrain.Online.Contracts;

public static class ProtocolIdentity
{
    public const int Version = 6;
    public const int MaximumStateBytes = 256 * 1024 * 1024;
    public const int StateChunkBytes = 48 * 1024;
    public const string MixedRuleset = "mixed-railway-1";
    public const string MixedContent = "mixed-railway-content-1";
    public const int MaximumRailwayVoxels = 8192;
    public const int MaximumAccountTransactions = 8192;
    public const string ReferenceRuleset = "reference-spatial-4";
    public const string ReferenceContent = "reference-spatial-content-2";
    public static RequiredPackRead[] ReferencePacks => [new("reference-spatial", "2", "7dd845abcb42aa1797e5d15185881300fa98ffafcefc3e0bbbd5259559c87d82")];
    public const string PrototypeRuleset = "prototype-reference-1";
    public const string PrototypeContent = "prototype-content-1";
    // SHA-256 of the reviewed prototype sprites.json catalog (not a canonical content pack).
    public static RequiredPackRead[] PrototypePacks => [new("prototype-reference", "1", "0607821d22faaae836a3f04246a9a79f6c7779aa19081390244fb227a461af2c")];
}
public sealed record RequiredPackRead(string Id, string Version, string CatalogHash);

public static class WireJson
{
    public static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            AllowOutOfOrderMetadataProperties = true,
            RespectRequiredConstructorParameters = true,
            RespectNullableAnnotations = true
        };
        options.Converters.Add(new DecimalInt64Converter());
        return options;
    }
}

// JSON long values are canonical decimal strings, never JS floating-point numbers.
public sealed class DecimalInt64Converter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String) throw new JsonException("Expected decimal string.");
        var text = reader.GetString();
        if (!long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
            || text != value.ToString(CultureInfo.InvariantCulture))
            throw new JsonException("Expected canonical Int64 decimal string.");
        return value;
    }
    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(CommandMessage), "command")]
[JsonDerivedType(typeof(SnapshotMessage), "snapshot")]
[JsonDerivedType(typeof(DeltaMessage), "delta")]
[JsonDerivedType(typeof(AcceptedMessage), "accepted")]
[JsonDerivedType(typeof(RejectedMessage), "rejected")]
[JsonDerivedType(typeof(JoinMessage), "join")]
[JsonDerivedType(typeof(ResyncMessage), "resync")]
[JsonDerivedType(typeof(HeartbeatMessage), "heartbeat")]
[JsonDerivedType(typeof(InputResetMessage), "inputReset")]
[JsonDerivedType(typeof(SoundEventMessage), "soundEvent")]
[JsonDerivedType(typeof(StateChunkMessage), "stateChunk")]
[JsonDerivedType(typeof(SessionOfferMessage), "sessionOffer")]
public abstract record WireMessage;

public sealed record StateChunkMessage(string TransferId, long WorldEpoch, long Revision, int Index, int Count,
    int TotalBytes, string Sha256, byte[] Data) : WireMessage;
public sealed record SessionOfferMessage(int ProtocolVersion, string ContentVersion, string ServerIncarnation,
    string SessionId, string ParticipantId, string Role, long WorldEpoch, string Ruleset, RequiredPackRead[] RequiredPacks) : WireMessage;
public sealed record StateTransferPayload(WireMessage Message, PackedTerrainRead? PackedTerrain);

public sealed record SoundEventMessage(string EventId, long WorldEpoch, string Kind) : WireMessage;
public sealed record ResyncMessage : WireMessage;
public sealed record HeartbeatMessage : WireMessage;
public sealed record InputResetMessage : WireMessage;

public sealed record JoinMessage(int ProtocolVersion, string ContentVersion, string ServerIncarnation,
    string SessionId, string ParticipantId, string Role, long WorldEpoch, [property: JsonConverter(typeof(NestedSnapshotConverter))] SnapshotMessage Snapshot,
    string Ruleset, RequiredPackRead[] RequiredPacks) : WireMessage;
public sealed record CommandMessage(string RequestId, long ExpectedWorldEpoch, CommandPayload Payload) : WireMessage;
public sealed record AcceptedMessage(string RequestId, long WorldEpoch, long Sequence, long Tick, long Revision) : WireMessage;
public sealed record RejectedMessage(string RequestId, string Code, Dictionary<string, string> Arguments) : WireMessage;
public sealed record SnapshotMessage(long WorldEpoch, long Revision, long Tick, bool Paused, long Cash,
    MapRead Map, TrainRead[] Trains, StationRead[] Stations, BuildingRead[] Buildings, TrackRead[] Tracks,
    string Ruleset, string ContentVersion, RequiredPackRead[] RequiredPacks,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SpatialEntityRead[]? Entities = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TerrainRead? Terrain = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] MixedRead? Mixed = null) : WireMessage;
public sealed record DeltaMessage(long WorldEpoch, long BaseRevision, long Revision, long Tick,
    bool Paused, long Cash, TrainRead[] Trains, StationRead[] Stations, BuildingRead[] Buildings,
    TrackRead[] Tracks, RemovedRead Removed, string Ruleset, string ContentVersion, RequiredPackRead[] RequiredPacks,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SpatialEntityRead[]? Entities = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TerrainDeltaRead? Terrain = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] MixedRead? Mixed = null) : WireMessage;
public sealed record RemovedRead(string[] TrainIds, string[] StationIds, string[] BuildingIds, PositionRead[] TrackPositions,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long[]? EntityIds = null);
// Width/Height are H/V storage dimensions for hv; X/Y bounds for obsolete prototypeXY.
public sealed record MapRead(int Width, int Height, int Levels, string CoordinateSystem);
public sealed record PositionRead(int X, int Y, int Z);
public sealed record TrackRead(PositionRead Position, string Shape, long Revision,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? EntityId = null);
public sealed record DefinitionRead(string PackId, string ContributionId, string RecipeId);
public sealed record VoxelMemberRead(PositionRead Offset, bool Transparent);
public sealed record SpatialEntityRead(long Id, long Revision, DefinitionRead Definition, PositionRead Anchor,
    VoxelMemberRead[] Footprint, bool Owned, bool SilentlyReclaimable, long Value);
public sealed record TrainRead(string Id, long Revision, string Name, PositionRead Position, int Cars, string Controller,
    int Heading, PositionRead[] CarPositions, int[] CarHeadings, string State, ControlLeaseRead? ControlLease);
public sealed record ControlLeaseRead(string ParticipantId, string LeaseId, long ExpiresAt);
public sealed record StationRead(string Id, long Revision, string Name, PositionRead Position);
public sealed record BuildingRead(string Id, string DefinitionId, PositionRead Position, int Height);

// Read-only geometry, never a placement permission or cost quote.
public sealed record SpatialQuery(long ExpectedWorldEpoch, PositionRead Position, PositionRead? Anchor, int Snap);
public sealed record ProjectedPointRead(int A, int B);
public sealed record StoragePositionRead(int H, int V, int Z);
public sealed record SpatialPreviewRead(long WorldEpoch, long Revision, string Ruleset, string ContentVersion,
    RequiredPackRead[] RequiredPacks, PositionRead Position, bool Inside, ProjectedPointRead Projected,
    StoragePositionRead? Storage, int? Heading, int? Distance);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(AcquireControlCommand), "acquireControl")]
[JsonDerivedType(typeof(ReleaseControlCommand), "releaseControl")]
[JsonDerivedType(typeof(RunCommand), "run")]
[JsonDerivedType(typeof(PauseCommand), "pause")]
[JsonDerivedType(typeof(StepCommand), "step")]
[JsonDerivedType(typeof(RenameStationCommand), "renameStation")]
[JsonDerivedType(typeof(BuildTrackCommand), "buildTrack")]
[JsonDerivedType(typeof(RemoveTrackCommand), "removeTrack")]
[JsonDerivedType(typeof(SetControllerCommand), "setController")]
[JsonDerivedType(typeof(ControllerInputCommand), "controllerInput")]
[JsonDerivedType(typeof(EditCornerCommand), "editCorner")]
[JsonDerivedType(typeof(SetAuthoringModeCommand), "setAuthoringMode")]
[JsonDerivedType(typeof(EditRailwayCommand), "editRailway")]
[JsonDerivedType(typeof(EditSlopeCommand), "editSlope")]
[JsonDerivedType(typeof(RemoveObjectCommand), "removeObject")]
public abstract record CommandPayload;
public sealed record AcquireControlCommand(string TrainId) : CommandPayload;
public sealed record ReleaseControlCommand(string TrainId, string LeaseId) : CommandPayload;
public sealed record RunCommand : CommandPayload;
public sealed record PauseCommand : CommandPayload;
public sealed record StepCommand : CommandPayload;
public sealed record RenameStationCommand(string StationId, long ExpectedRevision, string Name) : CommandPayload;
public sealed record BuildTrackCommand(PositionRead Position, string Shape) : CommandPayload;
public sealed record RemoveTrackCommand(PositionRead Position, long ExpectedRevision) : CommandPayload;
public sealed record RemovalQuery(long ExpectedWorldEpoch, long ExpectedGeometryRevision, string ExpectedContentVersion,
    PositionRead Position, long ExpectedEntityId, long ExpectedEntityRevision,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PositionRead? End = null);
public sealed record RemoveObjectCommand(RemovalQuery Query) : CommandPayload;
public sealed record RemovalSkipRead(PositionRead Position, string Reason);
public sealed record RemovalPreviewRead(RemovalQuery Query, bool Legal, string? Reason, string ObjectKind,
    PositionRead[] Positions, PositionRead[] Path, RemovalSkipRead[] Skipped);
public sealed record SetControllerCommand(string TrainId, long ExpectedRevision, string Controller, string LeaseId) : CommandPayload;
public sealed record ControllerInputCommand(string TrainId, long InputSequence, bool Pass, bool Depart,
    bool Reverse, bool Left, bool Right, bool Reset, string LeaseId) : CommandPayload;

// Nested snapshots retain the same discriminator as standalone snapshots.
public sealed class NestedSnapshotConverter : JsonConverter<SnapshotMessage>
{
    public override SnapshotMessage Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        => JsonSerializer.Deserialize<WireMessage>(ref reader, options) as SnapshotMessage
            ?? throw new JsonException("Expected a snapshot message.");
    public override void Write(Utf8JsonWriter writer, SnapshotMessage value, JsonSerializerOptions options)
        => JsonSerializer.Serialize<WireMessage>(writer, value, options);
}

// H/V row-major cells. Domain enums have explicit portable string identities.
public sealed record TerrainCellRead(int Ground, string Surface);
public sealed record TerrainTreeRead(int X, int Y, int Sprite);
public sealed record ForestRead(DefinitionRead Definition, TerrainTreeRead[] Trees);
public sealed record TerrainOccupantRead(long EntityId, string Kind, int NE, int SE, int SW, int NW,
    ForestRead? Forest, int CreekConnections);
public sealed record TerrainProvenanceRead(string Profile, string Source, string RequestHash, string RequestJson);
public sealed record TerrainRead(string Name, int WaterLevel, bool ConstructionMode, long GeometryRevision,
    long NextEntityId, TerrainProvenanceRead Provenance, TerrainCellRead[] Cells, TerrainOccupantRead[] Occupants);
public sealed record TerrainCellChangeRead(int Index, int Ground, string Surface);
public sealed record TerrainDeltaRead(int WaterLevel, bool ConstructionMode, long GeometryRevision, long NextEntityId,
    TerrainCellChangeRead[] Cells, TerrainOccupantRead[] Occupants);

// Packed actual terrain; byte arrays use canonical base64 in JSON.
public sealed record PackedTerrainRead(string Encoding, string Name, int WaterLevel, bool ConstructionMode,
    long GeometryRevision, long NextEntityId, TerrainProvenanceRead Provenance,
    DefinitionRead[] Definitions, byte[] Cells, int OccupantCount, byte[] Occupants);

// Additive terrain tool contract; durable state uses Save8 / protocol6.
public sealed record CornerTerrainQuery(long ExpectedWorldEpoch, long ExpectedGeometryRevision, PositionRead Position,
    string Action, int? OffsetX = null, int? OffsetY = null, CornerTerrainSite[]? Stroke = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RectangleTerrainSelection? Rectangle = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DefinitionRead? Forest = null);
public sealed record RectangleTerrainSelection(PositionRead End, bool Soft = false);
public sealed record CornerTerrainSite(PositionRead Position, string Action, int? OffsetX = null, int? OffsetY = null);
public sealed record EditCornerCommand(CornerTerrainQuery Query) : CommandPayload;
public sealed record SetAuthoringModeCommand(long ExpectedGeometryRevision, bool Enabled) : CommandPayload;
public sealed record CornerTerrainChangeRead(PositionRead Position, int BeforeGround, int AfterGround,
    int BeforeNE, int BeforeSE, int BeforeSW, int BeforeNW, int AfterNE, int AfterSE, int AfterSW, int AfterNW,
    bool Remove, bool LosesForest, bool LosesOwnership);
public sealed record CornerTerrainPreviewRead(long WorldEpoch, long GeometryRevision, PositionRead Position,
    string Action, bool Legal, string? Reason, long Cost, CornerTerrainChangeRead[] Changes, CornerTerrainSkipRead[] Skipped,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AuthoringDecorationRead[]? Decorations = null);
public sealed record AuthoringDecorationRead(PositionRead Position, ForestRead? Forest, int CreekConnections);
public sealed record CornerTerrainSkipRead(PositionRead Position, string Reason);

// Additive normal-track task; prices and candidate graphs are never command inputs.
public sealed record RailwayQuery(long ExpectedWorldEpoch, long ExpectedGeometryRevision, string ExpectedContentVersion,
    PositionRead From, PositionRead To, string Action, string Routing, string Infrastructure = "normal", string TunnelRouting = "exact", string SignalType = "{5349DE2A-DF3E-49B4-87A0-973105B7E6A4}", int SignalDirection = 0, string PoleType = "{DDF7F835-8100-45fd-8AE7-EC498796D387}", string StationaryType = "");
public sealed record EditRailwayCommand(RailwayQuery Query) : CommandPayload;
public sealed record RailwayRouteCellRead(PositionRead Position, int? BeforePattern, int? AfterPattern, bool ProtectedByCar);
public sealed record RailwayLossRead(string Kind, PositionRead Position, long OwnerId, long? SupportedOwnerId);
public sealed record RailwayPreviewRead(RailwayQuery Query, bool Legal, string? Reason, long Cost, bool ReverseBend,
    RailwayRouteCellRead[] Cells, PositionRead[] AddedPiers, RailwayLossRead[] Losses);

public sealed record SlopeQuery(long ExpectedWorldEpoch, long ExpectedGeometryRevision, string ExpectedContentVersion,
    PositionRead Position, int Climb, string Action);
public sealed record EditSlopeCommand(SlopeQuery Query) : CommandPayload;
public sealed record SlopeMemberRead(PositionRead Position, string Kind, int? Pattern, bool DrawGroundBelow, bool DrawGroundAbove);
public sealed record SlopeRailPositionRead(int Level, PositionRead Position, int Pattern, int ZDiff, int ZAngle);
public sealed record SlopePreviewRead(SlopeQuery Query, bool Legal, string? Reason, long Cost, PositionRead Base, int Climb,
    SlopeMemberRead[] Members, SlopeRailPositionRead[] Rails, PositionRead[] AddedPiers, RailwayLossRead[] Losses);

// Complete bounded railway/account publication, replaced atomically on every mixed delta.
// Terrain geometry and railway geometry have separate revisions and allocator high-water marks.
public sealed record CornersRead(int NE, int SE, int SW, int NW);
public sealed record RailComponentRead(DefinitionRead Definition, int Pattern, int Variant, CornersRead? TerrainBackup,
    int Direction, long? ControllerId);
public sealed record RoadComponentRead(DefinitionRead Definition, int Edges);
public sealed record AccessoryComponentRead(DefinitionRead Definition, int Direction);
public sealed record CarComponentRead(DefinitionRead Definition, long TrainId, long CarId);
public sealed record TrafficRead(RailComponentRead? Rail, RoadComponentRead? Road, AccessoryComponentRead? Accessory,
    CarComponentRead? Car, DefinitionRead? Platform);
public sealed record ConstructionCellRead(int State, int Ground, bool NeedsDoor);
public sealed record RailwayVoxelRead(PositionRead Position, long OwnerId, string Kind, TrafficRead? Traffic,
    DefinitionRead? Definition, int Sprite, bool DrawGroundBelow, bool DrawGroundAbove, long? SupportedOwnerId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ConstructionCellRead? Construction = null);
public sealed record SlopeOwnerRead(long EntityId, int Climb);
public sealed record AccountTransactionRead(long Sequence, long WorldRevision, long AbsoluteMinute, string Operation,
    string Genre, long Expenditure, long TopUps, long CashAfter);
public sealed record ConstructionAccountsRead(long InitialCash, long BankruptcyIncrement, AccountTransactionRead[] Transactions);
public sealed record MixedRead(long GeometryRevision, long NextEntityId, RailwayVoxelRead[] Voxels, SlopeOwnerRead[] Slopes,
    ConstructionAccountsRead Accounts);
