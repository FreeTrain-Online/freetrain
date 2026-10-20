"""ExAV profile separation, review closure and attributed callback evidence."""
import importlib.util
import json
from pathlib import Path
import sys
import unittest

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))
spec = importlib.util.spec_from_file_location('exav_persistence', SCRIPTS / 'review-exav-persistence.py')
review = importlib.util.module_from_spec(spec)
spec.loader.exec_module(review)
OUT = review.OUT


def read(name):
    return json.loads((OUT / name).read_text())


class ExavPersistenceEvidence(unittest.TestCase):
    def test_every_candidate_has_path_qualified_mapping_and_membership(self):
        ledger = read('exav-ledger.json')
        result = read('exav-serialization-review.json')
        self.assertEqual({r['path'].removeprefix('trunk/') for r in ledger['serializationCandidates']},
                         {r['path'] for r in result['sourceReviews']})
        self.assertEqual(len(result['types']), len({r['sourceTypeId'] for r in result['types']}))
        self.assertTrue(all('projectMembership' in r for r in result['sourceReviews']))
        self.assertTrue(all(r['target'] and r['phase'] for r in result['types']))

    def test_attribute_repairs_and_optional_fields_are_retained(self):
        rows = {r['path']: r for r in read('exav-serialization-review.json')['sourceReviews']}
        callbacks = rows['core/world/accounting/TransactionHistory.cs']['serializationAttributes']
        self.assertEqual(2, len(callbacks))
        self.assertTrue(all('OnDeserialized' in r['declaration'] for r in callbacks))
        fields = rows['core/world/rail/ThinPlatform.cs']['serializationAttributes']
        self.assertEqual(2, len(fields))
        self.assertTrue(all('VersionAdded=5' in r['declaration'] and 'passagewayRailType' in r['declaration'] for r in fields))
        # The Train source's OptionalField mention is only a comment.
        self.assertEqual([], rows['core/world/rail/Train.cs']['serializationAttributes'])

    def test_old_profiles_remain_distinct_from_stable(self):
        samples = read('exav-save-samples.json')['samples']
        self.assertEqual([12, 2], [len(s['fieldNameDifferences']) for s in samples])
        self.assertEqual(2, len({s['sampleSha256'] for s in samples}))
        newer = samples[1]
        self.assertEqual({'x': 400, 'y': 600, 'z': 16}, newer['worldFacts']['size'])
        self.assertEqual({'freetrain.finance.stock.Economy', 'freetrain.world.rail.Train'},
                         {r['type'] for r in newer['fieldNameDifferences']})
        self.assertTrue(all('0.6.3326.40211' in r['libraries']['2'] for r in newer['roots']))
        self.assertNotEqual('2.1.1.12', newer['roots'][0]['libraries']['2'])

    def test_transition_keeps_runtime_triple_and_replaces_reserved_authority(self):
        plan = read('exav-domain-transition.json')
        self.assertEqual(dict(protocol=2, content='prototype-content-1', save=1, scenario='reference-v1'), plan['current'])
        self.assertEqual('exav-2.1.1.12-v1', plan['reservedDesign']['parityRuleset'])
        self.assertEqual('exav-bundled-v2-1', plan['reservedDesign']['baseContent'])
        self.assertEqual(read('exav-baseline.json')['revision'], plan['baseRevision'])

    def test_review_rejects_hash_and_profile_drift(self):
        class Tree:
            revision = 'implementation'
            def read(self, path):
                return b'actual'
        config = dict(sourceId='exav', revision='stable', subtree='trunk')
        rules = dict(config, implementationRevision='implementation', sourceReviews=[dict(path='a', sha256='wrong')], implementationSourceReviews=[])
        plan = dict(sourceId='exav', baseRevision='stable', subtree='trunk', implementationRevision='implementation')
        with self.assertRaisesRegex(ValueError, 'hash drift'):
            review.validate_rules(Tree(), config, rules, plan, Tree())
        plan['baseRevision'] = 'older-profile'
        with self.assertRaisesRegex(ValueError, 'profile mismatch'):
            review.validate_rules(Tree(), config, rules, plan, Tree())


if __name__ == '__main__':
    unittest.main()
