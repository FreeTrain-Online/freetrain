#!/usr/bin/env python3
"""Read-only P6-02 oracle. No production imports, legacy execution or writes.

Literals are independently authored source calculations, not runtime captures.
--source-root verifies original pinned Git blobs rather than working-tree files.
"""
import argparse
from copy import deepcopy
from functools import cache
import hashlib
import itertools
import json
import math
from pathlib import Path
import struct
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
FIXTURES = ROOT / 'doc/parity/station-fixtures.json'
HEADINGS = (0, 2, 4, 6)
MATERIALS = ('concrete', 'brown', 'green')
OFFSETS = ((0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1))


def require(ok, message):
    if not ok:
        raise ValueError(message)


class InvalidHostGraph(ValueError):
    pass


@cache
def read(name):
    return json.loads((ROOT / 'doc/parity' / name).read_text())


def digest(value):
    return hashlib.sha256(json.dumps(value, ensure_ascii=False, sort_keys=True,
                                     separators=(',', ':')).encode()).hexdigest()


def i32(n):
    if not -(2**31) <= n < 2**31:
        raise OverflowError('Int32')
    return n


def f32(n):
    return struct.unpack('<f', struct.pack('<f', n))[0]


def trunc(n):
    return i32(math.trunc(n))


def div(n, d):
    return abs(n) // abs(d) * (-1 if (n < 0) != (d < 0) else 1)


def move(p, h, n=1):
    dx, dy = OFFSETS[h]
    return [p[0] + dx*n, p[1] + dy*n, p[2]]


def thin(i):
    cells = [move(i['base'], i['heading'], n) for n in range(i['length'])]
    roof = [i['roof'] and i['length']//4 <= n < i['length']-i['length']//4
            for n in range(i['length'])]
    return dict(cells=cells, roof=roof, center=[], removedRails=cells,
                removalCharge=0, outlookPass='before' if i['heading'] in (2, 4) else 'after')


def wide(i):
    centers = [move(i['base'], i['heading'], n) for n in range(i['length'])]
    lanes = [[move(p, (i['heading'] + (2 if k == 0 else 6)) % 8) for p in centers]
             if i['lanes'][k] else [] for k in range(2)]
    return dict(center=centers, lanes=lanes, roof=False, removedRails=lanes[0]+lanes[1],
                removedCenters=centers, removalCharge=0, laneEditCharge=0,
                rectangle=[(i['heading']//2 % 2)*32, 0, 32, 32], anchor=[0, 16])


def placement(i):
    if i['kind'] == 'station':
        legal = all(c['reclaimable'] and c['ground'] == i['z'] and c['inBounds'] for c in i['cells'])
    else:
        legal = i['heading'] in HEADINGS and all(
            c['kind'] == 'empty' or (c['kind'] == 'single' and not c['car'] and
                                    c['axis'] == i['heading'] % 4) for c in i['cells'])
        if i['kind'] == 'wide':
            legal = legal and all(c == 'empty' for c in i['center'])
    return dict(legal=legal)


def station(i):
    c = next(c for c in read('station-review.json')['contributions'] if c['id'] == i['definition'] and c['type'] == 'station')
    declaration = ET.fromstring(c['declarationXml'])
    x,y = map(int,declaration.findtext('size').split(','))
    z = int(declaration.findtext('height'))
    return dict(size=[x,y,z], voxelCount=x*y*z, endExclusive=[i['base'][0]+x,i['base'][1]+y,i['base'][2]+z],
                price=int(declaration.findtext('price')), operationCost=int(declaration.findtext('operationCost')),
                demolition=i32(i32(500000*x)*y), demolitionGenre='SUBSIDIARIES',
                owned=True, reclaimable=False, entityValue=0, heightCutColor='Gray', constructionSite=False)


def removal(i):
    if i['kind'] == 'thin':
        removable = not any(i['cars'])
        return dict(canRemove=removable, restoredRailCount=i['length'] if removable else 0,
                    centerRemoved=0, charge=0, cancelTimer=removable)
    present = [n for n in range(2) if i['lanes'][n]]
    removable = all(not any(i['cars'][n]) for n in present)
    lane = i['editLane']
    can_edit = i['lanes'][lane] and not any(i['cars'][lane])
    return dict(canRemove=removable, restoredRailCount=len(present)*i['length'] if removable else 0,
                centerRemoved=i['length'] if removable else 0, canRemoveLane=can_edit,
                laneRestoredRailCount=i['length'] if can_edit else 0, charge=0, cancelTimer=removable)


def host_range(i):
    end = move(i['base'], i['heading'], i['length'])
    lo = [min(i['base'][k], end[k])-8 for k in (0, 1)]
    hi = [max(i['base'][k], end[k])+8 for k in (0, 1)]
    columns = {tuple(move(i['base'], i['heading'], n)[:2]) for n in range(i['length'])}
    candidates = set()
    for c in i['candidates']:
        if c['kind'] == 'self' or c['kind'] == 'disconnected':
            continue
        for x, y, z in c['voxels']:
            if 0 <= z < i['worldHeight'] and ((lo[0] <= x <= hi[0] and lo[1] <= y <= hi[1]
                    and abs(z-i['base'][2]) <= 1) or (x, y) in columns):
                candidates.add(c['id'])
    direct = sorted(c['id'] for c in i['candidates'] if c['kind'] == 'station' and c['id'] in candidates)
    return dict(otherEnd=end, rectangle=[lo, hi], zRange=[i['base'][2]-1, i['base'][2]+1],
                candidates=sorted(candidates), automaticHost=direct[0] if direct else None)


def host_graph(i):
    parents = deepcopy(i['parents'])
    stations = set(i['stations'])
    def valid(ok, message):
        if not ok:
            raise InvalidHostGraph(message)
    def resolves(node):
        seen = set()
        while node in parents:
            valid(node not in seen, 'Invalid host cycle')
            seen.add(node)
            node = parents[node]
        valid(node is None or node in stations, 'Dangling host')
        return node in stations
    for node in parents:
        resolves(node)
    node, target = i['node'], i['target']
    valid(node in parents and node != target, 'Invalid host target')
    valid(target is None or target in parents or target in stations, 'Dangling host target')
    calls = []
    def detach(n):
        if parents[n] is None:
            return
        parents[n] = None
        calls.append(n)
        for child in list(parents):
            if parents[child] == n:
                detach(child)
    if parents[node] != target:
        detach(node)
        if target is not None and resolves(target):
            parents[node] = target
    return dict(parents=parents, disconnected=calls,
                resolved={n: resolves(n) for n in parents})


def cost(i):
    kind = i['kind']
    if kind == 'platform':
        height = i32(i['z'] - i['ground'])
        multiplier = i32(height * -2 if height < 0 else height + 1)
        amount = 0 if i['ground'] <= i['z'] <= i['water'] and i['ground'] < i['water'] else i32(i32(500000*i['length'])*multiplier)
    elif kind == 'platformDaily':
        amount = i32(180000*i['length'])
    elif kind == 'demolish':
        amount = i32(i32(500000*i['x'])*i['y'])
    elif kind in ('lane', 'passage', 'stairs', 'platformRemove'):
        amount = 0
    else:
        amount = i['amount']
        require(0 <= amount < 2**63, 'Invalid Int64 price')
    return dict(amount=amount, genre='SUBSIDIARIES' if kind == 'demolish' else 'RAIL_SERVICE',
                booking=kind not in ('lane', 'passage', 'stairs', 'platformRemove'))


# Original StairPlatform constructor table. Aliases retain source anchors/crops;
# index23 deliberately remains outside the authored 336x153 sheets (EXAV-GAL-001).
STAIRS = (
    (10,80,32,32,6,16), (42,80,32,32,6,16), (10,80,32,32,6,16), (42,80,32,32,6,16),
    (0,120,32,32,0,16), (32,120,32,32,0,16), (0,120,32,32,0,16), (32,120,32,32,0,16),
    (80,76,32,36,0,20), (112,76,32,36,0,20), (192,4,32,36,0,20), (224,4,32,36,0,20),
    (90,120,32,32,6,16), (122,120,32,32,6,16), (90,120,32,32,6,16), (122,120,32,32,6,16),
    (166,120,32,32,-6,16), (198,120,32,32,-6,16), (166,120,32,32,-6,16), (198,120,32,32,-6,16),
    (176,76,32,36,0,20), (208,76,32,36,0,20), (256,4,32,36,0,20), (388,4,32,36,0,20),
    (256,120,32,32,0,16), (288,120,32,32,0,16), (256,120,32,32,0,16), (288,120,32,32,0,16),
    (246,80,32,32,-6,16), (278,80,32,32,-6,16), (246,80,32,32,-6,16), (278,80,32,32,-6,16))


def drawing(i):
    h = i['heading']
    if i['kind'] == 'stairs':
        selector = h*4 + (0 if i['up'] else 4) + 2*i['double'] + i['roof']
        x,y,w,height,ax,ay = STAIRS[selector]
    elif i['kind'] == 'thin':
        selector = h + i['roof']
        x,y,w,height,ax,ay = selector*32,0,32,24,0,8
    else:
        variant = 2 if i['bridge'] and i['double'] else 1 if i['double'] else 0
        selector = h//2*3 + variant
        x = (h//2 if variant == 0 else h + variant)*32
        y,w,height,ax,ay = 8 if variant == 0 else 40,32,32,0,16
    return dict(selector=selector, rectangle=[x,y,w,height], anchor=[ax,ay],
                validCrop=x+w <= (256 if i['kind'] == 'thin' else 336) and y+height <= 153,
                passOrder=['rail.before', 'outlook', 'car', 'rail.after'] if h in (2,4)
                    else ['rail.before', 'car', 'rail.after', 'outlook'],
                bridgeAfterCar=i['kind'] == 'passage' and i['bridge'] and h in (2,4))


def passage(i):
    if i['kind'] == 'stairs':
        up = True if i['forward'] else False if i['backward'] else None
        return dict(legal=up is not None, up=up, charge=0, previewMutations=[],
                    sourcePreviewFault='invalid-cast' if up is not None else None)
    legal = len(i['cells']) > 1 and all(c in ('empty','orthogonal') for c in i['cells'])
    outlooks = [None if c == 'empty' else
                not ((n == 0 and i['rightFacing']) or (n == len(i['cells'])-1 and not i['rightFacing']))
                for n,c in enumerate(i['cells'])] if legal else []
    return dict(legal=legal, bridges=outlooks, createdFloatingRails=0, charge=0,
                removedOutlook='plain', hostChanged=False)


def yard(i):
    pos = div(i['platformLength'] + i['trainLength'], 2)-1 if i['sameDirection'] else div(i['platformLength']-i['trainLength'], 2)
    calls = []
    result = 0
    if 0 <= pos < i['platformLength'] and pos == i['index'] and i['host']:
        result = i['result']
        calls = ['controller'] + i['handlerEffects']
        if i['callCount'] == 0 and result > 0:
            calls += ['unload']
        elif i['callCount'] != 0 and result <= 0:
            calls += ([] if result in (-999,-1000) else ['load']) + ['bell']
    return dict(stopPosition=pos, result=result, calls=calls)


def capacity(i):
    normal = seated = 0
    for c in i['cars']:
        normal = i32(normal+c['normal'])
        seated = i32(seated+(c['seated'] if 0 <= c['seated'] <= c['normal'] else c['normal']))
    packed = i32(seated+i32(i32(normal-seated)*2))
    return dict(normal=normal, seated=seated, packed=packed, outside=min(100,normal))


def boarding(i):
    gone, loaded, exported, trains = i['gone'], i['loaded'], i['exported'], i['trains']
    trace = []
    for action in i['actions']:
        if action == 'hour':
            gone = trunc(f32(f32(gone)*f32(.8)))
            loaded = trunc(f32(f32(loaded)*f32(.9996)))
            trace.append(dict(gone=gone, loaded=loaded, waiting=max(0,i['population']-gone)))
            continue
        trains = i32(trains+1)
        available = max(0,i32(i['population']-gone))
        count = 0 if i['population'] == 0 else min(i['packed'],trunc(f32(f32(f32(i32(available*i['amenity']))*f32(.01))*f32(.3))))
        gone, loaded, exported = i32(gone+count), i32(loaded+count), i32(exported+count)
        trace.append(dict(boarded=count, gone=gone, loaded=loaded, waiting=max(0,i['population']-gone),
                          loaderCalled=i['population'] != 0, origin=i['head'] if i['population'] != 0 else None))
    return dict(trace=trace, exported=exported, trains=trains)


def fare(i):
    distance = math.isqrt(sum((a-b)**2 for a,b in zip(i['source'],i['destination']))) if i['source'] is not None else None
    amount = i32(i32(i32(i['passengers']*i['fare'])*distance)*2) if distance is not None else 0
    return dict(distance=distance, revenue=amount, passengers=0, origin=None,
                secondRevenue=0, calls=['unload']+(['fare'] if distance is not None else [])+
                ['import','train-log','landQ','accumulator']+(['traffic'] if i['passengers'] else [])+['notify'])


def decay(i):
    factor = f32(f32(24)*f32(f32(1)-f32(.9996)))
    return dict(gone=trunc(f32(f32(i['gone'])*f32(.8))),
                loaded=trunc(f32(f32(i['loaded'])*f32(.9996))),
                unloaded=trunc(f32(f32(i['unloaded'])*f32(.9996))),
                averageLoaded=trunc(f32(f32(i['loaded'])*factor)),
                averageUnloaded=trunc(f32(f32(i['unloaded'])*factor)), factor=factor)


def tables(i):
    table = next(t for t in read('station-review.json')['populationTables'] if t['source'] == i['source'] and t['name'] == i['table'])
    return dict(percent=table['values'], scaled=[div(i32(i['base']*v),100) for v in table['values']])


def provider(i):
    p = i['provider']
    weekend = i['weekend'] or i['holiday'] or (p == 'SchoolPopulation' and i['vacation'])
    day = 'weekend' if weekend else 'weekday'
    divisor = 100
    if p in ('LeisureLandPopulation','LeisureHotelPopulation') and i['vacation'] and not weekend:
        day, divisor = 'weekend',125
    if p == 'BusinessInnPopulation':
        day = 'weekday'
    if p == 'BarPopulation':
        day = 'sunday' if i['holiday'] or i['dayOfWeek'] == 0 else 'saturday' if i['dayOfWeek'] == 6 else 'weekday'
    name = day + ('Entering' if i['entering'] else 'Distribution')
    values = next(t['values'] for t in read('station-review.json')['populationTables']
                  if t['source'].endswith('/'+p+'.cs') and t['name'] == name)
    residents = 1 if p == 'SchoolPopulation' else 10 if p in ('LeisureLandPopulation','LeisureHotelPopulation') else i['base']
    return dict(table=name, divisor=divisor, value=div(i32(i['base']*values[i['hour']]),divisor), residents=residents)


def nested(i):
    def calc(n):
        if n['kind'] == 'constant': return n['value']
        if n['kind'] == 'hourly': return div(i32(n['base']*n['percent']),100)
        if n['kind'] == 'multiplier': return i32(calc(n['child'])*n['factor'])
        if n['kind'] == 'persistent': return calc(n['child'])
        value = 0
        for child in n['children']: value = i32(value+calc(child))
        return value
    return dict(value=calc(i['tree']))


def listener(i):
    local, reverse, trace = [], [], []
    radius = 20 if i['special'] else 10
    for s in i['stations']:
        ds = sum(v*v for v in s['offset'])
        eligible = math.isqrt(ds) < radius if i['order'] == 'listener-after' else all(abs(v) <= radius for v in s['offset'])
        if eligible:
            if len(local) < 4:
                local.append(s['id']); reverse.append(s['id'])
            else:
                farthest, remove = math.isqrt(ds), None
                for old in local:
                    d = math.isqrt(sum(v*v for v in next(t for t in i['stations'] if t['id'] == old)['offset']))
                    if d > farthest: farthest, remove = d, old
                if remove is not None:
                    reverse.remove(remove); local[local.index(remove)] = s['id']
        trace.append(dict(local=local[:], reverse=reverse[:]))
    for removed in i['remove']:
        # Only registered reverse members receive source station-removal notification.
        if removed in reverse:
            reverse.remove(removed); local.remove(removed)
    return dict(trace=trace, local=local, reverse=reverse,
                danglingAfterSourceRemoval=sorted(set(local).intersection(i['remove'])))


def listener_demand(i):
    raw = div(i['raw'],i['stations'])
    cap = i32(i32(i32(i['land']*i['xy'])*max(1,i['z']//4))+10)
    return dict(ordinary=min(raw,cap), special=raw)


def timers(i):
    now = i['now']
    pending = [dict(handler='hour',due=now+60-now%60,period=60,order=1),
               dict(handler='day',due=now+1440-now%1440,period=1440,order=2),
               dict(handler='platform',due=now+1440,period=1440,order=3)]
    return dict(registeredImmediately=True, constructionSite=False, pending=pending,
                afterRemoval=[q for q in pending if q['handler'] not in i['cancel']])


def fifo(i):
    gone, today, yesterday, cash = i['gone'],i['today'],i['yesterday'],i['cash']
    trace = []
    for e in sorted(i['entries'], key=lambda e:(e['due'],e['order'])):
        if e['handler'] == 'hour': gone = trunc(f32(f32(gone)*f32(.8)))
        elif e['handler'] == 'day': cash -= i['stationCost']; yesterday,today = today,0
        elif e['handler'] == 'depart':
            n = min(i['packed'],trunc(f32(f32(f32(i32(max(0,i['population']-gone)*i['amenity']))*f32(.01))*f32(.3))))
            gone += n; today += n
        elif e['handler'] == 'platform': cash -= i32(180000*i['length'])
        # A waiting train neither boards nor pays an operating expense.
        trace.append(dict(handler=e['handler'],gone=gone,today=today,yesterday=yesterday,cash=cash))
    return dict(trace=trace)


def logs(i):
    week, last, trace = 0.0,0.0,[]
    for day,total in enumerate(i['totals']):
        week += total**.2
        if day == 6: last,week = (week/7)**5,0.0
        trace.append(dict(today=0,yesterday=total,thisWeek=week,lastWeek=last))
    return dict(trace=trace)


EVALUATORS = dict(thin=thin, wide=wide, placement=placement, stations=station, removal=removal, hostRange=host_range,
                  hostGraph=host_graph, costs=cost, drawing=drawing, passage=passage,
                  stopIndices=yard, yards=yard, capacity=capacity, boarding=boarding,
                  fare=fare, decay=decay, tables=tables, providers=provider, nested=nested,
                  listeners=listener, listenerDemand=listener_demand, timers=timers, fifo=fifo, logs=logs)


def evaluate(name, value):
    try:
        return EVALUATORS[name](value)
    except OverflowError:
        return dict(rejected='checked-source-width', published=False)
    except InvalidHostGraph:
        return dict(rejected='invalid-host-graph', published=False)


def required_ids():
    product = itertools.product
    ids = dict(
        thin={f'thin-{h}-{r}-{n}' for h,r,n in product(HEADINGS,(0,1),(1,3,4,8))},
        wide={f'wide-{h}-{a}-{b}' for h,a,b in product(HEADINGS,(0,1),(0,1))},
        placement={f'place-{n}' for n in range(16)},
        stations={c['id'] for c in read('station-review.json')['contributions'] if c['type'] == 'station'},
        removal={f'remove-thin-{h}-{occupied}' for h,occupied in product(HEADINGS,(0,1))}
            | {f'remove-wide-{h}-{a}-{b}-{occupied}' for h,a,b,occupied in product(HEADINGS,(0,1),(0,1),(0,1))},
        hostRange={f'range-{h}' for h in HEADINGS},
        hostGraph={f'graph-{n}' for n in range(8)},
        costs={f'cost-{n}' for n in range(19)},
        drawing={f'stair-{m}-{light}-{h}-{u}-{w}-{r}' for m,light,h,u,w,r in product(MATERIALS,('day','night'),HEADINGS,(0,1),(0,1),(0,1))}
            | {f'passage-{m}-{light}-{h}-{b}-{w}' for m,light,h,b,w in product(MATERIALS,('day','night'),HEADINGS,(0,1),(0,1))}
            | {f'draw-thin-{h}-{r}' for h,r in product(HEADINGS,(0,1))},
        passage={f'passage-edit-{m}-{n}' for m,n in product(MATERIALS,range(8))},
        stopIndices={f'stop-{n}-{d}' for n,d in product(range(3,7),(0,1))},
        yards={c['id'] for c in read('train-fixtures.json')['groups']['yards']['cases']},
        capacity={f'capacity-{n}' for n in range(7)}, boarding={f'board-{n}' for n in range(10)},
        fare={f'fare-{n}' for n in range(7)}, decay={f'decay-{n}' for n in range(8)},
        tables={t['source'].split('/')[-1][:-3]+'-'+t['name'] for t in read('station-review.json')['populationTables']},
        providers={f'provider-{p}-{flag}-{h}-{e}' for p,flag,h,e in product(
            sorted({t['source'].split('/')[-1][:-3] for t in read('station-review.json')['populationTables']}),
            range(5),(7,17),(0,1))},
        nested={f'nested-{n}' for n in range(4)}, listeners={f'listener-{n}' for n in range(14)},
        listenerDemand={f'demand-{n}' for n in range(5)}, timers={f'timer-{n}' for n in range(4)},
        fifo={f'fifo-{n}' for n in range(6)}, logs={'log-uniform','log-nonuniform'})
    return ids


# Reviewed source-input/mapping fingerprints complement the explicit matrix IDs.
# They exclude expected outputs: rehashing a corrupted document cannot conceal a
# changed input domain, source-family/owner mapping or unrelated scenario link.
# The read-only checker never generates or rewrites literals.
REVIEWED_INPUTS = {'thin': '41e1d19f44e124a948251717f08cdfa50f94d4e993d661acff25aced873b600b',
    'wide': '08cf02f35d51f46264acf53582b4eb90160c517326bfb4ff0b2fc0d22fe9b277',
    'placement': '5b8752535c5a95ebc9af663b2ef361476f5575a28d529cbb284632f4a7fd61a4',
    'stations': 'a6ffb7446b7012993d9e3fd5d3a2101fbbe857d6bdb4b7d66d31f02c6319f563',
    'removal': '46445c840b909aa5d0f75e1931bf4e8784c0c21784894201c4abfc020ab769d9',
    'hostRange': '1fa70015f272496dd42fe259e50390a2f209da00c9ec0360f8fddc26316132a6',
    'hostGraph': 'dc3bf540b03ae86c1cc82c1bbba734428ac5e15c08cd20bcb99e6a892a1814da',
    'costs': 'e831258b96261a458303969bbe68cd5b3a408d867008472257c912b548dd735d',
    'drawing': 'ef39469cc2841491f6cb8b2f7afdd3ba84d35da9f944bd5fb4381f507eccf35d',
    'passage': 'c4325d2d70aac61ae90bc3f85c9b320fb61e15e1793705a50ab7acce29914966',
    'stopIndices': 'ea6b0daaf272b01770c0d1387af57f230c4e51622e9ff0896923b9391877f7e7',
    'yards': '78fb5fb98aef9a88de0ec89d6e474866f42a5feb0472df4824ae169360838fa3',
    'capacity': '46dc256d1d688b93425c9b740fa51472ce58e087036fce7645974b3e69f36fd9',
    'boarding': '343793a442616bbea0217ad5ce2d9b45c919b52024fec7812a5b97f285d6c4e8',
    'fare': '55d52602c16a9546e2e945451b90ee8057d33061ea7ee7647ebc933ccdf84522',
    'decay': '7f3bf23c12d0cc3f51f59929916fba96fac966d77b74e73c29371cffbd8c54a6',
    'tables': '2c395a1f12296d3a7a0da9b5d30d4002c358adde19b016314b797f832fea7fc9',
    'providers': '2c5986f001c69bf36dbdc0484e5203d23ff1f78b6e05e66664d5c660d5ad9d76',
    'nested': '17af6c8009dc33214ef948e0d27ca88dda812c874189d888387cbd48f24fdabe',
    'listeners': '2761d3195c2da267ecf694a198c47b0ff2bc0b488821c71e9e6681ff71117d92',
    'listenerDemand': '314dfd10e366715eabd08bf56ada4e12602a4f832f098936bfacb942ab3989d6',
    'timers': '28316b9f89e42a9828ccdbf2ef2f2990af47bc550a4110603d9f91ac052802fc',
    'fifo': '73fb7baba374f643b57916c48d1a46d62a9da0d03d19c110bd4f49266e92bd4f',
    'logs': '8484f286bd2c9a2c07a0512f8f64bcee862e35a4fa2dabf921ad5909ff427b65'}
SCENARIO_LINKS_SHA256 = '6a4960af8646e904231a52aca022797e76b9bcef7e4225cd931bccead81fa0d7'


def equal(actual, expected, approximate=False):
    if isinstance(actual, dict) and isinstance(expected, dict):
        return actual.keys() == expected.keys() and all(equal(actual[k],expected[k],approximate) for k in actual)
    if isinstance(actual,list) and isinstance(expected,list):
        return len(actual) == len(expected) and all(equal(a,b,approximate) for a,b in zip(actual,expected))
    if approximate and isinstance(actual,float) and type(expected) in (float,int):
        return math.isclose(actual,expected,rel_tol=1e-12,abs_tol=1e-12)
    return type(actual) is type(expected) and actual == expected


def validate(data, source_root=None):
    review = read('station-review.json')
    require(data['schemaVersion'] == 1 and data['slice'] == 'P6-02' and data['status'] == 'independent-source-fixtures' and
            data['runtimeStatus'] == 'station-gameplay-unimplemented' and data['authority'] == review['authority'], 'Fixture authority/status drift')
    require(data['sha256'] == digest({k:v for k,v in data.items() if k != 'sha256'}), 'Fixture digest drift')
    require(set(data['inputs']) == {'station-review.json','train-fixtures.json','exav-scenarios.json','clock-review.json'}, 'Missing prerequisite')
    for path,sha in data['inputs'].items():
        require(hashlib.sha256((ROOT/'doc/parity'/path).read_bytes()).hexdigest() == sha, 'Input artifact drift: '+path)
    known = {s['path']:s for s in review['sourceReviews']}
    sources = {s['path']:s for s in data['sources']}
    require(len(sources) == len(data['sources']), 'Duplicate source')
    for path,s in sources.items():
        require(s == known.get(path), 'Unreviewed source: '+path)
        if source_root:
            raw = subprocess.check_output(['git','-C',str(source_root),'show',data['authority']['revision']+':trunk/'+path])
            require(hashlib.sha256(raw).hexdigest() == s['sha256'], 'Pinned source drift: '+path)
            lines = raw.decode(s['encoding']).splitlines()
            for b in s['bindings']: require(lines[b['line']-1] == b['text'], 'Pinned excerpt drift: '+path)
    required = required_ids()
    require(data['groups'].keys() == required.keys(), 'Missing/unknown family')
    ids, bound, count = set(),set(),0
    for name,g in data['groups'].items():
        require(g['derivation'] and g['sources'] and set(g['sources']) <= sources.keys(), 'Missing derivation: '+name)
        require(g['runtimeOwner'].startswith('P6-') and g['decisions'] and
                set(g['decisions']) <= {d['id'] for d in review['decisions']}, 'Unbound owner/decision')
        bound.update(g['sources'])
        signature = dict(metadata={k:v for k,v in g.items() if k != 'cases'},
                         inputs=sorted([dict(id=c['id'],input=c['input']) for c in g['cases']],key=lambda c:c['id']))
        case_ids = [c['id'] for c in g['cases']]
        require(len(set(case_ids)) == len(case_ids) and set(case_ids) == required[name], 'Incomplete fixture matrix: '+name)
        require(digest(signature) == REVIEWED_INPUTS[name], 'Reviewed input/mapping drift: '+name)
        require(not ids.intersection(case_ids), 'Duplicate global case ID'); ids.update(case_ids)
        for c in g['cases']:
            before = deepcopy(c['input'])
            require(equal(evaluate(name,c['input']),c['expected'],approximate=name == 'logs'), 'Fixture mismatch: '+name+'/'+c['id'])
            require(before == c['input'], 'Oracle mutated input'); count += 1
    require(bound == sources.keys(), 'Orphan source binding')
    require(data['groups']['yards']['cases'] == read('train-fixtures.json')['groups']['yards']['cases'], 'Yard handoff drift')
    scenarios = {s['id']:s for s in read('exav-scenarios.json')['scenarios']}
    require(data['scenarioBindings'].keys() == {s['id'] for s in review['scenarioBindings']}, 'Missing scenario binding')
    for sid,b in data['scenarioBindings'].items():
        require(b['input'] == scenarios[sid]['input'] and b['expected'] == scenarios[sid]['expected'] and
                b['cases'] and set(b['cases']) <= ids and b['scope'] in ('P6 fixture','cross-phase companion'), 'Scenario binding drift: '+sid)
    require(data['differenceBindings'] == [dict(source=read('exav-scenarios.json')['differences'][0],
            decision='STATION-D003', cases=[f'listener-{n}' for n in range(14)],
            runtimeOwner='P6-16/P6-25/P7')], 'Listener difference binding drift')
    require(digest(data['scenarioBindings']) == SCENARIO_LINKS_SHA256, 'Reviewed scenario link drift')
    return count,len(sources)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check',action='store_true')
    parser.add_argument('--source-root',type=Path)
    args = parser.parse_args()
    count,sources = validate(json.loads(FIXTURES.read_text()),args.source_root)
    print(f'P6-02: {count} literal cases, {len(EVALUATORS)} families, {sources} pinned sources; production consumers remain with the implementing P6 slices.')


if __name__ == '__main__':
    main()
