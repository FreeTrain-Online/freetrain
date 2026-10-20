"""ExAV reassessment guards and source edge cases; no external checkout needed."""
import importlib.util
import copy
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
        cls.dependencies = json.loads((ROOT / 'doc/parity/exav-geometry-dependency-review.json').read_text())

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
        for row in self.rules['sourceReviews'] + self.dependencies['sourceReviews']:
            self.assertEqual(files['trunk/' + row['path']], row['sha256'])
        comparison = self.behavior['historicalComparison']
        self.assertTrue(comparison['patternsEqual'])
        self.assertTrue(comparison['directionIndicesAndVectorsEqual'])

    def test_dependency_edges_resolve_and_evidence_is_generated(self):
        paths = {row['path'] for row in self.dependencies['sourceReviews']}
        self.assertEqual(self.dependencies, self.behavior['dependencyReview'])
        steps = [s for c in self.dependencies['chains'] for s in c['steps']]
        self.assertEqual(len(steps), len({s['id'] for s in steps}))
        for chain in self.dependencies['chains']:
            self.assertLessEqual(set(chain['files']), paths)
            self.assertTrue(chain['handoffs'])
            for step in chain['steps']:
                for endpoint in ('caller', 'dependency'):
                    self.assertIn(step[endpoint]['path'], chain['files'])
                    self.assertTrue(step[endpoint]['symbol'])
                self.assertTrue(step['acceptanceCases'])
                self.assertTrue(step['phase'])

    def test_dependency_guard_rejects_missing_chain_and_unknown_endpoint(self):
        # Structure validation uses matching synthetic hashes; this deliberately
        # tests rejection of incomplete provenance, not a C# behavior simulator.
        class Synthetic:
            def read(self, path):
                return b'review fixture'
        valid = copy.deepcopy(self.dependencies)
        for row in valid['sourceReviews']:
            row['sha256'] = review.inv.digest(b'review fixture')
        review.validate_dependencies(Synthetic(), self.rules, valid)
        missing = copy.deepcopy(valid)
        missing['chains'].pop()
        with self.assertRaisesRegex(ValueError, 'Missing geometry dependency chain'):
            review.validate_dependencies(Synthetic(), self.rules, missing)
        unknown = copy.deepcopy(valid)
        unknown['chains'][0]['steps'][0]['dependency']['path'] = 'unreviewed.cs'
        with self.assertRaisesRegex(ValueError, 'Unreviewed dependency endpoint'):
            review.validate_dependencies(Synthetic(), self.rules, unknown)
        drift = copy.deepcopy(valid)
        drift['sourceReviews'][0]['sha256'] = 'stale'
        with self.assertRaisesRegex(ValueError, 'hash drift'):
            review.validate_dependencies(Synthetic(), self.rules, drift)

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
