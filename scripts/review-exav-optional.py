#!/usr/bin/env python3
"""P0-08 metadata review of explicitly supplied optional and stable ExAV Git trees.

No sibling discovery, legacy execution, asset conversion or source-tree writes.
The six local XML fragments are expanded from pinned objects only.
"""
import argparse
import collections
import importlib.util
import json
import posixpath
import re
from pathlib import Path
import xml.etree.ElementTree as ET

SPEC = importlib.util.spec_from_file_location('exav_content', Path(__file__).with_name('reconcile-exav-content.py'))
content = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(content)
inv = content.inv
RULES = inv.PARITY_ROOT / 'exav-optional-rules.json'
OUTPUT = inv.PARITY_ROOT / 'exav-optional-review.json'
LOCAL_INCLUDES = {
    'plugins/1703d.E233_keiyo/plugin.xml': {
        'E233/E233keiyo.xml', 'E233/E233keiyo2.xml', '209/209keiyo.xml',
        '209/209sobu.xml', '209/209kt.xml'},
    'plugins/1703d.keisei.AE/plugin.xml': {'AE/AE.xml'},
}


def canonical(node):
    # Element order, identity and all data survive; indentation does not.
    return [node.tag, sorted(node.attrib.items()), (node.text or '').strip(),
            [canonical(child) for child in node]]


def dependency_closure(plugin, graph, stack=()):
    if plugin in stack:
        raise ValueError('Cyclic plugin dependency: ' + ' -> '.join((*stack, plugin)))
    result = set()
    for dependency in graph.get(plugin, []):
        result.add(dependency)
        result.update(dependency_closure(dependency, graph, (*stack, plugin)))
    return sorted(result)


def parse_manifest(tree, path):
    raw = tree.read(path)
    if len(raw) > inv.XML_LIMIT:
        raise ValueError('Oversize manifest: ' + path)
    text = tree.text(path)[0]
    text = re.sub(r'<!--.*?-->', '', text, flags=re.S)
    includes, fragments = [], {}

    def include(match):
        name, _, relative = match.groups()
        if relative not in LOCAL_INCLUDES.get(path, set()):
            raise ValueError('Unreviewed local XML entity: ' + path + ': ' + relative)
        target = posixpath.normpath(posixpath.join(posixpath.dirname(path), relative))
        data = tree.read(target)
        fragment = tree.text(target)[0]
        if len(data) > inv.XML_LIMIT or re.search(r'<!\s*(?:DOCTYPE|ENTITY)', fragment, re.I):
            raise ValueError('Nested/oversize local XML entity: ' + target)
        if name in fragments:
            raise ValueError('Duplicate local XML entity: ' + name)
        fragments[name] = re.sub(r'<\?xml.*?\?>', '', fragment, flags=re.S)
        includes.append(dict(entity=name, path=target, sha256=inv.digest(data)))
        return '<!ENTITY ' + name + ' "">'

    text = re.sub(r'<!ENTITY\s+([\w.-]+)\s+SYSTEM\s+([\'"])(.*?)\2\s*>', include, text)
    if fragments:
        end = text.index(']>') + 2
        body = re.sub(r'&([\w.-]+);', lambda m: fragments.get(m[1], m[0]), text[end:])
        text = text[:end] + body
    # Preserve the decoded text, not its original declaration's codec.
    text = re.sub(r'(<\?xml[^>]*encoding=[\'"])[^\'"]+', r'\g<1>utf-8', text)
    root, entities = inv.manifest(text.encode())
    return root, includes, entities


def manifests(tree):
    return {p: parse_manifest(tree, p) for p in sorted(tree.entries)
            if p.startswith('plugins/') and posixpath.basename(p).lower() == 'plugin.xml'}


def registrations(parsed):
    explicit, generated = collections.defaultdict(list), collections.defaultdict(list)
    for path, (root, _, _) in parsed.items():
        for ordinal, node in enumerate(root.findall('contribution')):
            owner = dict(path=path, ordinal=ordinal, type=node.get('type'))
            explicit[node.attrib['id']].append(owner)
            if node.get('type') == 'GenericStructure':
                colors, sprites = inv.child_recipe(node)
                for color in range(colors):
                    for direction in range(len(sprites)):
                        generated[f'{node.attrib["id"]}-{color}:{direction}'].append(owner)
    return explicit, generated


def resolve(tree, base, owner, ref):
    targets, state = inv.resolve_path(tree, owner, ref)
    if targets or state == 'external':
        return dict(resolution=state, pack='optional', targets=targets)
    targets, state = inv.resolve_path(base, owner, ref)
    return dict(resolution=state, pack='bundled' if targets else None, targets=targets)


def compare_normalized(original, normalized, parsed):
    changes = []
    seen = set()
    for path in sorted(original.entries):
        matches = [p for p in normalized.entries if p == path]
        if not matches:
            matches = [p for p in normalized.entries if p.casefold() == path.casefold()]
        if len(matches) != 1:
            raise ValueError('Normalization has ambiguous/missing path: ' + path)
        target = matches[0]
        seen.add(target)
        if original.entries[path][0] != normalized.entries[target][0]:
            raise ValueError('Normalization changes file mode: ' + path)
        before, after = original.read(path), normalized.read(target)
        if before != after:
            a, b = original.text(path)[0], normalized.text(target)[0]
            a = re.sub(r'(<\?xml[^>]*encoding=[\'"])[^\'"]+', r'\g<1>utf-8', a)
            b = re.sub(r'(<\?xml[^>]*encoding=[\'"])[^\'"]+', r'\g<1>utf-8', b)
            if a != b:
                raise ValueError('Normalization changes decoded content: ' + path)
        if before != after or path != target:
            changes.append(dict(path=path, normalizedPath=target,
                                originalSha256=inv.digest(before), normalizedSha256=inv.digest(after),
                                encodingChanged=before != after, pathChanged=path != target))
        if path in parsed:
            if canonical(parsed[path][0]) != canonical(parse_manifest(normalized, target)[0]):
                raise ValueError('Normalization changes expanded recipe: ' + path)
    if seen != set(normalized.entries):
        raise ValueError('Normalization adds files')
    return dict(revision=normalized.revision, filesCompared=len(seen), changes=changes,
                status='decoded-text and expanded-manifest equivalence; resource resolution reviewed separately')


def build(tree, normalized, base, rules):
    parsed, base_parsed = manifests(tree), manifests(base)
    explicit, generated = registrations(parsed)
    base_explicit, base_generated = registrations(base_parsed)
    normalized_review = compare_normalized(tree, normalized, parsed)
    path_map = {r['path']: r['normalizedPath'] for r in normalized_review['changes']}
    base_review = json.loads((inv.PARITY_ROOT / 'exav-content-review.json').read_text())
    if base_review['source']['revision'] != base.revision:
        raise ValueError('Bundled content evidence revision mismatch')
    base_plugins = {posixpath.basename(posixpath.dirname(p)): p for p in base_parsed}
    plugins_by_name = collections.defaultdict(list)
    for path in parsed:
        plugins_by_name[posixpath.basename(posixpath.dirname(path))].append(path)
    recipes, plugins, file_edges, reference_edges, bindings, translations = [], [], [], [], [], []
    binding_cache = {}

    def bind(attributes):
        key = (attributes.get('name'), attributes.get('codebase'))
        if key not in binding_cache:
            binding_cache[key] = content.content.resolve_type(attributes, base_review['sourceTypes'], base_review['projects'])
        return binding_cache[key]

    factories = {}
    for root, _, _ in base_parsed.values():
        for node in root.findall('contribution'):
            if node.get('type') == 'contribution' and node.find('implementation') is not None:
                factories[node.findtext('name')] = dict(node.find('implementation').attrib)
    factory_names = {n.findtext('name') for root, _, _ in parsed.values()
                     for n in root.findall('contribution') if n.get('type') == 'contribution'}
    # Retain every text file, including unusually named readmes; permission wording
    # is a per-file review, not inferred from an English filename convention.
    notices = [p for p in tree.entries if p.lower().endswith('.txt') or
               re.search(r'(copying|licen[cs]e|notice)', posixpath.basename(p), re.I)]
    for path, (root, includes, entities) in parsed.items():
        folder = posixpath.dirname(path)
        name = posixpath.basename(folder)
        deps = sorted({'system', *(n.attrib['on'] for n in root.findall('depend'))})
        deps = [dict(pluginId=d, optional=plugins_by_name.get(d, []), bundled=base_plugins.get(d),
                     resolution='optional' if d in plugins_by_name else 'bundled' if d in base_plugins else 'missing') for d in deps]
        plugins.append(dict(path=path, pluginId=name, sha256=inv.digest(tree.read(path)),
                            title=root.findtext('title', ''), author=root.findtext('author', ''),
                            homepage=root.findtext('homepage', ''), version=root.findtext('version', ''),
                            directChildOfPluginRoot=folder.count('/') == 1,
                            dependencies=deps, localIncludes=includes, entities=entities,
                            notices=['readmeV2.txt'] + sorted(p for p in notices if p.startswith(folder + '/'))))
        for ordinal, node in enumerate(root.findall('contribution')):
            cid, kind = node.attrib['id'], node.attrib['type']
            key = f'{path}#{ordinal}:{cid}'
            explicit_class = node.find('class')
            implementation = (dict(explicit_class.attrib) if explicit_class is not None else
                              dict(name='freetrain.contributions.train.ParamTrainImpl') if kind == 'train' else
                              dict(name='freetrain.world.rail.signal.RailSignalContribution', codebase='FreeTrain.RailSignal.dll')
                              if kind == 'railSignal' else factories.get(kind))
            implementation_binding = bind(implementation) if implementation else None
            _, sprites = inv.child_recipe(node) if kind == 'GenericStructure' else (0, [])
            recipes.append(dict(key=key, path=path, ordinal=ordinal, id=cid, type=kind,
                                definitionSha256=inv.digest(inv.dump(canonical(node))),
                                # Full recipe stays in the separately pinned repo. Its
                                # expanded semantic hash includes all nested values/order.
                                parameters=[canonical(n) for n in node if n.tag in {
                                    'size', 'height', 'minHeight', 'maxHeight', 'price', 'pricePerArea',
                                    'capacity', 'seatedcapacity', 'fare', 'amenity', 'triprange',
                                    'style', 'operationCost', 'population', 'computerCannotBuild'}],
                                implementation=implementation.get('name') if implementation else None,
                                implementationStatus='bundled-source; recipe validation pending' if implementation_binding and
                                    implementation_binding['resolution'] == 'source-and-project' else
                                    'optional-factory' if kind in factory_names else
                                    'factory-definition' if kind == 'contribution' else 'unresolved',
                                directionSelectionsPinned=inv.direction_aliases(sprites, south_token='SOURTH') if sprites else None,
                                directionSelectionsPort=inv.direction_aliases(sprites, south_token='SOUTH') if sprites else None))
            for element in node.iter():
                for attr, value in element.attrib.items():
                    if attr in ('src', 'href', 'codebase'):
                        result = resolve(tree, base, path, value)
                        nresult = resolve(normalized, base, path_map.get(path, path), value)
                        file_edges.append(dict(owner=key, element=element.tag, attribute=attr, reference=value,
                                               **result, normalizedResolution=nresult))
                    if attr == 'ref' or attr.endswith('Ref') or attr == 'picture' and element.tag in ('colorMap', 'sprite'):
                        # Picture and contribution registries must not be conflated.
                        reference_edges.append(dict(owner=key, element=element.tag, attribute=attr, target=value,
                                                    registry='picture' if element.tag == 'picture' or attr == 'picture'
                                                    else 'contribution-candidate; loader review required'))
                if element.tag in ('class', 'implementation') and element.get('name'):
                    typ = element.attrib['name']
                    resolved = bind(element.attrib)
                    bindings.append(dict(owner=key, type=typ, codebase=element.get('codebase'),
                                         bindingKey=[typ, element.get('codebase')],
                                         status='bundled-source-and-project' if resolved['resolution'] == 'source-and-project' else
                                         'optional-code; source-or-adapter-review-required'))
                if element.get('file'):
                    value = element.get('file')
                    file_edges.append(dict(owner=key, element=element.tag, attribute='file', reference=value,
                                           **resolve(tree, base, path, value),
                                           normalizedResolution=resolve(normalized, base, path_map.get(path, path), value)))
                if element.tag in ('spriteType', 'colorVariation') and element.get('name'):
                    reference_edges.append(dict(owner=key, element=element.tag, attribute='name',
                                                target='spriteFactory:' + element.get('name'), registry='contribution'))
                if element.get('loader'):
                    reference_edges.append(dict(owner=key, element=element.tag, attribute='loader',
                                                target='spriteLoader:' + element.get('loader'), registry='contribution'))
                if element.tag == 'href' and (element.text or '').strip():
                    value = element.text.strip()
                    file_edges.append(dict(owner=key, element='href', attribute='text()', reference=value,
                                           **resolve(tree, base, path, value),
                                           normalizedResolution=resolve(normalized, base, path_map.get(path, path), value)))
        # Ordinal keeps duplicate explicit IDs distinct during text review.
        for ordinal, node in enumerate([root, *root.findall('contribution')]):
            for key, value in content.presentation_nodes(node).items():
                if ordinal == 0 and key.startswith(root.tag + '/contribution:'):
                    continue
                translations.append(dict(path=path, ordinal=ordinal - 1, key=key,
                                         text=value, textSha256=inv.digest(value.encode())))
    graph = {name: (['system'] if name != 'system' else []) + [n.attrib['on'] for n in base_parsed[path][0].findall('depend')]
             for name, path in base_plugins.items()}
    for plugin in plugins:
        graph[plugin['pluginId']] = [d['pluginId'] for d in plugin['dependencies']]
    for plugin in plugins:
        plugin['dependencyClosure'] = dependency_closure(plugin['pluginId'], graph)
    for edge in file_edges:
        matches = [b for b in bindings if b['owner'] == edge['owner'] and b['codebase'] == edge['reference']]
        edge['disposition'] = ('legacy-build-output; portable adapter required' if edge['attribute'] == 'codebase'
                               and matches and all(b['status'] == 'bundled-source-and-project' for b in matches)
                               else 'source-file-gap; P2 repair decision required' if edge['resolution'] == 'missing'
                               else 'case-map required; P2' if edge['resolution'] == 'case-folded' else 'source-resolved')
    files = [dict(path=p, sha256=inv.digest(tree.read(p)), bytes=len(tree.read(p)), mode=tree.entries[p][0])
             for p in sorted(tree.entries)]
    media = [r['path'] for r in files if Path(r['path']).suffix.lower() in inv.ASSETS]
    owners = collections.defaultdict(list)
    for row in file_edges:
        if row['pack'] == 'optional':
            for target in row['targets']:
                owners[target].append(row['owner'])
    assets = [dict(path=p, sha256=inv.digest(tree.read(p)), owners=sorted(set(owners[p])),
                   status='manifest-referenced' if owners[p] else 'unreferenced-by-manifest; loader/alternative review with P2',
                   noticeSource='owning manifest author and notices; no per-image grant inferred') for p in media]
    optional_ids = collections.defaultdict(list)
    base_ids = collections.defaultdict(list)
    for group in (explicit, generated):
        for cid, records in group.items():
            optional_ids[cid].extend(records)
    for group in (base_explicit, base_generated):
        for cid, records in group.items():
            base_ids[cid].extend(records)
    collisions = [dict(id=cid, owners=records, bundledOwners=base_ids.get(cid, []))
                  for cid, records in sorted(optional_ids.items()) if len(records) > 1 or cid in base_ids]
    # Inventory candidate registry matches without declaring picture refs resolved as contributions.
    picture_ids = {r['pictureId'] for r in base_review['pictureRegistrations']}
    picture_ids.update(node.attrib['id'] for root, _, _ in parsed.values() for node in root.findall('contribution')
                       if any(p.get('src') for p in node.iter('picture')))
    for row in reference_edges:
        row['contributionMatches'] = len(optional_ids.get(row['target'], [])) + len(base_ids.get(row['target'], []))
        row['pictureRegistered'] = row['target'] in picture_ids
    source_paths = {p for binding in binding_cache.values() for p in binding['sources'] + binding['projects']}
    source_lookup = {r['path']: r['sha256'] for r in base_review['sourceTypes'] + base_review['projects']}
    adapter_sources = []
    for path in sorted(source_paths):
        sha = inv.digest(base.read(path.removeprefix('trunk/')))
        if sha != source_lookup[path]:
            raise ValueError('Stale bundled adapter evidence: ' + path)
        adapter_sources.append(dict(path=path, sha256=sha))
    notice_reviews = {}
    for row in rules['noticeReviews']:
        path = row['path']
        if path not in notices or any(s not in tree.text(path)[0] for s in row['landmarks']):
            raise ValueError('Notice evidence drift: ' + path)
        notice_reviews[path] = row
    legacy = json.loads((inv.PARITY_ROOT / 'historical/extra-suite-ledger.json').read_text())
    legacy_ids = {r['contributionId'] for r in legacy['contributions'] if not r.get('generatedFrom')}
    local_sources = []
    for source in rules['exavSources']:
        path = source['path']
        code = base.text(path)[0]
        if any(symbol not in code for symbol in source['symbols']):
            raise ValueError('Missing ExAV source symbol: ' + path)
        local_sources.append(dict(**source, sha256=inv.digest(base.read(path))))
    active_paths = {p['path'] for p in plugins if p['directChildOfPluginRoot']}
    sample_review_path = inv.PARITY_ROOT / 'exav-save-samples.json'
    sample_review = json.loads(sample_review_path.read_text())
    if sample_review['revision'] != base.revision:
        raise ValueError('Save reference evidence revision mismatch')
    sample_bindings = []
    for sample in sample_review['samples']:
        references = []
        for cid in sorted({r['id'] for r in sample['contributionReferences']}):
            references.append(dict(id=cid, optional=[o for o in optional_ids.get(cid, []) if o['path'] in active_paths],
                                   bundled=base_ids.get(cid, [])))
        sample_bindings.append(dict(sampleSha256=sample['sampleSha256'], references=references,
                                    counts=dict(ids=len(references), optional=sum(bool(r['optional']) for r in references),
                                                bundled=sum(bool(r['bundled']) for r in references),
                                                unmatched=sum(not r['optional'] and not r['bundled'] for r in references)),
                                    status='exact selected-profile identity matches only; no alias, recipe, import or continuation verdict'))
    counts = dict(files=len(files), manifests=len(plugins), selectedManifests=len(active_paths),
                  selectedDeclarations=sum(r['path'] in active_paths for r in recipes),
                  directDeclarations=sum(len(v) for v in explicit.values()),
                  uniqueExplicitIds=len(explicit), generatedChildren=sum(len(v) for v in generated.values()),
                  uniqueGeneratedIds=len(generated), media=len(media),
                  manifestReferencedMedia=sum(bool(r['owners']) for r in assets),
                  translationCandidates=len(translations), typeBindings=len(bindings), distinctTypeBindings=len(binding_cache),
                  localIncludes=sum(len(p['localIncludes']) for p in plugins),
                  duplicateExplicitIds=sum(len(v) > 1 for v in explicit.values()),
                  collisions=len(collisions), bundledIdCollisions=len(set(optional_ids) & set(base_ids)),
                  nestedManifests=sum(not p['directChildOfPluginRoot'] for p in plugins),
                  missingPluginDependencies=sum(d['resolution'] == 'missing' for p in plugins for d in p['dependencies']),
                  fileEdges=len(file_edges), fileEdgeResolutions=dict(sorted(collections.Counter(r['resolution'] for r in file_edges).items())),
                  types=dict(sorted(collections.Counter(r['type'] for r in recipes).items())))
    if counts != rules['expectedCounts'] and rules['expectedCounts']:
        raise ValueError('Optional denominator/diagnostic drift: ' + json.dumps(counts))
    return dict(schemaVersion=1, packId=rules['packId'], status='P0-08 source inventory/specification; runtime unverified',
                metadataOnly=True, source=rules['source'], bundledSource=rules['bundledSource'],
                fileSetSha256=inv.digest(inv.dump(files)), rulesSha256=inv.digest(RULES.read_bytes()),
                generatorSources={p: inv.digest((inv.ROOT / p).read_bytes()) for p in (
                    'scripts/review-exav-optional.py', 'scripts/reconcile-exav-content.py',
                    'scripts/reconcile-parity-content.py', 'scripts/inventory-parity.py')},
                bundledReviewSha256=inv.digest((inv.PARITY_ROOT / 'exav-content-review.json').read_bytes()),
                historicalLedgerSha256=inv.digest((inv.PARITY_ROOT / 'historical/extra-suite-ledger.json').read_bytes()),
                saveSampleReviewSha256=inv.digest(sample_review_path.read_bytes()), saveReferenceBindings=sample_bindings,
                counts=counts, normalizedSourceReview=normalized_review, exavSourceEvidence=local_sources,
                adapterSources=adapter_sources,
                manifestSelection=dict(rule='stable Core.getDefaultProfile immediate-child enumeration',
                                       selected=sorted(active_paths),
                                       sourceOnly=sorted(set(parsed) - active_paths),
                                       status='specified; runtime implementation pending'),
                translationPolicy=dict(status='inventoried; feature wording review pending', englishDonor=None,
                                       proposedEnglish=None, identity='path + declaration ordinal + XML key; duplicate IDs kept distinct'),
                compatibility=dict(legacyRevision=legacy['revision'], legacyOnlyExplicitIds=sorted(legacy_ids - set(explicit)),
                                   sharedExplicitIds=len(legacy_ids & set(explicit)),
                                   status='identity accounting only; older suite requires its own selected profile'),
                files=files, plugins=plugins, recipes=recipes,
                generatedChildren=[dict(id=cid, owners=rows) for cid, rows in sorted(generated.items())],
                assets=assets, typeBindings=bindings, fileEdges=file_edges,
                resolvedTypes=list(binding_cache.values()),
                referenceEdges=reference_edges, collisions=collisions,
                pluginNameCollisions=[dict(pluginId=n, optional=paths, bundled=base_plugins.get(n))
                                      for n, paths in sorted(plugins_by_name.items()) if len(paths) > 1 or n in base_plugins],
                translationCandidates=translations,
                notices=[dict(path=p, sha256=inv.digest(tree.read(p)), review=notice_reviews.get(p),
                              status='source notice reviewed; per-resource derivative clearance pending' if p in notice_reviews else
                              'retained text/notice candidate; no grant inferred')
                         for p in sorted(set(notices))],
                binaryCode=[r for r in files if r['path'].lower().endswith('.dll')],
                decisions=rules['decisions'], acceptance=rules['acceptance'])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path, required=True, help='Explicit stable ExAV archive')
    parser.add_argument('--optional-root', type=Path, required=True, help='Explicit non-free Git repository')
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    rules = json.loads(RULES.read_text())
    tree = inv.Tree(args.optional_root, rules['source']['revision'])
    normalized = inv.Tree(args.optional_root, rules['normalizedRevision'])
    base = inv.Tree(args.source_root, rules['bundledSource']['revision'], rules['bundledSource']['subtree'])
    review = build(tree, normalized, base, rules)
    data = inv.dump_ledger(review)
    if args.check:
        if OUTPUT.read_bytes() != data:
            raise SystemExit('Stale optional review; regenerate with both explicit repositories')
    else:
        OUTPUT.write_bytes(data)
    print(json.dumps(review['counts'], sort_keys=True))


if __name__ == '__main__':
    main()
