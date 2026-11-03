"""Independent source vectors and complete bundle accounting for executable art."""
import importlib.util
import json
from pathlib import Path
import sys
import unittest
SCRIPTS=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(SCRIPTS))
from content_recipes import slices, effect, transform_pixel, integer
spec=importlib.util.spec_from_file_location('bundled',SCRIPTS/'prepare-bundled-content.py')
bundled=importlib.util.module_from_spec(spec);spec.loader.exec_module(bundled)


def node(**attributes): return dict(attributes=attributes,children=[])


class LoaderVectors(unittest.TestCase):
    def test_0d_size_offset_and_2d_diagonal_anchors_are_source_values(self):
        self.assertEqual([dict(cell=[0,0,0],rectangle=[4,7,32,32],anchor=[0,9])],slices(node(origin='4,7',offset='9'),[1,1,1],'0d'))
        result=slices(node(origin='4,7',offset='8',height='25'),[2,2,99],'2d')
        self.assertEqual([[4,7,32,24],[20,7,32,16],[20,7,32,25],[36,7,32,24]],[r['rectangle'] for r in result])
        self.assertEqual([[0,8],[0,0],[0,16],[0,8]],[r['anchor'] for r in result])

    def test_3d_top_hat_bottom_edges_and_invisible_occupancy(self):
        result={tuple(r['cell']):r for r in slices(node(origin='0,0'),[2,2,3],'3d')}
        self.assertEqual(12,len(result))
        self.assertEqual([0,8,16,32],result[0,0,2]['rectangle'])
        self.assertEqual([16,0,32,32],result[1,0,2]['rectangle'])
        self.assertEqual([16,32,32,16],result[0,1,2]['rectangle'])
        self.assertEqual([48,8,16,32],result[1,1,2]['rectangle'])
        self.assertEqual([-16,16],result[1,1,2]['anchor'])
        self.assertEqual([16,40,32,40],result[0,1,0]['rectangle'])
        self.assertEqual([0,24],result[0,1,0]['anchor'])
        self.assertTrue(result[1,0,0]['invisible']);self.assertTrue(result[0,0,1]['invisible'])

    def test_top_clipping_preserves_anchor_and_empty_generated_footprint(self):
        result=slices(node(origin='0,0',offset='0'),[1,1,1],'3d')[0]
        self.assertEqual([0,0,32,16],result['rectangle']);self.assertEqual([0,0],result['anchor'])
        self.assertEqual([],slices(node(origin='0,0'),[0,0,0],'3d'))
        with self.assertRaises(ValueError): slices(node(origin='0,0'),[256,256,256],'3d')

    def test_hue_retains_native_asymmetric_channel_rounding_and_nonprimary_colors(self):
        settings=dict(kind='hue',targets=[[255,180,64],None,None])
        self.assertEqual((254,179,64,255),transform_pixel((255,0,0,255),settings))
        self.assertEqual((127,90,32,255),transform_pixel((128,0,0,255),settings))
        self.assertEqual((255,1,0,255),transform_pixel((255,1,0,255),settings))
        self.assertEqual((255,0,0,0),transform_pixel((255,0,0,0),settings))

    def test_colormap_reverse_scan_and_authored_night_collision_order(self):
        settings=dict(kind='color-map',maps=[dict(source=[40,0,0],target=[200,0,0]),dict(source=[10,0,0],target=[80,0,0])])
        self.assertEqual((80,0,0,255),transform_pixel((10,0,0,255),settings,True))
        self.assertEqual((20,0,0,255),transform_pixel((2,0,0,255),settings,True))
        for value in ('1.5','NaN','1e3',str(2**63)):
            with self.assertRaises(ValueError):integer(value)


class CompleteRecipes(unittest.TestCase):
    @classmethod
    def setUpClass(cls): cls.catalog,cls.artifacts=bundled.prepare()

    def test_p5_independent_stock_frame_crops_including_reverse_tail_slopes(self):
        fixtures=json.loads((SCRIPTS.parent/'doc/parity/train-fixtures.json').read_text())
        recipes={r['id']:r for r in self.catalog['renderRecipes']}
        for case in fixtures['groups']['rendering']['cases']:
            i,e=case['input'],case['expected']
            key='angle' if i['kind']=='level' else 'slope'
            with self.subTest(case=case['id']):
                frames=[f for f in recipes[i['recipe']]['frames'] if f['selector'].get(key)==e['selector']]
                self.assertEqual(1,len(frames))
                self.assertEqual(e['rectangle'],frames[0]['rectangle'])

    def test_symmetric_reverse_wrapper_preserves_both_source_masked_frame_families(self):
        # No bundled symmetric wrapper exists. Feed the actual symmetric base
        # through the same generic compiler path used by optional definitions.
        from copy import deepcopy
        from content_recipes import Compiler
        base=deepcopy(next(r for r in self.catalog['renderRecipes'] if r['id']=='{9EE438DE-B766-4b37-A474-9CE6EE73FE75}'))
        wrapper=dict(id='controlled-symmetric-tail',handler='reverse-train-car',fields={'base':base['id']})
        compiler=Compiler(dict(pictures=[],assets=[],contributions=[base,wrapper]))
        compiler.compile=deepcopy
        result=compiler.run()[1]
        self.assertEqual(base['frames'],result['frames'])

    def test_all_definitions_and_code_pictures_accounted_for_without_fabricated_gameplay(self):
        c=self.catalog
        self.assertEqual(159,c['counts']['preparedPictures'])
        self.assertEqual({d['id'] for d in c['contributions']},{r['id'] for r in c['renderRecipes'] if not r['id'].startswith('system:')})
        self.assertEqual(21,c['recipeCounts']['invalidCrops'])
        self.assertTrue(all('invalidVariants' in f for r in c['renderRecipes'] for f in r['frames'] if f['status']=='invalid-source-crop'))
        self.assertEqual('2',c['version'])

    def test_every_generated_image_has_source_namespace_and_distributed_credit_evidence(self):
        c=self.catalog;provenance=json.loads(self.artifacts['provenance.json'])
        self.assertEqual(bundled.PIN,provenance['source']['revision'])
        for a in provenance['assets']:
            self.assertTrue(a['sourceSha256']);self.assertTrue(a['notices']);self.assertIn('author',a);self.assertIn('credits',a)
            expected=bundled.URL+a['path'].removeprefix('trunk/').rsplit('/',1)[0]+'/media/'
            for mode in ('day','night','native'):
                if mode in a:self.assertTrue(a[mode]['url'].startswith(expected),a['path'])
            for derivative in a.get('derivatives',[]):
                self.assertTrue(derivative['image']['url'].startswith(expected))
        self.assertEqual(54,len(c['pluginMetadata']))
        for m in c['pluginMetadata']:
            metadata=json.loads(self.artifacts[m['url'][len(bundled.URL):]])
            self.assertEqual(m['pluginId'],metadata['plugin']['id'])
            self.assertTrue(all(d['pluginId']==m['pluginId'] for d in metadata['contributions']))
            review=json.loads((bundled.INPUT/'review.json').read_text())
            directory=metadata['plugin']['path'].rsplit('/',1)[0]+'/'
            expected=[t for t in review['translations'] if t['path'].startswith(directory)]
            self.assertEqual(expected,metadata['translations'])

    def test_all_valid_season_light_bindings_have_bounded_opaque_masks_and_exact_bytes(self):
        c=self.catalog
        for r in c['renderRecipes']:
            for f in r['frames']:
                if f['status']=='invisible':continue
                x,y,w,h=f['rectangle']
                for key,b in c['surfaceSets'][f['surfaceSet']].items():
                    if key in f.get('invalidVariants',[]):
                        self.assertNotIn(key,f['masks']);continue
                    self.assertLessEqual(x+w,b['width']);self.assertLessEqual(y+h,b['height'])
                    rows=c['masks'][f['masks'][key]]
                    self.assertEqual(h,len(rows));self.assertTrue(all(len(row)==w and set(row)<=set('01') for row in rows))
        urls={b['image']['url']:b['image'] for s in c['surfaceSets'].values() for b in s.values()}
        for url,m in urls.items():
            raw=self.artifacts[url[len(bundled.URL):]]
            self.assertEqual(m['sha256'],bundled.digest(raw));self.assertEqual(m['bytes'],len(raw))

if __name__=='__main__':unittest.main()
