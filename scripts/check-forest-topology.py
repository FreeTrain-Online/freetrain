#!/usr/bin/env python3
"""Independent actual Save10 forest legality and connected level reservation check."""
import argparse
from array import array
import base64
from collections import Counter, deque
import hashlib
import json
from pathlib import Path
import struct
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]

def check(path):
    raw=path.read_bytes();saved=json.loads(raw);assert saved['saveVersion']==10
    packed=saved['packedTerrain'];r=json.loads(packed['provenance']['requestJson']);o=r['Options']
    h,v,z=[r['Dimensions'][k] for k in ('H','V','Z')];water=r['WaterLevel'];center=(v-1)//2
    assert saved['map']['width']==h and saved['map']['height']==v and saved['map']['levels']==z
    cells=base64.b64decode(packed['cells'],validate=True);assert len(cells)==h*v and all(c<64 for c in cells)
    corners=array('H',[0])*(h*v);forest=bytearray(h*v);definitions=packed['definitions']
    palette={b['ContributionId'] for b in o['Forests']['Palette']}
    manifest=ET.parse(ROOT/'content/bundled-v2/expanded/trunk/plugins/org.kohsuke.freetrain.land.forest/plugin.xml')
    species={c.attrib['id']:(int(c.findtext('density')),int(c.findtext('count'))) for c in manifest.findall('contribution') if c.find('density') is not None}
    occupants=base64.b64decode(packed['occupants'],validate=True);offset=0;counts=Counter();tree_count=0
    legal_positions={(x+y,((-x+y)>>1)+8) for x in range(16) for y in range(16)}
    for _ in range(packed['occupantCount']):
        x,y=struct.unpack_from('<ii',occupants,offset+18);col,row=(x-center+y)//2,y-x+center
        assert 0<=col<h and 0<=row<v;i=row*h+col
        assert occupants[offset+36]==0 and occupants[offset+26]==cells[i]
        corners[i]=struct.unpack_from('<H',occupants,offset+37)[0]
        binding=struct.unpack_from('<H',occupants,offset+40)[0];trees=occupants[offset+42]
        if binding!=65535:
            id=definitions[binding]['contributionId'];assert id in palette and id in species
            assert definitions[binding]['packId']=='exav-stable-bundled'
            density,sprites=species[id];assert 1<=trees<=2*density and cells[i]>=water
            assert struct.unpack_from('<q',occupants,offset+28)[0]==100
            assert not forest[i];forest[i]=1;counts[id]+=1;tree_count+=trees;last=-1
            for j in range(trees):
                a,b,s=occupants[offset+43+3*j:offset+46+3*j];assert (a,b) in legal_positions and b>=last and s<sprites;last=b
        else:assert trees==0
        offset+=43+3*trees
    assert offset==len(occupants);del occupants
    # Derive adjacency by stepping in canonical XY and converting back to stored H/V.
    def neighbors(i):
        col,row=i%h,i//h;x,y=col-row//2+center,col+(row+1)//2
        found=[]
        for xx,yy in [(x-1,y),(x+1,y),(x,y-1),(x,y+1)]:
            vv=yy-xx+center;hh=(xx-center+yy)//2
            if 0<=hh<h and 0<=vv<v:found.append(vv*h+hh)
        return sorted(found)
    def hashed(seed,i):
        n=(seed ^ i*0x9e3779b9)&0xffffffff;n=((n^(n>>16))*0x7feb352d)&0xffffffff;n=((n^(n>>15))*0x846ca68b)&0xffffffff
        return n^(n>>16)
    seed=int.from_bytes(hashlib.sha256(f"freetrain.world-generation.v1\nterrain-generation-1\n{o['Seed']}\nstarting-area\n0".encode()).digest()[:4],'little')&0x7fffffff
    flat=bytearray(g>=water and c==0 for g,c in zip(cells,corners));seen=bytearray(h*v);largest=0;best=(2**32,-1)
    for start in range(h*v):
        if not flat[start] or seen[start]:continue
        seen[start]=1;queue=deque([start]);size=0;root=(hashed(seed,start),start)
        while queue:
            i=queue.popleft();size+=1;root=min(root,(hashed(seed,i),i))
            for n in neighbors(i):
                if flat[n] and not seen[n] and cells[n]==cells[start]:seen[n]=1;queue.append(n)
        if size>largest or size==largest and root<best:largest=size;best=root
    area=o['StartingArea'];assert largest>=area
    reserved=set()
    if area:
        root=best[1];reserved.add(root);queue=deque([root])
        while len(reserved)<area:
            i=queue.popleft()
            for n in neighbors(i):
                if len(reserved)==area:break
                if flat[n] and n not in reserved and cells[n]==cells[root]:reserved.add(n);queue.append(n)
    assert all(not forest[i] for i in reserved)
    eligible=sum(g>=water for g in cells)-area;target=eligible*o['Forests']['CoveragePercent']//100
    assert sum(counts.values())<=target
    result=dict(saveSha256=hashlib.sha256(raw).hexdigest(),map=[h,v,z],eligibleCells=eligible,maskCells=target,forestCells=sum(counts.values()),trees=tree_count,reservedCells=len(reserved),largestFlatArea=largest,species=dict(counts))
    print(json.dumps(result));return result

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('save',type=Path);args=parser.parse_args();check(args.save)
