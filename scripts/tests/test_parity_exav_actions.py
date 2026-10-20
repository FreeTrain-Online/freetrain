"""Offline regression checks for ExAV action provenance and conservative review status."""
import copy
import importlib.util
import json
from pathlib import Path
import re
import unittest

SPEC = importlib.util.spec_from_file_location('exav_actions', Path(__file__).resolve().parents[1] / 'reconcile-exav-actions.py')
actions = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(actions)


class ExavActions(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.review = json.loads(actions.OUTPUT.read_text())
        cls.ledger = json.loads((actions.inv.PARITY_ROOT / 'exav-ledger.json').read_text())
        cls.files = {r['baselinePath']: r['sha256'] for r in cls.ledger['fileInventory']}

    def test_denominators_identities_and_source_hashes_are_preserved(self):
        expected = {r['coverageId'] for r in self.ledger['uiBindings']}
        expected.update(r['surfaceId'] for r in self.ledger['uiSurfaces'])
        self.assertEqual(expected, {r['id'] for r in self.review['actions']})
        self.assertEqual(len(expected), len(self.review['actions']))
        self.assertEqual({r['coverageId'] for r in self.ledger['contributions']},
                         {r['coverageId'] for r in self.review['contributions']})
        for row in self.review['actions'] + self.review['contributions'] + self.review['translationEntries']:
            self.assertEqual(self.files[row['path']], row['sourceSha256'])
        for path, sha in self.review['inputs'].items():
            self.assertEqual(sha, actions.inv.digest((actions.inv.ROOT / path).read_bytes()), path)

    def test_candidate_matches_never_approve_historical_behavior(self):
        self.assertIn('closure pending', self.review['status'])
        for row in self.review['actions']:
            self.assertEqual('inventoried', row['implementationStatus'])
            self.assertTrue(row['candidateWorkflow'] or row['scope'] in (
                'excluded', 'source-only-reference', 'authoring-reference'))
            if row['scope'] == 'review-required':
                self.assertIn('pending', row['status'])
        for row in self.review['contributions']:
            self.assertIn('review pending', row['status'])

    def test_manual_links_use_existing_requirement_identities(self):
        text = '\n'.join((actions.inv.ROOT / 'doc' / path).read_text() for path in (
            'web-parity-manual.md', 'web-parity-online-manual.md'))
        ids = set(re.findall(r'\| (MAN-[TMWO]\d+)', text))
        for finding in self.review['findings']:
            self.assertTrue(set(finding['manualRequirements']) <= ids, finding['id'])
            for evidence in finding['evidence']:
                self.assertEqual(self.files[evidence['path']], evidence['sha256'])

    def test_comments_char_literals_and_verbatim_strings_are_not_confused(self):
        rows = list(actions.literals('''// "コメント"
/* "ignored" */ char quote = '"';
label.Text = "駅名"; // "ignored"
message = @"A ""quoted"" name";
id = "{GUID}";
'''))
        self.assertEqual(['駅名', 'A "quoted" name', '{GUID}'], [r['originalText'] for r in rows])
        self.assertEqual(3, rows[0]['line'])
        self.assertEqual('Japanese-text-candidate', rows[0]['classification'])
        self.assertIn('non-presentation', rows[-1]['classification'])

    def test_finding_evidence_drift_and_duplicates_fail_closed(self):
        class FakeTree:
            def text(self, path):
                return ('void registered() {}', 'utf-8')
        rules = dict(findings=[dict(id='A', behavior='Open tool', phases=['P9'],
                                   evidence=[dict(path='core/tool.cs', symbol='registered')])])
        actions.validate_findings(FakeTree(), rules)
        changed = copy.deepcopy(rules)
        changed['findings'][0]['evidence'][0]['symbol'] = 'removedHandler'
        with self.assertRaisesRegex(ValueError, 'Stale finding evidence'):
            actions.validate_findings(FakeTree(), changed)
        rules['findings'] *= 2
        with self.assertRaisesRegex(ValueError, 'Duplicate/incomplete'):
            actions.validate_findings(FakeTree(), rules)

    def test_translations_keep_original_identity_and_unapproved_donors(self):
        ids = [(r['path'], r['key']) for r in self.review['translationEntries']]
        self.assertEqual(len(ids), len(set(ids)))
        for row in self.review['translationEntries']:
            self.assertEqual(actions.inv.digest(row['originalText'].encode()), row['originalTextSha256'])
            self.assertIsNone(row['proposedEnglish'])
            self.assertIn('review pending', row['reviewStatus'])
            if row['englishDonor']:
                self.assertEqual(row['key'], row['englishDonor']['key'])
                self.assertTrue(row['englishDonor']['sourceSha256'])


if __name__ == '__main__':
    unittest.main()
