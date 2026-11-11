"""P7-01 evidence corruption/coverage tests, not city runtime acceptance."""
import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('city_review', ROOT / 'scripts/review-city.py')
city = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(city)


class CityReviewTests(unittest.TestCase):
    def setUp(self):
        self.review = city.read('city-review.json')
        self.locale = city.read('city-localization-review.json')

    def validate(self):
        # Re-sign mutations so tests exercise semantic completeness, not just hashing.
        for doc in (self.review, self.locale):
            doc['reviewSha256'] = city.digest({k: v for k, v in doc.items() if k != 'reviewSha256'})
        return city.validate(self.review, self.locale)

    def test_checked_contract(self):
        result = self.validate()
        self.assertEqual((result['contributions'], result['controls'], result['decisions']), (209, 215, 20))

    def test_source_and_registered_dependencies_cannot_disappear(self):
        self.review['sourceReviews'].pop()
        with self.assertRaisesRegex(ValueError, 'Source denominator drift'):
            self.validate()

    def test_declaration_and_control_denominators(self):
        self.review['contributions'].pop()
        with self.assertRaisesRegex(ValueError, 'Contribution denominator'):
            self.validate()
        self.setUp()
        self.review['actions'].pop()
        with self.assertRaisesRegex(ValueError, 'Control/action denominator'):
            self.validate()

    def test_source_quirk_cannot_be_reclassified_as_an_approved_change(self):
        self.review['decisions'][0]['status'] = 'existing-plan-adaptation'
        with self.assertRaisesRegex(ValueError, 'Source quirk silently reclassified'):
            self.validate()

    def test_decisions_and_dependencies_have_reciprocal_owners(self):
        self.review['decisions'][0]['operations'] = ['property']
        with self.assertRaisesRegex(ValueError, 'Nonreciprocal decision'):
            self.validate()
        self.setUp()
        self.review['dependencies'][0]['owner'] = ''
        with self.assertRaisesRegex(ValueError, 'Dependency ownership drift'):
            self.validate()

    def test_xml_and_automatic_eligibility_are_source_bound(self):
        row = next(r for r in self.review['contributions'] if r['declarationXml'])
        row['computerCannotBuild'] = not row['computerCannotBuild']
        with self.assertRaisesRegex(ValueError, 'Definition metadata drift'):
            self.validate()

    def test_factory_and_resource_closure(self):
        self.review['factories'].pop()
        with self.assertRaisesRegex(ValueError, 'Factory support drift'):
            self.validate()
        self.setUp()
        self.review['resources'].pop()
        with self.assertRaisesRegex(ValueError, 'Resource denominator drift'):
            self.validate()
        self.setUp()
        self.review['referenceBindings'].pop()
        with self.assertRaisesRegex(ValueError, 'Code registration reference scope drift'):
            self.validate()

    def test_inherited_scenarios_are_not_rewritten(self):
        self.review['scenarioBindings'][0]['expectedSha256'] = 'changed'
        with self.assertRaisesRegex(ValueError, 'Scenario expectation drift'):
            self.validate()

    def test_literal_donors_and_catalog_scope(self):
        row = next(r for r in self.locale['sourceStrings'] if r['sourceText'].get('englishDonor'))
        row['sourceText']['englishDonor']['originalText'] = 'guessed translation'
        with self.assertRaisesRegex(ValueError, 'Text/donor provenance drift'):
            self.validate()
        self.setUp()
        self.locale['catalogStrings'].pop()
        with self.assertRaisesRegex(ValueError, 'Catalog/donor scope drift'):
            self.validate()

    def test_specification_never_credits_shipped_text_or_gameplay(self):
        self.locale['sourceStrings'][0]['status'] = 'shipped'
        with self.assertRaisesRegex(ValueError, 'Unclassified/overclaimed translation'):
            self.validate()
        self.setUp()
        self.review['runtimeStatus'] = 'implemented'
        with self.assertRaisesRegex(ValueError, 'Overclaimed specification'):
            self.validate()

    def test_followups_and_category_names_cannot_be_dropped(self):
        self.review['followUps'][0]['owner'] = ''
        with self.assertRaisesRegex(ValueError, 'Follow-up ownership drift'):
            self.validate()
        self.setUp()
        self.locale['categoryNames'].pop()
        with self.assertRaisesRegex(ValueError, 'Category metadata/text scope drift'):
            self.validate()


if __name__ == '__main__':
    unittest.main()
