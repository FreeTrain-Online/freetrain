#!/usr/bin/env python3
"""Synthetic inert NRBF fixtures; no CLR serialization/deserialization.

Layout: MS-NRBF, ExAV 883cf7d AdvancedStationHandler/TimeMask and the
CollectionBase/ArrayList metadata independently observed in P0 sample reports.
These are structural fixtures, not files captured from the original executable.
"""
import argparse
import json
from pathlib import Path
import struct

OUT = Path(__file__).resolve().parents[1] / 'web/tests/fixtures/timetable'
PREFIX = 'freetrain.world.rail.tattc.'
ACTIONS = ['pass', 'stop', 'go', 'reverse', 'forwarding', 'reverseforwarding', 'reverse2', 'rforwarding2']

def i(n): return struct.pack('<i', n)
def text(s):
    b = s.encode('utf8')
    n = len(b)
    result = b''
    while n >= 128:
        result += bytes([(n & 127) | 128])
        n >>= 7
    return result + bytes([n]) + b

def ref(n): return b'\x09' + i(n)
def cls(n, name, fields, types, extras, values, system=False):
    return bytes([4 if system else 5]) + i(n) + text(name) + i(len(fields)) + b''.join(map(text, fields)) + bytes(types) + extras + (b'' if system else i(1)) + values

def fixture(binary_array=False):
    header = b'\0' + i(1) + i(-1) + i(1) + i(0)
    library = b'\x0c' + i(1) + text('FreeTrain.TATTrainController, Version=2.0.3837.12345, Culture=neutral, PublicKeyToken=null')
    root = cls(1, PREFIX+'AdvancedStationHandler+RuleCollection', ['CollectionBase+list'], [3], text('System.Collections.ArrayList'), ref(2))
    array_list = cls(2, 'System.Collections.ArrayList', ['_items', '_size', '_version'], [5,0,0], bytes([8,8]), ref(3)+i(9)+i(9), True)
    array = (b'\x07'+i(3)+b'\0'+i(1)+i(16)+b'\x02' if binary_array else b'\x10'+i(3)+i(16)) + b''.join(ref(4+n) for n in range(8))+ref(4)+b'\x0d\x07'
    rules = b''
    expected = []
    for n in range(8):
        masks = [4,23,5,8,0] if n < 2 else [12,31,-1,23,50] if n==7 else [-1]*5
        enum = (cls(-100, PREFIX+'StationAction', ['value__'], [0], b'\x08', i(n)) if n==0 else b'\x01'+i(-100-n)+i(-100)+i(n))
        values = enum + struct.pack('5b', *masks)
        rules += (cls(4, PREFIX+'AdvStationRule', ['action','month','day','dayOfWeek','hour','minutes'], [4,0,0,0,0,0], text(PREFIX+'StationAction')+i(1)+bytes([10]*5), values) if n==0 else b'\x01'+i(4+n)+i(4)+values)
        expected.append(dict(zip(['month','day','dayOfWeek','hour','minutes','action'], masks+[ACTIONS[n]])))
    expected.append(dict(expected[0]))
    return header+library+root+array_list+array+rules+b'\x0b', expected

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    if not args.check:
        OUT.mkdir(parents=True, exist_ok=True)
    def output(path, data):
        if args.check:
            assert path.read_bytes() == data, f'Stale fixture: {path}'
        else:
            path.write_bytes(data)
    for kind in ['single', 'binary-array']:
        raw, rules = fixture(kind=='binary-array')
        output(OUT/(kind+'.ftdd'), raw)
    output(OUT/'expected.json', (json.dumps(rules, indent=2)+'\n').encode())
    print('Timetable fixtures: two NRBF layouts, eight actions and one repeated rule reference.')
