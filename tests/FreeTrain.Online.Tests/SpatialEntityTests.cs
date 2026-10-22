using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Server;
using FreeTrain.Online.Simulation;
using Xunit;

public class SpatialEntityTests
{
    private static readonly DefinitionReference Definition = new("test-pack", "日本語.Case", "recipe");
    private static readonly Footprint One = new([new(new(0,0,0), true)]);
    private static readonly Location Anchor = new(3,1,0); // H=0,V=1 in H4/V8.
    private static SpatialIndex Empty() => new(new(4,8,4),1,[]);

    [Fact]
    public void Source_cube_endpoints_reverse_exclusion_empty_extent_and_order_are_explicit()
    {
        var first = new Location(3,5,2); var second = new Location(1,2,0);
        var exclusive = Cube.Exclusive(first,second);
        Assert.Equal(new Location(2,3,1),exclusive.Corner);
        Assert.Equal(new Location(4,6,3),exclusive.End); Assert.Equal(12,exclusive.Volume);
        Assert.True(exclusive.Contains(first)); Assert.False(exclusive.Contains(second));
        var inclusive = Cube.Inclusive(first,second);
        Assert.Equal(new Location(1,2,0),inclusive.Corner); Assert.Equal(36,inclusive.Volume);
        Assert.True(inclusive.Contains(first));Assert.True(inclusive.Contains(second));
        Assert.Equal(new[]{new Location(0,0,0),new(0,0,1),new(0,1,0),new(0,1,1),new(1,0,0),new(1,0,1),new(1,1,0),new(1,1,1)},new Cube(new(0,0,0),2,2,2).Locations());
        Assert.Empty(Cube.Exclusive(first,first).Locations());
        Assert.Throws<OverflowException>(()=>new Cube(new(int.MaxValue,0,0),1,1,1));
        Assert.Throws<OverflowException>(()=>Cube.Inclusive(new(int.MinValue,0,0),new(int.MaxValue,0,0)));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new Cube(Anchor,-1,1,1));
    }

    [Fact]
    public void Shared_owner_invisible_cells_holes_and_outside_sentinel_remain_distinct()
    {
        var footprint = new Footprint([new(new(0,0,0),false),new(new(0,0,1),true),new(new(1,1,0),true)]);
        var graph = Empty().Add(Definition,Anchor,footprint,true,false,4321);
        var entity = Assert.Single(graph.Entities);
        Assert.Same(entity,graph.At(Anchor).Entity); Assert.Same(entity,graph.At(new(3,1,1)).Entity);
        Assert.True(graph.At(new(3,1,1)).Transparent); Assert.False(graph.At(new(3,1,1)).Reusable);
        Assert.Equal(VoxelStatus.Vacant,graph.At(new(3,2,0)).Status);Assert.True(graph.At(new(3,2,0)).Reusable);
        Assert.Equal(VoxelStatus.Outside,graph.At(new(3,1,4)).Status);Assert.False(graph.At(new(3,1,4)).Reusable);
        Assert.False(graph.At(new(-10,1,0)).Reusable);
        Assert.Equal(entity,Assert.Single(graph.Intersecting(new(Anchor,2,2,2))));
        Assert.Equal(4321,entity.Value);Assert.True(entity.Owned);Assert.False(entity.SilentlyReclaimable);
        Assert.Equal(3,entity.Footprint.Members.Length);
    }

    [Theory]
    [InlineData(false,false,false)][InlineData(false,true,true)][InlineData(true,false,false)][InlineData(true,true,true)]
    public void Reuse_depends_on_silent_reclamation_independently_of_ownership(bool owned,bool reclaimable,bool reusable)
        => Assert.Equal(reusable,Empty().Add(Definition,Anchor,One,owned,reclaimable,0).At(Anchor).Reusable);

    [Fact]
    public void Failed_transactions_preserve_graph_and_allocator_and_removed_ids_are_never_reused()
    {
        var empty = Empty(); var first = empty.Add(Definition,Anchor,One,false,false,0);
        Assert.Equal(1,Assert.Single(first.Entities).Id);Assert.Equal(2,first.NextEntityId);Assert.Empty(empty.Entities);
        Assert.Throws<ArgumentException>(()=>first.Add(Definition,Anchor,One,false,true,0));
        Assert.Throws<ArgumentException>(()=>first.Add(Definition,new(3,1,4),One,false,true,0));
        Assert.Throws<InvalidOperationException>(()=>first.Remove(1,1));
        var moved = first.Replace(first.Entities[0] with { Anchor = new(4,2,0), Revision = 1 },0);
        Assert.Equal(1,moved.At(new(4,2,0)).Entity!.Id);Assert.Equal(VoxelStatus.Vacant,moved.At(Anchor).Status);
        Assert.Equal(VoxelStatus.Occupied,first.At(Anchor).Status);
        Assert.Throws<InvalidOperationException>(()=>moved.Replace(moved.Entities[0] with {Revision=2},0));
        var removed = moved.Remove(1,1);
        var next = removed.Add(Definition,Anchor,One,false,true,0);
        Assert.Equal(2,Assert.Single(next.Entities).Id);Assert.Equal(3,next.NextEntityId);
        var restored = new SpatialIndex(next.Space,next.Capture().NextEntityId,next.Capture().Entities);
        Assert.Equal(3,restored.Add(Definition,new(4,2,0),One,false,false,0).Entities.Last().Id);
        Assert.Equal(2,first.NextEntityId);Assert.Single(first.Entities);
    }

    [Fact]
    public void Invalid_footprints_aliases_identity_and_allocator_fail_before_a_graph_is_published()
    {
        Assert.Throws<ArgumentException>(()=>new Footprint([]));
        Assert.Throws<ArgumentException>(()=>new Footprint([new(new(0,0,0),true),new(new(0,0,0),false)]));
        Assert.Throws<ArgumentException>(()=>Empty().Add(Definition,new(3,1,3),Footprint.Box(new(new(0,0,0),1,1,2)),false,false,0));
        Assert.Throws<ArgumentException>(()=>Empty().Add(Definition with {RecipeId=""},Anchor,One,false,false,0));
        var valid=Empty().Add(Definition,Anchor,One,false,false,0);
        Assert.Throws<ArgumentException>(()=>new SpatialIndex(valid.Space,1,valid.Entities));
        Assert.Throws<ArgumentException>(()=>new SpatialIndex(valid.Space,3,[valid.Entities[0],valid.Entities[0]]));
        Assert.Throws<OverflowException>(()=>new SpatialIndex(valid.Space,long.MaxValue,[]).Add(Definition,Anchor,One,false,false,0));
    }

    [Fact]
    public void Whole_profile_dispatch_is_exact_order_independent_and_preserves_pack_and_contribution_case()
    {
        var a = new RequiredPackRead("a","1",new('a',64)); var b = new RequiredPackRead("b","2",new('b',64));
        var profile = new WorldProfile("rules","content","hv",[a,b]);
        Assert.True(profile.Matches("rules","content",[b,a]));
        foreach(var packs in new[]{new[]{a},new[]{a,a},new[]{a,b,b},new[]{a,b with {Version="3"}},new[]{a,b with {CatalogHash=new('c',64)}}})
            Assert.False(profile.Matches("rules","content",packs));
        Assert.False(profile.Matches("Rules","content",[a,b]));Assert.False(profile.Matches("rules","Content",[a,b]));
        Assert.Null(WorldProfiles.Resolve("reference-spatial-3",ProtocolIdentity.ReferenceContent,ProtocolIdentity.ReferencePacks));
        Assert.True(WorldProfiles.Reference.Contains(new("reference-spatial","normal-rail","normal11")));
        Assert.False(WorldProfiles.Reference.Contains(new("other","normal-rail","normal11")));
        Assert.False(WorldProfiles.Reference.Contains(new("reference-spatial","Normal-rail","normal11")));
        Assert.False(WorldProfiles.Reference.Contains(new("reference-spatial","normal-rail","normal12")));
    }

    [Fact]
    public void Saved_ids_and_allocator_survive_reordering_large_integers_epoch_replacement_and_delta_generation()
    {
        var world = SpatialReferenceScenario.Create(); var document=JsonNode.Parse(SaveCodec.Write(world))!;
        // A stable ID is not a position/index and must remain exact beyond JS's safe integers.
        var old=document["spatial"]!["entities"]![0]!["id"]!.GetValue<string>();
        const string large="9007199254740993";
        document["spatial"]!["entities"]![0]!["id"]=large;
        foreach(var rail in document["rails"]!.AsArray())if(rail!["entityId"]!.GetValue<string>()==old)rail["entityId"]=large;
        document["spatial"]!["nextEntityId"]="9007199254741000";
        var restored=SaveCodec.Read(JsonSerializer.SerializeToUtf8Bytes(document));
        Assert.Equal(9007199254741000L,restored.Spatial.NextEntityId);
        Assert.Contains(restored.Rails,r=>r.EntityId==9007199254740993L);
        Assert.Equal(SaveCodec.Write(restored),SaveCodec.Write(SaveCodec.Read(SaveCodec.Write(restored))));
        var before=SessionComposition.CreateReferenceHost().Snapshot();
        var host=new FreeTrain.Online.Session.SessionHost(restored,"owner");var after=host.Snapshot() with {Revision=1};
        var delta=NetworkSession.Difference(before,after);
        Assert.Equal(long.Parse(old),Assert.Single(delta.Removed.EntityIds!));
        Assert.Equal(9007199254740993L,Assert.Single(delta.Entities!).Id);
        Assert.Contains(delta.Tracks,t=>t.EntityId==9007199254740993L);
        Assert.Equal("9007199254740993",JsonNode.Parse(JsonSerializer.Serialize<WireMessage>(delta,WireJson.CreateOptions()))!["entities"]![0]!["id"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("duplicateId")][InlineData("missingOwner")][InlineData("wrongOwner")][InlineData("occupiedTwice")]
    [InlineData("missingEntity")][InlineData("invisibleMissing")][InlineData("unknownDefinition")][InlineData("unknownPack")]
    [InlineData("wrongCase")][InlineData("allocator")][InlineData("numericId")][InlineData("extraPack")][InlineData("duplicatePack")]
    [InlineData("nullEntity")][InlineData("nullMember")][InlineData("nullOffset")][InlineData("ownership")]
    public async Task Invalid_spatial_graph_or_profile_preserves_current_world_and_epoch(string mutation)
    {
        var host=SessionComposition.CreateReferenceHost();var node=JsonNode.Parse(SaveCodec.Write(SpatialReferenceScenario.Create()))!;
        var before=host.Snapshot();var entities=node["spatial"]!["entities"]!.AsArray();var first=entities[0]!;
        switch(mutation)
        {
            case "duplicateId":entities[1]!["id"]=first["id"]!.DeepClone();break;
            case "missingOwner":node["rails"]![0]!["entityId"]="999";break;
            case "wrongOwner":node["rails"]![0]!["entityId"]=node["rails"]![1]!["entityId"]!.DeepClone();break;
            case "occupiedTwice":first["footprint"]!.AsArray().Add(first["footprint"]![0]!.DeepClone());break;
            case "missingEntity":entities.RemoveAt(0);break;
            case "invisibleMissing":first["footprint"]![0]!["transparent"]=false;break;
            case "unknownDefinition":first["definition"]!["recipeId"]="normal12";break;
            case "unknownPack":first["definition"]!["packId"]="optional";break;
            case "wrongCase":first["definition"]!["contributionId"]="Normal-rail";break;
            case "allocator":node["spatial"]!["nextEntityId"]="1";break;
            case "numericId":first["id"]=1;break;
            case "extraPack":node["requiredPacks"]!.AsArray().Add(new JsonObject{["id"]="unknown",["version"]="1",["catalogHash"]=new string('0',64)});break;
            case "duplicatePack":node["requiredPacks"]!.AsArray().Add(node["requiredPacks"]![0]!.DeepClone());break;
            case "nullEntity":entities[0]=null;break;
            case "nullMember":first["footprint"]![0]=null;break;
            case "nullOffset":first["footprint"]![0]!["offset"]=null;break;
            case "ownership":first["owned"]=false;break;
        }
        var load=host.LoadSave(JsonSerializer.SerializeToUtf8Bytes(node),1);host.Pump();
        await Assert.ThrowsAsync<InvalidDataException>(()=>load);
        Assert.Equal(JsonSerializer.Serialize(before),JsonSerializer.Serialize(host.Snapshot()));
    }
}
