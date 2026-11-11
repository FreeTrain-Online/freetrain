"""Delivery closure must reject silent station/content/locale drift."""
import copy
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('station_delivery', Path(__file__).resolve().parents[1] / 'reconcile-station-delivery.py')
delivery = importlib.util.module_from_spec(spec)
spec.loader.exec_module(delivery)


class StationDeliveryTests(unittest.TestCase):
    def setUp(self):
        self.catalog = delivery.read('src/FreeTrain.Online.Content/bundled-v2.json')
        self.profile = delivery.read('web/src/generated/station-profile.json')
        self.en = delivery.read('web/src/locales/en/app.json')
        self.ja = delivery.read('web/src/locales/ja/app.json')

    def reconcile(self):
        return delivery.reconcile(self.catalog, self.profile, self.en, self.ja)

    def test_current_evidence(self):
        self.assertEqual(self.reconcile(), delivery.read('doc/parity/station-delivery.json'))

    def test_missing_station_registration(self):
        self.profile['definitions'].pop()
        with self.assertRaisesRegex(AssertionError, 'Station registration'):
            self.reconcile()

    def test_missing_bell_registration(self):
        self.profile['bells'].pop()
        with self.assertRaisesRegex(AssertionError, 'Bell registration'):
            self.reconcile()

    def test_missing_art_or_alpha_in_each_geometry_family(self):
        for identity in ('system:thin-4', 'system:fat-1', 'system:green-passage-13', 'system:brown-stair-31'):
            with self.subTest(identity=identity):
                catalog = copy.deepcopy(self.catalog)
                recipe = next(x for x in catalog['renderRecipes'] if x['id'] == identity)
                recipe['frames'][0]['masks'].pop('winter/night')
                with self.assertRaises(AssertionError):
                    delivery.reconcile(catalog, self.profile, self.en, self.ja)

    def test_correction_provenance_is_required(self):
        recipe = next(x for x in self.catalog['renderRecipes'] if x['id'] == 'system:concrete-stair-23')
        recipe['sourceCorrection']['originalRectangle'][0] = 288
        with self.assertRaises(AssertionError):
            self.reconcile()

    def test_missing_localized_help_and_diagnosis(self):
        for key in ('passageHelp', 'stationDiagnosisForwarding', 'stationSupplyHelp'):
            with self.subTest(key=key):
                ja = dict(self.ja)
                del ja[key]
                with self.assertRaisesRegex(AssertionError, 'bilingual'):
                    delivery.reconcile(self.catalog, self.profile, self.en, ja)

    def test_no_unbound_source_actions_decisions_or_acceptance(self):
        result = self.reconcile()
        self.assertEqual((len(result['actions']), len(result['decisions']), len(result['acceptance'])), (78, 14, 26))
        for row in result['actions'] + result['acceptance']:
            self.assertIn(row['binding'], result['bindings'])
        self.assertEqual(sum(x['delivery'] == 'deferred-P2-audio' for x in result['contributions']), 3)
