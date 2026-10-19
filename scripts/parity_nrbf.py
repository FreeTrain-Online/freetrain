"""Bounded, inert NRBF record inspection for P0 historical-save review.

No CLR types, constructors, binders, callbacks, or delegates are activated.
This is an offline review tool, not a service importer or a complete NRBF
validator. See doc/web-parity-persistence.md for supported records and limits.
Layout reference: Microsoft [MS-NRBF] sections 2.1, 2.3, 2.4 and 2.5.
"""
from collections import Counter
from dataclasses import dataclass
import hashlib
import math
import struct


class RecordError(ValueError):
    pass


@dataclass(frozen=True)
class Limits:
    file_bytes: int = 64 * 1024 * 1024
    objects: int = 500_000
    records: int = 2_000_000
    string_bytes: int = 1024 * 1024
    total_string_bytes: int = 16 * 1024 * 1024
    members: int = 256
    array_elements: int = 20_000_000
    total_array_elements: int = 40_000_000
    depth: int = 100


class Reader:
    # Fixed-width primitive encodings. DateTime is retained as its raw bits.
    FORMATS = {1: 'B', 2: 'B', 6: 'd', 7: 'h', 8: 'i', 9: 'q', 10: 'b',
               11: 'f', 12: 'q', 13: 'Q', 14: 'H', 15: 'I', 16: 'Q'}

    def __init__(self, data, limits=None):
        self.data, self.pos, self.limits = data, 0, limits or Limits()
        if len(data) > self.limits.file_bytes:
            self.fail('fileLimit')
        self.record_counts = Counter()
        self.string_bytes = self.array_elements = self.object_count = 0

    def fail(self, code):
        raise RecordError(f'{code} at byte {self.pos}')

    def take(self, count):
        if count < 0 or count > len(self.data) - self.pos:
            self.fail('truncated')
        start = self.pos
        self.pos += count
        return self.data[start:self.pos]

    def number(self, fmt):
        return struct.unpack('<' + fmt, self.take(struct.calcsize('<' + fmt)))[0]

    def byte(self):
        return self.number('B')

    def integer(self):
        return self.number('i')

    def length(self, maximum):
        value = self.integer()
        if not 0 <= value <= maximum:
            self.fail('lengthLimit')
        return value

    def string(self):
        size = 0
        for shift in range(0, 35, 7):
            value = self.byte()
            if shift == 28 and value > 7:
                self.fail('invalidStringLength')
            size |= (value & 127) << shift
            if not value & 128:
                break
        else:
            self.fail('invalidStringLength')
        self.string_bytes += size
        if size > self.limits.string_bytes or self.string_bytes > self.limits.total_string_bytes:
            self.fail('stringLimit')
        try:
            return self.take(size).decode('utf-8', errors='strict')
        except UnicodeDecodeError:
            self.fail('invalidUtf8')

    def primitive(self, kind):
        if kind in self.FORMATS:
            value = self.number(self.FORMATS[kind])
            if kind == 1 and value not in (0, 1):
                self.fail('invalidBoolean')
            # Floats with nonfinite values remain diagnostic text in JSON reports.
            return value if not isinstance(value, float) or math.isfinite(value) else str(value)
        if kind == 3:
            first = self.take(1)
            size = 1 if first[0] < 128 else 2 if 0xc2 <= first[0] <= 0xdf else 3 if 0xe0 <= first[0] <= 0xef else 0
            if not size:
                self.fail('unsupportedChar')
            try:
                return (first + self.take(size - 1)).decode('utf-8', errors='strict')
            except UnicodeDecodeError:
                self.fail('invalidChar')
        if kind == 5:
            return self.string()  # Decimal's wire representation; no numeric coercion.
        self.fail('unsupportedPrimitive:' + str(kind))

    def additional(self, kind):
        if kind in (0, 7):
            primitive = self.byte()
            if primitive not in (*self.FORMATS, 3, 5):
                self.fail('unsupportedPrimitive:' + str(primitive))
            return primitive
        if kind == 3:
            return self.string()
        if kind == 4:
            value = dict(name=self.string(), libraryId=self.integer())
            self.library_refs.add(value['libraryId'])
            return value
        if kind not in (1, 2, 5, 6):
            self.fail('unsupportedBinaryType:' + str(kind))
        return None

    def add(self, object_id, node):
        if object_id == 0 or object_id in self.nodes:
            self.fail('duplicateOrZeroObjectId')
        self.object_count += 1
        if self.object_count > self.limits.objects:
            self.fail('objectLimit')
        self.nodes[object_id] = node
        return dict(ref=object_id)

    def array(self, object_id, lengths, lower, kind, extra, depth):
        count = math.prod(lengths)
        self.array_elements += count
        if count > self.limits.array_elements or self.array_elements > self.limits.total_array_elements:
            self.fail('arrayLimit')
        node = dict(kind='array', lengths=lengths, lowerBounds=lower,
                    elementType=kind, additional=extra, values={})
        ref = self.add(object_id, node)
        if kind == 0 and extra in self.FORMATS:
            fmt = self.FORMATS[extra]
            raw = self.take(count * struct.calcsize('<' + fmt))
            if extra == 1 and any(v not in (0, 1) for v in raw):
                self.fail('invalidBoolean')
            node['primitiveData'] = dict(bytes=len(raw), sha256=hashlib.sha256(raw).hexdigest())
            # Large primitive blocks stay compact; this reader is not a converter.
            if count <= 256:
                node['primitiveValues'] = [v[0] for v in struct.iter_unpack('<' + fmt, raw)]
            return ref
        index = 0
        while index < count:
            value = self.primitive(extra) if kind == 0 else self.value(depth + 1, array=True)
            if isinstance(value, tuple):
                run = value[0]
                if run <= 0 or index + run > count:
                    self.fail('invalidNullRun')
                index += run
            else:
                if value is not None:
                    node['values'][index] = value
                index += 1
        return ref

    def value(self, depth=0, array=False, top=False):
        if depth > self.limits.depth:
            self.fail('depthLimit')
        while True:
            offset = self.pos
            kind = self.byte()
            self.record_counts[kind] += 1
            if sum(self.record_counts.values()) > self.limits.records:
                self.fail('recordLimit')
            if kind == 12:
                library_id, name = self.integer(), self.string()
                if library_id <= 0 or library_id in self.libraries:
                    self.fail('duplicateOrInvalidLibrary')
                self.libraries[library_id] = name
                continue
            break
        if kind == 11:
            if not top:
                self.fail('unexpectedMessageEnd')
            return ('end',)
        if kind == 10:
            return None
        if kind in (13, 14):
            if not array:
                self.fail('nullRunOutsideArray')
            return (self.byte() if kind == 13 else self.integer(),)
        if kind == 9:
            object_id = self.integer()
            self.references.add(object_id)
            return dict(ref=object_id)
        if kind == 8:
            return self.primitive(self.byte())
        if kind == 6:
            object_id = self.integer()
            return self.add(object_id, dict(kind='string', value=self.string(), offset=offset))
        if kind in (1, 4, 5):
            object_id = self.integer()
            if kind == 1:
                metadata_id = self.integer()
                if metadata_id not in self.metadata:
                    self.fail('missingClassMetadata')
                meta = self.metadata[metadata_id]
            else:
                name = self.string()
                count = self.length(self.limits.members)
                names = [self.string() for _ in range(count)]
                types = [self.byte() for _ in range(count)]
                extras = [self.additional(t) for t in types]
                library_id = self.integer() if kind == 5 else None
                if library_id is not None:
                    self.library_refs.add(library_id)
                meta = dict(name=name, names=names, types=types, extras=extras, libraryId=library_id)
                if object_id in self.metadata:
                    self.fail('duplicateClassMetadata')
                self.metadata[object_id] = meta
            # Hidden inherited fields can have the same serialized name (e.g.
            # Station.type and Structure.type). Keep ordinal slots, never a dict
            # that silently overwrites one of those relationships.
            node = dict(kind='class', type=meta['name'], libraryId=meta['libraryId'], members=[], offset=offset)
            ref = self.add(object_id, node)
            for name, t, extra in zip(meta['names'], meta['types'], meta['extras']):
                node['members'].append(dict(name=name, value=self.primitive(extra) if t == 0 else self.value(depth + 1)))
            return ref
        if kind in (7, 15, 16, 17):
            object_id = self.integer()
            if kind == 7:
                array_type = self.byte()
                rank = self.length(8)
                if rank == 0 or array_type > 5 or (array_type in (0, 1, 3, 4) and rank != 1):
                    self.fail('invalidArrayShape')
                lengths = [self.length(self.limits.array_elements) for _ in range(rank)]
                lower = [self.integer() for _ in range(rank)] if array_type >= 3 else [0] * rank
                t = self.byte()
                extra = self.additional(t)
            else:
                lengths = [self.length(self.limits.array_elements)]
                lower = [0]
                t = {15: 0, 16: 2, 17: 1}[kind]
                extra = self.additional(t)
            return self.array(object_id, lengths, lower, t, extra, depth)
        self.fail('unsupportedRecord:' + str(kind))

    def streams(self):
        if self.take(2) != b'UC':
            self.fail('unsupportedWrapper')
        streams = []
        while self.pos < len(self.data):
            if len(streams) == 2:
                self.fail('trailingBytes')
            start = self.pos
            if self.byte() != 0:
                self.fail('missingStreamHeader')
            root, header, major, minor = [self.integer() for _ in range(4)]
            if root <= 0 or header != -1 or (major, minor) != (1, 0):
                self.fail('unsupportedStreamHeader')
            self.nodes, self.metadata, self.libraries = {}, {}, {}
            self.references, self.library_refs = {root}, set()
            while self.value(top=True) != ('end',):
                pass
            if self.references - self.nodes.keys():
                self.fail('danglingObjectReference')
            if self.library_refs - self.libraries.keys():
                self.fail('danglingLibraryReference')
            streams.append(dict(offset=start, endOffset=self.pos, rootId=root,
                                nodes=self.nodes, metadata=self.metadata, libraries=self.libraries,
                                referencedObjectIds=len(self.references)))
        if len(streams) != 2:
            self.fail('expectedTwoRoots')
        return streams


def summary(stream):
    nodes = stream['nodes']
    counts = Counter(n.get('type', n['kind']) for n in nodes.values())
    return dict(offset=stream['offset'], endOffset=stream['endOffset'], rootId=stream['rootId'],
                objects=len(nodes), referencedObjectIds=stream['referencedObjectIds'],
                libraries=stream['libraries'], typeCounts=dict(sorted(counts.items())),
                classLayouts=[dict(objectId=i, **m) for i, m in sorted(stream['metadata'].items())])
