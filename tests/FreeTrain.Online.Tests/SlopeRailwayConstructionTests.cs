using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public class SlopeRailwayConstructionTests
{
    static RailwayProfile Profile => RailwayProfiles.Bundled;
    static Location P => new(8,8,2);
    static GameTime Time => new(GameTime.StartMinute);
    static DefinitionReference Ref(string id) => new(PreparedContentCatalog.BundledPackId,id,id);
    static JsonDocument Fixtures() => JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spatial/railway-fixtures.json")));
    static Location Point(JsonElement a) => new(a[0].GetInt32(),a[1].GetInt32(),a[2].GetInt32());
    static TerrainWorld Terrain(int ground=2,int water=0,int levels=8) => new(Profile.Terrain,"Slope",new(32,16,levels),water,
        Enumerable.Repeat(new TerrainCell(ground,TerrainSurface.Cut),512),new(1,[]),[]);
    static MixedWorld World(int ground=2,int water=0,int levels=8) => MixedWorld.FromTerrain(Profile,Terrain(ground,water,levels));
    static MixedConstructionState Apply(MixedWorld w,SlopeRailwayConstructionPlan p) => SlopeRailwayConstruction.Apply(new(w),p,Time);
    static MixedWorld ChangeTraffic(MixedWorld w,Location p,TrafficComponents t,RailwayProfile? profile=null)
    {
        var voxels=w.Voxels.Select(v=>v.Position==p?v with{Traffic=t}:v).ToArray();
        return new(profile??w.Profile,w.Terrain,w.Spatial.Capture(),voxels,w.Slopes,w.Revision);
    }
    static MixedWorld WithRail(MixedWorld w,Location p)
    {
        var id=w.Spatial.NextEntityId;
        var e=new SpatialEntity(id,0,w.Profile.TrafficOwner,p,new([new(new(0,0,0),true)]),true,false,0);
        return new(w.Profile,w.Terrain,new(id+1,w.Spatial.Entities.Add(e)),w.Voxels.Add(new(p,id,RailwayVoxelKind.Traffic,
            new(new(Ref("system:pattern/4"),4)))),w.Slopes,w.Revision);
    }
    static void Reject(SlopeRailwayConstructionPlan p,string reason)
    {
        Assert.False(p.Legal);Assert.Equal(reason,p.Reason);Assert.Empty(p.Booking.Charges);
        Assert.Empty(p.Members);Assert.Empty(p.RemovedPiers);Assert.Empty(p.ElectedPiers);
        Assert.Equal(reason,Assert.Throws<InvalidOperationException>(()=>Apply(p.Source,p)).Message);
    }

    [Fact]
    public void Every_literal_build_fixture_checks_actual_terrain_occupancy_art_flags_and_booking()
    {
        using var fixtures=Fixtures();var count=0;
        foreach(var row in fixtures.RootElement.GetProperty("groups").GetProperty("slopes").GetProperty("cases").EnumerateArray())
        {
            var input=row.GetProperty("input");var expected=row.GetProperty("expected");
            var b=Point(input.GetProperty("base"));var d=new Heading(input.GetProperty("direction").GetInt32());
            var terrain=Terrain(water:input.GetProperty("water").GetInt32());var cells=terrain.Cells.ToBuilder();
            var p=b;
            foreach(var g in input.GetProperty("ground").EnumerateArray())
            {
                if(terrain.Space.ContainsVoxel(p)) { var s=terrain.Space.ToStorage(p);cells[s.V*32+s.H]=new(g.GetInt32(),TerrainSurface.Cut); }
                p=p.Move(d);
            }
            terrain=new(terrain.Profile,terrain.Name,new(32,16,8),terrain.WaterLevel,cells,terrain.Spatial.Capture(),[]);
            var w=MixedWorld.FromTerrain(Profile,terrain);
            foreach(var occupied in input.GetProperty("occupied").EnumerateArray())
            {
                var n=occupied.GetInt32();p=b;
                for(var i=0;i<n/2;i++)p=p.Move(d);
                w=WithRail(w,p with{Z=b.Z+n%2});
            }
            var plan=SlopeRailwayConstruction.PreviewBuild(w,b,d);
            if(expected.GetProperty("outcome").GetString()=="reject")
            { Assert.False(plan.Legal,row.GetProperty("id").GetString());Assert.Empty(plan.Booking.Charges);count++;continue; }
            Assert.True(plan.Legal,plan.Reason);Assert.Equal(8,plan.Members.Length);
            Assert.Equal(expected.GetProperty("build").GetInt64(),plan.Booking.Total);
            var built=Apply(w,plan);Assert.Single(built.World.Slopes);Assert.Same(terrain,built.World.Terrain);
            Assert.Equal(w.Spatial.NextEntityId,plan.OwnerId);Assert.Empty(w.Voxels);
            var iRail=0;
            foreach(var r in expected.GetProperty("rails").EnumerateArray())
            {
                var actual=plan.Rails[iRail++];Assert.Equal(Point(r.GetProperty("location")),actual.Position);
                Assert.Equal(r.GetProperty("patternId").GetInt32(),actual.Pattern);
                Assert.Equal(r.GetProperty("zdiff").GetInt32(),actual.ZDiff);Assert.Equal(r.GetProperty("zangle").GetInt32(),actual.ZAngle.Index);
                Assert.Equal(actual.Pattern,built.World.At(actual.Position)!.Traffic!.Rail!.Pattern);
            }
            foreach(var pair in expected.GetProperty("paired").EnumerateArray())
            {
                var actual=built.World.At(Point(pair.GetProperty("location")))!;
                Assert.Equal(pair.GetProperty("kind").GetString() switch{"filler"=>RailwayVoxelKind.SlopeFiller,"wall"=>RailwayVoxelKind.SlopeWall,_=>RailwayVoxelKind.SlopeSupport},actual.Kind);
                Assert.Equal(pair.GetProperty("below").GetBoolean(),actual.DrawGroundBelow);Assert.Equal(pair.GetProperty("above").GetBoolean(),actual.DrawGroundAbove);
                if(actual.Kind!=RailwayVoxelKind.SlopeFiller)Assert.Equal(pair.GetProperty("sprite").GetInt32(),actual.Sprite);
            }
            if(input.TryGetProperty("groundAfter",out var groundAfter))
            {
                var changed=terrain.Cells.ToBuilder();p=b;
                foreach(var g in groundAfter.EnumerateArray())
                { var s=terrain.Space.ToStorage(p);changed[s.V*32+s.H]=new(g.GetInt32(),TerrainSurface.Cut);p=p.Move(d); }
                var edited=terrain.Replace(terrain.Revision,changed,terrain.WaterLevel,terrain.Spatial.Capture(),[]);
                var successor=built.World.Replace(built.World.Revision,edited,built.World.Spatial.Capture(),built.World.Voxels,built.World.Slopes);
                Assert.Equal(built.World.Voxels.ToArray(),successor.Voxels.ToArray());
            }
            var removal=SlopeRailwayConstruction.PreviewRemoval(built.World,b,d);
            Assert.Equal(expected.GetProperty("remove").GetInt64(),removal.Booking.Total);
            var removed=SlopeRailwayConstruction.Apply(built,removal,Time);Assert.Empty(removed.World.Voxels);Assert.Empty(removed.World.Slopes);
            Assert.Equal(2,removed.Accounts.Transactions.Length);
            count++;
        }
        Assert.Equal(26,count);
    }

    [Theory]
    [InlineData(0)][InlineData(2)][InlineData(4)][InlineData(6)]
    public void Every_stored_base_level_allows_all_eight_members_to_resolve_and_remove(int direction)
    {
        var d=new Heading(direction);
        for(var z=0;z<7;z++)
        {
            var w=World(ground:0);var b=P with{Z=z};var plan=SlopeRailwayConstruction.PreviewBuild(w,b,d);
            Assert.True(plan.Legal,plan.Reason);var built=Apply(w,plan);
            Assert.Equal(4*z,plan.ElectedPiers.Length);
            Assert.All(plan.ElectedPiers,p=>Assert.Equal(direction<4?"system:pier":"system:pier/slopeNE",p.Definition.RecipeId));
            foreach(var m in plan.Members)
            {
                var resolved=SlopeRailwayConstruction.Resolve(built.World,m.Position)!.Value;
                Assert.Equal(b,resolved.Base);Assert.Equal(d,resolved.Climb);Assert.Equal(plan.OwnerId,resolved.OwnerId);
                var remove=SlopeRailwayConstruction.PreviewRemoval(built.World,m.Position,d);
                Assert.True(remove.Legal);Assert.Equal(4*z,remove.RemovedPiers.Length);
                Assert.Empty(SlopeRailwayConstruction.Apply(built,remove,Time).World.Voxels);
            }
        }
    }

    [Fact]
    public void Every_literal_removal_resolves_members_protects_all_cars_and_rejects_partial_owners()
    {
        using var f=Fixtures();var count=0;var car=Ref("test:car");
        var profile=new RailwayProfile(Profile.Terrain,Profile.Bindings.Append(new(car,RailwayHandler.Car)));
        foreach(var row in f.RootElement.GetProperty("groups").GetProperty("slopeRemoval").GetProperty("cases").EnumerateArray())
        {
            var input=row.GetProperty("input");var e=row.GetProperty("expected");var d=new Heading(input.GetProperty("direction").GetInt32());
            var selected=Point(input.GetProperty("selected"));var b=Point(e.GetProperty("base"));
            var initial=MixedWorld.FromTerrain(profile,Terrain());var built=Apply(initial,SlopeRailwayConstruction.PreviewBuild(initial,b,d)).World;
            foreach(var n in input.GetProperty("cars").EnumerateArray())
            {
                var rail=built.Voxels.Where(v=>v.Traffic!=null).OrderBy(v=>SlopeRailwayConstruction.Pattern(v.Traffic!.Rail!.Pattern).Level).ElementAt(n.GetInt32());
                built=ChangeTraffic(built,rail.Position,rail.Traffic! with{Car=new(car,100,101)});
            }
            if(!input.GetProperty("completeOwner").GetBoolean())
                Assert.Throws<ArgumentException>(()=>new MixedWorld(profile,built.Terrain,built.Spatial.Capture(),built.Voxels.Skip(1),built.Slopes,built.Revision));
            else
            {
                var p=SlopeRailwayConstruction.PreviewRemoval(built,selected,new(input.GetProperty("requestedDirection").GetInt32()));
                if(e.GetProperty("outcome").GetString()=="reject")
                {
                    Assert.False(p.Legal);Assert.Empty(p.Booking.Charges);
                    if(input.GetProperty("cars").GetArrayLength()>0)Reject(SlopeRailwayConstruction.PreviewEntityRemoval(built,selected),"occupiedCar");
                }
                else
                {
                    Assert.True(p.Legal);Assert.Equal(b,p.Base);Assert.Equal(e.GetProperty("spend").GetInt64(),p.Booking.Total);
                    var after=Apply(built,p).World;
                    foreach(var cell in e.GetProperty("cleared").EnumerateArray())Assert.Null(after.At(Point(cell)));
                }
            }
            count++;
        }
        Assert.Equal(22,count);
    }

    [Fact]
    public void Source_transitions_match_every_literal_pattern_and_each_travel_heading()
    {
        using var f=Fixtures();
        foreach(var row in f.RootElement.GetProperty("patterns").EnumerateArray().Skip(28))
        {
            var id=row.GetProperty("id").GetInt32();var p=SlopeRailwayConstruction.Pattern(id);
            Assert.Equal(row.GetProperty("level").GetInt32(),p.Level);Assert.Equal(row.GetProperty("zdiff").GetInt32(),p.ZDiff);
            Assert.Equal(row.GetProperty("zangle").GetInt32(),p.ZAngle.Index);
            for(var d=0;d<8;d++)Assert.Equal(d==p.ZAngle.Index?p.ZDiff:0,SlopeRailwayConstruction.ZDiff(id,new(d)));
        }
        Assert.Throws<ArgumentOutOfRangeException>(()=>SlopeRailwayConstruction.Pattern(27));
        Assert.Throws<ArgumentOutOfRangeException>(()=>SlopeRailwayConstruction.Pattern(60));
    }

    [Fact]
    public void Normal_flat_teardown_never_removes_any_slope_member_but_retains_its_source_charge()
    {
        var w=World();var built=Apply(w,SlopeRailwayConstruction.PreviewBuild(w,P,Heading.East));
        var end=P.Move(Heading.East).Move(Heading.East).Move(Heading.East);
        var plan=NormalRailwayConstruction.PreviewRemoval(built.World,P,end);
        Assert.True(plan.Legal);Assert.Equal(12_000_000,plan.Booking.Total);
        var after=NormalRailwayConstruction.Apply(built,plan,Time).World;
        Assert.Equal(built.World.Voxels.ToArray(),after.Voxels.ToArray());Assert.Equal(built.World.Slopes.ToArray(),after.Slopes.ToArray());
    }

    [Fact]
    public void Entity_callbacks_and_slope_tool_teardown_remove_foreign_piers_without_removing_their_rails()
    {
        var w=World(ground:0);var built=Apply(w,SlopeRailwayConstruction.PreviewBuild(w,P,Heading.East));
        var owner=built.World.Slopes[0].EntityId;
        // A separate higher rail in the first ramp column owns the existing piers.
        var foreign=WithRail(built.World,P with{Z=5});var foreignId=foreign.At(P with{Z=5})!.OwnerId;
        foreign=new(foreign.Profile,foreign.Terrain,foreign.Spatial.Capture(),foreign.Voxels.Select(v=>v.Kind==RailwayVoxelKind.Pier&&v.Position.X==P.X?
            v with{SupportedOwnerId=foreignId}:v),foreign.Slopes,foreign.Revision);
        var entity=SlopeRailwayConstruction.PreviewEntityRemoval(foreign,P with{Z=3});
        Assert.True(entity.Legal);Assert.Empty(entity.Booking.Charges);Assert.Equal(8,entity.RemovedPiers.Length);
        var entityResult=Apply(foreign,entity);Assert.Single(entityResult.World.Voxels);
        Assert.Empty(entityResult.World.Slopes);Assert.Empty(entityResult.Accounts.Transactions);
        var tool=SlopeRailwayConstruction.PreviewRemoval(foreign,P with{Z=3},Heading.East);
        Assert.Equal(8,tool.RemovedPiers.Length);Assert.Contains(tool.RemovedPiers,p=>p.SupportedOwnerId==foreignId);
        Assert.Contains(tool.RemovedPiers,p=>p.SupportedOwnerId==owner);
        var after=Apply(foreign,tool).World;Assert.Single(after.Voxels);Assert.Equal(foreign.At(P with{Z=5}),after.Voxels[0]);
    }

    [Fact]
    public void Source_base_election_scans_below_ground_obstructions_without_rejecting_the_ramp()
    {
        var w=WithRail(World(ground:1),P with{Z=0});
        var p=SlopeRailwayConstruction.PreviewBuild(w,P,Heading.South);Assert.True(p.Legal);
        Assert.Equal(3,p.ElectedPiers.Length);Assert.DoesNotContain(p.ElectedPiers,v=>v.Position.X==P.X&&v.Position.Y==P.Y);
        Assert.All(p.ElectedPiers,v=>Assert.Equal(1,v.Position.Z));
        var built=Apply(w,p).World;Assert.Equal(w.At(P with{Z=0}),built.At(P with{Z=0}));
    }

    [Fact]
    public void Tool_preserves_road_and_pole_with_fresh_standalone_owners_and_protects_platforms()
    {
        var road=Ref("test:road");var platform=Ref("test:platform");
        var profile=new RailwayProfile(Profile.Terrain,Profile.Bindings.Concat(new[]{new RailwayBinding(road,RailwayHandler.Road),new(platform,RailwayHandler.Platform)}));
        var w=MixedWorld.FromTerrain(profile,Terrain());var built=Apply(w,SlopeRailwayConstruction.PreviewBuild(w,P,Heading.East)).World;
        var before=built.At(P)!.Traffic!;var pole=profile.Bindings.First(b=>b.Handler==RailwayHandler.Pole).Reference;
        var t=before with{Road=new(road,68),Accessory=new(pole,0)};
        var mixed=ChangeTraffic(built,P,t);var removed=Apply(mixed,SlopeRailwayConstruction.PreviewRemoval(mixed,P,Heading.East)).World;
        Assert.Single(removed.Voxels);Assert.Null(removed.At(P)!.Traffic!.Rail);
        Assert.Equal(t with{Rail=null},removed.At(P)!.Traffic);Assert.True(removed.At(P)!.OwnerId>=built.Spatial.NextEntityId);
        Assert.Empty(removed.Slopes);
        var entityRemoval=SlopeRailwayConstruction.PreviewEntityRemoval(mixed,P);
        Assert.Equal(t,entityRemoval.Members.Single(m=>m.Position==P).Traffic);
        Assert.Empty(Apply(mixed,entityRemoval).World.Voxels); // Whole Traffic deletion differs from slope-tool clearing.
        var protectedWorld=ChangeTraffic(built,P,before with{Platform=platform});
        Reject(SlopeRailwayConstruction.PreviewRemoval(protectedWorld,P,Heading.East),"platformDependency");
        Reject(SlopeRailwayConstruction.PreviewEntityRemoval(protectedWorld,P),"platformDependency");
    }

    [Fact]
    public void Bounds_missing_ambiguous_bindings_overflow_and_wrong_targets_reject_without_partial_losses()
    {
        var w=World();
        Reject(SlopeRailwayConstruction.PreviewBuild(w,P,Heading.NorthEast),"nonCardinalSlope");
        foreach(var p in new[]{P with{Z=-1},P with{Z=7},new Location(7,1,2)})
            Reject(SlopeRailwayConstruction.PreviewBuild(w,p,Heading.North),"outside");
        Reject(SlopeRailwayConstruction.PreviewBuild(w,new(int.MaxValue,8,2),Heading.East),"constructionOverflow");
        Reject(SlopeRailwayConstruction.PreviewRemoval(w,P,Heading.East),"notSlope");
        var missing=new RailwayProfile(Profile.Terrain,Profile.Bindings.Where(b=>b.Handler!=RailwayHandler.Pier));
        Reject(SlopeRailwayConstruction.PreviewBuild(MixedWorld.FromTerrain(missing,w.Terrain),P,Heading.East),"unavailablePierBinding");
        missing=new(Profile.Terrain,Profile.Bindings.Where(b=>b.Handler!=RailwayHandler.Slope));
        Reject(SlopeRailwayConstruction.PreviewBuild(MixedWorld.FromTerrain(missing,w.Terrain),P,Heading.East),"unavailableSlopeBinding");
        var duplicate=new RailwayProfile(Profile.Terrain,Profile.Bindings.Append(new(Ref("test:pier"),RailwayHandler.Pier,0)));
        Reject(SlopeRailwayConstruction.PreviewBuild(MixedWorld.FromTerrain(duplicate,w.Terrain),P,Heading.East),"unavailablePierBinding");
        var overflow=new MixedWorld(Profile,w.Terrain,new(long.MaxValue,[]),[],[]);
        Reject(SlopeRailwayConstruction.PreviewBuild(overflow,P,Heading.East),"constructionOverflow");
    }

    [Fact]
    public void Exact_source_replay_and_account_budgets_preserve_the_original()
    {
        var w=World();var p=SlopeRailwayConstruction.PreviewBuild(w,P,Heading.East);var built=Apply(w,p);
        Assert.Equal("staleRevision",Assert.Throws<InvalidOperationException>(()=>SlopeRailwayConstruction.Apply(built,p,Time)).Message);
        Assert.Throws<InvalidOperationException>(()=>Apply(World(),p));Assert.Empty(w.Voxels);Assert.Equal(1,w.Spatial.NextEntityId);
        var entries=Enumerable.Range(1,8192).Select(i=>new AccountTransaction(i,1,GameTime.StartMinute,"capacity",AccountGenre.RailService,0,0,ConstructionAccounts.DefaultInitialCash)).ToImmutableArray();
        var accounts=ConstructionAccounts.Restore(new(ConstructionAccounts.DefaultInitialCash,new(),entries));
        var full=new MixedWorld(Profile,w.Terrain,w.Spatial.Capture(),[],[],1);var state=new MixedConstructionState(full,accounts);
        Assert.Equal("accountCapacityLimit",Assert.Throws<InvalidOperationException>(()=>SlopeRailwayConstruction.Apply(state,SlopeRailwayConstruction.PreviewBuild(full,P,Heading.East),Time)).Message);
        Assert.Empty(full.Voxels);Assert.Equal(8192,state.Accounts.Transactions.Length);
    }

    [Fact]
    public void Voxel_budget_counts_the_entire_ramp_and_every_elected_pier()
    {
        var terrain=new TerrainWorld(Profile.Terrain,"Capacity",new(128,128,8),0,
            Enumerable.Repeat(new TerrainCell(2),128*128),new(1,[]),[]);
        var b=terrain.Space.ToLocation(new(50,100,2));
        MixedWorld Fill(int count)
        {
            var entities=new List<SpatialEntity>();var voxels=new List<RailwayVoxel>();
            for(var i=0;i<count;i++)
            {
                var p=terrain.Space.ToLocation(new(i%128,i/128,2));var id=i+1;
                entities.Add(new(id,0,Profile.TrafficOwner,p,new([new(new(0,0,0),true)]),true,false,0));
                voxels.Add(new(p,id,RailwayVoxelKind.Traffic,new(new(Ref("system:pattern/4"),4))));
            }
            return new(Profile,terrain,new(count+1,entities.ToImmutableArray()),voxels,[]);
        }
        var fits=Fill(8184);Assert.Equal(8192,Apply(fits,SlopeRailwayConstruction.PreviewBuild(fits,b,Heading.East)).World.Voxels.Length);
        var full=Fill(8185);Reject(SlopeRailwayConstruction.PreviewBuild(full,b,Heading.East),"railwayCapacityLimit");
        Assert.Equal(8185,full.Voxels.Length);
    }

    [Fact]
    public void Stored_mixed_art_flags_booking_and_continuation_round_trip_without_preview_mutation()
    {
        var t=Terrain(ground:0);var request=new EmptyWorldRequest(WorldCreationProfile.Id,t.Name,new(32,16,8),0);
        var state=new MixedSessionState(new(MixedWorld.FromTerrain(Profile,t)),TerrainProvenance.From(request),constructionMode:true);
        state.Random["forest"].Next();var before=SaveCodec.Write(state);
        var plan=SlopeRailwayConstruction.PreviewBuild(state.World,P,Heading.West);Assert.True(plan.Legal);
        Assert.Equal(before,SaveCodec.Write(state));
        var built=state.Successor(state.Revision,SlopeRailwayConstruction.Apply(state.Construction,plan,state.Clock.Time),true);
        var bytes=SaveCodec.Write(built);var loaded=SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
        Assert.Equal(bytes,SaveCodec.Write(loaded));Assert.True(loaded.ConstructionMode);Assert.Equal(120_000_000,loaded.Construction.Accounts.Transactions[0].Expenditure);
        foreach(var id in RandomStreams.Ids)Assert.Equal(built.Random[id].Next(),loaded.Random[id].Next());
        var selected=plan.Rails[3].Position;
        MixedSessionState Remove(MixedSessionState s) => s.Successor(s.Revision,SlopeRailwayConstruction.Apply(s.Construction,
            SlopeRailwayConstruction.PreviewRemoval(s.World,selected,Heading.West),s.Clock.Time),true);
        var a=Remove(built);var b=Remove(loaded);Assert.Equal(SaveCodec.Write(a),SaveCodec.Write(b));Assert.Empty(b.World.Voxels);
        Assert.Equal(152_000_000,b.Construction.Accounts.Transactions.Sum(t=>t.Expenditure));
    }
}
