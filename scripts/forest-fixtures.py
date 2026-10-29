#!/usr/bin/env python3
"""Independent WORLDGEN-FOREST-1 XYZ component/forest oracle over literal terrain."""
import argparse
from collections import deque
import hashlib
import importlib.util
import json
from pathlib import Path
import struct
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / 'doc/parity/forest-fixtures.json'

def load(name):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(name+'-fixtures.py'))
    module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module); return module

relief, coast = load('relief'), load('coast')

def stage(seed, name):
    return int.from_bytes(hashlib.sha256(f'freetrain.world-generation.v1\nterrain-generation-1\n{seed}\n{name}\n0'.encode()).digest()[:4], 'little') & 0x7fffffff

def hashed(seed, i):
    n = (seed ^ i*0x9e3779b9) & 0xffffffff
    n = ((n ^ (n >> 16))*0x7feb352d) & 0xffffffff
    n = ((n ^ (n >> 15))*0x846ca68b) & 0xffffffff
    return n ^ (n >> 16)

def noise(seed, x, y, scale):
    def lattice(x,y):
        n = (seed ^ x*0x9e3779b9 ^ y*0x85ebca6b ^ 0xc2b2ae35) & 0xffffffff
        n = ((n ^ (n >> 16))*0x7feb352d) & 0xffffffff
        n = ((n ^ (n >> 15))*0x846ca68b) & 0xffffffff
        return (n ^ (n >> 16)) >> 16
    def fade(t): return (t*t//65536)*(3*65536-2*t)//65536
    def mix(a,b,t): return a+relief.trunc((b-a)*t,65536)
    u,v = x*65536//scale,y*65536//scale
    ix,iy = u//65536,v//65536; fx,fy = fade(u%65536),fade(v%65536)
    return mix(mix(lattice(ix,iy),lattice(ix+1,iy),fx),mix(lattice(ix,iy+1),lattice(ix+1,iy+1),fx),fy)

def realize(row, coverage, clustering, starting, palette):
    h,v,seed,water = [row[k] for k in ('h','v','seed','water')]
    sites,ground,corners = row['sites'],row['ground'],row['corners']; domain = {tuple(p):i for i,p in enumerate(sites)}
    def neighbors(i):
        x,y = sites[i]
        return sorted(domain[p] for p in [(x-1,y),(x+1,y),(x,y-1),(x,y+1)] if p in domain)
    eligible = {i for i,g in enumerate(ground) if g>=water}
    flat = {i for i in eligible if corners[i]==0}; seen=set(); components=[]
    for start in sorted(flat):
        if start in seen: continue
        component={start}; todo=[start]; seen.add(start)
        while todo:
            for n in neighbors(todo.pop()):
                if n in flat and n not in seen and ground[n]==ground[start]:
                    component.add(n);seen.add(n);todo.append(n)
        root=min(component,key=lambda i:(hashed(stage(seed,'starting-area'),i),i))
        components.append((len(component),hashed(stage(seed,'starting-area'),root),root))
    largest,tie,root = min(components,key=lambda c:(-c[0],c[1])) if components else (0,0,-1)
    if largest<starting: return dict(error='starting-area')
    reserved=set()
    if starting:
        reserved.add(root); queue=deque([root])
        while len(reserved)<starting:
            i=queue.popleft()
            for n in neighbors(i):
                if len(reserved)==starting: break
                if n in flat and n not in reserved and ground[n]==ground[root]:reserved.add(n);queue.append(n)
    eligible-=reserved
    mseed=stage(seed,'forest-mask');scale=max(4,min(h,v)//8)
    order=sorted(eligible,key=lambda i:((noise(mseed,*sites[i],scale)*clustering+(hashed(mseed,i)>>16)*(100-clustering))//100,i))
    target=len(eligible)*coverage//100; forests=[]
    palette=sorted(palette,key=lambda p:p[0])
    for i in sorted(order[:target]):
        state=hashed(stage(seed,'forest-scatter'),i)|1
        def draw(bound):
            nonlocal state
            state=(state ^ (state<<13)) & 0xffffffff;state^=state>>17;state=(state ^ (state<<5)) & 0xffffffff
            return state*bound>>32
        id,density,sprites=palette[draw(len(palette))]
        count=sum(draw(2)==0 for _ in range(2*density));positions=[];patterns=[]
        for _ in range(count):
            x,y=draw(16),draw(16);positions.append([x+y,((-x+y)>>1)+8]);patterns.append(draw(sprites))
        for a in range(count-1):
            for b in range(a+1,count):
                if positions[a][1]>positions[b][1]:positions[a],positions[b]=positions[b],positions[a]
        if count:forests.append(dict(index=i,id=id,trees=[p+[s] for p,s in zip(positions,patterns)]))
    payload='\n'.join(f"{f['index']}:{f['id']}:"+';'.join(','.join(map(str,t)) for t in f['trees']) for f in forests)
    mask=bytes(i in reserved for i in range(h*v))
    return dict(eligibleCells=len(eligible),maskCells=target,forestCells=len(forests),trees=sum(len(f['trees']) for f in forests),reservedCells=len(reserved),largestFlatArea=largest,
                forestHash=hashlib.sha256(payload.encode()).hexdigest(),startingHash=hashlib.sha256(mask).hexdigest(),examples=forests[:3])

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');args=parser.parse_args()
    manifest=ET.parse(ROOT/'content/bundled-v2/expanded/trunk/plugins/org.kohsuke.freetrain.land.forest/plugin.xml')
    definitions=[(c.attrib['id'],int(c.findtext('density')),int(c.findtext('count'))) for c in manifest.findall('contribution') if c.find('density') is not None]
    rows=[]
    cases=[(0,0,50,0,0,False),(0,15,0,64,0,False),(0,70,100,64,1,False),(42,35,50,0,2,False),(-2147483648,90,100,16,0,False),(2147483647,90,0,0,0,False),(0,35,50,16,0,True),(42,70,100,0,2,True),(0,35,50,200,0,True)]
    for seed,coverage,cluster,starting,variant,island in cases:
        terrain=coast.fixture(0,island=True,seed=seed,geometry=True) if island else relief.fixture('hills',seed,50,24,50,h=32,v=27,geometry=True)
        suffix=['','-m','-g'][variant];palette=[p for p in definitions if (p[0].endswith(suffix) if suffix else p[0].endswith('}'))]
        row=dict(id=f'WORLDGEN-FOREST-{len(rows)+1:03}',seed=seed,coverage=coverage,clustering=cluster,starting=starting,variant=variant,island=island,h=32,v=27,z=32,water=4)
        row.update(realize(terrain,coverage,cluster,starting,palette));rows.append(row)
    # Each species alone, including all three ground treatments.
    for id,density,sprites in definitions:
        terrain=relief.fixture('plains',-1,0,4,0,h=16,v=17,geometry=True)
        rows.append(dict(id=f'WORLDGEN-FOREST-{len(rows)+1:03}',seed=-1,coverage=90,clustering=50,starting=64,tree=id,h=16,v=17,z=32,water=4,**realize(terrain,90,50,64,[(id,density,sprites)])))
    result=json.dumps(dict(profile='terrain-generation-1',algorithm='WORLDGEN-FOREST-1',oracle='Independent Python XYZ components, source manifest density/count, fixed-point mask and cell-local realized scatter. UTF-8 row-index:contribution:x,y,sprite forest records; byte reservation grid.',cases=rows),ensure_ascii=False,indent=2)+'\n'
    if args.check:assert OUTPUT.read_text()==result,'Forest fixtures differ'
    else:OUTPUT.write_text(result)
    print(f'Forest fixtures {"verified" if args.check else "written"}: {len(rows)} cases')

if __name__=='__main__':main()
