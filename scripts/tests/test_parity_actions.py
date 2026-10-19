"""Action coverage must fail closed rather than silently defaulting new UI rows."""
import copy
import importlib.util
import json
from pathlib import Path
import unittest

SPEC = importlib.util.spec_from_file_location('inventory_actions_test', Path(__file__).resolve().parents[1] / 'inventory-parity.py')
inv = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(inv)


class ActionCoverage(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.base = json.loads((inv.OUT / 'base-ledger.json').read_text())
        cls.review = json.loads((inv.OUT / 'action-map.json').read_text())
        cls.tree = inv.Tree(inv.ROOT, cls.base['revision'])
        cls.registry = (inv.ROOT / 'web/src/lib/game-ui.ts').read_text()

    def apply(self, review=None, base=None, tree=None, registry=None):
        return inv.action_review.apply_reviews(
            tree or self.tree, copy.deepcopy(base or self.base),
            review or self.review, inv.strip_comments, inv.digest,
            self.registry if registry is None else registry)

    def test_reviewed_bindings_do_not_become_implemented_features(self):
        result = self.apply()
        self.assertEqual(248, result['counts']['bindings'])
        self.assertEqual(509, result['counts']['contributions'])
        self.assertEqual(45, result['counts']['manualRequirements'])
        self.assertEqual({'inventoried'}, {row['status'] for row in self.base['uiBindings']})
        self.assertTrue(all(row['mappingStatus'] != 'dynamic-review' for row in self.base['uiBindings']))

    def test_omitted_binding_review_is_rejected(self):
        review = copy.deepcopy(self.review)
        review['bindings'].pop()
        with self.assertRaisesRegex(ValueError, 'Binding review coverage'):
            self.apply(review)

    def test_omitted_or_duplicate_surface_is_rejected(self):
        review = copy.deepcopy(self.review)
        review['surfaces'].pop()
        with self.assertRaisesRegex(ValueError, 'Source surface coverage'):
            self.apply(review)
        review = copy.deepcopy(self.review)
        review['surfaces'].append(review['surfaces'][0])
        with self.assertRaisesRegex(ValueError, 'Duplicate surface decision'):
            self.apply(review)

    def test_new_field_without_event_is_not_silently_covered(self):
        original = self.tree

        class WithNewField:
            entries = original.entries
            revision = original.revision

            def read(self, path):
                return original.read(path)

            def text(self, path):
                text, encoding = original.text(path)
                if path == 'core/framework/ConfigDialog.cs':
                    text += '\nthis.newOption = new System.Windows.Forms.CheckBox();\n'
                return text, encoding

        with self.assertRaisesRegex(ValueError, 'Source surface coverage'):
            self.apply(tree=WithNewField())

    def test_new_contribution_cannot_bypass_dynamic_routes(self):
        base = copy.deepcopy(self.base)
        row = copy.deepcopy(base['contributions'][0])
        row['coverageId'] += ':new'
        base['contributions'].append(row)
        with self.assertRaisesRegex(ValueError, 'Dynamic contribution coverage'):
            self.apply(base=base)

    def test_unknown_operation_and_changed_registry_are_rejected(self):
        review = copy.deepcopy(self.review)
        review['workflows']['rail']['operations'].append('invented-operation')
        with self.assertRaisesRegex(ValueError, 'Unknown delivered operation'):
            self.apply(review)
        with self.assertRaisesRegex(ValueError, 'Delivered registry changed'):
            self.apply(registry=self.registry + '\n// changed\n')

    def test_stale_source_hash_and_symbol_are_rejected(self):
        review = copy.deepcopy(self.review)
        review['sourceReviews']['core/framework/ConfigDialog.cs']['sha256'] = '0' * 64
        with self.assertRaisesRegex(ValueError, 'Source review hash drift'):
            self.apply(review)
        review = copy.deepcopy(self.review)
        review['workflows']['rail']['sourceEvidence'][0]['symbol'] = 'invented_method'
        with self.assertRaisesRegex(ValueError, 'Stale source evidence'):
            self.apply(review)

    def test_manual_and_root_omissions_are_rejected(self):
        review = copy.deepcopy(self.review)
        del review['manualRequirements']['MAN-O08']
        with self.assertRaisesRegex(ValueError, 'Manual requirement coverage'):
            self.apply(review)
        review = copy.deepcopy(self.review)
        del review['rootDispositions']['lib']
        with self.assertRaisesRegex(ValueError, 'Baseline root review'):
            self.apply(review)
        review = copy.deepcopy(self.review)
        review['releaseActionLinks'].pop()
        with self.assertRaisesRegex(ValueError, '26 post-manual release'):
            self.apply(review)

    def test_fields_context_and_gestures_extend_old_event_scanner(self):
        surfaces = self.apply()['sourceSurfaces']
        self.assertTrue(any(row.get('control') == 'checkTurn1' and row['workflow'] == 'tat-station' for row in surfaces))
        self.assertTrue(any('onItemDropped' in row['expression'] and row['workflow'] == 'train-inventory' for row in surfaces))
        self.assertTrue(any('OnMouseWheel' in row['expression'] and row['workflow'] == 'map' for row in surfaces))
        placeholders = [r for r in surfaces if r.get('control') == 'selSize']
        self.assertEqual(['source-only'], [r['mappingStatus'] for r in placeholders])

    def test_source_gaps_and_absent_road_variants_remain_explicit(self):
        workflows = self.review['workflows']
        self.assertEqual('source-only', workflows['delegation']['status'])
        self.assertEqual('source-only', workflows['development-tuning']['status'])
        self.assertEqual('source-only', workflows['balance-sheet']['status'])
        result = self.apply()
        roads = [r for r in result['dynamicContributions'] if r['contributionType'] == 'road']
        self.assertEqual(3, len(roads))
        self.assertEqual({'roads'}, {r['workflow'] for r in roads})
        excluded = [r for r in result['dynamicContributions'] if r['scope'] == 'excluded']
        self.assertTrue(excluded)
        self.assertEqual({'excluded'}, {r['mappingStatus'] for r in excluded})


if __name__ == '__main__':
    unittest.main()
