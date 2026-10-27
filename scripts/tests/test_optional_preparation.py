"""Original synthetic optional packs; no sibling checkout or non-free artifacts."""
import importlib.util
import json
from pathlib import Path
import shutil
import tempfile
import unittest
from test_bundled_preparation import bmp, SCRIPTS

spec=importlib.util.spec_from_file_location('optional',SCRIPTS/'prepare-optional-content.py')
optional=importlib.util.module_from_spec(spec);spec.loader.exec_module(optional)


def fixture(parent,identity='fixture-one',plugin='synthetic-one',dependency='system'):
    root=parent/identity
    directory=root/'plugins'/plugin;directory.mkdir(parents=True)
    (root/'notice.txt').write_text('Original synthetic fixture, freely usable.\n')
    (root/'plugin-set.xml').write_text(f'''<?xml version="1.0" encoding="utf-8"?>
<plugin-set schemaVersion="1" id="{identity}" version="1" profile="synthetic-v2">
 <plugins root="plugins" discovery="immediate-children"/>
 <credits defaultLocale="ja">
  <locale lang="en"><button>{identity} credits</button><title>Fixture credits</title><text>Original synthetic fixture.\nSecond paragraph.</text></locale>
  <locale lang="ja"><button>{identity} 謝辞</button><title>謝辞</title><text>検証用の画像。</text></locale>
 </credits><notice path="notice.txt"/>
</plugin-set>''')
    (directory/'sheet.bmp').write_bytes(bmp()[0])
    (directory/'plugin.xml').write_text(f'''<plug-in><title>Synthetic</title><author>Fixture author</author><depend on="{dependency}"/>
<contribution type="picture" id="{identity}-picture"><picture src="sheet.bmp"/></contribution>
<contribution type="land" id="{identity}-land"><class name="freetrain.contributions.land.StaticLandBuilder"/>
<sprite origin="0,0" size="3,2" offset="0"><picture ref="{identity}-picture"/></sprite></contribution></plug-in>''')
    return root


class OptionalPreparation(unittest.TestCase):
    def setUp(self):
        self.temporary=tempfile.TemporaryDirectory();self.addCleanup(self.temporary.cleanup)
        self.root=Path(self.temporary.name)
    def prepare(self,roots):
        return optional.prepare(roots,optional.b.CATALOG,optional.b.INPUT,self.root/'output')
    def test_source_prepares_deterministically_without_touching_base_and_cache_is_validated(self):
        root=fixture(self.root)
        (root/'plugins/synthetic-one/readme-ja.txt').write_bytes('日本語の原文'.encode('cp932'))
        original=optional.b.CATALOG.read_bytes()
        paths=self.prepare([root]);self.assertEqual(paths,self.prepare([root]))
        pack=Path(paths[0]);catalog=json.loads((pack/'catalog.json').read_bytes());manifest=json.loads((pack/'prepared-pack.json').read_bytes())
        self.assertEqual('fixture-one',catalog['packId']);self.assertEqual(2,len(catalog['renderRecipes']))
        frame=catalog['renderRecipes'][1]['frames'][0]
        self.assertEqual([0,0,3,2],frame['rectangle']);self.assertEqual([0,0],frame['anchor'])
        self.assertEqual(8,len(frame['masks']))
        self.assertEqual('検証用の画像。',manifest['credits']['locales']['ja']['text'])
        self.assertEqual(original,optional.b.CATALOG.read_bytes())
        self.assertEqual('text/plain; charset=shift_jis',manifest['files']['notices/plugins/synthetic-one/readme-ja.txt']['contentType'])
        for name,entry in manifest['files'].items(): self.assertEqual(entry['sha256'],optional.b.digest((pack/name).read_bytes()))
        (pack/'catalog.json').write_text('changed')
        with self.assertRaisesRegex(ValueError,'cache drift'): self.prepare([root])
    def test_multiple_directories_dependency_order_and_argument_order_independence(self):
        one=fixture(self.root);two=fixture(self.root,'fixture-two','synthetic-two','synthetic-one')
        paths=self.prepare([two,one]);self.assertEqual(paths,self.prepare([one,two]))
        self.assertEqual(['fixture-one','fixture-two'],[json.loads((Path(p)/'catalog.json').read_bytes())['packId'] for p in paths])
    def test_missing_dependency_cycle_and_id_collision_never_publish(self):
        for problem in ('missing','cycle','collision'):
            with self.subTest(problem=problem),tempfile.TemporaryDirectory() as td:
                parent=Path(td);one=fixture(parent);two=fixture(parent,'fixture-two','synthetic-two')
                a=one/'plugins/synthetic-one/plugin.xml';bb=two/'plugins/synthetic-two/plugin.xml'
                if problem=='missing': a.write_text(a.read_text().replace('on="system"','on="absent"'))
                if problem=='cycle':
                    a.write_text(a.read_text().replace('on="system"','on="synthetic-two"'))
                    bb.write_text(bb.read_text().replace('on="system"','on="synthetic-one"'))
                if problem=='collision': bb.write_text(bb.read_text().replace('fixture-two-land','fixture-one-land'))
                with self.assertRaises(ValueError): self.prepare([one,two])
                self.assertFalse((one/'.freetrain-online').exists())
    def test_missing_image_unported_code_bad_reference_and_entities_are_named(self):
        for problem,expected in [('missing','Missing/ambiguous resource'),('code','unported qualified'),('reference','unresolved ref'),('entity','Unsafe content path')]:
            with self.subTest(problem=problem),tempfile.TemporaryDirectory() as td:
                root=fixture(Path(td));manifest=root/'plugins/synthetic-one/plugin.xml'
                source=manifest.read_text()
                if problem=='missing': source=source.replace('sheet.bmp','missing.bmp')
                if problem=='code': source=source.replace('freetrain.contributions.land.StaticLandBuilder','untrusted.Class')
                if problem=='reference': source=source.replace('ref="fixture-one-picture"','ref="absent"')
                if problem=='entity': source='<!DOCTYPE plug-in [<!ENTITY secret SYSTEM "file:///etc/passwd">]>'+source
                manifest.write_text(source)
                with self.assertRaisesRegex(ValueError,expected): self.prepare([root])
    def test_case_insensitive_unique_resolution_and_nested_manifest_is_inert(self):
        root=fixture(self.root);folder=root/'plugins/synthetic-one';(folder/'sheet.bmp').rename(folder/'SHEET.BMP')
        nested=folder/'nested';nested.mkdir();(nested/'plugin.xml').write_text('<plug-in><contribution type="menu" id="ignored"/></plug-in>')
        data=json.loads((Path(self.prepare([root])[0])/'catalog.json').read_bytes())
        self.assertEqual(2,len(data['contributions']));self.assertEqual('plugins/synthetic-one/SHEET.BMP',data['assets'][0]['path'])
    def test_prepared_distribution_can_supply_shared_art_to_a_new_source_set(self):
        one=fixture(self.root);prepared=Path(self.prepare([one])[0])
        two=fixture(self.root,'fixture-two','synthetic-two','synthetic-one')
        manifest=two/'plugins/synthetic-two/plugin.xml'
        manifest.write_text(manifest.read_text().replace('ref="fixture-two-picture"','ref="fixture-one-picture"'))
        paths=optional.prepare([two],optional.b.CATALOG,optional.b.INPUT,self.root/'mixed',[prepared/'catalog.json'])
        catalog=json.loads((Path(paths[0])/'catalog.json').read_bytes())
        self.assertEqual('fixture-one-picture',catalog['renderRecipes'][1]['frames'][0]['pictureId'])
        descriptor=json.loads((Path(paths[0])/'prepared-pack.json').read_bytes())
        self.assertIn('fixture-one',[d['id'] for d in descriptor['dependencies']])
    def test_internal_entities_are_bounded_and_local_fragments_require_explicit_metadata(self):
        root=fixture(self.root);path=root/'plugins/synthetic-one/plugin.xml'
        path.write_text('<!DOCTYPE plug-in [<!ENTITY label "Original fixture">]>'+path.read_text().replace('Synthetic','&label;'))
        self.assertEqual('Original fixture',json.loads((Path(self.prepare([root])[0])/'catalog.json').read_bytes())['plugins'][0]['title'])
        path.write_text('<!DOCTYPE plug-in [<!ENTITY part SYSTEM "part.xml">]><plug-in>&part;</plug-in>')
        (path.parent/'part.xml').write_text('<contribution type="picture" id="local-picture"><picture src="sheet.bmp"/></contribution>')
        with self.assertRaisesRegex(ValueError,'explicit local include'): self.prepare([root])
        metadata=root/'plugin-set.xml'
        metadata.write_text(metadata.read_text().replace('</plugin-set>','<xml-include manifest="plugins/synthetic-one/plugin.xml" entity="part" path="plugins/synthetic-one/part.xml"/></plugin-set>'))
        self.assertEqual(1,len(json.loads((Path(self.prepare([root])[0])/'catalog.json').read_bytes())['contributions']))
    def test_legacy_profile_is_explicit_and_does_not_change_canonical_utf8_default(self):
        root=fixture(self.root);path=root/'plugins/synthetic-one/plugin.xml'
        path.write_bytes(('<?xml version="1.0" encoding="cp932"?>'+path.read_text().replace('Synthetic','互換性')).encode('cp932'))
        with self.assertRaises(UnicodeError): self.prepare([root])
        metadata=root/'plugin-set.xml';metadata.write_text(metadata.read_text().replace('root="plugins"','root="plugins" encoding="cp932"').replace('synthetic-v2','historical-extra'))
        self.assertEqual('互換性',json.loads((Path(self.prepare([root])[0])/'catalog.json').read_bytes())['plugins'][0]['title'])
    def test_inline_picture_uses_own_binding_and_audio_remains_explicitly_pending(self):
        root=fixture(self.root);path=root/'plugins/synthetic-one/plugin.xml'
        path.write_text(path.read_text().replace('ref="fixture-one-picture"','src="sheet.bmp"'))
        (path.parent/'original.mid').write_bytes(b'Original synthetic MIDI placeholder; not playback evidence')
        output=Path(self.prepare([root])[0]);catalog=json.loads((output/'catalog.json').read_bytes())
        self.assertEqual('fixture-one-land',catalog['renderRecipes'][1]['frames'][0]['pictureId'])
        midi=next(a for a in catalog['assets'] if a['mediaType']=='mid')
        self.assertEqual('retained-audio; playback-adapter-pending',midi['status'])
        self.assertEqual(midi['sourceSha256'],optional.b.digest((output/midi['native']['url'].split('/',5)[-1]).read_bytes()))
    def test_credits_paths_symlinks_and_core_output_are_rejected(self):
        for problem in ('credits','path','symlink','output'):
            with self.subTest(problem=problem),tempfile.TemporaryDirectory() as td:
                root=fixture(Path(td));metadata=root/'plugin-set.xml'
                if problem=='credits': metadata.write_text(metadata.read_text().replace('lang="en"','lang="fr"'))
                if problem=='path': metadata.write_text(metadata.read_text().replace('path="notice.txt"','path="../notice.txt"'))
                if problem=='symlink':
                    resource=root/'plugins/synthetic-one/sheet.bmp';raw=resource.read_bytes();resource.unlink();(root/'elsewhere.bmp').write_bytes(raw);resource.symlink_to(root/'elsewhere.bmp')
                with self.assertRaises(ValueError):
                    if problem=='output': optional.prepare([root],optional.b.CATALOG,optional.b.INPUT,optional.b.ROOT/'tmp/forbidden-optional')
                    else: self.prepare([root])

if __name__=='__main__':
    import sys
    if len(sys.argv)==3 and sys.argv[1]=='--write-fixtures':
        parent=Path(sys.argv[2]);parent.mkdir(parents=True,exist_ok=True)
        fixture(parent);fixture(parent,'fixture-two','synthetic-two','synthetic-one')
    else: unittest.main()

