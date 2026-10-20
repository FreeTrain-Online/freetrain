"""Source-derived P0 geometry expectations and isolated sprite compositions.

This deliberately does not execute legacy assemblies or implement game rules in
the service. Tables are review aids for the pinned C# source named in SOURCES.
"""
import html
import hashlib
import json
from pathlib import Path
import tempfile


REVIEWED_REVISION = '1f755ca6f291253b8cd5024ef53402b85d467170'


SOURCES = {
    'core/world/Direction.cs': 'Direction indices, rotations, angle and vector offsets',
    'core/world/Location.cs': 'Location.toward/align4To/align8To, integer distance',
    'core/world/World.cs': 'World.toHV/toXYZ/fromXYZToAB/isBorderOfWorld and bounds',
    'core/world/Matrix.cs': 'Matrix half-integer transforms and return candidates',
    'core/world/Time.cs': 'Time calendar, display, day/night and seasons',
    'core/world/TimeLength.cs': 'TimeLength durations, tomorrow and random interval',
    'core/world/Clock.cs': 'Clock registration, cancellation and tick ordering',
    'core/util/PriorityQueue.cs': 'PriorityQueue.Entry.CompareTo and removal',
    'core/world/CarState.cs': 'Inside/Outside/Unplaced and immutable placed state',
    'core/world/Car.cs': 'Car.setState occupancy, invalidation and place/remove',
    'core/world/rail/Train.cs': 'Train place/timer/move/reverse/return and TrainCar.draw',
    'core/world/rail/RailPattern.cs': 'All 60 serialized IDs, 16 supports and connections',
    'core/world/rail/RailRoad.cs': 'RailRoad.zdiff/isWellConnected and draw passes',
    'core/world/rail/SingleRailRoad.cs': 'Guide, attach/detach, route, cost and construction',
    'core/world/rail/JunctionRailRoad.cs': 'Controller/head/follower guide and detach',
    'core/world/rail/SlopeRailRoad.cs': 'Eight-voxel slope, legality, support/hole and removal',
    'core/world/rail/SpecialPurposeRailRoad.cs': 'Straight special rail attachment restrictions',
    'core/world/rail/BridgePierVoxel.cs': 'Support election, teardown and cut rendering',
    'core/world/rail/BridgeRail.cs': 'Bridge background/car/foreground and six indices',
    'core/world/rail/TunnelRail.cs': 'Tunnel terrain restoration and draw/invalidation',
    'core/world/rail/SimpleTrainControllerImpl.cs': 'Default straight junction, 30-minute dwell',
    'core/world/rail/YardRailRoad.cs': 'Platform-relative stop position and passenger/bell callback ordering',
    'core/world/rail/DelegationTrainControllerImpl.cs': 'Controller ancestor lookup and default fallback',
    'core/world/rail/StealSupportedRailContributionImpl.cs': 'Raised eight-way girder and four sprite indices',
    'core/world/rail/PassagewayRail.cs': 'Floating passageway after-car pass',
    'core/world/rail/FatPlatform.cs': 'Yard chip then inherited rail before-car pass',
    'core/world/rail/ThinPlatform.cs': 'Platform rail presentation and yard ownership',
    'core/world/Traffic.cs': 'TrafficVoxel occupancy, crossings and six draw passes',
    'core/world/Voxel.cs': 'Fence wrappers, transparency and HoleVoxel contract',
    'core/views/QuarterViewDrawer.cs': 'Traversal, projection, picking, water/cuts/overlays',
    'core/views/GlobalViewOptions.cs': 'Clock/forced day/night selection',
    'core/framework/MainWindow.cs': 'timerHandler double ticks and four speed settings',
    'core/framework/graphics/SimpleSprite.cs': 'Crop origin, draw anchor and transparency',
    'core/contributions/train/TrainCarContribution.cs': '16-angle level / cardinal-slope API',
    'core/contributions/train/SymTrainCarImpl.cs': 'Symmetric level and slope frame lookup',
    'core/contributions/train/AsymTrainCarImpl.cs': 'Asymmetric level and slope frame lookup',
    'core/contributions/train/ReverseTrainCarImpl.cs': 'Tail frame transformation',
    'core/contributions/train/TrainContribution.cs': 'Speed contract and preview differences',
    'core/controllers/rail/RailRoadController.cs': 'Same-level selection and read-only route preview',
    'core/controllers/rail/SlopeRailRoadController.cs': 'Slope selection, removal base and preview',
    'core/controllers/AbstractLineController.cs': 'Special-rail four/eight-way snapping',
    'plugins/org.kohsuke.freetrain.rail.signal/src/SignalRailRoad.cs': 'Signal side gating and actual draw-pass predicate',
    'plugins/org.kohsuke.freetrain.rail.signal/src/RailSignalContribution.cs': 'Signal side and sprite-index predicate',
    'plugins/org.kohsuke.freetrain.trains/plugin.xml': '485 head/body/reverse-tail references and sprite origins',
    'plugins/org.kohsuke.freetrain.rail/plugin.xml': 'Special bridge/tunnel contribution registrations',
}


def angle(a, b):
    d = abs(a - b)
    return min(d, 8 - d)


def level_pose(heading, outgoing, reversed=False):
    x, y, frame = 0, -9, heading * 2
    if heading != outgoing:
        diff = (outgoing - heading) & 7
        if diff == 7:
            diff = -1
        if diff not in (-1, 1):
            raise ValueError('A level car may turn only 45 degrees')
        dd = (outgoing * 2 + diff * 3) & 15
        x += 3 if 2 < dd < 10 else -3
        y += 2 if 6 < dd <= 14 else -2
        frame = (heading * 2 + diff) & 15
    if reversed:
        frame ^= 8
    return dict(heading=heading, outgoing=outgoing, reversed=reversed,
                offset=[x, y], asymmetricFrame=frame, symmetricFrame=frame & 7)


def slope_pose(climb, level, heading, reversed=False):
    direction = (heading + 4) % 8 if reversed else heading
    climbing = heading != climb if reversed else heading == climb
    return dict(climb=climb, level=level, heading=heading, reversed=reversed,
                offset=[0, [-9, -13, -1, -5][level]], direction=direction,
                isClimbing=climbing, asymmetricFrame=direction + (0 if climbing else 1),
                symmetricFrame=(direction if climbing else (direction + 4) % 8) // 2)


def extract(tree, geometry, digest):
    if tree.revision != REVIEWED_REVISION:
        raise ValueError('Geometry semantics require a fresh review before changing the pinned revision')
    normals = [p for p in geometry['patterns'] if p['family'] == 'normal']
    by_edges = {tuple(sorted(p['connections'])): p['id'] for p in geometry['patterns']
                if p['family'] in ('normal', 'junction')}
    attach = []
    for p in normals:
        a, b = sorted(p['connections'])
        for connected in (False, True):
            for d in range(8):
                edges, result = [a, b], p['id']
                allowed = d in edges
                if not allowed:
                    allowed = (all(angle(x, d) in (1, 3, 4) for x in edges) if connected
                               else any(angle(x, d) >= 3 for x in edges))
                    if allowed:
                        if connected:
                            edges = sorted([a, b, d])
                        else:
                            edges = sorted([a if angle(a, d) >= 3 else b, d])
                        result = by_edges.get(tuple(edges))
                        if result is None:
                            raise ValueError('Attachment lacks authored pattern')
                attach.append(dict(patternId=p['id'], wellConnected=connected, newDirection=d,
                                   allowed=allowed, resultId=result if allowed else None))
    poses = [level_pose(d, (d + turn) % 8, reverse) for d in range(8)
             for turn in (-1, 0, 1) for reverse in (False, True)]
    slopes = [slope_pose(d, level, heading, reverse) for d in (0, 2, 4, 6)
              for level in range(4) for heading in (d, (d + 4) % 8)
              for reverse in (False, True)]
    return dict(schemaVersion=1, revision=tree.revision,
                evidence='Pinned source expectations; isolated offline compositions, no original runtime comparison',
                sourceReviews=[dict(path=p, sha256=digest(tree.read(p)), review=review)
                               for p, review in SOURCES.items()],
                levelPoses=poses, slopePoses=slopes, normalAttachments=attach,
                sourceDifferences=[
                    dict(id='GEO-001', finding='Cut drawer passes Z-z+1 (never zero); BridgePierVoxel alpha-at-zero branch is not reached by this caller.', phase='P2/P3'),
                    dict(id='GEO-002', finding='Clock invokes the queued callback before removeMin. Self/other cancellation can remove the current entry and cause the following entry to be removed instead.', phase='P1/P5'),
                    dict(id='GEO-003', finding='SingleRailRoad.build(equal endpoints) proceeds to invalid Direction; UI excludes it. Route preview returns empty while remove quote returns zero.', phase='P4'),
                    dict(id='GEO-004', finding='Bridge support teardown ignores its owner argument; slope entity removal excludes BridgePierVoxel and leaves elected piers.', phase='P3/P4'),
                    dict(id='GEO-005', finding='StoppingAtSignal is not assigned by Train.clockHandler; all positive waits set StoppingAtStation. Default dwell is 30 minutes despite the one-hour comment.', phase='P5'),
                    dict(id='GEO-006', finding='Bridge predicate does not require water despite its comment; tunnel removal excludes its final endpoint. UI reachability/geometry guards must accompany direct domain calls.', phase='P4'),
                    dict(id='GEO-007', finding='Signal needsToDrawAfter predicate is used for drawBefore; its negation is used for drawAfter. Preserve actual calls in source expectations.', phase='P2/P5'),
                    dict(id='GEO-008', finding='Train.place follows loc += direction without slope z transitions; traversal across a ramp is not equivalent to movement and can fail placement.', phase='P5'),
                ],
                acceptanceBoundary='P0 source extraction only. P1/P2/P3/P4/P5 must implement and verify these expectations; runtime captures, full-world cutaway and original-save continuation remain open.')


def gallery(tree, geometry, review, out, images):
    """Draw exact selected crops in source call order, with no external content."""
    paths = ['core/res/RailRoads.bmp', 'core/res/ugslope.bmp', 'core/res/BridgePier.bmp',
             'plugins/org.kohsuke.freetrain.trains/Train485a.bmp',
             'plugins/org.kohsuke.freetrain.trains/Train485b.bmp',
             'plugins/org.kohsuke.freetrain.rail/BridgeRail.bmp',
             'plugins/org.kohsuke.freetrain.rail/TunnelRail.bmp']
    decoded = {}
    with tempfile.TemporaryDirectory(prefix='freetrain-online-composition-') as directory:
        for index, path in enumerate(paths):
            local = Path(directory) / f'{index}.bmp'
            local.write_bytes(tree.read(path))
            decoded[path] = images.bitmap(local)

    def crop(path, rectangle, at):
        width, height, pixels = decoded[path]
        x, y, w, h = rectangle
        if min(x, y) < 0 or x + w > width or y + h > height:
            raise ValueError(f'Invalid crop: {path} {rectangle}')
        return dict(path=path, rectangle=rectangle, at=at,
                    pixels=[pixels[(y+j)*width+x+i] for j in range(h) for i in range(w)])

    def rail(p, at):
        sheet = 'core/res/' + p['sheet']
        return crop(sheet, p['rectangle'], [at[0]-p['anchor'][0], at[1]-p['anchor'][1]])

    def car(pose, at, body=False, tail=False, slope=False):
        if body:
            frame = pose['symmetricFrame']
            rectangle = [frame*32, 32 if slope else 0, 32, 32]
        else:
            frame = pose['asymmetricFrame']
            if tail:
                if slope:
                    direction, climbing = (pose['direction']+4) % 8, not pose['isClimbing']
                    frame = direction + (0 if climbing else 1)
                else:
                    frame ^= 8
            rectangle = [frame*32, 64, 32, 32] if slope else [(frame%8)*32, (frame//8)*32, 32, 32]
        offset = pose['offset']
        return crop(paths[4] if body else paths[3], rectangle, [at[0]+offset[0], at[1]+offset[1]])

    scenes, cards, thumbnails = [], [], []

    def scene(name, title, layers, details):
        width, height = 160, 112
        pixels = [(0, 0, 0, 0)] * (width * height)
        for layer in layers:
            _, _, w, h = layer['rectangle']
            x, y = layer['at']
            if x < 0 or y < 0 or x+w > width or y+h > height:
                raise ValueError(f'Composition clipped: {name}')
            for j in range(h):
                for i in range(w):
                    p = layer['pixels'][j*w+i]
                    if p[3]:
                        pixels[(y+j)*width+x+i] = p
        filename = name + '.png'
        (out / filename).write_bytes(images.png(width, height, pixels))
        thumbnails.append((name, pixels))
        record = dict(id=name, title=title, image=filename, details=details,
                      layers=[{k: v for k, v in layer.items() if k != 'pixels'} for layer in layers])
        scenes.append(record)
        cards.append(f'<article><h2>{html.escape(title)}</h2><img src="{filename}" width="320" height="224"><pre>{html.escape(json.dumps(details, ensure_ascii=False))}</pre></article>')

    patterns = {p['id']: p for p in geometry['patterns']}
    normals = [p for p in geometry['patterns'] if p['family'] == 'normal']
    at = [64, 48]
    for index, pose in enumerate(review['levelPoses']):
        edges = {(pose['heading']+4) % 8, pose['outgoing']}
        pattern = next(p for p in normals if set(p['connections']) == edges)
        scene(f'pose-{index}', f'Level pose {index}', [rail(pattern, at), car(pose, at)], pose)
    for index, pose in enumerate(review['slopePoses']):
        p = patterns[28+pose['climb']*2+pose['level']]
        scene(f'slope-pose-{index}', f'Slope pose {index}', [rail(p, at), car(pose, at, slope=True)], pose)

    for kind, count in [('bridge', 6), ('tunnel', 2)]:
        for index in range(count):
            path = paths[5] if kind == 'bridge' else paths[6]
            before = [index*32, 0, 32, 32]
            after = [index*32, 32, 32, 32] if kind == 'bridge' else [64+index*32, 0, 32, 32]
            d = 6 if (kind == 'bridge' and index < 3) or (kind == 'tunnel' and index == 0) else 4
            layers = [crop(path, before, [64, 32]), car(level_pose(d, d), at), crop(path, after, [64, 32])]
            for stage in range(1, 4):
                scene(f'{kind}-{index}-stage-{stage}', f'{kind} {index}: pass {stage}', layers[:stage],
                      dict(stage=['rail.before', 'car', 'rail.after'][stage-1], pictureIndex=index))

    # Four-car 485 head/body/body/reverse-tail; isolated voxel inclusion at cuts.
    # Terrain/water/HoleVoxel rendering is specified in prose, not simulated here.
    for underground in (False, True):
        for climb in (0, 2, 4, 6):
            dx, dy = geometry['directions'][climb]['offset']
            voxels = []
            for level in range(4):
                z = 1 + level//2
                x, y = dx*level, dy*level
                point = [64+16*(x+y), 56+8*(-x+y)-16*(z-1)]
                p = patterns[(44 if underground else 28)+climb*2+level]
                layers = [rail(p, point), car(slope_pose(climb, level, climb), point,
                                             body=level in (1, 2), tail=level == 0, slope=True)]
                # HV traversal: v=y-x, then h=(x+y)>>1, then z ascending.
                voxels.append((y-x, (x+y)>>1, z, layers))
                if level >= 2:
                    support = next(p for p in geometry['patterns']
                                   if p['id'] == f'{"underground-slope-support" if underground else "slope-support"}-{climb*2+level}')
                    voxels.append((y-x, (x+y)>>1, 1, [rail(support, [point[0], point[1]+16])]))
            for cut in (1, 2):
                layers = [layer for _, _, z, ls in sorted(voxels, key=lambda v: v[:3]) if z <= cut for layer in ls]
                name = f'ramp-{"underground" if underground else "surface"}-{climb}-cut-{cut}'
                scene(name, name, layers, dict(climb=climb, cut=cut, evidence='Isolated rail/support/consist draw; terrain/water omitted'))

    for group in ('pose-', 'slope-pose-', 'bridge-', 'tunnel-', 'ramp-'):
        selected = [pixels for name, pixels in thumbnails if name.startswith(group)]
        columns = min(8, len(selected))
        rows = (len(selected)+columns-1)//columns
        width, height = columns*160, rows*112
        pixels = [(184, 195, 209, 255)]*(width*height)
        for index, thumbnail in enumerate(selected):
            left, top = (index%columns)*160, (index//columns)*112
            for y in range(112):
                for x in range(160):
                    pixel = thumbnail[y*160+x]
                    if pixel[3]:
                        pixels[(top+y)*width+left+x] = pixel
        (out / f'{group}contact.png').write_bytes(images.png(width, height, pixels))

    metadata = dict(schemaVersion=1, revision=tree.revision,
                    evidence='Source-derived offline sprite compositions, not runtime captures',
                    sourceHashes={path: hashlib.sha256(tree.read(path)).hexdigest() for path in paths},
                    scenes=scenes)
    (out / 'compositions.json').write_text(json.dumps(metadata, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    (out / 'compositions.html').write_text('''<!doctype html><meta charset="utf-8"><title>P0 rail/car compositions</title>
<style>body{font:14px system-ui;background:#ddd;margin:24px}main{display:grid;grid-template-columns:repeat(auto-fill,minmax(350px,1fr));gap:12px}article{background:white;padding:12px;min-width:0}h2{font-size:16px}img{image-rendering:pixelated;background:#aac}pre{white-space:pre-wrap;overflow-wrap:anywhere;font-size:11px}</style>
<h1>Source-derived rail/car compositions</h1><p>48 level poses; 64 slope poses; 24 bridge/tunnel pass stages; 16 ramp/cut snapshots. Exact pinned crops and source formulas, 485 head/body/reverse-tail. Isolated offline compositions: no original runtime or full-world rendering claim. Terrain/water and runtime performance remain later acceptance work. <a href="index.html">Raw pattern gallery</a></p><main>'''+''.join(cards)+'</main>', encoding='utf-8')
    return len(scenes)
