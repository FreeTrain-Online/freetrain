#!/usr/bin/env python3
"""Check maintained P0-06 specifications against explicit pinned ExAV Git objects.

This checks source provenance, fixture references, selected independent arithmetic
and rail recipe geometry. It neither runs ExAV nor tests the modern simulation.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import struct
import subprocess

ROOT = Path(__file__).resolve().parents[1]
PARITY = ROOT / 'doc/parity'


def require(condition, message):
    if not condition:
        raise ValueError(message)


def f32(value):
    return struct.unpack('f', struct.pack('f', value))[0]


def check_numbers(rows, profiles):
    by_id = {row['id']: row for row in rows}

    def case(number):
        row = by_id[f'EXAV-SCN-{number:03}']
        return row['input'], row['expected']

    i, e = case(10)
    seated = [n if 0 <= n <= i['carCapacity'] else i['carCapacity'] for n in i['seatedCases']]
    require(seated == e['seatedCapacity'], 'Seated capacity vector')
    require([2*i['carCapacity']-n for n in seated] == e['packedCapacity'], 'Packed capacity vector')
    i, e = case(11)
    gone = i['gone']
    events, boarded = [], []
    for event in i['sequence']:
        if event == 'hourly decay':
            gone = int(f32(gone*f32(0.8)))
        else:
            available = max(0, i['population']-gone)
            count = min(i['packedCapacity'], int(f32(f32(f32(available*i['amenity'])*f32(0.01))*f32(0.3))))
            gone += count
            boarded.append(count)
        events.append(gone)
    require(events == e['goneAfterEachEvent'] and boarded == e['boarded'], 'Boarding/recovery trace')
    require([i['population']-n for n in events] == e['waitingAfterEachEvent'], 'Waiting trace')
    require(sum(boarded) == e['exportToday'], 'Export total')
    require(int(f32(e['loadedAccumulatorBeforeDecay']*f32(0.9996))) == e['loadedAccumulatorAfterDecay'], 'Loaded decay')
    i, e = case(12)
    counts = [min(i['packedCapacity'], int(f32(f32(f32(i['available']*a)*f32(0.01))*f32(0.3)))) for a in i['amenities']]
    require(counts == e['boarded'], 'Amenity vector')
    i, e = case(13)
    distance = math.isqrt(sum((a-b)**2 for a, b in zip(i['source'], i['destination'])))
    require(distance == e['distance'] and i['passengers']*i['fare']*distance*2 == e['revenue'], 'Fare vector')
    i, e = case(14)
    charges = [n+1 for n in range(i['moves']) if (i['initialMoveCount']+n) % 4 == 0]
    cost = (i['cars']*20+i['passengers']//20)*2000
    require(charges == e['chargedMoveNumbers'] and cost == e['costPerCharge'], 'Operating charge vector')
    spent = len(charges)*cost
    require(spent == e['operatingExpenditure'] and i['cashBefore']+i['fareReceiptAfterMoves']-spent == e['cashAfter'], 'Integrated cash trace')
    i, e = case(18)
    values = [min(i['patternPopulation']//i['attachedStations'], i['landValue']*i['xySize']*max(1,z//4)+10) for z in i['zSizeCases']]
    require(values == e['ordinaryPopulationPerStation'], 'Footprint/height population cap')
    i, e = case(26)
    require([i['compositionPrice']//(days//365+2) for days in i['ageDays']] == e['resaleRailServiceRevenue'], 'Resale vector')
    i, e = case(27)
    years = [days//365 for days in i['termDays']]
    require([int(i['principal']*(y/400)*y) for y in years] == e['depositInterestDue'], 'Deposit interest vector')
    require([int(i['principal']*(0.045+y/200)*y) for y in years] == e['debtInterestDue'], 'Debt interest vector')
    i, e = case(29)
    fee = i['currentPriceU']*i['buy']//100
    spend = i['currentPriceU']*i['buy']+fee
    price = i['currentPriceU']+i['currentPriceU']*i['buy']//i['numTotal']
    sell_fee = price*i['sell']//100
    earned = price*i['sell']-sell_fee
    require((fee, spend, price, sell_fee, earned) == (e['buyFee'], e['buySpend'], e['priceAfterBuy'], e['sellFee'], e['sellEarn']), 'Stock transaction trace')
    require(price-price*i['sell']//i['numTotal'] == e['priceAfterSell'] and i['cash']-spend+earned == e['cashAfter'], 'Stock price/cash trace')

    # Independent geometric sanity check, not source placement or content legality.
    vertices = profiles['railRecipe']['octagonVerticesXY']
    cells, directions = [], []
    for a, b in zip(vertices, vertices[1:]+vertices[:1]):
        dx, dy = b[0]-a[0], b[1]-a[1]
        require(dx == 0 or dy == 0 or abs(dx) == abs(dy), 'Ring edge must be cardinal or diagonal')
        count = max(abs(dx), abs(dy))
        step = (0 if dx == 0 else dx//abs(dx), 0 if dy == 0 else dy//abs(dy))
        directions.append(step)
        cells.extend((a[0]+step[0]*n, a[1]+step[1]*n) for n in range(count))
    require(len(cells) == len(set(cells)) == 36, 'Ring must have 36 unique rail cells')
    compass = [(0,-1),(1,-1),(1,0),(1,1),(0,1),(-1,1),(-1,0),(-1,-1)]
    for a, b in zip(directions[-1:]+directions[:-1], directions):
        delta = (compass.index(b)-compass.index(a)) % 8
        require(min(delta, 8-delta) == 1, 'Ring corners must turn 45 degrees')
    for profile in profiles['profiles']:
        x, y, z = profile['dimensions']
        safe_rows = (y-2*z)//32
        require(profile['trains'] <= 2*(x//16)*safe_rows, 'Insufficient safe ring modules')
        # Convert each local X,Y ring into source H,V storage coordinates.
        # Even anchor V means local h=floor((x+y)/2), v=y-x.
        placed = set()
        for column in range(x//16):
            for row in range(safe_rows):
                for local_x, local_y in cells:
                    h = 16*column+(local_x+local_y)//2
                    v = 16+32*row+local_y-local_x
                    elevation = profiles['railRecipe']['elevation']
                    require(0 <= h < x and 2*elevation <= v < y-2*(z-elevation), 'Source train border violation')
                    require((h,v) not in placed, 'Overlapping ring modules')
                    placed.add((h,v))
        require(profile['cars'] % profile['trains'] == 0 and 2*(profile['cars']//profile['trains']) < len(cells), 'Invalid ring formation budget')
        require(0 <= profiles['railRecipe']['elevation'] < z and all(0 <= a < 16 and 0 <= b < 16 for a,b in cells), 'Ring bounds')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path, required=True, help='Archive repository; never inferred from a sibling path')
    parser.add_argument('--check', action='store_true', help='Explicit read-only check (also the default)')
    args = parser.parse_args()
    config = json.loads((PARITY/'exav-baseline.json').read_text())
    document = json.loads((PARITY/'exav-scenarios.json').read_text())
    for key in ('sourceId', 'revision', 'subtree', 'engineVersion'):
        require(document[key] == config[key], 'Scenario baseline mismatch: '+key)
    source_rows = document['sourceReviews']
    sources = {r['path']:r for r in source_rows}
    require(len(sources) == len(source_rows), 'Duplicate source review')
    for path, row in sources.items():
        raw = subprocess.check_output(['git','-C',str(args.source_root),'show',f"{config['revision']}:{config['subtree']}/{path}"])
        require(hashlib.sha256(raw).hexdigest() == row['sha256'], 'Source hash drift: '+path)
        try:
            text = raw.decode('utf-8-sig')
        except UnicodeDecodeError:
            text = raw.decode('cp932')
        require(all(symbol in text for symbol in row['symbols']), 'Missing source symbol: '+path)
    rows = document['scenarios']
    ids = {r['id'] for r in rows}
    require(len(ids) == len(rows), 'Duplicate scenario ID')
    differences = {r['id'] for r in document['differences']}
    for row in rows:
        require(row['status'] == 'specified' and row['expected'] and row['input'] and row['derivation'], 'Incomplete or overclaimed scenario')
        for ref in row['sources']:
            require(ref['symbol'] in sources[ref['path']]['symbols'], 'Unreviewed source reference')
        if 'difference' in row:
            require(row['difference'] in differences, 'Missing difference disposition')
    for path in document['relatedArtifacts']:
        require((ROOT/path).is_file(), 'Missing related artifact: '+path)
    manual = (ROOT/'doc/web-parity-manual.md').read_text()
    workflows = document['bilingualWorkflows']
    require(len({r['id'] for r in workflows}) == len(workflows), 'Duplicate locale workflow ID')
    for row in workflows:
        require(set(row['scenarioIds']) <= ids and row['locales'] == ['en','ja'], 'Broken bilingual scenario reference')
        require(all(f'| {identity} |' in manual for identity in row['manualRequirements']), 'Broken manual reference')
    check_numbers(rows, document['capacityProfiles'])
    print(f"P0-06 specification check passed: {len(rows)} scenarios, {len(sources)} pinned files, {len(workflows)} bilingual workflows; arithmetic and ring geometry consistent. Runtime, city legality and capacity remain unverified.")


if __name__ == '__main__':
    main()
