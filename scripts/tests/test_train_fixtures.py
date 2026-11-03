"""P5-02 source-oracle corruption tests; no native/runtime train claims."""
from copy import deepcopy
import importlib.util
import json
from pathlib import Path
import unittest

ROOT=Path(__file__).resolve().parents[2]
SPEC=importlib.util.spec_from_file_location('train_fixtures',ROOT/'scripts/train-fixtures.py')
oracle=importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(oracle)


class TrainFixtures(unittest.TestCase):
    def setUp(self):
        self.data=json.loads(oracle.FIXTURES.read_text())

    def validate(self):
        # Semantic checks must reject corruptions even with a refreshed digest.
        self.data['sha256']=oracle.digest({k:v for k,v in self.data.items() if k!='sha256'})
        return oracle.validate(self.data)

    def case(self,group,id):
        return next(c for c in self.data['groups'][group]['cases'] if c['id']==id)

    def test_complete_checked_in_literals(self):
        self.assertEqual((744,20),oracle.validate(self.data))

    def test_every_family_rejects_changed_expected_outputs(self):
        for group in self.data['groups'].values():
            c=group['cases'][0]; original=c['expected']; c['expected']={'invented':'runtime output'}
            with self.subTest(id=c['id']),self.assertRaisesRegex(ValueError,'Fixture mismatch'):
                self.validate()
            c['expected']=original

    def test_missing_matrix_member_rehashed_in_every_family(self):
        for name,g in self.data['groups'].items():
            c=g['cases'].pop()
            with self.subTest(family=name),self.assertRaisesRegex(ValueError,'Incomplete fixture matrix'):
                self.validate()
            g['cases'].append(c)

    def test_unreviewed_source_hash_and_symbol(self):
        for field in ('sha256','bindings'):
            old=deepcopy(self.data['sources'][0][field]); self.data['sources'][0][field]='invented'
            with self.subTest(field=field),self.assertRaisesRegex(ValueError,'Unreviewed source'):
                self.validate()
            self.data['sources'][0][field]=old

    def test_prerequisite_digest_and_status(self):
        self.data['inputs']['speed-limit-handoff.json']='wrong'
        with self.assertRaisesRegex(ValueError,'Input artifact drift'): self.validate()
        self.setUp(); self.data['runtimeStatus']='gameplay-verified'
        with self.assertRaisesRegex(ValueError,'authority/status drift'): self.validate()

    def test_scenario_copy_or_link_loss(self):
        self.data['scenarioBindings']['EXAV-SCN-014']['expected']['costPerCharge']=61
        with self.assertRaisesRegex(ValueError,'Scenario binding drift'): self.validate()
        self.setUp();self.data['scenarioBindings']['EXAV-SCN-009']['cases']=[]
        with self.assertRaisesRegex(ValueError,'Scenario binding drift'): self.validate()

    def test_illegal_arrival_duplicate_car_and_disconnected_consist(self):
        c=self.case('callbacks','move-2');c['input']['cars'][0]['heading']=0
        with self.assertRaisesRegex(ValueError,'Illegal starting pose'): self.validate()
        self.setUp();c=self.case('callbacks','move-2');c['input']['cars'][1]['id']='A'
        with self.assertRaisesRegex(ValueError,'Duplicate car identity'): self.validate()
        self.setUp();c=self.case('callbacks','move-2');p=c['input']['cars'][1]['location'];p[0]-=1
        c['input']['rails'].append({'location':p,'pattern':4})
        with self.assertRaisesRegex(ValueError,'Disconnected consist'): self.validate()

    def test_own_tail_blocks_before_tail_vacates_and_both_null_keeps_one_retry(self):
        c=self.case('callbacks','blocked-own-tail');e=oracle.callback(c['input'])
        self.assertEqual(8,len(c['input']['cars']))
        self.assertEqual(c['input']['cars'],e['cars']);self.assertEqual(0,e['moveCount']);self.assertEqual(0,e['spend'])
        for id in ('both-null','both-null-after-handler-reverse'):
            c=self.case('callbacks',id);e=oracle.callback(c['input'])
            self.assertEqual('null-dereference-after-reversal',e['sourceFault'])
            self.assertEqual(1,len([q for q in e['queue'] if q['handler']=='T']))
            self.assertEqual(204,e['queue'][-1]['due']);self.assertEqual('neighbor',e['queue'][0]['handler'])

    def test_reverse_wrapper_flips_both_slope_components(self):
        c=self.case('rendering','slope-asymmetric-True-0-0-1-False')
        self.assertEqual({'offset':[0,-13],'selector':0,'frame':5,'rectangle':[160,64,32,32]},oracle.render(c['input']))
        c['expected']['frame']=1
        with self.assertRaisesRegex(ValueError,'Fixture mismatch'):self.validate()

    def test_turning_single_diagonal_can_enable_forwarding_and_preserves_timer(self):
        c=self.case('turning','turn-1-1--1'); e=oracle.turning(c['input'])
        self.assertEqual(-1000,e['result']);self.assertTrue(e['forwarding'])
        self.assertEqual(c['input']['cars'],e['afterCentral']);self.assertEqual(c['input']['queue'],e['queue'])
        c=self.case('turning','turn-2-3--1');e=oracle.turning(c['input'])
        self.assertEqual(['A','B','C'],[c['id'] for c in e['afterCentral']])
        self.assertEqual([18,20,2],e['afterCentral'][0]['location'])

    def test_occupied_first_return_does_not_search_second_connected_candidate(self):
        c=self.case('outside','return-blocked-1'); e=oracle.outside(c['input'])
        self.assertEqual(0,e['selected']);self.assertEqual(c['input']['car'],e['car'])
        self.assertEqual(0,e['moveCount']);self.assertEqual(0,e['spend'])

    def test_outside_exchange_fare_and_boarding_origin_use_the_pre_shift_head(self):
        c=self.case('outside','exchange-fare-service');e=oracle.outside(c['input'])
        self.assertEqual(1170,e['revenue']);self.assertEqual(911170,e['cash'])
        self.assertEqual(51,e['boardingSource']['countdown'])
        self.assertEqual(50,e['car']['countdown'])
        c=self.case('outside','exchange-fare-forwarding');e=oracle.outside(c['input'])
        self.assertEqual(1170,e['revenue']);self.assertIsNone(e['boardingSource'])
        self.assertEqual(0,e['passengers']);self.assertTrue(e['forwarding'])

    def test_all_evaluators_leave_inputs_unchanged(self):
        for group,g in self.data['groups'].items():
            for c in g['cases']:
                original=deepcopy(c['input']); oracle.EVALUATORS[group](c['input'])
                self.assertEqual(original,c['input'],c['id'])


if __name__=='__main__':unittest.main()
