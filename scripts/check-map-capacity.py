#!/usr/bin/env python3
"""Loopback-only packaged-server capacity smoke; no historical save execution."""
import argparse
import base64
import hashlib
from http.client import HTTPException
import http.cookiejar
import json
import os
from pathlib import Path
import queue
import socket
import struct
import threading
import time
import urllib.parse
import urllib.request

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--base', default='http://127.0.0.1:5698')
parser.add_argument('--secret', default='capacity-smoke')
parser.add_argument('--save', type=Path, required=True)
parser.add_argument('--restart', action='store_true')
args = parser.parse_args()
url = urllib.parse.urlsplit(args.base)
if url.scheme != 'http' or url.hostname not in ('127.0.0.1', 'localhost'):
    parser.error('This smoke only operates a loopback HTTP test server.')
if args.save.stat().st_size > 256 * 1024 * 1024:
    parser.error('Input exceeds the canonical state bound.')
expected = args.save.read_bytes()
expected_hash = hashlib.sha256(expected).hexdigest()
saved = json.loads(expected)
assert saved['saveVersion'] == 7
jar = http.cookiejar.CookieJar()
http = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))
compat = 'protocolVersion=5&contentVersion=reference-spatial-content-2&contentVersion=terrain-content-1'


def call(path, data=None):
    if data is not None and not isinstance(data, bytes):
        data = json.dumps(data).encode()
    request = urllib.request.Request(args.base + path, data=data, headers={'Content-Type': 'application/json'})
    return http.open(request, timeout=60)


deadline = time.monotonic() + 60
while True:
    try:
        assert json.load(call('/health'))['status'] == 'ok'
        break
    except (OSError, HTTPException):
        if time.monotonic() >= deadline:
            raise
        time.sleep(.25)
call('/api/access', {'secret': args.secret}).read()
response = call('/api/bootstrap?' + compat + '&metadataOnly=true')
content_set = response.headers['X-FreeTrain-Content-Set']
identity = json.load(response)
assert identity['type'] == 'sessionOffer'
if args.restart:
    actual = call('/api/saves/download').read()
    assert hashlib.sha256(actual).hexdigest() == expected_hash
    print(json.dumps({'result': 'checkpoint restart exact', 'saveBytes': len(actual), 'sha256': expected_hash,
                      'map': saved['map'], 'epoch': identity['worldEpoch']}))
    raise SystemExit()

sock = socket.create_connection((url.hostname, url.port or 80), timeout=60)
key = base64.b64encode(os.urandom(16)).decode()
cookie = '; '.join(f'{c.name}={c.value}' for c in jar)
request = (f'GET /api/session?{compat}&packSetHash={content_set} HTTP/1.1\r\nHost: {url.netloc}\r\n'
           'Connection: Upgrade\r\nUpgrade: websocket\r\nSec-WebSocket-Version: 13\r\n'
           f'Sec-WebSocket-Key: {key}\r\nCookie: {cookie}\r\n\r\n')
sock.sendall(request.encode())
header = b''
while not header.endswith(b'\r\n\r\n'):
    header += sock.recv(1)
assert b'101 Switching Protocols' in header
assert base64.b64encode(hashlib.sha1((key + '258EAFA5-E914-47DA-95CA-C5AB0DC85B11').encode()).digest()) in header
output = queue.Queue(maxsize=32)
stop = threading.Event()
write_lock = threading.Lock()


def send(opcode, payload):
    assert len(payload) < 126
    mask = os.urandom(4)
    with write_lock:
        sock.sendall(bytes([0x80 | opcode, 0x80 | len(payload)]) + mask + bytes(b ^ mask[i % 4] for i, b in enumerate(payload)))


def reader():
    def exact(n):
        parts = bytearray()
        while len(parts) < n:
            part = sock.recv(n - len(parts))
            if not part:
                raise EOFError('Socket closed')
            parts.extend(part)
        return bytes(parts)
    payload = bytearray()
    try:
        while not stop.is_set():
            a, b = exact(2)
            opcode, length = a & 15, b & 127
            if length == 126:
                length = struct.unpack('!H', exact(2))[0]
            elif length == 127:
                length = struct.unpack('!Q', exact(8))[0]
            assert not b & 128 and length <= 128 * 1024
            part = exact(length)
            if opcode == 9:
                send(10, part)
                continue
            if opcode == 10:
                continue
            if opcode == 8:
                raise EOFError('Server closed socket')
            assert opcode in (0, 1)
            payload.extend(part)
            if a & 128:
                output.put(json.loads(payload), timeout=30)
                payload.clear()
    except Exception as error:
        if not stop.is_set():
            output.put(error, timeout=30)


def heartbeat():
    while not stop.wait(4):
        try:
            send(1, b'{"type":"heartbeat"}')
        except OSError:
            return


threading.Thread(target=reader, daemon=True).start()
threading.Thread(target=heartbeat, daemon=True).start()


def state():
    message = output.get(timeout=60)
    if isinstance(message, Exception):
        raise message
    if message['type'] != 'stateChunk':
        return message, None, 0
    first = message
    assert 0 < first['totalBytes'] <= 256 * 1024 * 1024
    assert first['count'] == (first['totalBytes'] + 49151) // 49152
    data = bytearray(first['totalBytes'])
    for index in range(first['count']):
        current = first if index == 0 else output.get(timeout=60)
        if isinstance(current, Exception):
            raise current
        assert current['type'] == 'stateChunk' and current['index'] == index
        for field in ('transferId', 'worldEpoch', 'revision', 'count', 'totalBytes', 'sha256'):
            assert current[field] == first[field]
        part = base64.b64decode(current['data'], validate=True)
        assert len(part) == min(49152, len(data) - index * 49152)
        data[index * 49152:index * 49152 + len(part)] = part
    assert hashlib.sha256(data).hexdigest() == first['sha256']
    payload = json.loads(data)
    message = payload['message']
    snapshot = message['snapshot'] if message['type'] == 'join' else message
    assert snapshot['worldEpoch'] == first['worldEpoch'] and snapshot['revision'] == first['revision']
    return message, payload['packedTerrain'], first['count']


try:
    joined, _, _ = state()
    assert joined['type'] == 'join'
    call('/api/saves/upload?expectedWorldEpoch=' + identity['worldEpoch'], expected).read()
    snapshot, terrain, chunks = state()
    assert snapshot['type'] == 'snapshot' and chunks > 1
    assert snapshot['map'] == saved['map']
    assert terrain == saved['packedTerrain']
    assert snapshot['tick'] == saved['clock']['absoluteMinute']
    assert hashlib.sha256(call('/api/saves/download').read()).hexdigest() == expected_hash
    call('/api/saves/upload?expectedWorldEpoch=' + snapshot['worldEpoch'], expected).read()
    reopened, reopened_terrain, _ = state()
    assert reopened['type'] == 'snapshot' and reopened_terrain == terrain
    assert hashlib.sha256(call('/api/saves/download').read()).hexdigest() == expected_hash
    print(json.dumps({'result': 'Linux chunked publication/save/reopen exact', 'saveBytes': len(expected), 'chunks': chunks,
                      'sha256': expected_hash, 'map': saved['map'], 'occupants': terrain['occupantCount']}))
finally:
    stop.set()
    sock.close()
