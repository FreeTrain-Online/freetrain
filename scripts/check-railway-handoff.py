#!/usr/bin/env python3
"""Check P4 acceptance reconciliation and independent P5 interface witnesses.

Reads evidence only, never production movement code. Optional source verification
uses pinned Git blobs. Passing this checker does not close P4 or implement P5.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]


def read(path):
    return json.loads((ROOT / path).read_text())


def check(source_root=None):
    acceptance = read('doc/parity/railway-acceptance.json')
    handoff = read('doc/parity/railway-p5-handoff.json')
    review = read('doc/parity/railway-review.json')
    accessories = read('doc/parity/railway-accessories.json')
    localization = read('doc/parity/railway-localization-review.json')
    patterns = read('doc/parity/railway-fixtures.json')['patterns']
    for document in (acceptance, handoff):
        for row in document['inputs']:
            assert hashlib.sha256((ROOT / row['path']).read_bytes()).hexdigest() == row['sha256'], row['path']
    assert acceptance['phaseGate'] == 'accepted-browser-portable'
    assert acceptance['ownerDisposition']['choice'] == 'defer-performance-findings-to-P11'
    assert acceptance['translations']['count'] == len(localization['sourceStrings']) == 78
    assert (ROOT / acceptance['translations']['evidence']).is_file()
    assert {r['id']: r['status'] for r in acceptance['deferredFindings']} == {
        'P4-CAP-001': 'open-deferred-to-P11', 'P4-CAP-002': 'open-deferred-to-P11'}
    for field, key in [('operations', 'id'), ('decisions', 'id'), ('contributions', 'coverageId')]:
        rows = acceptance[field]
        assert len(rows) == len({r[key] for r in rows}) == len(review[field])
        assert {r[key] for r in rows} == {r[key] for r in review[field]}
        for row in rows:
            assert (ROOT / row['evidence']).is_file(), row['evidence']
    assert {r['coverageId'] for r in accessories['contributions']} == {
        r['coverageId'] for r in acceptance['contributions']}
    delivered = {r['coverageId']: r for r in accessories['contributions']}
    for row in acceptance['contributions']:
        assert row['portableHandler'] == delivered[row['coverageId']]['portableHandler']
    for row, original in zip(acceptance['operations'], review['operations']):
        assert row['owner'] == original['owner'] and row['decisions'] == original['decisions']
    for row, original in zip(acceptance['decisions'], review['decisions']):
        assert row['disposition'] == original['status']
    known = {r['baselinePath']: r['sha256'] for r in read('doc/parity/exav-ledger.json')['fileInventory']}
    assert handoff['authority']['revision'] == review['authority']['revision']
    for row in handoff['sources']:
        assert known['trunk/' + row['path']] == row['sha256']
        if source_root:
            raw = subprocess.check_output(['git', '-C', str(source_root), 'show',
                handoff['authority']['revision'] + ':trunk/' + row['path']])
            assert hashlib.sha256(raw).hexdigest() == row['sha256']
            lines = raw.decode(row['encoding']).splitlines()
            for binding in row['bindings']:
                assert lines[binding['line'] - 1] == binding['text']
                assert binding['symbol'] in binding['text']
    offsets = [(0,-1),(1,-1),(1,0),(1,1),(0,1),(-1,1),(-1,0),(-1,-1)]
    assert len(handoff['guideCases']) == 17
    for case in handoff['guideCases']:
        edges = patterns[case['pattern']]['connections']
        d = case['incoming']; left = (d-1)%8; right = (d+1)%8
        assert (d+4)%8 in edges  # Legal arrival, not arbitrary heading.
        if d in edges:
            outgoing = d
            if case['kind'] == 'junction' and (left in edges or right in edges):
                dx, dy = offsets[d]; x, y, z = case['location']
                straight = (case['previous'] == [x+dx,y+dy,z] if case['previous'] is not None
                            else case['controller'] == 'straight')
                if not straight:
                    outgoing = left if left in edges else right
        else:
            outgoing = left if left in edges else right
        assert outgoing in edges and outgoing == case['outgoing'], case['id']
    assert {r['incoming'] for r in handoff['guideCases']} == set(range(8))
    assert len(handoff['rampCases']) == 16
    for case in handoff['rampCases']:
        d = case['heading']; dx, dy = offsets[d]
        assert len(case['locations']) == len(case['patterns']) == len(case['zdiffs']) == 4
        for i, (point, pattern, dz) in enumerate(zip(case['locations'], case['patterns'], case['zdiffs'])):
            assert d in patterns[pattern]['connections']
            level = (pattern-28)%4; climb = ((pattern-28)%16)//4*2
            expected = 1 if level == 1 and d == climb else -1 if level == 2 and d == (climb+4)%8 else 0
            assert dz == expected, case['id']
            if i < 3:
                assert case['locations'][i+1] == [point[0]+dx,point[1]+dy,point[2]+dz]
    assert len(handoff['signalCases']) == 8
    assert {c['heading'] for c in handoff['signalCases']} == set(range(8))
    for case in handoff['signalCases']:
        difference = abs(case['heading']-case['signalDirection'])
        assert case['delegates'] == (min(difference,8-difference) <= 1)
    assert len(handoff['occupancyCases']) == 3
    for case in handoff['occupancyCases']:
        assert case['blocked'] == (case['destinationCar'] is not None)
    print('P4 reconciliation: 14 operations/decisions, 64 contributions, 78 translation dispositions; P4 accepted at browser/portable scope; performance deferred to P11.')
    print('P5 source witnesses: 17 guide, 16 ramp, 8 signal, 3 occupancy cases; movement runtime PENDING.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path)
    args = parser.parse_args()
    check(args.source_root)
