#!/usr/bin/env python3
"""Reconcile base content against pinned source; never load legacy assemblies.

This is a source specification, not an importer or a historical-save reader.
Reviewed rules live in doc/parity/content-rules.json. Review artifacts go in tmp/.
"""
import argparse
import collections
import importlib.util
import json
import posixpath
import re
import struct
from pathlib import Path
import xml.etree.ElementTree as ET

SPEC = importlib.util.spec_from_file_location('inventory', Path(__file__).with_name('inventory-parity.py'))
inv = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(inv)
OUTPUT = inv.OUT / 'base-content-review.json'


def xml_tree(node):
    return dict(tag=node.tag, attributes=dict(node.attrib), text=(node.text or '').strip(),
                children=[xml_tree(child) for child in node])


def masked(code):
    # Keep character positions/newlines while making strings inert for brace scans.
    return re.sub(r'@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'',
                  lambda m: re.sub(r'[^\n]', ' ', m[0]), code)


def source_code(text):
    pattern = r'@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|//[^\n]*|/\*.*?\*/'
    text = re.sub(pattern, lambda m: re.sub(r'[^\n]', ' ', m[0])
                  if m[0].startswith(('//', '/*')) else m[0], text, flags=re.S)
    return re.sub(r'(?m)^\s*#[^\n]*', lambda m: re.sub(r'[^\n]', ' ', m[0]), text)


def closing(code, start):
    depth = 0
    for i in range(start, len(code)):
        if code[i] == '{':
            depth += 1
        elif code[i] == '}':
            depth -= 1
            if depth == 0:
                return i
    raise ValueError('Unclosed source block')


def source_types(tree):
    """Index declarations and direct fields, retaining exact source and line evidence.

    Deliberately lexical: field/type tokens are not a CLR graph or a save allowlist.
    Nested types get their own scope; methods/properties do not become fields.
    """
    types = []
    for path in sorted(tree.entries):
        if not path.startswith(('core/', 'plugins/', 'lib/')) or not path.endswith('.cs'):
            continue
        text, encoding = tree.text(path)
        code = source_code(text)
        safe = masked(code)
        namespaces = [(m[1], m.end() - 1, closing(safe, m.end() - 1))
                      for m in re.finditer(r'\bnamespace\s+([\w.]+)\s*\{', safe)]
        found = []
        pattern = r'\b(class|struct|interface|enum)\s+(\w+)([^{};]*)\{'
        for m in re.finditer(pattern, safe):
            start, end = m.end() - 1, closing(safe, m.end() - 1)
            namespace = '.'.join(n for n, a, b in namespaces if a < start < b)
            parents = [name for name, a, b in found if a < start < b]
            name = namespace + '.' + '+'.join([*parents, m[2]])
            found.append((m[2], start, end))
            # A direct member is terminated by ; or a brace block. Initializer
            # blocks remain part of fields; method/property/nested bodies are skipped.
            fields, members = [], []
            cursor = start + 1
            i = cursor
            while i < end:
                if safe[i] == '{':
                    head = code[cursor:i].strip()
                    stop = closing(safe, i)
                    if '=' in head and not re.search(r'\b(class|struct|enum|interface)\b', head):
                        i = stop + 1
                        continue
                    if head:
                        members.append(dict(signature=' '.join(head.split()),
                                            line=code.count('\n', 0, cursor) + 1))
                    cursor = stop + 1
                    i = cursor
                    continue
                if safe[i] == ';':
                    raw = code[cursor:i + 1].strip()
                    declaration = re.sub(r'\[[^\]]*\]', '', raw).strip()
                    # Only the declaration before '=' matters: constructor calls
                    # in a field initializer must not turn a field into a method.
                    head = declaration.split('=', 1)[0]
                    if head.strip(';').strip() and '(' not in head and not re.search(r'\b(delegate|event|using)\b', head):
                        storage = 'static' if re.search(r'\b(static|const)\b', head) else 'nonserialized' if '[NonSerialized]' in raw else 'instance'
                        fields.append(dict(declaration=' '.join(raw.split()), storage=storage,
                                           line=code.count('\n', 0, cursor) + 1))
                    cursor = i + 1
                i += 1
            types.append(dict(coverageId=f'base:type:{path}:{name}', type=name, path=path, sha256=inv.digest(tree.read(path)),
                              encoding=encoding, kind=m[1], bases=' '.join(m[3].strip().lstrip(':').split()),
                              declaredSerializable=bool(re.search(r'\[Serializable\]\s*(?:(?:public|internal|protected|private|sealed|abstract)\s+)*$', code[:m.start()])),
                              line=code.count('\n', 0, m.start()) + 1,
                              fields=fields, members=members))
    return types


def projects(tree):
    result = []
    for path in sorted(tree.entries):
        if not path.startswith(('core/', 'plugins/', 'lib/')) or not path.endswith('.csproj'):
            continue
        root = ET.fromstring(tree.read(path))
        elements = list(root.iter())
        names = sorted({e.text for e in elements if e.tag.split('}')[-1] == 'AssemblyName' and e.text})
        compiled, embedded, content = [], [], []
        for node in elements:
            tag = node.tag.split('}')[-1]
            if tag in ('Compile', 'EmbeddedResource', 'Content', 'None') and node.get('Include'):
                matches, resolution = inv.resolve_path(tree, path, node.get('Include'))
                (compiled if tag == 'Compile' else embedded if tag == 'EmbeddedResource' else content).append(dict(
                    include=node.get('Include'), paths=matches, resolution=resolution))
        result.append(dict(path=path, sha256=inv.digest(tree.read(path)), assemblies=names,
                           compile=compiled, embeddedResources=embedded, content=content))
    return result


def resolve_type(attributes, types, project_rows):
    name = attributes.get('name', '')
    assembly = posixpath.basename(attributes.get('codebase', 'FreeTrain.Core.dll').replace('\\', '/'))
    assembly = re.sub(r'\.dll$', '', assembly, flags=re.I)
    builds = [p for p in project_rows if assembly.casefold() in [n.casefold() for n in p['assemblies']]]
    compiled = {path for p in builds for entry in p['compile'] for path in entry['paths']}
    exact = [t for t in types if t['type'] == name and t['path'] in compiled]
    other = [t for t in types if t['type'] == name and t not in exact]
    return dict(name=name, codebase=attributes.get('codebase'), assembly=assembly,
                projects=[p['path'] for p in builds], sources=[t['path'] for t in exact],
                otherSources=[t['path'] for t in other],
                resolution='source-and-project' if len(exact) == 1 else 'ambiguous' if exact else 'baseline-type-or-build-defect')


def bmp_metadata(data):
    if data[:2] != b'BM' or len(data) < 54:
        raise ValueError('Unsupported BMP header')
    dib = struct.unpack_from('<I', data, 14)[0]
    if dib < 40:
        raise ValueError('Unsupported BMP DIB')
    width, height, planes, depth, compression = struct.unpack_from('<iiHHI', data, 18)
    return dict(width=width, height=abs(height), topDown=height < 0,
                bitsPerPixel=depth, compression=compression, planes=planes)


def indirect_graph(tree, project_rows, rules):
    edges = []
    literal = r'@?"([^"\n]*\.(?:bmp|wav|mid|xml|ico|png|gif|jpg|html))"'
    for path in sorted(tree.entries):
        if not path.startswith(('core/', 'plugins/', 'lib/')):
            continue
        if path.endswith('.cs'):
            code = source_code(tree.text(path)[0])
            for m in re.finditer(literal, code, re.I):
                value = m[1]
                if any(c in value for c in '*|;') or value.startswith('.'):
                    continue  # dialog filters/extensions, not resource loads
                if path.startswith('plugins/org.kohsuke.freetrain.music.a4mem/'):
                    targets, resolution = [], 'excluded-external-a4'
                elif path.endswith('/VCRConsole.cs'):
                    targets, _ = inv.resolve_path(tree, path, '../resource/icons.bmp')
                    resolution = 'excluded-embedded-resource'
                elif path.startswith('plugins/') or path in rules['sourceLoadRoots']:
                    root = rules['sourceLoadRoots'].get(path, '/'.join(path.split('/')[:2]))
                    targets, resolution = inv.resolve_path(tree, root + '/plugin.xml', value)
                else:
                    targets, resolution = inv.resolve_path(tree, 'core/res/anchor', value)
                if value == 'plugin.xml':
                    resolution, targets = 'manifest-discovery', []
                edges.append(dict(owner=path, line=code.count('\n', 0, m.start()) + 1,
                                  reference=value, targets=targets, resolution=resolution,
                                  evidence='source-literal; dynamic load rules in content-rules.json'))
        elif path.endswith(('.html', '.htm', '.xml')) and not path.lower().endswith('/plugin.xml'):
            text = tree.text(path)[0]
            for m in re.finditer(r'(?:src|href)\s*=\s*[\'"]([^\'"]+)[\'"]', text, re.I):
                value = m[1]
                if value.startswith('#'):
                    continue
                targets, resolution = inv.resolve_path(tree, path, value.split('#')[0])
                edges.append(dict(owner=path, reference=value, targets=targets, resolution=resolution,
                                  evidence='document-resource-reference'))
    for project in project_rows:
        for entry in project['embeddedResources']:
            edges.append(dict(owner=project['path'], reference=entry['include'], targets=entry['paths'],
                              resolution='project-embedded-' + entry['resolution'], evidence='project-resource-item'))
        for entry in project['content']:
            edges.append(dict(owner=project['path'], reference=entry['include'], targets=entry['paths'],
                              resolution='project-content-' + entry['resolution'], evidence='project-content-item; does not prove runtime use'))
    # WinForms .resx streams can contain images even with no freestanding filename.
    embedded = []
    for path in sorted(tree.entries):
        if not path.startswith(('core/', 'plugins/', 'lib/')) or not path.endswith('.resx'):
            continue
        root = ET.fromstring(tree.read(path))
        for node in root.findall('data'):
            kind = node.get('type', '')
            mime = node.get('mimetype', '')
            if 'ResXFileRef' in kind:
                value = node.findtext('value', '').split(';')[0]
                targets, resolution = inv.resolve_path(tree, path, value)
                edges.append(dict(owner=path, reference=value, targets=targets, resolution=resolution,
                                  evidence='resx-file-reference'))
            if mime or any(s in kind for s in ('Bitmap', 'Icon', 'Image')):
                value = node.findtext('value', '')
                embedded.append(dict(path=path, fileSha256=inv.digest(tree.read(path)), name=node.get('name'),
                                     type=kind, mimeType=mime, valueSha256=inv.digest(value.encode()),
                                     disposition='WinForms embedded resource; preserve role, replace chrome; no CLR activation'))
    return edges, embedded


def reconcile(tree, base, rules):
    types, builds = source_types(tree), projects(tree)
    bindings, recipes, refs, aliases, local_children = [], [], [], [], []
    pictures, picture_refs = [], []
    declarations = {r['coverageId']: r for r in base['contributions'] if not r.get('generatedFrom')}
    ids = {r['contributionId'] for r in base['contributions'] if r['contributionType'] != 'contribution'}
    plugin_paths = {p['pluginId']: p['baselinePath'] for p in base['plugins']}
    nodes = {}
    for plugin, path in plugin_paths.items():
        root, _ = inv.manifest(tree.read(path))
        for index, node in enumerate(root.findall('contribution')):
            key = f'base:contribution:{plugin}:{index}:{node.get("id")}'
            nodes[key] = node
            for element in node.iter():
                if element.tag in ('class', 'implementation'):
                    bindings.append(dict(owner=key, role=element.tag,
                                         **resolve_type(element.attrib, types, builds)))
    binding_by_owner = collections.defaultdict(list)
    for binding in bindings:
        binding_by_owner[binding['owner']].append(binding)
    for key, row in declarations.items():
        node = nodes[key]
        kind = row['contributionType']
        definitions = base['factories'].get(kind, [])
        implementation = row['implementation']
        if kind != 'contribution' and not implementation:
            if kind == 'train':
                implementation = dict(name='freetrain.contributions.train.ParamTrainImpl')
            elif kind == 'railSignal':
                implementation = dict(name='freetrain.world.rail.signal.RailSignalContribution',
                                      codebase='FreeTrain.RailSignal.dll')
            elif len(definitions) == 1:
                implementation = definitions[0]['implementation']
        type_binding = resolve_type(implementation, types, builds) if implementation else None
        dependencies = set(row['dependencies'])
        for element in node.iter():
            if (element.tag == 'picture' and element.get('src') or
                    kind == 'HalfVoxelStructure' and element.tag == 'highlight' and element.get('src')):
                picture_id = row['contributionId'] + ('#HL' if element.tag == 'highlight' else '')
                pictures.append(dict(owner=key, pictureId=picture_id, source='manifest-picture',
                                     declaration=xml_tree(element)))
            if element.tag == 'picture' and element.get('ref'):
                picture_refs.append(dict(owner=key, target=element.get('ref')))
            if element.tag in ('spriteType', 'colorVariation') and element.get('name'):
                dependencies.add('spriteFactory:' + element.get('name'))
            if element.get('loader'):
                dependencies.add('spriteLoader:' + element.get('loader'))
            if kind == 'HalfVoxelStructure' and element.tag == 'map' and element.get('to'):
                dependencies.add(element.get('to'))
        if node.find('sprite') is not None or node.find('sprites') is not None:
            dependencies.add('spriteLoader:default')
        for lands in node.findall('lands'):
            dependencies.update((lands.text or '').split())
        for dependency in sorted(dependencies):
            refs.append(dict(owner=key, target=dependency,
                             resolution='registered' if dependency in ids else 'baseline-reference-defect'))
        # BGM stores href as element text, not an attribute. Keep this distinct
        # from sound/@href and from a plugin homepage hyperlink.
        for href in node.findall('href'):
            targets, resolution = inv.resolve_path(tree, row['baselinePath'], href.text.strip())
            refs.append(dict(owner=key, target=href.text.strip(), targets=targets,
                             resolution=resolution, kind='manifest-text-resource'))
        for data in node.findall('data'):
            if data.get('file'):
                targets, resolution = inv.resolve_path(tree, row['baselinePath'], data.get('file'))
                refs.append(dict(owner=key, target=data.get('file'), targets=targets,
                                 resolution=resolution, kind='manifest-data-resource'))
        if kind == 'GenericStructure':
            color_count, sprites = inv.child_recipe(node)
            directions = inv.direction_aliases(sprites)
            for color in range(color_count):
                for direction, selected in enumerate(directions):
                    aliases.append(dict(owner=key, colorIndex=color, direction=direction,
                                        target=f'{row["contributionId"]}-{color}:{selected}',
                                        registeredAlias=False,
                                        resolution='registered' if selected < len(sprites) else 'invalid-source-fallback'))
        if implementation.get('name', '').endswith('.ReverseTrainCarImpl'):
            aliases.append(dict(owner=key, target=node.find('base').get('carRef'),
                                registeredAlias=True, transform='level angle XOR 8; slope opposite direction and inverted climb',
                                resolution='registered'))
        if row['pluginId'] == 'org.kohsuke.freetrain.music.a4mem' and kind == 'bgmFactory':
            source = 'plugins/org.kohsuke.freetrain.music.a4mem/src/FactoryImpl.cs'
            for filename, title in re.findall(r'create\("([^\"]+)","([^\"]+)"\)', source_code(tree.text(source)[0])):
                local_children.append(dict(owner=key, contributionId=row['contributionId'] + '-' + filename,
                                           filename=filename, title=title, scope='excluded',
                                           registeredInGlobalCatalog=False, reachability='optional external installation; plugin-local list only',
                                           evidence=source, sha256=inv.digest(tree.read(source))))
        recipes.append(dict(coverageId=key, contributionId=row['contributionId'],
                            pluginId=row['pluginId'], scope=row['scope'], contributionType=kind,
                            factoryDefinitions=definitions, effectiveImplementation=type_binding,
                            # Full trees preserve nesting, order, defaults and all unrecognized fields.
                            declaration=xml_tree(node), generatedChildren=[c['coverageId'] for c in base['contributions'] if c.get('generatedFrom') == key],
                            ruleIds=rules['typeRules'][kind], stateRuleIds=[r['id'] for r in rules['stateRules']],
                            serialization='factory-definition; not registered as contribution' if kind == 'contribution' else 'Contribution.ReferenceImpl(id); exceptions and world instance state in stateRules',
                            status='source-reconciled; runtime parity unverified'))
    indirect, embedded = indirect_graph(tree, builds, rules)
    indirect.extend(dict(owner=r['owner'], reference=r['target'], targets=r['targets'],
                         resolution=r['resolution'], evidence='BGMContribution href element')
                    for r in refs if r.get('kind') in ('manifest-text-resource', 'manifest-data-resource'))
    source_refs = []
    for path in sorted(tree.entries):
        if not path.startswith(('core/', 'plugins/')) or not path.endswith('.cs'):
            continue
        code = source_code(tree.text(path)[0])
        for m in re.finditer(r'loadSystemPicture\s*\(\s*"([^\"]+)"', code):
            pictures.append(dict(owner=path, pictureId='{8AD4EF28-CBEF-4C73-A8FF-5772B87EF005}:' + m[1],
                                 source='cached-system-picture', resource=m[1]))
        for m in re.finditer(r'(?<!\w)loadPicture\s*\(\s*"([^\"]+)"', code):
            for recipe in recipes:
                binding = recipe['effectiveImplementation']
                if binding and path in binding['sources']:
                    pictures.append(dict(owner=recipe['coverageId'], pictureId=recipe['contributionId'] + ':' + m[1],
                                         source='contribution-loadPicture', resource=m[1], evidence=path))
        for m in re.finditer(r'new\s+Picture\s*\(\s*"([^\"]+)"', code):
            pictures.append(dict(owner=path, pictureId=m[1], source='source-picture-literal'))
        constants = dict(re.findall(r'\bstring\s+(\w+)\s*=\s*"([^\"]+)"\s*;', code))
        for m in re.finditer(r'(PictureManager\.get|getContribution)\s*\(\s*("[^\"]+"|\w+)\s*\)', code):
            argument = m[2]
            target = argument[1:-1] if argument.startswith('"') else constants.get(argument)
            if not target:
                continue
            source_refs.append(dict(owner=path, line=code.count('\n', 0, m.start()) + 1,
                                    target=target, registry='picture' if m[1].startswith('PictureManager') else 'contribution',
                                    resolution='registered' if target in ids else 'unresolved-source-reference'))
    picture_ids = {p['pictureId'] for p in pictures}
    picture_owners = collections.defaultdict(list)
    for picture in pictures:
        if picture['source'] != 'cached-system-picture':
            picture_owners[picture['pictureId']].append(picture['owner'])
    if any(len(owners) != 1 for owners in picture_owners.values()):
        raise ValueError('Duplicate picture identity outside cached system loads')
    for ref in picture_refs:
        ref['resolution'] = 'registered' if ref['target'] in picture_ids else 'unresolved-picture-reference'
    for ref in source_refs:
        if ref['registry'] == 'picture':
            ref['resolution'] = 'registered' if ref['target'] in picture_ids else 'unresolved-picture-reference'
    # Retain every manifest file edge. DLLs are explained by BOTH type and project,
    # not by a basename match or by suppressing the original missing-file issue.
    manifest_edges = []
    for edge in base['resourceEdges']:
        matches = [b for b in binding_by_owner[edge['owner']] if b['codebase'] == edge['value']]
        manifest_edges.append(dict(**edge, sourceBindings=matches,
                                   disposition='legacy-build-output' if matches and all(b['resolution'] == 'source-and-project' for b in matches)
                                   else 'baseline-type-or-build-defect' if matches else edge['resolution']))
    incoming = collections.defaultdict(list)
    for edge in [*manifest_edges, *indirect]:
        for path in edge['targets']:
            incoming[path].append(edge['owner'])
    notices = []
    for path in sorted(tree.entries):
        if re.search(r'(?:^|/)(?:readme[^/]*|copying[^/]*|licen[cs]e[^/]*|notice[^/]*)$', path, re.I) and (path.startswith(('core/', 'plugins/')) or '/' not in path):
            notices.append(dict(path=path, sha256=inv.digest(tree.read(path))))
    assets = []
    for asset in base['assets']:
        plugin = asset['pluginId']
        plugin_row = next((p for p in base['plugins'] if p['pluginId'] == plugin), None)
        path = asset['baselinePath']
        notice_paths = [n for n in notices if '/' not in n['path'] or plugin and n['path'].startswith('plugins/' + plugin + '/')]
        credits = [dict(coverageId=key, author=declarations[key]['author'])
                   for key in sorted(set(incoming[path])) if key in declarations]
        assets.append(dict(coverageId=asset['coverageId'], baselinePath=path, sha256=asset['sha256'],
                           bytes=asset['bytes'], mediaType=asset['mediaType'], scope=asset['scope'],
                           dimensions=bmp_metadata(tree.read(path)) if asset['mediaType'] == 'bmp' else None,
                           declaredPluginAuthor=plugin_row['author'] if plugin_row else None,
                           directContributionCredits=credits, notices=notice_paths,
                           attributionDisposition='manifest credits retained; no per-file author declaration found' if plugin else 'core resource; repository notice applies; no per-file author declaration found',
                           incomingOwners=sorted(set(incoming[path])),
                           reachability='referenced' if incoming[path] else rules['unreferencedAssets'].get(path, 'UNREVIEWED'),
                           decodingRule='bitmap-surface' if asset['mediaType'] == 'bmp' else 'audio-segment' if asset['mediaType'] in ('wav', 'mid', 'midi') else 'ui-image',
                           cropAnchorRule='consumer recipe/source; never trim transparent margins; see recipes and rule sources'))
    unknown = [a['baselinePath'] for a in assets if a['reachability'] == 'UNREVIEWED']
    if unknown:
        raise ValueError('Unclassified unreferenced assets: ' + ', '.join(unknown))
    stale = set(rules['unreferencedAssets']) - {a['baselinePath'] for a in assets if not a['incomingOwners']}
    if stale:
        raise ValueError('Stale unreferenced-asset dispositions: ' + ', '.join(sorted(stale)))
    if set(rules['typeRules']) != {r['contributionType'] for r in declarations.values()}:
        raise ValueError('Recipe type rules do not reconcile with declarations')
    rule_ids = {r['id'] for r in rules['rules']}
    if rules['baselineRevision'] != tree.revision or any(set(v) - rule_ids for v in rules['typeRules'].values()):
        raise ValueError('Rule baseline/IDs do not reconcile')
    for group in ('rules', 'stateRules'):
        for rule in rules[group]:
            rule['sourceHashes'] = [dict(path=p, sha256=inv.digest(tree.read(p))) for p in rule['sources']]
    for difference in rules['differences']:
        difference['sourceHashes'] = [dict(path=p, sha256=inv.digest(tree.read(p))) for p in difference['sources']]
    diagnostics = [dict(owner=e['owner'], reference=e['reference'], resolution=e['resolution'])
                   for e in indirect if e['resolution'].endswith(('missing', 'ambiguous'))]
    if diagnostics != rules['expectedResourceDiagnostics']:
        raise ValueError('Resource diagnostics differ from reviewed dispositions: ' + repr(diagnostics))
    if any(b['resolution'] != 'source-and-project' for b in bindings):
        raise ValueError('Unreconciled manifest type/build binding')
    if any(r['resolution'].endswith(('defect', 'reference')) for r in [*refs, *source_refs, *picture_refs]):
        raise ValueError('Unreconciled contribution/picture reference')
    if any(a['resolution'] != 'registered' for a in aliases):
        raise ValueError('Unreconciled alias target')
    resources = sorted({path for edge in [*manifest_edges, *indirect] for path in edge['targets']})
    return dict(schemaVersion=1, packId='base', revision=tree.revision, metadataOnly=True,
                status='P0 source reconciliation; no runtime/content/save implementation',
                counts=dict(manifests=len(base['plugins']), declarations=len(recipes),
                            registeredGeneratedChildren=base['counts']['generated'], assets=len(assets),
                            manifestFileEdges=len(manifest_edges), typeBindings=len(bindings),
                            indirectEdges=len(indirect), embeddedResources=len(embedded), aliases=len(aliases),
                            excludedLocalMusicChildren=len(local_children), pictureIdentities=len(picture_ids),
                            pictureReferences=len(picture_refs)),
                rules=rules, manifests=[dict(coverageId=p['coverageId'], baselinePath=p['baselinePath'],
                    sha256=p['sha256'], author=p['author'], scope=p['scope'], differences=p['differences'],
                    declarations=[r['coverageId'] for r in recipes if r['pluginId'] == p['pluginId']],
                    disposition='excluded; retain inventory only' if p['scope'] == 'excluded' else 'source catalog; port behavior under listed recipe/state rules') for p in base['plugins']],
                recipes=recipes, typeBindings=bindings, projects=builds, manifestFileEdges=manifest_edges,
                contributionReferences=refs, sourceReferenceEdges=source_refs, aliases=aliases,
                pictureRegistrations=pictures, pictureReferences=picture_refs,
                excludedLocalMusicChildren=local_children, indirectResourceEdges=indirect, embeddedResources=embedded,
                referencedFiles=[dict(path=p, sha256=inv.digest(tree.read(p)), bytes=len(tree.read(p))) for p in resources],
                assets=assets, sourceTypes=types, notices=notices,
                resourceDiagnostics=diagnostics, differences=rules['differences'])


def report(review):
    lines = ['# P0-02 base content reconciliation', '',
             f'Pinned baseline: `{review["revision"]}`. Source evidence only; runtime parity is unverified.', '',
             '| Measure | Count |', '| --- | ---: |']
    lines += [f'| {key} | {value} |' for key, value in review['counts'].items()]
    lines += ['', '## Type/build bindings', '']
    for resolution, count in sorted(collections.Counter(b['resolution'] for b in review['typeBindings']).items()):
        lines.append(f'- {resolution}: {count}')
    lines += ['', '## Recorded differences', '']
    for row in review['differences']:
        lines.append(f'- **{row["id"]}**: {row["finding"]} {row["disposition"]}')
    lines += ['', '## Asset reachability', '']
    for value, count in sorted(collections.Counter(a['reachability'] for a in review['assets']).items()):
        lines.append(f'- {value}: {count}')
    return ('\n'.join(lines) + '\n').encode()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--report', type=Path)
    args = parser.parse_args()
    if args.report and not args.report.resolve().is_relative_to((inv.ROOT / 'tmp').resolve()):
        parser.error('--report must be under tmp/')
    base = json.loads((inv.OUT / 'base-ledger.json').read_text())
    config = json.loads(inv.CONFIG.read_text())
    if base['revision'] != config['baseRevision']:
        raise ValueError('Base ledger revision differs from pinned baseline')
    tree = inv.Tree(inv.ROOT, config['baseRevision'])
    rules = json.loads((inv.OUT / 'content-rules.json').read_text())
    review = reconcile(tree, base, rules)
    inv.write(OUTPUT, inv.dump_ledger(review), args.check)
    if args.report:
        inv.write(args.report, report(review))
    print(json.dumps(review['counts']))


if __name__ == '__main__':
    main()
