#!/usr/bin/env python3
"""Verify a built image's prepared bundled resources on an ephemeral loopback port.

No user save directory, published service or external content repository is used.
The temporary container and its tmpfs saves are removed even on failure.
"""
import argparse
import hashlib
import http.cookiejar
import json
import secrets
import subprocess
import time
import urllib.error
import urllib.request


def run(image, platform):
    secret = secrets.token_hex(24)
    container = None
    try:
        container = subprocess.check_output(['docker', 'run', '-d', '--platform', platform,
            '--read-only', '--tmpfs', '/tmp', '--tmpfs', '/data:rw,mode=1777', '-p', '127.0.0.1::8080',
            '-e', 'FreeTrainOnline__OperatorSecret=' + secret, image], text=True).strip()
        binding = json.loads(subprocess.check_output(['docker', 'inspect', container], text=True))[0]['NetworkSettings']['Ports']['8080/tcp'][0]
        base = 'http://127.0.0.1:' + binding['HostPort']
        opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
        def get(path):
            if not path.startswith('/') or path.startswith('//') or '..' in path:
                raise ValueError('Nonlocal artifact path')
            with opener.open(base + path, timeout=5) as response:
                return response.read()
        for _ in range(40):
            try:
                health = json.loads(get('/health'))
                break
            except (OSError, urllib.error.URLError):
                time.sleep(.5)
        else:
            raise AssertionError(subprocess.check_output(['docker', 'logs', container], text=True))
        assert health['status'] == 'ok', health
        raw = get('/content/bundled-v2/catalog.json')
        catalog = json.loads(raw)
        digest = get('/content/bundled-v2/catalog.sha256').decode().strip()
        assert hashlib.sha256(raw).hexdigest() == digest
        assert catalog['source']['revision'] == '883cf7d81cf49db25ff38cd3a25d0f140afe31a0'
        assert catalog['counts']['contributions'] == 448
        artifacts = set()
        for asset in catalog['assets']:
            for mode in ('day', 'night', 'native'):
                if mode in asset:
                    data = get(asset[mode]['url'])
                    assert hashlib.sha256(data).hexdigest() == asset[mode]['sha256'], asset['path']
                    assert len(data) == asset[mode]['bytes'], asset['path']
                    artifacts.add(asset[mode]['url'])
        for surface_set in catalog['surfaceSets'].values():
            for binding in surface_set.values():
                media = binding['image']
                if media['url'] in artifacts:
                    continue
                data = get(media['url'])
                assert hashlib.sha256(data).hexdigest() == media['sha256']
                assert len(data) == media['bytes']
                artifacts.add(media['url'])
        for metadata in [catalog['provenance']] + catalog['pluginMetadata']:
            assert hashlib.sha256(get(metadata['url'])).hexdigest() == metadata['sha256']
        assert b'Recipe composition' in get('/content-gallery.html')
        assert sum(a['status'] == 'retained-midi' for a in catalog['assets']) == 3
        for notice in catalog['notices']:
            assert hashlib.sha256(get(notice['url'])).hexdigest() == notice['sha256']
            managed = '/app/ContentNotices/bundled-v2/' + notice['path'].removeprefix('trunk/')
            data = subprocess.check_output(['docker', 'exec', container, 'cat', managed])
            assert hashlib.sha256(data).hexdigest() == notice['sha256']
        request = urllib.request.Request(base + '/api/access', data=json.dumps(dict(secret=secret)).encode(),
                                         headers={'Content-Type': 'application/json'})
        with opener.open(request, timeout=5) as response:
            assert response.status == 204
        bootstrap = json.loads(get('/api/bootstrap?protocolVersion=6&contentVersion=reference-spatial-content-2'))
        assert bootstrap['snapshot']['ruleset'] == 'reference-spatial-4'
        assert bootstrap['snapshot']['requiredPacks'][0]['id'] == 'reference-spatial'
        print(json.dumps(dict(health=health['status'], catalogHash=digest, assets=len(catalog['assets']),
                              uniqueMedia=len(artifacts), notices=len(catalog['notices']),
                              ruleset=bootstrap['snapshot']['ruleset'], result='container check passed')))
    finally:
        if container:
            subprocess.run(['docker', 'rm', '-f', container], check=False, stdout=subprocess.DEVNULL)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--image', default='freetrain-online:p2-02')
    parser.add_argument('--platform', default='linux/amd64')
    args = parser.parse_args()
    run(args.image, args.platform)


if __name__ == '__main__':
    main()
