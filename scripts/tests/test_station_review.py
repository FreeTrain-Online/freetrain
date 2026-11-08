"""P6-01 evidence integrity tests; these execute no station simulation."""
import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('station_review', ROOT / 'scripts/review-stations.py')
review = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(review)


class StationReview(unittest.TestCase):
    def setUp(self):
        self.contract = review.read('station-review.json')
        self.locale = review.read('station-localization-review.json')

    def validate(self, refresh=True):
        if refresh:
            for document in (self.contract, self.locale):
                document['reviewSha256'] = review.digest({k: v for k, v in document.items()
                                                        if k != 'reviewSha256'})
        return review.validate(self.contract, self.locale)

    def test_repository_evidence_without_archive(self):
        counts = self.validate(refresh=False)
        self.assertEqual(dict(sources=106, resources=102, operations=12, decisions=14,
                              contributions=99, controls=78, acceptance=26, tables=42,
                              providerBindings=169, sourceStrings=215, catalogStrings=90), counts)

    def test_digest_and_source_authority(self):
        self.contract['operations'][0]['semantics'] = 'unreviewed'
        with self.assertRaisesRegex(ValueError, 'Review digest drift'):
            self.validate(refresh=False)
        self.contract['sourceReviews'][0]['sha256'] = 'wrong-source'
        with self.assertRaisesRegex(ValueError, 'Source hash drift'):
            self.validate()

    def test_prerequisite_and_dependency_closure(self):
        self.contract['inputs'].pop()
        with self.assertRaisesRegex(ValueError, 'Missing prerequisite'):
            self.validate()
        self.setUp()
        self.contract['dependencies'].pop()
        with self.assertRaisesRegex(ValueError, 'Dependency closure drift'):
            self.validate()

    def test_operation_controls_and_reciprocal_decisions(self):
        self.contract['operations'][0]['controls'] = []
        with self.assertRaisesRegex(ValueError, 'Missing operation'):
            self.validate()
        self.setUp()
        self.contract['operations'][0]['requirements'].pop()
        with self.assertRaisesRegex(ValueError, 'Missing reciprocal MAN/DEV/action link'):
            self.validate()
        self.setUp()
        self.contract['decisions'][0]['operations'] = ['station-building']
        with self.assertRaisesRegex(ValueError, 'Nonreciprocal decision'):
            self.validate()

    def test_source_quirks_and_portable_policies_cannot_change_silently(self):
        for key in review.POLICIES:
            with self.subTest(key=key):
                self.setUp()
                self.contract['policies'][key] = 'unreviewed-replacement'
                with self.assertRaisesRegex(ValueError, 'Portable policy drift'):
                    self.validate()

    def test_contribution_boundaries_and_metadata(self):
        for kind in ('station', 'trainDepartureBell', 'railStationary', 'bgm', 'dockingContent'):
            with self.subTest(kind=kind):
                self.setUp()
                row = next(r for r in self.contract['contributions'] if r['type'] == kind)
                self.contract['contributions'].remove(row)
                with self.assertRaisesRegex(ValueError, 'Contribution coverage drift'):
                    self.validate()
        self.setUp()
        row = next(r for r in self.contract['contributions'] if r['type'] == 'railStationary')
        row['disposition'] = 'P6-runtime-unimplemented'
        with self.assertRaisesRegex(ValueError, 'Contribution disposition drift'):
            self.validate()
        self.setUp()
        row = next(r for r in self.contract['contributions'] if r['type'] == 'station')
        row['fields']['price'] = '1'
        with self.assertRaisesRegex(ValueError, 'Declaration metadata drift'):
            self.validate()

    def test_provider_combinations_and_table_values(self):
        self.contract['providerBindings'].pop()
        with self.assertRaisesRegex(ValueError, 'Provider binding coverage drift'):
            self.validate()
        self.setUp()
        self.contract['populationTables'][0]['values'][0] += 1
        with self.assertRaisesRegex(ValueError, 'Population table value drift'):
            self.validate()
        self.setUp()
        self.contract['populationTables'].pop()
        with self.assertRaisesRegex(ValueError, 'Population table scope drift'):
            self.validate()

    def test_resource_and_action_coverage(self):
        self.contract['resources'].pop()
        with self.assertRaisesRegex(ValueError, 'Resource coverage drift'):
            self.validate()
        self.setUp()
        self.contract['resourceEdges'].pop()
        with self.assertRaisesRegex(ValueError, 'Resource edge drift'):
            self.validate()
        self.setUp()
        self.contract['actions'].pop()
        with self.assertRaisesRegex(ValueError, 'Control/action inventory drift'):
            self.validate()

    def test_acceptance_and_scenario_scope(self):
        self.contract['acceptanceMatrix'].pop()
        with self.assertRaisesRegex(ValueError, 'Acceptance matrix scope drift'):
            self.validate()
        self.setUp()
        self.contract['acceptanceMatrix'][0]['status'] = 'passed'
        with self.assertRaisesRegex(ValueError, 'Overclaimed/unbound acceptance'):
            self.validate()
        self.setUp()
        self.contract['scenarioBindings'][0]['expectedSha256'] = 'invented'
        with self.assertRaisesRegex(ValueError, 'Scenario expectation drift'):
            self.validate()

    def test_source_text_and_donor_provenance(self):
        self.locale['sourceStrings'].pop()
        with self.assertRaisesRegex(ValueError, 'Source text scope drift'):
            self.validate()
        self.setUp()
        row = next(r for r in self.locale['sourceStrings'] if r['englishDonor'])
        row['englishDonor']['originalText'] = 'invented translation'
        with self.assertRaisesRegex(ValueError, 'Text/donor provenance drift'):
            self.validate()
        self.setUp()
        self.locale['sourcePaths'].pop()
        with self.assertRaisesRegex(ValueError, 'Localization source scope drift'):
            self.validate()

    def test_catalog_and_unshipped_text_status(self):
        self.locale['catalogStrings'].pop()
        with self.assertRaisesRegex(ValueError, 'Catalog text scope drift'):
            self.validate()
        self.setUp()
        self.locale['sourceStrings'][0]['status'] = 'shipped'
        with self.assertRaisesRegex(ValueError, 'Overclaimed/unclassified text'):
            self.validate()

    def test_named_open_art_followup_and_no_gameplay_credit(self):
        self.contract['followUps'][0]['owner'] = ''
        with self.assertRaisesRegex(ValueError, 'Unnamed/lost follow-up'):
            self.validate()
        self.setUp()
        self.contract['runtimeStatus'] = 'verified'
        with self.assertRaisesRegex(ValueError, 'Overclaimed specification'):
            self.validate()


if __name__ == '__main__':
    unittest.main()
