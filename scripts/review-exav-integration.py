#!/usr/bin/env python3
"""P0-09 source/scope accounting. Requires an explicit archive; reads Git blobs.

Assignments identify delivery owners, not semantic equivalence or runtime parity.
"""
import argparse
import collections
import importlib.util
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PARITY = ROOT / 'doc/parity'
SPEC = importlib.util.spec_from_file_location('inventory', ROOT/'scripts/inventory-parity.py')
inv = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(inv)
RULES = PARITY/'exav-integration-rules.json'
OUTPUT = PARITY/'exav-integration-review.json'
EXTENSIONS = {'.cs', '.cpp', '.h'}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def assign(path, rules):
    matches = [r for r in rules if any(path == p or path.startswith(p.rstrip('/')+'/')
                                      for p in r['paths'])]
    require(matches, 'Unassigned source: '+path)
    return matches[0]


def strings(value):
    if isinstance(value, str):
        yield value
    elif isinstance(value, dict):
        for key, item in value.items():
            yield key
            yield from strings(item)
    elif isinstance(value, list):
        for item in value:
            yield from strings(item)


def evidence(tree, rows):
    result = []
    for row in rows:
        raw = tree.read(row['path'])
        text = tree.text(row['path'])[0]
        require(all(s in text for s in row['symbols']), 'Missing landmark: '+row['path'])
        result.append(dict(row, sha256=inv.digest(raw)))
    return result


def build(source_root):
    config = json.loads((PARITY/'exav-baseline.json').read_text())
    rules = json.loads(RULES.read_text())
    require(rules['sourceId'] == config['sourceId'], 'Wrong integration authority')
    tree = inv.Tree(source_root, config['revision'], config['subtree'])
    original = inv.Tree(ROOT, config['historicalComparisonRevision'])
    implementation = inv.Tree(ROOT, rules['implementationRevision'])
    ledger = json.loads((PARITY/'exav-ledger.json').read_text())
    require(ledger['revision'] == tree.revision, 'Discovery revision drift')
    inputs = {name: inv.digest((PARITY/name).read_bytes())
              for name in ['exav-baseline.json', 'exav-ledger.json']}
    referenced = collections.defaultdict(set)
    for name in rules['reviewArtifacts']:
        path = ROOT/name
        raw = path.read_bytes()
        inputs[name] = inv.digest(raw)
        for value in strings(json.loads(raw)):
            p = value.removeprefix('trunk/')
            if p in tree.entries and Path(p).suffix in EXTENSIONS:
                referenced[p].add(name)
    sources = {r['baselinePath'].removeprefix('trunk/'): r for r in ledger['sourceFiles']}
    source_rows = []
    files = {r['baselinePath'].removeprefix('trunk/'): r['sha256'] for r in ledger['fileInventory']}
    for path in sorted(p for p in tree.entries if Path(p).suffix in EXTENSIONS):
        scope = assign(path, rules['sourceAssignments'])
        raw = tree.read(path)
        require(inv.digest(raw) == files[path], 'Discovery source hash drift: '+path)
        same_path = path in original.entries
        old = original.read(path) if same_path else None
        source_rows.append(dict(path=path, sha256=inv.digest(raw),
            assignment=scope['id'], disposition=scope['disposition'], phases=scope['phases'],
            destination=scope['destination'],
            projectMembership=sources.get(path, {}).get('projectMembership', []),
            projectMembershipKind='lexical C# membership only; native projects remain dependency evidence',
            historicalComparison=dict(path=path if same_path else None,
                sha256=inv.digest(old) if same_path else None,
                result='byte-identical; dependency review still required' if old == raw else
                       'bytes-differ; no semantic-equivalence claim' if same_path else
                       'no historical same-path source'),
            reviewReferences=sorted(referenced[path]), status='scope-assigned; feature detail and runtime acceptance pending'))
    original_only = []
    roots = {p.split('/')[0] for p in tree.entries}
    for path in sorted(p for p in original.entries if p.split('/')[0] in roots
                       and Path(p).suffix in EXTENSIONS and p not in tree.entries):
        original_only.append(dict(path=path, sha256=inv.digest(original.read(path)),
            disposition='historical-native-host-reference' if Path(path).suffix in {'.cpp', '.h'}
                        else 'historical-only; no additional stable feature obligation',
            phases=['P2', 'P11'] if Path(path).suffix in {'.cpp', '.h'} else ['P10'],
            acceptance='Retain native-host comparison/notices without porting Windows APIs.'
                       if Path(path).suffix in {'.cpp', '.h'} else
                       'Map identities/relocations only when required for historical import; do not add original-only features to stable scope.'))
    requirements = []
    require([r['id'] for r in rules['requirements']] == [f'EXAV-{n:02}' for n in range(1, 17)], 'Requirement coverage')
    for row in rules['requirements']:
        require(row['phases'] and row['specifications'] and row['acceptance'], 'Incomplete requirement')
        for name in row['specifications']:
            require((ROOT/name).is_file(), 'Missing maintained specification: '+name)
        requirements.append(dict(row, evidence=evidence(tree, row['evidence'])))
    gaps = []
    for row in rules['gaps']:
        require(row['disposition'] and row['phases'] and row['acceptance'], 'Incomplete gap disposition')
        gaps.append(dict(row, evidence=evidence(tree, row['evidence'])))
    branches = []
    for row in rules['branches']:
        branch = inv.Tree(source_root, config['revision'], row['subtree'])
        require(row['version'] in branch.text('core/AssemblyInfo.cs')[0], 'Wrong branch version')
        branches.append(dict(row, evidence=evidence(branch, row['evidence'])))
    require(len(source_rows) == 465, 'Stable source denominator drift')
    require({r['id'] for r in rules['sourceAssignments']} == {r['assignment'] for r in source_rows}, 'Unused source assignment')
    # Exact text occurrences, including comments; not a C# call graph.
    searches = []
    for needle in ['getTimer(', '.calcEntering(', '.triprange', 'new Bus(']:
        matches = []
        for row in source_rows:
            if row['path'].endswith('.cs'):
                for number, line in enumerate(tree.text(row['path'])[0].splitlines(), 1):
                    if needle in line:
                        matches.append(dict(path=row['path'], line=number, text=line.strip()))
        searches.append(dict(needle=needle, matches=matches,
                             method='exact text across all stable C# sources, including comments; not compiler reachability'))
    return dict(schemaVersion=1, sourceId=config['sourceId'], revision=config['revision'],
        subtree=config['subtree'], reviewedOn=rules['reviewedOn'],
        status='P0-09 source authority/scope integration; no implementation or runtime closure',
        inputs=inputs, rulesSha256=inv.digest(RULES.read_bytes()),
        generatorSha256=inv.digest(Path(__file__).read_bytes()), requirements=requirements,
        implementationRevision=rules['implementationRevision'],
        implementationSources=evidence(implementation, rules['implementationSources']),
        gaps=gaps, sourceGapSearches=searches, branchProfiles=branches, sourceAssignments=source_rows,
        historicalOnlySources=original_only,
        counts=dict(sourceFiles=len(source_rows), sourceExtensions=dict(collections.Counter(Path(r['path']).suffix for r in source_rows)),
                    assignments=dict(collections.Counter(r['assignment'] for r in source_rows)),
                    historicalOnlySources=len(original_only), requirements=len(requirements), gaps=len(gaps)),
        boundaries=rules['boundaries'])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', required=True, type=Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    result = build(args.source_root)
    data = json.dumps(result, ensure_ascii=False, indent=2)+'\n'
    if args.check:
        require(OUTPUT.read_text() == data, 'Stale integration review; regenerate explicitly')
    else:
        OUTPUT.write_text(data)
    print(f"P0-09: {len(result['requirements'])} requirements, {len(result['gaps'])} dispositions, "
          f"{len(result['sourceAssignments'])} stable source assignments; runtime parity remains unverified.")


if __name__ == '__main__':
    main()
