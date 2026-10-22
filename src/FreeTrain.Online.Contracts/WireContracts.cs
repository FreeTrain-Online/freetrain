using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FreeTrain.Online.Contracts;

public static class ProtocolIdentity
{
    public const int Version = 3;
    public const string ReferenceRuleset = "reference-spatial-2";
    public const string ReferenceContent = "reference-spatial-content-1";
    public static RequiredPackRead[] ReferencePacks => [new("reference-spatial", "1", "b40daa7c21d5cac12800a13e04a947df4ff1ba52833d6bd471cefbfbae38ee0c")];
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
public abstract record WireMessage;

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
    string Ruleset, string ContentVersion, RequiredPackRead[] RequiredPacks) : WireMessage;
public sealed record DeltaMessage(long WorldEpoch, long BaseRevision, long Revision, long Tick,
    bool Paused, long Cash, TrainRead[] Trains, StationRead[] Stations, BuildingRead[] Buildings,
    TrackRead[] Tracks, RemovedRead Removed, string Ruleset, string ContentVersion, RequiredPackRead[] RequiredPacks) : WireMessage;
public sealed record RemovedRead(string[] TrainIds, string[] StationIds, string[] BuildingIds, PositionRead[] TrackPositions);
// Width/Height are H/V storage dimensions for hv; X/Y bounds for obsolete prototypeXY.
public sealed record MapRead(int Width, int Height, int Levels, string CoordinateSystem);
public sealed record PositionRead(int X, int Y, int Z);
public sealed record TrackRead(PositionRead Position, string Shape, long Revision);
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
public abstract record CommandPayload;
public sealed record AcquireControlCommand(string TrainId) : CommandPayload;
public sealed record ReleaseControlCommand(string TrainId, string LeaseId) : CommandPayload;
public sealed record RunCommand : CommandPayload;
public sealed record PauseCommand : CommandPayload;
public sealed record StepCommand : CommandPayload;
public sealed record RenameStationCommand(string StationId, long ExpectedRevision, string Name) : CommandPayload;
public sealed record BuildTrackCommand(PositionRead Position, string Shape) : CommandPayload;
public sealed record RemoveTrackCommand(PositionRead Position, long ExpectedRevision) : CommandPayload;
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
