"""Offline optional-pack evidence and bounded XML/profile checks; no sibling checkout."""
import collections
import importlib.util
import json
from pathlib import Path
import unittest

SPEC = importlib.util.spec_from_file_location('exav_optional', Path(__file__).resolve().parents[1] / 'review-exav-optional.py')
reviewer = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(reviewer)


class MemoryTree:
    def __init__(self, files):
        self.files = files

    def read(self, path):
        return self.files[path]

    def text(self, path):
        return self.read(path).decode(), 'utf-8'


class ExavOptional(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.review = json.loads(reviewer.OUTPUT.read_text())
        cls.rules = json.loads(reviewer.RULES.read_text())

    def test_evidence_pins_hashes_and_denominators(self):
        r = self.review
        self.assertEqual(self.rules['source'], r['source'])
        self.assertEqual(self.rules['expectedCounts'], r['counts'])
        self.assertEqual(reviewer.inv.digest(reviewer.RULES.read_bytes()), r['rulesSha256'])
        self.assertEqual(reviewer.inv.digest(reviewer.inv.dump(r['files'])), r['fileSetSha256'])
        self.assertEqual(len(r['plugins']), r['counts']['manifests'])
        self.assertEqual(len(r['recipes']), r['counts']['directDeclarations'])
        for path, expected in r['generatorSources'].items():
            self.assertEqual(expected, reviewer.inv.digest((reviewer.inv.ROOT / path).read_bytes()))
        for field, path in [('bundledReviewSha256', 'exav-content-review.json'),
                            ('saveSampleReviewSha256', 'exav-save-samples.json'),
                            ('historicalLedgerSha256', 'historical/extra-suite-ledger.json')]:
            self.assertEqual(r[field], reviewer.inv.digest((reviewer.inv.PARITY_ROOT / path).read_bytes()))
        bundled = json.loads((reviewer.inv.PARITY_ROOT / 'exav-ledger.json').read_text())
        sources = {r['baselinePath']: r['sha256'] for r in bundled['fileInventory']}
        for row in r['adapterSources'] + r['exavSourceEvidence']:
            self.assertEqual(sources[row['path'] if row['path'].startswith('trunk/') else 'trunk/' + row['path']], row['sha256'])

    def test_source_inventory_and_selected_profile_keep_different_counts(self):
        r = self.review
        self.assertEqual(452, len(r['plugins']))
        selected = set(r['manifestSelection']['selected'])
        self.assertEqual(450, len(selected))
        self.assertEqual(2, len(r['manifestSelection']['sourceOnly']))
        active_ids = [row['id'] for row in r['recipes'] if row['path'] in selected]
        self.assertEqual(11470, len(active_ids))
        self.assertEqual(len(active_ids), len(set(active_ids)))
        self.assertEqual(158, len(r['collisions']))
        self.assertTrue(all(sum(owner['path'] in selected for owner in row['owners']) == 1 for row in r['collisions']))
        self.assertTrue(all(not row['bundledOwners'] for row in r['collisions']))

    def test_dependencies_resources_and_source_gaps_stay_explicit(self):
        r = self.review
        for plugin in r['plugins']:
            self.assertIn('system', plugin['dependencyClosure'])
            self.assertTrue(all(d['resolution'] != 'missing' for d in plugin['dependencies']))
        missing = [e for e in r['fileEdges'] if e['resolution'] == 'missing']
        self.assertEqual(collections.Counter(codebase=773, src=27), collections.Counter(e['attribute'] for e in missing))
        self.assertEqual(36, sum(e['resolution'] == 'case-folded' for e in r['fileEdges']))
        self.assertTrue(all(e['disposition'].startswith('legacy-build-output') for e in missing if e['attribute'] == 'codebase'))
        self.assertTrue(all(e['resolution'] == e['normalizedResolution']['resolution'] for e in r['fileEdges']))
        unresolved = [t['name'] for t in r['resolvedTypes'] if t['resolution'] != 'source-and-project']
        self.assertEqual(['freetrain.tools.capture.MenuContributionImpl'], unresolved)
        self.assertEqual(2, len(r['binaryCode']))
        self.assertEqual(31, len(r['notices']))
        self.assertEqual(10, sum(bool(n['review']) for n in r['notices']))
        files = {f['path']: f['sha256'] for f in r['files']}
        for notice in r['notices']:
            self.assertEqual(files[notice['path']], notice['sha256'])

    def test_normalization_and_translation_do_not_claim_runtime_or_wording(self):
        r = self.review
        self.assertEqual(4933, r['normalizedSourceReview']['filesCompared'])
        self.assertEqual(507, len(r['normalizedSourceReview']['changes']))
        self.assertEqual(2, sum(c['pathChanged'] for c in r['normalizedSourceReview']['changes']))
        self.assertIsNone(r['translationPolicy']['englishDonor'])
        self.assertIsNone(r['translationPolicy']['proposedEnglish'])
        keys = set()
        for row in r['translationCandidates']:
            key = (row['path'], row['ordinal'], row['key'])
            self.assertNotIn(key, keys)
            keys.add(key)
            self.assertEqual(row['textSha256'], reviewer.inv.digest(row['text'].encode()))
        self.assertEqual(82, len(r['compatibility']['legacyOnlyExplicitIds']))
        self.assertEqual([dict(ids=150, optional=138, bundled=11, unmatched=1),
                          dict(ids=7, optional=0, bundled=7, unmatched=0)],
                         [s['counts'] for s in r['saveReferenceBindings']])

    def test_approved_local_fragment_expands_only_from_pinned_bytes(self):
        path = 'plugins/1703d.keisei.AE/plugin.xml'
        tree = MemoryTree({path: b'<!DOCTYPE plugin [<!ENTITY local SYSTEM "AE/AE.xml">]><plugin>&local;</plugin>',
                           'plugins/1703d.keisei.AE/AE/AE.xml': b'<contribution type="train" id="A"><fare>10</fare></contribution>'})
        root, includes, _ = reviewer.parse_manifest(tree, path)
        self.assertEqual('10', root.findtext('contribution/fare'))
        self.assertEqual('plugins/1703d.keisei.AE/AE/AE.xml', includes[0]['path'])

    def test_unreviewed_external_and_nested_entities_are_rejected(self):
        path = 'plugins/1703d.keisei.AE/plugin.xml'
        for relative in ('https://example.com/a.xml', '/etc/passwd', '../../outside.xml', 'unknown.xml'):
            with self.subTest(relative=relative), self.assertRaises(ValueError):
                reviewer.parse_manifest(MemoryTree({path: ('<!DOCTYPE plugin [<!ENTITY local SYSTEM "' + relative + '">]><plugin>&local;</plugin>').encode()}), path)
        tree = MemoryTree({path: b'<!DOCTYPE plugin [<!ENTITY local SYSTEM "AE/AE.xml">]><plugin>&local;</plugin>',
                           'plugins/1703d.keisei.AE/AE/AE.xml': b'<!DOCTYPE nested><contribution/>'})
        with self.assertRaises(ValueError):
            reviewer.parse_manifest(tree, path)

    def test_dependency_cycles_fail_and_diamond_dependencies_close(self):
        self.assertEqual(['B', 'C', 'system'], reviewer.dependency_closure('A', {
            'A': ['B', 'C'], 'B': ['system'], 'C': ['system'], 'system': []}))
        with self.assertRaisesRegex(ValueError, 'Cyclic'):
            reviewer.dependency_closure('A', {'A': ['B'], 'B': ['A']})


if __name__ == '__main__':
    unittest.main()
