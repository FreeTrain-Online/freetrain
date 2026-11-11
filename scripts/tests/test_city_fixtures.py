"""P7-02 oracle and evidence-integrity regressions, not city gameplay tests."""
import ast
from copy import deepcopy
import importlib.util
import json
import math
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('city_fixtures', ROOT / 'scripts/city-fixtures.py')
city = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(city)


class CityFixtureTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.fixture = json.loads(city.FIXTURES.read_text())

    def setUp(self):
        self.doc = deepcopy(self.fixture)

    def validate(self):
        # Re-sign evidence mutations to test semantics beyond the outer checksum.
        self.doc['sha256'] = city.digest({k:v for k,v in self.doc.items() if k!='sha256'})
        return city.validate(self.doc)

    def test_checked_source_fixtures(self):
        self.assertEqual(self.validate(), dict(cases=703, families=29, sources=197,
                                              randomContracts=8, runtimeAcceptance=False))

    def test_every_family_rejects_rehashed_wrong_output(self):
        for family in city.EVALUATORS:
            with self.subTest(family=family):
                self.doc = deepcopy(self.fixture)
                self.doc['groups'][family]['cases'][0]['expected']['fabricated'] = True
                with self.assertRaisesRegex(ValueError, 'Expectation mismatch'):
                    self.validate()

    def test_every_family_rejects_missing_case(self):
        for family in city.EVALUATORS:
            with self.subTest(family=family):
                self.doc = deepcopy(self.fixture)
                self.doc['groups'][family]['cases'].pop()
                with self.assertRaisesRegex(ValueError, 'matrix drift'):
                    self.validate()

    def test_input_sources_and_owners_are_reviewed_separately_from_results(self):
        for mutation in (
            lambda d: d['groups']['heat']['cases'][0]['input'].update(size=[4,4]),
            lambda d: d['groups']['heat'].update(sources=['core/world/World.cs']),
            lambda d: d['groups']['heat'].update(runtimeOwner='P8'),
            lambda d: d['groups']['heat'].update(derivation='corrected offset'),
            lambda d: d['groups']['heat']['cases'][0].update(classification='runtime-tested'),
        ):
            self.doc = deepcopy(self.fixture)
            mutation(self.doc)
            with self.assertRaisesRegex(ValueError, 'matrix drift'):
                self.validate()

    def test_scenarios_random_and_inherited_companions_are_not_editable_by_resigning(self):
        for key in ('scenarioBindings','acceptanceCompanions','randomContracts','inheritedBindings'):
            with self.subTest(key=key):
                self.doc = deepcopy(self.fixture)
                self.doc[key].pop()
                with self.assertRaisesRegex(ValueError, 'matrix drift'):
                    self.validate()

    def test_source_bytes_and_excerpts_cannot_be_rebound(self):
        self.doc['sources'][0]['bindings'][0]['text'] = 'new source'
        with self.assertRaisesRegex(ValueError, 'matrix drift'):
            self.validate()

    def test_no_gameplay_or_shipped_locale_acceptance(self):
        self.doc['runtimeStatus'] = 'city-gameplay-delivered'
        with self.assertRaisesRegex(ValueError, 'Overclaimed fixture scope'):
            self.validate()

    def test_readonly_oracle_has_no_production_or_other_fixture_imports(self):
        tree = ast.parse((ROOT/'scripts/city-fixtures.py').read_text())
        allowed = {'argparse','copy','functools','hashlib','json','math','pathlib','struct','subprocess','xml.etree.ElementTree'}
        for node in ast.walk(tree):
            if isinstance(node,ast.Import):
                self.assertTrue(all(n.name in allowed for n in node.names))
            elif isinstance(node,ast.ImportFrom):
                self.assertIn(node.module,allowed)
            elif isinstance(node,ast.Attribute):
                self.assertNotIn(node.attr,('write_text','write_bytes'))

    def test_evaluators_preserve_inputs(self):
        for family,g in self.fixture['groups'].items():
            for case in g['cases']:
                original = deepcopy(case['input'])
                city.EVALUATORS[family](case['input'])
                self.assertEqual(case['input'],original,case['id'])

    def case(self,family,id):
        return next(c for c in self.fixture['groups'][family]['cases'] if c['id']==id)

    def test_offsets_buffer_swap_and_border_are_not_normalized(self):
        border = self.case('heat','heat-border')['expected']['trace']
        self.assertEqual([t['q'][0] for t in border],[10.,0.,10.,0.])
        self.assertEqual(self.case('coordinates','hv-0')['expected']['publicIndex'],[1,1])
        odd = self.case('heat','heat-odd')['expected']
        even = self.case('heat','heat-even')['expected']
        self.assertNotEqual(odd,even)

    def test_float32_results_are_exact_and_nonlinear_weekly_has_explicit_tolerance(self):
        self.assertFalse(city.equal(1.0,math.nextafter(1.0,2.0)))
        self.assertTrue(city.equal(243.0,math.nextafter(243.0,244.0),True))
        self.assertEqual(city.f32(.03),0.029999999329447746)

    def test_native_purchase_preview_and_authoritative_receipts_are_distinct(self):
        e = self.case('trade','trade-shared')['expected']
        self.assertEqual((e['nativePreview'],e['authoritativeQuote']),(80,117))
        self.assertEqual([v for _,_,v in e['receipts']],[50,7,10,20,30])
        self.assertEqual(self.case('trade','trade-sell-current')['expected']['authoritativeQuote'],-70)

    def test_index_quirks_and_empty_arrays_are_retained(self):
        self.assertEqual(self.case('candidateIndexes','index-ordinary')['expected']['work'],[1,3,1,3])
        self.assertEqual(self.case('candidateIndexes','index-one-per-pool')['expected']['selectionDraws'],[])
        self.assertEqual(self.case('candidateIndexes','index-fixed-empty')['expected']['outcome'],'invalid-state')

    def test_fixed_loader_has_top_art_and_invisible_middle_occupancy(self):
        e = self.case('fixedSprites','fixed-loader-2x2x3')['expected']['sprites']
        self.assertEqual(sum(c['crop'] is not None for c in e),7)
        self.assertTrue(all(c['crop'] is None for c in e[4:8]))
        self.assertEqual(e[-1]['anchor'],[-16,16])
        case = self.case('footprints','foot-3--1-1-1-0')
        self.assertFalse(case['expected']['portLegal'])
        self.assertFalse(case['input']['reclaimable'][4])

    def test_constructor_order_ground_retry_and_no_walled_delay_draw(self):
        e = self.case('randomFlows','random-constructor-order')['expected']
        self.assertEqual([p['cell'] for p in e['result'][:4]],[[0,0,0],[0,1,0],[1,0,0],[1,1,0]])
        self.assertEqual([p['bound'] for p in e['random'][:5]],[4,80,80,4,80])
        trace = self.case('constructionTraces','construct-one-voxel')['expected']
        self.assertEqual([s['minute'] for s in trace['trace']],[580,620,660,700])
        self.assertEqual([d['bound'] for d in trace['random']],[80,2,80,80,80])
        self.assertEqual(self.case('constructionTraces','construct-cancel')['expected']['completionCount'],0)

    def test_forest_source_sort_does_not_swap_pattern_slots(self):
        result = self.case('randomFlows','random-forest-sort')['expected']['result']
        self.assertEqual(result,[[15,0,2],[0,8,1],[15,15,0]])

    def test_weekday_holiday_and_hour_boundary_provider_values(self):
        weekday = self.case('providers','provider-weekday-False-6-1')['expected']
        holiday = self.case('providers','provider-weekday-True-6-1')['expected']
        seven = self.case('providers','provider-weekday-False-7-1')['expected']
        self.assertEqual((weekday['outgoing'],holiday['outgoing'],seven['outgoing']),(50,25,125))
        self.assertEqual((weekday['entering'],seven['entering']),(6,12))
        variable = self.case('providers','provider-Saturday-False-7-4')['expected']
        self.assertEqual((variable['outgoing'],variable['entering']),(248,48))

    def test_nonfinite_admission_and_context_sensitive_limits(self):
        for value in ('NaN','Infinity','-Infinity'):
            e = city.settings(dict(kind='finite',value=value,registeredHours=10))
            self.assertEqual(e['reason'],'nonfinite')
        self.assertTrue(city.settings(dict(kind='amp',value=0,registeredHours=10))['portAccept'])
        self.assertEqual(city.settings(dict(kind='maxPricePower',value=1e-320,registeredHours=10))['reason'],
                         'invalid-reciprocal-power')
        self.assertEqual(city.arithmetic(dict(factors=[500000,4295])),
                         dict(sourceUncheckedInt32=-2147467296,portChecked='overflow'))

    def test_automatic_land_plan_value_and_unique_variable_height_cost(self):
        land = self.case('plans','plan-land')['expected']
        self.assertEqual((land['planValue'],land['finalPrice']),(400,1500))
        self.assertFalse(land['timed'])
        variable = self.case('plans','plan-variable-dedup')['expected']
        self.assertEqual((variable['uniqueReplacementValue'],variable['height']),(250,3))
        self.assertEqual(variable['upfrontBookings'],[])

    def test_station_empty_loading_and_zero_unloading_have_different_effects(self):
        load = self.case('stationEffects','effect-1')['expected']
        unload = self.case('stationEffects','effect-5')['expected']
        self.assertEqual(load['trainEvents'],1)
        self.assertIsNone(load['deltaQ'])
        self.assertEqual(unload['deltaQ'],0)
        self.assertTrue(unload['globalTrafficNotification'])
        self.assertEqual(unload['activeBundledTrafficSubscribers'],0)


if __name__=='__main__':
    unittest.main()
