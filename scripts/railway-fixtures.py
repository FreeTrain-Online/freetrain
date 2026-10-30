#!/usr/bin/env python3
"""Read-only P4-02 source oracle. Never imports production or writes expectations.

Literal vectors are reviewed separately in doc/parity/railway-fixtures.json.
This small, detached evaluator describes source outcomes and approved boundary
decisions; passing it is not evidence that a live railway handler exists.
"""
import argparse
from copy import deepcopy
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
PARITY = ROOT / 'doc/parity'
FIXTURES = PARITY / 'railway-fixtures.json'
OFFSETS = ((0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1))
MAX_MONEY = (1 << 63) - 1
UNCHANGED = dict(cash=0, allocator=0, terrain=0, entities=0, clock=0, random=0, listeners=0)


def require(condition, message):
    if not condition:
        raise ValueError(message)


def read(name):
    return json.loads((PARITY / name).read_text())


def angle(a, b):
    return min((a-b) % 8, (b-a) % 8)


def multiplier(height):
    return -2*height if height < 0 else height+1


def pattern_for(edges, patterns):
    return next((p['id'] for p in patterns.values()
                 if p['family'] in ('normal', 'junction') and set(p['connections']) == set(edges)), None)


def attachment(i, patterns):
    p, d = patterns[i['patternId']], i['newDirection']
    edges = sorted(p['connections'])
    if d in edges:
        return dict(allowed=True, resultId=p['id'])
    if p['family'] != 'normal':
        return dict(allowed=False, resultId=None)
    if i['wellConnected']:
        allowed = all(angle(e, d) in (1, 3, 4) for e in edges)
        result = pattern_for(edges+[d], patterns) if allowed else None
    else:
        keep = next((e for e in edges if angle(e, d) >= 3), None)
        allowed = keep is not None
        result = pattern_for([keep, d], patterns) if allowed else None
    return dict(allowed=allowed, resultId=result)


def detach(i, patterns):
    p = patterns[i['patternId']]
    edges, requested = set(p['connections']), set(i['directions'])
    hits = len(edges & requested)
    result, outcome = p['id'], 'unchanged'
    if p['family'] == 'normal' and hits or p['family'] == 'junction' and hits == 2:
        result, outcome = None, 'cleared'
    elif p['family'] == 'junction' and hits == 1:
        result = pattern_for(edges-requested, patterns)
        outcome = 'normal' if result is not None else 'null-pattern'
    return dict(sourceOutcome=outcome, patternId=result,
                portOutcome='reject-invalid-pattern' if outcome == 'null-pattern' else 'apply')


def connected(i, _):
    return sum(n['direction'] in i['edges'] and n['zOffset'] == 0 and
               (n['direction']+4) % 8 in n['edges'] for n in i['neighbors']) >= 2


def route(i, patterns):
    start, end = i['from'], i['to']
    reject = dict(outcome='reject', cells=[], patterns=[], quote=0, spend=0, changed=[], previewDelta=UNCHANGED)
    if start[2] != end[2]:
        return reject
    if start == end:
        return dict(reject, outcome='empty-preview-reject-commit')
    length = max(abs(end[0]-start[0]), abs(end[1]-start[1]))
    cells = [[start[a]+max(-t, min(t, end[a]-start[a])) for a in range(3)] for t in range(length+1)]
    headings = [OFFSETS.index((b[0]-a[0], b[1]-a[1])) for a, b in zip(cells, cells[1:])]
    result, cost, changed = [], 0, []
    require(len(cells) == len(i['cells']), 'Route input lacks per-cell state')
    for n, (loc, state) in enumerate(zip(cells, i['cells'])):
        incoming = (headings[max(0, n-1)]+4) % 8
        outgoing = headings[min(n, len(headings)-1)]
        pid = pattern_for([incoming, outgoing], patterns)
        g, z, w = state['ground'], loc[2], i['water']
        if pid is None or g <= z <= w and g < w or state['kind'] in ('obstacle', 'car'):
            return reject
        if state['kind'] == 'rail':
            d = outgoing if n == 0 else incoming
            attach = attachment(dict(patternId=state['patternId'], newDirection=d,
                                     wellConnected=state.get('wellConnected', False)), patterns)
            if 0 < n < length or not attach['allowed']:
                return reject
            pid = attach['resultId']
            if pid != state['patternId']:
                changed.append(n)
        else:
            changed.append(n)
        cost += 6000000*multiplier(z-g) + state.get('reclaimedValue', 0)
        result.append(pid)
    return dict(outcome='apply', cells=cells, patterns=result, quote=cost, spend=cost,
                changed=changed, previewDelta=UNCHANGED)


def slope(i, _):
    x, y, z = i['base']
    d, ground = i['direction'], i['ground']
    def inside(x, y, z):
        hsize, vsize, zsize = i['size']
        xx = x-(vsize-1)//2
        return 0 <= (xx+y)//2 < hsize and 0 <= y-xx < vsize and 0 <= z < zsize

    dx, dy = OFFSETS[d]
    if (d % 2 or z+1 >= i['ceiling'] or i['occupied'] or
            not all(inside(x+n*dx, y+n*dy, zz) for n in range(4) for zz in (z, z+1))):
        return dict(outcome='reject', rails=[], paired=[], build=0, remove=0)
    rails, paired = [], []
    for n, g in enumerate(ground):
        underground = z < g
        rz, pz = z+n//2, z+1-n//2
        rails.append(dict(location=[x+n*dx, y+n*dy, rz],
                          patternId=(44 if underground else 28)+2*d+n,
                          zdiff=[0, 1, -1, 0][n], zangle=(d+4) % 8 if n == 2 else d))
        paired.append(dict(location=[x+n*dx, y+n*dy, pz],
                           kind='filler' if n < 2 else 'wall' if underground else 'support',
                           sprite=None if n < 2 else d+n-2,
                           below=not(n < 2 and g >= pz), above=not(n >= 2 and g >= pz+1)))
    return dict(outcome='apply', rails=rails, paired=paired,
                pierStyle='default' if d in (0, 2) else 'slopeNE',
                build=40000000*multiplier(z-i['water']),
                remove=8000000*(abs(z+1-i['water'])+1))


def slope_remove(i, _):
    d, level = i['direction'], i['selectedLevel']
    dx, dy = OFFSETS[d]
    x, y, z = i['selected']
    base = [x-level*dx, y-level*dy, z-level//2]
    if not i['completeOwner'] or i['cars'] or i['requestedDirection'] != d:
        return dict(outcome='reject', base=base, spend=0, cleared=[])
    cleared = [[base[0]+n*dx, base[1]+n*dy, base[2]+zz] for n in range(4) for zz in (0, 1)]
    return dict(outcome='apply', base=base, spend=8000000*(abs(base[2]+1-i['water'])+1), cleared=cleared)


def support(i, _):
    occupants = i['occupants']
    if i['action'] == 'elect':
        elected = [] if any(0 <= o['z'] < i['railZ'] for o in occupants) else list(range(i['ground'], i['railZ']))
        return dict(elected=[dict(z=z, style=i['top'] if z == i['railZ']-1 else i['body'], owner=i['owner'])
                             for z in elected], removed=[], retained=occupants, spend=0)
    removed = [o for o in occupants if o['kind'] == 'pier' and 0 <= o['z'] < i['railZ']]
    return dict(elected=[], removed=removed, retained=[o for o in occupants if o not in removed], spend=0)


def normal_remove(i, patterns):
    changed, remaining, removed = [], [], []
    spend = 0
    for n, state in enumerate(i['cells']):
        pid = state.get('patternId')
        if pid is not None and not state.get('car', False):
            spend += 2000000*(abs(i['z']-i['water'])+1)
            outcome = detach(dict(patternId=pid, directions=i['directions']), patterns)
            if outcome['portOutcome'] != 'apply':
                return dict(outcome='reject-invalid-pattern', spend=0, changed=[], remaining=[], removedPiers=[])
            if outcome['sourceOutcome'] != 'unchanged':
                changed.append(n)
            pid = outcome['patternId']
        remaining.append(pid)
        # Teardown is unconditional, even when no rail, wrong edges or a car.
        removed.extend(dict(cell=n, **p) for p in state['piers'] if p['z'] < i['z'])
    return dict(outcome='apply', spend=spend, changed=changed, remaining=remaining, removedPiers=removed)


def special(i, _):
    op, action, cells, d = i['operation'], i['action'], i['cells'], i['direction']
    z, water = i['z'], i['water']
    reject = dict(outcome='reject', spend=0, charges=[], changed=[], skipped=[], art=[], pierCalls=[], restored=[])
    if len(cells) < 2:
        return dict(reject, outcome='source-noop') if action == 'remove' else reject
    if op != 'girder' and d % 2:
        return reject
    if action == 'build':
        if any(c.get('car') for c in cells):
            return reject  # Approved protection fence, separate from source predicates.
        for c in cells:
            if c['kind'] != 'empty' and not(op == 'tunnel' and c['kind'] == 'mountain'):
                if c['kind'] not in ('normal', 'junction', 'bridge', 'girder', 'tunnel', 'garage') or not {d, (d+4) % 8} <= set(c['edges']):
                    return reject
        if op == 'girder' and any(c['ground'] >= z for c in cells):
            return reject
        if op == 'garage' and any(c['ground'] != z for c in cells):
            return reject
        if op == 'tunnel' and (z < water or not any(c['kind'] == 'mountain' for c in cells)):
            return reject
    count = len(cells) if action == 'build' or op in ('bridge', 'girder') else len(cells)-1
    changed, skipped, art, piers, restored, charges = [], [], [], [], [], []
    subtotal, new_count = 0, 0
    for n, c in enumerate(cells[:count]):
        matching = c['kind'] == op and d in c.get('edges', [])
        if action == 'remove':
            if matching and not c.get('car'):
                changed.append(n)
                if op in ('bridge', 'girder'):
                    piers.append(n)
                if op == 'tunnel':
                    restored.append(dict(cell=n, corners=c['corners'], owned=False, trees=[], overlay=None))
            elif matching and c.get('car'):
                skipped.append(n)
            if op == 'garage':
                subtotal += 200000 if matching else 0
                charges.append(subtotal)
        elif op == 'garage' or c['kind'] in ('empty', 'mountain'):
            changed.append(n)
            if op == 'bridge':
                first = n == 0 or cells[n-1]['kind'] != 'empty'
                last = n == count-1 or cells[n+1]['kind'] != 'empty'
                mode = 0 if first else 2 if last else 1
                sprite = {0: 3+mode, 2: 2-mode, 4: 5-mode, 6: mode}[d]
                art.append(dict(cell=n, mode=mode, sprite=sprite))
                if new_count % 2 == 0:
                    piers.append(n)
                new_count += 1
            elif op == 'girder':
                art.append(dict(cell=n, sprite=[3, 1, 0, 2][d % 4]))
                piers.append(n)
    if op in ('bridge', 'girder'):
        first = cells[0]['ground']
        cost = 0 if first <= z <= water and first < water else sum(
            (6000000 if action == 'build' else 2000000)*multiplier(z-c['ground']) for c in cells)
    elif op == 'tunnel':
        cost = len(changed)*(12000000 if action == 'build' else 4000000)
    else:
        cost = 6500000*len(cells) if action == 'build' else sum(charges)
    if not charges:
        charges = [cost]
    return dict(outcome='apply', spend=cost, charges=charges, changed=changed, skipped=skipped,
                art=art, pierCalls=piers, restored=restored)


def boundary(i, _):
    if i['kind'] == 'coordinate-admission':
        valid = all(-(1 << 31) <= v < (1 << 31) for p in (i['from'], i['to']) for v in p)
        if not valid:
            return dict(outcome='reject-coordinate-range', visited=0)
        if i['from'][2] != i['to'][2]:
            return dict(outcome='reject-noncoplanar', visited=0)
        count = max(abs(a-b) for a, b in zip(i['from'], i['to']))+1
        return dict(outcome='reject-work-limit' if count > i['limit'] else 'apply',
                    visited=0 if count > i['limit'] else count)
    if i['kind'] == 'space':
        hsize, vsize, zsize = i['size']
        x, y, z = i['location']
        xx = x-(vsize-1)//2
        h, v = (xx+y)//2, y-xx
        horizontal = 0 <= h < hsize and 0 <= v < vsize
        return dict(storage=[h, v, z], inside=horizontal and 0 <= z < zsize,
                    border=not(0 <= h < hsize and 2*z <= v < vsize-2*(zsize-z)))
    if i['kind'] == 'product':
        product = i['unit']*i['multiplier']*i['count']
        return dict(outcome='apply' if 0 <= product <= MAX_MONEY else 'reject-overflow',
                    amount=product if 0 <= product <= MAX_MONEY else None)
    cash, spend, increment = i['cash'], i['spend'], i['increment']
    result = cash-spend
    if increment <= 0 or not -MAX_MONEY-1 <= result <= MAX_MONEY:
        return dict(outcome='reject', cash=cash, topUps=0)
    topups = max(0, (-result+increment-1)//increment)
    final = result+topups*increment
    if final > MAX_MONEY:
        return dict(outcome='reject', cash=cash, topUps=0)
    return dict(outcome='apply', cash=final, topUps=topups)


def drawing(i, _):
    if i['kind'] == 'traffic':
        return ['road.before', 'accessory.before', 'rail.before', 'car', 'rail.after', 'accessory.after']
    if i['kind'] == 'cut':
        diff = i['cutZ']-i['z']+1
        return dict(pierDiff=diff, mountainDiff=diff-1, pierAlpha=diff == 0)
    # Actual signal dispatch, despite needsToDrawAfter's misleading name.
    return dict(before=i['needsToDrawAfter'], after=not i['needsToDrawAfter'], spend=0)


def components(i, patterns):
    kind = i['kind']
    if kind == 'stationary-booking':
        return dict(account='RAIL_SERVICE' if i['action'] == 'complete' else 'SUBSIDIARIES',
                    spend=i['price'] if i['action'] == 'complete' else 500000*i['size'][0]*i['size'][1])
    if kind == 'site-cancel':
        return dict(spend=0, completed=False)
    if kind == 'special-attach':
        return dict(allowed=i['newDirection'] in i['edges'], spend=0)
    if kind == 'signal':
        allowed = not i['car'] and i['railKind'] in ('empty', 'normal')
        return dict(allowed=allowed, patternId=(10 if i['direction'] in (0, 4) else 4) if allowed else None, spend=0)
    if kind == 'pole':
        edges = i['edges']
        allowed = len(edges) == 2 and all(d % 2 == 0 for d in edges) and (edges[0]+4) % 8 == edges[1]
        return dict(allowed=allowed, orientation=('NS' if 0 in edges else 'EW') if allowed else None,
                    oldOnRemoved=False, spend=0)
    if kind == 'crossing':
        ns = set(i['railEdges']) == {0, 4} and {2, 6} <= set(i['roadEdges'])
        ew = set(i['railEdges']) == {2, 6} and {0, 4} <= set(i['roadEdges'])
        eligible = ns or ew
        return dict(accessory=('crossing' if eligible else None) if i['accessory'] in (None, 'crossing') else i['accessory'])
    if kind == 'speed-eligibility':
        return dict(allowed=all(c['traffic'] and not c['listed'] for c in i['cells']), spend=0)
    if kind == 'speed-cleanup':
        return [c['id'] for c in i['cells'] if c['traffic']]
    raise ValueError('Unknown component rule: '+kind)


EVALUATORS = dict(attachments=attachment, detachments=detach, connectivity=connected,
                  routes=route, slopes=slope, slopeRemoval=slope_remove, supports=support, normalRemoval=normal_remove,
                  specialRails=special, boundaries=boundary, drawing=drawing, components=components)


def validate(data, source_root=None):
    review, geometry = read('railway-review.json'), read('exav-geometry.json')
    require(data['schemaVersion'] == 1 and data['slice'] == 'P4-02' and
            data['status'] == 'independent-source-vectors-runtime-unimplemented' and
            data['authority'] == review['authority'], 'Fixture authority/status drift')
    for name, expected in data['artifactBindings'].items():
        require(hashlib.sha256((PARITY / name).read_bytes()).hexdigest() == expected,
                'Fixture artifact binding drift: '+name)
    require(set(data['artifactBindings']) == {'railway-review.json', 'exav-scenarios.json',
            'exav-geometry.json', 'exav-geometry-behavior.json', 'exav-geometry-dependency-review.json'},
            'Missing fixture artifact binding')
    scenarios = {s['id']: s for s in read('exav-scenarios.json')['scenarios']}
    require(set(data['scenarioBindings']) == {'EXAV-SCN-001', 'EXAV-SCN-002', 'EXAV-SCN-003', 'EXAV-SCN-031'},
            'Missing source scenario')
    groups = data['groups']
    require(groups.keys() == EVALUATORS.keys(), 'Missing/unknown fixture group')
    require(data['patterns'] == [p for p in geometry['patterns'] if isinstance(p['id'], int)],
            'Literal P0 pattern drift')
    patterns = {p['id']: p for p in data['patterns']}
    require(set(patterns) == set(range(60)), 'Missing source pattern')
    directions = data['directions']
    require([{k: d[k] for k in ('index', 'name', 'offset')} for d in directions] == geometry['directions'] and
            all(d['left'] == (d['index']-1) % 8 and d['right'] == (d['index']+1) % 8 and
                d['opposite'] == (d['index']+4) % 8 for d in directions), 'Literal direction drift')
    sources = {s['path']: s for s in review['sourceReviews']}
    checked_sources, ids = set(), set()
    for name, group in groups.items():
        require(group['derivation'] and group['sources'] and group['cases'], 'Incomplete group: '+name)
        require([c['id'] for c in group['cases']] == group['requiredCaseIds'], 'Missing/reordered fixture witness: '+name)
        require(set(group['decisions']) <= {d['id'] for d in review['decisions']}, 'Unknown decision')
        for binding in group['sources']:
            path = binding['path']
            require(path in sources and binding['sha256'] == sources[path]['sha256'] and
                    binding['symbols'] == [b['symbol'] for b in sources[path]['bindings']],
                    'Unreviewed fixture source: '+path)
            if source_root is not None:
                raw = subprocess.check_output(['git', '-C', str(source_root), 'show',
                    f"{data['authority']['revision']}:{data['authority']['subtree']}/{path}"])
                require(hashlib.sha256(raw).hexdigest() == binding['sha256'], 'Pinned fixture blob drift')
                text = raw.decode(sources[path]['encoding'])
                require(all(symbol in text for symbol in binding['symbols']), 'Pinned fixture symbol drift')
            checked_sources.add(path)
        for case in group['cases']:
            require(case['id'] not in ids, 'Duplicate fixture ID')
            ids.add(case['id'])
            before = deepcopy(case['input'])
            actual = EVALUATORS[name](case['input'], patterns)
            require(case['input'] == before, 'Oracle mutated input: '+case['id'])
            require(actual == case['expected'], f"Fixture mismatch: {case['id']}\nexpected {case['expected']}\nactual   {actual}")
    # Exact Cartesian coverage prevents a passing subset from closing P4-02.
    attachments = groups['attachments']['cases']
    keys = [(c['input']['patternId'], c['input']['wellConnected'], c['input']['newDirection']) for c in attachments]
    expected = {(p, connected, d) for p in range(60) for connected in
                ([False, True] if p < 12 else [False]) for d in range(8)}
    require(len(keys) == len(expected) and set(keys) == expected, 'Incomplete attachment matrix')
    old = read('exav-geometry-behavior.json')['normalAttachments']
    require([dict(**c['input'], **c['expected']) for c in attachments if c['input']['patternId'] < 12] == old,
            'P0 normal attachment binding drift')
    requests = [tuple(sorted(patterns[p]['connections'])) for p in range(12)]
    keys = [(c['input']['patternId'], tuple(c['input']['directions'])) for c in groups['detachments']['cases']]
    require(len(keys) == 720 and set(keys) == {(p, d) for p in range(60) for d in requests},
            'Incomplete detachment matrix')
    slopes = groups['slopes']['cases']
    require({(c['input']['direction'], tuple(c['input']['ground'])) for c in slopes if c['expected']['outcome'] == 'apply'} >=
            {(d, g) for d in (0, 2, 4, 6) for g in ((2, 2, 2, 2), (4, 4, 4, 4), (2, 4, 2, 4))},
            'Missing cardinal/surface/underground slope matrix')
    require({c['input']['occupied'][0] for c in slopes if c['input']['occupied']} == set(range(8)),
            'Missing paired-cell obstruction')
    for identity, binding in data['scenarioBindings'].items():
        require(binding['sourceInput'] == scenarios[identity]['input'] and
                binding['sourceExpected'] == scenarios[identity]['expected'] and
                binding['cases'] and set(binding['cases']) <= ids, 'Invalid scenario binding: '+identity)
    cases = {c['id']: c for g in groups.values() for c in g['cases']}
    scn = scenarios['EXAV-SCN-001']
    d = directions[scn['input']['direction']]
    require({k: d[k] for k in scn['expected'] if k in d} ==
            {k: v for k, v in scn['expected'].items() if k in d} and
            (pattern_for(scn['input']['cornerDirections'], patterns) is not None) == scn['expected']['cornerNormalPattern'],
            'SCN-001 direction/corner binding drift')
    dry = cases['ROUTE-dry-18m']
    scn = scenarios['EXAV-SCN-002']
    require(dry['input']['from'] == scn['input']['from'] and dry['input']['to'] == scn['input']['to'] and
            dry['input']['water'] == scn['input']['waterLevel'] and
            all(c['ground'] == scn['input']['groundLevel'] for c in dry['input']['cells']) and
            dry['expected']['cells'] == scn['expected']['cells'] and
            dry['expected']['quote'] == scn['expected']['previewCost'] and
            dry['expected']['previewDelta']['cash'] == scn['expected']['previewCashDelta'] and
            dry['expected']['spend'] == scn['expected']['commitRailServiceExpenditure'],
            'SCN-002 route/booking binding drift')
    scn = scenarios['EXAV-SCN-003']
    for z, expected_cost in zip(scn['input']['emptyCellZ'], scn['expected']['costs']):
        c = cases[f'ROUTE-SCN003-z{z}']
        require(c['input']['from'][2] == z and c['expected']['quote'] == 3*expected_cost and
                all(cell['ground'] == scn['input']['groundLevel'] for cell in c['input']['cells']),
                'SCN-003 per-cell price binding drift')
    wet = cases['ROUTE-underwater']
    require(wet['expected']['outcome'] == 'reject' and wet['expected']['quote'] == scn['expected']['submergedCost'] and
            wet['input']['from'][2] == scn['input']['submergedCase']['z'] and
            wet['input']['water'] == scn['input']['submergedCase']['water'] and
            all(c['ground'] == scn['input']['submergedCase']['ground'] for c in wet['input']['cells']),
            'SCN-003 water binding drift')
    require(cases['DRAW-traffic-order']['expected'] == scenarios['EXAV-SCN-031']['expected']['order'],
            'SCN-031 draw order binding drift')
    require({r['id'] for r in data['coverage']} == {'normal-and-junction', 'slopes', 'piers', 'booking',
            'preview', 'endpoints', 'protection', 'boundaries', 'drawing', 'trusted-components'},
            'Missing fixture coverage obligation')
    for requirement in data['coverage']:
        require(requirement['cases'] and set(requirement['cases']) <= ids and requirement['review'],
                'Missing coverage witness')
    return len(ids), len(patterns), len(checked_sources)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true', help='Read-only (also default); no fixture update mode')
    parser.add_argument('--source-root', type=Path, help='Explicit archive; read pinned Git blobs only')
    args = parser.parse_args()
    counts = validate(json.loads(FIXTURES.read_text()), args.source_root)
    print('P4-02 independent fixtures passed: %d cases, %d literal patterns, %d pinned source files. '
          'No production/native/runtime parity claim.' % counts)


if __name__ == '__main__':
    main()
