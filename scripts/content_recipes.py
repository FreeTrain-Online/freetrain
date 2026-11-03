"""Trusted portable sprite compiler for stable bundled declarations.

No class names from XML are activated. Rectangles/anchors follow the pinned
loaders; invalid authored crops are diagnostics, never silently clipped.
"""
import copy
import re
from content_bitmap import night_color


def children(node, tag):
    return [c for c in node['children'] if c['tag'] == tag]


def child(node, tag, required=True):
    found = children(node, tag)
    if len(found) > 1 or (required and not found):
        raise ValueError('Expected one ' + tag)
    return found[0] if found else None


def text(node, tag, default=None):
    value = child(node, tag, default is None)
    return value['text'] if value else default


def integer(value, minimum=-(2**31), maximum=2**31-1):
    if not re.fullmatch(r'[+-]?\d+', str(value).strip()):
        raise ValueError('Invalid source integer: ' + str(value))
    result = int(value)
    if not minimum <= result <= maximum:
        raise ValueError('Source integer out of range: ' + str(value))
    return result


def pair(value):
    fields = value.split(',')
    if len(fields) != 2:
        raise ValueError('Expected two coordinates: ' + value)
    return [integer(v) for v in fields]


def color(value):
    # All reviewed bundled colors are decimal triples. No CSS color guessing.
    fields = value.split(',')
    if len(fields) != 3:
        raise ValueError('Expected RGB triple: ' + value)
    return [integer(v, 0, 255) for v in fields]


def effect(node):
    factory = child(node, 'spriteType', False) or child(node, 'colorVariation', False)
    if not factory:
        return dict(kind='simple')
    identity = 'spriteFactory:' + factory['attributes']['name']
    if identity == 'spriteFactory:default':
        return dict(kind='simple')
    if identity not in ('spriteFactory:colorMap', 'spriteFactory:hueTransform'):
        raise ValueError('Unsupported sprite factory: ' + str(identity))
    maps = children(factory, 'map')
    if identity == 'spriteFactory:colorMap':
        return dict(kind='color-map', maps=[dict(source=color(m['attributes']['from']), target=color(m['attributes']['to'])) for m in maps])
    targets = [None, None, None]
    for m in maps:
        source = m['attributes']['from']
        fields = source.split(',')
        index = fields.index('*') if len(fields) == 3 and '*' in fields else {'r':0,'red':0,'g':1,'green':1,'b':2,'blue':2}.get(source.lower())
        if index is None:
            raise ValueError('Invalid hue mask: ' + source)
        targets[index] = color(m['attributes']['to'])
    return dict(kind='hue', targets=targets)


def transform_pixel(pixel, settings, night=False):
    rgb, alpha = tuple(pixel[:3]), pixel[3]
    if not alpha:
        return pixel
    if settings['kind'] == 'color-map':
        # Native translateColor scans backwards. Night duplicates (authored
        # colors last) deliberately retain the source collision behavior.
        maps = settings['maps']
        table = [(tuple(night_color(tuple(m['source']))), tuple(night_color(tuple(m['target'])))) for m in maps] if night else []
        table += [(tuple(m['source']), tuple(m['target'])) for m in maps]
        for source, target in reversed(table):
            if rgb == source:
                return (*target, alpha)
    if settings['kind'] == 'hue':
        for i, target in enumerate(settings['targets']):
            if target and any(target) and rgb[i] and all(rgb[j] == 0 for j in range(3) if j != i):
                # Exact native RGB888 arithmetic, including per-channel +255
                # before the masked shift in HueTransformer.procChannel.
                return (*( ((v * rgb[i] + (255 if j == 2 else 0)) >> 8) for j,v in enumerate(target)), alpha)
    return pixel


def slices(node, dimensions, mode):
    """DefaultSpriteLoader 0D/2D/3D. Return null cells as explicit records."""
    a = node['attributes']; ox, oy = pair(a['origin'])
    if mode == '0d':
        w,h = pair(a.get('size', '32,32'))
        return [dict(cell=[0,0,0], rectangle=[ox,oy,w,h], anchor=[0,integer(a['offset'])])]
    X,Y,Z = dimensions
    if min(X,Y,Z) < 0 or max(X,Y,Z) > 256 or X*Y*max(Z,1) > 65536:
        raise ValueError('Unbounded sprite dimensions')
    records = {}
    def emit(x,y,z,rect,anchor):
        records[x,y,z] = dict(cell=[x,y,z], rectangle=rect, anchor=anchor, invisible=rect[3] == 0)
    if mode == '2d':
        h = integer(a.get('offset', str(Z))); maxh = integer(a.get('height', str(2**31-1)))
        for y in range(Y):
            for x in range(X):
                emit(x,y,0,[(x+y)*16+ox,oy,32,min(maxh,h+16+(y-x)*8)],[0,h+(y-x)*8])
    elif mode == '3d':
        if Z == 0:
            return []
        h = integer(a.get('offset', str(((Z*2)+(X-1))*8)))
        for y in range(Y):
            for x in range(X):
                vx,vy = (x+y)*16+ox, oy+h-16*(Z-1)+(y-x)*8
                sx,sy,w,hh = vx,vy,32,16
                if y == 0 or x == X-1:
                    sy -= 16; hh += 16
                    if y == 0 and x == X-1:
                        pass
                    elif y == 0 and Y > 1:
                        w = 16
                    elif x == X-1 and X > 1:
                        sx += 16; w -= 16
                if sy < 0:
                    hh += sy; sy = 0
                emit(x,y,Z-1,[sx,sy,w,hh],[vx-sx,vy-sy])
        if Z > 1:
            for y in range(Y):
                for x in range(X):
                    if x != 0 and y != Y-1:
                        continue
                    vx,vy = (x+y)*16+ox,oy+h+(y-x)*8
                    sx,sy = vx,vy-(Z-2)*16-8
                    w = 32 if x == 0 and y == Y-1 else 16
                    if x != 0:
                        sx += 16
                    emit(x,y,0,[sx,sy,w,16*(Z-1)+8],[vx-sx,vy-sy])
        for z in range(Z):
            for y in range(Y):
                for x in range(X):
                    if (x,y,z) not in records:
                        records[x,y,z] = dict(cell=[x,y,z], invisible=True)
    else:
        raise ValueError('Unknown sprite loader mode')
    return list(records.values())


class Compiler:
    def __init__(self, catalog):
        self.catalog = catalog
        self.pictures = {p['id']:p for p in catalog['pictures']}
        self.definitions = {d['id']:d for d in catalog['contributions']}
        self.assets = {a['path']:a for a in catalog['assets']}
        self.current = None
        self.frames = []

    def picture(self, node):
        p = child(node,'picture')
        identity = p['attributes'].get('ref', self.current['id'])
        if identity not in self.pictures:
            raise ValueError('Unresolved recipe picture: ' + identity)
        return identity

    def add(self, key, picture, rect, anchor, cell=(0,0,0), stage=20, settings=None, **selector):
        if picture not in self.pictures:
            raise ValueError('Unresolved picture: ' + picture)
        record = dict(key=key, pictureId=picture, rectangle=list(rect), anchor=list(anchor), cell=list(cell),
                      stage=stage, effect=settings or dict(kind='simple'), selector=selector)
        failures = []
        p = self.pictures[picture]
        if p['status'] != 'prepared-picture':
            raise ValueError('Unbound picture: ' + picture)
        for variant in p['variants']:
            for light in ('day','night'):
                asset = self.assets[variant[light]['path']]
                x,y,w,h = rect
                if min(x,y) < 0 or min(w,h) <= 0 or x+w > asset['width'] or y+h > asset['height']:
                    failures.append(variant['season']+'/'+light)
        record['status'] = 'invalid-source-crop' if failures else 'ready'
        if failures:
            record['invalidVariants'] = failures
        self.frames.append(record)

    def load(self, key, node, dimensions, mode, z=0):
        loader = node['attributes'].get('loader', 'default')
        if loader != 'default':
            raise ValueError('Unsupported sprite loader: ' + loader)
        picture, settings = self.picture(node), effect(node)
        for i,r in enumerate(slices(node,dimensions,mode)):
            cell = r['cell']; cell[2] += z
            if r.get('invisible'):
                self.frames.append(dict(key=key+f'/{i}',cell=cell,status='invisible', selector={}))
            else:
                self.add(key+f'/{i}',picture,r['rectangle'],r['anchor'],cell,settings=settings)

    def compile(self, d):
        self.current, self.frames = d, []
        if 'declaration' not in d:
            # The only bundled generated primitive has a zero footprint.
            return dict(id=d['id'], pluginId=d['pluginId'], handler='empty-generated-primitive', frames=[], fields={}, status='ready')
        e=d['declaration']; a=e['attributes']; t=d['type']; name=d['implementation']['name'].split('.')[-1]
        fields={}
        for key in ('price','operationCost','capacity','seatedcapacity','fare','amenity','height','minHeight','maxHeight','density','count'):
            v=child(e,key,False)
            if v:
                fields[key] = str(integer(v['text'], -(2**63) if key=='price' and t!='train' else -(2**31),
                                          2**63-1 if key=='price' and t!='train' else 2**31-1))
        handler='metadata'
        if t in ('commercial','station','railStationary'):
            size=pair(text(e,'size')); h=integer(text(e,'height'),1,256)
            self.load('voxel',child(e,'sprite'),[*size,h],'3d'); handler='fixed-3d'; fields['size']=size
        elif t=='varHeightBuilding':
            size=pair(text(e,'size')); pics=child(e,'pictures'); handler='variable-height'; fields['size']=size
            fields['topCount']=len(children(pics,'top')); fields['bottomCount']=len(children(pics,'bottom'))
            fields['overlay']=False # Direct ExAV constructor does not read overlay.
            for tag in ('top','bottom'):
                for i,node in enumerate(children(pics,tag)):
                    self.load(f'{tag}/{i}',node,[*size,size[0]*8],'2d')
            self.load('middle',child(pics,'middle'),[*size,16],'2d')
        elif t=='GenericStructure':
            handler='generated-parent'
        elif t=='HalfVoxelStructure':
            handler='half-voxel'; spr=child(e,'sprite'); p=self.picture(spr); h=integer(text(e,'height'),0,256)
            offsets=[(0,-8),(-8,-8),(0,-8),(-8,-8),(-8,-4),(0,-4),(-8,-4),(0,-4)]
            mapping=child(spr,'map',False)
            colors=[None]
            if mapping:
                lib=self.definitions[mapping['attributes']['to']]['declaration']
                colors=[color(n['attributes']['color']) for n in children(lib,'element')]
            for pi,pattern in enumerate(children(spr,'pattern')):
                direction={'north':0,'east':2,'south':4,'west':6}[pattern['attributes']['direction']]
                side=pattern['attributes']['side']; origin=pair(pattern['attributes']['origin'])
                for si in ([0,1] if side=='either' else [0] if side=='fore' else [1] if side=='back' else []):
                    off=offsets[direction//2+si*4]
                    for ci,col in enumerate(colors):
                        settings=dict(kind='simple')
                        if col:
                            source=mapping['attributes']['from']; idx=source.split(',').index('*')
                            targets=[None,None,None]; targets[idx]=col; settings=dict(kind='hue',targets=targets)
                        self.add(f'pattern/{pi}/{si}/{ci}',p,[*origin,24,8+h*16],[off[0],off[1]+h*16],settings=settings,direction=direction,side=si,color=ci)
        elif name in ('SymTrainCarImpl','AsymTrainCarImpl','ColorTestTrainCar','ColoredTrainCarImpl'):
            handler='train-car'; asym=name=='AsymTrainCarImpl'
            if name in ('ColorTestTrainCar','ColoredTrainCarImpl'):
                mapping=child(e,'colorMap') if name=='ColoredTrainCarImpl' else None
                p=mapping['attributes']['picture'] if mapping else next(v['id'] for v in self.catalog['contributions'] if v['type']=='colorMapTrainPicture')
                if p not in self.definitions or self.definitions[p]['type']!='colorMapTrainPicture':
                    raise ValueError('Invalid colored train template: '+p)
                ox,oy=0,0; source=[(0,0,255),(0,0,180),(0,255,0),(0,180,0),(255,0,0),(180,0,0),(255,255,0),(180,180,0)]
                target=[color(mapping['attributes'][key]) for key in ('base','line1','line2','line3')] if mapping else [(255,0,0),(0,0,255),(0,255,0),(255,255,0)]
                if mapping: fields.update(colorTemplate=p,colors=target)
                target=[v for c in target for v in (c,tuple(n*180//255 for n in c))]
                settings=dict(kind='color-map',maps=[dict(source=list(s),target=list(v)) for s,v in zip(source,target)])
                if not mapping: fields.update(capacity='0',seatedcapacity='0')
            else:
                spr=child(e,'sprite'); p=self.picture(spr); ox,oy=pair(spr['attributes']['origin']); settings=effect(spr)
            for i in range(16 if asym else 8):
                self.add(f'level/{i}',p,[(i%8)*32+ox,(i//8)*32+oy,32,32],[0,0],settings=settings,angle=i)
            for i in range(8 if asym else 4):
                self.add(f'slope/{i}',p,[i*32+ox,(64 if asym else 32)+oy,32,32],[0,0],settings=settings,slope=i)
            fields['asymmetric']=asym
        elif name=='ReverseTrainCarImpl':
            handler='reverse-train-car'; fields['base']=child(e,'base')['attributes']['carRef']
        elif t=='train':
            handler='train-composition'
            if name=='PatternTrainImpl':
                config=child(e,'config'); cars={}
                for car in children(config,'car'):
                    symbol=car['attributes']['char']
                    if len(symbol)!=1 or symbol in cars: raise ValueError('Invalid/duplicate pattern character')
                    cars[symbol]=car['attributes']['ref']
                patterns={}
                for composition in children(config,'composition'):
                    symbols=''.join(composition['text'].split())
                    if not 1 <= len(symbols) <= 100 or str(len(symbols)) in patterns: raise ValueError('Invalid/duplicate pattern length')
                    try: patterns[str(len(symbols))]=[cars[c] for c in symbols]
                    except KeyError as err: raise ValueError('Undefined pattern character: '+str(err)) from err
                if not patterns: raise ValueError('Missing train patterns')
                fields['patterns']=patterns; targets=cars.values()
            elif name=='ParamTrainImpl':
                comp=child(e,'composition'); fields['composition']={n['tag']:n['attributes']['carRef'] for n in comp['children']}
                if 'body' not in fields['composition']:
                    raise ValueError('Train composition requires body')
                targets=fields['composition'].values()
            else: raise ValueError('Unsupported train composition implementation: '+name)
            for target in targets:
                if target not in self.definitions or self.definitions[target]['type']!='trainCar':
                    raise ValueError('Invalid train car reference: '+target)
            speed=text(e,'speed').lower(); fields['minutesPerVoxel']={'slow':4,'medium':3,'fast':2,'superb':1}.get(speed)
            if fields['minutesPerVoxel'] is None: fields['minutesPerVoxel']=integer(speed)
            fields['effectiveAmenity']=max(50,min(200,int(fields['amenity'])))
            fields['triprange']={'short':0,'middle':1,'long':2}.get(text(e,'triprange','short').lower(),0)
        elif name=='Bulldozer':
            handler='terrain-tool'
        elif name=='StaticLandBuilder':
            handler='land-0d'; self.load('land',child(e,'sprite'),[1,1,1],'0d')
        elif name=='RandomLandBuilder':
            handler='random-land'; fields['lands']=text(e,'lands').split()
            if any(p not in self.definitions for p in fields['lands']): raise ValueError('Unresolved random land')
        elif name in ('FenceBuilder','FenceRemover'):
            handler='fence'; pic=child(e,'picture'); p=self.picture(e); off=integer(pic['attributes']['offset'])
            for i,tag in enumerate(('left_back','right_back','right_front','left_front')):
                self.add(str(i),p,[*pair(text(pic,tag)),32,32],[0,off],stage=0 if i<2 else 40,direction=i*2,settings=effect(e))
        elif name=='ForestBuilder':
            handler='forest'; w,h=pair(text(e,'size')); count=integer(text(e,'count'),1,256); w//=count
            for i in range(count): self.add(f'tree/{i}',self.picture(e),[i*w,0,w,h],[w//2,h],settings=effect(e),pattern=i)
            ground=child(e,'ground',False)
            if ground:
                self.add('ground',self.picture(ground),[*pair(child(ground,'picture')['attributes']['origin']),32,16],[0,0],stage=0,settings=effect(ground))
        elif name=='CreekBuilder':
            handler='creek'; masks=[0,10,5,15,12,6,3,9,13,14,7,11]
            fields['connections']=masks
            for i in range(12): self.add(str(i),self.picture(e),[(i%4)*32,(i//4)*16,32,16],[0,0],settings=effect(e),connections=masks[i])
        elif name=='RiceFieldBuilder':
            handler='rice'; pic=child(e,'picture'); ox,oy=pair(pic['attributes']['origin']); h=16+integer(pic['attributes'].get('offset','0'))
            for i,(x,y) in enumerate(((0,0),(0,16),(32,0),(32,16),(128,16),(64,16),(96,0),(96,16),(64,0))):
                self.add(str(i),self.picture(e),[ox+x,oy+y,32,h],[0,0],settings=effect(e),x=i%3,y=i//3)
        elif name in ('XVinylHouseBuilder','YVinylHouseBuilder'):
            handler='vinyl'; pic=child(e,'picture'); x=integer(pic['attributes']['offset']); fields['axis']='x' if name[0]=='X' else 'y'
            for i in range(3): self.add(str(i),self.picture(e),[x,i*24,32,24],[0,8],settings=effect(e),index=i)
        elif t=='electricPole':
            handler='electric-pole'; spr=child(e,'sprite')
            for y in range(2):
                for x in range(2): self.add(f'{x}/{y}',self.picture(spr),[(y*2+x)*32,0,32,32],[0,16],stage=40 if y else 10,settings=effect(spr),axis=x,foreground=bool(y))
        elif t=='railSignal':
            handler='signal'; onleft=text(e,'side').strip()=='left'
            for i in range(4): self.add(str(i),self.picture(e),[i*32,0,32,24],[0,8],stage=40 if ((i not in (0,3)) if onleft else (i in (0,3))) else 10,settings=effect(e),direction=i*2)
        elif t=='road':
            handler='road'; p=self.picture(e); pic=child(e,'picture')
            locations=[(2,4),(1,4),(1,1),(1,3),(0,1),(2,0),(0,2),(2,3),(2,1),(0,0),(0,3),(1,0),(2,2),(1,2),(0,4)]
            for i in range(1,16):
                if name=='A3RoadContribution':
                    index=0 if i in (2,8,10) else 1 if i in (1,4,5) else 2
                    rect=[index*32,0,32,32]; anchor=[0,16]
                elif name=='StandardRoadContribution':
                    w,h=pair(pic['attributes']['size']); x,y=locations[i-1]; rect=[x*w,y*h,w,h]; anchor=[0,integer(pic['attributes']['offset'])]
                else: raise ValueError('Unsupported road implementation')
                self.add(str(i),p,rect,anchor,connections=i)
        elif t=='specialRail':
            handler='special-rail'
            resource={'BridgeRailContributionImpl':'BridgeRail.bmp','TunnelRailContributionImpl':'TunnelRail.bmp','StealSupportedRailContributionImpl':'StealSupportedRail.bmp','TrainGarageContributionImpl':'garage.bmp'}[name]
            p=d['id']+':'+resource
            if name=='StealSupportedRailContributionImpl':
                for i in range(4): self.add(str(i),p,[i*32,0,32,32],[0,16],stage=10,index=i)
            else:
                count=6 if name=='BridgeRailContributionImpl' else 2; h=27 if name=='TrainGarageContributionImpl' else 32; off=h-16
                for i in range(count):
                    for stage in (10,40):
                        rect=[i*32,32 if stage==40 else 0,32,h] if count==6 else [i*32+(64 if stage==40 else 0),0,32,h]
                        self.add(f'{i}/{stage}',p,rect,[0,off],stage=stage,index=i)
                if count==6:
                    for y in range(2):
                        for x in range(2): self.add(f'pier/{x}/{y}',d['id']+':BridgePier.bmp',[x*32,y*32,32,32],[0,16],stage=0)
        elif name=='TerraceContributionImpl':
            handler='terrace'; self.add('terrace','{2A333E1B-7AA0-4A96-A054-DAEEEB84F953}',[0,1,32,23],[0,7])
        elif t=='specialStructure' and name in ('Mountain2ContributionImpl','ContributionImpl'):
            handler='procedural-world'; fields['diagnostic']='Source uses procedural terrain/PThreeDimStructure geometry, not a bitmap recipe; world construction belongs to P3/P7'
        elif t not in {'picture','colorMapTrainPicture','ColorLibrary','trainDepartureBell','bgm','bgmFactory','trainController','newGame','terrainOnlyNewGame','menu','dockingContent','accountGenre','anonymous','spriteFactory','spriteLoader'}:
            raise ValueError('Unsupported visual contribution: '+d['id']+' / '+name)
        return dict(id=d['id'],pluginId=d['pluginId'],sourcePath=d['sourcePath'],handler=handler,frames=self.frames,fields=fields,
                    status='source-diagnostic' if any(f['status']=='invalid-source-crop' for f in self.frames) else 'ready')

    def run(self):
        recipes=[self.compile(d) for d in self.catalog['contributions']]
        byid={r['id']:r for r in recipes}
        for r in recipes:
            if r['handler']=='reverse-train-car':
                base=byid[r['fields']['base']]
                if base['handler']!='train-car': raise ValueError('Invalid reversed train base')
                r['fields'].update({k:v for k,v in base['fields'].items() if k!='base'})
                r['frames']=copy.deepcopy(base['frames'])
                for f in r['frames']:
                    # ReverseTrainCarImpl flips BOTH direction and climbing.
                    # Asymmetric slope index = direction + !climbing, so the
                    # inverse selector flips bits 4 and 1. Symmetric art masks
                    # reversal away on level ground and retains the climb axis.
                    if base['fields']['asymmetric']:
                        if 'angle' in f['selector']: f['selector']['angle'] ^= 8
                        if 'slope' in f['selector']: f['selector']['slope'] ^= 5
        return recipes
