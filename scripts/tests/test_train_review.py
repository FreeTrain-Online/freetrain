"""Evidence corruption tests for P5-01; no movement/controller implementation."""
import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('train_review', ROOT / 'scripts/review-trains.py')
review = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(review)


class TrainReview(unittest.TestCase):
    def setUp(self):
        self.contract = review.read('train-review.json')
        self.locale = review.read('train-localization-review.json')

    def validate(self, refresh=True):
        # Rehash deliberate mutations to exercise semantic guards beyond digests.
        if refresh:
            for document in (self.contract, self.locale):
                document['reviewSha256'] = review.digest({k: v for k, v in document.items()
                                                        if k != 'reviewSha256'})
        return review.validate(self.contract, self.locale)

    def test_repository_review_without_external_archive(self):
        self.assertEqual((70, 18, 18, 8, 34, 638, 55), self.validate(refresh=False))

    def test_digest_and_source_authority_drift(self):
        self.contract['operations'][0]['semantics'] = 'unreviewed replacement'
        with self.assertRaisesRegex(ValueError, 'Review digest drift'):
            self.validate(refresh=False)
        self.contract['sourceReviews'][0]['sha256'] = 'wrong-source'
        with self.assertRaisesRegex(ValueError, 'Source hash drift'):
            self.validate()

    def test_prerequisites_and_operation_controls_are_required(self):
        self.contract['inputs'].pop()
        with self.assertRaisesRegex(ValueError, 'Missing prerequisite evidence'):
            self.validate()
        self.setUp()
        self.contract['operations'][0]['controls'] = []
        with self.assertRaisesRegex(ValueError, 'Missing/overclaimed operation contract'):
            self.validate()

    def test_dependency_and_decision_reciprocity(self):
        self.contract['dependencies'].pop()
        with self.assertRaisesRegex(ValueError, 'Dependency closure drift'):
            self.validate()
        self.setUp()
        self.contract['decisions'][0]['operations'] = ['trade']
        with self.assertRaisesRegex(ValueError, 'Nonreciprocal decision'):
            self.validate()

    def test_signal_ownership_and_both_null_adapter_cannot_change_silently(self):
        for field, replacement in [('signalDispatch', 'per-signal-controller'),
                                   ('bothNull', 'dereference-null'),
                                   ('dayEditor', 'copy-month-to-day'),
                                   ('arithmetic', 'widen-and-wrap'),
                                   ('rules', 'sort-execution-order')]:
            with self.subTest(field=field):
                self.setUp()
                self.contract['policies'][field] = replacement
                with self.assertRaisesRegex(ValueError, 'Portable policy drift'):
                    self.validate()

    def test_support_car_and_registered_provider_scope_are_required(self):
        for kind in ('trainCar', 'trainController'):
            with self.subTest(kind=kind):
                self.setUp()
                row = next(r for r in self.contract['contributions'] if r['type'] == kind)
                self.contract['contributions'].remove(row)
                with self.assertRaisesRegex(ValueError, 'Contribution coverage drift'):
                    self.validate()

    def test_text_and_catalog_completeness_and_donor_provenance(self):
        self.locale['sourceStrings'].pop()
        with self.assertRaisesRegex(ValueError, 'Source text scope drift'):
            self.validate()
        self.setUp()
        row = next(r for r in self.locale['sourceStrings'] if r['englishDonor'])
        row['englishDonor']['originalText'] = 'invented donor wording'
        with self.assertRaisesRegex(ValueError, 'Text/donor provenance drift'):
            self.validate()
        self.setUp()
        self.locale['catalogStrings'].pop()
        with self.assertRaisesRegex(ValueError, 'Catalog text scope drift'):
            self.validate()

    def test_localization_paths_cannot_be_shrunk_to_hide_missing_text(self):
        path = self.locale['sourcePaths'].pop()
        self.locale['sourceStrings'] = [r for r in self.locale['sourceStrings'] if r['source'] != path]
        with self.assertRaisesRegex(ValueError, 'Localization source scope drift'):
            self.validate()

    def test_named_followups_and_source_only_acceptance(self):
        self.contract['followUps'][0]['owner'] = ''
        with self.assertRaisesRegex(ValueError, 'Unnamed/lost follow-up'):
            self.validate()
        self.setUp()
        self.contract['runtimeStatus'] = 'verified'
        with self.assertRaisesRegex(ValueError, 'Overclaimed review'):
            self.validate()
        self.setUp()
        self.locale['sourceStrings'][0]['status'] = 'shipped'
        with self.assertRaisesRegex(ValueError, 'Unclassified/overclaimed text'):
            self.validate()


if __name__ == '__main__':
    unittest.main()
