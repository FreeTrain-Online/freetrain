"""Independent content accounting and scanner boundaries for the P0-02 review."""
import importlib.util
import copy
import json
from pathlib import Path
import struct
import unittest

SPEC = importlib.util.spec_from_file_location('content', Path(__file__).resolve().parents[1] / 'reconcile-parity-content.py')
content = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(content)


class SourceBoundaries(unittest.TestCase):
    def test_nested_state_excludes_method_locals_and_retains_storage(self):
        class Tree:
            entries = {'plugins/example/State.cs': None}

            def text(self, path):
                return '''namespace Demo {
                  // class Imaginary { }
                  [Serializable] public class State : Base {
                    private int saved;
                    [NonSerialized] private string listener;
                    private static int shared;
                    private int[] values = new int[] {1,2};
                    public int Value { get { int local = 2; return local; } }
                    public void Work() { int temporary = 1; }
                    [Serializable] internal class Child { private string id; }
                  }
                }''', 'utf-8'

            def read(self, path):
                return self.text(path)[0].encode()

        outer, child = content.source_types(Tree())
        self.assertEqual(('Demo.State', 'Demo.State+Child'), (outer['type'], child['type']))
        self.assertTrue(outer['declaredSerializable'])
        self.assertTrue(child['declaredSerializable'])
        self.assertEqual(['instance', 'nonserialized', 'static', 'instance'], [f['storage'] for f in outer['fields']])
        self.assertEqual(['private string id;'], [f['declaration'] for f in child['fields']])
        self.assertNotIn('temporary', str(outer['fields']))
        self.assertNotIn('local', str(outer['fields']))

    def test_source_mapping_requires_type_and_project_compile_membership(self):
        types = [dict(type='Demo.Widget', path='plugins/a/Widget.cs')]
        projects = [dict(path='plugins/a/a.csproj', assemblies=['Actual'], compile=[dict(paths=['plugins/a/Widget.cs'])])]
        good = content.resolve_type(dict(name='Demo.Widget', codebase='bin/Debug/Actual.DLL'), types, projects)
        self.assertEqual('source-and-project', good['resolution'])
        for attributes in (dict(name='Wrong.Widget', codebase='Actual.dll'), dict(name='Demo.Widget', codebase='Other.dll')):
            self.assertEqual('baseline-type-or-build-defect', content.resolve_type(attributes, types, projects)['resolution'])

    def test_bmp_storage_orientation_and_palette_depth_are_not_assumed(self):
        data = bytearray(54)
        data[:2] = b'BM'
        struct.pack_into('<IiiHHI', data, 14, 40, 256, -64, 1, 4, 0)
        self.assertEqual(dict(width=256, height=64, topDown=True, bitsPerPixel=4, compression=0, planes=1), content.bmp_metadata(data))
        with self.assertRaises(ValueError):
            content.bmp_metadata(b'not a bitmap')

    def test_unknown_direction_token_does_not_become_default(self):
        root, _ = content.inv.manifest(b'<plugin><sprite><direction front="BOGUS"/></sprite><sprite><direction front="NORTH"/></sprite></plugin>')
        self.assertEqual([1, 2, 2, 2, 2, 2, 2, 2], content.inv.direction_aliases(list(root)))
        root, _ = content.inv.manifest(b'<plugin><sprite><direction front=""/></sprite></plugin>')
        self.assertEqual([1] * 8, content.inv.direction_aliases(list(root)))


class ReconciledContent(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.review = json.loads(content.OUTPUT.read_text())
        cls.base = json.loads((content.inv.OUT / 'base-ledger.json').read_text())

    def test_complete_manifest_declaration_and_asset_denominators(self):
        for group in ('manifests', 'recipes', 'assets'):
            original = {'manifests': 'plugins', 'recipes': 'contributions', 'assets': 'assets'}[group]
            expected = {r['coverageId'] for r in self.base[original] if not r.get('generatedFrom')}
            rows = self.review[group]
            self.assertEqual(expected, {r['coverageId'] for r in rows})
            self.assertEqual(len(expected), len(rows))
        self.assertEqual(54, len(self.review['manifests']))
        self.assertEqual(393, len(self.review['recipes']))
        self.assertEqual(224, len(self.review['assets']))
        self.assertEqual(3, sum(m['scope'] == 'excluded' for m in self.review['manifests']))

    def test_all_legacy_dll_edges_have_exact_source_and_project_evidence(self):
        edges = self.review['manifestFileEdges']
        self.assertEqual(76, sum(e['disposition'] == 'legacy-build-output' for e in edges))
        for e in edges:
            if e['resolution'] == 'missing':
                self.assertEqual('legacy-build-output', e['disposition'])
                self.assertTrue(e['sourceBindings'])
                for b in e['sourceBindings']:
                    self.assertEqual('source-and-project', b['resolution'])
                    self.assertTrue(b['sources'])
                    self.assertTrue(b['projects'])

    def test_aliases_point_to_real_children_and_reverse_cars(self):
        ids = {r['contributionId'] for r in self.base['contributions'] if r['contributionType'] != 'contribution'}
        aliases = self.review['aliases']
        self.assertEqual(616, sum(not a['registeredAlias'] for a in aliases))
        self.assertEqual(2, sum(a['registeredAlias'] for a in aliases))
        self.assertTrue(all(a['target'] in ids for a in aliases))
        children = {c for r in self.review['recipes'] for c in r['generatedChildren']}
        self.assertEqual(116, len(children))

    def test_bgm_text_stock_config_and_code_loaded_art_have_graph_edges(self):
        edges = self.review['indirectResourceEdges']
        targets = {p for e in edges for p in e['targets']}
        for path in ('plugins/org.kohsuke.freetrain.music/map.mid',
                     'plugins/jp.co.tripod.chiname.financial.stockmarket/companies.xml',
                     'plugins/jp.co.tripod.chiname.lib.generic/CategoryTree.xml',
                     'plugins/org.kohsuke.freetrain.rail/BridgeRail.bmp',
                     'plugins/org.kohsuke.freetrain.rail.garage/garage.bmp',
                     'plugins/org.kohsuke.freetrain.terrain.terrace/Terrace.bmp',
                     'core/res/train.wav'):
            self.assertIn(path, targets)
        self.assertEqual(14, len(self.review['embeddedResources']))

    def test_excluded_a4_children_are_not_global_or_bundled(self):
        children = self.review['excludedLocalMusicChildren']
        self.assertEqual({'BLS.mid', 'FLK.mid', 'FNK.mid', 'FOG.mid', 'OLD.mid', 'RGG.mid', 'WHT.mid', 'OP_RIN.mid', 'OP_SNW.mid'}, {c['filename'] for c in children})
        self.assertTrue(all(c['scope'] == 'excluded' and not c['registeredInGlobalCatalog'] for c in children))
        assets = {a['baselinePath'] for a in self.review['assets']}
        self.assertFalse(any('a4mem/' in a for a in assets))

    def test_picture_references_resolve_in_picture_registry(self):
        picture_ids = {p['pictureId'] for p in self.review['pictureRegistrations']}
        self.assertEqual(148, len(picture_ids))
        self.assertEqual(260, len(self.review['pictureReferences']))
        self.assertTrue(all(p['target'] in picture_ids for p in self.review['pictureReferences']))
        self.assertIn('{2A333E1B-7AA0-4A96-A054-DAEEEB84F953}', picture_ids)
        for edge in self.review['sourceReferenceEdges']:
            if edge['registry'] == 'picture':
                self.assertIn(edge['target'], picture_ids)

    def test_unreviewed_diagnostics_and_stale_dispositions_fail_generation(self):
        tree = content.inv.Tree(content.inv.ROOT, self.base['revision'])
        rules = json.loads((content.inv.OUT / 'content-rules.json').read_text())
        changed = copy.deepcopy(rules)
        changed['expectedResourceDiagnostics'] = []
        with self.assertRaisesRegex(ValueError, 'Resource diagnostics differ'):
            content.reconcile(tree, self.base, changed)
        changed = copy.deepcopy(rules)
        changed['unreferencedAssets']['core/res/Arrows.bmp'] = 'unused'
        with self.assertRaisesRegex(ValueError, 'Stale unreferenced-asset'):
            content.reconcile(tree, self.base, changed)

    def test_every_asset_has_hash_notice_decode_and_reachability_disposition(self):
        for a in self.review['assets']:
            self.assertEqual(64, len(a['sha256']))
            self.assertTrue(a['notices'])
            self.assertTrue(a['decodingRule'])
            self.assertTrue(a['cropAnchorRule'])
            self.assertNotEqual('UNREVIEWED', a['reachability'])
        self.assertEqual(209, sum(bool(a['incomingOwners']) for a in self.review['assets']))
        self.assertEqual([dict(owner='plugins/org.kohsuke.freetrain.tc.tat/FreeTrain.TATTrainController.csproj', reference='res\\Straight.bmp', resolution='project-content-missing')], self.review['resourceDiagnostics'])

    def test_contribution_reference_and_instance_state_stay_distinct(self):
        types = {t['type']: t for t in self.review['sourceTypes']}
        half = types['freetrain.world.structs.hv.ContributionReference']
        self.assertEqual(6, len(half['fields']))
        self.assertTrue(all(f['storage'] == 'instance' for f in half['fields']))
        bank = types['freetrain.views.bank.BankModule']
        self.assertIn('nonserialized', {f['storage'] for f in bank['fields']})
        rules = {r['id']: r for r in self.review['rules']['stateRules']}
        self.assertIn('fileName', rules['temporary-bgm']['behavior'])
        self.assertIn('only ID', rules['color-test']['behavior'])


if __name__ == '__main__':
    unittest.main()
