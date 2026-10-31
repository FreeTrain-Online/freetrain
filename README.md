# FreeTrain Online

A web-based multiplayer version of FreeTrain, a train simulation game originally developed by [Kohsuke Kawaguchi](https://www.kohsuke.org/freetrain/). This project aims to bring the classic FreeTrain experience to modern web browsers, allowing players to enjoy the game together online.

We are forked from [FreeTrain English version](https://sourceforge.net/projects/freetrain/) while borrowing elements from [FreeTrain EX Av](https://w.atwiki.jp/freetrain-exav/), but the codebase is largely rewritten in modern C# and TypeScript. The server is built with .NET 10, while the client uses Svelte and Vite.

FreeTrain Online is in early development. You can explore and edit a shared world, then build and remove normal railway routes using two map points or keyboard coordinates with a cost and loss preview. Moving trains, stations, special railway infrastructure and the city economy are still in development. There is no public online instance yet; running it currently requires a local setup.

## Features

- Explore a live map together, with elevation, water and forests.
- Create flat or generated worlds, or import a heightmap image.
- Modify terrain with painting and other terrain tools.
- Lay, attach, branch and remove normal railway with an authoritative preview.
- Save and restore worlds, including automatic checkpoints.
- English and Japanese localizations.

## Run locally

Install the .NET SDK in `global.json` (currently 10.0.402) and Deno in
`.deno-version` (currently 2.9.7). See the official
[.NET download](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) and
[Deno installation guide](https://docs.deno.com/runtime/getting_started/installation/).

The solution to build is `FreeTrain.Online.slnx`.

In separate terminals:

```sh
dotnet run --project src/FreeTrain.Online.Server --no-launch-profile
```

```sh
deno task --cwd web dev
```

Open the Vite loopback URL printed in the second terminal. The server binds to
`http://127.0.0.1:5080`; Vite proxies `/api` HTTP and WebSocket traffic to it.
Local browsers join automatically and share the same world.

## Getting started

Drag the map to pan and use the mouse wheel or zoom buttons to zoom. To edit the
landscape, open **Game → Modify terrain**, choose **Terrain selection → Paint
terrain**, and select **Raise terrain** or **Lower terrain**. Hold the left mouse
button over the map to paint; changes appear in the shared world immediately.

Use **Game → New world** to create or import a world. The game saves automatic
checkpoints, and you can also save, load or download a world from the save panel.
**Game → Settings** lets you change the language and appearance.

For more setup, multiplayer, deployment and development details, see
[README.web.md](README.web.md).

## License

FreeTrain itself is licensed under the LGPL. All use and redistribution are
subject to this license. Individual plugins may have different licensing
agreements, but unless specifically noted, please assume LGPL.
See [COPYING.txt](COPYING.txt) for the license text and
[NOTICE.txt](web/public/assets/NOTICE.txt) for asset notices.

    FreeTrain
    Copyright (C) 2002, Kohsuke Kawaguchi et al.
    FreeTrainEx
    Copyright (C) 2005, C477 et al.
    FreeTrainEx-Aver
    Copyright (C) 2008, FreeTrainEx-Aver Project et al.
    FreeTrain Online
    Copyright (C) 2026, Daniel Markstedt <daniel@mindani.net>

    This library is free software; you can redistribute it and/or
    modify it under the terms of the GNU Lesser General Public
    License as published by the Free Software Foundation; either
    version 2.1 of the License, or any later version.

    This library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
    Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public
    License along with this library; if not, write to the Free Software
    Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
