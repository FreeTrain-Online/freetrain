"""P4-02 literal-oracle and evidence corruption regressions, no live rail claims."""
from copy import deepcopy
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('railway_fixtures', ROOT / 'scripts/railway-fixtures.py')
oracle = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(oracle)


class RailwayFixtures(unittest.TestCase):
    def setUp(self):
        self.data = json.loads(oracle.FIXTURES.read_text())

    def case(self, group, identity):
        return next(c for c in self.data['groups'][group]['cases'] if c['id'] == identity)

    def test_all_checked_in_vectors_without_external_source_or_runtime(self):
        self.assertEqual((1484, 60, 26), oracle.validate(self.data))

    def test_expected_output_corruption_in_every_operation_family(self):
        for group in self.data['groups'].values():
            with self.subTest(group=group['cases'][0]['id']):
                original = deepcopy(group['cases'][0]['expected'])
                group['cases'][0]['expected'] = {'invented': 'production output'}
                with self.assertRaisesRegex(ValueError, 'Fixture mismatch'):
                    oracle.validate(self.data)
                group['cases'][0]['expected'] = original

    def test_missing_matrix_cell_even_if_witness_list_is_rewritten(self):
        for name, expected in [('attachments', 'attachment'), ('detachments', 'detachment')]:
            with self.subTest(matrix=name):
                data = deepcopy(self.data)
                group = data['groups'][name]
                group['cases'].pop()
                group['requiredCaseIds'].pop()
                with self.assertRaisesRegex(ValueError, 'Incomplete '+expected+' matrix'):
                    oracle.validate(data)

    def test_missing_small_group_case_and_unknown_operation(self):
        self.data['groups']['supports']['cases'].pop()
        with self.assertRaisesRegex(ValueError, 'Missing/reordered fixture witness'):
            oracle.validate(self.data)
        self.data['groups']['invented'] = self.data['groups']['supports']
        with self.assertRaisesRegex(ValueError, 'Missing/unknown fixture group'):
            oracle.validate(self.data)

    def test_source_hash_symbol_artifact_and_status_drift(self):
        for mutate, message in [
            (lambda d: d['groups']['routes']['sources'][0].update(sha256='wrong'), 'Unreviewed fixture source'),
            (lambda d: d['groups']['routes']['sources'][0].update(symbols=['invented']), 'Unreviewed fixture source'),
            (lambda d: d['artifactBindings'].update({'exav-geometry.json': 'wrong'}), 'artifact binding drift'),
            (lambda d: d.update(status='runtime-verified'), 'authority/status drift'),
            (lambda d: d['authority'].update(revision='wrong'), 'authority/status drift'),
        ]:
            with self.subTest(message=message):
                data = deepcopy(self.data)
                mutate(data)
                with self.assertRaisesRegex(ValueError, message):
                    oracle.validate(data)

    def test_direction_pattern_scenario_and_coverage_loss(self):
        for mutate, message in [
            (lambda d: d['directions'][0].update(opposite=2), 'Literal direction drift'),
            (lambda d: d['patterns'][0].update(connections=[0, 2]), 'Literal P0 pattern drift'),
            (lambda d: d['scenarioBindings']['EXAV-SCN-002']['sourceExpected'].update(previewCost=3), 'Invalid scenario binding'),
            (lambda d: d['coverage'].pop(), 'Missing fixture coverage obligation'),
        ]:
            with self.subTest(message=message):
                data = deepcopy(self.data)
                mutate(data)
                with self.assertRaisesRegex(ValueError, message):
                    oracle.validate(data)

    def test_preview_preserves_supplied_world_sentinel_and_rejected_work_spends_nothing(self):
        patterns = {p['id']: p for p in self.data['patterns']}
        for identity in ('ROUTE-no-mutation-all-state', 'ROUTE-protected-car', 'ROUTE-reclaim-shared-value-per-cell'):
            case = self.case('routes', identity)
            before = deepcopy(case['input'])
            result = oracle.route(case['input'], patterns)
            self.assertEqual(before, case['input'])
            self.assertEqual(oracle.UNCHANGED, result['previewDelta'])
            if result['outcome'] == 'reject':
                self.assertEqual(0, result['spend'])

    def test_retained_noop_detach_still_charges_and_removes_foreign_piers(self):
        case = self.case('normalRemoval', 'REMOVE-noop-foreign-piers')
        result = oracle.normal_remove(case['input'], {p['id']: p for p in self.data['patterns']})
        self.assertEqual([], result['changed'])
        self.assertEqual(16000000, result['spend'])
        self.assertEqual(3, len(result['removedPiers']))
        self.assertEqual('other', result['removedPiers'][0]['owner'])

    def test_junction_null_pattern_hazard_stays_distinct_from_clear_or_normal(self):
        hazards = [c for c in self.data['groups']['detachments']['cases']
                   if c['expected']['sourceOutcome'] == 'null-pattern']
        self.assertEqual(16, len(hazards))
        self.assertTrue(all(c['expected']['portOutcome'] == 'reject-invalid-pattern' for c in hazards))
        self.assertEqual('reject-invalid-pattern', self.case('normalRemoval', 'REMOVE-invalid-junction-rollback')['expected']['outcome'])

    def test_special_endpoint_noop_and_repeated_subtotal_are_separate(self):
        garage = self.case('specialRails', 'GARAGE-nonmatching-still-spends')['expected']
        self.assertEqual([200000, 200000, 400000], garage['charges'])
        self.assertEqual(800000, garage['spend'])
        for op in ('BRIDGE', 'GIRDER', 'TUNNEL', 'GARAGE'):
            self.assertEqual('source-noop', self.case('specialRails', op+'-coincident')['expected']['outcome'])
            self.assertEqual('reject', self.case('specialRails', op+'-coincident-build')['expected']['outcome'])

    def test_late_ground_edits_preserve_stored_slope_flags_and_art(self):
        original = self.case('slopes', 'SLOPE-2-mixed')
        edited = self.case('slopes', 'SLOPE-stored-flags-after-ground-edit')
        self.assertNotEqual(edited['input']['ground'], edited['input']['groundAfter'])
        self.assertEqual(oracle.slope(original['input'], {}), oracle.slope(edited['input'], {}))


if __name__ == '__main__':
    unittest.main()
