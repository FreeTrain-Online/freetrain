"""Offline checks for ExAV content evidence and identity-safe translation donors."""
import importlib.util
import json
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET

SPEC = importlib.util.spec_from_file_location('exav_content', Path(__file__).resolve().parents[1] / 'reconcile-exav-content.py')
content = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(content)


class ExavContent(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.review = json.loads(content.OUTPUT.read_text())
        cls.ledger = json.loads((content.inv.PARITY_ROOT / 'exav-ledger.json').read_text())
        cls.files = {r['baselinePath']: r['sha256'] for r in cls.ledger['fileInventory']}

    def test_all_source_denominators_and_hashes_reconcile(self):
        for key, value in self.review['source'].items():
            self.assertEqual(self.ledger['source'][key], value)
        self.assertEqual(content.inv.digest(content.RULES.read_bytes()), self.review['rulesSha256'])
        for path, expected in self.review['generatorSources'].items():
            self.assertEqual(expected, content.inv.digest((content.inv.ROOT / path).read_bytes()))
        for reviewed, discovered in [('manifests', 'plugins'), ('recipes', 'contributions'), ('assets', 'assets')]:
            expected = {r['coverageId'] for r in self.ledger[discovered] if not r.get('generatedFrom')}
            rows = self.review[reviewed]
            self.assertEqual(expected, {r['coverageId'] for r in rows})
            self.assertEqual(len(expected), len(rows))
        for row in self.review['referencedFiles'] + self.review['notices']:
            self.assertEqual(self.files[row['path']], row['sha256'])
        for row in self.review['assets']:
            self.assertTrue(row['notices'])
            self.assertNotEqual('UNREVIEWED', row['reachability'])

    def test_dll_bindings_require_qualified_type_and_real_compile_items(self):
        projects = {p['path']: p for p in self.review['projects']}
        missing = [e for e in self.review['manifestFileEdges'] if e['resolution'] == 'missing']
        self.assertEqual(75, len(missing))
        for edge in missing:
            self.assertEqual('legacy-build-output', edge['disposition'])
            self.assertTrue(edge['sourceBindings'])
            for binding in edge['sourceBindings']:
                self.assertEqual('source-and-project', binding['resolution'])
                self.assertTrue(binding['sources'])
                compiled = {p for project in binding['projects'] for e in projects[project]['compile'] for p in e['paths']}
                self.assertTrue(set(binding['sources']) <= compiled)
                selected = {p for project in binding['projects'] if projects[project]['listedInSelectedSolution']
                            for e in projects[project]['compile'] for p in e['paths']}
                self.assertTrue(set(binding['sources']) <= selected)
        self.assertTrue(all(r['effectiveImplementation']['resolution'] == 'source-and-project' for r in self.review['recipes']))

    def test_registrations_and_registry_references_are_closed(self):
        ids = {r['contributionId'] for r in self.ledger['contributions'] if r['contributionType'] != 'contribution'}
        self.assertEqual(['null-contrib-0:0'], [r['contributionId'] for r in self.review['generatedChildren']])
        self.assertTrue(all(a['target'] in ids for a in self.review['aliases']))
        self.assertEqual(10, len(self.review['aliases']))
        pictures = {r['pictureId'] for r in self.review['pictureRegistrations']}
        self.assertTrue(all(r['target'] in pictures for r in self.review['pictureReferences']))
        self.assertEqual(353, len(self.review['pictureReferences']))
        self.assertEqual(4, len(self.review['registrationSites']))

    def test_exclusions_and_project_gaps_stay_visible(self):
        self.assertEqual(2, sum(m['scope'] == 'excluded' for m in self.review['manifests']))
        self.assertEqual(9, len(self.review['excludedLocalMusicChildren']))
        self.assertTrue(all(not r['registeredInGlobalCatalog'] for r in self.review['excludedLocalMusicChildren']))
        diagnostics = self.review['resourceDiagnostics']
        self.assertEqual(5, len(diagnostics))
        self.assertTrue(any('.2008.csproj' in r['owner'] and r['reference'] == 'res\\Straight.bmp' for r in diagnostics))

    def test_translation_donors_use_contribution_identity_not_manifest_ordinal(self):
        old = ET.fromstring('<plugin><contribution id="A"><name>Alpha</name></contribution><contribution id="B"><name>Beta</name></contribution></plugin>')
        new = ET.fromstring('<plugin><contribution id="B"><name>ベータ</name></contribution><contribution id="A"><name>アルファ</name></contribution></plugin>')
        a, b = content.presentation_nodes(old), content.presentation_nodes(new)
        self.assertEqual(a.keys(), b.keys())
        self.assertEqual('Beta', a['plugin/contribution:B/name[0]/text()'])
        self.assertEqual('ベータ', b['plugin/contribution:B/name[0]/text()'])
        for row in self.review['translationEntries']:
            self.assertEqual(self.files[row['path']], row['sourceSha256'])
            self.assertEqual(content.inv.digest(row['originalText'].encode()), row['originalTextSha256'])
            self.assertIsNone(row['proposedEnglish'])
            if row['englishDonor']:
                self.assertEqual(row['key'], row['englishDonor']['key'])
                self.assertFalse(row['englishDonor']['path'].startswith('trunk/'))

    def test_exav_rule_sources_do_not_retain_absent_native_paths(self):
        for group in ('rules', 'stateRules'):
            for rule in self.review['rules'][group]:
                for source in rule['sourceHashes']:
                    self.assertEqual(self.files[source['path']], source['sha256'])
                    self.assertNotIn('DirectDraw.AlphaBlend', source['path'])
        rules = {r['id']: r for r in self.review['rules']['rules']}
        self.assertIn('seatedcapacity', rules['train-recipe']['behavior'])
        self.assertIn('Int64', rules['structure-variable']['behavior'])


if __name__ == '__main__':
    unittest.main()
