using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Server;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using FreeTrain.Online.Plugins.ManualController;
using Xunit;
using static NetworkTests;

public class MultiplayerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    private static CommandMessage Command(string id, CommandPayload payload, long epoch = 1) => new(id, epoch, payload);
    private static async Task<WireMessage> Apply(NetworkSession session, NetworkSession.Peer peer, CommandMessage command, DateTimeOffset? now = null)
    {
        var task = session.Submit(peer, command, now ?? Now); session.Tick(now ?? Now); return await task;
    }
    private static void Drain(NetworkSession.Peer peer) { while (peer.Output.Reader.TryRead(out _)) { } }
    private static string Json(object value) => JsonSerializer.Serialize(value, NetworkSession.Json);

    [Fact]
    public void Revocable_credentials_bind_identity_role_and_server_incarnation()
    {
        var session = new NetworkSession(new());
        var participant = new ParticipantContext("viewer", ParticipantRole.Observer);
        var credential = Assert.IsType<string>(session.IssueAccess(participant, Now));
        Assert.Equal(participant, session.ResolveAccess(credential, Now));
        Assert.Null(new NetworkSession(new()).ResolveAccess(credential, Now));
        var changed = (credential[0] == 'A' ? "B" : "A") + credential[1..];
        Assert.Null(session.ResolveAccess(changed, Now));
        var otherLogin = Assert.IsType<string>(session.IssueAccess(participant, Now));
        Assert.NotEqual(credential, otherLogin);
        session.RevokeAccess(credential, Now);
        Assert.Null(session.ResolveAccess(credential, Now));
        Assert.Equal(participant, session.ResolveAccess(otherLogin, Now));
        Assert.Null(session.ResolveAccess(otherLogin, Now.AddHours(8)));
    }

    [Fact]
    public void Credential_registry_is_bounded_and_reuses_revoked_and_expired_capacity()
    {
        var session = new NetworkSession(new());
        var first = Assert.IsType<string>(session.IssueAccess(new("viewer", ParticipantRole.Observer), Now));
        for (var i = 1; i < NetworkSession.MaximumAccessCredentials; i++)
            Assert.NotNull(session.IssueAccess(new("viewer", ParticipantRole.Observer), Now));
        Assert.Null(session.IssueAccess(new("extra"), Now));
        session.RevokeAccess(first, Now);
        Assert.NotNull(session.IssueAccess(new("replacement"), Now));
        Assert.NotNull(session.IssueAccess(new("after-expiry"), Now.AddHours(8)));
    }

    [Fact]
    public async Task Revoking_a_login_disconnects_only_its_peer_and_fences_queued_commands()
    {
        var session = new NetworkSession(new());
        var firstCredential = Assert.IsType<string>(session.IssueAccess(new("a"), Now));
        var otherCredential = Assert.IsType<string>(session.IssueAccess(new("b"), Now));
        var a = session.Connect(Now, new("a"), firstCredential)!;
        var b = session.Connect(Now, new("b"), otherCredential)!;
        await Apply(session, a, Command("lease", new AcquireControlCommand("train-1")));
        var pending = session.Submit(a, Command("queued", new StepCommand()), Now);
        session.RevokeAccess(firstCredential, Now);
        session.Tick(Now);
        Assert.Equal("notAuthorized", Assert.IsType<RejectedMessage>(await pending).Code);
        Assert.Null(session.Bootstrap().Snapshot.Trains[0].ControlLease);
        Assert.True(a.Stop.IsCancellationRequested); Assert.False(b.Stop.IsCancellationRequested);
        Assert.Null(session.Connect(Now, new("a"), firstCredential));
        Assert.NotNull(session.ResolveAccess(otherCredential, Now));
    }

    [Fact]
    public void Concurrent_peer_limit_is_bounded_and_disconnected_capacity_can_be_reused()
    {
        var session = new NetworkSession(new() { MaximumPeers = 2 });
        var first = session.Connect(Now, new("first"))!;
        var second = session.Connect(Now, new("second"))!;
        Assert.Null(session.Connect(Now, new("third")));
        Assert.Null(session.Connect(Now, new("first")));
        session.Disconnect(first, Now);
        Assert.NotNull(session.Connect(Now, new("third")));
        Assert.False(second.Stop.IsCancellationRequested);
    }

    [Fact]
    public async Task Concurrent_edits_have_one_ordered_winner_and_receipts_are_scoped_to_participant()
    {
        var session = new NetworkSession(new());
        var alice = session.Connect(Now, new("alice", ParticipantRole.Owner))!;
        var bob = session.Connect(Now, new("bob", ParticipantRole.Operator))!;
        var observer = session.Connect(Now, new("viewer", ParticipantRole.Observer))!;
        var revision = session.Bootstrap().Snapshot.Stations[0].Revision;
        var a = Command("same-id", new RenameStationCommand("station-1", revision, "Alice"));
        var b = Command("same-id", new RenameStationCommand("station-1", revision, "Bob"));
        Task<WireMessage> first = null!, second = null!;
        await Task.WhenAll(Task.Run(() => { first = session.Submit(alice, a, Now); }), Task.Run(() => { second = session.Submit(bob, b, Now); }));
        session.Tick(Now);
        var results = await Task.WhenAll(first, second);
        Assert.Single(results.OfType<AcceptedMessage>());
        Assert.Equal("staleRevision", Assert.Single(results.OfType<RejectedMessage>()).Code);
        Assert.Equal(results[0], await session.Submit(alice, a, Now));
        Assert.Equal(results[1], await session.Submit(bob, b, Now));
        Assert.Equal("requestConflict", Assert.IsType<RejectedMessage>(await session.Submit(alice, a with { Payload = new StepCommand() }, Now)).Code);
        var before = Json(session.Bootstrap().Snapshot);
        foreach (var payload in new CommandPayload[] { new StepCommand(), new BuildTrackCommand(new(37, 16, 1), "buffer"), new AcquireControlCommand("train-1") })
            Assert.Equal("notAuthorized", Assert.IsType<RejectedMessage>(await session.Submit(observer, Command(Guid.NewGuid().ToString(), payload), Now)).Code);
        Assert.Equal(before, Json(session.Bootstrap().Snapshot));
    }

    [Fact]
    public async Task Competing_builds_charge_once_and_broadcast_identical_confirmed_state()
    {
        var session = new NetworkSession(new()); var a = session.Connect(Now, new("a"))!; var b = session.Connect(Now, new("b"))!;
        Drain(a); Drain(b);
        var build = Command("build", new BuildTrackCommand(new(37, 16, 1), "buffer"));
        var first = session.Submit(a, build, Now); var second = session.Submit(b, build, Now);
        session.Tick(Now);
        var accepted = Assert.IsType<AcceptedMessage>(await first);
        Assert.Equal("invalidPlacement", Assert.IsType<RejectedMessage>(await second).Code);
        session.Reply(a, accepted);
        Assert.Equal(999800, session.Bootstrap().Snapshot.Cash);
        Assert.Equal(Json(Assert.IsType<DeltaMessage>(await a.Output.Reader.ReadAsync())), Json(Assert.IsType<DeltaMessage>(await b.Output.Reader.ReadAsync())));
        Assert.Equal(accepted, await session.Submit(a, build, Now));
        Assert.Equal(999800, session.Bootstrap().Snapshot.Cash);
    }

    [Fact]
    public async Task Train_control_is_exclusive_fenced_renewable_and_expires_while_paused()
    {
        var session = new NetworkSession(new()); var a = session.Connect(Now, new("a"))!; var b = session.Connect(Now, new("b"))!;
        var takeA = session.Submit(a, Command("take", new AcquireControlCommand("train-1")), Now);
        var takeB = session.Submit(b, Command("take", new AcquireControlCommand("train-1")), Now);
        session.Tick(Now); Assert.IsType<AcceptedMessage>(await takeA);
        Assert.Equal("controlBusy", Assert.IsType<RejectedMessage>(await takeB).Code);
        var train = session.Bootstrap().Snapshot.Trains[0]; var lease = train.ControlLease!;
        Assert.Equal("a", lease.ParticipantId);
        Assert.IsType<AcceptedMessage>(await Apply(session, a, Command("manual", new SetControllerCommand(train.Id, train.Revision, "manual", lease.LeaseId))));
        var input = new ControllerInputCommand(train.Id, 1, false, true, false, false, false, false, lease.LeaseId);
        Assert.Equal("leaseRequired", Assert.IsType<RejectedMessage>(await Apply(session, b, Command("steal", input))).Code);
        Assert.IsType<AcceptedMessage>(await Apply(session, a, Command("input", input)));
        // Input expires sooner than the lease, without relying on simulation ticks.
        session.Tick(Now.AddMilliseconds(600));
        Assert.NotNull(session.Bootstrap().Snapshot.Trains[0].ControlLease);
        Assert.True(session.Bootstrap().Snapshot.Paused);
        Assert.IsType<AcceptedMessage>(await Apply(session, a, Command("renew", new AcquireControlCommand(train.Id)), Now.AddSeconds(4)));
        session.Tick(Now.AddSeconds(6)); Assert.NotNull(session.Bootstrap().Snapshot.Trains[0].ControlLease);
        session.Tick(Now.AddSeconds(10)); Assert.Null(session.Bootstrap().Snapshot.Trains[0].ControlLease);
        Assert.IsType<AcceptedMessage>(await Apply(session, b, Command("take-again", new AcquireControlCommand(train.Id)), Now.AddSeconds(10)));
        var next = session.Bootstrap().Snapshot.Trains[0].ControlLease!;
        Assert.NotEqual(lease.LeaseId, next.LeaseId);
        Assert.Equal("leaseRequired", Assert.IsType<RejectedMessage>(await Apply(session, a, Command("delayed", input), Now.AddSeconds(10))).Code);
        // The new lease starts input sequence order independently of the former holder.
        Assert.IsType<AcceptedMessage>(await Apply(session, b, Command("new-input", input with { InputSequence = 0, LeaseId = next.LeaseId }), Now.AddSeconds(10)));
        Assert.IsType<AcceptedMessage>(await Apply(session, b, Command("release", new ReleaseControlCommand(train.Id, next.LeaseId)), Now.AddSeconds(10)));
        Assert.Null(session.Bootstrap().Snapshot.Trains[0].ControlLease);
    }

    [Fact]
    public async Task Disconnect_fences_queued_commands_reconnects_identity_and_preserves_other_operators_time()
    {
        var session = new NetworkSession(new()); var a = session.Connect(Now, new("a"))!; var b = session.Connect(Now, new("b"))!;
        Assert.IsType<AcceptedMessage>(await Apply(session, a, Command("take", new AcquireControlCommand("train-1"))));
        var oldLease = session.Bootstrap().Snapshot.Trains[0].ControlLease!;
        await Apply(session, b, Command("run", new RunCommand()));
        var pending = session.Submit(a, Command("orphan", new PauseCommand()), Now);
        session.Disconnect(a, Now);
        var recovered = session.Connect(Now, new("a"))!;
        session.Tick(Now.AddSeconds(2));
        Assert.Equal("notAuthorized", Assert.IsType<RejectedMessage>(await pending).Code);
        Assert.False(session.Bootstrap().Snapshot.Paused); Assert.False(b.Stop.IsCancellationRequested);
        Assert.Null(session.Bootstrap().Snapshot.Trains[0].ControlLease);
        Assert.IsType<AcceptedMessage>(await Apply(session, recovered, Command("retake", new AcquireControlCommand("train-1")), Now.AddSeconds(2)));
        Assert.NotEqual(oldLease.LeaseId, session.Bootstrap().Snapshot.Trains[0].ControlLease!.LeaseId);
        session.ResetInput(b); session.Tick(Now.AddSeconds(2.1)); Assert.False(session.Bootstrap().Snapshot.Paused);
        session.Disconnect(recovered, Now.AddSeconds(2.2)); session.Disconnect(b, Now.AddSeconds(2.2));
        session.Tick(Now.AddSeconds(3.3)); Assert.True(session.Bootstrap().Snapshot.Paused);
    }

    [Fact]
    public async Task Slow_observer_is_disconnected_without_interrupting_healthy_operator()
    {
        var session = new NetworkSession(new() { OutputCapacity = 2 });
        var active = session.Connect(Now, new("driver"))!; var slow = session.Connect(Now, new("viewer", ParticipantRole.Observer))!;
        Drain(active);
        await Apply(session, active, Command("run", new RunCommand())); Drain(active);
        session.Tick(Now.AddMilliseconds(100)); Drain(active);
        session.Tick(Now.AddMilliseconds(200)); Drain(active);
        Assert.True(slow.Stop.IsCancellationRequested); Assert.False(active.Stop.IsCancellationRequested);
        Assert.False(session.Bootstrap().Snapshot.Paused); Assert.True(session.Bootstrap().Snapshot.Tick >= 3);
    }

    [Fact]
    public async Task Load_clears_leases_for_all_peers_and_rejects_old_epoch_work()
    {
        var session = new NetworkSession(new()); var a = session.Connect(Now, new("a"))!; var b = session.Connect(Now, new("b"))!;
        var saved = session.CaptureSave(); session.Tick(Now); var document = await saved;
        await Apply(session, a, Command("take", new AcquireControlCommand("train-1"))); Drain(a); Drain(b);
        var load = session.LoadSave(document, 1);
        var old = session.Submit(b, Command("old", new StepCommand()), Now);
        session.Tick(Now); await load;
        Assert.Equal("staleEpoch", Assert.IsType<RejectedMessage>(await old).Code);
        foreach (var peer in new[] { a, b })
        {
            var state = Assert.IsType<SnapshotMessage>(await peer.Output.Reader.ReadAsync());
            Assert.Equal(2, state.WorldEpoch); Assert.Null(state.Trains[0].ControlLease); Assert.True(state.Paused);
        }
    }

    [Fact]
    public async Task Separate_train_leases_and_focus_resets_do_not_clear_another_participants_input()
    {
        var scenario = ReferenceScenario.Create();
        var second = scenario.Trains[0] with { Id = "train-2", Cars = [new(20, 24, 1), new(19, 24, 1), new(18, 24, 1)] };
        var host = new SessionHost(scenario with { Trains = scenario.Trains.Add(second) }, "a", [() => new AutomaticController(), () => new ManualTrainController()]);
        var a = new ParticipantContext("a"); var b = new ParticipantContext("b", ParticipantRole.Operator); host.RegisterParticipant(b);
        async Task<WireMessage> Submit(ParticipantContext participant, CommandPayload payload)
        { var task = host.Enqueue(participant, Command(Guid.NewGuid().ToString(), payload)); host.Pump(0, Now); return await task; }
        foreach (var (participant, id) in new[] { (a, "train-1"), (b, "train-2") })
        {
            Assert.IsType<AcceptedMessage>(await Submit(participant, new AcquireControlCommand(id)));
            var train = host.Snapshot().Trains.Single(t => t.Id == id);
            Assert.IsType<AcceptedMessage>(await Submit(participant, new SetControllerCommand(id, train.Revision, "manual", train.ControlLease!.LeaseId)));
            Assert.IsType<AcceptedMessage>(await Submit(participant, new ControllerInputCommand(id, 0, false, true, false, false, false, false, train.ControlLease.LeaseId)));
        }
        host.ResetParticipantInput(a); host.Pump(0, Now);
        Assert.False(host.ReadState().Trains.Single(t => t.Id == "train-1").Input.Depart);
        Assert.True(host.ReadState().Trains.Single(t => t.Id == "train-2").Input.Depart);
        var queuedInput = host.Enqueue(a, Command("queued-before-blur", new ControllerInputCommand("train-1", 1, false, true, false, false, false, false, host.Snapshot().Trains[0].ControlLease!.LeaseId)));
        host.ResetParticipantInput(a); host.Pump(0, Now);
        Assert.Equal("invalidInput", Assert.IsType<RejectedMessage>(await queuedInput).Code);
        Assert.False(host.ReadState().Trains.Single(t => t.Id == "train-1").Input.Depart);
        host.DisconnectParticipant(a); host.Pump(0, Now);
        Assert.Null(host.Snapshot().Trains.Single(t => t.Id == "train-1").ControlLease);
        Assert.NotNull(host.Snapshot().Trains.Single(t => t.Id == "train-2").ControlLease);
        Assert.True(host.ReadState().Trains.Single(t => t.Id == "train-2").Input.Depart);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("owner-key")]
    public async Task Real_leave_revokes_cookie_and_socket_and_allows_sign_in_again(string? secret)
    {
        await using var server = await Harness.Start(secret);
        if (secret != null) await server.Http.PostAsJsonAsync("/api/access", new { secret });
        var initial = await server.Bootstrap();
        var credential = server.Cookies.GetCookies(new Uri(server.Http.BaseAddress!, "/api")).Single(c => c.Name == "freetrain-online.operator").Value;
        using var socket = await server.Connect(); await Read(socket);
        await Send(socket, Command("take-before-leave", new AcquireControlCommand("train-1")));
        await Until(socket, m => m is AcceptedMessage);
        using var crossSite = new HttpRequestMessage(HttpMethod.Post, "/api/leave");
        crossSite.Headers.Add("Origin", "https://untrusted.invalid");
        Assert.Equal(HttpStatusCode.Forbidden, (await server.Http.SendAsync(crossSite)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await server.Http.GetAsync(Harness.BootstrapPath)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await server.Http.PostAsync("/api/leave", null)).StatusCode);
        await Assert.ThrowsAnyAsync<Exception>(async () => await Until(socket, _ => false));
        Assert.Equal(HttpStatusCode.Unauthorized, (await server.Http.GetAsync(Harness.BootstrapPath)).StatusCode);
        using var oldCookies = new HttpClient(new HttpClientHandler { UseCookies = false }) { BaseAddress = server.Http.BaseAddress };
        oldCookies.DefaultRequestHeaders.Add("Cookie", "freetrain-online.operator=" + credential);
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldCookies.GetAsync(Harness.BootstrapPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldCookies.GetAsync("/api/saves/download")).StatusCode);
        using var replay = new ClientWebSocket(); replay.Options.SetRequestHeader("Cookie", "freetrain-online.operator=" + credential);
        await Assert.ThrowsAsync<WebSocketException>(() => replay.ConnectAsync(server.SocketUri, CancellationToken.None));
        // Repeated leave is safe even when the credential was already revoked.
        Assert.Equal(HttpStatusCode.NoContent, (await server.Http.PostAsync("/api/leave", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await server.Http.PostAsJsonAsync("/api/access", new { secret = secret ?? "" })).StatusCode);
        var after = await server.Bootstrap();
        Assert.Equal(initial.WorldEpoch, after.WorldEpoch); Assert.Equal(initial.Snapshot.Tick, after.Snapshot.Tick);
        var fresh = server.Cookies.GetCookies(new Uri(server.Http.BaseAddress!, "/api")).Single(c => c.Name == "freetrain-online.operator").Value;
        Assert.NotEqual(credential, fresh);
        using var rejoined = await server.ConnectEventually();
        var rejoin = Assert.IsType<JoinMessage>(await Read(rejoined));
        // Disconnect cleanup is applied by the host's next pump boundary.
        if (rejoin.Snapshot.Trains[0].ControlLease != null)
            await Until(rejoined, m => m is DeltaMessage d && d.Trains.Any(t => t.Id == "train-1" && t.ControlLease == null));
        Assert.Null((await server.Bootstrap()).Snapshot.Trains[0].ControlLease);
    }

    [Fact]
    public async Task Real_clients_share_world_with_server_assigned_roles_receipts_and_reconnect_identity()
    {
        var settings = new Dictionary<string, string?> {
            ["FreeTrainOnline:Participants:0:Id"] = "builder", ["FreeTrainOnline:Participants:0:Role"] = "operator", ["FreeTrainOnline:Participants:0:Secret"] = "builder-key",
            ["FreeTrainOnline:Participants:1:Id"] = "viewer", ["FreeTrainOnline:Participants:1:Role"] = "observer", ["FreeTrainOnline:Participants:1:Secret"] = "viewer-key"
        };
        await using var server = await Harness.Start("owner-key", participants: settings);
        await server.Http.PostAsJsonAsync("/api/access", new { secret = "owner-key" });
        using var owner = await server.Connect(); var join = Assert.IsType<JoinMessage>(await Read(owner)); Assert.Equal("owner", join.Role);
        var builderCookies = new CookieContainer(); var viewerCookies = new CookieContainer();
        using var builder = new HttpClient(new HttpClientHandler { CookieContainer = builderCookies }) { BaseAddress = server.Http.BaseAddress };
        using var viewer = new HttpClient(new HttpClientHandler { CookieContainer = viewerCookies }) { BaseAddress = server.Http.BaseAddress };
        Assert.Equal(HttpStatusCode.NoContent, (await builder.PostAsJsonAsync("/api/access", new { secret = "builder-key" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await viewer.PostAsJsonAsync("/api/access", new { secret = "viewer-key" })).StatusCode);
        using var operatorSocket = new ClientWebSocket(); operatorSocket.Options.Cookies = builderCookies; await operatorSocket.ConnectAsync(server.SocketUri, CancellationToken.None);
        using var observerSocket = new ClientWebSocket(); observerSocket.Options.Cookies = viewerCookies; await observerSocket.ConnectAsync(server.SocketUri, CancellationToken.None);
        var operatorJoin = Assert.IsType<JoinMessage>(await Read(operatorSocket)); Assert.Equal("operator", operatorJoin.Role); Assert.Equal("builder", operatorJoin.ParticipantId);
        Assert.Equal("observer", Assert.IsType<JoinMessage>(await Read(observerSocket)).Role);
        Assert.Equal(Json(join.Snapshot), Json(operatorJoin.Snapshot));
        foreach (var http in new[] { builder, viewer })
            Assert.Equal(HttpStatusCode.Forbidden, (await http.PostAsync("/api/saves/upload?expectedWorldEpoch=1", new StringContent("{}"))).StatusCode);
        await Send(observerSocket, Command("shared", new StepCommand()));
        Assert.Equal("notAuthorized", Assert.IsType<RejectedMessage>(await Until(observerSocket, m => m is RejectedMessage)).Code);
        await Send(operatorSocket, Command("shared", new RenameStationCommand("station-1", 0, "共有駅")));
        var accepted = Assert.IsType<AcceptedMessage>(await Until(operatorSocket, m => m is AcceptedMessage));
        foreach (var socket in new[] { owner, observerSocket })
            Assert.Equal("共有駅", Assert.IsType<DeltaMessage>(await Until(socket, m => m is DeltaMessage d && d.Stations.Length > 0)).Stations[0].Name);
        operatorSocket.Abort();
        using var recovered = await server.ConnectEventually(builderCookies);
        var fresh = Assert.IsType<JoinMessage>(await Read(recovered)); Assert.Equal(operatorJoin.ParticipantId, fresh.ParticipantId); Assert.Equal("共有駅", fresh.Snapshot.Stations[0].Name);
        await Send(recovered, Command("shared", new RenameStationCommand("station-1", 0, "共有駅")));
        Assert.Equal(accepted, await Until(recovered, m => m is AcceptedMessage));
    }
}
