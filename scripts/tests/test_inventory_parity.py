"""Inventory boundary and accounting checks; run without the optional repository."""
import importlib.util
import json
import itertools
from pathlib import Path
import unittest

SPEC = importlib.util.spec_from_file_location('inventory_parity', Path(__file__).resolve().parents[1] / 'inventory-parity.py')
inventory = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(inventory)


class ManifestBoundaries(unittest.TestCase):
    def parse(self, text):
        return inventory.manifest(text.encode('utf-8'))[0]

    def test_nested_entities_both_quotes_markup_and_unicode(self):
        root = self.parse('''<!DOCTYPE plug-in [
          <!ENTITY text "駅 &amp; 列車">
          <!ENTITY child '<name>&text;</name>'>
        ]><plug-in>&child;</plug-in>''')
        self.assertEqual('駅 & 列車', root.findtext('name'))

    def test_duplicate_entity_matches_xml_first_declaration_rule(self):
        root, info = inventory.manifest(b'''<!DOCTYPE plug-in [
          <!ENTITY color 'first'><!ENTITY color 'second'>
        ]><plug-in><name>&color;</name></plug-in>''')
        self.assertEqual('first', root.findtext('name'))
        self.assertEqual(['color'], info['duplicates'])

    def test_external_and_parameter_entities_are_rejected(self):
        for text in [
            '<!DOCTYPE plug-in SYSTEM "file:///etc/passwd"><plug-in/>',
            '<!DOCTYPE plug-in [<!ENTITY x SYSTEM "file:///etc/passwd">]><plug-in>&x;</plug-in>',
            '<!DOCTYPE plug-in [<!ENTITY % x "anything">]><plug-in/>',
        ]:
            with self.subTest(text=text), self.assertRaises(ValueError):
                self.parse(text)

    def test_cycle_and_amplification_are_rejected(self):
        with self.assertRaises(ValueError):
            self.parse('<!DOCTYPE plug-in [<!ENTITY a "&b;"><!ENTITY b "&a;">]><plug-in>&a;</plug-in>')
        with self.assertRaises(ValueError):
            self.parse('<!DOCTYPE plug-in [<!ENTITY a "' + 'x' * 1024 + '"><!ENTITY b "' + '&a;' * 5000 + '">]><plug-in>&b;</plug-in>')

    def test_comments_do_not_become_contributions(self):
        root = self.parse('<plugin><!-- <contribution id="fake"/> --><contribution id="real"/></plugin>')
        self.assertEqual(['real'], [node.get('id') for node in root])

    def test_direction_aliases_use_legacy_pipe_separator_and_default_sprite(self):
        sprites = list(self.parse('<plugin><sprite><direction front="NORTH|EAST"/></sprite><sprite/></plugin>'))
        self.assertEqual([0, 1, 0, 1, 1, 1, 1, 1], inventory.direction_aliases(sprites))


class PinnedInventory(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.base = json.loads((inventory.OUT / 'base-ledger.json').read_text())

    def test_archived_and_external_exclusions_remain_in_denominator(self):
        plugins = {p['pluginId']: p for p in self.base['plugins']}
        self.assertEqual(54, len(plugins))
        self.assertEqual(set(inventory.EXCLUDED), {key for key, row in plugins.items() if row['scope'] == 'excluded'})
        self.assertEqual(['attic/desktop/plugins/org.kohsuke.freetrain.tools.vcr/plugin.xml'], plugins['org.kohsuke.freetrain.tools.vcr']['currentPaths'])

    def test_every_registered_generic_child_has_parent_and_unique_id(self):
        records = self.base['contributions']
        parents = {row['coverageId']: row for row in records}
        children = [row for row in records if row.get('generatedFrom')]
        self.assertEqual(116, len(children))
        for child in children:
            parent = parents[child['generatedFrom']]
            expected = f'{parent["contributionId"]}-{child["colorIndex"]}:{child["directionIndex"]}'
            self.assertEqual(expected, child['contributionId'])
        self.assertEqual([], self.base['collisions'])

    def test_record_keys_are_unique_and_status_does_not_imply_parity(self):
        for group in ('plugins', 'contributions', 'assets', 'sourceFiles', 'uiBindings'):
            rows = self.base[group]
            self.assertEqual(len(rows), len({row['coverageId'] for row in rows}), group)
            self.assertEqual({'inventoried'}, {row['status'] for row in rows})
            required = {'baselinePath', 'symbol', 'dependencies', 'reachability', 'scope', 'behavior',
                        'modernDestinations', 'fixtures', 'evidence', 'differences'}
            for row in rows:
                self.assertTrue(required.issubset(row), row['coverageId'])
        self.assertEqual([], self.base['historicalSaveExamples'])

    def test_reviewed_operation_ids_exist_in_delivered_registry(self):
        source = (inventory.ROOT / 'web/src/lib/game-ui.ts').read_text()
        registry = source.split('export const registry = {', 1)[1].split('} as const satisfies', 1)[0]
        # Preserve the historical ledger's operation IDs. The former controller
        # panel was removed with Manual driving; shared controller management is
        # delivered through Trains -> Controller manager instead.
        relocated = {'controller': 'fleet'}
        fleet = (inventory.ROOT / 'web/src/FleetControls.svelte').read_text()
        self.assertIn('<FleetControllerManager', fleet)
        self.assertIn('{text.controllerManager}</button>', fleet)
        for row in self.base['uiBindings']:
            for operation in row['modernOperations']:
                operation = relocated.get(operation, operation)
                self.assertRegex(registry, r"(?m)^  ['\"]?" + operation + r"['\"]?:")

    def test_baseline_sources_and_resources_are_pinned(self):
        tree = inventory.Tree(inventory.ROOT, self.base['revision'])
        for group in ('plugins', 'assets', 'sourceFiles'):
            for row in self.base[group]:
                self.assertEqual(row['sha256'], inventory.digest(tree.read(row['baselinePath'])))

    def test_rail_gallery_covers_every_legal_pair_and_serialized_pattern_id(self):
        geometry = json.loads((inventory.OUT / 'geometry.json').read_text())
        patterns = geometry['patterns']
        normal = {tuple(sorted(row['connections'])) for row in patterns if row['family'] == 'normal'}
        legal_pairs = {(a, b) for a, b in itertools.combinations(range(8), 2) if min(b-a, 8-(b-a)) >= 3}
        self.assertEqual(legal_pairs, normal)
        self.assertEqual(set(range(60)), {row['id'] for row in patterns if isinstance(row['id'], int)})
        self.assertEqual(16, sum('support' in row['family'] for row in patterns))


if __name__ == '__main__':
    unittest.main()
