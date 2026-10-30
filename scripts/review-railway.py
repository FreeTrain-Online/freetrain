#!/usr/bin/env python3
"""Validate P4-01 source/decision/translation coverage; no railway algorithms.

Default checks need only checked-in evidence. --source-root reads raw pinned Git
blobs from an explicitly supplied archive, never its mutable working tree.
Independent executable railway expectations belong to P4-02.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
PARITY = ROOT / 'doc/parity'
OPERATIONS = {
    'normal-route', 'attachment-detachment', 'slope', 'piers', 'bridge', 'girder',
    'tunnel', 'garage', 'signal', 'pole', 'stationary-accessory',
    'speed-limit-flat', 'speed-limit-slope', 'crossing',
}
CONTRIBUTION_TYPES = {'specialRail', 'electricPole', 'railSignal', 'railStationary'}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def read(name):
    return json.loads((PARITY / name).read_text())


def digest(value):
    raw = json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(',', ':')).encode()
    return hashlib.sha256(raw).hexdigest()


def check_digest(row, key, label):
    require(row[key] == digest({k: v for k, v in row.items() if k != key}),
            'Review digest drift: ' + label)


def unique(rows, key, label):
    result = {r[key]: r for r in rows}
    require(len(result) == len(rows), 'Duplicate ' + label)
    return result


def validate(review, locale, source_root=None):
    baseline = read('exav-baseline.json')
    authority = review['authority']
    require(authority['revision'] == baseline['revision'] and
            authority['subtree'] == baseline['subtree'] and
            authority['sourceId'] == 'exav-883cf7d-trunk' and
            authority['engineVersion'] == '2.1.1.12' and authority['pluginFamily'] == 'V2',
            'Unreviewed railway authority')
    require(review['slice'] == locale['slice'] == 'P4-01' and
            review['status'] == 'source-contract-specified' and
            locale['status'] == 'translation-scope-recorded-not-shipped' and
            locale['authority'] == authority, 'Overclaimed or mismatched review status')
    check_digest(review, 'reviewSha256', 'railway')
    check_digest(locale, 'reviewSha256', 'localization')
    known = {r['baselinePath']: r['sha256'] for r in read('exav-ledger.json')['fileInventory']}
    sources = unique(review['sourceReviews'], 'path', 'source')
    texts = {}
    for path, row in sources.items():
        require(known.get('trunk/' + path) == row['sha256'], 'Source hash drift: ' + path)
        require(row['review'] and row['bindings'], 'Missing semantic source review: ' + path)
        for binding in row['bindings']:
            require(binding['line'] > 0 and binding['symbol'] in binding['text'],
                    'Invalid source binding: ' + path)
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
                        'Pinned line/excerpt drift: ' + path)
    operations = unique(review['operations'], 'id', 'operation')
    require(operations.keys() == OPERATIONS, 'Missing or unknown railway operation')
    decisions = unique(review['decisions'], 'id', 'decision')
    require(decisions.keys() == {f'RAIL-D{i:03}' for i in range(1, 15)}, 'Missing decision')
    for identity, row in operations.items():
        require(row['status'] == 'source-contract-specified' and row['owner'].startswith('P4-'),
                'Overclaimed operation: ' + identity)
        require(all(row.get(k) for k in ('legality', 'mutation', 'booking', 'removal')),
                'Missing operation contract: ' + identity)
        require(row['sources'] and set(row['sources']) <= sources.keys(),
                'Unreviewed operation source: ' + identity)
        require(row['decisions'] and set(row['decisions']) <= decisions.keys(),
                'Unknown operation decision: ' + identity)
    for identity, row in decisions.items():
        check_digest(row, 'sha256', identity)
        require(row['status'] in ('retain-source', 'existing-plan-adaptation', 'approval-required')
                and row['authorization'] and row['owner'] and row['disposition'],
                'Unclassified decision: ' + identity)
        require(row['sources'] and set(row['sources']) <= sources.keys(),
                'Unreviewed decision source: ' + identity)
        expected = {r['id'] for r in operations.values() if identity in r['decisions']}
        require(set(row['operations']) == expected, 'Nonreciprocal decision: ' + identity)
    gates = {r['id'] for r in decisions.values() if r['status'] == 'approval-required'}
    require(set(review['approvalGates']) == gates == set(), 'Hidden/new approval gate')
    pier_decision = decisions['RAIL-D006']
    require(pier_decision['status'] == 'retain-source' and
            pier_decision.get('ownerDecision', {}).get('choice') == 'retain-source-owner-agnostic-teardown' and
            pier_decision['ownerDecision'].get('decidedOn') == '2026-10-30' and
            pier_decision['ownerDecision'].get('reason'), 'Owner pier-teardown decision drift')
    actions = read('exav-action-review.json')
    expected = {r['coverageId']: r for r in actions['contributions']
                if r['contributionType'] in CONTRIBUTION_TYPES}
    contributions = unique(review['contributions'], 'coverageId', 'contribution')
    require(contributions.keys() == expected.keys(), 'Bundled railway contribution coverage drift')
    for identity, row in contributions.items():
        donor = expected[identity]
        require(row['source'] in sources and row['id'] == donor['contributionId'] and
                row['type'] == donor['contributionType'] and
                row['source'] == donor['path'].removeprefix('trunk/') and
                row['handler'] == donor['implementationBinding'] and
                row['status'] == 'source-bound-runtime-unimplemented',
                'Contribution binding/claim drift: ' + identity)
        if texts:
            elements = ET.fromstring(texts[row['source']]).findall('contribution')
            element = next(e for e in elements if e.get('id') == row['id'])
            require(element.get('type') == row['type'] and
                    all(element.findtext(k) == row[field] for k, field in
                        [('name', 'nameJa'), ('price', 'price'), ('size', 'size'), ('height', 'height')]),
                    'Manifest metadata drift: ' + identity)
    action_doc = (ROOT / 'doc/web-parity-actions.md').read_text().split(
        '## P4-01 railway workflow bindings', 1)[1]
    manual_doc = (ROOT / 'doc/web-parity-manual.md').read_text().split(
        '## P4-01 railway source reconciliation', 1)[1]
    covered = set()
    for mapping in review['workflowMappings']:
        require(set(mapping['operations']) <= operations.keys() and mapping['operations'],
                'Invalid workflow operation mapping')
        require(all(identity in action_doc and identity in manual_doc
                    for identity in mapping['requirements']), 'Missing reciprocal requirement link')
        covered.update(mapping['operations'])
    require(covered == OPERATIONS, 'Unmapped railway operation')
    source_candidates = {(r['path'].removeprefix('trunk/'), r['line'], r['expression']): r
                         for r in actions['translationEntries'] if 'line' in r and 'expression' in r}
    strings = unique(locale['sourceStrings'], 'id', 'text')
    for row in strings.values():
        path = row['source']
        candidate = source_candidates.get((path, row['line'], row['expression']))
        require(path in sources and row['sourceSha256'] == sources[path]['sha256'] and candidate,
                'Unreviewed source text')
        require(row['ja'] == candidate['originalText'] and
                row['jaSha256'] == hashlib.sha256(row['ja'].encode()).hexdigest() and
                row['englishDonor'] == candidate['englishDonor'], 'Text/donor provenance drift')
        require(row['status'] == locale['status'] and row['disposition'] and row['classification'] in
                ('historical-donor-requires-feature-wording-review', 'new-translation-required'),
                'Overclaimed/unclassified translation')
        if texts:
            require(row['expression'] in texts[path].splitlines()[row['line'] - 1],
                    'Text line drift: ' + row['id'])
    # All Japanese candidates in these concrete UI/handler surfaces must remain
    # represented. Shared world/train/economic text is assigned to its own phases.
    locale_paths = {
        p for p in sources if p.startswith(('core/controllers/',
                                           'core/contributions/common/FixedSizeStructureController'))
        or p in {'core/world/rail/BridgeRail.cs', 'core/world/rail/TunnelRail.cs',
                 'core/world/rail/StealSupportedRailContributionImpl.cs'}
        or p.startswith(('plugins/SpeedLimit/', 'plugins/org.kohsuke.freetrain.rail.'))
    }
    expected_texts = {(p, line, expr) for (p, line, expr), r in source_candidates.items()
                      if p in locale_paths and r['classification'] == 'Japanese-text-candidate'}
    actual_texts = {(r['source'], r['line'], r['expression']) for r in strings.values()}
    require(expected_texts == actual_texts, 'Missing/extra P4 source text disposition')
    return len(sources), len(operations), len(decisions), len(contributions), len(strings)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true', help='Read-only check (also default)')
    parser.add_argument('--source-root', type=Path, help='Explicit archive repository for pinned Git verification')
    args = parser.parse_args()
    counts = validate(read('railway-review.json'), read('railway-localization-review.json'), args.source_root)
    print('P4-01 review passed: %d pinned files, %d operations, %d decisions, '
          '%d bundled definitions, %d source texts. P4-02 fixtures/runtime remain open; '
          'GEO-004 retains owner-confirmed teardown regardless of pier owner; no pending approval gates.' % counts)


if __name__ == '__main__':
    main()
