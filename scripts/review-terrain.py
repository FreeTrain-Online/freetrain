#!/usr/bin/env python3
"""Check P3-01 literal specification vectors; never run original or production terrain.

Default checks use checked-in evidence only. --source-root additionally verifies
raw pinned Git blobs and symbols; a sibling checkout is never discovered implicitly.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import struct
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
PARITY = ROOT / 'doc/parity'


def require(condition, message):
    if not condition:
        raise ValueError(message)


def f32(value):
    return struct.unpack('<f', struct.pack('<f', value))[0]


def expected_primitive(kind, i):
    """Small independent arithmetic oracles, not a whole-world reference port."""
    if kind == 'dimensions':
        h, v, z = i['dimensions']
        return {'storage': [h, v + 2*z, z], 'water': z//4} if i['route'] == 'source-empty' else {
            'storage': [h, v, z], 'water': i['water']}
    if kind == 'coordinates':
        h, v = i['hv']
        return {'xy': [h-v//2+(i['dimensions'][1]-1)//2, h+(v+1)//2]}
    if kind == 'compass':
        x, y = i['deltaXY']
        return {'deltaScreen': [16*(x+y), 8*(y-x)]}
    if kind == 'coast-sector':
        x, y = i['xy']
        x0, y0, x1, y1 = i['boundsXY']
        require(x1 > x0 and y1 > y0, 'Degenerate compass footprint')
        qx, qy = 2*x-x0-x1, 2*y-y0-y1
        return {'sector': ('N' if qy < 0 else 'S') if abs(qy)*(x1-x0) >= abs(qx)*(y1-y0)
                else ('W' if qx < 0 else 'E')}
    if kind == 'water-ground':
        return {'underwater': i['z'] < i['water'], 'underground': i['z'] < i['ground'],
                'elevated': i['z'] > i['ground']}
    if kind == 'lightness':
        return {'brightness': f32((min(i['rgb'])+max(i['rgb']))/510)}
    if kind == 'sample':
        h, v = i['hv']
        image = i['image']
        height, width = len(image), len(image[0])
        x = f32(f32(h + (v % 2)*0.5)*width/i['worldHV'][0])
        y = f32(f32(v)*height/i['worldHV'][1])
        x0, y0 = min(math.floor(x), width-1), min(math.floor(y), height-1)
        x1, y1 = min(math.ceil(x), width-1), min(math.ceil(y), height-1)
        dx, dy = f32(x-x0), f32(y-y0)
        top = f32(f32(image[y0][x0]*f32(1-dx))+f32(image[y0][x1]*dx))
        bottom = f32(f32(image[y1][x0]*f32(1-dx))+f32(image[y1][x1]*dx))
        return {'depth': f32(f32(top*f32(1-dy))+f32(bottom*dy))}
    if kind == 'displace':
        return {'ground': int(min(i['Z']-1, f32(i['Z']*f32(i['depth']))))}
    if kind == 'interpolation-group':
        cap = min(i['ground'])+1
        return {'ground': [min(n, cap) for n in i['ground']]}
    if kind == 'smooth-corner':
        offset = int(min(i['Z']*4-1, f32(i['Z']*4*f32(i['depth'])))) % 4
        return {'offset': offset if i['min2'] == i['max2'] and i['max4'] != i['max2'] else 0}
    if kind == 'corner-total':
        return {'fine': [i['ground']*4+h for h in i['corners']],
                'packed': sum(h << shift for h, shift in zip(i['corners'], [12, 8, 4, 0]))}
    if kind == 'corner-edit':
        g = i['ground']
        c = list(i['corners']) if i['corners'] is not None else [4]*4
        if i['corners'] is None:
            require(i['delta'] == -1, 'Flat-lowering primitive only')
            g -= 1
        c[i['cornerIndex']] += i['delta']
        require(all(0 <= n <= 4 for n in c), 'Invalid quarter corner')
        return {'ground': g + int(c == [4]*4), 'corners': None if c in ([0]*4, [4]*4) else c}
    if kind == 'corner-raise-gate':
        return {'allowed': i['matched'] and i['sameGround'] and not i['blocked'] and
                i['offset'] < 4 and not (i['ground'] == i['Z']-1 and i['offset'] == 3)}
    if kind == 'rectangle-upper-target':
        return {'offset': 4 if not i['shift'] else
                (2 if i['cornersAtLeast2'] < 2 or i['eastOrSouthConstraint'] else 4)}
    if kind == 'rectangle-sites':
        return {'sites': [[x, y] for x in range(i['minXY'][0], i['maxXY'][0])
                          for y in range(i['minXY'][1], i['maxXY'][1])]}
    if kind == 'repair':
        refs = [v for v in i['neighborFine'] if v != -1]
        offset = refs[0]-4*i['ground'] if refs else -1
        return {'offset': offset if refs and len(set(refs)) == 1 and 0 <= offset <= 4 else None}
    if kind == 'forest-scatter':
        require(len(i['coinDraws']) == 2*i['density'], 'Forest coin consumption')
        count = i['coinDraws'].count(0)
        require(count == len(i['treeDraws']), 'Forest placement draw count')
        # Source swaps only positions, never sprite slots. Stable position sort is
        # sufficient for these distinct-Y vectors; this is not a full sort port.
        positions = sorted(((x+y, ((y-x) >> 1)+8) for x, y, _ in i['treeDraws']), key=lambda p: p[1])
        return {'count': count, 'triples': [[x, y, draw[2]] for (x, y), draw in zip(positions, i['treeDraws'])],
                'drawCount': 2*i['density']+3*count}
    if kind == 'forest-eligibility':
        return {'allowed': i['z'] == i['ground'] and (i['reusable'] or i['mountain']) and
                (not i['generated'] or i['z'] >= i['water'])}
    if kind == 'creek-seed':
        a, b = i['fromXY'], i['toXY']
        seed = ((a[0]*128+a[1]) ^ (b[0]*128+b[1])) & 0xffffffff
        return {'seed': seed-0x100000000 if seed >= 0x80000000 else seed}
    if kind == 'creek-route':
        x, y = i['fromXY']
        ex, ey = i['toXY']
        draws = iter(i['axisDraws'])
        route, back = [], 0
        while (x, y) != (ex, ey):
            dx, dy = (ex > x)-(ex < x), (ey > y)-(ey < y)
            if dx and dy:
                # For SE: left=E, right=S. Generalize via source heading index.
                heading = {(1, -1): 1, (1, 1): 3, (-1, 1): 5, (-1, -1): 7}[dx, dy]
                cardinal = (heading + (-1 if next(draws) == 0 else 1)) % 8
                dx, dy = {0: (0, -1), 2: (1, 0), 4: (0, 1), 6: (-1, 0)}[cardinal]
            bits, opposite = {(0, -1): (8, 2), (1, 0): (4, 1), (0, 1): (2, 8), (-1, 0): (1, 4)}[dx, dy]
            route.append([x, y, back | bits])
            x, y, back = x+dx, y+dy, opposite
        route.append([x, y, back])
        require(next(draws, None) is None, 'Unused creek draw')
        return {'route': route}
    if kind == 'creek-apply':
        placed = [[x, y, bits | i['existingBits'].get(f'{x},{y}', 0)] for x, y, bits in i['route']
                  if [x, y] not in i['blockedXY']]
        return {'placed': placed, 'continuous': len(placed) == len(i['route'])}
    if kind == 'land-booking':
        return {'spend': i['width']*i['depth']*i['price'] if i['owned'] else 0}
    if kind == 'terrace-roundtrip':
        return {'terraceGround': i['ground']+1, 'terraceZ': i['ground'],
                'restoredGround': i['ground'], 'restoredCorners': i['corners'],
                'restoredTrees': False, 'restoredOwned': False}
    if kind == 'cliff-restore':
        return {'result': 'reject' if i['occupied'] or any(n < 0 or n > 4 for n in i['sampleOffsets'])
                else ('noop' if not any(i['sampleOffsets']) else 'create')}
    raise ValueError('Unknown terrain fixture kind: '+kind)


def same(actual, expected):
    if isinstance(expected, dict):
        return isinstance(actual, dict) and actual.keys() == expected.keys() and all(same(actual[k], v) for k, v in expected.items())
    if isinstance(expected, list):
        return isinstance(actual, list) and len(actual) == len(expected) and all(same(a, b) for a, b in zip(actual, expected))
    if type(expected) in (int, float) and type(actual) in (int, float):
        return abs(actual-expected) < 1e-7
    return type(actual) is type(expected) and actual == expected


def validate(review, fixtures, locale, source_root=None):
    baseline = json.loads((PARITY/'exav-baseline.json').read_text())
    require(review['authority']['revision'] == baseline['revision'] and review['authority']['subtree'] == baseline['subtree'], 'Unreviewed source identity')
    require(fixtures['authority'] == review['authority'], 'Fixture authority mismatch')
    require(review['status'] == 'source-contract-specified' and fixtures['status'] == 'specified', 'Overclaimed status')
    ledger = json.loads((PARITY/'exav-ledger.json').read_text())
    known = {r['baselinePath']: r['sha256'] for r in ledger['fileInventory']}
    sources = {r['path']: r for r in review['sourceReviews']}
    require(len(sources) == len(review['sourceReviews']), 'Duplicate source')
    texts = {}
    for path, row in sources.items():
        require(known.get('trunk/'+path) == row['sha256'], 'Source hash drift: '+path)
        if source_root is not None:
            raw = subprocess.check_output(['git', '-C', str(source_root), 'show', f"{baseline['revision']}:trunk/{path}"])
            require(hashlib.sha256(raw).hexdigest() == row['sha256'], 'Pinned blob drift: '+path)
            try:
                text = raw.decode('utf-8-sig')
            except UnicodeDecodeError:
                text = raw.decode('cp932')
            require(all(symbol in text for symbol in row['symbols']), 'Missing source symbol: '+path)
            texts[path] = text
    decisions = review['decisions']+review['generationDecisions']
    ids = {d['id'] for d in decisions}
    require(len(ids) == len(decisions), 'Duplicate decision')
    require({f'P3-TERRAIN-{n:03}' for n in range(1, 13)} <= ids, 'Missing terrain decision')
    rows = fixtures['fixtures']
    require(len({r['id'] for r in rows}) == len(rows), 'Duplicate fixture')
    require({r['id'] for r in rows} == {f'TERRAIN-{n:03}' for n in range(1, 78)}, 'Missing or unknown fixture')
    for row in rows:
        require(row['status'] == 'specified' and row['derivation'], 'Overclaimed or unexplained fixture')
        require(set(row['sources']) <= sources.keys() and row['sources'], 'Unreviewed fixture source')
        require(set(row['decisions']) <= ids, 'Unknown fixture decision')
        require(row['classification'] in ('source', 'adaptation', 'generation'), 'Unclassified fixture')
        require(same(expected_primitive(row['kind'], row['input']), row['expected']), 'Fixture mismatch: '+row['id'])
    require(len(review['forestContributions']) == 18, 'Missing forest variants')
    require(len({r['id'] for r in review['forestContributions']}) == 18, 'Duplicate forest variant')
    require(all(r['price'] == 100 and r['computerCannotBuild'] for r in review['forestContributions']), 'Forest data drift')
    if texts:
        manifest = ET.fromstring(texts['plugins/org.kohsuke.freetrain.land.forest/plugin.xml'])
        actual = []
        for contribution in manifest.findall('contribution'):
            if contribution.get('type') != 'land':
                continue
            ground = contribution.find('ground/picture')
            actual.append({'id': contribution.get('id'), 'nameJa': contribution.findtext('name'),
                           'groupJa': contribution.findtext('group'), 'density': int(contribution.findtext('density')),
                           'spriteCount': int(contribution.findtext('count')), 'price': int(contribution.findtext('price')),
                           'groundPicture': ground.attrib if ground is not None else None,
                           'computerCannotBuild': contribution.find('computerCannotBuild') is not None})
        require(actual == review['forestContributions'], 'Forest manifest mismatch')
    manual = (ROOT/'doc/web-parity-manual.md').read_text()+(ROOT/'doc/web-parity-online-manual.md').read_text()
    # P3's maintained action overlay retains historical developer ID meanings;
    # ordinary clean-checkout validation never depends on ignored tmp files.
    actions = (ROOT/'doc/web-parity-actions.md').read_text()
    for mapping in review['workflowMappings']:
        require(all(identity in manual for identity in mapping['manual']), 'Missing manual mapping')
        require(all(identity in actions for identity in mapping['developer']), 'Missing developer mapping')
        require(set(mapping['sources']) <= sources.keys(), 'Unreviewed workflow source')
    strings = locale['sourceStrings']
    require(len({r['id'] for r in strings}) == len(strings), 'Duplicate localization ID')
    for row in strings:
        require(row['source'] in sources and row['proposedEn'], 'Incomplete source text')
        require(hashlib.sha256(row['ja'].encode()).hexdigest() == row['jaSha256'], 'Source text hash drift')
        if texts:
            require(row['ja'] in texts[row['source']], 'Source text absent')
        require(row['status'] == 'wording-reviewed-not-shipped', 'Overclaimed translation')
    return len(sources), len(rows), len(strings)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path, help='Explicit ExAV archive repository for raw Git verification')
    parser.add_argument('--check', action='store_true', help='Read-only check (also default)')
    args = parser.parse_args()
    documents = [json.loads((PARITY/name).read_text()) for name in
                 ('terrain-review.json', 'terrain-fixtures.json', 'terrain-localization-review.json')]
    counts = validate(*documents, source_root=args.source_root)
    print('P3-01 specification check passed: %d pinned files, %d literal fixtures, %d source text records. '
          'Production terrain, browser and original-runtime acceptance remain unverified.' % counts)


if __name__ == '__main__':
    main()
