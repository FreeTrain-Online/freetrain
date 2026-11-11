#!/usr/bin/env python3
"""Validate and summarize P6-26 evidence; report budget misses without waivers."""
import argparse
import hashlib
import json
import math
import platform
import re
import subprocess
from pathlib import Path

MODES = ('small', 'medium', 'large', 'extent')

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def stats(values):
    a = sorted(values)
    return dict(count=len(a), p50=a[math.ceil(.5*len(a))-1], p95=a[math.ceil(.95*len(a))-1], max=a[-1]) if a else dict(count=0)

def command(*args):
    return subprocess.check_output(args, text=True).strip()

def environment():
    result = dict(os=platform.platform(), architecture=platform.machine(),
                  dotnet=command('dotnet', '--version'), deno=command('deno', '--version'))
    if platform.system() == 'Darwin':
        result['hardware'] = {key: command('sysctl', '-n', key) for key in ('hw.model', 'machdep.cpu.brand_string', 'hw.memsize', 'hw.logicalcpu')}
        result['macOS'] = command('sw_vers', '-productVersion')
    return result

def summarize(root):
    result = dict(schemaVersion=1, date='2026-11-11', scope='P6-26 controlled station/passenger resource measurements; P7 providers and P11 full-city/native/long-soak acceptance remain open',
                  environment=environment(), budgets=dict(frameP95Ms=1000/30, localFeedbackP95Ms=100, rtt100FeedbackP95Ms=250, minimumClockProgressFraction=.9),
                  fixtures={}, browser={}, misses=[])
    def miss(identifier, mode, reason, actual, target):
        result['misses'].append(dict(id=identifier, scene=mode, owner='P11 integrated station/resource performance', reason=reason, actual=actual, target=target))
    for mode, rings in zip(MODES, (4,16,64,64)):
        folder = root / mode
        path = folder / 'measurement.json'
        f = json.loads(path.read_text())
        assert digest(folder/'world.json') == f['saveSha256']
        assert digest(folder/'railway/world.json') == f['railwaySha256']
        assert digest(Path('src/FreeTrain.Online.Content/bundled-v2.json')) == f['contentSha256']
        assert digest(Path('doc/parity/station-fixtures.json')) == f['fixtureSha256']
        assert (f['stations'],f['platforms'],f['trains'],f['cars'],f['providers'],f['callbacks']) == (rings*2,rings*2,rings,rings*3,rings*8,rings*7)
        assert len(f['ticks']) == 120 and int(f['imports']) > 0 and int(f['departures']) > 0
        f['tickMs'] = stats([t['ms'] for t in f['ticks']])
        f['hourBurstMs'] = stats([t['ms'] for t in f['ticks'] if t['hour'] and not t['midnight']])
        f['midnightBurstMs'] = stats([t['ms'] for t in f['ticks'] if t['midnight']])
        f['maximumDueCallbacks'] = max(t['due'] for t in f.pop('ticks'))
        for key in ('buildMs','attachMs','churnMs','previewMs','editMs','waiting'):
            f[key] = stats(f[key])
        f.update(rawReport=str(path), rawReportSha256=digest(path), expectedSha256=digest(folder/'world.json.expected'))
        result['fixtures'][mode] = f
        path = root/'browser'/f'{mode}.json'
        b = json.loads(path.read_text())
        assert b['fixtureSha256'] == f['saveSha256'] and not b['errors']
        assert len(b['windows']) == 2 and len(b['coldMs']) == 3
        world = json.loads((folder/'world.json').read_text())
        initial_tick = int(world['clock']['absoluteMinute'])
        for w in b['windows']:
            assert w['elapsedMs'] >= 60000 and w['frameMs']['count'] > 0
            assert len(w['stationStats']) == rings*2
            entries = w.pop('stationStats')
            w['stationCount'] = len(entries)
            w['stationCounterTotals'] = {group:{key:sum(e[group][key] for e in entries) for key in entries[0][group]}
                                         for group in ('passengers','exports','imports','trains')}
            wire = w.pop('trace')['wire']
            ticks = [(e['at'],int(e['tick'])) for e in wire if e['type']=='delta' and e.get('tick') is not None]
            changed = [t for i,t in enumerate(ticks) if not i or t[1]!=ticks[i-1][1]]
            w['tickDeliveryIntervalMs'] = stats([b[0]-a[0] for a,b in zip(changed,changed[1:])])
            incoming = [e for e in wire if e['type'] != 'sent']
            w['receivedWebSocketBytes'] = sum(e['bytes'] for e in incoming)
            w['receivedBytesPerSecond'] = w['receivedWebSocketBytes']/(w['elapsedMs']/1000)
            w['largestMessageBytes'] = max((e['bytes'] for e in incoming), default=0)
            w['advancedMinutes'] = int(w['finalTick'])-initial_tick
            w['nominalMinutesAtSlowestRate'] = w['elapsedMs']/400 if w['running'] else 0
            if w['running']:
                assert min(w['moves']) > 0 and w['advancedMinutes'] > 3
                if w['advancedMinutes'] < .9*w['nominalMinutesAtSlowestRate']:
                    miss('STATION-CAP-001',mode,'Progress below 90% of slowest nominal rate',w['advancedMinutes'],w['nominalMinutesAtSlowestRate'])
            else:
                assert not any(w['moves']) and w['advancedMinutes']==0
            if w['frameMs']['p95'] > 1000/30:
                miss('STATION-CAP-003',mode,f'Frame interval p95 (running={w["running"]})',w['frameMs']['p95'],1000/30)
        b['feedbackCoverage'] = {}
        for network, limit in (('local',100),('rtt100',250)):
            succeeded = b['feedback'][network]['count']
            failed = sum(e['network']==network for e in b.get('feedbackFailures',[]))
            b['feedbackCoverage'][network] = dict(requested=30,succeeded=succeeded,failed=failed,unmeasured=30-succeeded-failed)
            assert 0 <= succeeded+failed <= 30
            assert b['feedback'][network]['count']==30 or any(e['network']==network for e in b.get('feedbackFailures',[]))
            if b['feedback'][network].get('p95',0) >= limit:
                miss('STATION-CAP-002',mode,f'{network} feedback p95 includes driver overhead',b['feedback'][network]['p95'],limit)
        for failure in b.get('feedbackFailures',[]):
            miss('STATION-CAP-004',mode,'Feedback unresolved/timed out; phase aborted and accepted paused scene restored',failure['elapsedMs'],15000)
            failure.pop('wire',None)
        memory = b.pop('memorySamples')
        b['sampledServerPeakRss'] = max(m['serverRss'] for m in memory)
        b['heapAfterGC'] = memory[-1]['metrics'].get('JSHeapUsedSize')
        b['coldMs'] = stats(b['coldMs'])
        b['stationPreviewMs'] = stats(b['stationPreviewMs'])
        b['feedback'].pop('raw')
        b.pop('images')
        b.update(rawReport=str(path), rawReportSha256=digest(path), screenshotSha256=digest(root/'browser'/f'{mode}.png'))
        result['browser'][mode] = b
    files = [Path('tools/TerrainTransportProbe/StationCapacity.cs'),Path('tools/TerrainTransportProbe/RailwayCapacity.cs'),
             Path('web/tests/browser/capacity-harness.ts'),Path('web/tests/browser/station-capacity.spec.ts'),
             Path('web/playwright.station-capacity.config.ts'),Path('tests/FreeTrain.Online.Tests/StationCapacityTests.cs'),Path('scripts/summarize-station-capacity.py'),
             Path('web/public/content/bundled-v2/catalog.json'),
             Path('src/FreeTrain.Online.Server/bin/Release/net10.0/FreeTrain.Online.Server.dll')]
    files += sorted(Path('src/FreeTrain.Online.Server/bin/Release/net10.0').glob('FreeTrain*.dll'))
    files += [Path('web/dist/index.html')] + sorted(Path('web/dist/assets').glob('*'))
    result['diagnosticFailures'] = {str(p):digest(p) for p in sorted((root/'diagnostics').glob('*.md'))}
    if result['diagnosticFailures']:
        result['misses'].append(dict(id='STATION-CAP-004', scenes=['large','extent'], owner='P11 command/publication performance', reason='Initial normal-rate pause became unresolved in both 128-station scenes; diagnostic assertions timed out after 60 seconds'))
    result['validation'] = {}
    for key, name in (('dotnetCapacity','p6-26-isolated.log'), ('dotnetStationSaveRegression','p6-26-regression.log'), ('client','p6-26-client.log')):
        path = Path('tmp')/name
        log = path.read_text()
        pattern = r'ok \| (\d+) passed \| 0 failed' if key == 'client' else r'Failed:\s+0, Passed:\s+(\d+), Skipped:\s+0'
        matched = re.search(pattern,log)
        assert matched, f'No clean passing count in {path}'
        result['validation'][key] = dict(passed=int(matched[1]), log=str(path), sha256=digest(path))
    result['validation']['chromiumReports'] = len(result['browser'])
    result['buildAndHarnessHashes'] = {str(p):digest(p) for p in files if p.is_file()}
    return result

if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root',type=Path,default=Path('tmp/p6-26-capacity'))
    parser.add_argument('--output',type=Path,default=Path('doc/parity/station-capacity-results.json'))
    args=parser.parse_args()
    args.output.write_text(json.dumps(summarize(args.root),indent=2)+'\n')
