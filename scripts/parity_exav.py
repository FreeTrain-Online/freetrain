"""Separate P0-01 stable ExAV discovery; never apply historical review decisions."""
import collections
import json
import re
import xml.etree.ElementTree as ET


def local(tag):
    return tag.rsplit('}', 1)[-1]


def project_inventory(scanner, tree, config):
    solution = config['solution']
    solution_paths = re.findall(r'^Project\([^\n]+?=\s*"[^"]+",\s*"([^"]+)"',
                               tree.text(solution)[0], re.M)
    listed = {path.replace('\\', '/') for path in solution_paths}
    projects, edges = [], []
    for path in sorted(p for p in tree.entries if p.endswith(('.csproj', '.vcproj'))):
        root = ET.fromstring(tree.text(path)[0])
        assemblies, references, items = [], [], []
        for element in root.iter():
            tag = local(element.tag)
            if tag == 'AssemblyName' and element.text:
                assemblies.append(element.text.strip())
            if element.get('AssemblyName'):
                assemblies.append(element.get('AssemblyName'))
            if tag == 'Reference':
                references.append(dict(element.attrib))
            value = element.get('Include') if tag in (
                'Compile', 'Content', 'EmbeddedResource', 'None', 'ProjectReference') else None
            # Retain VS2003 C# File and native vcproj File membership as well.
            if tag == 'File':
                value = element.get('RelPath') or element.get('RelativePath')
            if value:
                targets, resolution = scanner.resolve_path(tree, path, value)
                edge = dict(project=path, kind=element.get('BuildAction', tag), value=value,
                            conditions=[node.get('Condition') for node in root.iter()
                                        if node.get('Condition') and element in node.iter()],
                            targets=targets, resolution=resolution)
                edges.append(edge)
                items.append(edge)
        projects.append(dict(path=path, sha256=scanner.digest(tree.read(path)),
                             listedInSelectedSolution=path in listed,
                             assemblies=sorted(set(assemblies)), references=references,
                             itemCount=len(items), status='lexical-membership; conditions not evaluated'))
    solution_edges = [dict(solution=solution, value=path,
                           targets=[path] if path in tree.entries else [],
                           resolution='exact' if path in tree.entries else 'missing')
                      for path in sorted(listed)]
    return projects, edges, solution_edges


def build(scanner, tree, config, historical):
    if tree.revision != config['revision']:
        raise ValueError('ExAV revision must be the full configured commit')
    assembly = tree.text('core/AssemblyInfo.cs')[0]
    if f'AssemblyVersion("{config["engineVersion"]}")' not in assembly:
        raise ValueError('ExAV engine version differs from pinned configuration')
    recipe = config['generatedRegistration']
    if scanner.digest(tree.read(recipe['path'])) != recipe['sha256']:
        raise ValueError('ExAV generated-registration source hash drift')
    pack = scanner.build_pack(tree, 'base', 'plugins/', south_token=recipe['southToken'], allow_cp932=True)
    if (pack['counts']['manifests'], pack['counts']['declared']) != (
            config['expectedManifests'], config['expectedDeclarations']):
        raise ValueError('ExAV manifest/declaration count differs from pinned expectation')
    source, bindings, serializers, resources = scanner.source_inventory(tree, {}, all_sources=True)
    surfaces = scanner.action_review.discover_surfaces(tree, scanner.strip_comments, scanner.digest)
    projects, project_edges, solution_edges = project_inventory(scanner, tree, config)
    membership = collections.defaultdict(list)
    for edge in project_edges:
        for target in edge['targets']:
            membership[target].append(dict(project=edge['project'], kind=edge['kind'],
                                           conditions=edge['conditions']))
    selected = {p['path'] for p in projects if p['listedInSelectedSolution']}

    def disposition(path):
        root = path.split('/')[0]
        if '/' not in path:
            return dict(scope='build-reference', disposition=config['rootFilesDisposition'], reviewOwners=['P0-02'])
        if root not in config['roots']:
            raise ValueError('ExAV root has no explicit disposition: ' + root)
        result = dict(config['roots'][root])
        plugin = path.split('/')[1] if root == 'plugins' else None
        if plugin in scanner.EXCLUDED:
            result.update(scope='excluded', disposition=scanner.EXCLUDED[plugin])
        return result

    files = []
    for path, (mode, oid) in sorted(tree.entries.items()):
        row = scanner.common('base:file:' + path, path, path)
        row.update(disposition(path))
        row.update(sha256=scanner.digest(tree.read(path)), gitBlob=oid, mode=mode,
                   bytes=len(tree.read(path)), projectMembership=membership[path])
        row['reachability'] = ('selected-solution-project-item; runtime review pending'
                               if any(m['project'] in selected for m in membership[path])
                               else 'other-project-item; runtime review pending' if membership[path]
                               else 'not-listed-as-project-item; runtime/resource review pending')
        files.append(row)
    by_path = {r['baselinePath']: r for r in files}
    source_by_path = {r['baselinePath']: r for r in source}
    for path, row in source_by_path.items():
        # Expand callback markers missed by the original discovery scanner.
        code = scanner.strip_comments(tree.text(path)[0])
        flags = set(row['serializationFlags'])
        flags.update(re.findall(r'\[(?:\w+\.)?(OnDeserialized|OnDeserializing|OnSerialized|OnSerializing)\b', code))
        row['serializationFlags'] = sorted(flags)
    serializers = [dict(coverageId=row['coverageId'], path=row['baselinePath'],
                        symbols=row['symbols'], flags=row['serializationFlags'],
                        status='lexical-candidate; P0-05 nested type/field graph pending')
                   for row in source if row['serializationFlags']]

    for group in (pack['plugins'], pack['contributions'], pack['assets'], source, bindings, surfaces):
        for row in group:
            path = row['baselinePath']
            identity = row.get('coverageId', row.get('surfaceId'))
            defaults = scanner.common(identity, path, row.get('handler', row.get('expression', path)))
            for key, value in defaults.items():
                row.setdefault(key, value)
            row.update(disposition(path))
            row.update(sourceSha256=by_path[path]['sha256'],
                       sourceFile='base:file:' + path,
                       projectMembership=membership[path],
                       translationReferences=[dict(path=path, sha256=by_path[path]['sha256'],
                                                   identity=row.get('contributionId', row['symbol']),
                                                   status='P0-02/P0-03 per-string extraction/donor review pending')])
            if path in historical.entries:
                old_hash = scanner.digest(historical.read(path))
                row['historicalComparison'] = dict(revision=historical.revision, path=path, sha256=old_hash,
                    result='identical-bytes; dependency review pending' if old_hash == by_path[path]['sha256']
                    else 'changed; semantic review pending')
            else:
                row['historicalComparison'] = dict(revision=historical.revision, result='path absent in historical source')
            row.pop('currentPaths', None)  # No original-to-modern relocation is ExAV evidence.
            if row.get('generatedRecipe'):
                row['generatedRecipe'].update(sourceSha256=recipe['sha256'], southToken=recipe['southToken'])
            if row.get('generatedFrom'):
                row['dependencies'].append('base:source:' + recipe['path'])
            if group is bindings or group is surfaces:
                row['differences'] = ['ExAV P0-03 action mapping pending; historical decisions not applied.']

    old_manifests = [p for p in historical.entries if p.startswith('plugins/') and p.lower().endswith('/plugin.xml')]
    old_ids = {n.get('id') for p in old_manifests for n in scanner.manifest(historical.read(p))[0].findall('contribution')}
    direct_ids = {r['contributionId'] for r in pack['contributions'] if not r.get('generatedFrom')}
    pack.update(schemaVersion=2, packId='exav-stable-bundled', sourceId=config['sourceId'],
                baselineConfigSha256=scanner.digest((scanner.PARITY_ROOT / 'exav-baseline.json').read_bytes()),
                generatorSources={path: scanner.digest((scanner.ROOT / path).read_bytes())
                                  for path in ('scripts/inventory-parity.py', 'scripts/parity_exav.py', 'scripts/parity_actions.py')},
                source=dict(repository=config['repository'], revision=tree.revision,
                            subtree=config['subtree'], svnRevision=config['svnRevision'],
                            engineVersion=config['engineVersion'], pluginFamily=config['pluginFamily']),
                fileInventory=files, sourceFiles=source, uiBindings=bindings, uiSurfaces=surfaces,
                serializationCandidates=serializers, sourceResourceEdges=resources,
                projects=projects, projectEdges=project_edges, solutionEdges=solution_edges,
                roots=[dict(path=root, files=sum(p.startswith(root + '/') for p in tree.entries), **rules)
                       for root, rules in config['roots'].items()],
                historicalSaveExamples=[p for p in sorted(tree.entries) if p.lower().endswith(('.ftgd', '.ftgt', '.ftgd3', '.ftdd'))],
                historicalComparison=dict(revision=historical.revision,
                    originalOnlyDirectIds=sorted(old_ids - direct_ids), exavOnlyDirectIds=sorted(direct_ids - old_ids),
                    sharedDirectIds=len(old_ids & direct_ids), status='ID comparison only; definitions/dependencies not equivalent'),
                absentHistoricalExclusions=config['absentHistoricalExclusions'],
                generatedRegistrationReview=recipe, boundaries=config['boundaries'])
    pack['counts'].update(files=len(files), sourceFiles=len(source), uiBindings=len(bindings),
                          uiSurfaces=len(surfaces), serializationCandidates=len(serializers), projects=len(projects),
                          selectedSolutionProjects=sum(p['listedInSelectedSolution'] for p in projects),
                          projectEdges=len(project_edges))
    pack['source']['fileSetSha256'] = scanner.digest(scanner.dump(
        [[r['baselinePath'], r['mode'], r['sha256']] for r in files]))

    # Preserve actual archive paths and namespace every identity, including graph
    # references. Do not rename contribution/plugin IDs or relative resource refs.
    def qualify(value, key=None):
        if isinstance(value, dict):
            if key == 'historicalComparison':
                return value  # These paths belong to the original snapshot, not trunk.
            return {k: qualify(v, k) for k, v in value.items()}
        if isinstance(value, list):
            return [qualify(v, key) for v in value]
        if isinstance(value, str):
            if value.startswith('base:'):
                return config['sourceId'] + ':' + value[5:]
            if key in ('baselinePath', 'path', 'source', 'project', 'solution', 'targets', 'notices') and value in tree.entries:
                return config['subtree'] + '/' + value
        return value
    return qualify(pack)


def report(pack):
    c = pack['counts']
    lines = ['# Stable ExAV P0-01 inventory', '',
             f'Source: `{pack["source"]["repository"]}` / `{pack["revision"]}` / `trunk/`.', '',
             'Metadata discovery only; P0-02–P0-09 and runtime acceptance remain open.', '',
             '| Inventory | Count |', '| --- | ---: |']
    lines.extend(f'| {key} | {value} |' for key, value in c.items() if isinstance(value, int))
    lines += ['', '## Discovery diagnostics', '']
    lines.extend(f'- {kind}: {count}' for kind, count in sorted(collections.Counter(i['kind'] for i in pack['issues']).items()))
    lines += [f'- Contribution collisions: {len(pack["collisions"])}',
              f'- Unresolved project items: {sum(e["resolution"] != "exact" for e in pack["projectEdges"])}', '',
              '## Scope boundaries', '']
    lines.extend('- ' + text for text in pack['boundaries'])
    return ('\n'.join(lines) + '\n').encode()


def run(scanner, args):
    if args.report and not args.report.resolve().is_relative_to((scanner.ROOT / 'tmp').resolve()):
        raise ValueError('--report must be under this checkout\'s tmp/')
    config = json.loads((scanner.PARITY_ROOT / 'exav-baseline.json').read_text())
    tree = scanner.Tree(args.source_root, config['revision'], config['subtree'])
    historical = scanner.Tree(scanner.ROOT, config['historicalComparisonRevision'])
    ledger = build(scanner, tree, config, historical)
    scanner.write(scanner.PARITY_ROOT / 'exav-ledger.json', scanner.dump_ledger(ledger), args.check)
    if args.report:
        scanner.write(args.report, report(ledger))
    c = ledger['counts']
    print(f'ExAV: {c["manifests"]} manifests, {c["declared"]} declarations, {c["generated"]} children, '
          f'{c["sourceFiles"]} source files, {c["uiBindings"]} bindings, {c["uiSurfaces"]} surfaces, '
          f'{c["serializationCandidates"]} serializer candidates')
