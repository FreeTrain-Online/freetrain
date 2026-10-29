#!/usr/bin/env python3
"""Independent WORLDGEN-RELIEF-1 oracle: min-plus graph, not runtime scan order."""
import argparse
import hashlib
import heapq
import json
from pathlib import Path

ONE = 65536
ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / 'doc/parity/relief-fixtures.json'


def trunc(n, d):
    return n // d if n >= 0 else -((-n) // d)


def fixture(kind, seed, amount, scale, smooth, h=32, v=32, z=32, water=4, shape=None, repair=None, geometry=False):
    stage = int.from_bytes(hashlib.sha256(f'freetrain.world-generation.v1\nterrain-generation-1\n{seed}\nrelief\n0'.encode()).digest()[:4], 'little') & 0x7fffffff
    def lattice(x, y, octave):
        n = stage ^ (x * 0x9e3779b9 & 0xffffffff) ^ (y * 0x85ebca6b & 0xffffffff) ^ ((octave+1) * 0xc2b2ae35 & 0xffffffff)
        n = ((n ^ (n >> 16)) * 0x7feb352d) & 0xffffffff
        n = ((n ^ (n >> 15)) * 0x846ca68b) & 0xffffffff
        return (n ^ (n >> 16)) >> 16
    def noise(x2, y, wavelength, octave):
        x, yy = x2 * ONE // (2*wavelength), y * ONE // wavelength
        def fade(t): return (t*t//ONE)*(3*ONE-2*t)//ONE
        def mix(a,b,t): return a+trunc((b-a)*t,ONE)
        i,j = x//ONE, yy//ONE
        fx,fy = fade(x%ONE), fade(yy%ONE)
        return mix(mix(lattice(i,j,octave),lattice(i+1,j,octave),fx),mix(lattice(i,j+1,octave),lattice(i+1,j+1,octave),fx),fy)
    def sample(h2, row):
        detail = (100-smooth)*256//100
        a,b,c = [noise(h2+row%2,row,w,i) for i,w in enumerate([scale,max(2,scale//2),max(1,scale//4)])]
        n = (a*256+b*detail+c*detail//2)//(256+detail+detail//2)
        if kind == 'mountains': n = ONE-abs(2*n-ONE)
        bottom=4*(water+1)
        amplitude=(4*(z-1)-bottom)*amount*{'plains':25,'hills':65,'mountains':100}[kind]//10000
        return bottom+amplitude*n//ONE
    sites = [(col-row//2+(v-1)//2,col+(row+1)//2) for row in range(v) for col in range(h)]
    heights = {xy:sample(2*(i%h),i//h)//4 for i,xy in enumerate(sites)}
    if shape is not None: heights = shape(sites, heights)
    raw = dict(heights)
    queue = [(g,xy) for xy,g in heights.items()]
    heapq.heapify(queue)
    while queue:
        g,(x,y) = heapq.heappop(queue)
        if heights[x,y] != g: continue
        for dx in [-1,0,1]:
            for dy in [-1,0,1]:
                q=(x+dx,y+dy)
                if q in heights and heights[q] > g+1:
                    heights[q]=g+1
                    heapq.heappush(queue,(g+1,q))
    index={xy:i for i,xy in enumerate(sites)}
    def corner(x,y):
        local=[heights[q] for q in [(x,y),(x+1,y),(x,y+1),(x+1,y+1)] if q in heights]
        lo,hi=min(local),max(local)
        surround=[heights[xx,yy] for xx in range(x-1,x+3) for yy in range(y-1,y+3) if (xx,yy) in heights]
        fine=0
        if lo==hi and max(surround)>hi and (x,y) in index:
            i=index[x,y];fine=sample(2*(i%h)+1,i//h)%4
        return 4*hi+fine
    packed=[]
    for xy in sites:
        x,y=xy;g=heights[xy]
        offsets=[corner(xx,yy)-4*g for xx,yy in [(x,y-1),(x,y),(x-1,y),(x-1,y-1)]]
        assert all(0<=c<=4 for c in offsets)
        packed.append(sum(c << shift for c,shift in zip(offsets,[12,8,4,0])))
    ground=[heights[xy] for xy in sites]
    if repair is not None:
        replacement = repair(sites, ground, packed)
        if replacement is not None:
            return fixture(kind, seed, amount, scale, smooth, h, v, z, water,
                           shape=lambda sites, raw: dict(zip(sites, replacement)), geometry=geometry)
    result = dict(kind=kind,seed=seed,amount=amount,featureScale=scale,smoothness=smooth,h=h,v=v,z=z,water=water,
                samples=[sample(hh,vv) for hh,vv in [(0,0),(1,0),(2,1),(17,9),(2*h-1,v-1)]],
                groundHash=hashlib.sha256(bytes(ground)).hexdigest(),cornerHash=hashlib.sha256(b''.join(c.to_bytes(2,'little') for c in packed)).hexdigest(),
                variation=sum(abs(ground[i+1]-ground[i]) for i in range(len(ground)) if i%h<h-1),
                curvature=sum(abs(ground[i-1]-2*ground[i]+ground[i+1]) for i in range(len(ground)) if 0<i%h<h-1),
                minimum=min(ground),maximum=max(ground),occupants=sum(c!=0 for c in packed),lowered=sum(raw[p]>heights[p] for p in sites))
    if geometry: result.update(sites=sites,ground=ground,corners=packed)
    return result


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');args=parser.parse_args()
    presets={'plains':(20,32,75),'hills':(50,24,50),'mountains':(85,32,25)}
    rows=[fixture(kind,seed,*preset) for seed in [0,42] for kind,preset in presets.items()]
    rows += [fixture('mountains',0,100,8,0),fixture('mountains',0,100,8,100),fixture('mountains',-2147483648,100,4,0,z=64),fixture('hills',2147483647,100,128,100,h=16,v=17,z=8,water=4),fixture('plains',-1,0,4,0)]
    rows=[dict(id=f'WORLDGEN-RELIEF-{i:03d}',**row) for i,row in enumerate(rows,1)]
    output=json.dumps(dict(profile='terrain-generation-1',algorithm='WORLDGEN-RELIEF-1',oracle='Independent Python fixed-point evaluator and multi-source shortest-path lowering; SHA256 of row-major byte ground and UInt16LE packed corners.',cases=rows),indent=2)+'\n'
    if args.check:
        assert OUTPUT.read_text()==output, 'Relief fixtures differ'
        print(f'Relief fixtures verified: {len(rows)} cases')
    else:
        OUTPUT.write_text(output)
        print(output)

if __name__ == '__main__': main()
