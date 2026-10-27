#!/usr/bin/env python3
"""Check source/prepared mounts and a subsequent base-only start using original fixtures."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import time
import urllib.request

SCRIPTS=Path(__file__).resolve().parent
sys.path[:0]=[str(SCRIPTS),str(SCRIPTS/'tests')]
from test_optional_preparation import fixture, optional


def check(image,platform):
    with tempfile.TemporaryDirectory(prefix='freetrain-optional-container-') as temporary:
        root=Path(temporary);one=fixture(root);two=fixture(root,'fixture-two','synthetic-two','synthetic-one')
        (one/'plugins/synthetic-one/readme-ja.txt').write_bytes('原文の検証'.encode('cp932'))
        prepared=optional.prepare([one,two],optional.b.CATALOG,optional.b.INPUT,root/'distribution')
        for mode in ('source','prepared','base-after-optional'):
            command=['docker','run','-d','--platform',platform,'--read-only','--tmpfs','/tmp','--tmpfs','/data:rw,mode=1777',
                '-p','127.0.0.1::8080','-e','FreeTrainOnline__OperatorSecret=synthetic-container-secret',
                '-e','FreeTrainOnline__PluginCacheDirectory=/data/plugin-cache']
            if mode=='source': command+=['-v',str(root)+':/plugins:ro']
            if mode=='prepared':
                for i,path in enumerate(prepared): command+=['-v',path+':/plugins/prepared-'+str(i)+':ro']
            command+=[image]
            if mode=='source':command+=['--plugin-dir','/plugins/fixture-two','--plugin-dir','/plugins/fixture-one']
            if mode=='prepared':command+=['--plugin-dir','/plugins/prepared-1','--plugin-dir','/plugins/prepared-0']
            container=subprocess.check_output(command,text=True).strip()
            try:
                binding=json.loads(subprocess.check_output(['docker','inspect',container],text=True))[0]['NetworkSettings']['Ports']['8080/tcp'][0]
                base='http://127.0.0.1:'+binding['HostPort']
                def get(path):
                    with urllib.request.urlopen(base+path,timeout=5) as response:return response.read()
                for _ in range(50):
                    try:
                        assert json.loads(get('/health'))['status']=='ok';break
                    except OSError:time.sleep(.4)
                else:raise AssertionError(subprocess.check_output(['docker','logs',container],text=True))
                content=json.loads(get('/api/content'))
                assert len(content['packs'])==(0 if mode=='base-after-optional' else 2)
                for pack in content['packs']:
                    catalog_raw=get(pack['catalogUrl']);assert hashlib.sha256(catalog_raw).hexdigest()==pack['catalogHash']
                    catalog=json.loads(catalog_raw)
                    for asset in catalog['assets']:
                        for light in ('day','night'):
                            media=asset[light];raw=get(media['url']);assert len(raw)==media['bytes'];assert hashlib.sha256(raw).hexdigest()==media['sha256']
                    for notice in pack['notices']:
                        with urllib.request.urlopen(base+notice['url'],timeout=5) as response:
                            raw=response.read();assert hashlib.sha256(raw).hexdigest()==notice['sha256']
                            assert response.headers.get_content_type()=='text/plain'
                            if notice['path'].endswith('readme-ja.txt'):assert response.headers.get_content_charset()=='shift_jis'
                # Optional mounts/cache cannot leak into the immutable application tree.
                leaked=subprocess.check_output(['docker','exec',container,'find','/app','-name','plugin-set.xml','-o','-name','prepared-pack.json'],text=True)
                assert not leaked,leaked
                print(json.dumps(dict(mode=mode,packs=len(content['packs']),setHash=content['setHash'],result='passed')),flush=True)
            finally:subprocess.run(['docker','rm','-f',container],check=False,stdout=subprocess.DEVNULL)
        shutil.rmtree(one);shutil.rmtree(two)

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--image',default='freetrain-online:p2-03');parser.add_argument('--platform',default='linux/amd64');args=parser.parse_args()
    check(args.image,args.platform)
