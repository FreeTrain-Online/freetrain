#!/usr/bin/env python3
"""Check actual packed Save8 coasts using independent XYZ face floods, including large maps."""
import argparse
import base64
from collections import deque
import hashlib
import json
from pathlib import Path
import struct


def check(path):
    raw = path.read_bytes(); saved = json.loads(raw)
    assert saved['saveVersion'] == 8 and saved['map']['coordinateSystem'] == 'hv'
    h, v = saved['map']['width'], saved['map']['height']
    assert 16 <= h <= 2048 and 16 <= v <= 2048 and h*v <= 1700000
    packed = saved['packedTerrain']; request = json.loads(packed['provenance']['requestJson'])
    coast = request['Options']['Coast']; assert coast['Kind'] in (1, 2)
    selected = 15 if coast['Kind'] == 1 else coast['Sides']
    water = packed['waterLevel']; center = (v-1)//2
    ground = base64.b64decode(packed['cells'], validate=True); assert len(ground) == h*v
    wet = bytearray((g & 63) < water for g in ground)
    corners_wet = bytearray(15 if is_wet else 0 for is_wet in wet)
    occupants = base64.b64decode(packed['occupants'], validate=True)
    offset = 0
    for _ in range(packed['occupantCount']):
        x, y = struct.unpack_from('<ii', occupants, offset+18)
        col, row = (x-center+y)//2, y-x+center
        assert 0 <= col < h and 0 <= row < v
        assert occupants[offset+36] == 0 and occupants[offset+42] == 0  # coast-only mountains
        corners = struct.unpack_from('<H', occupants, offset+37)[0]
        corners_wet[row*h+col] = sum(1 << index for index,shift in enumerate((12,8,4,0))
                                      if 4*(ground[row*h+col] & 63)+((corners >> shift) & 15) < 4*water)
        wet[row*h+col] = corners_wet[row*h+col] != 0
        offset += 43
    assert offset == len(occupants)
    del occupants
    # Canonical bounds derived independently from each stored row's extrema.
    xmin = min(center-row//2 for row in range(v)); xmax = max(h-1+center-row//2 for row in range(v))
    ymin = min((row+1)//2 for row in range(v)); ymax = max(h-1+(row+1)//2 for row in range(v))
    def boundary(col,row): return col in (0,h-1) or row in (0,v-1)
    def sector(x,y):
        qx = (2*x-xmin-xmax)/(xmax-xmin); qy = (2*y-ymin-ymax)/(ymax-ymin)
        return (1 if qy < 0 else 4) if abs(qy) >= abs(qx) else (8 if qx < 0 else 2)
    visited = bytearray(h*v); land_components = sea_components = sea_cells = sea_sides = 0
    for start in range(h*v):
        if visited[start]: continue
        is_sea = wet[start]; visited[start] = 1; queue = deque([start]); outlets = count = 0
        while queue:
            i = queue.popleft(); count += 1; col, row = i % h, i//h
            x, y = col-row//2+center, col+(row+1)//2
            if boundary(col,row):
                side = sector(x,y); outlets |= side
                if is_sea: assert side & selected
                elif coast['Kind'] == 1: raise AssertionError('Dry island boundary')
            for xx, yy in [(x-1,y),(x+1,y),(x,y-1),(x,y+1)]:
                if is_sea:
                    a,b = (0,1) if xx>x else (2,3) if xx<x else (0,3) if yy<y else (1,2)
                    if not corners_wet[i] & ((1 << a) | (1 << b)): continue
                hh, vv = (xx-center+yy)//2, yy-xx+center
                if 0 <= hh < h and 0 <= vv < v:
                    n = vv*h+hh
                    if not visited[n] and wet[n] == is_sea:
                        visited[n] = 1; queue.append(n)
        if is_sea:
            assert outlets, 'Enclosed sea'; sea_sides |= outlets; sea_cells += count; sea_components += 1
        else: land_components += 1
    percent = 100*sea_cells/(h*v)
    assert land_components == 1 and sea_sides == selected
    assert abs(percent-coast['WaterPercent']) <= 5
    if coast['Kind'] == 1: assert sea_components == 1
    return dict(path=str(path),sha256=hashlib.sha256(raw).hexdigest(),h=h,v=v,water=water,seaCells=sea_cells,
                landCells=h*v-sea_cells,seaComponents=sea_components,landComponents=land_components,seaSides=sea_sides,
                waterPercent=percent,buriedLowCells=sum((g & 63)<water for g in ground),surfaceMaskHash=hashlib.sha256(wet).hexdigest())


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('saves',type=Path,nargs='+'); args = parser.parse_args()
    for path in args.saves: print(json.dumps(check(path)),flush=True)
