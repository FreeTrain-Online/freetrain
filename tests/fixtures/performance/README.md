# Backend benchmarks and correctness checks

Performance numbers are informational. CI publishes a base-versus-PR comparison
for human review; there are no allocation, timing or payload-size thresholds.
The normal C# tests still enforce deterministic state and protocol correctness.

## PR report

`.github/workflows/backend-benchmarks.yml` checks out the exact PR head and its
base commit and runs both on the same Ubuntu runner. It installs the PR's same
benchmark harness into the disposable base checkout, so a base without the new
benchmark files can still be measured. Production sources and base package
dependencies stay at their respective commits.

Three fresh test processes per version run in alternating base/head order. The
report shows:

- Median milliseconds per operation and the minimum–maximum range.
- Median allocated bytes per operation and percentage changes for both metrics.
- Whether initial/final canonical saves and operation counts match.
- Exact commit SHAs, sample success counts and a link to the raw measurements.

Missing samples, build/test errors and changed states remain visible. Percentage
comparisons are omitted for incomplete samples or different workloads. A slower
or more allocation-heavy implementation does not fail the benchmark job.

Numbers appear in the job summary and the `backend-benchmark-report` artifact,
including JSON measurements, TRX results and build/test/SDK logs. A separate
publisher creates or updates one bot comment on the PR and skips obsolete heads
and older benchmark runs. Manual runs accept an optional `base_ref` and publish
the summary/artifact without creating a PR comment.

The read-only benchmark workflow executes PR code. The write-enabled publisher
executes only default-branch code and validates the downloaded JSON as data; it
does not run PR scripts or binaries. PR lookup comes from GitHub's commit/PR APIs,
including fork PRs whose `workflow_run.pull_requests` list is empty. No personal
access token is needed. The publisher workflow must first exist on the default
branch, as required by [GitHub's `workflow_run` trigger](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#workflow_run).

## Local runs

Run correctness checks and print one set of measurements:

```sh
FREETRAIN_BACKEND_PERFORMANCE_OUTPUT="$PWD/tmp/backend-measurements" \
  dotnet test tests/FreeTrain.Online.Tests -c Release \
  --filter 'Category=BackendPerformance' --logger 'console;verbosity=detailed'
```

Use an absolute measurement output path. For the same comparison as CI, prepare
a **fresh, clean, disposable base checkout** and a separate PR checkout:

```sh
python3 scripts/benchmark-backend.py \
  --base /tmp/freetrain-base --head "$PWD" \
  --output "$PWD/tmp/backend-comparison" --repetitions 3
node .github/scripts/backend-benchmark-report.cjs \
  tmp/backend-comparison/report.json > tmp/backend-comparison/summary.md
```

The comparison output directory must be new or empty. The runner overlays only
the two benchmark C# sources, their expected-state JSON and the corresponding
resource entry in the base test project. It restores locked dependencies and
builds both versions in Release. Always inspect failed build/test logs when a
base's APIs or save contract are incompatible with the PR harness.

## Scenarios and correctness

The fixture uses only bundled public content and never reads a developer save,
`tmp/`, Chromium or a live server. Both map sizes contain two purchased/deployed
three-car trains, two stations/platforms and six active city construction sites.
The small map is 64×64×24 with 1,024 natural owners; the large map is 400×600×24
with 55,179. Dynamic content is built through production domain operations;
literal natural owners are installed once outside the measurement.

| Cases | Measured operation |
|---|---|
| Small/large mixed simulation | One minute, averaged across 64 minutes |
| Midnight boundary | One minute, averaged across four minutes |
| Small/large unchanged replacement | One replacement, averaged across 32/three calls |
| Running publication, one/four peers | One 50 ms tick, averaged across 17 ticks |
| Paused publication, one/four peers | One 50 ms tick, averaged across 128 ticks |

Construction, heat diffusion and hourly callbacks execute within the daytime
window. The midnight case exercises daily/hourly station work and stopped trains.
The publication cases use real `NetworkSession.Tick`, publication and queue code,
followed by the socket sender's JSON encoding. They drain in-memory queues;
socket I/O, joins and backpressure are outside this benchmark.
The harness binds the server's cached outbound encoder when available, falling
back to the original JSON encoding on older base commits. Delegate lookup occurs
outside measurement, so comparisons include the actual encoding work for each
version.

The nine measured cases and a tenth retention test still enforce:

- Fixed operation counts and complete canonical save hashes from
  `backend-expectations.json`, covering geometry, accounts, clock/queue, random
  streams and all heat buffers. These are behavior expectations, not performance
  baselines. Do not refresh them merely to accept an optimization.
- Exact save/reopen continuation and real train/construction/station progress.
- Correct per-peer delta bases, full heat on diffusion and empty-buffer references
  for unchanged heat; paused worlds send no deltas.
- Collection of obsolete worlds while the current session remains alive, guarding
  against history chains in future validation certificates and occupancy caches.

These invariants run in the normal C# suite. Each benchmark sample also runs them
and retains its test exit status in the report.

## Interpretation

`GC.GetAllocatedBytesForCurrentThread` brackets synchronous operations with no
`await` inside the measurement. Setup, warmup, reporting and save/reopen checks
are excluded. An operation moved to another thread needs a revised measurement
scope. Shared-runner timing is noisy; three samples and ranges provide context,
not statistical confidence or a performance verdict.

Use the live profiling harness for process/native/GC CPU, real sockets, sustained
load and end-to-end latency. Review improvements against the PR's freshly measured
base rather than a machine-specific checked-in allocation allowance.
