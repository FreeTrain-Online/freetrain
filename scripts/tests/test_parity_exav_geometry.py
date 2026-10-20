"""ExAV reassessment guards and source edge cases; no external checkout needed."""
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('exav_geometry', ROOT / 'scripts/review-exav-geometry.py')
review = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(review)


class ExavGeometry(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.rules = json.loads((ROOT / 'doc/parity/exav-geometry-rules.json').read_text())
        cls.behavior = json.loads((ROOT / 'doc/parity/exav-geometry-behavior.json').read_text())

    def test_rejects_unreviewed_revision_and_source_drift(self):
        class Unreviewed:
            revision = 'unreviewed'
        with self.assertRaisesRegex(ValueError, 'fresh source review'):
            review.build(Unreviewed(), None, self.rules)
        class Drift:
            revision = self.rules['revision']
            def read(self, path):
                return b'changed source'
        with self.assertRaisesRegex(ValueError, 'hash drift'):
            review.build(Drift(), None, self.rules)

    def test_review_sources_resolve_to_pinned_exav_discovery(self):
        ledger = json.loads((ROOT / 'doc/parity/exav-ledger.json').read_text())
        files = {r['baselinePath']: r['sha256'] for r in ledger['fileInventory']}
        for row in self.rules['sourceReviews']:
            self.assertEqual(files['trunk/' + row['path']], row['sha256'])
        comparison = self.behavior['historicalComparison']
        self.assertTrue(comparison['patternsEqual'])
        self.assertTrue(comparison['directionIndicesAndVectorsEqual'])

    def test_numeric_speed_five_is_not_clamped_to_native(self):
        cases = {r['nativeMinutes']: r for r in self.behavior['speedLimitCases']}
        self.assertEqual((4, 3, 2, 5), tuple(cases[5][k] for k in ('low', 'medium', 'fast', 'unrestricted')))
        self.assertEqual((4, 4, 4), tuple(cases[4][k] for k in ('low', 'medium', 'fast')))

    def test_platform_length_boundary_uses_csharp_truncation(self):
        cases = {(r['platformLength'], r['trainLength']): r for r in self.behavior['platformStopCases']}
        self.assertEqual((3, 0), (cases[4, 5]['along'], cases[4, 5]['against']))
        self.assertTrue(cases[4, 5]['alongStops'])
        self.assertTrue(cases[4, 5]['againstStops'])
        self.assertFalse(cases[4, 6]['alongStops'])
        self.assertFalse(cases[4, 6]['againstStops'])
        charges = self.behavior['movementCostCases']
        self.assertEqual([1, 5, 9], [r['successfulMove'] for r in charges if r['charge']])
        self.assertEqual({122000}, {r['charge'] for r in charges if r['charge']})


if __name__ == '__main__':
    unittest.main()
