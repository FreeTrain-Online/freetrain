"""P5-03 source factory vectors through the actual optional preparation path.

All authored definitions are synthetic. Shared art is already bundled; no
optional/non-free checkout is discovered or redistributed by these checks.
"""
import json
from pathlib import Path
import tempfile
import unittest
from test_optional_preparation import fixture, optional


def stock_fixture(parent):
    root = fixture(parent)
    path = root / 'plugins/synthetic-one/plugin.xml'
    path.write_text(path.read_text().replace('</plug-in>', '''
<contribution type="trainCar" id="synthetic-color-car">
 <class name="freetrain.contributions.train.ColoredTrainCarImpl"/>
 <capacity>100</capacity><seatedcapacity>25</seatedcapacity>
 <colorMap picture="{243D9F53-67FB-432E-B985-3B9B696D8CE0}-2"
  base="10,20,30" line1="40,50,60" line2="70,80,90" line3="100,110,120"/>
</contribution>
<contribution type="train" id="synthetic-pattern-train">
 <class name="freetrain.contributions.train.PatternTrainImpl"/>
 <company>Fixture company</company><type>Mixed</type><name>Pattern</name>
 <author>Fixture author</author><description>Original synthetic stock.</description>
 <price>1000</price><speed>5</speed><fare>450</fare><amenity>250</amenity><triprange>middle</triprange>
 <config><car char="A" ref="{9EE438DE-B766-4b37-A474-9CE6EE73FE75}"/>
 <car char="C" ref="synthetic-color-car"/><composition>A C\nC A</composition><composition>C</composition></config>
</contribution></plug-in>'''))
    return root


class StockPreparation(unittest.TestCase):
    def test_pattern_and_colored_car_compile_exact_bindings_and_palette(self):
        with tempfile.TemporaryDirectory() as td:
            parent = Path(td)
            root = stock_fixture(parent)
            prepared = Path(optional.prepare([root], optional.b.CATALOG, optional.b.INPUT, parent/'out')[0])
            catalog = json.loads((prepared/'catalog.json').read_bytes())
            recipes = {r['id']: r for r in catalog['renderRecipes']}
            pattern = recipes['synthetic-pattern-train']
            self.assertEqual({'1': ['synthetic-color-car'], '4': [
                '{9EE438DE-B766-4b37-A474-9CE6EE73FE75}', 'synthetic-color-car',
                'synthetic-color-car', '{9EE438DE-B766-4b37-A474-9CE6EE73FE75}']}, pattern['fields']['patterns'])
            self.assertEqual((5, 200, 1), tuple(pattern['fields'][k] for k in ('minutesPerVoxel', 'effectiveAmenity', 'triprange')))
            car = recipes['synthetic-color-car']
            self.assertEqual('ready', car['status']); self.assertEqual(12, len(car['frames']))
            self.assertEqual('{243D9F53-67FB-432E-B985-3B9B696D8CE0}-2', car['fields']['colorTemplate'])
            maps = car['frames'][0]['effect']['maps']
            self.assertEqual([10, 20, 30], maps[0]['target'])
            self.assertEqual([7, 14, 21], maps[1]['target'])
            self.assertEqual([100, 110, 120], maps[6]['target'])

    def test_duplicate_lengths_unknown_characters_and_oversize_patterns_never_prepare(self):
        for original, replacement in [('A C\nC A', 'Z'), ('<composition>C</composition>', '<composition>AAAA</composition>'),
                                      ('A C\nC A', 'A'*101)]:
            with self.subTest(replacement=replacement[:20]), tempfile.TemporaryDirectory() as td:
                parent = Path(td); root = stock_fixture(parent)
                path = root/'plugins/synthetic-one/plugin.xml'
                path.write_text(path.read_text().replace(original, replacement))
                with self.assertRaises(ValueError):
                    optional.prepare([root], optional.b.CATALOG, optional.b.INPUT, parent/'out')


if __name__ == '__main__':
    import sys
    if len(sys.argv) == 3 and sys.argv[1] == '--write-fixture':
        parent = Path(sys.argv[2]); parent.mkdir(parents=True, exist_ok=True)
        root = stock_fixture(parent)
        print(optional.prepare([root], optional.b.CATALOG, optional.b.INPUT, parent/'prepared')[0])
    else:
        unittest.main()
