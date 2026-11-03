#!/usr/bin/env python3
"""Read-only P5-02 source oracle; no production imports or expectation writes.

The checked-in literals are source derivations, never native runtime captures.
Train gameplay consumers remain with the implementing P5 slices.
"""
import argparse
from copy import deepcopy
from functools import cache
import hashlib
import json
import math
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
FIXTURES = ROOT / 'doc/parity/train-fixtures.json'
OFFSETS = ((0,-1),(1,-1),(1,0),(1,1),(0,1),(-1,1),(-1,0),(-1,-1))
SCENARIOS = {f'EXAV-SCN-{n:03}' for n in (4,5,6,7,8,9,14,26,31)}


def require(ok, message):
    if not ok:
        raise ValueError(message)


def read(name):
    return json.loads((ROOT / 'doc/parity' / name).read_text())


def digest(value):
    return hashlib.sha256(json.dumps(value, ensure_ascii=False, sort_keys=True,
                                    separators=(',', ':')).encode()).hexdigest()


def step(p, d, dz=0):
    x,y = OFFSETS[d]
    return [p[0]+x,p[1]+y,p[2]+dz]


@cache
def ports(pattern):
    return read('railway-fixtures.json')['patterns'][pattern]['connections']


def guide(pattern, heading, choice='straight', previous=None, location=None):
    edges = ports(pattern)
    require((heading+4)%8 in edges, 'Illegal incoming rail heading')
    if heading in edges:
        if 12 <= pattern < 28 and any(d in edges for d in ((heading-1)%8,(heading+1)%8)):
            straight = previous == step(location,heading) if previous is not None else choice == 'straight'
            if not straight:
                return next(d for d in ((heading-1)%8,(heading+1)%8) if d in edges)
        return heading
    return next(d for d in ((heading-1)%8,(heading+1)%8) if d in edges)


def zdiff(pattern, heading):
    if pattern < 28:
        return 0
    level, climb = (pattern-28)%4, ((pattern-28)%16)//4*2
    return 1 if level == 1 and heading == climb else -1 if level == 2 and heading == (climb+4)%8 else 0


def reverse(cars, rails, choice='straight'):
    result = []
    # Source visits original array order before swapping, including follower routing.
    for n,c in enumerate(cars):
        c = deepcopy(c)
        if c['state'] == 'Inside':
            p = next(r['pattern'] for r in rails if r['location'] == c['location'])
            previous = result[n-1]['location'] if n else None
            c['heading'] = (guide(p,c['heading'],choice,previous,c['location'])+4)%8
        result.append(c)
    return result[::-1]


def speed(native, limits):
    if 'low' in limits: return 4
    if 'medium' in limits: return native if native == 4 else 3
    if 'fast' in limits: return native if native in (3,4) else 2
    return native


def guidance(i):
    return {'outgoing':guide(i['pattern'],i['incoming'],i.get('controller','straight'),
                             i.get('previous'),i.get('location'))}


def ramp(i):
    cars = deepcopy(i['cars']); frames=[]; cash=i['cash']; count=0
    for n in range(3):
        p=i['patterns'][n]; h=i['heading']; loc=step(cars[0]['location'],h,zdiff(p,h))
        require(loc == i['locations'][n+1] and (h+4)%8 in ports(i['patterns'][n+1]),
                'Illegal reciprocal ramp walk')
        previous=deepcopy(cars[0]); cars[0]['location']=loc; cars[1]['location']=previous['location']
        count+=1; spend=80000 if count == 1 else 0; cash-=spend
        frames.append({'cars':deepcopy(cars),'moveCount':count,'cash':cash,'spend':spend,
                       'due':i['now']+(n+1)*i['native'],'order':i['nextOrder']+n})
    return {'steps':frames}


def reversal(i):
    return {'cars':reverse(i['cars'],i['rails']), 'isReversed':not i['isReversed'],
            'moveCount':i['moveCount'],'queue':i['queue'],'cash':i['cash']}


def callback(i):
    cars=deepcopy(i['cars']); prior=deepcopy(cars[0]); calls=[]; rev=i['isReversed']
    forwarding=i['forwarding']; stop=i['stop']; count=i['moveCount']; cash=i['cash']
    calls.append({'method':'stop','train':i['train'],'controller':i['controller'],
                  'location':prior['location'],'callCount':i['stopCallCount']})
    stop_count=i['stopCallCount']; spent=0; source_fault=None
    if stop>0:
        stop_count+=1; state='StoppingAtStation'; delay=stop
    else:
        forwarding=stop in (-999,-1000); stop_count=0
        if stop<0 and stop != -999:
            cars=reverse(cars,i['rails']); rev=not rev
        def candidate():
            c=cars[0]; rail=next(r for r in i['rails'] if r['location']==c['location'])
            out=guide(rail['pattern'],c['heading'],i['junction'])
            if 12<=rail['pattern']<28 and out != (c['heading']+4)%8:
                edges=ports(rail['pattern'])
                if c['heading'] in edges and any(d in edges for d in ((c['heading']-1)%8,(c['heading']+1)%8)):
                    calls.append({'method':'junction','train':i['train'],'controller':i['controller'],'location':c['location']})
            loc=step(c['location'],out,zdiff(rail['pattern'],c['heading']))
            r=next((r for r in i['rails'] if r['location']==loc),None)
            if r is None or (out+4)%8 not in ports(r['pattern']): return None
            return dict(c,state='Inside',location=loc,heading=out)
        next_car=candidate()
        if next_car is None:
            cars=reverse(cars,i['rails']); rev=not rev; next_car=candidate()
            if next_car is None: source_fault='null-dereference-after-reversal'
        occupied=[c['location'] for c in cars]+i['occupied']
        blocked=next_car is None or next_car['location'] in occupied
        state='EmergencyStopping' if blocked else 'Moving'
        if not blocked:
            old=deepcopy(cars)
            for n,c in enumerate(cars):
                pose=next_car if n==0 else old[n-1]
                c.update({k:pose[k] for k in ('state','location','heading')})
            spent=(len(cars)*20+i['passengers']//20)*2000 if count%4==0 else 0
            count+=1; cash-=spent
        delay=speed(i['native'],i['priorLimits'])
    queue=deepcopy(i['queue'])+[{'handler':i['train'],'due':i['now']+delay,'order':i['nextOrder']}]
    queue.sort(key=lambda a:(a['due'],a['order']))
    return {'cars':cars,'isReversed':rev,'forwarding':forwarding,'state':state,
            'stopCallCount':stop_count,'moveCount':count,'cash':cash,'spend':spent,
            'calls':calls,'queue':queue,'nextOrder':i['nextOrder']+1,'sourceFault':source_fault}


def return_points(location, heading):
    x,y=OFFSETS[heading]
    if heading%2==0:
        vectors=[(y,-x),(-y,x),(y-2*x,-x-2*y),(-y-2*x,x-2*y),(-x,-y)]
    else:
        # Matrix stores doubled integers and C# truncates division towards zero.
        t=lambda n: n//2 if n>=0 else -((-n)//2)
        vectors=[(t(-x+y),t(-x-y)),(t(-x-y),t(x-y)),
                 (t(-3*x+y),t(-x-3*y)),(t(-3*x-y),t(x-3*y)),(-x,-y)]
    return [[location[0]+a,location[1]+b,location[2]] for a,b in vectors]


def outside(i):
    c=deepcopy(i['car']); points=return_points(c['location'],c['heading']); calls=[]
    found=next((n for n in range(5) if n in i['connected']),None)
    blocked=False
    if i['operation']=='reverse':
        if found is not None:
            c['location']=step(points[found],c['heading']); c['countdown']=100-c['countdown']
    elif c['countdown']!=0:
        c['countdown']-=1
    elif found is None:
        c['countdown']=10
    elif found in i['occupied']:
        blocked=True
    else:
        c.update(state='Inside',location=points[found],heading=(c['heading']+4)%8)
        del c['countdown']
    passengers=i['passengers']; origin=deepcopy(i['boardingSource']); revenue=0
    moving=i['operation']=='advance' and not blocked
    if moving and c['state']=='Outside' and c['countdown']==50:
        if origin is not None:
            distance=math.isqrt(sum((a-b)**2 for a,b in zip(origin['location'],i['car']['location'])))
            revenue=passengers*i['fare']*distance*2
        origin=None
        calls.append('unload'); passengers=0
        if not i['forwarding']:
            calls.append('load'); passengers=min(100,i['capacity']); origin=deepcopy(i['car'])
    count=i['moveCount']+int(moving)
    spend=(i['length']*20+passengers//20)*2000 if moving and i['moveCount']%4==0 else 0
    return {'candidates':points,'selected':found,'car':c,'calls':calls,'passengers':passengers,
            'forwarding':i['forwarding'],'moveCount':count,'spend':spend,'revenue':revenue,
            'boardingSource':origin,'cash':i['cash']+revenue-spend,
            'state':'EmergencyStopping' if blocked else 'Moving',
            'queue':i['queue'] if i['operation']=='reverse' else
            sorted(i['queue']+[{'handler':'T','due':i['now']+i['native'],'order':i['nextOrder']}],key=lambda x:(x['due'],x['order']))}


def timetable(i):
    if i['kind']=='daily':
        wait=(i['departure']-i['arrival'])%1440
        return {'wait':wait or 1440}
    def matches(mask):
        return all(v==-1 or i['clock'][k]==v for k,v in mask.items())
    if i['kind']=='mask': return {'matches':matches(i['mask'])}
    wait=10 if i['callCount']==0 else 10-i['clock']['minutes']%10
    matched=None
    for r in i['rules']:
        if not matches(r['mask']): continue
        a=r['action']
        if i['callCount']==0:
            if a not in ('pass','stop'): continue
            wait=0 if a=='pass' else 10
        else:
            if a=='pass': continue
            wait={'go':0,'reverse':-1,'forwarding':-999,'rforwarding':-1000}.get(a,wait)
        matched=r['id']; break
    return {'wait':wait,'matched':matched}


def turning(i):
    cars=deepcopy(i['cars']); rails=i['rails']; requested=i['requested']; events=[]
    if cars[0]['state']!='Inside':
        return {'cars':cars,'isReversed':i['isReversed'],'result':requested,'events':[],
            'afterCentral':reverse(cars,rails),'afterCentralReversed':not i['isReversed'],'forwarding':requested==-1000,'queue':i['queue']}
    head=deepcopy(cars[0]); cars=reverse(cars,rails); rev=not i['isReversed']; events.append('reverse')
    tail=cars[0]; straight=True
    for c in reversed(cars):
        if c['state']!='Inside': straight=False; break
        r=next((r for r in rails if r['location']==c['location']),None)
        if r is None or len(ports(r['pattern']))!=2 or abs(ports(r['pattern'])[0]-ports(r['pattern'])[1])!=4:
            straight=False; break
    result=requested
    if straight:
        if len(cars)==1:
            d=cars[0]['heading']; straight=d%2==0
            if not straight: result=-1000
            lower=d<=2
        else:
            dx=tail['location'][0]-head['location'][0]; dy=tail['location'][1]-head['location'][1]
            straight=dx!=dy; lower=dy<0 or dx>0
        if straight:
            anchor=head['location'] if lower else tail['location']; events.append('remove-cars')
            # cars[] in source is the order after its first reverse.
            placement=deepcopy(cars)
            for c in cars: c.update(state='Unplaced',location=None,heading=None)
            if lower:
                cars=cars[::-1]; placement=placement[::-1]; rev=not rev; events.append('reverse-unplaced')
            p=list(anchor); d=None
            by_id={c['id']:c for c in cars}
            for c in reversed(placement):
                pattern=next(r['pattern'] for r in rails if r['location']==p)
                if d is None: d=min(ports(pattern))
                by_id[c['id']].update(state='Inside',location=list(p),heading=d)
                d=guide(pattern,d); p=step(p,d)
            events.append('place-cars')
            if lower:
                cars=reverse(cars,rails); rev=not rev; events.append('reverse')
    after=reverse(cars,rails) if result<0 and result!=-999 else deepcopy(cars)
    return {'cars':cars,'isReversed':rev,'result':result,'events':events,
            'afterCentral':after,'afterCentralReversed':not rev,'forwarding':result==-1000,'queue':i['queue']}


def render(i):
    h=i['heading']; reversed_=i['isReversed']; wrapper=i['wrapper']; family=i['family']
    x,y=0,-9
    if i['kind']=='level':
        out=i['outgoing']; diff=(out-h)%8
        require(diff in (0,1,7),'Illegal drawing curve')
        if diff:
            diff=-1 if diff==7 else diff; dd=(2*out+3*diff)%16
            x=3 if 2<dd<10 else -3; y+=2 if 6<dd<=14 else -2
        angle=(2*h+diff)%16
        if reversed_: angle^=8
        selector=angle if family=='asymmetric' else angle&7
        source_angle=angle^8 if wrapper else angle
        frame=source_angle if family=='asymmetric' else source_angle&7
        rect=[frame%8*32,(frame//8)*32,32,32]
    else:
        require(h%2==0 and i['climb']%2==0,'Illegal slope drawing heading')
        y+=[0,-4,8,4][i['level']]
        d=(h+4)%8 if reversed_ else h; climbing=(h==i['climb'])!=reversed_
        selector=d+(0 if climbing else 1) if family=='asymmetric' else (d if climbing else (d+4)%8)//2
        if wrapper: d=(d+4)%8; climbing=not climbing
        frame=d+(0 if climbing else 1) if family=='asymmetric' else (d if climbing else (d+4)%8)//2
        rect=[frame*32,64 if family=='asymmetric' else 32,32,32]
    return {'offset':[x,y],'selector':selector,'frame':frame,'rectangle':rect}


def cost(i):
    charged=[n for n in range(1,i['moves']+1) if (i['initialMoveCount']+n-1)%4==0]
    expense=(i['cars']*20+i['passengers']//20)*2000
    return {'chargedMoveNumbers':charged,'costPerCharge':expense,'operatingExpenditure':expense*len(charged),
            'sales':i['fareReceiptAfterMoves'],'balance':i['fareReceiptAfterMoves']-expense*len(charged),
            'cashAfter':i['cashBefore']+i['fareReceiptAfterMoves']-expense*len(charged)}


def sale(i):
    revenue=0 if i['ageDays'] is None else i['price']//(i['ageDays']//365+2)
    return {'revenue':revenue,'cash':i['cash']+revenue,
            'cars':[dict(c,state='Unplaced',location=None,heading=None) for c in i['cars']],
            'inventory':[], 'queue':
            [q for q in i['queue'] if q['handler']!=i['train']], 'groupItems':[], 'listeners':[]}


def signal(i):
    calls=[]; waits=[]
    for t in i['trains']:
        diff=(t['heading']-i['direction'])%8
        delegates=min(diff,8-diff)<=1
        if delegates: calls.append({'controller':t['controller'],'train':t['id'],'harbor':i['harbor'],'callCount':t['callCount']})
        waits.append(t['wait'] if delegates else 0)
    return {'calls':calls,'waits':waits,'signalControllerId':None}


def yard(i):
    delta=i['platformLength']-i['trainLength']
    position=(i['platformLength']+i['trainLength'])//2-1 if i['sameDirection'] else (delta//2 if delta>=0 else -((-delta)//2))
    calls=[]; result=0
    if 0<=position<i['platformLength'] and position==i['index'] and i['host']:
        calls=['controller']+i['handlerEffects']; result=i['result']
        if i['callCount']==0 and result>0: calls.append('unload')
        if i['callCount']!=0 and result<=0:
            if result not in (-999,-1000): calls.append('load')
            calls.append('bell')
    return {'stopPosition':position,'result':result,'calls':calls}


def exit_world(i):
    p=step(i['car']['location'],i['car']['heading'],i['zdiff'])
    hsize,vsize,zsize=i['size']; xx=p[0]-(vsize-1)//2
    h=(xx+p[1])//2; v=p[1]-xx
    border=h<0 or h>=hsize or v<2*p[2] or v>=vsize-(zsize-p[2])*2
    require(border,'Illegal exit fixture')
    return {'storage':[h,v,p[2]],'car':dict(i['car'],state='Outside',location=p,countdown=100),
            'destinationRailChecked':False,'due':i['now']+i['native']}


def numeric(i):
    return {'minutes':speed(i['native'],i['limits']),'admitted':0<i['native']<=2147483647}


def timing(i):
    wait=i.get('stopMinutes')
    native=wait or 'outside' in i.get('afterHead','') or i['beforeHead']=='outside'
    return {'minutes':wait if wait else i['native'] if native else speed(i['native'],[i['priorLimit']]),
            'lookup':None if native else 'A'}


def queue(i):
    pending=deepcopy(i['pending']); calls=[]; order=i['nextOrder']
    for event in sorted(i['executing'],key=lambda e:(e['due'],e['order'])):
        calls.append(event['handler'])
        if event.get('cancel'): pending=[e for e in pending if e['handler']!=event['cancel']]
        pending.append({'handler':event['handler'],'due':i['now']+event['delay'],'order':order}); order+=1
    return {'calls':calls,'pending':sorted(pending,key=lambda e:(e['due'],e['order'])),'nextOrder':order}


EVALUATORS={'guides':guidance,'ramps':ramp,'reversals':reversal,'callbacks':callback,'outside':outside,
            'timetables':timetable,'turning':turning,'rendering':render,'costs':cost,'sales':sale,
            'signals':signal,'numericSpeed':numeric,'timing':timing,'queue':queue,
            'yards':yard,'exits':exit_world,
            'drawing':lambda i:{'order':['road.before','accessory.before','rail.before','car','rail.after','accessory.after']}}


def required_ids():
    handoff=read('railway-p5-handoff.json'); speeds=read('speed-limit-handoff.json')
    return {
        'guides':{c['id'] for c in handoff['guideCases']},
        'ramps':{c['id'] for c in handoff['rampCases']},
        'reversals':{f'reverse-{h}-{turn}' for h in range(8) for turn in (-1,0,1)},
        'callbacks':{f'move-{h}' for h in range(8)}|{'curve-follow','junction-follow','blocked-other','blocked-own-tail','dead-end',
            'both-null','both-null-after-handler-reverse','stop-positive','go-clears-forwarding','forwarding','reverse-forwarding','reverse'},
        'outside':{f'return-{h}-{n}' for h in range(8) for n in range(5)}|
            {f'{kind}-{h}' for h in range(8) for kind in ('return-blocked','return-none','reverse-found','reverse-none')}|
            {f'countdown-{n}' for n in (100,51,1)}|{f'exchange-{cap}-{f}' for cap in (0,40,160) for f in (False,True)}|
            {'exchange-fare-service','exchange-fare-forwarding'},
        'timetables':{'daily-479','daily-480','daily-481','mask-any','mask-exact'}|
            {f'mask-mismatch-{k}' for k in ('month','day','dayOfWeek','hour','minutes')}|
            {'first-pass-stop','first-stop-pass','first-default','later-go','later-reverse','later-forwarding','later-rforwarding','later-stop-go','later-next-boundary','later-masked-go'},
        'turning':{f'turn-{h}-{length}-{f}' for h in range(8) for length in (1,3) for f in (-1,-1000)}|
            {'turn-curve','turn-junction','turn-outside','turn-mixed-outside'},
        'rendering':{f'level-{family}-{wrapper}-{h}-{turn}-{r}' for family,wrapper in (('symmetric',False),('asymmetric',False),('asymmetric',True),('color',False))
                     for h in range(8) for turn in (-1,0,1) for r in (False,True)}|
            {f'slope-{family}-{wrapper}-{h}-{climb}-{level}-{r}' for family,wrapper in (('symmetric',False),('asymmetric',False),('asymmetric',True),('color',False))
             for climb in (0,2,4,6) for h in (climb,(climb+4)%8) for level in range(4) for r in (False,True)},
        'costs':{'SCN014'}, 'sales':{f'sale-{age}' for age in (0,364,365,730,None)},
        'signals':{c['id'] for c in handoff['signalCases']}|{'shared-signal-two-controllers'},
        'numericSpeed':{f'speed-{n}' for n in range(len(speeds['numericBoundaryVectors']))}|{'speed-overlap'},
        'timing':{c['id'] for c in speeds['timingBoundaryVectors']},
        'queue':{'equal-time-retry','cancel-preserves-neighbor'}, 'drawing':{'DRAW-traffic-order'},
        'yards':{'yard-first-stop','yard-first-pass','yard-depart-go','yard-depart-reverse','yard-depart-forwarding',
                 'yard-depart-rforwarding','yard-handler-effects','yard-wait-again','yard-no-host','yard-wrong-index',
                 'yard-overlong-same','yard-overlong-opposite','yard-too-long-same','yard-too-long-opposite'},
        'exits':{f'exit-{h}' for h in range(8)},
    }


def validate(data, source_root=None):
    review=read('train-review.json'); handoff=read('railway-p5-handoff.json'); speeds=read('speed-limit-handoff.json')
    require(data['slice']=='P5-02' and data['status']=='independent-source-fixtures' and
            data['runtimeStatus']=='train-gameplay-unimplemented' and data['authority']==review['authority'], 'Fixture authority/status drift')
    require(data['sha256']==digest({k:v for k,v in data.items() if k!='sha256'}),'Fixture digest drift')
    inputs={'train-review.json','exav-scenarios.json','railway-p5-handoff.json','speed-limit-handoff.json','railway-fixtures.json','exav-geometry.json'}
    require(set(data['inputs'])==inputs,'Missing prerequisite binding')
    for path,sha in data['inputs'].items():
        require(hashlib.sha256((ROOT/'doc/parity'/path).read_bytes()).hexdigest()==sha,'Input artifact drift: '+path)
    known={s['path']:s for s in review['sourceReviews']}
    sources={s['path']:s for s in data['sources']}
    require(len(sources)==len(data['sources']) and sources,'Missing/duplicate source')
    for path,s in sources.items():
        require(s==known.get(path),'Unreviewed source: '+path)
        if source_root:
            raw=subprocess.check_output(['git','-C',str(source_root),'show',data['authority']['revision']+':trunk/'+path])
            require(hashlib.sha256(raw).hexdigest()==s['sha256'],'Pinned source drift: '+path)
            lines=raw.decode(s['encoding']).splitlines()
            for b in s['bindings']: require(lines[b['line']-1]==b['text'],'Pinned excerpt drift: '+path)
    expected=required_ids(); require(data['groups'].keys()==expected.keys(),'Missing/unknown fixture family')
    all_ids=set(); count=0; bound_sources=set()
    for name,g in data['groups'].items():
        require(g['derivation'] and g['sources'] and set(g['sources'])<=sources.keys(),'Missing source derivation: '+name)
        bound_sources.update(g['sources'])
        require(g['decisions'] and set(g['decisions'])<={d['id'] for d in review['decisions']},'Unbound decision')
        ids=[c['id'] for c in g['cases']]
        require(len(ids)==len(set(ids)) and set(ids)==expected[name],'Incomplete fixture matrix: '+name)
        require(not all_ids.intersection(ids),'Duplicate global case ID'); all_ids.update(ids)
        for c in g['cases']:
            before=deepcopy(c['input'])
            for key in ('cars',):
                cars=before.get(key,[])
                if isinstance(cars,list):
                    require(len({c['id'] for c in cars})==len(cars),'Duplicate car identity')
                    placed=[tuple(c['location']) for c in cars if c['state']=='Inside']
                    require(len(placed)==len(set(placed)),'Duplicate occupancy')
                    for car in cars:
                        require(car['state'] in ('Inside','Outside','Unplaced') and (car['heading'] is None or car['heading'] in range(8)), 'Illegal car state')
                        if car['state']=='Inside':
                            rail=next((r for r in before.get('rails',[]) if r['location']==car['location']),None)
                            require(rail is not None and (car['heading']+4)%8 in ports(rail['pattern']),'Illegal starting pose: '+c['id'])
                    for predecessor,follower in zip(cars,cars[1:]):
                        if predecessor['state']==follower['state']=='Inside':
                            r=next(r for r in before['rails'] if r['location']==follower['location'])
                            out=guide(r['pattern'],follower['heading'],previous=predecessor['location'],location=follower['location'])
                            require(step(follower['location'],out,zdiff(r['pattern'],follower['heading']))==predecessor['location'],
                                    'Disconnected consist: '+c['id'])
            if name=='outside':
                require(before['car']['state']=='Outside' and 0<=before['car']['countdown']<=100 and
                        set(before['occupied'])<=set(before['connected'])<=set(range(5)), 'Illegal Outside candidates')
            if name=='timetables' and before['kind'] in ('mask','rules'):
                masks=[before['mask']] if before['kind']=='mask' else [r['mask'] for r in before['rules']]
                ranges={'month':(1,12),'day':(1,31),'dayOfWeek':(0,6),'hour':(0,23),'minutes':(0,59)}
                for mask in masks:
                    require(mask.keys()==ranges.keys() and all(v==-1 or ranges[k][0]<=v<=ranges[k][1] for k,v in mask.items()),
                            'Illegal timetable mask')
            result=EVALUATORS[name](c['input'])
            require(result==c['expected'],'Fixture mismatch: '+name+'/'+c['id'])
            require(before==c['input'],'Oracle mutated input: '+c['id']); count+=1
    require(bound_sources==sources.keys(),'Orphaned source binding')
    # Exact reuse bindings prevent handoff IDs being attached to unrelated fixtures.
    def case(group,id): return next(c for c in data['groups'][group]['cases'] if c['id']==id)
    for c in handoff['guideCases']:
        f=case('guides',c['id']); require(f['input']=={k:v for k,v in c.items() if k not in ('id','outgoing')} and f['expected']=={'outgoing':c['outgoing']},'Guide handoff drift')
    for c in handoff['rampCases']:
        f=case('ramps',c['id'])
        require(all(f['input'][k]==c[k] for k in ('locations','patterns','heading')),'Ramp handoff drift')
    for n,c in enumerate(speeds['numericBoundaryVectors']):
        f=case('numericSpeed',f'speed-{n}')
        require(f['input']=={'native':c['nativeMinutesPerVoxel'],'limits':[c['limit']]} and f['expected']['minutes']==c['expectedMinutes'],'Speed handoff drift')
    for c in speeds['timingBoundaryVectors']:
        f=case('timing',c['id']); require(f['input']=={k:v for k,v in c.items() if k not in ('id','expectedMinutes','lookup','behavior')} and f['expected']=={'minutes':c['expectedMinutes'],'lookup':c['lookup']},'Timing handoff drift')
    scenarios={s['id']:s for s in read('exav-scenarios.json')['scenarios']}
    require(data['scenarioBindings'].keys()==SCENARIOS,'Missing scenario binding')
    for id,b in data['scenarioBindings'].items():
        require(b['input']==scenarios[id]['input'] and b['expected']==scenarios[id]['expected'] and
                b['cases'] and set(b['cases'])<=all_ids,'Scenario binding drift: '+id)
    return count,len(sources)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root',type=Path); parser.add_argument('--check',action='store_true')
    args=parser.parse_args(); count,sources=validate(json.loads(FIXTURES.read_text()),args.source_root)
    print(f'P5-02: {count} literal cases, {len(EVALUATORS)} families, {sources} pinned sources; train gameplay remains unimplemented.')


if __name__=='__main__': main()
