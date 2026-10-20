"""ExAV accounting/authority boundaries, without discovering an archive checkout."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest


def module(name, filename):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).resolve().parents[1] / filename)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


scanner = module('exav_inventory_scanner', 'inventory-parity.py')
exav = module('exav_inventory', 'parity_exav.py')


class SourceBoundaries(unittest.TestCase):
    def test_shift_jis_is_explicit_and_preserves_xml_security_boundaries(self):
        data = '<?xml version="1.0" encoding="Shift_JIS"?><plugin><title>駅と列車</title></plugin>'.encode('cp932')
        root, _ = scanner.manifest(data, allow_cp932=True)
        self.assertEqual('駅と列車', root.findtext('title'))
        with self.assertRaises((ValueError, UnicodeError)):
            scanner.manifest(data)
        bad = b'<?xml version="1.0" encoding="Shift_JIS"?><!DOCTYPE plugin SYSTEM "file:///tmp/x"><plugin/>'
        with self.assertRaisesRegex(ValueError, 'External DTD'):
            scanner.manifest(bad, allow_cp932=True)

    def test_exav_misspelled_south_token_is_not_silently_corrected(self):
        root, _ = scanner.manifest(b'<plugin><sprite><direction front="SOUTH"/></sprite><sprite/></plugin>')
        self.assertEqual(0, scanner.direction_aliases(list(root))[4])
        self.assertEqual(1, scanner.direction_aliases(list(root), 'SOURTH')[4])
        root, _ = scanner.manifest(b'<plugin><sprite><direction front="SOURTH"/></sprite><sprite/></plugin>')
        self.assertEqual(0, scanner.direction_aliases(list(root), 'SOURTH')[4])

    def test_tree_reads_only_pinned_subtree_and_ignores_dirty_files(self):
        with tempfile.TemporaryDirectory() as folder:
            repo = Path(folder)
            scanner.git(repo, 'init', '-q')
            (repo / 'trunk').mkdir()
            (repo / 'beta').mkdir()
            (repo / 'trunk/pinned.cs').write_text('pinned')
            (repo / 'beta/experiment.cs').write_text('experimental')
            scanner.git(repo, 'add', '.')
            scanner.git(repo, '-c', 'user.name=Inventory test', '-c', 'user.email=inventory@example.invalid',
                        'commit', '-qm', 'fixture')
            revision = scanner.git(repo, 'rev-parse', 'HEAD').decode().strip()
            (repo / 'trunk/pinned.cs').write_text('dirty')
            (repo / 'trunk/untracked.cs').write_text('untracked')
            tree = scanner.Tree(repo, revision, 'trunk')
            self.assertEqual({'pinned.cs'}, set(tree.entries))
            self.assertEqual(b'pinned', tree.read('pinned.cs'))
            self.assertEqual(revision, tree.revision)

    def test_project_conditions_and_missing_items_remain_explicit(self):
        class Fixture:
            entries = {'main.sln': None, 'main.csproj': None, 'present.cs': None}
            data = {
                'main.sln': 'Project("{guid}") = "Main", "main.csproj", "{id}"\nEndProject',
                'main.csproj': '<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">'
                    '<PropertyGroup><AssemblyName>Main</AssemblyName></PropertyGroup>'
                    '<ItemGroup Condition="Debug"><Compile Include="present.cs"/>'
                    '<Compile Include="absent.cs"/></ItemGroup></Project>',
            }

            def read(self, path):
                return self.data[path].encode()

            def text(self, path):
                return self.data[path], 'utf-8'

        projects, edges, solutions = exav.project_inventory(scanner, Fixture(), {'solution': 'main.sln'})
        self.assertTrue(projects[0]['listedInSelectedSolution'])
        self.assertEqual(['Debug'], edges[0]['conditions'])
        self.assertEqual(['present.cs'], edges[0]['targets'])
        self.assertEqual('missing', edges[1]['resolution'])
        self.assertEqual('exact', solutions[0]['resolution'])


class CheckedInExavInventory(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.pack = json.loads((scanner.PARITY_ROOT / 'exav-ledger.json').read_text())
        cls.config = json.loads((scanner.PARITY_ROOT / 'exav-baseline.json').read_text())
        cls.files = {row['baselinePath']: row for row in cls.pack['fileInventory']}

    def test_pin_and_generator_metadata_have_not_drifted(self):
        p, c = self.pack, self.config
        self.assertEqual(c['revision'], p['revision'])
        self.assertEqual(c['subtree'], p['source']['subtree'])
        self.assertEqual(c['sourceId'], p['sourceId'])
        self.assertEqual(scanner.digest((scanner.PARITY_ROOT / 'exav-baseline.json').read_bytes()), p['baselineConfigSha256'])
        for path, expected in p['generatorSources'].items():
            self.assertEqual(expected, scanner.digest((scanner.ROOT / path).read_bytes()), path)
        fingerprint = [[r['baselinePath'].removeprefix('trunk/'), r['mode'], r['sha256']]
                       for r in p['fileInventory']]
        self.assertEqual(scanner.digest(scanner.dump(fingerprint)), p['source']['fileSetSha256'])

    def test_full_discovery_accounting_and_separate_historical_ids(self):
        p = self.pack
        self.assertEqual((56, 482), (len(p['plugins']), sum(not r.get('generatedFrom') for r in p['contributions'])))
        for name in ('sourceFiles', 'uiBindings', 'uiSurfaces', 'serializationCandidates', 'projects'):
            self.assertEqual(p['counts'][name], len(p[name]), name)
        self.assertEqual(1041, len(self.files))
        self.assertEqual(482, len({r['contributionId'] for r in p['contributions'] if not r.get('generatedFrom')}))
        self.assertEqual([], p['collisions'])
        self.assertEqual(42, len(p['historicalComparison']['originalOnlyDirectIds']))
        self.assertEqual(131, len(p['historicalComparison']['exavOnlyDirectIds']))
        self.assertEqual({'org.kohsuke.freetrain.music.a4mem', 'org.kohsuke.freetrain.tools.dbgwnd'},
                         {r['pluginId'] for r in p['plugins'] if r['scope'] == 'excluded'})
        historical_ids = {r['coverageId'] for r in json.loads((scanner.OUT / 'base-ledger.json').read_text())['sourceFiles']}
        self.assertFalse(historical_ids & {r['coverageId'] for r in p['sourceFiles']})

    def test_records_and_dependency_targets_have_source_hashes_and_open_status(self):
        p = self.pack
        ids = set()
        for group in ('fileInventory', 'plugins', 'contributions', 'assets', 'sourceFiles', 'uiBindings', 'uiSurfaces'):
            for row in p[group]:
                self.assertNotIn(row['coverageId'], ids)
                ids.add(row['coverageId'])
                self.assertTrue(row['coverageId'].startswith(p['sourceId'] + ':'))
                self.assertIn(row['baselinePath'], self.files)
                self.assertEqual('inventoried', row['status'])
                for field in ('dependencies', 'reachability', 'scope', 'modernDestinations', 'fixtures', 'evidence', 'reviewOwners'):
                    self.assertIn(field, row)
                self.assertEqual([], row['modernDestinations'])
                if group != 'fileInventory':
                    self.assertEqual(self.files[row['baselinePath']]['sha256'], row['sourceSha256'])
                    self.assertTrue(row['translationReferences'])
                    old_path = row['historicalComparison'].get('path', '')
                    self.assertFalse(old_path.startswith('trunk/'))
        for group in ('resourceEdges', 'sourceResourceEdges', 'projectEdges', 'solutionEdges'):
            for edge in p[group]:
                for path in edge['targets']:
                    self.assertIn(path, self.files)
                    self.assertEqual(64, len(self.files[path]['sha256']))
        for row in p['serializationCandidates']:
            self.assertIn(row['coverageId'], ids)
            self.assertIn(row['path'], self.files)

    def test_build_gaps_and_source_only_files_are_retained(self):
        p = self.pack
        self.assertEqual(7, sum(e['resolution'] == 'missing' for e in p['projectEdges']))
        self.assertTrue(all(e['resolution'] == 'exact' for e in p['solutionEdges']))
        self.assertEqual(33, sum(project['listedInSelectedSolution'] for project in p['projects']))
        self.assertTrue(any(r['scope'] == 'source-only-reference' for r in p['sourceFiles']))
        recipe = p['generatedRegistrationReview']
        self.assertEqual(recipe['sha256'], self.files[recipe['path']]['sha256'])
        children = [r for r in p['contributions'] if r.get('generatedFrom')]
        self.assertEqual(['null-contrib-0:0'], [r['contributionId'] for r in children])
        sources = {r['coverageId'] for r in p['sourceFiles']}
        self.assertTrue(any(dep in sources for dep in children[0]['dependencies']))


if __name__ == '__main__':
    unittest.main()
