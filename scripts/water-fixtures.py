#!/usr/bin/env python3
"""Independent XY/heap/min-plus oracle for WORLDGEN-WATER-1.
No runtime calls. Literal geometry, path and component witnesses are reviewed inputs.
"""
import argparse
from collections import deque
import hashlib
import heapq
import importlib.util
import json
import math
from functools import lru_cache
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / 'doc/parity/water-fixtures.json'

def module(name):
    spec = importlib.util.spec_from_file_location(name, ROOT / f'scripts/{name}-fixtures.py')
    mod = importlib.util.module_from_spec(spec); spec.loader.exec_module(mod); return mod
relief, coast = module('relief'), module('coast')

def hashed(seed, index):
    n = ((seed ^ index) * 0x9e3779b9) & 0xffffffff
    n ^= n >> 16; n = n * 0x846ca68b & 0xffffffff
    return n ^ (n >> 16)

def stage(seed, name, attempt):
    return int.from_bytes(hashlib.sha256(f'freetrain.world-generation.v1\nterrain-generation-1\n{seed}\n{name}\n{attempt}'.encode()).digest()[:4], 'little') & 0x7fffffff

@lru_cache(maxsize=65536)
def fractal(seed, x, y, scale):
    def noise(wavelength, octave):
        def lattice(x,y):
            n=(seed ^ x*0x9e3779b9 ^ y*0x85ebca6b ^ (octave+1)*0xc2b2ae35)&0xffffffff
            n^=n>>16;n=n*0x7feb352d&0xffffffff
            n^=n>>15;n=n*0x846ca68b&0xffffffff;n^=n>>16
            return n>>16
        def fade(t):return (t*t//65536)*(3*65536-2*t)//65536
        def mix(a,b,t):return a+relief.trunc((b-a)*t,65536)
        u,v=x*65536//wavelength,y*65536//wavelength
        ix,iy=u//65536,v//65536;fx,fy=fade(u%65536),fade(v%65536)
        return mix(mix(lattice(ix,iy),lattice(ix+1,iy),fx),mix(lattice(ix,iy+1),lattice(ix+1,iy+1),fx),fy)
    return (4*noise(scale,0)+2*noise(max(2,scale//2),1)+noise(max(1,scale//4),2))//7

def fixture(lakes=1, direction=0, sides=0, island=False, width=1, seed=0, meander=70, h=64, v=64, z=32, water=4, area=3):
    inputs = dict(lakes=lakes,direction=direction,sides=sides,island=island,width=width,seed=seed,meander=meander,h=h,v=v,z=z,water=water,area=area if lakes else 0)
    if sides or island:
        base = coast.fixture(sides,island,seed,30,h,v,z,water,geometry=True)
        if 'error' in base: return dict(**inputs,error=base['error'])
    else: base = relief.fixture('hills',seed,50,24,50,h,v,z,water,geometry=True)
    sites = base['sites']; index = {p:i for i,p in enumerate(sites)}
    xy = list(sites); size = len(sites)
    adjacent = [[index[q] for q in [(x,y-1),(x+1,y),(x-1,y),(x,y+1)] if q in index] for x,y in sites]
    boundary = {i for i in range(size) if i%h in [0,h-1] or i//h in [0,v-1]}
    xmin,xmax = min(x for x,y in sites),max(x for x,y in sites)
    ymin,ymax = min(y for x,y in sites),max(y for x,y in sites)
    def sector(i):
        x,y = sites[i]; xx,yy = 2*x-xmin-xmax,2*y-ymin-ymax
        return (1 if yy<0 else 4) if abs(yy)*(xmax-xmin)>=abs(xx)*(ymax-ymin) else (8 if xx<0 else 2)
    def square(i,width,apron=False):
        a=-(width//2); b=a+width-1
        if apron: a-=1; b+=1
        x,y=sites[i]
        return [index[x+dx,y+dy] for dy in range(a,b+1) for dx in range(a,b+1) if (x+dx,y+dy) in index]
    def distances(seeds,blocked=set(),fringe=set()):
        distance = [-1]*size; queue=deque(sorted(seeds))
        for i in seeds: distance[i]=0
        while queue:
            i=queue.popleft()
            for n in adjacent[i]:
                if distance[n]<0 and (n not in blocked or n in fringe):
                    distance[n]=distance[i]+1
                    if n not in fringe: queue.append(n)
        return distance
    def inspect(row):
        heights = [[4*row['ground'][i]+((row['corners'][i]>>shift)&15) for shift in [12,8,4,0]] for i in range(size)]
        wet = {i for i,c in enumerate(heights) if min(c)<4*water}
        ids=[-1]*size; sizes=[]; contacts=[]
        def face(i,n):
            x,y=sites[i]; xx,yy=sites[n]
            a,b = (0,1) if xx>x else (2,3) if xx<x else (0,3) if yy<y else (1,2)
            return min(heights[i][a],heights[i][b])<4*water
        for start in sorted(wet):
            if ids[start]>=0: continue
            group=len(sizes);queue=deque([start]);ids[start]=group;count=0;touches=0
            while queue:
                i=queue.popleft();count+=1
                if i in boundary: touches |= sector(i)
                for n in adjacent[i]:
                    if n in wet and ids[n]<0 and face(i,n): ids[n]=group;queue.append(n)
            sizes.append(count);contacts.append(touches)
        return heights,wet,ids,sizes,contacts,face
    original = inspect(base)[1]
    def normalize(ground):
        return relief.fixture('hills',seed,50,24,50,h,v,z,water,shape=lambda sites,raw:dict(zip(sites,ground)),geometry=True)
    def check(row, count):
        heights,wet,ids,sizes,contacts,face=inspect(row)
        enclosed=[n for n,t in zip(sizes,contacts) if not t]
        target=size*area//100
        if any(abs(n-target)>max(size//100,target//4) for n in enclosed): raise ValueError('lake-area')
        if len(enclosed)>lakes or not count and len(enclosed)!=lakes: raise ValueError('lake-rim')
        axis=5 if direction==1 and count else 10 if direction==2 and count else 0
        expected=(15 if island else sides)|axis
        actual=0
        for contact in contacts: actual|=contact
        if actual!=expected: raise ValueError('river-boundary')
        if any(i in wet and not expected&sector(i) or island and i not in wet for i in boundary): raise ValueError('river-boundary')
        if axis and not any(contact&axis==axis for contact in contacts): raise ValueError('river-connectivity')
        return heights,wet,ids,sizes,contacts,face
    failure=None
    for attempt in range(4):
        try:
            ground=list(base['ground']);reserved=set(original)|boundary;centers=[]
            lake_seed=stage(seed,'lakes',attempt)
            for lake in range(lakes):
                distance=distances(reserved)
                center=min(range(size),key=lambda i:(-distance[i],hashed(lake_seed+lake,i)))
                root=math.isqrt(2*(size*area//100))
                if root*root<2*(size*area//100): root+=1
                target=max(1,size*area//100-4*root)
                radius=math.isqrt(size*area//100)
                if radius*radius<size*area//100:radius+=1
                scale=max(4,radius//3);cx,cy=sites[center]
                def score(i):
                    x,y=sites[i];displacement=fractal(lake_seed+lake,x,y,scale)-32768
                    return ((x-cx)**2+(y-cy)**2)*(65536+displacement)
                queue=[(score(center),center)];seen={center};core=[]
                while queue and len(core)<target:
                    _,i=heapq.heappop(queue)
                    if distance[i]<4: continue
                    core.append(i)
                    for n in adjacent[i]:
                        if n not in seen: seen.add(n);heapq.heappush(queue,(score(n),n))
                if len(core)!=target: raise ValueError('lake-space')
                centers.append(center)
                for i in core:
                    for n in square(i,1,True): ground[n]=water-1;reserved.add(n)
            lake_world=normalize(ground) if lakes else base
            check(lake_world,0)
            ground=list(lake_world['ground'])
            heights,wet,ids,sizes,contacts,face=inspect(lake_world)
            water_distance=distances(wet);edge_distance=distances(boundary)
            paths=[];river_seed=stage(seed,'rivers',attempt)
            gate={i for i in boundary if any(n not in boundary for n in adjacent[i])}
            if direction:
                source_side,target_side=(1,4) if direction==1 else (2,8)
                axis=source_side|target_side
                other=distances({i for i in wet if contacts[ids[i]]!=0 and not sector(i)&axis})
                blocked={i for i in range(size) if 0<=other[i]<=width+3}
                middle=(h-1+(v-1)//2)//2 if direction==1 else (h-1+v//2)//2
                def pick(side):
                    candidates=[i for i in gate if i not in blocked and sector(i)==side]
                    if not candidates: raise ValueError('river-space')
                    return min(candidates,key=lambda i:(abs(sites[i][0 if direction==1 else 1]-middle),hashed(river_seed,i)))
                source,target=pick(source_side),pick(target_side)
                goals={target};blocked|=boundary-{source,target}
                def route(roughness):
                    costs={source:0};previous={source:None};queue=[(0,source)]
                    while queue:
                        cost,i=heapq.heappop(queue)
                        if cost!=costs[i]: continue
                        if i in goals:
                            result=[]
                            while i is not None: result.append(i);i=previous[i]
                            return result[::-1]
                        for n in adjacent[i]:
                            if n in blocked and n not in goals: continue
                            value=cost+100+roughness[n]
                            if value<costs.get(n,2**31-1): costs[n]=value;previous[n]=i;heapq.heappush(queue,(value,n))
                    raise ValueError('river-route')
                roughness=[0]*size
                if meander:
                    guide=route(roughness);sx,sy=sites[source];ex,ey=sites[guide[-1]]
                    dx,dy=ex-sx,ey-sy;length2=dx*dx+dy*dy;length=max(1,abs(dx),abs(dy))
                    scale=max(6,min(h,v)//5);amplitude=max(1,min(h,v)*meander//400);offsets=[]
                    for k in range(length+1):
                        t=k*65536//length
                        x=sx+relief.trunc(dx*t,65536);y=sy+relief.trunc(dy*t,65536)
                        noise=fractal(river_seed,x,y,scale)-32768
                        offsets.append(relief.trunc(noise*amplitude*4*t*(65536-t),65536**3))
                    for i,(x,y) in enumerate(sites):
                        dot=(x-sx)*dx+(y-sy)*dy
                        along=max(0,min(length*65536,relief.trunc(dot*length*65536,length2)));k=along//65536
                        offset=offsets[k]+relief.trunc((offsets[min(k+1,length)]-offsets[k])*(along%65536),65536)
                        cross=relief.trunc(dx*(y-sy)-dy*(x-sx),length)
                        roughness[i]=min(800,abs(cross-offset)*24)
                path=route(roughness)
                if path is None: raise ValueError('river-route')
                paths.append(path)
                for i in path:
                    for n in square(i,width,True): ground[n]=water-1
            final=normalize(ground)
            heights,wet,ids,sizes,contacts,face=check(final,int(direction!=0))
            for path in paths:
                group=ids[path[-1]]
                if group<0 or ids[path[0]]!=group: raise ValueError('river-outlet')
                for k,i in enumerate(path):
                    if ids[i]!=group or k and not face(path[k-1],i): raise ValueError('river-connectivity')
                    if any(ids[n]!=group or max(heights[n])>=4*water for n in square(i,width)): raise ValueError('river-width')
                    if (water_distance[i]<0 or water_distance[i]>2*(width+6)) and edge_distance[i]>2*(width+6):
                        x,y=sites[i];xx,yy=sites[path[k+1]];px,py=sites[path[max(0,k-1)]]
                        dx=(xx>px)-(xx<px);dy=(yy>py)-(yy<py)
                        bank_sides=set()
                        for n in square(i,width+8):
                            if n in wet: continue
                            qx,qy=sites[n];cross=dx*(qy-y)-dy*(qx-x)
                            if cross: bank_sides.add((cross>0)-(cross<0))
                        if bank_sides!={-1,1}: raise ValueError('river-banks')
            return dict(**inputs,groundHash=final['groundHash'],cornerHash=final['cornerHash'],
                wetCells=len(wet),enclosedSizes=[n for n,t in zip(sizes,contacts) if not t],openComponents=sum(t!=0 for t in contacts),
                lakeCenters=centers,paths=paths,attempt=attempt)
        except ValueError as error: failure=str(error)
    return dict(**inputs,error=failure)


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');args=parser.parse_args()
    cases=[fixture(),fixture(lakes=3),fixture(lakes=1,direction=1),fixture(lakes=0,direction=2),fixture(island=True)]
    cases += [fixture(direction=direction,island=True) for direction in [1,2]]
    cases += [fixture(direction=direction,sides=side) for direction in [1,2] for side in [1,2,4,8]]
    cases += [fixture(lakes=0,direction=1,sides=5,width=4),fixture(lakes=0,direction=2,sides=10,width=4),
        fixture(lakes=2,seed=-2147483648),fixture(lakes=0,direction=2,seed=2147483647,width=4),
        fixture(lakes=0,direction=1,meander=0),fixture(lakes=8,h=16,v=16,z=8,water=1),
        fixture(lakes=1,h=17,v=16,z=8,water=1,area=1),fixture(lakes=1,h=64,v=64,z=64,water=60,area=20),
        fixture(lakes=0,direction=1,h=128,v=127,seed=-2147483648),
        fixture(lakes=0,direction=2,h=17,v=16,width=4),fixture(lakes=8,h=128,v=127,area=1)]
    output=json.dumps(dict(profile='terrain-generation-1',algorithm='WORLDGEN-WATER-1',
        oracle='Independent canonical XY dictionaries, min-plus normalization, heap routing and shared-edge water floods; literal geometry and river path witnesses.',cases=cases),indent=2)+'\n'
    if args.check: assert OUTPUT.read_text()==output,'Water fixtures differ'
    else: OUTPUT.write_text(output)
    print(f'Water fixtures {"verified" if args.check else "written"}: {len(cases)} cases ({sum("error" in c for c in cases)} explicit impossible requests)')

if __name__=='__main__': main()
