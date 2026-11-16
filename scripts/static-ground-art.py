#!/usr/bin/env python3
"""Explicit portable intersection for the four ASSET-CROP-15..18 farmland crops."""
import argparse, hashlib, json, struct, zlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def main():
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args()
    catalog=json.loads((ROOT/'web/public/content/bundled-v2/catalog.json').read_text());frames=[];masks={}
    for recipe in catalog['renderRecipes']:
        if recipe['handler']!='land-0d':continue
        for frame in recipe['frames']:
            if frame['status']!='invalid-source-crop':continue
            x,y,w,h=frame['rectangle'];assert y==64 and w==h==32
            correction=dict(recipe=recipe['id'],key=frame['key'],originalRectangle=frame['rectangle'],originalAnchor=frame['anchor'],surfaces={},status='ready',rectangle=[x,y,w,16],anchor=frame['anchor'],masks={})
            for variant,binding in catalog['surfaceSets'][frame['surfaceSet']].items():
                assert (binding['width'],binding['height'])==(128,80)
                raw=(ROOT/'web/public'/binding['image']['url'].lstrip('/')).read_bytes();assert hashlib.sha256(raw).hexdigest()==binding['image']['sha256'] and raw[24:26]==bytes([8,6])
                correction['surfaces'][variant]=binding['image']['sha256'];chunks=[];pos=8
                while pos<len(raw):
                    n=struct.unpack('>I',raw[pos:pos+4])[0]
                    if raw[pos+4:pos+8]==b'IDAT':chunks.append(raw[pos+8:pos+8+n])
                    pos+=12+n
                pixels=zlib.decompress(b''.join(chunks));stride=128*4+1
                assert len(pixels)==80*stride and all(pixels[row*stride]==0 for row in range(80))
                rows=[''.join('1' if pixels[row*stride+1+col*4+3] else '0' for col in range(x,x+w)) for row in range(y,80)]
                key=hashlib.sha256('\n'.join(rows).encode()).hexdigest();masks[key]=rows;correction['masks'][variant]=key
            frames.append(correction)
    assert len(frames)==4
    output=ROOT/'web/src/generated/static-ground-art.json';text=json.dumps(dict(sourceRevision=catalog['source']['revision'],frames=frames,masks=masks),indent=2)+'\n'
    if args.check:assert output.read_text()==text,'Stale static ground art'
    else:output.write_text(text)
    print('Verified four explicit farmland crop intersections')
if __name__=='__main__':main()
