#!/usr/bin/env python3
"""Validate P5-01 source/operation/decision/text scope, without movement code.

Default mode uses repository evidence; --source-root additionally verifies raw
pinned Git blobs and exact symbol excerpts. Neither mode executes desktop code
or establishes P5-02 independent fixture/runtime acceptance.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
OPERATIONS = {
    'stock', 'trade', 'deploy', 'movement', 'reversal', 'timing-cost', 'outside',
    'simple', 'manual', 'manager', 'tat-junction', 'tat-stop', 'tat-advanced',
    'turning', 'groups', 'diagram', 'information-tracking', 'color-test',
}
DEPENDENCIES = {
    'fleet-occupancy', 'scheduler-accounts', 'content-composition',
    'controller-inheritance', 'harbor-dispatch', 'masks-and-turning',
    'files-and-clone', 'presentation',
}
STOCK_TYPES = {'train', 'trainCar', 'colorMapTrainPicture', 'trainController'}
INPUTS = {f'doc/parity/{name}.json' for name in (
    'exav-action-review', 'exav-content-review', 'exav-scenarios',
    'exav-geometry-dependency-review', 'railway-p5-handoff',
    'speed-limit-handoff', 'clock-review',
)}
POLICIES = {
    'signalDispatch': 'effective-train-controller',
    'signalControllerId': 'reserved-null-not-dispatch',
    'bothNull': 'emergency-one-prior-head-retry-after-source-reversal',
    'delegationNull': 'simple-fallback-reject-cycles',
    'placement': 'retain-source-no-zdiff',
    'positiveStopState': 'StoppingAtStation',
    'dayEditor': 'restore-rule-day-in-both-editors',
    'rules': 'stored-order-sorter-commented',
    'arithmetic': 'checked-at-original-source-width',
    'numericSpeed': 'positive-int32-native-branches-retained',
    'save': 'current-format-only-no-P5-world-import',
    'ge002': 'preserve-P1-executing-entry-cancellation',
}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def read(name):
    return json.loads((ROOT / 'doc/parity' / name).read_text())


def digest(value):
    return hashlib.sha256(json.dumps(
        value, ensure_ascii=False, sort_keys=True, separators=(',', ':')).encode()).hexdigest()


def unique(rows, key, label):
    values = {r[key]: r for r in rows}
    require(len(values) == len(rows), 'Duplicate ' + label)
    return values


def literal_key(row):
    return row['source'], row['line'], row['key'], row['expression']


def validate(review, locale, source_root=None):
    baseline = read('exav-baseline.json')
    authority = review['authority']
    require(authority == {k: baseline[k] for k in
                         ('sourceId', 'revision', 'subtree', 'engineVersion', 'pluginFamily')},
            'Unreviewed authority')
    require(review['slice'] == locale['slice'] == 'P5-01' and
            review['status'] == 'source-contract-specified' and
            review['runtimeStatus'] == 'unimplemented' and
            locale['status'] == 'translation-scope-recorded-not-shipped' and
            locale['authority'] == authority, 'Overclaimed review or translation status')
    for label, document in [('contract', review), ('localization', locale)]:
        require(document['reviewSha256'] == digest({k: v for k, v in document.items()
                                                  if k != 'reviewSha256'}),
                'Review digest drift: ' + label)
    require(review['policies'] == POLICIES and review['approvalGates'] == [],
            'Portable policy drift or invented approval gate')
    require(set(unique(review['inputs'], 'path', 'input')) == INPUTS,
            'Missing prerequisite evidence')
    for row in review['inputs']:
        require(hashlib.sha256((ROOT / row['path']).read_bytes()).hexdigest() == row['sha256'],
                'Input evidence drift: ' + row['path'])

    ledger = {r['baselinePath']: r['sha256'] for r in read('exav-ledger.json')['fileInventory']}
    sources = unique(review['sourceReviews'], 'path', 'source')
    texts = {}
    for path, row in sources.items():
        require(ledger.get('trunk/' + path) == row['sha256'], 'Source hash drift: ' + path)
        require(row['review'] and row['reviewKind'] and row['bindings'],
                'Missing source review: ' + path)
        for binding in row['bindings']:
            require(binding['line'] > 0 and binding['symbol'] in binding['text'],
                    'Invalid symbol binding: ' + path)
        if source_root is not None:
            raw = subprocess.check_output([
                'git', '-C', str(source_root), 'show',
                f"{authority['revision']}:{authority['subtree']}/{path}"])
            require(hashlib.sha256(raw).hexdigest() == row['sha256'], 'Pinned blob drift: ' + path)
            text = raw.decode(row['encoding'])
            texts[path] = text
            lines = text.splitlines()
            for binding in row['bindings']:
                require(binding['line'] <= len(lines) and
                        lines[binding['line'] - 1] == binding['text'],
                        'Pinned excerpt drift: ' + path)

    operations = unique(review['operations'], 'id', 'operation')
    require(operations.keys() == OPERATIONS, 'Operation coverage drift')
    decisions = unique(review['decisions'], 'id', 'decision')
    require(decisions.keys() == {f'TRAIN-D{i:03}' for i in range(1, 19)}, 'Decision coverage drift')
    covered_sources = set()
    for row in operations.values():
        require(row['status'] == 'source-contract-specified' and
                all(row.get(k) for k in ('owner', 'requirements', 'controls', 'semantics', 'acceptance')),
                'Missing/overclaimed operation contract: ' + row['id'])
        require(row['sources'] and set(row['sources']) <= sources.keys(), 'Unbound operation source')
        covered_sources.update(row['sources'])
        require(row['decisions'] and set(row['decisions']) <= decisions.keys(), 'Unbound operation decision')
    for row in decisions.values():
        require(row['status'] in ('retain-source', 'specified-portable-adaptation',
                                 'existing-plan-adaptation', 'named-follow-up') and
                all(row.get(k) for k in ('owner', 'sourceBehavior', 'disposition', 'acceptance', 'authority')),
                'Missing/overclaimed decision disposition')
        require(row['sources'] and set(row['sources']) <= sources.keys(), 'Unbound decision source')
        require(set(row['operations']) == {r['id'] for r in operations.values()
                                          if row['id'] in r['decisions']}, 'Nonreciprocal decision mapping')
    dependencies = unique(review['dependencies'], 'id', 'dependency')
    require(dependencies.keys() == DEPENDENCIES, 'Dependency closure drift')
    for row in dependencies.values():
        require(row['sources'] and set(row['sources']) <= sources.keys() and
                row['operations'] and set(row['operations']) <= operations.keys() and
                row['contract'] and row['owner'], 'Unbound dependency chain')
        covered_sources.update(row['sources'])
    require(all(path in covered_sources or row['reviewKind'] in (
        'project-membership-review', 'manifest-and-existing-consumer-review') or
        path.startswith('core/contributions/train/') or
        row['reviewKind'] == 'reused-P0-P1-P4-dependency-with-pinned-symbol-verification'
        for path, row in sources.items()), 'Orphaned source review')

    actions = read('exav-action-review.json')
    expected = {r['coverageId']: r for r in actions['contributions']
                if r['contributionType'] in STOCK_TYPES or 'P5' in r['phases']}
    contributions = unique(review['contributions'], 'coverageId', 'contribution')
    require(contributions.keys() == expected.keys(), 'Contribution coverage drift')
    for identity, row in contributions.items():
        old = expected[identity]
        require(row['id'] == old['contributionId'] and row['type'] == old['contributionType'] and
                row['handler'] == old['implementationBinding'] and
                row['source'] == old['path'].removeprefix('trunk/') and
                row['sourceSha256'] == sources[row['source']]['sha256'] == old['sourceSha256'] and
                row['operation'] in operations and row['owner'] == operations[row['operation']]['owner'] and
                row['status'] == 'source-bound-runtime-unimplemented', 'Contribution binding/claim drift')
        if texts:
            elements = ET.fromstring(texts[row['source']].split('?>', 1)[-1]).findall('contribution')
            require(any(e.get('id') == row['id'] and e.get('type') == row['type'] for e in elements),
                    'Pinned declaration drift')

    action_section = (ROOT / 'doc/web-parity-actions.md').read_text().split(
        '## P5-01 train/controller workflow bindings', 1)[1]
    manual_section = (ROOT / 'doc/web-parity-manual.md').read_text().split(
        '## P5-01 train/controller source reconciliation', 1)[1]
    for row in operations.values():
        for section in (action_section, manual_section):
            line = next((line for line in section.splitlines() if line.startswith('| `' + row['id'] + '` |')), '')
            require(all(r in line for r in row['requirements']) and row['owner'] in line,
                    'Missing reciprocal workflow requirement: ' + row['id'])

    locale_paths = set(locale['sourcePaths'])
    expected_paths = {p for p in sources if p.endswith('.cs') and (
        p.startswith(('core/contributions/train/', 'core/controllers/rail/Train',
                      'core/views/map/TrainTracking', 'plugins/org.kohsuke.freetrain.tc.',
                      'plugins/org.kohsuke.freetrain.trains.colorTest/')) or
        p in {'core/world/rail/' + name + '.cs' for name in
              ('Train', 'TrainGroup', 'SimpleTrainControllerImpl', 'DelegationTrainControllerImpl')})}
    require(len(locale_paths) == len(locale['sourcePaths']) and locale_paths == expected_paths,
            'Localization source scope drift')
    candidates = {(r['path'].removeprefix('trunk/'), r['line'], r['key'], r['expression']): r
                  for r in actions['translationEntries'] if 'line' in r and
                  r['path'].removeprefix('trunk/') in locale_paths}
    unique(locale['sourceStrings'], 'id', 'text')
    strings = {literal_key(r): r for r in locale['sourceStrings']}
    require(len(strings) == len(locale['sourceStrings']) and strings.keys() == candidates.keys(),
            'Source text scope drift')
    for key, row in strings.items():
        old = candidates[key]
        require(row['sourceSha256'] == sources[row['source']]['sha256'] and
                row['ja'] == old['originalText'] and row['jaSha256'] == old['originalTextSha256'] ==
                hashlib.sha256(row['ja'].encode()).hexdigest() and
                row['englishDonor'] == old.get('englishDonor') and row['sourceContext'] == old['context'],
                'Text/donor provenance drift')
        require(row['status'] == 'scope-reviewed-wording-not-shipped' and
                row['classification'] in ('inactive-source-reference', 'technical-or-format-literal',
                                          'numeric-mask-or-designer-value', 'content-diagnostic',
                                          'presentation-or-dynamic-wording') and
                row['owner'] and row['disposition'] and row['donorStatus'], 'Unclassified/overclaimed text')
        if texts:
            lines = texts[row['source']].splitlines()
            require(0 < row['line'] <= len(lines) and
                    row['expression'] in lines[row['line'] - 1], 'Pinned literal drift')

    content = read('exav-content-review.json')
    catalog_expected = {(r['path'].removeprefix('trunk/'), r['key']): r
                        for r in content['translationEntries']
                        if r['path'].removeprefix('trunk/') in {c['source'] for c in contributions.values()}
                        and ('/contribution:' not in r['key'] or any(
                            'contribution:' + c['id'] + '/' in r['key'] for c in contributions.values()
                            if c['source'] == r['path'].removeprefix('trunk/')))
                        and any(s in r['key'] for s in
                                ('/name', '/company', '/type', '/description', '/author', '/title'))}
    catalog = {(r['source'], r['key']): r for r in locale['catalogStrings']}
    require(len(catalog) == len(locale['catalogStrings']) and catalog.keys() == catalog_expected.keys(),
            'Catalog text scope drift')
    for key, row in catalog.items():
        old = catalog_expected[key]
        require(row['sourceSha256'] == old['sourceSha256'] and row['ja'] == old['originalText'] and
                row['jaSha256'] == old['originalTextSha256'] and row['englishDonor'] == old.get('englishDonor') and
                row['classification'] == 'catalog-metadata' and row['owner'] and
                row['status'] == 'scope-reviewed-wording-not-shipped', 'Catalog provenance/claim drift')
    require({r['id'] for r in locale['meaningDecisions']} == {
        f'TRAIN-WORD-{i:03}' for i in range(1, 8)} and
        all(r['source'] and r['ja'] and r['english'] and r['reason'] for r in locale['meaningDecisions']),
        'Missing wording/meaning disposition')
    require({r['id'] for r in review['followUps']} == {'TRAIN-F001', 'TRAIN-F002', 'TRAIN-F003'} and
            all(r['owner'] and r['status'].startswith('open-') and r['requirement'] for r in review['followUps']),
            'Unnamed/lost follow-up')
    return (len(sources), len(operations), len(decisions), len(dependencies),
            len(contributions), len(strings), len(catalog))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path)
    parser.add_argument('--check', action='store_true', help='Read-only validation (also the default).')
    args = parser.parse_args()
    counts = validate(read('train-review.json'), read('train-localization-review.json'), args.source_root)
    print('P5-01 source scope: %d files, %d operations, %d decisions, %d dependency chains, '
          '%d declarations, %d source literals and %d catalog texts.' % counts)
    print('Source specification only; movement, browser, native and P5 phase acceptance remain open.')


if __name__ == '__main__':
    main()
