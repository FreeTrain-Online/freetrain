#!/usr/bin/env python3
"""Independent XYZ distance fields, min-plus normalization and surface flood oracle."""
import argparse
from collections import deque
import hashlib
import importlib.util
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('relief', Path(__file__).with_name('relief-fixtures.py'))
relief = importlib.util.module_from_spec(spec)
spec.loader.exec_module(relief)
OUTPUT = ROOT / 'doc/parity/coast-fixtures.json'


def fixture(sides, island=False, seed=0, percent=40, h=32, v=27, z=32, water=4, geometry=False):
    sites = [(col-row//2+(v-1)//2, col+(row+1)//2) for row in range(v) for col in range(h)]
    domain = set(sites)
    xmin, xmax = min(x for x, y in sites), max(x for x, y in sites)
    ymin, ymax = min(y for x, y in sites), max(y for x, y in sites)
    def sector(p):
        x, y = p
        qx, qy = 2*x-xmin-xmax, 2*y-ymin-ymax
        return (1 if qy < 0 else 4) if abs(qy)*(xmax-xmin) >= abs(qx)*(ymax-ymin) else (8 if qx < 0 else 2)
    boundary = {p: sector(p) for i, p in enumerate(sites) if i % h in (0, h-1) or i//h in (0, v-1)}
    selected = 15 if island else sides
    def neighbors(p):
        x, y = p
        return [q for q in [(x-1,y),(x+1,y),(x,y-1),(x,y+1)] if q in domain]
    def distances(seeds, blocked=(), can_cross=None):
        found = dict.fromkeys(seeds, 0)
        queue = deque(found)
        while queue:
            p = queue.popleft()
            for q in neighbors(p):
                if q not in found and q not in blocked and (can_cross is None or can_cross(p,q)):
                    found[q] = found[p]+1
                    queue.append(q)
        return found
    dry = {p for p, s in boundary.items() if not s & selected}
    sea = distances([p for p in boundary if p not in dry], dry)
    retained = distances(dry | {sites[(v//2)*h+h//2]})
    def wet_surface(ground, corners):
        return {p for p, g, c in zip(sites, ground, corners) if 4*g+min((c >> shift) & 15 for shift in [12,8,4,0]) < water*4}
    def passages(ground, corners):
        heights = {p:[4*g+((c >> shift) & 15) for shift in [12,8,4,0]] for p,g,c in zip(sites,ground,corners)}
        def connected(p,q):
            dx,dy = q[0]-p[0],q[1]-p[1]
            a,b = (0,1) if dx>0 else (2,3) if dx<0 else (0,3) if dy<0 else (1,2)
            return min(heights[p][a],heights[p][b]) < 4*water
        return connected
    def repair(sites, ground, corners):
        wet = wet_surface(ground, corners)
        connected = distances(wet & boundary.keys(), domain-wet, passages(ground,corners))
        enclosed = wet-connected.keys()
        return [water+1 if p in enclosed else g for p, g in zip(sites, ground)] if enclosed else None
    def metrics(row):
        wet = wet_surface(row['ground'], row['corners'])
        can_cross = passages(row['ground'],row['corners'])
        seen = set(); sea_count = land_count = 0; outlets = 0
        for start in sites:
            if start in seen: continue
            is_sea = start in wet
            component = distances([start], domain-wet if is_sea else wet, can_cross if is_sea else None)
            seen.update(component)
            if is_sea:
                sea_count += 1
                touches = {boundary[p] for p in component if p in boundary}
                if not touches: return 'coast-enclosed-sea'
                for side in touches: outlets |= side
            else: land_count += 1
        if land_count != 1: return 'coast-land-connectivity'
        if outlets != selected: return 'coast-boundary'
        if island and sea_count != 1: return 'coast-sea-connectivity'
        if island and not boundary.keys() <= wet: return 'coast-island-rim'
        if abs(100*len(wet)/len(sites)-percent)>5: return 'coast-coverage'
        return dict(seaCells=len(wet),landCells=len(sites)-len(wet),seaComponents=sea_count,landComponents=land_count,seaSides=outlets)
    for attempt in range(4):
        stage = int.from_bytes(hashlib.sha256(f'freetrain.world-generation.v1\nterrain-generation-1\n{seed}\ncoast\n{attempt}'.encode()).digest()[:4], 'little') & 0x7fffffff
        scale = max(4, min(h, v)//3)
        def noise(x, y, wavelength, octave):
            def lattice(x, y):
                n = (stage ^ x*0x9e3779b9 ^ y*0x85ebca6b ^ (octave+1)*0xc2b2ae35) & 0xffffffff
                n ^= n >> 16; n = n*0x7feb352d & 0xffffffff
                n ^= n >> 15; n = n*0x846ca68b & 0xffffffff; n ^= n >> 16
                return n >> 16
            def fade(t): return (t*t//65536)*(3*65536-2*t)//65536
            def mix(a, b, t): return a + relief.trunc((b-a)*t, 65536)
            u, v = x*65536//wavelength, y*65536//wavelength
            ix, iy = u//65536, v//65536
            fx, fy = fade(u % 65536), fade(v % 65536)
            return mix(mix(lattice(ix,iy),lattice(ix+1,iy),fx),mix(lattice(ix,iy+1),lattice(ix+1,iy+1),fx),fy)
        def shape(sites, raw):
            def key(i):
                p = sites[i]
                n = ((i ^ stage)*0x9e3779b9) & 0xffffffff
                n ^= n >> 16; n = (n*0x846ca68b) & 0xffffffff; n ^= n >> 16
                x, y = p
                fractal = (4*noise(x,y,scale,0)+2*noise(x,y,max(2,scale//2),1)+noise(x,y,max(1,scale//4),2))//7-32768
                score = sea[p]*65536//max(1,sea[p]+retained[p])
                score += relief.trunc(fractal*score*(65536-score), 65536*65536)
                return (score*256+(n & 255), i)
            order = sorted((i for i, p in enumerate(sites) if p in sea), key=key)
            target = len(sites)*(percent+attempt)//100
            if not 0 < target < len(order): raise ValueError('coast-coverage')
            carved = {sites[i] for i in order[:target]}
            return {p:water-1 if p in carved else raw[p] for p in sites}
        row = relief.fixture('hills',seed,50,24,50,h,v,z,water,shape=shape,repair=repair,geometry=True)
        result = metrics(row)
        if isinstance(result, dict): break
    inputs = dict(sides=sides,island=island,seed=seed,percent=percent,h=h,v=v,z=z,water=water)
    if isinstance(result, str): return dict(**inputs,error=result)
    if geometry: return row
    return dict(**inputs,groundHash=row['groundHash'],cornerHash=row['cornerHash'],**result)


def main():
    parser = argparse.ArgumentParser(); parser.add_argument('--check',action='store_true'); args = parser.parse_args()
    rows = [fixture(side) for side in range(1,16)] + [fixture(0,island=True)]
    rows += [fixture(0,island=True,seed=-2147483648,water=1,percent=25),
             fixture(0,island=True,seed=2147483647,water=28,percent=75),
             fixture(1,seed=42,h=17,v=16,z=8,water=1), fixture(8,seed=-1,h=16,v=17,z=8,water=4),
             fixture(0,island=True,h=16,v=16,z=8,water=1,percent=5)]
    output = json.dumps(dict(algorithm='WORLDGEN-COAST-1',profile='terrain-generation-1',
        oracle='Independent canonical XY dictionaries/BFS, min-plus ground normalization, literal surface and boundary-sector measurements.',cases=rows),indent=2)+'\n'
    if args.check:
        assert OUTPUT.read_text() == output, 'Coast fixtures differ'
        print(f'Coast fixtures verified: {len(rows)} cases')
    else:
        OUTPUT.write_text(output)
        print(f'Coast fixtures written: {len(rows)} cases')


if __name__ == '__main__': main()
