#!/usr/bin/env python3
"""Reproduce P0-05 source inventories and optionally inspect a local UC save.

Default generation never searches for user files or optional plugin checkouts.
Historical sample reports are opt-in and must remain under this checkout's tmp/.
"""
import argparse
from collections import Counter
import importlib.util
import json
from pathlib import Path

import parity_nrbf
import parity_serialization


SPEC = importlib.util.spec_from_file_location('inventory_parity', Path(__file__).with_name('inventory-parity.py'))
inventory = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(inventory)
ROOT = inventory.ROOT
PLAN = ROOT / 'doc/parity/domain-transition.json'
BASE_REVISION = '1f755ca6f291253b8cd5024ef53402b85d467170'
IMPLEMENTATION_REVISION = '4ddb808a337e805f519451bad92b71ec02f67ae2'

IMPLEMENTATION_SOURCES = {
    # Paths identify the pinned implementation commit, before the FreeTrain Online rename.
    'src/FreeTrain.Simulation/Definitions.cs': 'Four-direction ordinals, eight prototype shapes, fixed definitions and Park-Miller random stream',
    'src/FreeTrain.Simulation/GridPosition.cs': 'Prototype rectangular coordinates',
    'src/FreeTrain.Simulation/SimulationClock.cs': 'Prototype DateTime/tick origin and semantic move events',
    'src/FreeTrain.Simulation/World.cs': 'Prototype track/car/station state and validation',
    'src/FreeTrain.Content/ReferenceScenario.cs': 'reference-v1 identities, geometry, content and initial state',
    'src/FreeTrain.Contracts/WireContracts.cs': 'Protocol DTOs, roles/epochs/revisions, leases, exact Int64 JSON',
    'src/FreeTrain.Persistence/SaveDocument.cs': 'Save 1 triple, strict decode/restore, 1 MiB and entity/event validation',
    'src/FreeTrain.Persistence/SaveStorage.cs': 'Atomic writes and single writer; path allowlist',
    'src/FreeTrain.Session/SessionHost.cs': 'Authority, leases, wall-time expiry and validated candidate replacement',
    'src/FreeTrain.Server/NetworkEndpoints.cs': 'Owner/epoch save-transfer gates and protocol/content compatibility',
    'src/FreeTrain.Server/NetworkSession.cs': 'Participant lifecycle, coherent snapshots/deltas and bounded transfer',
    'prototype/protocol/schema.json': 'Canonical schema: wire strings, exact counters and independent versions',
    'prototype/protocol/fixtures/save-reference.json': 'Prototype save fixture',
    'web/src/lib/protocol.ts': 'Generated wire/save types; regenerate atomically with schema',
    'web/src/lib/session-client.ts': 'Read-store revision/coherence and command boundary',
    'web/src/lib/remote-session-client.ts': 'Transport generation, epoch, receipts/reconnect and no automatic replay',
    'web/src/lib/shell-model.ts': 'Picked IDs, authority/controller lease and request generations',
    'web/src/lib/save-transfer.ts': '1 MiB upload, captured expected epoch and failure codes',
    'web/src/lib/demo-scene.ts': 'Separate prototype demo identities/data',
    'web/src/App.svelte': 'Draft/conflict/load confirmation, demo/live gates and selected stable IDs',
    'web/src/MapView.svelte': 'Persistent canvas/camera, decoded assets/first draw and draft generations',
    'web/src/lib/map-renderer.ts': 'Prototype geometry/assets and renderer content boundary',
    'tests/FreeTrain.Tests/PersistenceTests.cs': 'Current strict Save 1 round-trip/rejection/continuation obligations',
    'tests/FreeTrain.Tests/MultiplayerTests.cs': 'Authority, lease and epoch regression obligations',
}
ENVELOPE_SOURCES = {
    'core/framework/MainWindow.cs': 'UC writer/BZ reader wrapper and binary-vs-SOAP extension selection',
    'core/framework/plugin/PluginSerializationBinder.cs': 'Historical binder ignores supplied assembly identity and searches contributions',
    'core/AssemblyInfo.cs': 'Canonical assembly version declaration; no ExAV version alias implied',
}


def extract(plan, base, implementation, ledger):
    if (plan['baseRevision'], base.revision) != (BASE_REVISION, BASE_REVISION) or (
        plan['implementationRevision'], implementation.revision) != (IMPLEMENTATION_REVISION, IMPLEMENTATION_REVISION):
        raise ValueError('Persistence transition requires a fresh source review before changing revisions')
    review = parity_serialization.inventory(base, ledger, inventory.digest)
    review['envelopeSourceReviews'] = [dict(path=p, sha256=inventory.digest(base.read(p)), review=r)
                                       for p, r in ENVELOPE_SOURCES.items()]
    transition = dict(schemaVersion=1, baseRevision=base.revision,
                      implementationRevision=implementation.revision,
                      normalizedPlanSha256=inventory.digest(inventory.dump(plan)),
                      status='design-input; no runtime migration or compatibility mode implemented',
                      implementationSourceReviews=[dict(path=p, sha256=inventory.digest(implementation.read(p)), review=r)
                                                   for p, r in IMPLEMENTATION_SOURCES.items()],
                      decisionIds=[r['id'] for r in plan['decisions']],
                      differenceIds=[r['id'] for r in plan['differences']],
                      acceptanceIds=[r['id'] for r in plan['acceptance']])
    return review, transition


def member(node, name):
    values = [m['value'] for m in node['members'] if m['name'] == name]
    if len(values) != 1:
        raise ValueError('Ambiguous or missing field: ' + name)
    return values[0]


def inspect_sample(path, review, ledger):
    # Reject by stat before reading the input into memory.
    limits = parity_nrbf.Limits()
    if path.stat().st_size > limits.file_bytes:
        raise ValueError('fileLimit before read')
    raw = path.read_bytes()
    reader = parity_nrbf.Reader(raw, limits)
    streams = reader.streams()
    bgm, world_stream = streams
    bgm_root = bgm['nodes'][bgm['rootId']]
    nodes = world_stream['nodes']
    world = nodes[world_stream['rootId']]
    if (bgm_root['kind'], bgm_root.get('lengths'), bgm_root.get('elementType')) != ('array', [1], 4) or (
        bgm_root.get('additional', {}).get('name') != 'freetrain.contributions.sound.BGMContribution' or
        world.get('type') != 'freetrain.world.World'):
        raise ValueError('unexpected FreeTrain root types')
    def deref(value):
        return nodes[value['ref']]
    by_type = {}
    for t in review['types']:
        by_type.setdefault(t['name'], []).append(t)
    layouts, seen_layouts = [], set()
    for meta in world_stream['metadata'].values():
        signature = json.dumps(meta, sort_keys=True)
        if signature in seen_layouts:
            continue
        seen_layouts.add(signature)
        name = meta['name']
        choices = by_type.get(name, [])
        row = dict(type=name, observedNames=meta['names'], sourceCandidates=[t['sourceTypeId'] for t in choices])
        if name.startswith('System.'):
            row['disposition'] = 'framework-record; explicit inert container/value/delegate mapping required in P10'
        elif len(choices) != 1:
            row['disposition'] = 'unresolved-or-ambiguous-type; block conversion'
        else:
            t = choices[0]
            expected = set(['value__'] if t['kind'] == 'enum' else t['customKeys'] if t['serializationMode'] == 'custom'
                           else [f['name'] for f in t['fieldSlots'] if not f['exclusion']])
            observed = {n.rsplit('+', 1)[-1] for n in meta['names']}
            # Default serializers may emit additional qualified copies of
            # inherited nonpublic fields. Compare names only, keeping the full
            # ordinal wire layout above; this is not a field-signature proof.
            framework = {'list'} if 'CollectionBase' in t['externalOrUnresolvedBases'] else set()
            if 'Random' in t['externalOrUnresolvedBases']:
                framework |= {'inext', 'inextp', 'SeedArray'}
            row.update(sourceOnlyFields=sorted(expected - observed),
                       sampleOnlyFields=sorted(observed - expected - framework),
                       inheritedFrameworkFields=sorted(observed & framework),
                       externalOrUnresolvedBases=t['externalOrUnresolvedBases'],
                       disposition='field-name-set-match; types/order/duplicates/semantics still unverified' if expected == observed - framework
                       else 'field-name-difference; explicit source/profile review required')
        layouts.append(row)
    base_ids = {r['contributionId'] for r in ledger['contributions'] if r.get('contributionId')}
    contributions = []
    for object_id, node in sorted(nodes.items()):
        if node.get('type') == 'freetrain.framework.plugin.Contribution+ReferenceImpl':
            contribution_id = deref(member(node, 'id'))['value']
            contributions.append(dict(objectId=object_id, id=contribution_id,
                                      disposition='exact-base-ID' if contribution_id in base_ids else 'not-in-base; pack/alias resolution required'))
    delegates = Counter()
    for node in nodes.values():
        if node.get('type') == 'System.DelegateSerializationHolder+DelegateEntry':
            target, method = member(node, 'targetTypeName'), member(node, 'methodName')
            delegates[(deref(target)['value'], deref(method)['value'])] += 1
    size = deref(member(world, 'size'))
    clock = deref(member(world, 'clock'))
    return dict(schemaVersion=1, sampleSha256=inventory.digest(raw), sampleBytes=len(raw),
                status='offline-structural-inspection; ExAV reference; no domain conversion or runtime load',
                sourceRevision=review['revision'], container='UC', fullyConsumed=True,
                unresolvedObjectReferences=0, limits=vars(limits), recordCounts=dict(sorted(reader.record_counts.items())),
                roots=[parity_nrbf.summary(s) for s in streams],
                worldFacts=dict(size={k: member(size, k) for k in ('x', 'y', 'z')},
                                waterLevel=member(world, 'waterLevel'),
                                clockFields=clock['members'],
                                groundArray={k: v for k, v in deref(member(world, 'groundLevels')).items() if k != 'values'}),
                sourceLayoutComparison=layouts, contributionReferences=contributions,
                delegateTargets=[dict(type=t, method=m, entries=c) for (t, m), c in sorted(delegates.items())],
                evidenceLimits=['NRBF structural decoding is not CLR/domain validation or proof of canonical compatibility.',
                                'Field-name set comparisons do not verify type/order/duplicate/value semantics; raw layouts retain every ordinal.',
                                'Primitive arrays above 256 elements retain byte hashes, not full conversion values.',
                                'No optional-suite identity resolution, original runtime, SOAP sample or continuation was tested.'])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--sample', type=Path)
    parser.add_argument('--sample-report', type=Path)
    args = parser.parse_args()
    if bool(args.sample) != bool(args.sample_report):
        parser.error('--sample and --sample-report must be used together')
    if args.sample_report and (not args.sample_report.resolve().is_relative_to((ROOT / 'tmp').resolve()) or
                               args.sample_report.resolve() == args.sample.resolve()):
        parser.error('--sample-report must be a separate file under this checkout\'s tmp/')
    plan = json.loads(PLAN.read_text(encoding='utf-8'))
    base = inventory.Tree(ROOT, plan['baseRevision'])
    implementation = inventory.Tree(ROOT, plan['implementationRevision'])
    ledger = json.loads((inventory.OUT / 'base-ledger.json').read_text(encoding='utf-8'))
    review, transition = extract(plan, base, implementation, ledger)
    inventory.write(inventory.OUT / 'serialization-review.json', inventory.dump_ledger(review), args.check)
    inventory.write(inventory.OUT / 'domain-transition-review.json', inventory.dump(transition), args.check)
    print(json.dumps(review['counts'], sort_keys=True))
    if args.sample:
        report = inspect_sample(args.sample, review, ledger)
        inventory.write(args.sample_report, inventory.dump_ledger(report), args.check)
        print(json.dumps(dict(sampleBytes=report['sampleBytes'], roots=len(report['roots']),
                              objects=sum(r['objects'] for r in report['roots']),
                              layoutDifferences=sum('difference' in r['disposition'] for r in report['sourceLayoutComparison']),
                              contributionReferences=len(report['contributionReferences'])), sort_keys=True))


if __name__ == '__main__':
    main()
