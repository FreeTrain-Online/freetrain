#!/usr/bin/env python3
"""Extract the pinned rail-pattern table and render its raw sprite review gallery."""
import argparse
import html
import importlib.util
import json
from pathlib import Path
import re
import tempfile


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


HERE = Path(__file__).resolve().parent
inventory = load('parity_inventory', HERE / 'inventory-parity.py')
images = load('content_bitmap', HERE / 'import-web-content.py')
semantics = load('parity_geometry', HERE / 'parity_geometry.py')


def extract(tree):
    path = 'core/world/rail/RailPattern.cs'
    source = inventory.strip_comments(tree.text(path)[0])
    records = []
    for index, a, b in re.findall(r'RailPattern\.createNormal\(\s*(\d+),\s*(\d+),\s*(\d+)\s*\)', source):
        index, a, b = map(int, (index, a, b))
        records.append(dict(id=index, family='normal', sheet='RailRoads.bmp',
                            rectangle=[index * 32, 0, 32, 16], anchor=[0, 0], connections=[a, b]))
    for index, a, b, offset in re.findall(r'RailPattern\.createJunction\(\s*(\d+),\s*(\d+),\s*(\d+),\s*([+-]?\d+)\s*\)', source):
        index, a, b, offset = map(int, (index, a, b, offset))
        records.append(dict(id=12 + index, family='junction', sheet='RailRoads.bmp',
                            rectangle=[index * 32, 16, 32, 16], anchor=[0, 0],
                            connections=[a, b, (b + offset + 8) % 8]))
    for kind, index, up, zdiff, zangle, visible in re.findall(r'RailPattern\.(createSlope|createUGSlope)\(\w+,\s*(\d+),\s*(\d+),\s*([+-]?\d+),\s*(\d+),\s*(true|false)\s*\)', source):
        index, up, zdiff, zangle = map(int, (index, up, zdiff, zangle))
        visible = visible == 'true'
        records.append(dict(id=(28 if kind == 'createSlope' else 44) + index,
                            family='slope' if kind == 'createSlope' else 'underground-slope',
                            sheet='RailRoads.bmp' if kind == 'createSlope' else 'ugslope.bmp',
                            rectangle=[index * 32 if visible else 384, 32 if visible else 0, 32 if visible else 1, 32 if visible else 1],
                            anchor=[0, 16], connections=[up, (up + 4) % 8],
                            climb=up, level=index % 4, zdiff=zdiff, zangle=zangle, visible=visible))
    for sheet, index in re.findall(r'RailPattern\.createSlopeSupport\((railChips|ugChips),\s*(\d+)\s*\)', source):
        index = int(index)
        family = 'slope-support' if sheet == 'railChips' else 'underground-slope-support'
        records.append(dict(id=f'{family}-{index}', family=family,
                            sheet='RailRoads.bmp' if sheet == 'railChips' else 'ugslope.bmp',
                            rectangle=[index * 32, 32, 32, 32], anchor=[0, 16], connections=[]))
    directions = [dict(index=int(index), name=name, offset=[int(x), int(y)]) for x, y, name, index in
                  re.findall(r'new Direction\(\s*(-?\d+),\s*(-?\d+),\s*"([^"]+)"\s*,\s*(\d+)\)',
                             inventory.strip_comments(tree.text('core/world/Direction.cs')[0]))]
    if len(records) != 76 or len(directions) != 8:
        raise ValueError('Pinned geometry table changed; review extraction')
    return dict(schemaVersion=1, revision=tree.revision, evidence='source-derived; not original-runtime comparison',
                sourceHashes={path: inventory.digest(tree.read(path)) for path in
                              ('core/world/rail/RailPattern.cs', 'core/world/Direction.cs', 'core/world/Traffic.cs',
                               'core/world/Clock.cs', 'core/util/PriorityQueue.cs', 'core/world/rail/Train.cs',
                               'core/views/QuarterViewDrawer.cs')},
                directions=directions, patterns=records,
                trafficDrawOrder=['road.before', 'accessory.before', 'rail.before', 'car', 'rail.after', 'accessory.after'],
                sourceSpecification='doc/web-parity-geometry.md',
                behaviorTable='doc/parity/geometry-behavior.json',
                openCases=['Original-runtime visual comparison', 'Runtime route/attachment/slope/support acceptance',
                           'Runtime train composition, terrain/water height cuts and picking acceptance'])


def gallery(tree, geometry, out):
    out.mkdir(parents=True, exist_ok=True)
    decoded = {}
    with tempfile.TemporaryDirectory(prefix='freetrain-rail-') as folder:
        for sheet in ('RailRoads.bmp', 'RailRoads_n.bmp', 'ugslope.bmp'):
            source = Path(folder) / sheet
            source.write_bytes(tree.read('core/res/' + sheet))
            decoded[sheet] = images.bitmap(source)
    cards = []
    for record in geometry['patterns']:
        paths = []
        for sheet in [record['sheet']] + (['RailRoads_n.bmp'] if record['sheet'] == 'RailRoads.bmp' else []):
            width, height, pixels = decoded[sheet]
            x, y, w, h = record['rectangle']
            if x + w > width or y + h > height:
                raise ValueError(f'Out-of-sheet rectangle: {record}')
            rgba = [pixels[(y + j) * width + x + i] for j in range(h) for i in range(w)]
            name = f'{record["id"]}-{sheet}.png'
            (out / name).write_bytes(images.png(w, h, rgba))
            paths.append(f'<figure><img src="{html.escape(name)}" width="{w*3}" height="{h*3}"><figcaption>{sheet}</figcaption></figure>')
        text = html.escape(json.dumps(record, ensure_ascii=False))
        cards.append(f'<article><h2>{record["family"]} {record["id"]}</h2><div>{"".join(paths)}</div><pre>{text}</pre></article>')
    (out / 'index.html').write_text('''<!doctype html><meta charset="utf-8"><title>Source rail patterns</title>
<style>body{font:14px system-ui;background:#ddd;margin:24px}main{display:grid;grid-template-columns:repeat(auto-fill,minmax(350px,1fr));gap:12px}article{background:white;padding:12px;min-width:0}h2{font-size:16px}article div{display:flex;gap:8px}figure{margin:0}img{image-rendering:pixelated;background:#aac}pre{white-space:pre-wrap;overflow-wrap:anywhere;font-size:11px}</style>
<h1>Source rail patterns</h1><p>All 60 RailPattern IDs and 16 support slices. Explicit day/night crops; upper-left color key. Invisible 1×1 slope samples are retained. <a href="compositions.html">Train poses, bridge/tunnel passes and ramp/cut compositions</a>. Source-derived review aid; this does not establish live train composition or original-runtime fidelity.</p><main>''' + ''.join(cards) + '</main>')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--gallery', type=Path, help='Render review artifacts under tmp/')
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    if args.gallery and not args.gallery.resolve().is_relative_to((inventory.ROOT / 'tmp').resolve()):
        parser.error('--gallery must be under this checkout\'s tmp/')
    config = json.loads(inventory.CONFIG.read_text())
    tree = inventory.Tree(inventory.ROOT, config['baseRevision'])
    geometry = extract(tree)
    behavior = semantics.extract(tree, geometry, inventory.digest)
    inventory.write(inventory.OUT / 'geometry.json', inventory.dump(geometry), args.check)
    inventory.write(inventory.OUT / 'geometry-behavior.json', inventory.dump_ledger(behavior), args.check)
    if args.gallery:
        gallery(tree, geometry, args.gallery)
        count = semantics.gallery(tree, geometry, behavior, args.gallery, images)
        print(f'Rendered {count} source-derived composition scenes')
    print(f'Extracted {len(geometry["directions"])} directions and {len(geometry["patterns"])} rail/support records')


if __name__ == '__main__':
    main()
