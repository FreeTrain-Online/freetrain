#!/usr/bin/env python3
"""Validate P6-01 station source, decisions, controls, dependencies and text scope.

Ordinary checks use repository P0 evidence. --source-root also reads original
pinned Git objects (including binary resources), never the mutable working tree.
This checker executes no desktop or station gameplay and generates no fixtures.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
OPERATIONS = {'station-building', 'thin-platform', 'wide-platform', 'host-graph',
              'passage-stairs', 'providers', 'listeners', 'timers-logs',
              'passenger-fare', 'yard-controller', 'departure-events', 'information-list'}
DEPENDENCIES = {'spatial-render', 'content-registration', 'provider-lifecycle', 'scheduler-accounting'}
POLICIES = {
    'hostOrdering': 'stable-station-id-portable-tie-break',
    'hostGraph': 'source-child-disconnect-then-validate-detached',
    'listeners': 'retain-creation-order-and-missing-fifth-reverse-registration',
    'arithmetic': 'TRAIN-D008-checked-original-width-float32-order',
    'harborKeys': 'TRAIN-D012-station-location',
    'stationConstruction': 'immediate-no-ConstructionSite',
    'platformTimer': 'relative-24-hours',
    'bell': 'post-commit-epoch-sequence-ordinal-live-only',
    'stairPreview': 'pure-no-cast-or-write',
    'invalidCrop': 'unresolved-P6-15-no-guessed-correction',
    'manual': 'owner-removed',
    'save': 'current-format-only-Save9-protocol7-unchanged',
}
INPUTS = {f'doc/parity/{n}.json' for n in (
    'exav-action-review', 'exav-content-review', 'exav-geometry-dependency-review',
    'exav-geometry-gallery-review', 'exav-scenarios', 'train-review', 'train-fixtures',
    'railway-review', 'clock-review')} | {
        'doc/web-parity-p6-train-handoff.md', 'doc/web-parity-runtime-policy.md'}
PICTURES = {'{3FF9F902-6B2A-44A4-9C5F-DE8D82CFD37D}',
            '{3197A63A-89DC-4237-8C9B-045F41F31CDB}',
            '{3197A63A-89DC-4237-8C9B-045F41F31CDB}-brown',
            '{3197A63A-89DC-4237-8C9B-045F41F31CDB}-green'}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def read(name):
    return json.loads((ROOT / 'doc/parity' / name).read_text())


def digest(value):
    return hashlib.sha256(json.dumps(value, ensure_ascii=False, sort_keys=True,
                                     separators=(',', ':')).encode()).hexdigest()


def unique(rows, key, label):
    values = {r[key]: r for r in rows}
    require(len(values) == len(rows), 'Duplicate ' + label)
    return values


def xml_node(e):
    return (e.tag, dict(e.attrib), (e.text or '').strip(), [xml_node(c) for c in e])


def recipe_node(n):
    return (n['tag'], n['attributes'], n['text'].strip(), [recipe_node(c) for c in n['children']])


def descendants(n, tag):
    for child in n['children']:
        if child['tag'] == tag:
            yield child
        yield from descendants(child, tag)


def resource_scope(review, actions, content):
    ids = {r['coverageId'] for r in review['contributions']}
    ids.update(r['coverageId'] for r in actions['contributions'] if r['contributionId'] in PICTURES)
    while True:
        targets = {r['target'] for r in content['pictureReferences'] if r['owner'] in ids}
        expanded = ids | {r['coverageId'] for r in actions['contributions'] if r['contributionId'] in targets}
        if expanded == ids:
            break
        ids = expanded
    sources = {r['path'] for r in review['sourceReviews']}
    return ([dict(kind='manifest', **r) for r in content['manifestFileEdges'] if r['owner'] in ids] +
            [dict(kind='source', **r) for r in content['indirectResourceEdges']
             if r['owner'].removeprefix('trunk/') in sources])


def validate(review, locale, source_root=None):
    authority = {k: read('exav-baseline.json')[k] for k in
                 ('sourceId', 'revision', 'subtree', 'engineVersion', 'pluginFamily')}
    require(review['authority'] == locale['authority'] == authority, 'Unreviewed authority')
    require(review['slice'] == locale['slice'] == 'P6-01' and
            review['status'] == 'source-contract-specified' and
            review['runtimeStatus'] == 'unimplemented' and
            locale['status'] == 'translation-scope-recorded-not-shipped', 'Overclaimed specification')
    for doc in (review, locale):
        require(doc['reviewSha256'] == digest({k: v for k, v in doc.items() if k != 'reviewSha256'}),
                'Review digest drift')
    require(review['policies'] == POLICIES and review['approvalGates'] == [], 'Portable policy drift')
    require(set(unique(review['inputs'], 'path', 'input')) == INPUTS, 'Missing prerequisite evidence')
    for r in review['inputs']:
        require(hashlib.sha256((ROOT / r['path']).read_bytes()).hexdigest() == r['sha256'],
                'Input evidence drift: ' + r['path'])
    sources = unique(review['sourceReviews'], 'path', 'source')
    ledger = {r['baselinePath'].removeprefix('trunk/'): r['sha256']
              for r in read('exav-ledger.json')['fileInventory']}
    texts = {}
    for p, r in sources.items():
        require(r['sha256'] == ledger.get(p), 'Source hash drift: ' + p)
        require(r['review'] and r['bindings'] and all(b['line'] > 0 and b['symbol'] in b['text']
                for b in r['bindings']), 'Missing source binding: ' + p)
        if source_root is not None:
            raw = subprocess.check_output(['git', '-C', str(source_root), 'show',
                                           f"{authority['revision']}:{authority['subtree']}/{p}"])
            require(hashlib.sha256(raw).hexdigest() == r['sha256'], 'Pinned blob drift: ' + p)
            texts[p] = raw.decode(r['encoding'])
            lines = texts[p].splitlines()
            require(all(b['line'] <= len(lines) and lines[b['line'] - 1] == b['text']
                        for b in r['bindings']), 'Pinned excerpt drift: ' + p)
    operations = unique(review['operations'], 'id', 'operation')
    require(operations.keys() == OPERATIONS, 'Operation coverage drift')
    decisions = unique(review['decisions'], 'id', 'decision')
    require(decisions.keys() == {f'STATION-D{i:03}' for i in range(1, 15)}, 'Decision coverage drift')
    covered = set()
    for r in operations.values():
        require(r['status'] == 'source-contract-specified' and
                all(r.get(k) for k in ('owner', 'requirements', 'controls', 'semantics', 'acceptance', 'decisions')),
                'Missing operation contract')
        require(r['sources'] and set(r['sources']) <= sources.keys(), 'Unbound operation source')
        require(set(r['decisions']) <= decisions.keys(), 'Unbound operation decision')
        covered.update(r['sources'])
    for r in decisions.values():
        require(r['status'] in ('retain-source', 'specified-portable-adaptation',
                               'existing-plan-adaptation', 'named-follow-up') and
                all(r.get(k) for k in ('owner', 'sourceBehavior', 'disposition', 'acceptance')),
                'Missing decision disposition')
        require(r['sources'] and set(r['sources']) <= sources.keys(), 'Unbound decision source')
        require(set(r['operations']) == {o['id'] for o in operations.values() if r['id'] in o['decisions']},
                'Nonreciprocal decision')
    deps = unique(review['dependencies'], 'id', 'dependency')
    require(deps.keys() == DEPENDENCIES, 'Dependency closure drift')
    for r in deps.values():
        require(r['owner'] and r['contract'] and r['operations'] and
                set(r['operations']) <= operations.keys() and r['sources'] and
                set(r['sources']) <= sources.keys(), 'Unbound dependency')
        covered.update(r['sources'])
    actions = read('exav-action-review.json')
    content = read('exav-content-review.json')
    expected = {r['coverageId']: r for r in actions['contributions'] if 'P6' in r['phases']}
    contributions = unique(review['contributions'], 'coverageId', 'contribution')
    require(contributions.keys() == expected.keys(), 'Contribution coverage drift')
    recipes = {r['coverageId']: r for r in content['recipes']}
    for identity, r in contributions.items():
        old = expected[identity]
        p = old['path'].removeprefix('trunk/')
        require(r['id'] == old['contributionId'] and r['type'] == old['contributionType'] and
                r['source'] == p and r['sourceSha256'] == sources[p]['sha256'] == old['sourceSha256'] and
                r['handler'] == old['implementationBinding'] and r['operation'] in operations and r['owner'],
                'Contribution source binding drift')
        expected_disposition = {'bgm': 'owner-deferred-BGM', 'railStationary': 'P4-decorative-not-service-host'}
        require(r['disposition'] == expected_disposition.get(r['type'], 'P6-runtime-unimplemented'),
                'Contribution disposition drift')
        e = ET.fromstring(r['declarationXml'])
        require(xml_node(e) == recipe_node(recipes[identity]['declaration']) and
                r['fields'] == {k: e.findtext(k) for k in ('name', 'price', 'size', 'operationCost')} and
                r['classReferences'] == [dict(x.attrib) for x in e.iter('class')], 'Declaration metadata drift')
        if texts:
            raw_root = ET.fromstring(texts[p].split('?>', 1)[-1])
            require(any(xml_node(x) == xml_node(e) for x in raw_root.findall('contribution')), 'Pinned declaration drift')
        covered.add(p)
    provider_expected = {}
    for recipe in content['recipes']:
        old = next(r for r in actions['contributions'] if r['coverageId'] == recipe['coverageId'])
        for i, node in enumerate(descendants(recipe['declaration'], 'population')):
            provider_expected[(old['path'].removeprefix('trunk/'), recipe['contributionId'], i)] = recipe_node(node)
    provider_keys = [(r['source'], r['contributionId'], r['index']) for r in review['providerBindings']]
    require(len(set(provider_keys)) == len(provider_keys) and set(provider_keys) == provider_expected.keys(),
            'Provider binding coverage drift')
    for r in review['providerBindings']:
        key = r['source'], r['contributionId'], r['index']
        require(r['sourceSha256'] == sources[r['source']]['sha256'] and r['owner'] == 'P6-16/P7' and
                r['status'] == 'source-bound-city-runtime-not-credited' and
                xml_node(ET.fromstring(r['populationXml'])) == provider_expected[key], 'Provider source drift')
        if texts:
            e = next(e for e in ET.fromstring(texts[r['source']].split('?>', 1)[-1]).findall('contribution')
                     if e.get('id') == r['contributionId'])
            require(xml_node(e.findall('.//population')[r['index']]) == provider_expected[key], 'Pinned provider drift')
        covered.add(r['source'])
    require(covered == sources.keys(), 'Orphaned source review')
    # Required negative dependency: generic construction is NOT on station create path.
    require('core/contributions/rail/StationContribution.cs' in sources and
            'core/world/structs/ConstructionSite.cs' not in operations['station-building']['sources'],
            'Station immediate construction drift')
    edges = resource_scope(review, actions, content)
    require(review['resourceEdges'] == edges, 'Resource edge drift')
    resource_paths = {t.removeprefix('trunk/') for r in edges for t in r['targets']}
    resource_paths.update(p[:-3] + '.resx' for p in sources if p.endswith('.cs') and p[:-3] + '.resx' in ledger)
    resources = unique(review['resources'], 'path', 'resource')
    require(resources.keys() == resource_paths, 'Resource coverage drift')
    for p, r in resources.items():
        require(r['sha256'] == ledger.get(p) and r['owner'], 'Resource hash drift')
        if source_root is not None:
            raw = subprocess.check_output(['git', '-C', str(source_root), 'show',
                                           f"{authority['revision']}:{authority['subtree']}/{p}"])
            require(hashlib.sha256(raw).hexdigest() == r['sha256'], 'Pinned resource drift')
    tables = {(r['source'], r['name']): r for r in review['populationTables']}
    require(len(tables) == len(review['populationTables']) == 42 and
            len({p for p, name in tables}) == 10 and
            all(len(r['values']) == 24 and all(type(x) is int for x in r['values']) and r['owner']
                and r['source'] in sources for r in tables.values()), 'Population table scope drift')
    for r in tables.values():
        expression = r['sourceExpression']
        numbers = re.findall(r'-?\d+', re.sub(r'//[^\n]*', '', expression.split('{', 1)[1]))
        require([int(x) for x in numbers] == r['values'] and r['name'] in expression,
                'Population table value drift')
        if texts:
            require(expression in texts[r['source']] and
                    texts[r['source']].splitlines()[r['line'] - 1].strip() == expression.splitlines()[0],
                    'Pinned population table excerpt drift')
    if texts:
        raw_tables = {}
        for p, t in texts.items():
            if not p.startswith('core/contributions/population/'):
                continue
            for m in re.finditer(r'(?:private|public)\s+static\s+readonly\s+int\[\]\s+(\w+)\s*=\s*new int\[\]\s*\{(.*?)\};', t, re.S):
                raw_tables[p, m.group(1)] = [int(x) for x in re.findall(r'-?\d+', re.sub(r'//[^\n]*', '', m.group(2)))]
        require({k: r['values'] for k, r in tables.items()} == raw_tables, 'Pinned population table drift')
    ui_paths = {p for p in sources if p.startswith('core/controllers/rail/') or '/stationlist/' in p}
    expected_actions = [r for r in actions['actions'] if r['path'].removeprefix('trunk/') in ui_paths]
    require([r['sourceAction'] for r in review['actions']] == expected_actions and
            all(r['operation'] in operations and r['source'] == r['sourceAction']['path'].removeprefix('trunk/')
                for r in review['actions']), 'Control/action inventory drift')
    for file, heading in [('doc/web-parity-actions.md', 'P6-01 station workflow bindings'),
                          ('doc/web-parity-manual.md', 'P6-01 station source reconciliation'),
                          ('doc/web-parity-station-contract.md', 'Operation and MAN/DEV bindings')]:
        section = (ROOT / file).read_text().split('## ' + heading, 1)[1]
        for r in operations.values():
            line = next((x for x in section.splitlines() if x.startswith('| `' + r['id'] + '` |')), '')
            require(line and [q.strip() for q in line.split('|')[2].split(',')] == r['requirements']
                    and r['owner'] in line,
                    'Missing reciprocal MAN/DEV/action link: ' + r['id'])
    accept = unique(review['acceptanceMatrix'], 'id', 'acceptance')
    require(len(accept) == 26 and {r['operation'] for r in accept.values()} == OPERATIONS and
            {r.get('decision') for r in accept.values() if r.get('decision')} == decisions.keys(),
            'Acceptance matrix scope drift')
    for r in accept.values():
        require(r['status'] == 'specified-not-executed' and r['fixtureOwner'] == 'P6-02' and
                r['input'] and r['expected'] and r['runtimeOwner'] and r['owner'] and r['sourceBindings'] and
                set(r['sourceBindings']) <= sources.keys(), 'Overclaimed/unbound acceptance')
    scenarios = {r['id']: r for r in read('exav-scenarios.json')['scenarios']
                 if 8 <= int(r['id'][-3:]) <= 20 or r['id'].endswith(('030', '031'))}
    require(set(unique(review['scenarioBindings'], 'id', 'scenario')) == scenarios.keys(), 'Scenario scope drift')
    for r in review['scenarioBindings']:
        require(r['expectedSha256'] == digest(scenarios[r['id']]['expected']) and r['owner'] and r['disposition'],
                'Scenario expectation drift')
    locale_paths = {p for p in sources if p.endswith('.cs') and (
        p in ui_paths or p.startswith('core/contributions/population/') or p.startswith('core/world/rail/') or
        p == 'core/contributions/rail/DepartureBellContribution.cs')}
    require(len(set(locale['sourcePaths'])) == len(locale['sourcePaths']) and
            set(locale['sourcePaths']) == locale_paths, 'Localization source scope drift')
    expected_texts = {(r['path'].removeprefix('trunk/'), r['line'], r['key'], r['expression']): r
                      for r in actions['translationEntries'] if 'line' in r and
                      r['path'].removeprefix('trunk/') in locale_paths}
    strings = {(r['source'], r['line'], r['key'], r['expression']): r for r in locale['sourceStrings']}
    require(len(strings) == len(locale['sourceStrings']) and strings.keys() == expected_texts.keys(),
            'Source text scope drift')
    for key, r in strings.items():
        old = expected_texts[key]
        require(r['sourceSha256'] == sources[r['source']]['sha256'] == old['sourceSha256'] and
                r['ja'] == old['originalText'] and r['jaSha256'] == old['originalTextSha256'] ==
                hashlib.sha256(r['ja'].encode()).hexdigest() and r['englishDonor'] == old.get('englishDonor') and
                r['sourceContext'] == old['context'], 'Text/donor provenance drift')
        require(r['status'] == 'scope-reviewed-wording-not-shipped' and r['owner'] and
                r['disposition'] and r['donorStatus'] and r['classification'] in (
                    'inactive-source-reference', 'presentation-or-dynamic-wording', 'technical-or-format-literal'),
                'Overclaimed/unclassified text')
        if texts:
            require(r['expression'] in texts[r['source']].splitlines()[r['line'] - 1], 'Pinned literal drift')
    catalog_expected = {(r['path'].removeprefix('trunk/'), r['key']): r for r in content['translationEntries']
                        if r['path'].removeprefix('trunk/') in sources and r['path'].endswith('plugin.xml') and
                        ('/contribution:' not in r['key'] or any('contribution:' + x['id'] + '/' in r['key']
                         for x in contributions.values() if x['source'] == r['path'].removeprefix('trunk/'))) and
                        any(s in r['key'] for s in ('/name', '/company', '/type', '/description', '/author', '/title'))}
    catalog = {(r['source'], r['key']): r for r in locale['catalogStrings']}
    require(len(catalog) == len(locale['catalogStrings']) and catalog.keys() == catalog_expected.keys(),
            'Catalog text scope drift')
    for key, r in catalog.items():
        old = catalog_expected[key]
        require(r['ja'] == old['originalText'] and r['jaSha256'] == old['originalTextSha256'] and
                r['sourceSha256'] == sources[r['source']]['sha256'] and r['englishDonor'] == old.get('englishDonor')
                and r['status'] == 'scope-reviewed-wording-not-shipped' and r['owner'] and r['disposition'],
                'Catalog provenance drift')
    require({r['id'] for r in review['followUps']} == {f'STATION-F{i:03}' for i in range(1, 4)} and
            all(r['owner'] and r['requirement'] for r in review['followUps']), 'Unnamed/lost follow-up')
    return dict(sources=len(sources), resources=len(resources), operations=len(operations), decisions=len(decisions),
                contributions=len(contributions), controls=len(review['actions']), acceptance=len(accept),
                tables=len(tables), providerBindings=len(provider_keys), sourceStrings=len(strings), catalogStrings=len(catalog))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true', help='Read-only check (also default)')
    parser.add_argument('--source-root', type=Path, help='Explicit archive Git repository')
    args = parser.parse_args()
    print('P6-01 station source specification:', json.dumps(validate(
        read('station-review.json'), read('station-localization-review.json'), args.source_root), sort_keys=True))


if __name__ == '__main__':
    main()
