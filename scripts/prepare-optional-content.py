#!/usr/bin/env python3
"""Prepare explicit external plugin sets. Never writes bundled or web build inputs."""
import argparse
import importlib.util
import json
from pathlib import Path, PurePosixPath
import re
import sys
import tempfile
import os
import posixpath
from content_xml import manifest as bounded_manifest
import xml.etree.ElementTree as ET

spec = importlib.util.spec_from_file_location('bundled_preparation', Path(__file__).with_name('prepare-bundled-content.py'))
b = importlib.util.module_from_spec(spec)
spec.loader.exec_module(b)
from content_bitmap import decode, surface, png
from content_recipes import Compiler, child, children, text

ID = re.compile(r'[a-z0-9][a-z0-9._-]{0,95}\Z')
XML_LIMIT = 8 * 1024 * 1024


def parse_xml(raw, owner):
    if len(raw) > XML_LIMIT or re.search(br'<!\s*(?:DOCTYPE|ENTITY)', raw, re.I):
        raise ValueError(f'{owner}: XML entities/DTDs or oversized XML are not supported; supply expanded UTF-8 XML')
    # Online inputs are explicitly UTF-8. Legacy CP932 conversion is a separate preparation operation.
    decoded = raw.decode('utf-8-sig')
    encoding = re.search(r'<\?xml[^>]*encoding=[\'"]([^\'"]+)', decoded, re.I)
    if encoding and encoding[1].lower() not in ('utf-8', 'utf8'):
        raise ValueError(f'{owner}: expected UTF-8 XML')
    return ET.fromstring(decoded)


def scan(root):
    root = root.resolve()
    metadata = b.checked_read(root, 'plugin-set.xml')
    node = parse_xml(metadata, str(root / 'plugin-set.xml'))
    if node.tag != 'plugin-set' or node.get('schemaVersion') != '1' or not ID.fullmatch(node.get('id', '')) or not node.get('version'):
        raise ValueError(f'{root}: invalid plugin-set identity/schema')
    plugins_node = node.find('plugins')
    if plugins_node is None or plugins_node.get('discovery') != 'immediate-children':
        raise ValueError(f'{root}: expected immediate-children plugin discovery')
    source_encoding=plugins_node.get('encoding','utf-8')
    if source_encoding not in ('utf-8','cp932'): raise ValueError(f'{root}: unsupported manifest encoding')
    plugin_root = b.safe_path(plugins_node.get('root'))
    directory = root / plugin_root
    if not directory.resolve().is_relative_to(root) or not directory.is_dir() or directory.is_symlink():
        raise ValueError(f'{root}: invalid plugin root')
    credits_node = node.find('credits')
    locales = {}
    if credits_node is None:
        raise ValueError(f'{root}: missing credits')
    for locale in credits_node.findall('locale'):
        lang = locale.get('lang')
        if lang in locales or not lang:
            raise ValueError(f'{root}: duplicate/invalid credits locale')
        locales[lang] = {tag: ''.join(locale.find(tag).itertext()).strip() if locale.find(tag) is not None else ''
                         for tag in ('button', 'title', 'text')}
        if any(not v for v in locales[lang].values()):
            raise ValueError(f'{root}: incomplete credits for {lang}')
    default = credits_node.get('defaultLocale')
    if not {'en', 'ja'}.issubset(locales) or default not in locales:
        raise ValueError(f'{root}: English/Japanese credits and valid defaultLocale are required')
    notices = [b.safe_path(n.get('path')) for n in node.findall('notice')]
    if not notices:
        raise ValueError(f'{root}: at least one notice is required')
    files = {'plugin-set.xml': metadata}
    for path in notices:
        files[path] = b.checked_read(root, path)
    includes = {}
    for include in node.findall('xml-include'):
        owner = b.safe_path(include.get('manifest')); target = b.safe_path(include.get('path')); entity = include.get('entity')
        if not entity or (owner,entity) in includes: raise ValueError(f'{root}: invalid XML include metadata')
        includes[owner,entity] = target
    plugins, declarations = [], []
    for folder in sorted(directory.iterdir()):
        if not folder.is_dir():
            continue
        if folder.is_symlink():
            raise ValueError(f'{folder}: symlink plugin directories are not supported')
        manifests = [p for p in folder.iterdir() if p.name.casefold() == 'plugin.xml']
        if len(manifests) > 1:
            raise ValueError(f'{folder}: ambiguous plugin.xml')
        if not manifests:
            continue
        path = manifests[0].relative_to(root).as_posix()
        raw = b.checked_read(root, path)
        if len(raw)>XML_LIMIT: raise ValueError(f'{path}: oversized manifest')
        source = raw.decode('utf-8-sig' if source_encoding=='utf-8' else 'cp932')
        local_fragments={}
        def expand_local(match):
            entity,quote,relative = match.groups()
            target = str(PurePosixPath(path).parent / b.safe_path(relative))
            if includes.get((path,entity)) != target:
                raise ValueError(f'{path}: external entities/DTDs require explicit local include metadata: {entity}: {relative}')
            fragment = b.checked_read(root,target)
            if len(fragment)>XML_LIMIT or re.search(br'<!\s*(?:DOCTYPE|ENTITY)',fragment,re.I):
                raise ValueError(f'{target}: nested/oversized XML include')
            files[target] = fragment
            value = re.sub(r'<\?xml.*?\?>','',fragment.decode('utf-8-sig' if source_encoding=='utf-8' else 'cp932'),flags=re.S)
            local_fragments[entity]=value
            return '<!ENTITY '+entity+' '+quote+quote+'>'
        source=re.sub(r'(<\?xml[^>]*encoding=[\'"])[^\'"]+',r'\g<1>utf-8',source)
        source=re.sub(r'<!ENTITY\s+([\w.-]+)\s+SYSTEM\s+([\'"])(.*?)\2\s*>',expand_local,source)
        if local_fragments:
            boundary=source.index(']>')+2
            body=source[boundary:]
            for entity,value in local_fragments.items():
                token='&'+entity+';'
                expanded_size=len(body.encode('utf-8'))+body.count(token)*(len(value.encode('utf-8'))-len(token))
                if expanded_size>32*1024*1024: raise ValueError('Local XML include expansion limit')
                body=body.replace(token,value)
            source=source[:boundary]+body
        manifest,_ = bounded_manifest(source.encode('utf-8'))
        if manifest.tag not in ('plug-in','plugin'):
            raise ValueError(f'{path}: expected plug-in manifest')
        plugin_id = folder.name
        plugins.append(dict(id=plugin_id, path=path, dependencies=sorted(set(['system'] + [d.attrib['on'] for d in manifest.findall('depend')])),
                            scope='in-scope', title=manifest.findtext('title', ''), author=manifest.findtext('author', ''),
                            homepage=manifest.findtext('homepage', '')))
        # Read resources without following symlinks. Nested manifests remain source-only.
        for resource in sorted(folder.rglob('*')):
            if resource.is_file() or resource.is_symlink():
                relative = resource.relative_to(root).as_posix()
                files[relative] = b.checked_read(root, relative)
        for ordinal, declaration in enumerate(manifest.findall('contribution')):
            declarations.append(dict(id=declaration.attrib['id'], pluginId=plugin_id, type=declaration.attrib['type'],
                sourcePath=path, ordinal=ordinal, declaration=b.xml_tree(declaration), status='prepared; gameplay-unavailable'))
    if not plugins:
        raise ValueError(f'{root}: no immediate-child plugin manifests')
    return dict(root=root, id=node.attrib['id'], version=node.attrib['version'],
                profile=node.get('profile', 'online-v2'), sourceRevision=node.get('sourceRevision', 'working-tree'),
                credits=dict(defaultLocale=default, locales=locales), notices=notices, files=files,
                plugins=plugins, contributions=declarations)


def inline_pictures(declaration):
    result=[]
    def visit(node):
        if node['tag']=='picture' and 'src' in node['attributes']: result.append(node)
        for child_node in node['children']: visit(child_node)
    visit(declaration)
    return result

def resolve_optional(files,directory,reference):
    reference=reference.replace('\\','/')
    if not reference or ':' in reference or reference.startswith('/'):
        raise ValueError('Unsafe optional resource reference: '+reference)
    path=posixpath.normpath(posixpath.join(directory,reference))
    b.safe_path(path) # Parent steps may stay inside the set, never escape it.
    matches=[p for p in files if p.casefold()==path.casefold()]
    if not matches:
        matches=[p for p in files if p.casefold()==('trunk/'+path).casefold()]
    if len(matches)!=1: raise ValueError('Missing/ambiguous resource: '+path)
    return matches[0]

def register(rows, key, label):
    result = {}
    for row in rows:
        identity = row[key]
        if identity in result:
            previous = result[identity]
            raise ValueError(f'Duplicate {label} {identity}: {previous.get("sourcePath", previous.get("path", "bundled"))} and {row.get("sourcePath", row.get("path", "bundled"))}')
        result[identity] = row
    return result


def prepare(roots, bundled_catalog, bundled_input, output=None, reference_catalogs=()):
    base_bytes = bundled_catalog.read_bytes()
    base = json.loads(base_bytes)
    references = [json.loads(p.read_bytes()) for p in reference_catalogs]
    sets = sorted([scan(Path(root)) for root in roots], key=lambda s: s['id'])
    register([dict(id=base['packId'])] + [dict(id=r['packId']) for r in references] + sets, 'id', 'set ID')
    base_plugins = base['plugins'] + [p for r in references for p in r['plugins']]
    all_plugins=base_plugins + [p for s in sets for p in s['plugins']]
    known_plugins=register(all_plugins,'id','plugin ID')
    for plugin in all_plugins:
        for dependency in plugin['dependencies']:
            if dependency not in known_plugins: raise ValueError(f'{plugin.get("path",plugin["id"])}: missing plugin dependency {dependency}')
    order = b.plugin_order(all_plugins)
    existing = base['contributions'] + base['factories'] + [d for r in references for d in r['contributions'] + r['factories']]
    declarations = [d for s in sets for d in s['contributions']]
    register(existing + declarations, 'id', 'contribution ID')
    # Exact portable implementations are registered by bundled factories, never by loading DLLs.
    factories = {text(d['declaration'], 'name'): d for d in base['factories']}
    allowed_classes = {d['implementation']['name'] for d in base['contributions'] + base['factories'] if 'implementation' in d}
    allowed_classes.update(n['attributes']['name'] for d in base['factories']
                           for n in children(d['declaration'], 'implementation'))
    errors = []
    for d in declarations:
        try:
            if d['type'] == 'contribution':
                raise ValueError('optional factory registration requires a reviewed portable adapter')
            factory = factories.get(d['type'])
            if not factory:
                raise ValueError('unregistered contribution type: ' + d['type'])
            fixed = child(factory['declaration'], 'implementation', False)
            factory_class = child(factory['declaration'], 'class')['attributes']['name']
            binding = (child(d['declaration'], 'class', False) if factory_class.endswith('.DynamicContributionFactory') else fixed)
            if d['type'] == 'train': binding = child(d['declaration'],'class',False) or dict(attributes=dict(name='freetrain.contributions.train.ParamTrainImpl'))
            if not binding or binding['attributes']['name'] not in allowed_classes:
                raise ValueError('unported qualified implementation: ' + str(binding))
            d['implementation'] = dict(name=binding['attributes']['name'])
            if d['type'] == 'GenericStructure':
                raise ValueError('nonempty GenericStructure generated recipes require a portable adapter')
        except (ValueError, KeyError) as e:
            errors.append(f'{d["sourcePath"]}#{d["ordinal"]} ({d["id"]}): {e}')
    # Resource/reference validation precedes output or cache reuse.
    all_definitions = register(existing + declarations, 'id', 'contribution ID')
    for d in declarations:
        def refs(node):
            for key in ('ref', 'carRef'):
                if key in node['attributes'] and node['attributes'][key] not in all_definitions:
                    errors.append(f'{d["sourcePath"]} ({d["id"]}): unresolved {key}: {node["attributes"][key]}')
            for n in node['children']: refs(n)
        refs(d['declaration'])
    reference_files={}
    for catalog,path in zip(references,reference_catalogs):
        for asset in catalog['assets']:
            if 'sourceFile' not in asset: continue
            raw=b.checked_read(path.parent,asset['sourceFile'])
            if b.digest(raw)!=asset['sourceSha256']: raise ValueError('Prepared reference source hash mismatch: '+asset['path'])
            reference_files[asset['path']]=raw
    resource_files={a['path']:None for a in base['assets']}
    resource_files.update(reference_files)
    resource_files.update({p:data for s in sets for p,data in s['files'].items()})
    generated=[]
    for d in declarations:
        if d['type']=='GenericStructure':
            node=d['declaration']
            colors=children(node,'spriteType')+children(node,'colorVariation')
            sprites=[c for c in node['children'] if c['tag'] in ('picture','sprite','pictures','sprites')]
            for color_index in range(max(1,len(colors))):
                for direction_index in range(len(sprites)):
                    generated.append(dict(id=f'{d["id"]}-{color_index}:{direction_index}',sourcePath=d['sourcePath']))
        for picture_node in inline_pictures(d['declaration']):
            try: b.picture_variants(picture_node,str(PurePosixPath(d['sourcePath']).parent),resource_files,resolve_optional)
            except (ValueError,KeyError) as e: errors.append(f'{d["sourcePath"]} ({d["id"]}): {e}')
    register(existing+declarations+generated,'id','contribution/generated ID')
    if errors:
        raise ValueError('Optional content validation failed (no packs published):\n'+'\n'.join(sorted(set(errors))))
    converter = b.digest(b''.join(Path(__file__).with_name(p).read_bytes() for p in
                         ('prepare-optional-content.py', 'prepare-bundled-content.py', 'content_bitmap.py', 'content_recipes.py', 'content_xml.py')))
    source_hashes = [dict(id=s['id'], files={p: b.digest(v) for p,v in sorted(s['files'].items())}) for s in sets]
    context_hash = b.digest(b.dump(dict(converter=converter, bundled=b.digest(base_bytes), sources=source_hashes,
                                      references=[b.digest(p.read_bytes()) for p in reference_catalogs])))
    completed = []
    for s in sets:
        prefix = '/api/content/' + s['id'] + '/' + context_hash + '/'
        artifacts, assets, pictures = {}, [], []
        def artifact(data, extension, source_path, treatment):
            name = str(PurePosixPath(source_path).parent) + '/media/' + PurePosixPath(source_path).name + '.' + treatment + '.' + b.digest(data) + '.' + extension
            artifacts[name] = data
            return dict(url=prefix+name, sha256=b.digest(data), bytes=len(data))
        assets_by_path = {}
        def get_asset(path):
            if path not in assets_by_path:
                raw = resource_files[path] if resource_files[path] is not None else b.checked_read(bundled_input,'source/'+path)
                w,h,pixels = decode(raw)
                source_file='sources/'+path
                artifacts[source_file]=raw
                plugin_id=PurePosixPath(path).parts[2 if path.startswith('trunk/') else 1]
                a = dict(path=path,pluginId=plugin_id,mediaType='bmp',sourceSha256=b.digest(raw),sourceFile=source_file,width=w,height=h,
                         author=next((p['author'] for p in base_plugins + [p for other in sets for p in other['plugins']] if p['id']==plugin_id),''),
                         attributionStatus='declared plugin author; individual image authorship not independently verified',
                         day=artifact(png(w,h,surface(pixels)),'png',path,'day'),
                         night=artifact(png(w,h,surface(pixels,True)),'png',path,'night'))
                assets_by_path[path] = a; assets.append(a)
            return assets_by_path[path]
        for d in s['contributions']:
            inline=inline_pictures(d['declaration'])
            if not inline: continue
            try:
                if any(node!=inline[0] for node in inline[1:]): raise ValueError('multiple distinct inline pictures require a portable picture binding')
                variants = b.picture_variants(inline[0], str(PurePosixPath(d['sourcePath']).parent), resource_files,resolve_optional)
                for v in variants:
                    for mode in ('day','night'):
                        value = v[mode]; a = get_asset(value['path'])
                        value['image'] = a['night' if value['treatment']=='automatic-night-rgb888-v1' else 'day']
                pictures.append(dict(id=d['id'],status='prepared-picture',variants=variants))
            except (ValueError, KeyError) as e:
                errors.append(f'{d["sourcePath"]} ({d["id"]}): {e}')
        for path,raw in sorted(s['files'].items()):
            extension=PurePosixPath(path).suffix.lower().lstrip('.')
            if extension in ('mid','midi','wav'):
                assets.append(dict(path=path,mediaType=extension,sourceSha256=b.digest(raw),
                    status='retained-audio; playback-adapter-pending',native=artifact(raw,extension,path,'native')))
        catalog = dict(schemaVersion=2,packId=s['id'],version=s['version'],source=dict(revision=s['sourceRevision'],profile=s['profile']),
                       status='prepared; not selected by a world profile',plugins=s['plugins'],pluginOrder=[p for p in order if p in {r['id'] for r in s['plugins']}],
                       contributions=s['contributions'],factories=[],pictures=pictures,selections=[],assets=assets)
        s['catalog'], s['artifacts'] = catalog, artifacts
    if errors:
        raise ValueError('Optional content validation failed (no packs published):\n'+'\n'.join(sorted(set(errors))))
    combined = dict(contributions=base['contributions'] + [d for r in references for d in r['contributions']] + declarations,
                    pictures=base['pictures'] + [p for r in references for p in r['pictures']] + [p for s in sets for p in s['catalog']['pictures']],
                    assets=base['assets'] + [a for r in references for a in r['assets']] + [a for s in sets for a in s['catalog']['assets']])
    register(combined['pictures'],'id','picture ID')
    shared_files = dict(reference_files)
    shared_files.update({path:data for s in sets for path,data in s['files'].items()})
    # Only needed for new recipes using pinned bundled art, never optional source discovery.
    for picture in base['pictures']:
        for v in picture.get('variants',[]):
            for mode in ('day','night'):
                path = v[mode]['path']
                if path not in shared_files: shared_files[path] = b.checked_read(bundled_input, 'source/'+path)
    try:
        compiled = Compiler(combined).run()
    except (ValueError, KeyError, IndexError, TypeError) as e:
        raise ValueError('Optional recipe validation failed: '+str(e)) from e
    for s in sets:
        catalog,artifacts = s['catalog'],s['artifacts']
        # Bind the closure explicitly: callbacks must retain each set's namespace.
        prefix = '/api/content/' + s['id'] + '/' + context_hash + '/'
        def artifact(data,extension,source_path,treatment):
            name=str(PurePosixPath(source_path).parent)+'/media/'+PurePosixPath(source_path).name+'.'+treatment+'.'+b.digest(data)+'.'+extension
            artifacts[name]=data
            return dict(url=prefix+name,sha256=b.digest(data),bytes=len(data))
        recipes=[r for r in compiled if r['id'] in {d['id'] for d in s['contributions']}]
        try:
            surfaces_catalog = dict(combined, renderRecipes=recipes)
            b.render_surfaces(surfaces_catalog,shared_files,artifact)
            catalog.update(renderRecipes=recipes,surfaceSets=surfaces_catalog['surfaceSets'],masks=surfaces_catalog['masks'])
        except (ValueError,KeyError,IndexError,TypeError) as e:
            errors.append(f'{s["id"]}: {e}')
        if errors: continue
        artifacts['catalog.json'] = b.dump(catalog)
        notices=[]
        notice_paths=sorted(set(s['notices']) | {p for p in s['files'] if PurePosixPath(p).suffix.lower() in ('.txt','.md','.html','.htm') or re.search(r'(?i)^(copying|licen[cs]e|notice)$',PurePosixPath(p).name)})
        notice_types={}
        for path in notice_paths:
            name='notices/'+path; artifacts[name]=s['files'][path]
            try: artifacts[name].decode('utf-8-sig'); charset='utf-8'
            except UnicodeError:
                try: artifacts[name].decode('cp932'); charset='shift_jis'
                except UnicodeError: charset=None
            notice_types[name]='text/plain; charset='+charset if charset else 'application/octet-stream'
            notices.append(dict(path=path,url=prefix+name,sha256=b.digest(artifacts[name])))
        dependencies=[dict(id=base['packId'],version=base['version'],catalogHash=b.digest(base_bytes))]
        dependencies += [dict(id=r['packId'],version=r['version'],catalogHash=b.digest(p.read_bytes())) for r,p in zip(references,reference_catalogs)]
        manifest=dict(schemaVersion=1,id=s['id'],version=s['version'],sourceRevision=s['sourceRevision'],profile=s['profile'],
            contentKey=context_hash,converterHash=converter,sourceHashes=next(r['files'] for r in source_hashes if r['id']==s['id']),
            dependencies=dependencies,credits=s['credits'],notices=notices,
            files={path:dict(sha256=b.digest(data),bytes=len(data),**({'contentType':notice_types[path]} if path in notice_types else {})) for path,data in sorted(artifacts.items())})
        s['manifest'] = manifest
        artifacts['prepared-pack.json'] = b.dump(manifest)
        destination = (Path(output) / s['id'] / context_hash) if output else (s['root'] / '.freetrain-online' / 'prepared' / context_hash)
        completed.append((destination,artifacts))
    if errors:
        raise ValueError('Optional content validation failed (no packs published):\n'+'\n'.join(sorted(set(errors))))
    for s in sets:
        # Pin all jointly prepared sets. Catalogs never contain local paths.
        s['manifest']['dependencies'] += [dict(id=other['id'],version=other['version'],catalogHash=b.digest(other['artifacts']['catalog.json'])) for other in sets if other is not s]
        s['artifacts']['prepared-pack.json']=b.dump(s['manifest'])
    # Validate every set before atomically publishing immutable directories.
    for destination, artifacts in completed:
        if destination.resolve().is_relative_to(b.ROOT.resolve()):
            raise ValueError('Optional output must be outside the core repository: '+str(destination))
        if destination.exists():
            for path,data in artifacts.items():
                if b.checked_read(destination,path)!=data: raise ValueError('Prepared cache drift: '+str(destination/path))
            continue
        destination.parent.mkdir(parents=True,exist_ok=True)
        with tempfile.TemporaryDirectory(prefix='.preparing-',dir=destination.parent) as temporary:
            stage=Path(temporary)/'pack'; stage.mkdir()
            for path,data in artifacts.items():
                target=stage/path; target.parent.mkdir(parents=True,exist_ok=True); target.write_bytes(data)
            try: os.rename(stage,destination)
            except FileExistsError:
                for path,data in artifacts.items():
                    if b.checked_read(destination,path)!=data: raise ValueError('Concurrent cache drift')
    return [str(p.resolve()) for p,_ in completed]


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plugin-dir',type=Path,action='append',required=True)
    parser.add_argument('--output-dir',type=Path)
    parser.add_argument('--bundled-catalog',type=Path,default=b.CATALOG)
    parser.add_argument('--bundled-input',type=Path,default=b.INPUT)
    parser.add_argument('--reference-catalog',type=Path,action='append',default=[])
    args=parser.parse_args()
    print(json.dumps(prepare(args.plugin_dir,args.bundled_catalog,args.bundled_input,args.output_dir,args.reference_catalog)))

if __name__=='__main__':
    try: main()
    except (ValueError,OSError,KeyError,ET.ParseError,UnicodeError) as error:
        print(str(error),file=sys.stderr); sys.exit(1)
