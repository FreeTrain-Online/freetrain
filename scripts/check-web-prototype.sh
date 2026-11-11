#!/bin/sh
set -eu
cd "$(dirname "$0")/.."
deno eval --no-config '
const expected = Deno.readTextFileSync(".deno-version").trim();
const actual = Deno.version.deno;
const stableVersion = /^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$/;
const minimum = stableVersion.exec(expected);
if (!minimum) throw new Error(`Expected a stable major.minor.patch version in .deno-version, found ${expected}`);
const installed = stableVersion.exec(actual);
if (!installed || installed[1] !== minimum[1] || installed[2] !== minimum[2] || Number(installed[3]) < Number(minimum[3])) {
  throw new Error(`Expected Deno >=${expected} <${minimum[1]}.${Number(minimum[2]) + 1}.0 (stable), found ${actual}`);
}
'
python3 scripts/check-project-boundaries.py
python3 scripts/review-stations.py --check
python3 scripts/reconcile-station-delivery.py --check
python3 scripts/prepare-bundled-content.py --check
python3 -m unittest discover -s scripts/tests
python3 scripts/relief-fixtures.py --check
python3 scripts/coast-fixtures.py --check
python3 scripts/water-fixtures.py --check
python3 scripts/forest-fixtures.py --check
dotnet restore FreeTrain.Online.slnx --locked-mode
dotnet build FreeTrain.Online.slnx -c Release --no-restore
dotnet test FreeTrain.Online.slnx -c Release --no-build --no-restore
deno install --config web/deno.json --frozen
deno task --cwd web test
deno task --cwd web build
