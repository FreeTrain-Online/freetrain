#!/usr/bin/env python3
"""P7-10 bounded portable accommodation for ASSET-CROP-01..14.

Retain visible pixels inside the pinned 353x352 sheet, translating the anchor by
exactly the omitted left margin. Empty intersections/nonpositive height have no
source pixels and become invisible; full voxel ownership is unchanged.
"""
import argparse
import hashlib
import json
from pathlib import Path
import struct
import zlib
ROOT=Path(__file__).resolve().parents[1]
IDS=['{D57200E0-8E73-4899-8EEC-0E9AC95C3D40}','{488D9D06-46B1-45EA-8DCE-61B1041254A2}']
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');args=parser.parse_args()
    catalog=json.loads((ROOT/'web/public/content/bundled-v2/catalog.json').read_text())
    frames=[];masks={}
    for recipe in catalog['renderRecipes']:
        if recipe['id'] not in IDS:continue
        for frame in recipe['frames']:
            if frame['status']!='invalid-source-crop':continue
            x,y,w,h=frame['rectangle'];left=max(0,x);right=min(353,x+w)
            correction=dict(recipe=recipe['id'],key=frame['key'],originalRectangle=frame['rectangle'],originalAnchor=frame['anchor'],surfaces={})
            for variant,binding in catalog['surfaceSets'][frame['surfaceSet']].items():
                assert (binding['width'],binding['height'])==(353,352)
                correction['surfaces'][variant]=binding['image']['sha256']
            if h<=0 or right<=left:correction['status']='invisible'
            else:
                assert y>=0 and y+h<=352
                correction.update(status='ready',rectangle=[left,y,right-left,h],anchor=[frame['anchor'][0]-(left-x),frame['anchor'][1]],masks={})
                for variant,binding in catalog['surfaceSets'][frame['surfaceSet']].items():
                    raw=(ROOT/'web/public'/binding['image']['url'].lstrip('/')).read_bytes()
                    assert hashlib.sha256(raw).hexdigest()==binding['image']['sha256']
                    assert raw[24:26]==bytes([8,6])
                    chunks=[];pos=8
                    while pos<len(raw):
                        n=struct.unpack('>I',raw[pos:pos+4])[0]
                        if raw[pos+4:pos+8]==b'IDAT':chunks.append(raw[pos+8:pos+8+n])
                        pos+=12+n
                    pixels=zlib.decompress(b''.join(chunks));stride=353*4+1
                    assert len(pixels)==352*stride and all(pixels[row*stride]==0 for row in range(352))
                    rows=[''.join('1' if pixels[row*stride+1+col*4+3] else '0' for col in range(left,right)) for row in range(y,y+h)]
                    key=hashlib.sha256('\n'.join(rows).encode()).hexdigest();masks[key]=rows;correction['masks'][variant]=key
            frames.append(correction)
    assert len(frames)==14
    result=dict(sourceRevision=catalog['source']['revision'],frames=frames,masks=masks)
    output=ROOT/'web/src/generated/variable-city-art.json';text=json.dumps(result,indent=2)+'\n'
    if args.check:assert output.read_text()==text,'Stale variable city crop accommodation'
    else:output.write_text(text)
    print('Verified 14 explicit variable-city crop accommodations')
if __name__=='__main__':main()
