#!/usr/bin/env python3
"""Read-only P7-02 source oracle; no production imports or expectation generation.

All expected results are checked-in literals. Optional archive checking reads
original Git objects, never corrected checkout bytes or desktop assemblies.
"""
import argparse
from copy import deepcopy
from functools import cache
import hashlib
import json
import math
from pathlib import Path
import struct
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
FIXTURES = ROOT / 'doc/parity/city-fixtures.json'
# Reviewed inputs, case matrices, ownership and acceptance mappings, excluding
# expected outputs. Updating this pin requires an explicit source fixture review.
MATRIX_SHA256 = '2d351503f65bb9fdd1ce58cb1e718b739b5ba0ed1a6af53a53fb4f6cb7afe8fb'


def require(ok, message):
    if not ok:
        raise ValueError(message)


def digest(v):
    return hashlib.sha256(json.dumps(v, ensure_ascii=False, sort_keys=True,
                                     separators=(',', ':')).encode()).hexdigest()


@cache
def read(name):
    return json.loads((ROOT / 'doc/parity' / name).read_text())


def f32(v):
    return struct.unpack('<f', struct.pack('<f', v))[0]


def checked(v, bits=32):
    if not -(2**(bits-1)) <= v < 2**(bits-1):
        raise OverflowError('Int' + str(bits))
    return v


def wrap(v):
    return (v + 2**31) % 2**32 - 2**31


def definition(i):
    c = next(c for c in read('city-review.json')['contributions']
             if c['sourceContribution']['coverageId'] == i['definition'])
    e = ET.fromstring(c['declarationXml'])
    x, y = map(int, e.findtext('size').split(','))
    h = int(e.findtext('height')) if e.get('type') == 'commercial' else i['height']
    price = int(e.findtext('price'))
    return dict(size=[x,y,h], occupied=x*y*h, endExclusive=[i['base'][0]+x,i['base'][1]+y,i['base'][2]+h],
                price=price if e.get('type') == 'commercial' else price*h,
                pricePerArea=price//max(1,x*y), demolition=wrap(wrap(500000*x)*y),
                providerMultiplier=1 if e.get('type') == 'commercial' else h,
                computerEligible=not c['computerCannotBuild'])


def heat(i):
    h, v = i['size']
    q = [[0.0]*(v+2) for _ in range(h+2)]
    back = deepcopy(q)
    rho = deepcopy(q)
    for x in range(1,h+1):
        for y in range(1,v+1):
            rho[x][y] = f32(.8)
    for x,y,r in i['rho']:
        rho[x][y] = f32(r)
    history=[]
    for action in i['actions']:
        if action['kind'] == 'deposit':
            x,y = action['hv']
            q[x][y] = f32(q[x][y] + f32(f32(action['delta']*action['frequency'])/4))
        else:
            q,back = back,q
            for x in range(1,h+1):
                for y in range(1,v+1):
                    t = back[x][y]
                    dx = 1 if y%2==0 else -1
                    ns=[(x,y-1),(x,y+1),(x+dx,y-1),(x+dx,y+1)]
                    if rho[x][y] > f32(.8):
                        terms=[f32(max(0,f32(back[a][b]-t))**2) for a,b in ns]
                        tr=terms[0]
                        for term in terms[1:]: tr=f32(tr+term)
                        t=f32(f32(t*f32(.999))+f32(math.sqrt(f32(tr*f32(.24)))))
                    else:
                        terms=[f32(f32(back[a][b]-t)*rho[a][b]) for a,b in ns]
                        tr=terms[0]
                        for term in terms[1:]: tr=f32(tr+term)
                        t=f32(f32(t*f32(.99))+f32(f32(tr*f32(.24))*rho[x][y]))
                    q[x][y]=max(0,t)
        history.append(dict(q=[q[x][y] for x,y in i['probes']],
                            back=[back[x][y] for x,y in i['probes']]))
    return dict(trace=history, publicValue=[checked(math.trunc(math.pow(q[x+1][y+1],f32(.3)))*10)
                                           for x,y in i['publicReads']])


def hv(i):
    xx=i['xy'][0]-(i['worldV']-1)//2
    h=(xx+i['xy'][1])>>1
    v=i['xy'][1]-xx
    return dict(hv=[h,v], depositIndex=[h,v], rhoIndex=[h,v], publicIndex=[h+1,v+1])


def conductivity(i):
    road=None
    for slot in i['column']:
        if slot['kind']=='traffic': road=slot.get('road')
    mountain=any(s['kind']=='mountain' for s in i['column'])
    r=f32(.8)
    if road:
        if road['major']>=2:
            r=f32(.999) if road['pavement'] else f32(f32(f32(.999)+f32(.8))/2)
    elif i['ground']<i['water']: r=f32(.03)
    elif mountain: r=f32(.4)
    return dict(rho=r, roadFound=road)


def trade(i):
    cells=deepcopy(i['cells']); entities=i['entities']; receipts=[]; preview=0; seen=set()
    for cell in cells:
        e=entities.get(cell['entity'])
        if i['operation']=='sell':
            if cell['entity']=='property': preview+=cell['land']
        elif e and not e['owned'] and e['reclaimable']:
            if cell['entity'] not in seen: preview+=e['value']; seen.add(cell['entity'])
        elif cell['entity'] is None: preview+=cell['land']
    for n,cell in enumerate(cells):
        if i['operation']=='sell':
            if cell['entity']=='property':
                receipts.append(['earn','SUBSIDIARIES',cell['land']]);cell['entity']=None
        else:
            name=cell['entity'];e=entities.get(name)
            if e and not e['owned'] and e['reclaimable']:
                receipts.append(['spend','SUBSIDIARIES',e['value']])
                receipts.extend([['spend','SUBSIDIARIES',v] for v in e['removeFees']])
                for c in cells:
                    if c['entity']==name:c['entity']=None
            if cell['entity'] is None:
                receipts.append(['spend','SUBSIDIARIES',cell['land']]);cell['entity']='property'
    return dict(nativePreview=preview, receipts=receipts, entities=[c['entity'] for c in cells],
                authoritativeQuote=sum(v if kind=='spend' else -v for kind,_,v in receipts))


def ulv(i):
    if i['z']<i['water'] or any(c['ground']!=i['z'] or c['owned'] for c in i['cells']):
        return dict(result=None)
    unique={c['entity']:c['value'] for c in i['cells'] if c['entity'] is not None}
    return dict(result=dict(landValue=sum(c['land'] for c in i['cells']),entityValue=sum(unique.values())))


def ulv_cache(i):
    cache=None;trace=[]
    for state in i['states']:
        if cache is None: cache=ulv(state)['result']
        trace.append(deepcopy(cache))
    return dict(trace=trace)


def roads(i):
    m=i['mask']; d=i['heading']//2
    remain=m & ~(1<<d) & ~(1<<((d+2)%4))
    standard=[None,[2,4],[1,4],[1,1],[1,3],[0,1],[2,0],[0,2],
              [2,3],[2,1],[0,0],[0,3],[1,0],[2,2],[1,2],[0,4]][m]
    tile=([1,0] if m in (1,4,5) else [0,0] if m in (2,8,10) else [2,0]) if i['a3'] else standard
    return dict(directions=[k*2 for k in range(4) if m&(1<<k)], attach=m|(1<<d), detach=remain,
                roadRemoved=remain==0, crop=[tile[0]*32,tile[1]*i['chipHeight'],32,i['chipHeight']],
                anchor=[0,i['offset']], drawPass='before-car', styleRetained=True)


def road_route(i):
    d=i['heading']//2; reverse=(d+2)%4
    masks=[];cost=0;removeCount=0
    legal=len(i['cells'])>1
    for n,c in enumerate(i['cells']):
        legal=legal and c['kind'] in ('empty','traffic') and not c['car']
        m=c['mask']
        attach=(1<<d if n<len(i['cells'])-1 else 0)|(1<<reverse if n>0 else 0)
        masks.append(m|attach)
        if not m:
            depth=i['z']-c['ground']; mult=-2*depth if depth<0 else depth+1
            if not c['ground']<=i['z']<=i['water'] or c['ground']>=i['water']:
                cost+=wrap(300000*mult)
        if m and c['car']!='train':removeCount+=1
    return dict(legal=legal, masks=masks, constructionCost=cost if len(i['cells'])>1 else 0,
                removalCost=removeCount*100000*(abs(i['z']-i['water'])+1) if len(i['cells'])>1 else 0,
                removalKeepsRail=True)


def footprint(i):
    x,y,z=i['size'];base=i['base']
    cells=[[base[0]+a,base[1]+b,base[2]+c] for c in range(z) for b in range(y) for a in range(x)]
    # The loader, rather than PThreeDimStructure's old summary comment,
    # decides visibility: top hats, bottom front edges, invisible middles.
    visible=[p for p in cells if p[2]==base[2]+z-1 or
             (z>1 and p[2]==base[2] and (p[0]==base[0] or p[1]==base[1]+y-1))]
    cut=i['cut']; normal=cut<0 or z<cut
    require(len(i['reclaimable'])==len(cells),'Incomplete footprint occupancy')
    return dict(occupied=cells, visible=visible, invisible=[p for p in cells if p not in visible],
                draw='sprite' if normal else 'base-shape', cutColor=[179,115,51],
                portLegal=i['inside'] and i['flat'] and all(i['reclaimable']),
                sourceGenericControllerError=i['inside'] and i['flat'] and all(i['reclaimable']))


def variable_recipe(i):
    trace=[]
    for z in range(i['height']):
        if z>=i['height']-i['tops']:
            layers=[f'top:{i["height"]-z-1}']
            if i['overlay'] and z==i['bottoms']-1: layers.insert(0,f'bottom:{z}')
        elif z<i['bottoms']:
            layers=[f'bottom:{z}']
            if i['overlay'] and z==i['bottoms']-1:layers.append('middle')
        else: layers=['middle']
        trace.append(layers)
    return dict(layers=trace, cut=['base-shape']+['invisible']*(i['height']-1))


def half(i):
    compatible=i['firstSide']!=i['addSide'] and i['firstAxis']==i['addAxis']
    price=i['firstPrice']+(i['addPrice'] if compatible else 0)
    whole=i['remove']=='whole' or (i['remove']=='first' and not compatible)
    present=0 if whole else 1 if i['remove']=='first' else 2 if compatible else 1
    cut=i['cut'];draw=['shape' if cut>=0 else 'ground']
    for name in ('back','fore'):
        if name in i['art'] and (cut<0 or i['art'][name]['height']<cut):
            draw.append(name)
            if i['art'][name]['highlight']: draw.append(name+'-highlight')
    return dict(add=compatible, structurePriceBeforeRemove=price, halvesAfter=present,
                receipts=[i['firstPrice']]+([300000] if whole else []),
                listenerPopulation=None if whole else i['population'], isOwned=True, draw=draw)


def ground_cost(i):
    # LandBuilder's wrapper adjusts endpoint differences modulo the authored grid.
    ends=[]
    for a,b,g in zip(i['from'],i['to'],i['grid']):
        w=max(abs(a-b),g-1); w-=w%g
        ends.append([a-w,a] if a>b else [a,a+w])
    area=math.prod(b-a+1 for a,b in ends)
    calls=([i['price']]*i['success'] if i['kind'] in ('rice','vinyl') else [])
    if i['entry']=='side':calls=[]
    elif i['entry']=='wrapper':calls.append(i['price']*area)
    return dict(bounds=ends, area=area, spends=calls if i['owned'] else [],
                total=sum(calls) if i['owned'] else 0)


def construction_gate(i):
    ok=not i['weekend'] and 9<=i['hour']<=17
    b=i['below'];a=i['above'];s=i['state']
    if b is not None:
        ok=ok and b>=2 and i['belowMin']>s and (s!=1 or i['belowMin']>=3)
    if a is not None:
        ok=ok and (s!=2 or i['aboveMin']>=1) and (s!=3 or i['aboveMin']>=3)
    return dict(proceed=ok)


def construction_trace(i):
    now=i['start'];state=0;ground='ground';draws=iter(i['draws']);trace=[];calls=[]
    def take(bound,reason):
        value=next(draws);require(0<=value<bound,'Random draw bounds')
        calls.append(dict(stream='construction',bound=bound,value=value,reason=reason));return value
    if not i['southwest']:take(4,'constructor-door')
    now+=40+take(80,'delay')
    for allowed in i['allowed']:
        if i['cancelAt'] is not None and i['cancelAt']<=now:
            trace.append(dict(minute=now,state='abandoned',ground=ground));break
        if allowed:
            if state==0 and ground=='ground' and take(2,'ground-branch')==0:
                kind=take(4,'ground-kind');ground=['hole','steel','woods','machine'][kind]
                if kind==3:ground='machine'+str(take(3,'machine-kind')+1)
            else: state+=1
        trace.append(dict(minute=now,state=['empty','bone1','bone2','bone3','walled'][state],ground=ground))
        if state==4:break
        now+=40+take(80,'delay')
    require(list(draws)==[],'Unused random draws')
    completed=state==4
    return dict(trace=trace, random=calls, completionCount=int(completed),
                completionOrder=['remove-site','create-structure','attach-provider'] if completed else [],
                portCancelRemovesQueuedCallbacks=i['cancelAt'] is not None,
                sourceQueuedCallbackAfterRemove=i['cancelAt'] is not None)


def queue(i):
    entries=[e for e in i['entries'] if e['id'] not in i['cancel']]
    return dict(order=[e['id'] for e in sorted(entries,key=lambda e:(e['due'],e['registration']))])


def growth(i):
    lo=math.trunc(i['land']*i['scale']);hi=max(lo+15,math.trunc(lo*1.2))
    return dict(strength=(i['imported']+i['exported']*.1)*.2,budget=math.trunc(i['trains']),
                priceWindow=[lo*10000,hi*10000],amp=10+math.trunc(i['land']**i['ampPower']*i['ampScale']))


def indexes(i):
    def find(a,p,last):
        if not a:return 'invalid-empty-array'
        n=len(a)>>1;pos=n
        while True:
            n=(n+1)>>1
            if last:
                pos=min(len(a)-1,pos+n) if a[pos]<=p else max(0,pos-n)
            else:pos=max(0,pos-n) if a[pos]>=p else min(len(a)-1,pos+n)
            if n<=1:break
        if last:
            if p==a[min(len(a)-1,pos+1)]:pos=min(len(a)-1,pos+1)
        elif p>a[pos]:pos+=1
        return pos
    a,b=i['fixed'],i['variable'];lo,hi=i['window']
    w=[find(a,lo,False),find(a,hi,True),find(b,lo,False),find(b,hi,True)]
    if any(isinstance(v,str) for v in w):return dict(work=w,outcome='invalid-state',selectionDraws=[])
    n1=w[1]-w[0];n2=w[3]-w[2];n=n1+n2
    if n<=0:return dict(work=w,outcome='End',selectionDraws=[])
    r=i['draw'];require(0<=r<n,'Selection draw bounds')
    pool='fixed' if r<n1 else 'variable';idx=w[0]+r if r<n1 else w[2]+r-n1
    return dict(work=w,outcome=pool+':'+str(idx),selectionDraws=[dict(stream='development',bound=n,value=r)])


def replace(i):
    threshold=math.trunc(i['land']*2.5)
    ok=not i['owned'] and not i['construction']
    if i['kind']=='subsidiary':ok=ok and (i['structure']<threshold or i['structure']<i['totalLand'])
    elif i['kind']=='land':ok=ok and i['value']<=threshold
    return dict(replaceable=ok,threshold=threshold)


def valuation(i):
    t=i['structure'];prices=[];p=0
    for land in i['land']:
        prime=.01*(1+math.log(t+1));maximum=.01*(t+1)*math.log(t+1)
        profit=math.trunc(prime*(2 if t<land else .5)*(land-t)+maximum)
        p+=t+land+10*profit;prices.append(p)
    return dict(marketPrices=prices, variableEntityValue=i['unitPrice']*i['height'])


def arithmetic(i):
    n=math.prod(i['factors'])
    return dict(sourceUncheckedInt32=wrap(n),portChecked='overflow' if not -(2**31)<=n<2**31 else n)


def settings(i):
    # Branch-local hazards, not a newly imposed balancing range. P7-28 must
    # validate the complete reachable arithmetic against admitted world/content.
    value=float(i['value']);kind=i['kind']
    reason=None
    if not math.isfinite(value):reason='nonfinite'
    elif kind=='cadence' and (value<=0 or value!=math.trunc(value)):reason='nonpositive-or-noninteger-cadence'
    elif kind=='maxPricePower' and (value<=0 or not math.isfinite(1/value)):
        reason='invalid-reciprocal-power'
    elif kind=='amp' and value<0:reason='negative-Next-bound'
    elif kind=='amp' and (value>2**31-1 or value!=math.trunc(value)):reason='invalid-Int32-Next-bound'
    elif kind=='float' and abs(value)>3.4028234663852886e38:reason='outside-float32'
    elif kind=='powerAtZero' and value<0:reason='nonfinite-power-at-zero'
    return dict(portAccept=reason is None,reason=reason,
                capturedIntervalMinutes=i['registeredHours']*60,
                depositFactor=value/4 if kind=='cadence' and reason is None else None)


def provider(i):
    raw=checked(i['base']*i['height'])
    weekend=i['day']=='Saturday' or i['holiday']
    rate=({6:20,7:50,8:40} if weekend else {6:40,7:100,8:80})[i['hour']]
    outgoing=checked((checked(i['base']*rate)//100)*i['height'])
    cap=checked(checked(i['land']*i['xy'])*max(1,i['height']//4)+10)
    return dict(residents=raw, outgoing=outgoing, perStation=min(outgoing//i['stations'],cap),
                entering=checked((checked(i['base']*(5 if i['hour']==6 else 10))//100)*i['height']),
                attachAt='completed-structure', duringConstruction=0)


def weekly(i):
    score=(sum(v**.2 for v in i['totals'])/7)**5
    return dict(lastWeekPerDay=score,today=0,yesterday=i['totals'][-1],thisWeek=0,
                developmentBudget=math.trunc(score))


def plan(i):
    kind=i['kind'];p=i['price'];h=i['minHeight'];area=i['size'][0]*i['size'][1]
    cost=sum({e['id']:e['value'] for e in i['entities']}.values())
    if kind=='variable':
        while p*h<cost and h<i['maxHeight']:h+=1
        value=final=p*h
    elif kind=='land':
        h=1;value=p*4;final=p*area
    else:value=final=p
    return dict(height=h,planValue=value,finalPrice=final,uniqueReplacementValue=cost,
                cube=[*i['size'],h],timed=kind!='land',initiallyOwned=False,upfrontBookings=[])


def station_effect(i):
    delta=f32(min(f32(i['lastWeekImported']/24),i['passengers'])) if i['kind']=='unload' else f32(i['passengers'])
    deposit=f32(f32(delta*i['frequency'])/4)
    if i['kind']=='load' and i['total']==0:delta=deposit=None
    return dict(deltaQ=delta,storedQ=deposit,trainEvents=1,
                globalTrafficNotification=i['kind']=='unload',activeBundledTrafficSubscribers=0)


def fixed_sprites(i):
    xsize,ysize,zsize=i['size'];ox,oy=i['origin'];h=i['offset'];sprites=[]
    for z in range(zsize):
        for y in range(ysize):
            for x in range(xsize):
                rect=None;anchor=None
                if z==zsize-1:
                    vx=(x+y)*16+ox;vy=oy+h-16*(zsize-1)+(y-x)*8
                    sx,sy,w,sh=vx,vy,32,16
                    if y==0 or x==xsize-1:
                        sy-=16;sh+=16
                        if not (y==0 and x==xsize-1):
                            if y==0 and ysize>1:w=16
                            elif x==xsize-1 and xsize>1:sx+=16;w-=16
                    if sy<0:sh+=sy;sy=0
                    if sh!=0:rect=[sx,sy,w,sh];anchor=[vx-sx,vy-sy]
                elif z==0 and (x==0 or y==ysize-1):
                    vx=(x+y)*16+ox;vy=oy+h+(y-x)*8
                    sx,sy=vx,vy-(zsize-2)*16-8
                    w=32 if x==0 and y==ysize-1 else 16
                    if x!=0:sx+=16
                    rect=[sx,sy,w,16*(zsize-1)+8];anchor=[vx-sx,vy-sy]
                sprites.append(dict(cell=[x,y,z],crop=rect,anchor=anchor))
    return dict(sprites=sprites)


def random_flow(i):
    values=iter(i['values']);trace=[]
    def draw(stream,bound):
        v=next(values);require(0<=v<bound or bound==v==0,'Random flow bounds')
        trace.append(dict(stream=stream,bound=bound,value=v));return v
    kind=i['kind'];result=[]
    if kind=='forest':
        count=sum(draw('forest',2)==0 for _ in range(i['density']*2))
        for _ in range(count):
            x=draw('forest',16);y=draw('forest',16);p=draw('forest',i['sprites'])
            result.append([x+y,((-x+y)>>1)+8,p])
        for a in range(count-1):
            for b in range(a+1,count):
                if result[a][1]>result[b][1]:
                    # Source intentionally leaves sprite pattern slots in place.
                    for k in (0,1):result[a][k],result[b][k]=result[b][k],result[a][k]
    elif kind=='pickPlace':
        for valid in i['legal'][:4]:
            x=i['base'][0]+draw('development',i['amp'])-draw('development',i['amp'])
            y=i['base'][1]+draw('development',i['amp'])-draw('development',i['amp'])
            result.append([x,y])
            if valid:break
    elif kind=='landSize':
        result=[4+draw('development',2)-draw('development',2) for _ in range(2)]
    elif kind=='landSelection':
        result=[draw('land-selection',i['count']) if legal else None for legal in i['legal']]
    elif kind=='market':
        result=draw('subsidiaries',10)==0 if not i['owned'] and not i['listed'] else None
    elif kind=='timeLength':
        v=next(values);require(0<=v<1,'Double draw bounds')
        trace.append(dict(stream='time-length',bound='[0,1)',value=v))
        result=math.trunc(20160+v*20160)
    elif kind=='constructionInit':
        sx,sy,sz=i['size']
        for z in range(sz):
            for x in range(sx):
                for y in range(sy):
                    door=False if x==0 and y==sy-1 else draw('construction',4)==0
                    due=i['start']+40+draw('construction',80)
                    result.append(dict(cell=[x,y,z],door=door,due=due))
    else:raise ValueError('Unknown random flow')
    require(list(values)==[],'Unused random flow values')
    return dict(random=trace,result=result)


def raw_pixels(raw):
    """Independent packed BMP reader used only by the literal source oracle."""
    def u(p,n,signed=False):return int.from_bytes(raw[p:p+n],'little',signed=signed)
    require(raw[:2]==b'BM' and u(30,4)==0,'Unsupported raw BMP')
    start=u(10,4);dib=u(14,4);w=u(18,4,True);sh=u(22,4,True);bpp=u(28,2)
    require(bpp in (4,8,24) and w>0 and sh!=0,'Unsupported raw pixels')
    palette=[tuple(raw[p:p+3][::-1]) for p in range(14+dib,start,4)]
    stride=((w*bpp+31)//32)*4; pixels=[]
    for y in range(abs(sh)):
        row=start+(abs(sh)-y-1 if sh>0 else y)*stride
        for x in range(w):
            if bpp==24:rgb=tuple(raw[row+3*x:row+3*x+3][::-1])
            else:
                byte=raw[row+x*(bpp)//8]
                idx=byte if bpp==8 else (byte>>(4 if x%2==0 else 0))&15
                rgb=palette[idx]
            pixels.append(rgb)
    return w,abs(sh),pixels


def art(i, source_root=None):
    if source_root:
        raw=subprocess.check_output(['git','-C',str(source_root),'show',
             read('city-review.json')['authority']['revision']+':trunk/'+i['path']])
    else:
        path=ROOT/'content/bundled-v2/source/trunk'/i['path']
        require(path.is_file(),'Missing pinned resource: '+i['path'])
        raw=path.read_bytes()
    require(hashlib.sha256(raw).hexdigest()==i['sourceSha256'],'Raw art hash drift')
    w,h,pixels=raw_pixels(raw);key=pixels[0]
    x,y,cw,ch=i['crop'];require(0<=x and 0<=y and x+cw<=w and y+ch<=h,'Invalid source crop')
    selected=[pixels[b*w+a] for b in range(y,y+ch) for a in range(x,x+cw)]
    rgba=bytes(c for p in selected for c in (*p,0 if p==key else 255))
    return dict(size=[w,h],key=list(key),cropRgbaSha256=hashlib.sha256(rgba).hexdigest(),
                opaque=sum(p!=key for p in selected),transparent=sum(p==key for p in selected),
                probes=[list((*p,0 if p==key else 255)) for p in (selected[0],selected[len(selected)//2],selected[-1])])


EVALUATORS = dict(definitions=definition, heat=heat, coordinates=hv, conductivity=conductivity,
                  trade=trade, ulv=ulv, ulvCache=ulv_cache, roads=roads, roadRoutes=road_route,
                  footprints=footprint, variableRecipes=variable_recipe, halves=half,
                  groundCosts=ground_cost, constructionGates=construction_gate,
                  constructionTraces=construction_trace, fifo=queue, growth=growth,
                  candidateIndexes=indexes, replacement=replace, valuation=valuation,
                  arithmetic=arithmetic, settings=settings, providers=provider, weekly=weekly,
                  fixedSprites=fixed_sprites, randomFlows=random_flow, plans=plan,
                  stationEffects=station_effect, art=art)


def matrix(doc):
    return {k:v for k,v in doc.items() if k!='sha256' and k!='groups'} | {
        'groups':{k:{a:b for a,b in g.items() if a!='cases'} | {
            'cases':[{a:b for a,b in c.items() if a!='expected'} for c in g['cases']]}
                  for k,g in doc['groups'].items()}}


def equal(a,b,tolerant=False):
    if isinstance(a,float) or isinstance(b,float):
        return type(a) in (int,float) and type(b) in (int,float) and (
            math.isclose(a,b,rel_tol=1e-12,abs_tol=1e-12) if tolerant else a==b)
    if type(a)!=type(b):return False
    if isinstance(a,dict):return a.keys()==b.keys() and all(equal(a[k],b[k],tolerant) for k in a)
    if isinstance(a,list):return len(a)==len(b) and all(equal(x,y,tolerant) for x,y in zip(a,b))
    return a==b


def validate(doc,source_root=None):
    original=deepcopy(doc);review=read('city-review.json')
    require(doc['slice']=='P7-02' and doc['status']=='independent-source-fixtures' and
            doc['runtimeStatus']=='city-gameplay-unimplemented','Overclaimed fixture scope')
    require(doc['authority']==review['authority'],'Authority drift')
    require(doc['sha256']==digest({k:v for k,v in doc.items() if k!='sha256'}),'Fixture digest drift')
    require(digest(matrix(doc))==MATRIX_SHA256,'Reviewed input/mapping matrix drift')
    for name,sha in doc['inputs'].items():
        require(hashlib.sha256((ROOT/'doc/parity'/name).read_bytes()).hexdigest()==sha,'Prerequisite hash drift: '+name)
    sources={r['path']:r for r in doc['sources']}
    ledger={s['baselinePath'].removeprefix('trunk/'):s for s in read('exav-ledger.json')['fileInventory']}
    for path,row in sources.items():
        old=next((s for s in review['sourceReviews'] if s['path']==path),None)
        if old is not None:require(row==old,'Source binding drift')
        else:
            require(row['sha256']==ledger[path]['sha256'] and row['bindings'],
                    'Supplementary source binding drift')
        if source_root:
            raw=subprocess.check_output(['git','-C',str(source_root),'show',doc['authority']['revision']+':trunk/'+path])
            require(hashlib.sha256(raw).hexdigest()==row['sha256'],'Pinned source drift')
            lines=raw.decode(row['encoding']).splitlines()
            require(all(lines[b['line']-1]==b['text'] for b in row['bindings']),'Pinned excerpt drift')
    require(doc['groups'].keys()==EVALUATORS.keys(),'Fixture family drift')
    ids=set();count=0
    for family,g in doc['groups'].items():
        require(g['sources'] and set(g['sources'])<=sources.keys() and g['runtimeOwner'] and
                g['derivation'] and g['acceptance'],'Unbound fixture family')
        for c in g['cases']:
            require(c['id'] not in ids,'Duplicate fixture ID');ids.add(c['id']);count+=1
            try:
                result=art(c['input'],source_root) if family=='art' else EVALUATORS[family](c['input'])
            except OverflowError as e:result=dict(error=str(e))
            require(equal(result,c['expected'],family=='weekly'),f'Expectation mismatch: {family}/{c["id"]}\nactual={result}\nexpected={c["expected"]}')
    scenarios={s['id']:s for s in read('exav-scenarios.json')['scenarios']}
    for b in doc['scenarioBindings']:
        require(b['expectedSha256']==digest(scenarios[b['id']]['expected']) and
                b['fixtures'] and set(b['fixtures'])<=ids and b['acceptance'],'Scenario binding drift')
    for b in doc['acceptanceCompanions']:
        require(b['fixtures'] and set(b['fixtures'])<=ids and b['owner'] and b['expected'] and
                b['status']=='specified-runtime-unverified','Unbound acceptance companion')
    inherited=read('station-fixtures.json')['groups']
    for b in doc['inheritedBindings']:
        rows={c['id']:c for c in inherited[b['group']]['cases']}
        require(b['cases']=={id:digest(c) for id,c in rows.items()} and b['owner'] and
                b['scope']=='immutable-P6-source-companion-not-city-runtime', 'Inherited fixture binding drift')
    streams={s['id'] for s in read('random-review.json')['durableStreams']}
    for b in doc['randomContracts']:
        require(b['stream'] in streams|{'local-ui','coordinate-local'} and b['order'] and
                b['noDraw'] and set(b['sources'])<=sources.keys(),'Random contract drift')
    require(doc==original,'Evaluator mutated input')
    return dict(cases=count,families=len(doc['groups']),sources=len(sources),
                randomContracts=len(doc['randomContracts']),runtimeAcceptance=False)


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--check',action='store_true',required=True)
    p.add_argument('--source-root',type=Path)
    args=p.parse_args()
    print(json.dumps(validate(json.loads(FIXTURES.read_text()),args.source_root),sort_keys=True))


if __name__=='__main__':main()
