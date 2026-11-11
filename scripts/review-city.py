#!/usr/bin/env python3
"""Check P7-01 city source/decision/control/text evidence; no gameplay execution.

Offline mode checks immutable P0 inputs. --source-root additionally checks original
Git blobs and exact excerpts, never a developer's corrected working tree.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
OPERATIONS = {'property', 'land-value', 'fixed-buildings', 'variable-buildings',
              'half-voxel', 'catalog', 'construction', 'ground', 'fences', 'roads',
              'road-extensions', 'providers-feedback', 'development', 'settings',
              'valuation', 'stadium-boundary'}
DEPENDENCIES = {'city-spatial-content', 'time-random-accounting', 'station-city', 'economy-events'}
INPUTS = {f'doc/parity/{n}.json' for n in (
    'exav-action-review', 'exav-content-review', 'exav-geometry-dependency-review',
    'exav-optional-review', 'exav-scenarios', 'exav-integration-review',
    'station-review', 'station-fixtures', 'random-review', 'railway-review', 'forest-review'
)} | {'doc/web-parity-p7-p8-station-handoff.md', 'doc/web-parity-runtime-policy.md'}
POLICIES = dict(save='current-format-only-Save10-protocol9-unchanged',
                semanticCorrections='none-newly-authorized', runtimeAcceptance='none',
                fixtures='P7-02-independent-not-generated-here', approvalGates=[])
FACTORIES = {'GenericStructure', 'GenericLand', 'HalfVoxelStructure', 'roadAccessory',
             'DummyCar', 'commercial', 'varHeightBuilding', 'land', 'road'}


def require(value, message):
    if not value:
        raise ValueError(message)


def read(name):
    return json.loads((ROOT / 'doc/parity' / name).read_text())


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def digest(value):
    return sha(json.dumps(value, ensure_ascii=False, sort_keys=True,
                          separators=(',', ':')).encode())


def index(rows, key, label):
    result = {r[key]: r for r in rows}
    require(len(result) == len(rows), 'Duplicate ' + label)
    return result


def xml_node(e):
    return e.tag, dict(e.attrib), (e.text or '').strip(), [xml_node(c) for c in e]


def recipe_node(n):
    return n['tag'], n['attributes'], n['text'].strip(), [recipe_node(c) for c in n['children']]


def resource_scope(review, actions, content):
    ids = {r['sourceContribution']['coverageId'] for r in review['contributions']}
    paths = {r['path'] for r in review['sourceReviews']}
    code_pictures = {r['target'] for r in content['sourceReferenceEdges']
                     if r['owner'].removeprefix('trunk/') in paths and r['registry'] == 'picture'}
    ids.update(r['coverageId'] for r in actions['contributions'] if r['contributionId'] in code_pictures)
    while True:
        targets = {r['target'] for r in content['pictureReferences'] if r['owner'] in ids}
        expanded = ids | {r['coverageId'] for r in actions['contributions'] if r['contributionId'] in targets}
        if expanded == ids:
            break
        ids = expanded
    return ([dict(kind='manifest', **r) for r in content['manifestFileEdges'] if r['owner'] in ids] +
            [dict(kind='source', **r) for r in content['indirectResourceEdges']
             if r['owner'].removeprefix('trunk/') in paths])


def validate(review, locale, source_root=None):
    baseline = read('exav-baseline.json')
    authority = {k: baseline[k] for k in ('sourceId', 'revision', 'subtree', 'engineVersion', 'pluginFamily')}
    require(review['authority'] == locale['authority'] == authority, 'Authority drift')
    require(review['slice'] == locale['slice'] == 'P7-01' and
            review['status'] == 'source-contract-specified' and
            review['runtimeStatus'] == 'unimplemented' and
            locale['status'] == 'translation-scope-recorded-not-shipped', 'Overclaimed specification')
    for doc in (review, locale):
        require(doc['reviewSha256'] == digest({k: v for k, v in doc.items() if k != 'reviewSha256'}),
                'Review digest drift')
    require(review['policies'] == POLICIES, 'Unapproved semantic/runtime policy')
    require(index(review['inputs'], 'path', 'input').keys() == INPUTS, 'Input scope drift')
    for row in review['inputs']:
        require(sha((ROOT / row['path']).read_bytes()) == row['sha256'], 'Input hash drift: ' + row['path'])
    ledger = {r['baselinePath'].removeprefix('trunk/'): r for r in read('exav-ledger.json')['fileInventory']}
    sources = index(review['sourceReviews'], 'path', 'source')
    require(len(sources) == 193, 'Source denominator drift')
    texts = {}
    for path, row in sources.items():
        original = ledger[path]
        require(row['sha256'] == original['sha256'] and
                row['gitBlob'] == original.get('gitBlob', original.get('blob')), 'Source hash/blob drift')
        require(row['review'] and row['operations'] and row['bindings'] and all(
            b['line'] > 0 and b['symbol'] and b['symbol'] in b['text'] for b in row['bindings']),
            'Missing source review/excerpt')
        if source_root is not None:
            raw = subprocess.check_output(['git', '-C', str(source_root), 'show',
                                           f"{authority['revision']}:trunk/{path}"])
            require(sha(raw) == row['sha256'], 'Pinned source bytes drift: ' + path)
            texts[path] = raw.decode(row['encoding'])
            lines = texts[path].splitlines()
            require(all(b['line'] <= len(lines) and lines[b['line'] - 1] == b['text']
                        for b in row['bindings']), 'Pinned excerpt drift: ' + path)
    operations = index(review['operations'], 'id', 'operation')
    require(operations.keys() == OPERATIONS, 'Operation scope drift')
    decisions = index(review['decisions'], 'id', 'decision')
    require(decisions.keys() == {f'CITY-D{i:03}' for i in range(1, 21)}, 'Decision scope drift')
    for row in operations.values():
        require(all(row.get(k) for k in ('owner', 'requirements', 'controls', 'semantics', 'acceptance', 'decisions'))
                and row['status'] == 'source-contract-specified', 'Incomplete operation contract')
        require(row['sources'] and set(row['sources']) <= sources.keys() and
                set(row['decisions']) <= decisions.keys(), 'Unbound operation')
    for path, row in sources.items():
        require(set(row['operations']) == {o['id'] for o in operations.values() if path in o['sources']},
                'Nonreciprocal source ownership')
    for row in decisions.values():
        require(row['status'] in ('retain-source', 'existing-plan-adaptation') and
                all(row.get(k) for k in ('owner', 'sourceBehavior', 'disposition', 'acceptance', 'authority')),
                'Unowned/unapproved source decision')
        require(row['sources'] and set(row['sources']) <= sources.keys() and
                set(row['operations']) == {o['id'] for o in operations.values() if row['id'] in o['decisions']},
                'Nonreciprocal decision')
        adaptations = {f'CITY-D{i:03}' for i in (4, 7, 8, 10, 13)}
        require((row['status'] == 'existing-plan-adaptation') == (row['id'] in adaptations),
                'Source quirk silently reclassified')
    deps = index(review['dependencies'], 'id', 'dependency')
    require(deps.keys() == DEPENDENCIES and all(r['owner'] and r['contract'] and r['operations'] and
            set(r['operations']) <= OPERATIONS for r in deps.values()), 'Dependency ownership drift')
    actions = read('exav-action-review.json')
    content = read('exav-content-review.json')
    expected = [r for r in actions['contributions'] if 'P7' in r['phases']]
    require([r['sourceContribution'] for r in review['contributions']] == expected,
            'Contribution denominator/provenance drift')
    recipes = {r['coverageId']: r for r in content['recipes']}
    for row in review['contributions']:
        old = row['sourceContribution']
        require(row['operation'] in operations and row['owner'] == operations[row['operation']]['owner']
                and row['disposition'] and row['status'] == 'source-bound-runtime-not-credited',
                'Unowned/overclaimed contribution')
        require(old['path'].removeprefix('trunk/') in sources and
                old['sourceSha256'] == sources[old['path'].removeprefix('trunk/')]['sha256'],
                'Unbound contribution manifest')
        for binding in old['currentTypeBindings']:
            require(all(p.removeprefix('trunk/') in sources for p in binding['sources'] + binding['projects']),
                    'Missing registered implementation dependency')
        recipe = recipes.get(old['coverageId'])
        if recipe:
            e = ET.fromstring(row['declarationXml'])
            require(xml_node(e) == recipe_node(recipe['declaration']), 'Declaration XML drift')
            require(row['metadata'] == {k: e.findtext(k) for k in (
                'name', 'price', 'size', 'height', 'minHeight', 'maxHeight', 'group')} and
                row['computerCannotBuild'] == (e.find('computerCannotBuild') is not None) and
                row['playerCannotBuild'] == (e.find('playerCannotBuild') is not None), 'Definition metadata drift')
        else:
            require(row['declarationXml'] is None and old['generatedFrom'] and
                    old['contributionId'] == 'null-contrib-0:0', 'Generated identity drift')
    workflows = {k for k, v in actions['workflows'].items() if 'P7' in v['phases']}
    require({w for r in operations.values() for w in r['workflows']} == workflows, 'Workflow scope drift')
    require([r['sourceAction'] for r in review['actions']] == [r for r in actions['actions'] if r['workflow'] in workflows],
            'Control/action denominator drift')
    for row in review['actions']:
        require(row['operation'] in operations and row['owner'] == operations[row['operation']]['owner'] and
                row['controlContract'] == operations[row['operation']]['controls'] and row['disposition'],
                'Unbound control')
        require(row['sourceAction']['path'].removeprefix('trunk/') in sources, 'Control source omitted')
    expected_factories = [r for r in content['recipes'] if r['contributionType'] == 'contribution' and
                          any(c['text'] in FACTORIES for c in r['declaration']['children'] if c['tag'] == 'name')]
    require([r['coverageId'] for r in review['factories']] == [r['coverageId'] for r in expected_factories] and
            all(r['declaration'] == old['declaration'] and r['owner'] and
                r['disposition'] == 'factory-not-playable-instance'
                for r, old in zip(review['factories'], expected_factories)), 'Factory support drift')
    code_edges = [r for r in content['sourceReferenceEdges'] if r['owner'].removeprefix('trunk/') in sources]
    require([r['sourceEdge'] for r in review['referenceBindings']] == code_edges,
            'Code registration reference scope drift')
    for row in review['referenceBindings']:
        target = next(r for r in actions['contributions'] if r['contributionId'] == row['sourceEdge']['target'])
        require(row['targetContribution'] == target and row['owner'] == '/'.join(target['phases']) and
                row['disposition'], 'Code registration reference ownership drift')
    edges = resource_scope(review, actions, content)
    require(review['resourceEdges'] == edges, 'Resource edge drift')
    resource_paths = {p.removeprefix('trunk/') for r in edges for p in r['targets']}
    resource_paths.update(p[:-3] + '.resx' for p in sources if p.endswith('.cs') and p[:-3] + '.resx' in ledger)
    resource_paths -= sources.keys()
    resources = index(review['resources'], 'path', 'resource')
    require(resources.keys() == resource_paths, 'Resource denominator drift')
    for path, row in resources.items():
        require(row['sha256'] == ledger[path]['sha256'] and row['owner'] and
                row['disposition'] == 'inert-resource-never-activate-desktop-objects', 'Resource binding drift')
        if source_root is not None:
            raw = subprocess.check_output(['git', '-C', str(source_root), 'show',
                                           f"{authority['revision']}:trunk/{path}"])
            require(sha(raw) == row['sha256'], 'Pinned resource drift: ' + path)
    scenarios = {r['id']: r for r in read('exav-scenarios.json')['scenarios']}
    require({r['id'] for r in review['scenarioBindings']} == {
        f'EXAV-SCN-{i:03}' for i in (17, 18, 19, 20, 21, 22, 23, 24, 30, 31)}, 'Scenario scope drift')
    require(all(r['expectedSha256'] == digest(scenarios[r['id']]['expected']) and r['owner'] and r['disposition']
                for r in review['scenarioBindings']), 'Scenario expectation drift')
    require({r['id'] for r in review['followUps']} == {f'CITY-F{i:03}' for i in range(1, 5)} and
            all(r['owner'] and r['requirement'] for r in review['followUps']), 'Follow-up ownership drift')
    # Exact reciprocal records keep implementing MAN/DEV controls reviewable.
    for file, heading in [('doc/web-parity-city-contract.md', 'Operation and MAN/DEV bindings'),
                          ('doc/web-parity-actions.md', 'P7-01 city workflow bindings'),
                          ('doc/web-parity-manual.md', 'P7-01 city source reconciliation')]:
        section = (ROOT / file).read_text().split('## ' + heading, 1)[1]
        for row in operations.values():
            line = next((l for l in section.splitlines() if l.startswith('| `' + row['id'] + '` |')), '')
            require(line and [q.strip() for q in line.split('|')[2].split(',')] == row['requirements'] and
                    row['owner'] in line, 'Missing reciprocal MAN/DEV/action binding: ' + row['id'])
    require(set(locale['sourcePaths']) == {p for p in sources if p.endswith('.cs')}, 'Text source scope drift')
    expected_text = [r for r in actions['translationEntries'] if 'line' in r and
                     r['path'].removeprefix('trunk/') in sources]
    require([r['sourceText'] for r in locale['sourceStrings']] == expected_text, 'Text/donor provenance drift')
    contribution_ids = {(r['sourceContribution']['path'], r['sourceContribution']['contributionId'])
                        for r in review['contributions']}
    catalog_expected = [r for r in content['translationEntries'] if
                        r['path'].removeprefix('trunk/') in sources and r['path'].endswith('plugin.xml') and
                        ('/contribution:' not in r['key'] or any(
                            p == r['path'] and 'contribution:' + id + '/' in r['key'] for p, id in contribution_ids))]
    require([r['sourceText'] for r in locale['catalogStrings']] == catalog_expected, 'Catalog/donor scope drift')
    for row in locale['sourceStrings'] + locale['catalogStrings']:
        old = row['sourceText']
        require(row['operation'] in operations and row['owner'] == operations[row['operation']]['owner'] and
                row['status'] == 'scope-reviewed-wording-not-shipped' and row['classification'] in (
                    'inactive-source-reference', 'diagnostic', 'presentation-or-dynamic-wording',
                    'technical-or-format-literal', 'attribution-or-link', 'catalog-presentation') and
                row['disposition'], 'Unclassified/overclaimed translation')
        require(sha(old['originalText'].encode()) == old['originalTextSha256'] and
                sources[old['path'].removeprefix('trunk/')]['sha256'] == old['sourceSha256'], 'Text source drift')
        if texts and 'line' in old:
            require(old['expression'] in texts[old['path'].removeprefix('trunk/')].splitlines()[old['line'] - 1],
                    'Pinned literal drift')
    categories = review['categories']
    require(len(categories) == len(index(categories, 'id', 'category')) == 252 and
            locale['categoryNames'] == categories and all(r['name'] and r['owner'] for r in categories),
            'Category metadata/text scope drift')
    if texts:
        p = 'plugins/jp.co.tripod.chiname.lib.generic/CategoryTree.xml'
        tree = ET.fromstring(texts[p].split('?>', 1)[1])
        expected_cats = [dict(id=n.get('id'), name=n.findtext('name'),
                             icon=n.find('icon').attrib if n.find('icon') is not None else None,
                             owner='P7-12/P9') for n in tree.iter('category')]
        require(categories == expected_cats, 'Pinned category metadata drift')
        # Prove the traffic-monitor absence claim against every pinned C# blob,
        # not merely the subset chosen for this review.
        hits = []
        bus_callers = []
        forest_callers = []
        for path in ledger:
            if not path.endswith('.cs'):
                continue
            raw = subprocess.check_output(['git', '-C', str(source_root), 'show',
                                           f"{authority['revision']}:trunk/{path}"])
            for line in raw.decode('cp932', errors='replace').splitlines():
                if 'OnPassengerTransported' in line:
                    hits.append((path, line.strip()))
                if 'new Bus(' in line:
                    bus_callers.append(path)
                if 'new ForestVoxel(' in line:
                    forest_callers.append(path)
        require(len(hits) == 4 and sum('//' not in line for _, line in hits) == 3,
                'Traffic subscriber absence evidence changed')
        require(not bus_callers and not forest_callers, 'Source-only constructor disposition changed')
    require({r['id'] for r in locale['meaningDecisions']} == {f'CITY-T{i:03}' for i in range(1, 6)} and
            all(r['owner'] and r['decision'] for r in locale['meaningDecisions']), 'Meaning decision drift')
    return dict(sources=len(sources), resources=len(resources), operations=len(operations),
                decisions=len(decisions), contributions=len(expected), controls=len(review['actions']),
                factories=len(expected_factories), categories=len(categories),
                sourceStrings=len(expected_text), catalogStrings=len(catalog_expected),
                scenarios=len(review['scenarioBindings']))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true', help='Read-only check (also the default)')
    parser.add_argument('--source-root', type=Path, help='Explicit archive Git repository')
    args = parser.parse_args()
    print('P7-01 city source specification:', json.dumps(validate(
        read('city-review.json'), read('city-localization-review.json'), args.source_root), sort_keys=True))


if __name__ == '__main__':
    main()
