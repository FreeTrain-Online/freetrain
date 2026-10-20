"""Pinned ExAV crop/dispatch studies; no game runtime or world simulator."""
import ast
import hashlib
import html
import json
from pathlib import Path
import re
import tempfile


SIZE = (192, 144)
STYLES = ('concrete', 'brown', 'green')


def number(expression):
    """Only constant +/- arithmetic from reviewed Point/Size constructors."""
    node = ast.parse(expression.strip(), mode='eval').body
    def read(n):
        if isinstance(n, ast.Constant) and type(n.value) is int:
            return n.value
        if isinstance(n, ast.UnaryOp) and isinstance(n.op, (ast.UAdd, ast.USub)):
            return read(n.operand) * (-1 if isinstance(n.op, ast.USub) else 1)
        if isinstance(n, ast.BinOp) and isinstance(n.op, (ast.Add, ast.Sub)):
            return read(n.left) + read(n.right) * (-1 if isinstance(n.op, ast.Sub) else 1)
        raise ValueError('Unsupported source crop expression: ' + expression)
    return read(node)


def recipes(tree, rail):
    rows = {}
    def add(key, path, rect, anchor, symbol, night=None):
        rows[key] = dict(id=key, path=path, nightPath=night,
                         rectangle=rect, anchor=anchor, sourceSymbol=symbol,
                         nightTreatment='authored-sheet' if night else 'RGB888 source formula; not native pixel-format capture')
    special = 'plugins/org.kohsuke.freetrain.rail/StealSupportedRail.bmp'
    for i in range(4):
        add(f'girder-{i}', special, [i*32, 0, 32, 32], [0, 16], 'StealSupportedRailContributionImpl.onInitComplete')
    for i in range(2):
        add(f'fat-{i}', 'core/res/FatPlatform.bmp', [i*32, 0, 32, 32], [0, 16], 'FatPlatform.getSprite')
    add('yard', 'core/res/YardChip.bmp', [0, 0, 32, 16], [0, 0], 'FatPlatform.RailRoadImpl.drawBefore')
    add('pier', 'core/res/BridgePier.bmp', [0, 0, 32, 32], [0, 16], 'BridgePierVoxel.defaultSprite')
    root = rail.inventory.manifest(tree.read('plugins/system/plugin.xml'), allow_cp932=True)[0]
    def picture(identity):
        node = next(n.find('picture') for n in root.findall('contribution') if n.get('id') == identity)
        override = node.find('override')
        if override is None or override.get('when') != 'night':
            raise ValueError('Reviewed authored night binding changed: ' + identity)
        return ('plugins/system/' + node.get('src'), 'plugins/system/' + override.get('src'))
    thin, thin_n = picture('{3FF9F902-6B2A-44A4-9C5F-DE8D82CFD37D}')
    for i in range(8):
        add(f'thin-{i}', thin, [i*32, 0, 32, 24], [0, 8], 'ThinPlatform.getSprite', thin_n)
    # Parse every explicit stair constructor and alias, including unusual offsets
    # and the authored SOUTH index 23 x=388. Never regularize the source table.
    source = rail.inventory.strip_comments(tree.text('core/world/rail/PassagewayRail.cs')[0])
    stairs = {}
    pattern = (r'stairSprites\[j,\s*(\d+)\]\s*=\s*new SimpleSprite\(\s*bmp\[j\],\s*'
               r'(offset|new Point\(([^,]+),([^)]*)\)),\s*new Point\(([^,]+),([^)]*)\),\s*'
               r'(sz|new Size\(([^,]+),([^)]*)\))\s*\)')
    for m in re.finditer(pattern, source):
        index, anchor, ax, ay, x, y, size, w, h = m.groups()
        stairs[int(index)] = ([number(x), number(y), 32 if size == 'sz' else number(w),
                              32 if size == 'sz' else number(h)],
                             [0, 16] if anchor == 'offset' else [number(ax), number(ay)])
    for a, b in re.findall(r'stairSprites\[j,\s*(\d+)\]\s*=\s*stairSprites\[j,\s*(\d+)\]', source):
        stairs[int(a)] = stairs[int(b)]
    if set(stairs) != set(range(32)):
        raise ValueError('Incomplete ExAV stair constructor/alias table')
    for style in STYLES:
        suffix = '' if style == 'concrete' else '-' + style
        day, night = picture('{3197A63A-89DC-4237-8C9B-045F41F31CDB}' + suffix)
        for d in range(4):
            for variant in range(3):
                rect = [d*32, 8, 32, 32] if variant == 0 else [(d*2+variant)*32, 40, 32, 32]
                add(f'{style}-passage-{d*3+variant}', day, rect, [0, 16], 'PassagewayRail static sprites', night)
        for index in (12, 13):
            add(f'{style}-passage-{index}', day, [(index-8)*32, 8, 32, 32], [0, 16], 'PassagewayRail.getFloatingSprite', night)
        for index, (rect, anchor) in sorted(stairs.items()):
            add(f'{style}-stair-{index}', day, rect, anchor, 'PassagewayRail.getStairSprite', night)
    for p in rail.extract(tree)['patterns']:
        if p['family'] == 'normal' and len(p['connections']) == 2 and (p['connections'][0]+4) % 8 == p['connections'][1]:
            d = p['connections'][0] % 4
            add(f'rail-{d}', 'core/res/RailRoads.bmp', p['rectangle'], p['anchor'], 'RailRoad.drawBefore / pattern.draw', 'core/res/RailRoads_n.bmp')
    for d in range(8):
        frame = (d*2) % 16
        add(f'car-{d}', 'plugins/org.kohsuke.freetrain.trains/Train485a.bmp',
            [(frame % 8)*32, (frame//8)*32, 32, 32], [0, 0], 'TrainCar.draw level: 2*heading, offset(0,-9)')
    return rows


def studies(geometry):
    """Describe voxel/pass order separately from pixel rasterization."""
    dirs = {d['index']: d['offset'] for d in geometry['directions']}
    scenes = []
    def emit(key, group, params, layers):
        # Full view starts at water level=0; cuts include whole voxel drawings.
        # No sprite slicing at cut height: tall platform/stair art crosses it.
        points = [(16*(l['voxel'][0]+l['voxel'][1]),
                   8*(-l['voxel'][0]+l['voxel'][1])-16*l['voxel'][2]) for l in layers]
        center = [(min(p[i] for p in points)+max(p[i] for p in points))//2 for i in (0,1)]
        for light in ('day', 'night'):
            for cut in (0, 1, None):
                kept = [l for l in layers if cut is None or l['voxel'][2] <= cut]
                kept.sort(key=lambda l: (l['voxel'][1]-l['voxel'][0],
                    (l['voxel'][0]+l['voxel'][1]) >> 1, l['voxel'][2], l['order']))
                scenes.append(dict(id=f'{key}-{light}-cut-{cut if cut is not None else "full"}',
                    group=group, parameters=dict(params, light=light, cut=cut),
                    projectionCenter=center, layers=kept))
    def layer(recipe, voxel, stage, order, offset=(0, 0)):
        return dict(recipe=recipe, voxel=list(voxel), stage=stage, order=order, offset=list(offset))
    def railcar(d, v):
        return [layer(f'rail-{d%4}', v, 'rail.before', 10),
                layer(f'car-{d}', v, 'car', 30, (0, -9))]
    for d in range(8):
        emit(f'girder-{d}', 'girder', dict(direction=d, pictureIndex=[3, 1, 0, 2][d%4]),
             [layer('pier', (0, 0, 0), 'support', 0),
              layer(f'girder-{[3,1,0,2][d%4]}', (0, 0, 1), 'rail.before', 10),
              layer(f'car-{d}', (0, 0, 1), 'car', 30, (0, -9))])
    for d in (0, 2, 4, 6):
        for roof in (False, True):
            v = (0, 0, 1)
            outlook = layer(f'thin-{d+int(roof)}', v, 'outlook.before' if d in (2, 4) else 'outlook.after', 20 if d in (2, 4) else 40)
            emit(f'plain-{d}-roof-{int(roof)}', 'plain', dict(direction=d, roof=roof), railcar(d, v)+[outlook])
            for length in (4, 8):
                ls = []
                dx, dy = dirs[d]
                for i in range(length):
                    has_roof = roof and length//4 <= i < length-length//4
                    at = (dx*i, dy*i, 1)
                    ls += railcar(d, at) + [layer(f'thin-{d+int(has_roof)}', at,
                        'outlook.before' if d in (2,4) else 'outlook.after', 20 if d in (2,4) else 40)]
                emit(f'roof-run-{d}-{length}-{int(roof)}', 'roof-run', dict(direction=d,length=length,withRoof=roof), ls)
        # Dedicated wide-platform center with lane 0 required; lane 1 optional.
        for lanes in (1, 2):
            ls = [layer(f'fat-{d//2%2}', (0, 0, 1), 'platform.voxel', 10)]
            for ld in ((d+2)%8,) if lanes == 1 else ((d+2)%8, (d-2)%8):
                dx, dy = dirs[ld]
                v = (dx, dy, 1)
                ls += [layer('yard', v, 'yard.before', 0)] + railcar(d, v)
            emit(f'fat-{d}-lanes-{lanes}', 'fat', dict(direction=d,lanes=lanes,roof='no roof drawing in source'), ls)
        for style in STYLES:
            # Demonstrate dispatch gap: ordinary two-argument rail dispatch
            # has no floating passageway layer; explicit crops appear separately.
            emit(f'standalone-{style}-{d}', 'dispatch', dict(direction=d,style=style,
                finding='three-argument PassagewayRail.drawAfter is not called by TrafficVoxel'), railcar(d,(0,0,1)))
            for double in (False, True):
                for bridge in (False, True):
                    index = d//2*3 + ((2 if bridge else 1) if double else 0)
                    ls = railcar(d,(0,0,1)) + [layer(f'{style}-passage-{index}',(0,0,1),
                        'outlook.before' if d in (2,4) else 'outlook.after',20 if d in (2,4) else 40)]
                    if bridge and d in (2,4):
                        # d.right90: N/S floating for E, E/W floating for S.
                        ls += [layer(f'{style}-passage-{13 if d==2 else 12}',(0,0,1),'outlook.after.float',50)]
                    emit(f'passage-{style}-{d}-{int(double)}-{int(bridge)}','passage',
                        dict(direction=d,style=style,doubleWidth=double,hasBridge=bridge,pictureIndex=index), ls)
                for up in (False, True):
                    for roof in (False, True):
                        index = d*4 | (0 if up else 4) | (2 if double else 0) | int(roof)
                        emit(f'stair-{style}-{d}-{int(double)}-{int(up)}-{int(roof)}','stair',
                            dict(direction=d,style=style,doubleWidth=double,upward=up,roof=roof,pictureIndex=index,
                                 context='width flag requires left neighbor with opposite direction and passage outlook'),
                            railcar(d,(0,0,1))+[layer(f'{style}-stair-{index}',(0,0,1),
                                'outlook.before' if d in (2,4) else 'outlook.after',20 if d in (2,4) else 40)])
    return scenes


def render(tree, geometry, rail, out):
    out.mkdir(parents=True, exist_ok=True)
    rows = recipes(tree, rail)
    source_paths = sorted({p for r in rows.values() for p in (r['path'], r['nightPath']) if p})
    decoded = {}
    with tempfile.TemporaryDirectory(prefix='exav-expanded-bitmaps-') as folder:
        for i, p in enumerate(source_paths):
            local = Path(folder) / f'{i}.bmp'
            local.write_bytes(tree.read(p))
            decoded[p] = rail.images.bitmap(local)
    lights = {(8,0,0):(255,8,8), (0,8,0):(252,243,148), (0,0,8):(255,227,99)}
    crops = {}
    raw_records = []
    for key, r in rows.items():
        for light in ('day', 'night'):
            path = r['nightPath'] if light == 'night' and r['nightPath'] else r['path']
            width, height, pixels = decoded[path]
            x,y,w,h = r['rectangle']
            if min(x,y,w,h) < 0 or x+w > width or y+h > height:
                # Pinned SOUTH/down/double/roof stair index 23 really uses
                # x=388 in a 336-wide sheet. Show a diagnostic, never repair to
                # a plausible x or claim an original-runtime pixel result.
                if key not in {f'{s}-stair-23' for s in STYLES} or (x,y,w,h,width,height) != (388,4,32,36,336,153):
                    raise ValueError('Unreviewed out-of-sheet crop: ' + key)
                r['cropStatus'] = 'source-invalid-crop'
                r['cropFinding'] = f'{r["rectangle"]} exceeds {path} ({width}x{height}); native draw result unverified'
                crop = [(220,35,45,255) if i in (0,w-1) or j in (0,h-1) or i==j%w else (255,225,225,255)
                        for j in range(h) for i in range(w)]
            else:
                r.setdefault('cropStatus', 'valid')
                crop = [pixels[(y+j)*width+x+i] for j in range(h) for i in range(w)]
            if light == 'night' and not r['nightPath'] and r['cropStatus']=='valid':
                crop = [(*lights.get(p[:3],tuple(c//4 for c in p[:3])),p[3]) for p in crop]
            crops[key, light] = crop
            data = rail.images.png(w,h,crop)
            filename = f'raw-{key}-{light}.png'
            (out/filename).write_bytes(data)
            raw_records.append(dict(recipe=key,light=light,image=filename,
                status=r['cropStatus'], parameters=dict(rectangle=r['rectangle'],anchor=r['anchor'],
                    source=path,sourceSymbol=r['sourceSymbol']),sha256=hashlib.sha256(data).hexdigest()))
    plans = studies(geometry)
    contacts = {}
    for scene in plans:
        layers = scene['layers']
        scene['status'] = 'source-invalid-crop-diagnostic' if any(
            rows[l['recipe']]['cropStatus'] != 'valid' for l in layers) else 'source-formula-study'
        # Center the union of voxel screen positions, retaining projection and
        # source anchors. roof-run studies deliberately have a larger canvas.
        width,height = (384,240) if scene['group']=='roof-run' else SIZE
        cx,cy = scene['projectionCenter']
        pixels = [(0,0,0,0)]*(width*height)
        for l in layers:
            r = rows[l['recipe']]
            vx,vy,vz = l['voxel']
            px = width//2-16+16*(vx+vy)-cx-r['anchor'][0]+l['offset'][0]
            py = height//2+8*(-vx+vy)-16*vz-cy-r['anchor'][1]+l['offset'][1]
            w,h = r['rectangle'][2:]
            if px<0 or py<0 or px+w>width or py+h>height:
                raise ValueError('Composition clips source art: '+scene['id'])
            l['at'] = [px,py]
            src = crops[l['recipe'],scene['parameters']['light']]
            for y in range(h):
                for x in range(w):
                    p = src[y*w+x]
                    if p[3]: pixels[(py+y)*width+px+x] = p
        data = rail.images.png(width,height,pixels)
        scene.update(image=scene['id']+'.png',sha256=hashlib.sha256(data).hexdigest(),size=[width,height])
        (out/scene['image']).write_bytes(data)
        # All full/at-cut variants are identical by source dispatch; contact full
        # studies exhaust every parameter. Girder contacts also show lower cuts.
        if scene['parameters']['cut'] is None or (scene['group']=='girder' and scene['parameters']['cut']==0):
            group = scene['group']+'-'+scene['parameters']['light']
            if scene['parameters'].get('style'): group += '-'+scene['parameters']['style']
            if scene['parameters']['cut']==0: group += '-below'
            contacts.setdefault(group,[]).append((scene,pixels))
    contact_records=[]
    for group, items in sorted(contacts.items()):
        # At most 32 studies per sheet, without rescaling source pixels.
        for page in range(0,len(items),32):
            selected=items[page:page+32]
            tw=max(s['size'][0] for s,_ in selected); th=max(s['size'][1] for s,_ in selected)
            columns=4; width=columns*tw; height=((len(selected)+columns-1)//columns)*th
            pixels=[(184,195,209,255)]*(width*height)
            for i,(s,thumb) in enumerate(selected):
                w,h=s['size'];left=(i%columns)*tw;top=(i//columns)*th
                for y in range(h):
                    for x in range(w):
                        p=thumb[y*w+x]
                        if p[3]: pixels[(top+y)*width+left+x]=p
            filename=f'contact-{group}-{page//32}.png'
            data=rail.images.png(width,height,pixels);(out/filename).write_bytes(data)
            contact_records.append(dict(image=filename,sha256=hashlib.sha256(data).hexdigest(),studies=[s['id'] for s,_ in selected]))
    def card(scene):
        label=scene['id'] if 'id' in scene else scene['recipe']+'-'+scene['light']
        return f'<article><h3>{html.escape(label)}</h3><p>{scene["status"]}</p><a href="{scene["image"]}"><img loading="lazy" width="384" src="{scene["image"]}"></a><pre>{html.escape(json.dumps(scene.get("parameters",{}),ensure_ascii=False))}</pre></article>'
    style='<style>body{font:14px system-ui;margin:24px;background:#ddd}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(400px,1fr));gap:12px}article{background:white;padding:12px;overflow:auto}img{image-rendering:pixelated;background:#b8c3d1;max-width:100%}pre{white-space:pre-wrap}</style>'
    for group in sorted({s['group'] for s in plans}):
        page=[s for s in plans if s['group']==group]
        (out/(group+'.html')).write_text('<!doctype html><meta charset="utf-8">'+style+f'<h1>{group}</h1><p><a href="index.html">Gallery index and limitations</a></p><main>'+''.join(map(card,page))+'</main>')
    (out/'raw.html').write_text('<!doctype html><meta charset="utf-8">'+style+'<h1>Source crops</h1><a href="index.html">Index</a><main>'+''.join(map(card,raw_records))+'</main>')
    links=''.join(f'<li><a href="{g}.html">{g}</a></li>' for g in sorted({s['group'] for s in plans}))
    (out/'index.html').write_text('<!doctype html><meta charset="utf-8">'+style+f'''<h1>Expanded ExAV girder/platform review</h1><p>Source-derived offline studies; pinned {tree.revision} / trunk. No game/runtime capture, terrain/water model, host simulation or placement legality claim. Connected representative voxels omit warning badges/fences/other traffic. Night uses authored sheets where bound, otherwise RGB888 source-formula conversion (not native pixel-format equivalence). Cuts filter occupied voxels, never slice tall sprite pixels. Below-cut empty studies are intentional. EXAV-GAL-001: red crosses mark the out-of-sheet SOUTH stair23 source selection; these are diagnostics, not predicted native pixels. Double-width flags are parameter studies with neighbor requirements recorded, not whole-world reachability proof.</p><p>{len(plans)} compositions; {len(rows)} recipes × day/night. <a href="raw.html">All raw crops</a> · <a href="../index.html">Existing raw rail gallery</a> · <a href="../compositions.html">Existing 152 rail/car studies</a></p><ul>{links}</ul><h2>Contact sheets</h2>'''+''.join(f'<p><a href="{r["image"]}">{r["image"]}</a></p>' for r in contact_records))
    return dict(schemaVersion=1,revision=tree.revision,subtree='trunk',
        evidence='Source-derived offline crops/compositions; no runtime comparison',
        nightBoundary='Authored sheets or RGB888 formula; native 555/565/24/32-bit output unverified',
        sourceDifferences=[dict(id='EXAV-GAL-001',phase='P2/P6',
            finding='SOUTH downward double-width roofed stair index 23 uses x=388, beyond all three 336-wide day/night sheets. Red placeholders are diagnostics, not predicted native pixels.',
            disposition='Preserve source recipe and explicit invalid-crop case; correction/native failure behavior requires a feature-slice decision.')],
        sourceHashes={p:hashlib.sha256(tree.read(p)).hexdigest() for p in source_paths},
        recipes=list(rows.values()),rawCrops=raw_records,scenes=plans,contacts=contact_records,
        counts=dict(recipes=len(rows),rawCrops=len(raw_records),compositions=len(plans),contactSheets=len(contact_records)))
