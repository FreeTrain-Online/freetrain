"""P3-01 evidence guards; these are not production terrain acceptance tests."""
import copy
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('terrain_review', ROOT/'scripts/review-terrain.py')
review = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(review)


class TerrainReview(unittest.TestCase):
    def setUp(self):
        self.contract, self.fixtures, self.locale = [json.loads((ROOT/'doc/parity'/name).read_text())
            for name in ('terrain-review.json', 'terrain-fixtures.json', 'terrain-localization-review.json')]

    def validate(self):
        return review.validate(self.contract, self.fixtures, self.locale)

    def test_checked_in_source_specification_and_independent_vectors(self):
        self.assertEqual((40, 77, 59), self.validate())

    def test_unknown_revision_and_source_hash_are_rejected(self):
        original = copy.deepcopy(self.contract)
        self.contract['authority']['revision'] = 'unreviewed'
        with self.assertRaisesRegex(ValueError, 'Unreviewed source identity'):
            self.validate()
        self.contract = original
        self.contract['sourceReviews'][0]['sha256'] = 'changed'
        with self.assertRaisesRegex(ValueError, 'Source hash drift'):
            self.validate()

    def test_wrong_padding_and_lightness_expectations_are_rejected(self):
        for kind, key, wrong in [('dimensions', 'storage', [30, 30, 6]), ('lightness', 'brightness', 0.2126)]:
            with self.subTest(kind=kind):
                original = copy.deepcopy(self.fixtures)
                row = next(r for r in self.fixtures['fixtures'] if r['kind'] == kind and
                           (kind != 'lightness' or r['input']['rgb'] == [255, 0, 0]))
                row['expected'][key] = wrong
                with self.assertRaisesRegex(ValueError, 'Fixture mismatch'):
                    self.validate()
                self.fixtures = original

    def test_missing_decision_and_unknown_fixture_source_are_rejected(self):
        original = copy.deepcopy(self.contract)
        self.contract['decisions'].pop()
        with self.assertRaisesRegex(ValueError, 'Missing terrain decision'):
            self.validate()
        self.contract = original
        self.fixtures['fixtures'][0]['sources'] = ['unreviewed.cs']
        with self.assertRaisesRegex(ValueError, 'Unreviewed fixture source'):
            self.validate()

    def test_unknown_fixture_and_overclaimed_status_are_rejected(self):
        self.fixtures['fixtures'][0]['kind'] = 'unreviewed-algorithm'
        with self.assertRaisesRegex(ValueError, 'Unknown terrain fixture kind'):
            self.validate()
        self.fixtures['fixtures'][0]['status'] = 'verified'
        with self.assertRaisesRegex(ValueError, 'Overclaimed or unexplained fixture'):
            self.validate()

    def test_manual_developer_and_translation_drift_are_rejected(self):
        original = copy.deepcopy(self.contract)
        self.contract['workflowMappings'][0]['manual'].append('MAN-NONEXISTENT')
        with self.assertRaisesRegex(ValueError, 'Missing manual mapping'):
            self.validate()
        self.contract = copy.deepcopy(original)
        self.contract['workflowMappings'][0]['developer'].append('DEV-NONEXISTENT')
        with self.assertRaisesRegex(ValueError, 'Missing developer mapping'):
            self.validate()
        self.contract = original
        self.locale['sourceStrings'][0]['ja'] = 'changed text'
        with self.assertRaisesRegex(ValueError, 'Source text hash drift'):
            self.validate()

    def test_lost_forest_variant_and_per_tree_charging_are_rejected(self):
        original = copy.deepcopy(self.contract)
        self.contract['forestContributions'].pop()
        with self.assertRaisesRegex(ValueError, 'Missing forest variants'):
            self.validate()
        self.contract = original
        row = next(r for r in self.fixtures['fixtures'] if r['kind'] == 'land-booking' and r['input']['placed'] == 0)
        row['expected']['spend'] = 0
        with self.assertRaisesRegex(ValueError, 'Fixture mismatch'):
            self.validate()

    def test_source_and_added_generation_water_eligibility_stay_distinct(self):
        rows = [r for r in self.fixtures['fixtures'] if r['kind'] == 'forest-eligibility' and
                r['input']['z'] < r['input']['water']]
        self.assertEqual({'source', 'generation'}, {r['classification'] for r in rows})
        self.assertEqual([True, False], [r['expected']['allowed'] for r in rows])
        self.assertNotEqual('source', self.contract['generationDecisions'][0]['classification'])


if __name__ == '__main__':
    unittest.main()
