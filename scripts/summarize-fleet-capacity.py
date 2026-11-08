#!/usr/bin/env python3
"""Summarize P5-28 probe/browser evidence without promoting missed targets."""
import argparse
import hashlib
import json
import math
from pathlib import Path

MODES = ('small', 'medium', 'large', 'network', 'empty')
BUDGETS = ((8, 32), (32, 256), (128, 1024), (2, 16), (128, 1024))


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def stats(values):
    a = sorted(values)
    return dict(count=len(a), p50=a[math.ceil(len(a)*.5)-1],
                p95=a[math.ceil(len(a)*.95)-1], max=a[-1]) if a else dict(count=0)


def summarize(root):
    env = json.loads((root / 'environment.json').read_text())
    # Machine identifiers do not help reproduce a resource measurement.
    env['hardware'] = '\n'.join(line for line in env['hardware'].splitlines()
                                if not any(s in line for s in ('Serial Number', 'UUID', 'UDID')))
    result = dict(schemaVersion=1, date='2026-11-08',
                  scope='P5-28 legal fleet/railway measurements; no passenger/growth/full-city capacity',
                  environment=env, fixtures={}, browser={}, misses=[],
                  validation=dict(dotnetPassed=381, clientPassed=53, chromiumPassed=5, chromiumCaptureRecheckPassed=2,
                                  dotnetCapacityRecheckPassed=6,
                                  productionBuild=True, fullSuite=False))
    for mode, (trains, cars) in zip(MODES, BUDGETS):
        folder = root / mode
        fixture = json.loads((folder / 'measurement.json').read_text())
        world = json.loads((folder / 'world.json').read_text())
        assert digest(folder / 'world.json') == fixture['saveSha256']
        assert (fixture['trains'], fixture['cars'], fixture['callbacks']) == (trains, cars, trains)
        assert all(m > 0 for m in fixture['moves'])
        # Derive actual occupied components/recipes from accepted state, even
        # when reading an older version of the report's descriptive metadata.
        bound = fixture.pop('trainsBound')
        fixture.pop('rings', None)
        fixture['constructedRings'] = 0 if mode == 'network' else {'small': 4, 'medium': 16}.get(mode, 64)
        fixture['occupiedRings'] = fixture['ringTrains'] // 2
        fixture['definition'] = bound[0]['definition']
        fixture['carRecipes'] = list({c['definition']['recipeId']: c['definition']
                                     for t in bound for c in t['cars']}.values())
        fixture['tickMs'] = stats(fixture['tickMs'])
        fixture['rawReport'] = str(folder / 'measurement.json')
        fixture['rawReportSha256'] = digest(folder / 'measurement.json')
        result['fixtures'][mode] = fixture
        browser_path = root / 'browser' / (mode + '.json')
        b = json.loads(browser_path.read_text())
        assert b['fixtureSha256'] == fixture['saveSha256'] and not b['errors']
        assert len(b['windows']) == 2 and len(b['coldMs']) == 3
        start_tick = int(world['clock']['absoluteMinute'])
        windows = []
        for w in b['windows']:
            assert w['elapsedMs'] >= 60000
            if w['running']:
                assert min(w['moves']) > 0
            else:
                assert not any(w['moves'])
            events = w.pop('trace')['wire']
            ticks = [(e['at'], int(e['tick'])) for e in events
                     if e['type'] == 'delta' and e.get('tick') is not None]
            changed = [t for i, t in enumerate(ticks) if not i or t[1] != ticks[i-1][1]]
            w['tickDeliveryIntervalMs'] = stats([b[0]-a[0] for a, b in zip(changed, changed[1:])])
            w['advancedMinutes'] = int(w['finalTick']) - start_tick
            w['nominalMinutesAtSlowestRate'] = w['elapsedMs'] / 400 if w['running'] else 0
            incoming = [e for e in events if e['type'] != 'sent']
            w['receivedWebSocketBytes'] = sum(e['bytes'] for e in incoming)
            w['receivedWebSocketBytesPerSecond'] = w['receivedWebSocketBytes'] / (w['elapsedMs']/1000)
            w['largestWebSocketMessageBytes'] = max((e['bytes'] for e in incoming), default=0)
            windows.append(w)
            if w['running'] and w['advancedMinutes'] < .9*w['nominalMinutesAtSlowestRate']:
                result['misses'].append(dict(id='FLEET-CAP-001', scene=mode, owner='P11 simulation/resource performance',
                                             reason='Observed simulation progression below 90% of slowest nominal rate',
                                             actual=w['advancedMinutes'], nominal=w['nominalMinutesAtSlowestRate']))
            if w['frameMs']['p95'] > 1000/30:
                result['misses'].append(dict(id='FLEET-CAP-003', scene=mode, running=w['running'],
                                             owner='P11 rendering/resource performance', reason='Frame interval p95 exceeds 33.33ms',
                                             actualMs=w['frameMs']['p95']))
        for key, limit in (('local', 100), ('rtt100', 250)):
            assert b['feedback'][key]['count'] == 30
            if b['feedback'][key]['p95'] >= limit:
                result['misses'].append(dict(id='FLEET-CAP-002', scene=mode, network=key,
                                             owner='P11 command/publication performance',
                                             reason='Complete authority feedback p95 exceeds provisional target; includes driver overhead',
                                             actualMs=b['feedback'][key]['p95'], targetMs=limit))
        memory = b.pop('memorySamples')
        b['sampledServerPeakRss'] = max(m['serverRss'] for m in memory)
        b['heapAfterGC'] = memory[-1]['metrics'].get('JSHeapUsedSize')
        b['coldMs'] = stats(b['coldMs'])
        b['feedback'].pop('raw')
        b.pop('images')
        b['rawReport'] = str(browser_path)
        b['rawReportSha256'] = digest(browser_path)
        result['browser'][mode] = b
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path('tmp/p5-28-capacity'))
    parser.add_argument('--output', type=Path, default=Path('doc/parity/fleet-capacity-results.json'))
    args = parser.parse_args()
    args.output.write_text(json.dumps(summarize(args.root), indent=2) + '\n')
