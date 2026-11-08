"""P6-02 source-oracle regressions; these do not execute station gameplay."""
from copy import deepcopy
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('station_fixtures', ROOT/'scripts/station-fixtures.py')
oracle = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(oracle)


class StationFixtures(unittest.TestCase):
    def setUp(self):
        self.data = json.loads(oracle.FIXTURES.read_text())

    def validate(self):
        # Rehash corruption, so the independent checks must do the work.
        self.data['sha256'] = oracle.digest({k:v for k,v in self.data.items() if k != 'sha256'})
        return oracle.validate(self.data)

    def case(self, group, id):
        return next(c for c in self.data['groups'][group]['cases'] if c['id'] == id)

    def test_complete_source_literals(self):
        self.assertEqual((821,38), oracle.validate(self.data))

    def test_every_family_rejects_rehashed_changed_expectations(self):
        for name,g in self.data['groups'].items():
            c = g['cases'][0]; old = c['expected']; c['expected'] = {'invented':'production output'}
            with self.subTest(family=name), self.assertRaisesRegex(ValueError,'Fixture mismatch'):
                self.validate()
            c['expected'] = old

    def test_missing_case_in_every_matrix(self):
        for name,g in self.data['groups'].items():
            row = g['cases'].pop()
            with self.subTest(family=name), self.assertRaisesRegex(ValueError,'Incomplete fixture matrix'):
                self.validate()
            g['cases'].append(row)

    def test_reviewed_input_domain_cannot_be_replaced_and_recomputed(self):
        c = self.case('thin','thin-6-1-4')
        c['input']['heading'] = 2
        c['expected'] = oracle.thin(c['input'])
        with self.assertRaisesRegex(ValueError,'Reviewed input/mapping drift'):
            self.validate()

    def test_sources_status_prerequisites_and_owners_are_pinned(self):
        self.data['sources'][0]['sha256'] = 'invented'
        with self.assertRaisesRegex(ValueError,'Unreviewed source'): self.validate()
        self.setUp(); self.data['runtimeStatus'] = 'station-gameplay-verified'
        with self.assertRaisesRegex(ValueError,'authority/status drift'): self.validate()
        self.setUp(); self.data['inputs']['station-review.json'] = 'changed'
        with self.assertRaisesRegex(ValueError,'Input artifact drift'): self.validate()
        self.setUp(); self.data['groups']['boarding']['runtimeOwner'] = 'P6-28'
        with self.assertRaisesRegex(ValueError,'Reviewed input/mapping drift'): self.validate()

    def test_scenario_links_cannot_be_removed_or_attached_to_unrelated_cases(self):
        self.data['scenarioBindings']['EXAV-SCN-011']['cases'] = ['draw-thin-0-0']
        with self.assertRaisesRegex(ValueError,'Reviewed scenario link drift'): self.validate()
        self.setUp(); del self.data['scenarioBindings']['EXAV-SCN-020']
        with self.assertRaisesRegex(ValueError,'Missing scenario binding'): self.validate()
        self.setUp(); self.data['differenceBindings'][0]['decision'] = 'normalize'
        with self.assertRaisesRegex(ValueError,'Listener difference binding drift'): self.validate()

    def test_all_evaluators_preserve_inputs(self):
        for name,g in self.data['groups'].items():
            for c in g['cases']:
                before = deepcopy(c['input'])
                oracle.evaluate(name,c['input'])
                self.assertEqual(before,c['input'],c['id'])

    def test_range_uses_L_endpoint_but_only_L_actual_full_height_columns(self):
        e = self.case('hostRange','range-2')['expected']
        self.assertEqual([[2,2],[22,18]],e['rectangle'])
        self.assertEqual([14,10,2],e['otherEnd'])
        self.assertIn('column-high',e['candidates'])
        self.assertNotIn('endpoint-not-column',e['candidates'])
        self.assertEqual('S2',e['automaticHost'])

    def test_descendant_reparent_disconnects_before_testing_requested_host(self):
        e = self.case('hostGraph','graph-1')['expected']
        self.assertEqual({'A':None,'B':None,'C':None,'D':None},e['parents'])
        for n in (4,5,6):
            self.assertEqual({'rejected':'invalid-host-graph','published':False},
                             oracle.evaluate('hostGraph',self.case('hostGraph',f'graph-{n}')['input']))

    def test_all_six_invalid_stair_resources_stay_invalid(self):
        rows = [c for c in self.data['groups']['drawing']['cases'] if not c['expected']['validCrop']]
        self.assertEqual(6,len(rows))
        self.assertEqual({'concrete','brown','green'},{c['input']['material'] for c in rows})
        self.assertEqual({'day','night'},{c['input']['light'] for c in rows})
        self.assertTrue(all(c['expected']['selector'] == 23 and c['expected']['rectangle'] == [388,4,32,36] for c in rows))

    def test_stop_positions_and_exact_yard_handoff(self):
        for direction,positions in ((1,[2,3,3,4]),(0,[0,0,0,-1])):
            for n,pos in zip(range(3,7),positions):
                self.assertEqual(pos,self.case('stopIndices',f'stop-{n}-{direction}')['expected']['stopPosition'])
        self.assertEqual(14,len(self.data['groups']['yards']['cases']))
        self.assertEqual(['controller','reverse','re-place','bell'],self.case('yards','yard-handler-effects')['expected']['calls'])

    def test_packed_capacity_fallback_and_zero_population_loader_gate(self):
        self.assertEqual(100,self.case('capacity','capacity-1')['expected']['seated'])
        self.assertEqual(160,self.case('capacity','capacity-0')['expected']['packed'])
        e = self.case('boarding','board-5')['expected']
        self.assertEqual(1,e['trains']); self.assertFalse(e['trace'][0]['loaderCalled'])
        self.assertEqual([160,320,256,416],[r['gone'] for r in self.case('boarding','board-0')['expected']['trace']])

    def test_float32_decay_and_checked_original_width_rejections(self):
        self.assertEqual(319,self.case('decay','decay-2')['expected']['unloaded'])
        self.assertNotEqual(int(16777217*.9996),self.case('decay','decay-5')['expected']['loaded'])
        for family,id in [('costs','cost-8'),('capacity','capacity-6'),('boarding','board-8'),('fare','fare-5'),('nested','nested-3')]:
            self.assertEqual({'rejected':'checked-source-width','published':False},oracle.evaluate(family,self.case(family,id)['input']))
        self.case('decay','decay-0')['expected']['factor'] += 1e-14
        with self.assertRaisesRegex(ValueError,'Fixture mismatch'): self.validate()

    def test_listener_order_fifth_asymmetry_tie_and_source_removal_diagnostic(self):
        self.assertEqual([],self.case('listeners','listener-7')['expected']['local'])
        self.assertEqual(['S'],self.case('listeners','listener-6')['expected']['local'])
        e = self.case('listeners','listener-10')['expected']
        self.assertIn('S4',e['local']); self.assertNotIn('S4',e['reverse'])
        self.assertNotIn('S4',self.case('listeners','listener-11')['expected']['local'])
        self.assertEqual(['S4'],self.case('listeners','listener-13')['expected']['danglingAfterSourceRemoval'])

    def test_hour_midnight_fifo_changes_boarding_and_log_assignment(self):
        before = self.case('fifo','fifo-0')['expected']['trace'][1]
        after = self.case('fifo','fifo-1')['expected']['trace'][0]
        self.assertEqual(118,before['today']); self.assertEqual(70,after['today'])
        self.assertEqual(0,self.case('fifo','fifo-3')['expected']['trace'][1]['today'])
        self.assertEqual(8680000,self.case('fifo','fifo-2')['expected']['trace'][-1]['cash'])

    def test_all_provider_cells_and_vacation_branches_are_distinct(self):
        self.assertEqual(1008,sum(len(c['expected']['percent']) for c in self.data['groups']['tables']['cases']))
        self.assertEqual('weekendDistribution',self.case('providers','provider-SchoolPopulation-3-17-0')['expected']['table'])
        self.assertEqual(125,self.case('providers','provider-LeisureLandPopulation-3-17-0')['expected']['divisor'])
        self.assertEqual('sundayEntering',self.case('providers','provider-BarPopulation-2-7-1')['expected']['table'])
        e = self.case('logs','log-nonuniform')['expected']['trace'][-1]['lastWeek']
        self.assertNotAlmostEqual(sum([0,1,32,243,1024,3125,7776])/7,e)


if __name__ == '__main__':
    unittest.main()
