"""P4-01 evidence corruption checks, not P4-02 geometry or runtime tests."""
import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('railway_review', ROOT / 'scripts/review-railway.py')
review = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(review)


class RailwayReview(unittest.TestCase):
    def setUp(self):
        self.contract = review.read('railway-review.json')
        self.locale = review.read('railway-localization-review.json')

    def validate(self, refresh=True):
        # Rehash intentional mutations so semantic checks, not just digests, must
        # reject corrupt source identities, incomplete scope or overstated claims.
        if refresh:
            for row in self.contract['decisions']:
                row['sha256'] = review.digest({k: v for k, v in row.items() if k != 'sha256'})
            for row in (self.contract, self.locale):
                row['reviewSha256'] = review.digest({k: v for k, v in row.items() if k != 'reviewSha256'})
        return review.validate(self.contract, self.locale)

    def test_checked_in_review_without_external_checkout(self):
        self.assertEqual((66, 14, 14, 64, 78), self.validate(refresh=False))

    def test_unreviewed_source_and_stale_decision_digest(self):
        self.contract['decisions'][0]['disposition'] = 'silently changed'
        with self.assertRaisesRegex(ValueError, 'Review digest drift'):
            self.validate(refresh=False)
        self.contract['sourceReviews'][0]['sha256'] = 'unreviewed'
        with self.assertRaisesRegex(ValueError, 'Source hash drift'):
            self.validate()

    def test_missing_operation_and_booking_are_not_generic_helper_coverage(self):
        self.contract['operations'][0]['booking'] = ''
        with self.assertRaisesRegex(ValueError, 'Missing operation contract'):
            self.validate()
        self.contract['operations'].pop()
        with self.assertRaisesRegex(ValueError, 'Missing or unknown railway operation'):
            self.validate()

    def test_owner_confirmed_teardown_cannot_be_restricted_or_regated(self):
        row = next(r for r in self.contract['decisions'] if r['id'] == 'RAIL-D006')
        row['status'] = 'approval-required'
        with self.assertRaisesRegex(ValueError, 'Hidden/new approval gate'):
            self.validate()
        row['status'] = 'retain-source'
        row['ownerDecision']['choice'] = 'owner-only-teardown'
        with self.assertRaisesRegex(ValueError, 'Owner pier-teardown decision drift'):
            self.validate()

    def test_missing_variant_or_relabelled_handler_is_rejected(self):
        self.contract['contributions'][0]['handler'] = {'name': 'GenericRailBuilder'}
        with self.assertRaisesRegex(ValueError, 'Contribution binding/claim drift'):
            self.validate()
        self.contract['contributions'].pop()
        with self.assertRaisesRegex(ValueError, 'contribution coverage drift'):
            self.validate()

    def test_nonreciprocal_requirement_and_decision_mapping(self):
        self.contract['workflowMappings'][0]['requirements'].append('MAN-NONEXISTENT')
        with self.assertRaisesRegex(ValueError, 'Missing reciprocal requirement link'):
            self.validate()
        self.contract['decisions'][0]['operations'] = []
        with self.assertRaisesRegex(ValueError, 'Nonreciprocal decision'):
            self.validate()

    def test_translation_loss_and_donor_mutation_are_rejected(self):
        row = next(r for r in self.locale['sourceStrings'] if r['englishDonor'])
        row['englishDonor']['originalText'] = 'made up donor'
        with self.assertRaisesRegex(ValueError, 'Text/donor provenance drift'):
            self.validate()
        self.locale['sourceStrings'].remove(row)
        with self.assertRaisesRegex(ValueError, 'Missing/extra P4 source text disposition'):
            self.validate()

    def test_scope_review_cannot_claim_runtime_or_shipped_translation(self):
        self.contract['operations'][0]['status'] = 'runtime-verified'
        with self.assertRaisesRegex(ValueError, 'Overclaimed operation'):
            self.validate()

        self.locale['status'] = 'shipped'
        with self.assertRaisesRegex(ValueError, 'Overclaimed or mismatched review status'):
            self.validate()

    def test_delivered_wording_and_each_source_disposition_are_required(self):
        row = self.locale['sourceStrings'][0]
        row['delivery']['wording']['en']['railObstacle'] = 'Invented wording'
        with self.assertRaisesRegex(ValueError, 'Missing/stale delivered translation'):
            self.validate()
        row.pop('delivery')
        with self.assertRaisesRegex(ValueError, 'Missing/stale delivered translation'):
            self.validate()


if __name__ == '__main__':
    unittest.main()
