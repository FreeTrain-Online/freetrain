"""Completeness and independently calculated P0 source expectation checks."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest


SPEC = importlib.util.spec_from_file_location('rail_gallery', Path(__file__).resolve().parents[1] / 'parity-rail-gallery.py')
rail = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(rail)


class GeometryReview(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        config = json.loads(rail.inventory.CONFIG.read_text())
        cls.tree = rail.inventory.Tree(rail.inventory.ROOT, config['baseRevision'])
        cls.geometry = rail.extract(cls.tree)
        cls.review = rail.semantics.extract(cls.tree, cls.geometry, rail.inventory.digest)

    def test_source_review_pins_dependencies_and_current_generated_output(self):
        class FutureTree:
            revision = 'unreviewed-baseline'
        with self.assertRaisesRegex(ValueError, 'fresh review'):
            rail.semantics.extract(FutureTree(), self.geometry, rail.inventory.digest)
        reviews = self.review['sourceReviews']
        self.assertEqual(set(rail.semantics.SOURCES), {r['path'] for r in reviews})
        for r in reviews:
            self.assertEqual(rail.inventory.digest(self.tree.read(r['path'])), r['sha256'])
            self.assertTrue(r['review'])
        saved = json.loads((rail.inventory.OUT / 'geometry-behavior.json').read_text())
        self.assertEqual(self.review, saved)

    def test_both_orientations_and_reversal_cover_all_16_angles(self):
        poses = self.review['levelPoses']
        self.assertEqual(48, len(poses))
        self.assertEqual(48, len({(p['heading'], p['outgoing'], p['reversed']) for p in poses}))
        self.assertEqual(set(range(16)), {p['asymmetricFrame'] for p in poses})
        cases = {(p['heading'], p['outgoing'], p['reversed']): p for p in poses}
        # Source-derived golden calculations: N→NE and NE→N use the same frame
        # but different pixel shifts. Reversal affects the art, not the offset.
        for key, frame, offset in [((0, 1, False), 1, [3, -11]),
                                   ((1, 0, False), 1, [-3, -7]),
                                   ((0, 1, True), 9, [3, -11]),
                                   ((6, 6, False), 12, [0, -9])]:
            self.assertEqual((frame, offset), (cases[key]['asymmetricFrame'], cases[key]['offset']))

    def test_slope_levels_keep_invisible_occupancy_and_independent_frames(self):
        patterns = [p for p in self.geometry['patterns'] if p['family'] in ('slope', 'underground-slope')]
        self.assertEqual(32, len(patterns))
        self.assertEqual(16, sum(not p['visible'] for p in patterns))
        for p in patterns:
            self.assertEqual([0, 1, -1, 0][p['level']], p['zdiff'])
            if not p['visible']:
                self.assertEqual([384, 0, 1, 1], p['rectangle'])
                self.assertEqual(2, len(p['connections']))
        poses = self.review['slopePoses']
        self.assertEqual(64, len(poses))
        self.assertEqual(set(range(8)), {p['asymmetricFrame'] for p in poses})
        cases = {(p['climb'], p['level'], p['heading'], p['reversed']): p for p in poses}
        for key, frame, offset in [((2, 1, 2, False), 2, [0, -13]),
                                   ((2, 2, 6, False), 7, [0, -1]),
                                   ((2, 2, 6, True), 2, [0, -1])]:
            self.assertEqual((frame, offset), (cases[key]['asymmetricFrame'], cases[key]['offset']))

    def test_attachment_table_keeps_open_endpoint_and_junction_cases_distinct(self):
        rows = self.review['normalAttachments']
        self.assertEqual(192, len(rows))
        cases = {(r['patternId'], r['wellConnected'], r['newDirection']): r for r in rows}
        self.assertEqual(192, len(cases))
        # Authored N/S pattern 10: NE becomes N/NE/S junction 19 only when
        # already connected; otherwise retain S to form normal NE/S pattern 0.
        self.assertEqual(19, cases[10, True, 1]['resultId'])
        self.assertEqual(0, cases[10, False, 1]['resultId'])
        self.assertFalse(cases[10, True, 2]['allowed'])
        self.assertIsNone(cases[10, True, 2]['resultId'])
        self.assertEqual(10, cases[10, True, 0]['resultId'])

    def test_differences_have_stable_ids_and_later_phase_owners(self):
        rows = self.review['sourceDifferences']
        self.assertEqual({f'GEO-{i:03d}' for i in range(1, 9)}, {r['id'] for r in rows})
        for r in rows:
            self.assertTrue(r['finding'])
            self.assertTrue(r['phase'])

    def test_gallery_pass_order_cuts_and_every_image_are_reviewable(self):
        with tempfile.TemporaryDirectory() as folder:
            out = Path(folder)
            self.assertEqual(152, rail.semantics.gallery(self.tree, self.geometry, self.review, out, rail.images))
            report = json.loads((out / 'compositions.json').read_text())
            scenes = {s['id']: s for s in report['scenes']}
            self.assertEqual(152, len(scenes))
            for s in scenes.values():
                data = (out / s['image']).read_bytes()
                self.assertEqual(b'\x89PNG\r\n\x1a\n', data[:8])
                self.assertEqual((160, 112), (int.from_bytes(data[16:20], 'big'), int.from_bytes(data[20:24], 'big')))
            for kind, count in [('bridge', 6), ('tunnel', 2)]:
                for i in range(count):
                    stages = [scenes[f'{kind}-{i}-stage-{stage}']['layers'] for stage in (1, 2, 3)]
                    self.assertEqual([1, 2, 3], list(map(len, stages)))
                    self.assertEqual(stages[0], stages[2][:1])
                    self.assertEqual(stages[1], stages[2][:2])
                    self.assertIn('Train485a.bmp', stages[2][1]['path'])
            for kind in ('surface', 'underground'):
                for d in (0, 2, 4, 6):
                    self.assertEqual(6, len(scenes[f'ramp-{kind}-{d}-cut-1']['layers']))
                    self.assertEqual(10, len(scenes[f'ramp-{kind}-{d}-cut-2']['layers']))


if __name__ == '__main__':
    unittest.main()
