"""Canonical preparation rejects drift and builds without historical/archive trees."""
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import struct
import subprocess
import sys
import tempfile
import unittest
import zlib

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))
SPEC = importlib.util.spec_from_file_location('bundled', SCRIPTS / 'prepare-bundled-content.py')
bundled = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(bundled)
import content_bitmap as bitmap


def bmp(bits=24, top_down=False):
    # Independent 3x2 fixture: magenta key, a special night light and opaque RGB.
    rows = [[(255, 0, 255), (8, 0, 0), (100, 80, 60)], [(0, 8, 0), (0, 0, 8), (255, 0, 255)]]
    palette = rows[0] + rows[1][:2]
    palette_bytes = b'' if bits == 24 else b''.join(bytes((*rgb[::-1], 0)) for rgb in palette)
    data = bytearray()
    for row in rows if top_down else rows[::-1]:
        if bits == 24:
            raw = b''.join(bytes(rgb[::-1]) for rgb in row)
        elif bits == 8:
            raw = bytes(palette.index(rgb) for rgb in row)
        else:
            indexes = [palette.index(rgb) for rgb in row] + [0]
            raw = bytes((indexes[0] << 4 | indexes[1], indexes[2] << 4 | indexes[3]))
        data.extend(raw + b'\0' * (-len(raw) % 4))
    offset = 54 + len(palette_bytes)
    header = b'BM' + struct.pack('<IHHI', offset + len(data), 0, 0, offset)
    header += struct.pack('<IiiHHIIiiII', 40, 3, -2 if top_down else 2, 1, bits, 0, len(data), 0, 0,
                          len(palette) if bits != 24 else 0, 0)
    return header + palette_bytes + data, rows[0] + rows[1]


class BitmapConversion(unittest.TestCase):
    def test_indexed_padding_odd_nibble_and_both_scan_orders_have_literal_pixels(self):
        for bits in (4, 8, 24):
            for top_down in (False, True):
                raw, expected = bmp(bits, top_down)
                self.assertEqual((3, 2, expected), bitmap.decode(raw))

    def test_night_lights_quartering_key_collision_and_png_are_independent(self):
        pixels = [(1, 2, 3), (2, 3, 1), (8, 0, 0), (0, 8, 0), (0, 0, 8), (100, 80, 60)]
        self.assertEqual([(0, 0, 0, 0), (0, 0, 0, 0), (255, 8, 8, 255), (252, 243, 148, 255),
                          (255, 227, 99, 255), (25, 20, 15, 255)], bitmap.surface(pixels, True))
        rgba = bitmap.surface(pixels)
        raw = bitmap.png(3, 2, rgba)
        self.assertEqual(b'\x89PNG\r\n\x1a\n', raw[:8])
        self.assertEqual((3, 2), struct.unpack_from('>II', raw, 16))
        size = struct.unpack_from('>I', raw, 33)[0]
        self.assertEqual(b'\0' + bytes(v for p in rgba[:3] for v in p) + b'\0' + bytes(v for p in rgba[3:] for v in p),
                         zlib.decompress(raw[41:41+size]))

    def test_truncated_pixels_compression_and_invalid_palette_are_rejected(self):
        raw, _ = bmp(8)
        malformed = [raw[:20], raw[:-1], b'not bmp']
        compressed = bytearray(raw); struct.pack_into('<I', compressed, 30, 1); malformed.append(compressed)
        indexed = bytearray(raw); indexed[struct.unpack_from('<I', raw, 10)[0]] = 255; malformed.append(indexed)
        for value in malformed:
            with self.assertRaises(ValueError):
                bitmap.decode(value)


class PreparedInputs(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog, cls.artifacts = bundled.prepare()

    def test_pinned_denominators_exclusions_registries_and_native_media_remain_explicit(self):
        self.assertEqual(dict(manifests=56, selectedPlugins=54, declarations=482, factories=33, contributions=448,
                              pictures=159, preparedPictures=159, assets=238, convertedBitmaps=213, excluded=2),
                         self.catalog['counts'])
        self.assertEqual(bundled.PIN, self.catalog['source']['revision'])
        self.assertTrue(all(d['status'] == 'prepared; gameplay-unavailable' for d in self.catalog['contributions']))
        self.assertEqual(1, sum('generatedFrom' in d for d in self.catalog['contributions']))
        self.assertEqual(3, sum(a['status'] == 'retained-midi' for a in self.catalog['assets']))
        self.assertEqual(5, sum(d['code'] == 'source-project-gap' for d in self.catalog['diagnostics']))
        self.assertEqual(0, sum(p['status'] == 'code-picture-binding-pending' for p in self.catalog['pictures']))
        self.assertFalse(any('A4' in p['id'] or 'dbgwnd' in p['id'] for p in self.catalog['plugins']))
        for item in self.catalog['assets']:
            self.assertTrue(item['notices'])
            for mode in ('day', 'night', 'native'):
                if mode in item:
                    self.assertEqual(item[mode]['sha256'], hashlib.sha256(self.artifacts[item[mode]['url'][len(bundled.URL):]]).hexdigest())

    def test_catalog_and_all_generated_outputs_match_and_have_no_machine_paths(self):
        bundled.write_outputs(self.artifacts, check=True)
        self.assertNotIn(b'/Users/', self.artifacts['catalog.json'])
        self.assertNotIn(b'freetrain_plugins', self.artifacts['catalog.json'])
        self.assertEqual(4, len(self.catalog['notices']))

    def test_flat_ground_bitmaps_keep_transparency_and_other_tiles(self):
        for name, expected in [('EmptyChip.bmp', [(219, 195, 131), (255, 255, 255)]),
                               ('EmptyChip_n.bmp', [(55, 49, 33), (64, 64, 64)])]:
            path = 'trunk/core/res/' + name
            raw = (bundled.INPUT / 'source' / path).read_bytes()
            width, height, original = bitmap.decode(raw)
            w, h, pixels = bundled.prepared_bitmap(path, raw)
            self.assertEqual((width, height), (w, h))
            for y in range(height):
                for x in range(width):
                    i = y * width + x
                    self.assertEqual(expected[x // 32] if x < 64 and original[i] != original[0] else original[i], pixels[i])
            self.assertEqual([p[3] for p in bitmap.surface(original)], [p[3] for p in bitmap.surface(pixels)])

    def test_duplicate_ids_missing_dependencies_and_cycles_fail_without_silent_overrides(self):
        with self.assertRaisesRegex(ValueError, 'Duplicate contribution'):
            bundled.unique([{'id': 'X'}, {'id': 'X'}], 'id', 'contribution')
        for plugins in ([{'id': 'a', 'dependencies': ['absent']}],
                        [{'id': 'a', 'dependencies': ['b']}, {'id': 'b', 'dependencies': ['a']}],
                        [{'id': 'a', 'dependencies': []}, {'id': 'a', 'dependencies': []}]):
            with self.assertRaises(ValueError):
                bundled.plugin_order(plugins)
        self.assertEqual(['system', 'a', 'b'], bundled.plugin_order([
            {'id': 'b', 'dependencies': ['a']}, {'id': 'a', 'dependencies': ['system']}, {'id': 'system', 'dependencies': []}]))

    def test_resource_paths_preserve_case_and_reject_traversal_aliases_and_ambiguity(self):
        files = {'trunk/p/Art.bmp': b''}
        self.assertEqual('trunk/p/Art.bmp', bundled.resolve_resource(files, 'trunk/p', 'art.bmp'))
        for path in ('../Art.bmp', '/Art.bmp', 'https://host/Art.bmp', 'x/../Art.bmp', 'x//Art.bmp'):
            with self.assertRaises(ValueError):
                bundled.resolve_resource(files, 'trunk/p', path)
        with self.assertRaises(ValueError):
            bundled.resolve_resource({**files, 'trunk/p/art.bmp': b''}, 'trunk/p', 'art.bmp')

    def test_season_inheritance_and_first_child_night_rule_preserve_source_oddities(self):
        def node(src=None, when=None, nested=()):
            return dict(tag='override' if when else 'picture', attributes={**({'src': src} if src else {}), **({'when': when} if when else {})},
                        text='', children=list(nested))
        files = {'trunk/p/' + p: b'' for p in ('spring.bmp', 'summer.bmp', 'winter.bmp', 'night.bmp')}
        declaration = node('spring.bmp', nested=[node('summer.bmp', 'summer'), node('night.bmp', 'night'),
                           node('winter.bmp', 'winter', [node('night.bmp', 'night')])])
        variants = bundled.picture_variants(declaration, 'trunk/p', files)
        self.assertEqual('automatic-night-rgb888-v1', variants[0]['night']['treatment'])
        self.assertEqual('trunk/p/summer.bmp', variants[1]['night']['path'])
        self.assertEqual(variants[0]['night'], variants[2]['night'])
        self.assertEqual('authored', variants[3]['night']['treatment'])
        self.assertEqual('trunk/p/night.bmp', variants[3]['night']['path'])

    def test_input_hash_drift_extra_files_symlinks_and_unsafe_index_paths_fail(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder) / 'input'; shutil.copytree(bundled.INPUT, root)
            index_path = root / 'source-index.json'; original_index = index_path.read_bytes()
            index = json.loads(original_index)
            path = root / 'source' / index['files'][0]['path']; original = path.read_bytes()
            path.write_bytes(original + b'corruption')
            with self.assertRaisesRegex(ValueError, 'source drift'):
                bundled.load_inputs(root)
            path.write_bytes(original)
            extra = root / 'optional-content.bmp'; extra.write_bytes(b'not bundled')
            with self.assertRaisesRegex(ValueError, 'Unexpected'):
                bundled.load_inputs(root)
            extra.unlink()
            index['files'][0]['path'] = '../outside'; index_path.write_bytes(bundled.dump(index))
            with self.assertRaisesRegex(ValueError, 'Unsafe'):
                bundled.load_inputs(root)
            index_path.write_bytes(original_index)
            outside = Path(folder) / 'outside'; outside.write_bytes(original); path.unlink(); path.symlink_to(outside)
            with self.assertRaisesRegex(ValueError, 'escapes'):
                bundled.load_inputs(root)

    def test_minimal_default_build_has_no_git_doc_core_plugins_or_sibling_checkout(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            for source in ('content/bundled-v2', 'web/public/content/bundled-v2'):
                shutil.copytree(bundled.ROOT / source, root / source)
            paths = [Path('scripts/prepare-bundled-content.py'), Path('scripts/content_bitmap.py'), Path('scripts/content_recipes.py'),
                     Path('prototype/content-artifacts.json'), Path('src/FreeTrain.Online.Content/bundled-v2.json'),
                     Path('src/FreeTrain.Online.Content/bundled-v2.sha256')]
            frozen = json.loads((bundled.ROOT / 'prototype/content-artifacts.json').read_text())
            paths.extend(Path(r['path']) for r in frozen['files'])
            for path in paths:
                (root / path).parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(bundled.ROOT / path, root / path)
            result = subprocess.run([sys.executable, 'scripts/prepare-bundled-content.py', '--check'], cwd=root, capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stderr)
            self.assertIn('"contributions": 448', result.stdout)


if __name__ == '__main__':
    unittest.main()
