"""P5-29 rejects missing gameplay bindings and source/art drift."""
import copy
import importlib.util
from pathlib import Path
import unittest

SPEC = importlib.util.spec_from_file_location('delivery', Path(__file__).resolve().parents[1] / 'reconcile-train-delivery.py')
delivery = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(delivery)


class TrainDelivery(unittest.TestCase):
    def setUp(self):
        self.catalog = delivery.read('src/FreeTrain.Online.Content/bundled-v2.json')
        self.profile = delivery.read('web/src/generated/fleet-profile.json')
        self.stocks = delivery.read('web/src/generated/stock-catalog.json')

    def check(self):
        return delivery.reconcile(self.catalog, self.profile, self.stocks)

    def test_complete_inventory_matches_checked_report(self):
        self.assertEqual(self.check(), delivery.read('doc/parity/train-delivery.json'))

    def test_missing_stock_or_purchase_entry_rejects(self):
        self.profile['cars'].pop()
        with self.assertRaisesRegex(AssertionError, 'Stock registration'):
            self.check()
        self.setUp()
        self.stocks.pop()
        with self.assertRaisesRegex(AssertionError, 'Purchase catalog'):
            self.check()

    def test_diagnostic_art_cannot_count_as_gameplay(self):
        identity = self.stocks[0]['id']
        next(x for x in self.catalog['renderRecipes'] if x['id'] == identity)['status'] = 'source-diagnostic'
        with self.assertRaises(AssertionError):
            self.check()

    def test_retired_provider_cannot_reenter_gameplay(self):
        provider = copy.deepcopy(self.profile['providers'][0])
        provider['binding']['contributionId'] = '{5567FE11-FC2C-4a1d-836F-722EF9DC957C}'
        self.profile['providers'].append(provider)
        with self.assertRaisesRegex(AssertionError, 'Provider closure'):
            self.check()
