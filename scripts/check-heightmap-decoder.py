#!/usr/bin/env python3
"""Offline pin/distribution checks for the bounded source decoder. No downloads."""
import argparse,hashlib,json
from pathlib import Path

parser=argparse.ArgumentParser()
parser.add_argument('--publish-dir',type=Path)
args=parser.parse_args()
root=Path(__file__).resolve().parent.parent
source=root/'src/FreeTrain.Online.Server/Imaging/StbImageSharp'
manifest=json.loads((source/'SOURCE.json').read_text())
assert manifest['version']=='2.30.16'
assert manifest['commit']=='125af70cb557033f2c46aec8e82eaaf72ac49817'
assert manifest['licenseSelected']=='MIT'
assert set(manifest['files'])=={p.name for p in source.glob('*.cs')}
for name,record in manifest['files'].items():
    assert hashlib.sha256((source/name).read_bytes()).hexdigest()==record['localSha256'],name
    assert len(record['upstreamSha256'])==64
for path in [*root.glob('src/**/packages.lock.json'),*root.glob('tests/**/packages.lock.json')]:
    assert 'skiasharp' not in path.read_text().lower(),path
assert not (source/'Hebron.Runtime').exists()
assert 'StbImageSharp/LICENSE.txt' in (root/'src/FreeTrain.Online.Server/FreeTrain.Online.Server.csproj').read_text().replace('StbImageSharp/*.txt','StbImageSharp/LICENSE.txt')
if args.publish_dir:
    published=args.publish_dir.resolve()
    assert (published/'FreeTrain.Online.Server.dll').is_file()
    assert not [p for p in published.rglob('*') if 'skia' in p.name.lower()]
    for deps in published.glob('*.deps.json'):
        assert 'skiasharp' not in deps.read_text().lower(),deps
    notice=Path('Notices/StbImageSharp/LICENSE.txt')
    assert (published/notice).read_bytes()==(root/'doc/notices/StbImageSharp/LICENSE.txt').read_bytes()
print('Heightmap decoder: exact reviewed source/MIT pin, no Skia locks; publish scan passed.' if args.publish_dir else 'Heightmap decoder: exact reviewed source/MIT pin, no Skia locks.')
