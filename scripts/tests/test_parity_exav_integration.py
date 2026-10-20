"""Offline checks for P0-09 scope boundaries and complete source handoffs."""
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('integration', ROOT/'scripts/review-exav-integration.py')
review = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(review)


class IntegrationBoundaries(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.rules = json.loads(review.RULES.read_text())
        cls.report = json.loads(review.OUTPUT.read_text())

    def test_stable_sources_are_all_accounted_for_without_status_promotion(self):
        ledger = json.loads((review.PARITY/'exav-ledger.json').read_text())
        expected = {r['baselinePath'].removeprefix('trunk/'): r['sha256']
                    for r in ledger['fileInventory'] if Path(r['baselinePath']).suffix in review.EXTENSIONS}
        rows = self.report['sourceAssignments']
        self.assertEqual(len(expected), len(rows))
        self.assertEqual(expected, {r['path']: r['sha256'] for r in rows})
        self.assertTrue(all(r['phases'] and r['destination'] and 'pending' in r['status'] for r in rows))
        self.assertEqual({'inventoried'}, {r['status'] for r in ledger['sourceFiles']})

    def test_scope_distinguishes_gaps_reachable_features_and_retired_hosts(self):
        rules = self.rules['sourceAssignments']
        for path, identity in {
            'core/world/road/Bus.cs': 'source-bus-gap',
            'core/world/rail/DelegationTrainControllerImpl.cs': 'train-control',
            'tools/MapConstructionDriver/Driver.cs': 'map-construction',
            'plugins/org.kohsuke.freetrain.music.a4mem/src/FactoryImpl.cs': 'excluded-a4',
            'plugins/org.kohsuke.freetrain.tools.dbgwnd/src/DebugWindowPlugIn.cs': 'excluded-debug',
            'experiments/RoadSimulator/World.cs': 'experimental',
        }.items():
            self.assertEqual(identity, review.assign(path, rules)['id'])
        with self.assertRaisesRegex(ValueError, 'Unassigned source'):
            review.assign('plugins/new-unreviewed/Example.cs', rules)

    def test_all_requirements_and_defect_ids_survive_in_maintained_inputs(self):
        self.assertEqual([f'EXAV-{n:02}' for n in range(1, 17)], [r['id'] for r in self.report['requirements']])
        self.assertTrue({f'EXAV-D{n:02}' for n in range(1, 7)} <= {r['id'] for r in self.report['gaps']})
        for row in self.report['requirements']+self.report['gaps']:
            self.assertTrue(row['acceptance'] and row['phases'] and row['evidence'])
            self.assertTrue(all(len(e['sha256']) == 64 and e['symbols'] for e in row['evidence']))

    def test_branch_profiles_do_not_collapse_engine_or_time_boundaries(self):
        profiles = {r['id']: r for r in self.report['branchProfiles']}
        self.assertEqual('trunk', profiles['stable']['subtree'])
        self.assertIn('excluded', profiles['beta22']['disposition'])
        self.assertIn('excluded', profiles['alpha31']['disposition'])
        self.assertEqual('Beta2.0.2.11/ALPHA3/trunk', profiles['alpha30']['subtree'])
        self.assertEqual(6, len(profiles))

    def test_generated_inputs_remain_pinned(self):
        for name, digest in self.report['inputs'].items():
            path = ROOT/name if name.startswith('doc/') else review.PARITY/name
            self.assertEqual(digest, review.inv.digest(path.read_bytes()), name)
        self.assertEqual(self.report['rulesSha256'], review.inv.digest(review.RULES.read_bytes()))
        self.assertEqual(self.report['generatorSha256'], review.inv.digest((ROOT/'scripts/review-exav-integration.py').read_bytes()))
