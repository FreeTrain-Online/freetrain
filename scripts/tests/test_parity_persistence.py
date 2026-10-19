"""P0-05 inventory, design consistency and inert reader boundary checks."""
import importlib.util
import json
from pathlib import Path
import struct
import sys
import unittest


SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))
import parity_nrbf as nrbf
import parity_serialization as serialization
SPEC = importlib.util.spec_from_file_location('persistence_review', SCRIPTS / 'review-parity-persistence.py')
review = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(review)


def i(value):
    return struct.pack('<i', value)


def string(value):
    raw = value.encode('utf-8')
    if len(raw) >= 128:
        raise ValueError('Test helper only supports short strings')
    return bytes([len(raw)]) + raw


def stream(body, root=1):
    return b'\0' + i(root) + i(-1) + i(1) + i(0) + body + b'\x0b'


def two(body, second=None):
    return b'UC' + stream(body) + stream(second if second is not None else b'\x06' + i(1) + string('駅'))


def system_class(object_id, name, names, types, extras=b'', values=b''):
    return b'\x04' + i(object_id) + string(name) + i(len(names)) + b''.join(map(string, names)) + bytes(types) + extras + values


class InertRecordReader(unittest.TestCase):
    def test_two_streams_reset_object_ids_and_preserve_utf8(self):
        data = two(b'\x06' + i(1) + string('データ０１'))
        streams = nrbf.Reader(data).streams()
        self.assertEqual(2, len(streams))
        self.assertEqual('データ０１', streams[0]['nodes'][1]['value'])
        self.assertEqual('駅', streams[1]['nodes'][1]['value'])
        self.assertEqual(len(data), streams[-1]['endOffset'])
        self.assertEqual(streams[0]['endOffset'], streams[1]['offset'])

    def test_forward_shared_and_cyclic_references_are_data(self):
        body = system_class(1, 'Node', ['next', 'again'], [2, 2],
                            values=b'\x09' + i(2) + b'\x09' + i(2))
        body += system_class(2, 'Node', ['next'], [2], values=b'\x09' + i(1))
        result = nrbf.Reader(two(body)).streams()[0]
        self.assertEqual([{'ref': 2}, {'ref': 2}], [m['value'] for m in result['nodes'][1]['members']])
        self.assertEqual({'ref': 1}, result['nodes'][2]['members'][0]['value'])
        self.assertEqual(2, result['referencedObjectIds'])

    def test_duplicate_field_names_keep_ordinal_and_negative_value_type_ids(self):
        body = system_class(1, 'Derived', ['type', 'type'], [0, 0], b'\x08\x08', i(4) + i(10))
        body += system_class(-2, 'Point', ['x', 'y'], [0, 0], b'\x08\x08', i(-1) + i(3))
        result = nrbf.Reader(two(body)).streams()[0]
        fields = result['nodes'][1]['members']
        self.assertEqual([('type', 4), ('type', 10)], [(v['name'], v['value']) for v in fields])
        with self.assertRaisesRegex(ValueError, 'Ambiguous'):
            review.member(result['nodes'][1], 'type')
        self.assertEqual(-1, result['nodes'][-2]['members'][0]['value'])

    def test_classwithid_reuses_metadata_without_reusing_identity(self):
        body = system_class(1, 'Counter', ['count'], [0], b'\x08', i(7))
        body += b'\x01' + i(2) + i(1) + i(11)
        nodes = nrbf.Reader(two(body)).streams()[0]['nodes']
        self.assertEqual(7, nodes[1]['members'][0]['value'])
        self.assertEqual(11, nodes[2]['members'][0]['value'])

    def test_rank_lower_bounds_and_null_runs_stay_compact(self):
        # RectangularOffset object array: 2x3, lower bounds -2 and 5.
        body = b'\x07' + i(1) + b'\x05' + i(2) + i(2) + i(3) + i(-2) + i(5) + b'\x02\x0d\x06'
        node = nrbf.Reader(two(body)).streams()[0]['nodes'][1]
        self.assertEqual(([2, 3], [-2, 5], {}), (node['lengths'], node['lowerBounds'], node['values']))
        large = b'\x0f' + i(1) + i(300) + b'\x02' + bytes(range(100)) * 3
        node = nrbf.Reader(two(large)).streams()[0]['nodes'][1]
        self.assertEqual(300, node['primitiveData']['bytes'])
        self.assertNotIn('primitiveValues', node)

    def test_corrupt_unknown_and_ambiguous_records_fail_at_a_byte_offset(self):
        valid = b'\x06' + i(1) + string('ok')
        cases = [
            (two(valid)[:-1], 'truncated'),
            (two(valid) + b'\0', 'trailingBytes'),
            (b'BZ' + two(valid)[2:], 'unsupportedWrapper'),
            (two(b'\x15'), 'unsupportedRecord:21'),
            (two(b'\x02'), 'unsupportedRecord:2'),
            (two(b'\x09' + i(2)), 'danglingObjectReference'),
            (two(valid + valid), 'duplicateOrZeroObjectId'),
            (two(b'\x06' + i(1) + b'\x01\xff'), 'invalidUtf8'),
            (two(b'\x06' + i(1) + b'\xff\xff\xff\xff\x0f'), 'invalidStringLength'),
            (two(b'\x10' + i(1) + i(1) + b'\x0d\x02'), 'invalidNullRun'),
            (two(b'\x0d\x01'), 'nullRunOutsideArray'),
            (two(b'\x01' + i(1) + i(2)), 'missingClassMetadata'),
            (b'UC' + stream(valid), 'expectedTwoRoots'),
        ]
        for data, code in cases:
            with self.subTest(code=code), self.assertRaisesRegex(nrbf.RecordError, code + r'.*byte \d+'):
                nrbf.Reader(data).streams()

    def test_resource_limits_apply_before_allocation_or_unbounded_recursion(self):
        valid = b'\x06' + i(1) + string('abc')
        cases = [
            (two(valid), nrbf.Limits(file_bytes=4), 'fileLimit'),
            (two(valid), nrbf.Limits(string_bytes=2), 'stringLimit'),
            (two(valid), nrbf.Limits(total_string_bytes=3), 'stringLimit'),
            (two(valid), nrbf.Limits(objects=1), 'objectLimit'),
            (two(valid), nrbf.Limits(records=1), 'recordLimit'),
            (two(b'\x10' + i(1) + i(100)), nrbf.Limits(array_elements=5), 'lengthLimit'),
            (two(b'\x10' + i(1) + i(5)), nrbf.Limits(total_array_elements=3), 'arrayLimit'),
            (two(system_class(1, 'Node', ['v'], [2], values=valid[:1] + i(2) + valid[5:])), nrbf.Limits(depth=0), 'depthLimit'),
        ]
        for data, limits, code in cases:
            with self.subTest(code=code), self.assertRaisesRegex(nrbf.RecordError, code):
                nrbf.Reader(data, limits).streams()


class PersistenceInventory(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.plan = json.loads(review.PLAN.read_text())
        cls.ledger = json.loads((review.inventory.OUT / 'base-ledger.json').read_text())
        cls.base = review.inventory.Tree(review.ROOT, cls.plan['baseRevision'])
        cls.implementation = review.inventory.Tree(review.ROOT, cls.plan['implementationRevision'])
        cls.source, cls.transition = review.extract(cls.plan, cls.base, cls.implementation, cls.ledger)
        cls.types = {t['name']: t for t in cls.source['types']}

    def test_inventory_reproduces_every_candidate_and_pinned_source_hash(self):
        self.assertEqual({r['path'] for r in self.ledger['serializationCandidates']},
                         {r['path'] for r in self.source['sourceReviews']})
        for row in self.source['sourceReviews']:
            self.assertEqual(row['sha256'], review.inventory.digest(self.base.read(row['path'])))
        for filename, expected in [('serialization-review.json', self.source), ('domain-transition-review.json', self.transition)]:
            self.assertEqual(expected, json.loads((review.inventory.OUT / filename).read_text()))
        altered = dict(self.plan, baseRevision='unreviewed')
        with self.assertRaisesRegex(ValueError, 'fresh source review'):
            review.extract(altered, self.base, self.implementation, self.ledger)

    def test_nested_fields_custom_overrides_and_exclusions_are_distinct(self):
        outside = self.types['freetrain.world.CarState+Outside']
        self.assertEqual({'timeLeft', 'location', 'direction'}, {f['name'] for f in outside['fieldSlots']})
        clock = self.types['freetrain.world.Clock']
        self.assertEqual('nonserialized', next(f['exclusion'] for f in clock['fields'] if f['name'] == 'endOfTurnHandlers'))
        traincar = self.types['freetrain.world.rail.Train+TrainCar']
        self.assertIn('freetrain.world.Car', traincar['sourceAncestors'])
        voxel = self.types['freetrain.world.structs.PThreeDimStructure+VoxelImpl']
        self.assertIn('freetrain.world.Voxel', voxel['sourceAncestors'])
        for name, keys in [('freetrain.world.Direction', ['index']), ('freetrain.world.rail.RailPattern', ['id']),
                           ('freetrain.framework.BGMManager+TempBGMContribution', ['fileName'])]:
            self.assertEqual(keys, self.types[name]['customKeys'])
            self.assertEqual(name, self.types[name]['customWriter'])
        c = self.types['freetrain.contributions.train.SymTrainCarImpl']
        self.assertEqual('freetrain.framework.plugin.Contribution', c['customWriter'])
        duplicate = [t for t in self.source['types'] if t['name'] == 'freetrain.world.rail.signal.RailSignalContribution']
        self.assertEqual(2, len({t['sourceTypeId'] for t in duplicate}))

    def test_lexical_scanner_ignores_strings_comments_directives_and_properties(self):
        source = '''namespace Example {
        // [Serializable] class Fake { public int bad; }
        [Serializable] class Outer { public int x,y;
        #region data
        [NonSerialized] public object cache;
        public int Value { get { return 0; } }
        public static bool operator !=(Outer a, Outer b) { return false; }
        public static bool operator ==(Outer a, Outer b) { return true; }
        [Serializable] class Inner { private string note = "class Fake { }"; }
        #endregion
        } }'''
        outer, inner = serialization.declarations(source)
        self.assertEqual('Example.Outer+Inner', inner['name'])
        self.assertEqual(['x', 'y', 'cache'], [f['name'] for f in outer['fields']])
        self.assertEqual('nonserialized', outer['fields'][2]['exclusion'])

    def test_direction_track_and_version_design_matches_source_tables(self):
        plan = self.plan
        geometry = json.loads((review.inventory.OUT / 'geometry.json').read_text())
        self.assertEqual(list(range(8)), [d['index'] for d in plan['directions']])
        self.assertEqual([0, 2, 4, 6], [d['newIndex'] for d in plan['prototypeDirections']])
        patterns = [p for p in geometry['patterns'] if p['family'] in ('normal', 'junction')]
        self.assertEqual(8, len(plan['prototypeTracks']))
        for row in plan['prototypeTracks']:
            matches = [p['id'] for p in patterns if set(p['connections']) == set(row['ports'])]
            self.assertEqual([row['sourcePatternId']] if row['sourcePatternId'] is not None else [], matches)
        schema = json.loads(self.implementation.read('prototype/protocol/schema.json'))
        save = json.loads(self.implementation.read('prototype/protocol/fixtures/save-reference.json'))
        self.assertEqual((1, 'prototype-content-1', 'reference-v1'), (save['saveVersion'], save['contentVersion'], save['scenarioId']))
        self.assertIn('v2.schema.json', schema['$id'])
        self.assertEqual({f'TRANS-{i:03}' for i in range(1, 13)}, {d['id'] for d in plan['decisions']})
        self.assertEqual({f'MIG-{i:02}' for i in range(1, 9)}, {d['id'] for d in plan['acceptance']})
        self.assertEqual({f'SAVE-{i:03}' for i in range(1, 7)}, {d['id'] for d in plan['differences']})

    def test_optional_owner_sample_matches_its_local_report_without_becoming_a_ci_dependency(self):
        sample = review.ROOT / 'tmp/000-001-sample-save/recovered/データ０１.ftgd'
        report = review.ROOT / 'tmp/parity-p0/p0-05-sample-review.json'
        if not sample.is_file() or not report.is_file():
            self.skipTest('Owner-supplied historical sample not distributed with base source')
        result = review.inspect_sample(sample, self.source, self.ledger)
        # JSON normalizes dictionary integer keys; compare serialized values.
        self.assertEqual(json.loads(review.inventory.dump_ledger(result)), json.loads(report.read_text()))
        self.assertEqual('35b24fae1b40e5004ca46e9968dd34f3da61d5e6009f8ea1b4f60d256a7b9313', result['sampleSha256'])
        self.assertEqual({'x': 1500, 'y': 1090, 'z': 45}, result['worldFacts']['size'])
        self.assertEqual(33_367, sum(s['objects'] for s in result['roots']))
        self.assertEqual(12, sum(c['disposition'] == 'exact-base-ID' for c in result['contributionReferences']))
        self.assertEqual(3, sum('difference' in c['disposition'] for c in result['sourceLayoutComparison']))


if __name__ == '__main__':
    unittest.main()
