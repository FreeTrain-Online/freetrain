# P3-07 replacement decision — 29 October 2026

Selected: a bounded source build of **StbImageSharp 2.30.16**, commit
`125af70cb557033f2c46aec8e82eaaf72ac49817`, using its **MIT option**.
FreeTrain's existing LGPL grant and COPYING.txt are unchanged. This decision
covers the heightmap decoder, not a repository-wide licensing certification.

The downloaded NuGet manifest identifies that exact commit, declares
`Unlicense OR MIT`, and has empty dependency groups for net8.0, net471 and
netstandard2.0. The pinned repository's Directory.Build.props repeats that
expression and version; its README expressly grants "Public Domain or MIT".
The incorporated stb_image.h (2.30) supplies Sean Barrett's complete MIT grant.
The [shipped notice](notices/StbImageSharp/LICENSE.txt) preserves that grant,
port attribution, exact source and our selected path. No Apache/custom-license
codec bundle or native decoder binary is introduced.

Sources reviewed:

- [Pinned port/source and README](https://github.com/StbSharp/StbImageSharp/tree/125af70cb557033f2c46aec8e82eaaf72ac49817).
- [Exact NuGet release](https://www.nuget.org/packages/StbImageSharp/2.30.16).
- [Pinned stb source with MIT alternative](https://github.com/StbSharp/StbImageSharp/blob/125af70cb557033f2c46aec8e82eaaf72ac49817/generation/StbImageSharp.Generator/stb_image.h).
- [Hebron generator license at reviewed head](https://github.com/rds1983/Hebron/blob/934d31f363b68f3bffd4b70247bf85466953c5fe/LICENSE).

The Hebron generator repository is GPL3; it is a development tool, not a NuGet
runtime dependency. Nevertheless the port contains small `Hebron.Runtime`
helpers. Rather than infer a permissive sublicense for those copied helpers,
**none of that directory is adopted**. Project-owned C-runtime operations and
fixed table construction replace it. No generator, generated C compiler support,
upstream test code/images or runtime-helper implementation is shipped.
The namespace retained by the generated callers is only a code identifier.
The generated decoder itself is the stb-derived C# translation explicitly
distributed by StbImageSharp under its MIT option; use of a GPL generator does
not by itself license its output under GPL.

The unmodified NuGet binary is **not adopted**: malloc/realloc are unbounded,
its stream read helper expands a temporary array from input, and exceptions can
bypass intermediate frees. Instead the reviewed generated decoder files are
compiled into Server, with private/internal visibility, work/cancellation probes,
a thread-confined allocation arena, and strict span-based stream reads. The arena
counts both blocks during realloc, caps outstanding pointers at256, and frees all
pointers on every exit. GIF code
expansion uses a bounded iterative stack. The source pin, package SHA256 and
upstream file hashes are retained in
[the source manifest](../src/FreeTrain.Online.Server/Imaging/StbImageSharp/SOURCE.json).
Local changes are identified in the vendored source; decoder upgrades require
renewed source/license/resource review. No external package is needed to compile
this source. The independent PNG preview writer uses only .NET ZLibStream.

Distribution must retain the selected MIT text and attribution with the server
and source, including container/release artifacts. The server project copies
`Notices/StbImageSharp/LICENSE.txt` on publish; the Docker build supplies it.
Historical Skia notices and audit records remain documentation only and must not
be copied as current runtime notices. Replacement verification is recorded in
[the heightmap report](web-parity-heightmap.md); source/license review alone does
not close the step.
