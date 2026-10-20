"""Pinned source UI audit and fail-closed application of maintained P0-03 reviews.

Discovery is lexical evidence, never a reachability decision. Each discovered
surface and legacy binding must have an explicit decision in action-map.json.
"""
import collections
import re


CONTROL_TYPES = (
    'ComboBox', 'ListBox', 'ListView', 'TreeView', 'DDTreeView', 'TextBox',
    'CheckBox', 'RadioButton', 'NumericUpDown', 'TrackBar', 'TrackBarEx', 'Button',
    'IndexSelector', 'ColorPickButton', 'SubListSelector', 'MenuItem',
    'ToolBarButton', 'TabControl', 'ColorDialog', 'FontDialog', 'NumberEditEx',
)
HOOK = r'(?:onClick|onRightClick|onMouseMove|OnClick|OnMouse\w+|OnKey\w+|OnDrag\w+|OnItemDrag|OnLinkClicked|ProcessCmdKey)'


def discover_surfaces(tree, strip_comments, digest):
    """Cover controls without handlers, custom callbacks and virtual input hooks.

    IDs use path/kind/normalized expression/occurrence, not byte offsets. The
    original 248 binding IDs are retained separately for backwards traceability.
    """
    rows = []
    patterns = {
        'control': r'\b(?:this\.)?(?P<control>\w+)\s*=\s*new\s+(?:[\w]+\.)*(?P<type>' + '|'.join(CONTROL_TYPES) + r')\s*\(',
        'delegate': r'\bnew\s+[\w.]+(?:Handler|Listener)\s*\([^;]+?\)\s*(?=[;,)])',
        'input-hook': r'\b(?:public|protected|private|internal)\s+(?:(?:override|virtual|static)\s+)*(?:void|bool)\s+' + HOOK + r'\s*\([^)]*\)',
        'shortcut': r'\b[\w.]+\.Shortcut\s*=\s*[^;]+',
        'dialog-result': r'\b[\w.]+\.DialogResult\s*=\s*[^;]+',
    }
    for path in sorted(tree.entries):
        if path in ('core/res/about.html', 'core/res/balanceSheet.html'):
            for index, match in enumerate(re.finditer(r'(?:href|onselect)="[^"]*"', tree.text(path)[0])):
                rows.append(dict(surfaceId=f'base:surface:{path}:html:{index}', baselinePath=path,
                                 kind='html', expression=match[0], sourceSha256=digest(tree.read(path))))
            continue
        if not path.startswith(('core/', 'plugins/', 'lib/')) or not path.endswith('.cs'):
            continue
        code = strip_comments(tree.text(path)[0])
        occurrences = collections.Counter()
        for kind, pattern in patterns.items():
            for match in re.finditer(pattern, code):
                expression = ' '.join(match[0].split())
                identity = (kind, expression)
                occurrences[identity] += 1
                row = dict(surfaceId=f'base:surface:{path}:{kind}:{expression}:{occurrences[identity]}',
                           baselinePath=path, kind=kind, expression=expression,
                           sourceSha256=digest(tree.read(path)))
                if kind == 'control':
                    row.update(control=match['control'], controlType=match['type'])
                    # Retain read/commit-only options and native dialog defaults
                    # as source expressions, without evaluating C# or conflating
                    # a designer value with an enforced domain boundary.
                    property_pattern = r'\b(?:this\.)?' + re.escape(match['control']) + r'\.(?:ReadOnly|Minimum|Maximum|DialogResult|Shortcut|Visible|Enabled|Checked|Text)\s*=\s*[^;]+;'
                    row['sourceProperties'] = [' '.join(m[0].split()) for m in re.finditer(property_pattern, code)]
                rows.append(row)
    return rows


def unique_rows(rows, key, label):
    result = {}
    for row in rows:
        value = row[key]
        if value in result:
            raise ValueError(f'Duplicate {label}: {value}')
        result[value] = row
    return result


def same_keys(expected, actual, label):
    missing, stale = set(expected) - set(actual), set(actual) - set(expected)
    if missing or stale:
        raise ValueError(f'{label}: missing={sorted(missing)[:3]}, stale={sorted(stale)[:3]}')


def apply_reviews(tree, base, reviews, strip_comments, digest, registry_source):
    if reviews.get('schemaVersion') != 2:
        raise ValueError('P0-03 requires action-map schemaVersion 2')
    if reviews['baselineRevision'] != tree.revision:
        raise ValueError('Action review baseline revision differs')
    if digest(registry_source.encode()) != reviews['implementationSources']['web/src/lib/game-ui.ts']:
        raise ValueError('Delivered registry changed; refresh implementation mapping evidence')
    workflows = reviews['workflows']
    registry = registry_source.split('export const registry = {', 1)[1].split('} as const satisfies', 1)[0]
    operation_ids = set(re.findall(r"(?m)^  ['\"]?([\w-]+)['\"]?:", registry))
    allowed = {'missing', 'reference-slice', 'portable-adaptation', 'display-only',
               'mapped-dynamic', 'excluded', 'source-only', 'shared-control', 'infrastructure'}
    for key, workflow in workflows.items():
        if workflow['status'] not in allowed or not workflow['behavior'] or not workflow['destination']:
            raise ValueError('Incomplete workflow: ' + key)
        if not workflow['phases']:
            raise ValueError('Missing phase/disposition owner: ' + key)
        for operation in workflow['operations']:
            if operation not in operation_ids:
                raise ValueError('Unknown delivered operation: ' + operation)
        for evidence in workflow['sourceEvidence']:
            path, token = evidence['path'], evidence['symbol']
            if path not in tree.entries or token not in tree.text(path)[0]:
                raise ValueError(f'Stale source evidence: {key}: {path}: {token}')

    source_reviews = reviews['sourceReviews']
    for path, review in source_reviews.items():
        if path not in tree.entries or digest(tree.read(path)) != review['sha256']:
            raise ValueError('Source review hash drift: ' + path)
        if review['workflow'] not in workflows or not review['reachability']:
            raise ValueError('Incomplete source review: ' + path)

    decisions = unique_rows(reviews['bindings'], 'coverageId', 'binding review')
    same_keys([r['coverageId'] for r in base['uiBindings']], decisions, 'Binding review coverage')

    def enrich(row, decision):
        workflow_id = decision['workflow']
        if workflow_id not in workflows or row['baselinePath'] not in source_reviews:
            raise ValueError('Unreviewed workflow/source: ' + workflow_id + ': ' + row['baselinePath'])
        workflow = workflows[workflow_id]
        if not decision['effect']:
            raise ValueError('Empty action effect: ' + workflow_id)
        row.update(workflow=workflow_id, mappingStatus=workflow['status'],
                   behavior=decision['effect'], modernOperations=workflow['operations'],
                   modernDestinations=[workflow['destination']],
                   phases=workflow['phases'], manualRequirements=workflow['manualRequirements'],
                   reachability=source_reviews[row['baselinePath']]['reachability'],
                   evidence=[row['baselinePath'], 'doc/parity/historical/action-map.json#' + workflow_id],
                   differences=workflow['differences'])
        if 'disposition' in decision:
            if decision['disposition'] not in allowed:
                raise ValueError('Invalid surface disposition: ' + decision['disposition'])
            row['mappingStatus'] = decision['disposition']
        if 'resolvedHandler' in decision:
            row['resolvedHandler'] = decision['resolvedHandler']

    for action in base['uiBindings']:
        enrich(action, decisions[action['coverageId']])

    surfaces = discover_surfaces(tree, strip_comments, digest)
    surface_decisions = unique_rows(reviews['surfaces'], 'surfaceId', 'surface decision')
    same_keys([r['surfaceId'] for r in surfaces], surface_decisions, 'Source surface coverage')
    for row in surfaces:
        enrich(row, surface_decisions[row['surfaceId']])

    for path, review in source_reviews.items():
        for field in review.get('fields', {}):
            if not re.search(r'\b' + re.escape(field) + r'\b', strip_comments(tree.text(path)[0])):
                raise ValueError(f'Stale reviewed field: {path}: {field}')

    dynamic = unique_rows(reviews['contributions'], 'coverageId', 'contribution decision')
    # Include every declaration and generated child, even artwork/factory-only
    # entries, so a new contribution type cannot silently bypass this review.
    same_keys([r['coverageId'] for r in base['contributions']], dynamic, 'Dynamic contribution coverage')
    contributions = []
    for contribution in base['contributions']:
        decision = dynamic[contribution['coverageId']]
        workflow = workflows[decision['workflow']]
        if not decision['route'] or not decision['disposition']:
            raise ValueError('Incomplete contribution route: ' + contribution['coverageId'])
        contributions.append(dict(coverageId=contribution['coverageId'],
                                  contributionId=contribution['contributionId'],
                                  contributionType=contribution['contributionType'],
                                  baselinePath=contribution['baselinePath'], scope=contribution['scope'],
                                  workflow=decision['workflow'], route=decision['route'],
                                  disposition=decision['disposition'], mappingStatus=workflow['status'],
                                  modernDestinations=[workflow['destination']], phases=workflow['phases']))

    required_manual = {f'MAN-{family}{i:02}' for family, count in [('T', 10), ('M', 7), ('W', 12), ('O', 16)] for i in range(1, count + 1)}
    manual_links = reviews['manualRequirements']
    same_keys(required_manual, manual_links, 'Manual requirement coverage')
    for requirement, links in manual_links.items():
        if not links or any(link not in workflows for link in links):
            raise ValueError('Unassigned manual requirement: ' + requirement)
        for link in links:
            if requirement not in workflows[link]['manualRequirements']:
                raise ValueError('Nonreciprocal manual requirement: ' + requirement)
    for workflow_id, workflow in workflows.items():
        for requirement in workflow['manualRequirements']:
            if requirement not in manual_links or workflow_id not in manual_links[requirement]:
                raise ValueError('Unlinked workflow manual requirement: ' + workflow_id)

    roots = {r['path'] for r in base['roots']}
    same_keys(roots, reviews['rootDispositions'], 'Baseline root review')
    for root, disposition in reviews['rootDispositions'].items():
        if not disposition:
            raise ValueError('Empty root disposition: ' + root)

    releases = unique_rows(reviews['releaseActionLinks'], 'date', 'release action group')
    if len(releases) != 26:
        raise ValueError('Expected all 26 post-manual release action groups')
    notes = unique_rows(reviews['baselineChangeNotes'], 'number', 'baseline change note')
    same_keys(range(1, 16), notes, 'Baseline change notes')
    if digest(tree.read('whatsnew.txt')) != reviews['baselineChangeNotesSha256']:
        raise ValueError('Baseline release notes hash drift')
    for row in [*releases.values(), *notes.values()]:
        if not row['disposition'] or not row['workflows'] or any(key not in workflows for key in row['workflows']):
            raise ValueError('Unassigned release action/fix disposition')

    return dict(schemaVersion=1, baselineRevision=tree.revision,
                implementationRevision=reviews['implementationRevision'],
                implementationSources=reviews['implementationSources'],
                meaning='P0-03 source mapping complete; implementation and behavioral acceptance remain separate.',
                counts=dict(bindings=len(decisions), surfaces=len(surfaces),
                            reviewedSources=len(source_reviews), contributions=len(contributions),
                            manualRequirements=len(manual_links)),
                bindingStatuses=dict(sorted(collections.Counter(r['mappingStatus'] for r in base['uiBindings']).items())),
                sourceSurfaces=surfaces, dynamicContributions=contributions,
                manualRequirements=manual_links, rootDispositions=reviews['rootDispositions'],
                releaseActionLinks=reviews['releaseActionLinks'],
                baselineChangeNotes=reviews['baselineChangeNotes'],
                baselineChangeNotesSha256=reviews['baselineChangeNotesSha256'],
                differences=reviews['differences'])


def report(review, workflows):
    lines = ['# P0-03 action reconciliation', '', review['meaning'], '',
             f'Baseline: `{review["baselineRevision"]}`; implementation: `{review["implementationRevision"]}`.', '',
             '| Inventory | Reviewed records |', '| --- | ---: |']
    lines.extend(f'| {name} | {count} |' for name, count in review['counts'].items())
    lines += ['', '## Workflow destinations', '', '| Workflow | Mapping | Destination | Phases |', '| --- | --- | --- | --- |']
    for key, workflow in workflows.items():
        lines.append(f'| {key} | {workflow["status"]} | {workflow["destination"]} | {", ".join(workflow["phases"])} |')
    lines += ['', '## Source differences and dispositions', '']
    for difference in review['differences']:
        lines.append(f'- **{difference["id"]}**: {difference["finding"]} {difference["disposition"]}')
    lines += ['', '## Manual links', '', '| Requirement | Workflows |', '| --- | --- |']
    for key, links in review['manualRequirements'].items():
        lines.append(f'| {key} | {", ".join(links)} |')
    lines += ['', '## Later-release action reconciliation', '',
              'UI/source destinations only; numeric/fix/save regressions remain in their named P0 gates.', '',
              '| Release group | Workflows |', '| --- | --- |']
    for row in review['releaseActionLinks']:
        lines.append(f'| {row["date"]} | {", ".join(row["workflows"])} |')
    lines += ['', '## Pinned whatsnew.txt notes', '']
    for row in review['baselineChangeNotes']:
        lines.append(f'- #{row["number"]}: {row["disposition"]}')
    return ('\n'.join(lines) + '\n').encode()
