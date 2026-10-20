# Operating multiplayer games

Run several browser clients against one authoritative world to test Owner,
Operator and Observer permissions. Roles come from server configuration and the
access key used to sign in.

## Default local mode

Without `FreeTrainOnline__OperatorSecret`, loopback access automatically assigns an
independent Owner identity to each browser cookie jar. Firefox and Chrome can
therefore both show Owner on the same machine. Tabs sharing cookies share one
participant identity.

To exercise other roles, configure the main owner key as well as the additional
participant keys. Setting the owner key disables automatic Owner access and
makes the browser show the sign-in form.

## Start a server with three test identities

Use the .NET and Deno versions specified in [README.web.md](../README.web.md).
Run the following commands from the repository root.

Stop the existing backend, then start it with these local test credentials:

```sh
FreeTrainOnline__OperatorSecret=owner-test \
FreeTrainOnline__Participants__0__Id=builder \
FreeTrainOnline__Participants__0__Role=operator \
FreeTrainOnline__Participants__0__Secret=operator-test \
FreeTrainOnline__Participants__1__Id=viewer \
FreeTrainOnline__Participants__1__Role=observer \
FreeTrainOnline__Participants__1__Secret=observer-test \
dotnet run --project src/FreeTrain.Online.Server --no-launch-profile
```

The backend normally listens on `http://127.0.0.1:5080`. The example keys are for
local testing. Keep real access keys in backend configuration; use the
[deployment guide](web-deployment.md) for remote operation.

In another terminal, start the frontend, or leave the existing frontend running:

```sh
deno task --cwd web dev
```

Open the frontend URL printed by Vite. Its default proxy forwards `/api` requests
and WebSockets to the backend on port 5080. All clients must use the same backend
to share the world. Existing saves and checkpoints continue to use the configured
save directory; enabling authenticated access does not create a separate world.

## Sign in from separate browsers

Restarting the backend invalidates existing login cookies. Reload each browser
and enter a key in the **Operator access key** field. Despite that field's label,
it accepts the configured keys for all three roles.

| Browser/session | Access key | Participant ID | Expected role |
| --- | --- | --- | --- |
| Firefox | `owner-test` | `operator-1` | Owner |
| Chrome | `operator-test` | `builder` | Operator |
| Private/incognito window | `observer-test` | `viewer` | Observer |

The role appears beside **Reconnect** and the connection status. **Leave game**
ends the current login, disconnects its socket, releases its train control and
returns to the sign-in form. Other participants remain connected. Labels are
localized when the UI language is Japanese.

Each participant supports one active socket. Signing in with the same key in two
clients gives them the same identity; the second client waits until the first
disconnects. Use separate configured identities for simultaneous clients. Private
windows may share cookies with other private windows in the same browser, so use
separate browsers or profiles when more independent identities are needed.

## Expected permissions and shared behavior

| Action | Owner | Operator | Observer |
| --- | --- | --- | --- |
| Inspect the map and receive shared updates | Yes | Yes | Yes |
| Run/pause/step, rename stations, build/remove track | Yes | Yes | No |
| Acquire train control and change controller/send manual input | Yes | Yes | No |
| Write save slots or replace the world by loading/uploading | Yes | No | No |

The current browser disables the whole save panel for Operators and Observers.
The API permits authenticated participants to list/download saves; writing saves
and replacing the world require Owner authority.

Train control requires an exclusive five-second lease, including for Owners.
Successful manual input renews the lease. **Take control** acquires or renews it;
**Release control**, disconnect or expiry makes the train available to another
participant. The inspector shows the current holder.

For a quick manual check:

1. Rename the station as Owner and confirm that Operator and Observer see the
   same committed name. Operator can also rename; Observer's mutation controls
   should be disabled.
2. Take train control as Owner, then immediately try **Take control** as Operator
   while the lease is valid. Expect a message that another participant controls
   the train. Release it as Owner, then acquire it as Operator.
3. Run the game as Owner or Operator. Select a construction tool or hide another
   browser tab and confirm that shared time continues. Use **Pause** explicitly
   when needed.
4. Refresh one client. It should retain its authenticated identity and recover
   confirmed world state. Train control must be reacquired after disconnect.
5. Close all Owner/Operator clients while leaving Observer connected. The game
   should pause after the default one-second disconnect grace. Observer alone
   does not keep unattended simulation running.

## Change roles or return to automatic local access

To change an identity's role, edit its `FreeTrainOnline__Participants__<index>__Role`
setting to `owner`, `operator` or `observer`, then restart the backend. Reload
clients and sign in again. Additional identities need distinct IDs and keys;
`operator-1` is reserved for the main owner key.

To sign in with a different key without restarting the server, click **Leave
game**, enter the other key and click **Connect**. The old login credential is
revoked on the server, so copying or restoring its cookie cannot reuse it. The
configured access keys remain valid for signing in again. Tabs sharing the same
login cookie are signed out together.

After leaving, refreshing the browser keeps it at sign-in. In automatic local
mode, the form explains that you can leave the key blank and click **Connect** to
join as a new local Owner. When the main owner key is configured, a valid key is
required. If leaving cannot be confirmed, the displayed world is cleared and
**Leave game** remains available to retry.

Leaving preserves the shared world and changes already committed by the server.
The existing last-operator disconnect policy still pauses/checkpoints after its
grace period. Login sessions expire after eight hours or a server restart. The
server retains at most 1,024 unexpired login credentials; expired sessions are
pruned, and leaving frees that login's capacity immediately.

To restore automatic local Owner access, stop the configured backend and restart
it with the owner key and additional participant settings removed. The inline
environment assignments in the example apply only to that launch; if those
settings were exported separately or put in a configuration file, remove them
there as well.

If all clients still show Owner, verify that the active backend was restarted
with `FreeTrainOnline__OperatorSecret` set and that the frontend points to that backend.
If a second tab keeps reconnecting, check whether another tab uses the same
participant. The access endpoint allows ten sign-in attempts per minute across
the server; after HTTP 429, wait for that window to reset before retrying.
