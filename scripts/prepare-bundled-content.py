#!/usr/bin/env python3
"""Prepare reviewed stable bundled content; default builds need no Git/archive/desktop tree.

--source-root explicitly refreshes checked-in inputs from pinned Git blobs. It
never reads dirty archive working files or discovers an optional/sibling pack.
"""
import argparse
import hashlib
import html
import importlib.util
import json
from pathlib import Path, PurePosixPath
import re
import sys
import xml.etree.ElementTree as ET

from content_bitmap import decode, surface, png

ROOT = Path(__file__).resolve().parents[1]
INPUT = ROOT / 'content/bundled-v2'
OUTPUT = ROOT / 'web/public/content/bundled-v2'
CATALOG = ROOT / 'src/FreeTrain.Online.Content/bundled-v2.json'
PIN = '883cf7d81cf49db25ff38cd3a25d0f140afe31a0'
PACK = 'exav-stable-bundled'
URL = '/content/bundled-v2/'


def digest(data):
    return hashlib.sha256(data).hexdigest()


def dump(value):
    return (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode('utf-8')


def safe_path(path):
    if (not isinstance(path, str) or not path or '\\' in path or ':' in path
            or path.startswith('/') or any(p in ('', '.', '..') for p in path.split('/'))):
        raise ValueError('Unsafe content path: ' + str(path))
    return path


def checked_read(root, path):
    path = safe_path(path)
    target = root / path
    if not target.resolve().is_relative_to(root.resolve()) or target.is_symlink():
        raise ValueError('Content path escapes input root: ' + path)
    return target.read_bytes()


def xml_tree(node):
    return dict(tag=node.tag, attributes=dict(node.attrib), text=(node.text or '').strip(),
                children=[xml_tree(c) for c in node])


def children(node, tag):
    return [c for c in node['children'] if c['tag'] == tag]


def unique(rows, key, label):
    result = {}
    for row in rows:
        identity = row[key]
        if identity in result:
            raise ValueError('Duplicate ' + label + ': ' + identity)
        result[identity] = row
    return result


def plugin_order(plugins):
    by_id = unique(plugins, 'id', 'plugin')
    order, visiting, done = [], set(), set()
    def visit(identity):
        if identity in visiting:
            raise ValueError('Cyclic plugin dependency: ' + identity)
        if identity not in by_id:
            raise ValueError('Missing plugin dependency: ' + identity)
        if identity in done:
            return
        visiting.add(identity)
        for dependency in sorted(by_id[identity]['dependencies']):
            visit(dependency)
        visiting.remove(identity)
        done.add(identity)
        order.append(identity)
    for identity in sorted(by_id):
        visit(identity)
    return order


def refresh(source_root, check=False):
    # Reuse the bounded, source-reviewed XML parser only during explicit refresh.
    spec = importlib.util.spec_from_file_location('inventory', ROOT / 'scripts/inventory-parity.py')
    inventory = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(inventory)
    baseline = json.loads((ROOT / 'doc/parity/exav-baseline.json').read_text())
    review_bytes = (ROOT / 'doc/parity/exav-content-review.json').read_bytes()
    ledger_bytes = (ROOT / 'doc/parity/exav-ledger.json').read_bytes()
    review, ledger = json.loads(review_bytes), json.loads(ledger_bytes)
    if baseline['revision'] != PIN or review['source']['revision'] != PIN or ledger['source']['revision'] != PIN:
        raise ValueError('Bundled source pin differs from reviewed stable baseline')
    tree = inventory.Tree(source_root, PIN, 'trunk')
    files = {r['baselinePath']: r for r in ledger['fileInventory']}
    paths = ({r['baselinePath'] for r in review['manifests'] + review['assets']}
             | {r['path'] for r in review['referencedFiles'] + review['notices']})
    payload = {}
    records = []
    for path in sorted(paths):
        safe_path(path)
        raw = tree.read(path.removeprefix('trunk/'))
        if digest(raw) != files[path]['sha256']:
            raise ValueError('Reviewed source hash mismatch: ' + path)
        data, normalization = raw, 'none'
        if path.endswith('/plugin.xml'):
            text = raw.decode('cp932' if re.search(br'encoding=[\'"](?:Shift_JIS|CP932)', raw, re.I) else 'utf-8-sig')
            text = re.sub(r'(<\?xml[^>]*encoding=)[\'"][^\'"]+[\'"]', r'\1"utf-8"', text, count=1)
            data, normalization = text.encode('utf-8'), 'EXAV-ENC-001'
            parsed = inventory.manifest(data)[0]
            expected = [r['declaration'] for r in review['recipes']
                        if r['coverageId'] in next(m['declarations'] for m in review['manifests'] if m['baselinePath'] == path)]
            if [xml_tree(n) for n in parsed.findall('contribution')] != expected:
                raise ValueError('Normalized manifest differs from reviewed declarations: ' + path)
            # Normal builds parse expanded inert XML, never DTDs/entities/codebases.
            payload['expanded/' + path] = ET.tostring(parsed, encoding='utf-8', xml_declaration=True) + b'\n'
        payload['source/' + path] = data
        records.append(dict(path=path, sourceSha256=digest(raw), sha256=digest(data), bytes=len(data),
                            normalization=normalization,
                            expandedSha256=digest(payload['expanded/' + path]) if path.endswith('/plugin.xml') else None))
    selected = dict(source=review['source'], sourceId=review['sourceId'],
                    reviewSha256=digest(review_bytes), ledgerSha256=digest(ledger_bytes),
                    plugins=[dict(id=p['pluginId'], path=p['baselinePath'], dependencies=p['dependencies'],
                                  scope=p['scope'], title=p['title'], author=p['author']) for p in ledger['plugins']],
                    recipes=review['recipes'], generatedChildren=review['generatedChildren'], assets=review['assets'],
                    pictures=review['pictureRegistrations'], aliases=review['aliases'], notices=review['notices'],
                    resourceDiagnostics=review['resourceDiagnostics'], embeddedResources=review['embeddedResources'],
                    translations=review['translationEntries'])
    review_data = dump(selected)
    index = dict(schemaVersion=1, packId=PACK, version='1', source=review['source'],
                 reviewSha256=digest(review_data), files=records,
                 adaptations=['EXAV-ENC-001: UTF-8 manifests with identical expanded declarations',
                              'EXAV-DIR-001: SOUTH is the portable parser token; source metadata remains literal'])
    payload['review.json'], payload['source-index.json'] = review_data, dump(index)
    expected_paths = set(payload)
    actual_paths = {p.relative_to(INPUT).as_posix() for p in INPUT.rglob('*') if p.is_file()}
    # Validate everything before publishing; --check is fully read-only.
    if check:
        if actual_paths != expected_paths:
            raise ValueError('Pinned source input set drift')
        for path, data in payload.items():
            if checked_read(INPUT, path) != data:
                raise ValueError('Pinned source input drift: ' + path)
    else:
        for path, data in payload.items():
            target = INPUT / path
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
        for path in actual_paths - expected_paths:
            (INPUT / path).unlink()


def load_inputs(root=INPUT):
    index_data = checked_read(root, 'source-index.json')
    index = json.loads(index_data)
    if (index['schemaVersion'] != 1 or index['packId'] != PACK or index['version'] != '1'
            or index['source']['revision'] != PIN or index['source']['subtree'] != 'trunk'):
        raise ValueError('Unsupported prepared bundled source identity')
    review_data = checked_read(root, 'review.json')
    if digest(review_data) != index['reviewSha256']:
        raise ValueError('Prepared source review drift')
    review = json.loads(review_data)
    files, expected = {}, {'source-index.json', 'review.json'}
    unique(index['files'], 'path', 'source path')
    for record in index['files']:
        path = safe_path(record['path'])
        raw = checked_read(root, 'source/' + path)
        if len(raw) != record['bytes'] or digest(raw) != record['sha256']:
            raise ValueError('Prepared source drift: ' + path)
        files[path] = raw
        expected.add('source/' + path)
        if record['expandedSha256'] is not None:
            expanded = checked_read(root, 'expanded/' + path)
            if digest(expanded) != record['expandedSha256'] or b'<!DOCTYPE' in expanded or b'<!ENTITY' in expanded:
                raise ValueError('Expanded manifest drift: ' + path)
            if [xml_tree(n) for n in ET.fromstring(expanded).findall('contribution')] != [
                    r['declaration'] for r in review['recipes'] if r['pluginId'] == next(
                        p['id'] for p in review['plugins'] if p['path'] == path)]:
                raise ValueError('Prepared declaration drift: ' + path)
            expected.add('expanded/' + path)
    actual = {p.relative_to(root).as_posix() for p in root.rglob('*') if p.is_file()}
    if actual != expected:
        raise ValueError('Unexpected/missing bundled source inputs: ' + str(sorted(actual ^ expected)))
    return index, review, files, digest(index_data)


def resolve_resource(files, directory, reference):
    # Legacy case-insensitive paths are resolved exactly once into canonical paths.
    reference = reference.replace('\\', '/')
    safe_path(reference)
    path = directory + '/' + reference
    matches = [p for p in files if p.casefold() == path.casefold()]
    if len(matches) != 1:
        raise ValueError('Missing/ambiguous resource: ' + path)
    return matches[0]


def picture_variants(declaration, directory, files):
    def day(node):
        return dict(path=resolve_resource(files, directory, node['attributes']['src']), treatment='authored')
    def night(node):
        overrides = children(node, 'override')
        return day(overrides[0]) if overrides and overrides[0]['attributes'].get('when') == 'night' else None
    specified = [[None, None] for _ in range(4)]
    specified[0] = [day(declaration), night(declaration)]
    for node in children(declaration, 'override'):
        when = node['attributes']['when']
        if when == 'night':
            continue
        if when not in ('spring', 'summer', 'autumn', 'fall', 'winter'):
            raise ValueError('Unknown picture override: ' + when)
        season = {'spring': 0, 'summer': 1, 'autumn': 2, 'fall': 2, 'winter': 3}[when]
        if 'src' in node['attributes']:
            specified[season][0] = day(node)
        specified[season][1] = night(node)
    result = []
    for season, (authored_day, authored_night) in enumerate(specified):
        effective_day = authored_day or result[0]['day']
        effective_night = authored_night or (dict(path=effective_day['path'], treatment='automatic-night-rgb888-v1')
                                            if authored_day else result[0]['night'])
        result.append(dict(season=('spring', 'summer', 'autumn', 'winter')[season], day=effective_day, night=effective_night))
    return result


def prepare(root=INPUT):
    index, review, files, index_hash = load_inputs(root)
    plugins = [p for p in review['plugins'] if p['scope'] == 'in-scope']
    order = plugin_order(plugins)
    by_plugin = unique(plugins, 'id', 'plugin')
    recipes = unique(review['recipes'], 'coverageId', 'declaration coverage')
    artifacts, assets = {}, []
    def artifact(data, extension):
        name = 'media/' + digest(data) + '.' + extension
        artifacts[name] = data
        return dict(url=URL+name, sha256=digest(data), bytes=len(data))
    for row in review['assets']:
        path = row['baselinePath']
        raw = files[path]
        if digest(raw) != row['sha256']:
            raise ValueError('Media differs from pinned review: ' + path)
        asset = dict(path=path, sourceSha256=row['sha256'], mediaType=row['mediaType'],
                     reachability=row['reachability'], notices=row['notices'],
                     author=row['declaredPluginAuthor'], credits=row['directContributionCredits'])
        if row['mediaType'] == 'bmp':
            width, height, pixels = decode(raw)
            asset.update(width=width, height=height, status='converted-surface',
                         day=artifact(png(width, height, surface(pixels)), 'png'),
                         night=artifact(png(width, height, surface(pixels, True)), 'png'))
        else:
            asset.update(status='retained-midi; playback-adapter-pending' if row['mediaType'] == 'mid' else 'retained-native',
                         native=artifact(raw, row['mediaType']))
        assets.append(asset)
    assets_by_path = unique(assets, 'path', 'asset')
    factories, definitions, excluded = [], [], []
    for identity in order:
        for row in review['recipes']:
            if row['pluginId'] != identity:
                continue
            entry = dict(id=row['contributionId'], pluginId=identity, type=row['contributionType'],
                         sourcePath=by_plugin[identity]['path'], coverageId=row['coverageId'],
                         implementation=row['effectiveImplementation'], declaration=row['declaration'],
                         ruleIds=row['ruleIds'])
            if row['contributionType'] == 'contribution':
                factories.append(entry)
            else:
                entry.update(status='prepared; gameplay-unavailable', diagnostic='recipe-runtime-pending')
                definitions.append(entry)
    for row in review['generatedChildren']:
        parent = recipes[row['generatedFrom']]
        definitions.append(dict(id=row['contributionId'], pluginId=row['pluginId'], type=row['contributionType'],
                                sourcePath=by_plugin[row['pluginId']]['path'], coverageId=row['coverageId'],
                                generatedFrom=parent['contributionId'], colorIndex=row['colorIndex'],
                                directionIndex=row['directionIndex'], spriteAttributes=row['spriteAttributes'],
                                status='prepared; gameplay-unavailable', diagnostic='recipe-runtime-pending'))
    unique(definitions, 'id', 'contribution ID')
    unique(factories, 'id', 'factory ID')
    definitions_by_id = {d['id']: d for d in definitions}
    for row in review['recipes']:
        if row['scope'] == 'excluded':
            excluded.append(dict(id=row['contributionId'], pluginId=row['pluginId'], type=row['contributionType'], reason='owner-exclusion'))
    for row in review['aliases']:
        if row['target'] not in definitions_by_id:
            raise ValueError('Unresolved selection/reversal: ' + row['target'])
    pictures = {}
    for row in review['pictures']:
        record = dict(id=row['pictureId'], owner=row['owner'], source=row['source'])
        if row['source'] == 'manifest-picture':
            owner = recipes[row['owner']]
            directory = str(PurePosixPath(by_plugin[owner['pluginId']]['path']).parent)
            variants = picture_variants(row['declaration'], directory, files)
            for variant in variants:
                for mode in ('day', 'night'):
                    value = variant[mode]
                    asset = assets_by_path[value['path']]
                    value['image'] = asset['night' if value['treatment'] == 'automatic-night-rgb888-v1' else 'day']
            record.update(status='prepared-picture', variants=variants)
        else:
            record.update(status='code-picture-binding-pending', resource=row.get('resource'))
        if record['id'] in pictures:
            if {k:v for k,v in record.items() if k != 'owner'} != {k:v for k,v in pictures[record['id']].items() if k != 'owner'}:
                raise ValueError('Conflicting picture ID: ' + record['id'])
        else:
            pictures[record['id']] = record
    notices = []
    for row in review['notices']:
        raw = files[row['path']]
        # Distribution notices remain exact source bytes, not guessed translations.
        name = 'notices/' + PurePosixPath(row['path']).name
        if name in artifacts and artifacts[name] != raw:
            name = 'notices/' + row['sha256'] + '-' + PurePosixPath(row['path']).name
        artifacts[name] = raw
        notices.append(dict(path=row['path'], sha256=digest(raw), url=URL+name))
    diagnostics = [dict(code='recipe-runtime-pending', phase='P2/P3-P9', count=len(definitions),
                        detail='Prepared declarations do not register executable gameplay handlers.'),
                   dict(code='native-pixel-formats-pending', phase='P2', detail='RGB888 conversion is explicit; native 555/565 capture is unverified.'),
                   dict(code='embedded-ui-resource-pending', phase='P2/P9', count=len(review['embeddedResources']),
                        detail='Serialized resource metadata retained inert; no CLR objects activated.'),
                   dict(code='translation-review-pending', phase='P2/P9', count=len(review['translations']),
                        detail='Japanese source text and donor candidates preserved; no unreviewed English substituted.')]
    diagnostics.extend(dict(code='source-project-gap', phase='P2/P9', **r) for r in review['resourceDiagnostics'])
    catalog = dict(schemaVersion=1, packId=PACK, version='1', source=index['source'], sourceIndexSha256=index_hash,
                   status='prepared; not selected by a world profile', pluginOrder=order,
                   plugins=[by_plugin[p] for p in order], factories=factories, contributions=definitions,
                   pictures=list(pictures.values()), selections=review['aliases'], assets=assets, notices=notices,
                   excluded=excluded, diagnostics=diagnostics,
                   counts=dict(manifests=len(review['plugins']), selectedPlugins=len(plugins),
                               declarations=len(review['recipes']), factories=len(factories), contributions=len(definitions),
                               pictures=len(pictures), preparedPictures=sum(p['status']=='prepared-picture' for p in pictures.values()),
                               assets=len(assets), convertedBitmaps=sum(a['mediaType']=='bmp' for a in assets),
                               excluded=len(excluded)))
    data = dump(catalog)
    artifacts['catalog.json'] = data
    artifacts['catalog.sha256'] = (digest(data) + '\n').encode()
    return catalog, artifacts


def verify_historical_artifacts(root=ROOT):
    # Frozen regression/demo artifacts have their own original-source identity.
    # Reproducing them is an explicit historical research operation, never a
    # canonical build dependency on the obsolete core/ or plugins/ trees.
    frozen = json.loads((root / 'prototype/content-artifacts.json').read_text())
    for row in frozen['files']:
        if digest(checked_read(root, row['path'])) != row['sha256']:
            raise ValueError('Frozen historical artifact drift: ' + row['path'])


def write_outputs(artifacts, check=False):
    expected = set(artifacts)
    actual = {p.relative_to(OUTPUT).as_posix() for p in OUTPUT.rglob('*') if p.is_file()}
    for name, data in artifacts.items():
        path = OUTPUT / name
        if check:
            if not path.is_file() or path.read_bytes() != data:
                raise ValueError('Generated bundled content drift: ' + name)
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
    if check:
        if actual != expected:
            raise ValueError('Unexpected/missing bundled outputs: ' + str(sorted(actual ^ expected)))
        if CATALOG.read_bytes() != artifacts['catalog.json']:
            raise ValueError('Server/browser bundled catalog drift')
        if CATALOG.with_suffix('.sha256').read_bytes() != artifacts['catalog.sha256']:
            raise ValueError('Server/browser bundled catalog hash drift')
    else:
        for name in actual - expected:
            (OUTPUT / name).unlink()
        CATALOG.write_bytes(artifacts['catalog.json'])
        CATALOG.with_suffix('.sha256').write_bytes(artifacts['catalog.sha256'])


def gallery(catalog, output):
    if not output.resolve().is_relative_to((ROOT / 'tmp').resolve()):
        raise ValueError('Review gallery must be under this checkout\'s tmp/')
    output.mkdir(parents=True, exist_ok=True)
    cards = []
    for asset in catalog['assets']:
        if asset['mediaType'] != 'bmp':
            continue
        pictures = []
        for mode in ('day', 'night'):
            source = ROOT / 'web/public' / asset[mode]['url'].lstrip('/')
            name = source.name
            (output / name).write_bytes(source.read_bytes())
            pictures.append('<figure><img src="' + name + '"><figcaption>' + mode + '</figcaption></figure>')
        cards.append('<article><h2>' + html.escape(asset['path']) + '</h2><div>' + ''.join(pictures) + '</div></article>')
    (output / 'index.html').write_text('<!doctype html><meta charset="utf-8"><title>Bundled surface review</title>'
        '<style>body{font:14px system-ui;background:#ddd;margin:24px}article{background:white;padding:12px;margin:12px 0}'
        'h2{font-size:14px}article div{display:flex;gap:16px;overflow:auto}figure{margin:0}img{image-rendering:pixelated;max-width:none;background:#acb}</style>'
        '<h1>Stable ExAV bundled surface preparation</h1><p>213 native-size sheets, day and RGB888 automatic night. '
        'This reviews source surfaces, not complete recipes, gameplay or native display fidelity. Authored seasonal/night selection is in catalog.json.</p>'
        + ''.join(cards), encoding='utf-8')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path, help='Explicit archive refresh from pinned Git blobs')
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--gallery', type=Path)
    args = parser.parse_args()
    if args.source_root:
        refresh(args.source_root, args.check)
    catalog, artifacts = prepare()
    verify_historical_artifacts()
    write_outputs(artifacts, args.check)
    if args.gallery:
        gallery(catalog, args.gallery)
    print('Stable bundled preparation: ' + json.dumps(catalog['counts']) + '; catalog ' + digest(artifacts['catalog.json']))


if __name__ == '__main__':
    try:
        main()
    except (ValueError, OSError, KeyError, ET.ParseError) as error:
        print('Bundled content error: ' + str(error), file=sys.stderr)
        sys.exit(1)
