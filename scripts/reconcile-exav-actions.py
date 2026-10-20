#!/usr/bin/env python3
"""Pinned ExAV P0-03 reassessment, explicitly separating findings from candidates.

Reads Git objects only. No sibling discovery, legacy execution or historical writes.
"""
import argparse
import collections
import importlib.util
import json
import re
from pathlib import Path
import xml.etree.ElementTree as ET

SPEC = importlib.util.spec_from_file_location('inventory', Path(__file__).with_name('inventory-parity.py'))
inv = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(inv)
OUTPUT = inv.PARITY_ROOT / 'exav-action-review.json'
RULES = inv.PARITY_ROOT / 'exav-action-rules.json'
STRING = re.compile(r'@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"')
JAPANESE = re.compile(r'[\u3040-\u30ff\u3400-\u9fff]')


def literals(text):
    # Preserve line positions, including newlines inside comments.
    tokens = re.compile(STRING.pattern + r"|'(?:\\.|[^'\\])*'|//[^\n]*|/\*.*?\*/", re.S)
    code = tokens.sub(lambda m: re.sub(r'[^\n]', ' ', m[0])
                      if m[0].startswith(('//', '/*', "'")) else m[0], text)
    occurrences = collections.Counter()
    for match in STRING.finditer(code):
        line = code.count('\n', 0, match.start()) + 1
        start = code.rfind('\n', 0, match.start()) + 1
        prefix = code[start:match.start()].strip()
        # Statement/control identity is more stable than a line number or text.
        key = re.sub(r'\s+', ' ', prefix)
        occurrences[key] += 1
        raw = match[0]
        # Keep source escapes; do not reinterpret arbitrary C# as Python code.
        value = raw[2:-1].replace('""', '"') if raw.startswith('@') else raw[1:-1]
        yield dict(key=f'literal:{key}:{occurrences[key]}', line=line,
                   expression=raw, originalText=value,
                   context=code[start:code.find('\n', match.end()) if '\n' in code[match.end():] else len(code)].strip(),
                   classification='Japanese-text-candidate' if JAPANESE.search(value)
                   else 'presentation-candidate' if re.search(
                       r'(?:\.Text|\.Title|ToolTip|MessageBox|showError|statusText|Format)\b', prefix)
                   else 'unclassified-literal; may be non-presentation')


def text_candidates(tree, path):
    text = tree.text(path)[0]
    if path.endswith('.cs'):
        return list(literals(text))
    if path.endswith('.resx'):
        root = ET.fromstring(text)
        return [dict(key='resx:' + row.get('name'), originalText=row.findtext('value', ''),
                     classification='resource-string-candidate')
                for row in root.findall('data') if not row.get('type') and not row.get('mimetype')]
    if path.endswith('.html'):
        # Conservative text-node accounting; markup/links stay in original source.
        values = re.findall(r'>([^<>]+)<', text)
        return [dict(key=f'html-text:{n}', originalText=value.strip(),
                     classification='HTML-text-candidate')
                for n, value in enumerate(values) if value.strip()]
    return []


def validate_findings(tree, rules):
    ids = set()
    for row in rules['findings']:
        if row['id'] in ids or not row['behavior'] or not row['phases']:
            raise ValueError('Duplicate/incomplete action finding: ' + row['id'])
        ids.add(row['id'])
        for evidence in row['evidence']:
            if evidence['symbol'] not in tree.text(evidence['path'])[0]:
                raise ValueError('Stale finding evidence: ' + row['id'] + ': ' + evidence['path']
                                 + ': ' + evidence['symbol'])


def build(tree, historical, config, ledger, previous, rules):
    if ledger['revision'] != tree.revision or ledger['sourceId'] != config['sourceId']:
        raise ValueError('ExAV ledger/source selection differs')
    # Fail closed on changed source and dependency files, not just UI paths.
    for row in ledger['fileInventory']:
        path = row['baselinePath'].removeprefix('trunk/')
        if inv.digest(tree.read(path)) != row['sha256']:
            raise ValueError('ExAV file hash drift: ' + path)
    validate_findings(tree, rules)
    registry = (inv.ROOT / 'web/src/lib/game-ui.ts').read_text()
    body = registry.split('export const registry = {', 1)[1].split('} as const satisfies', 1)[0]
    operations = set(re.findall(r"(?m)^  ['\"]?([\w-]+)['\"]?:", body))
    workflows = {}
    for name, row in previous['workflows'].items():
        if set(row['operations']) - operations:
            raise ValueError('Unknown delivered operation: ' + name)
        workflows[name] = dict(candidateDestination=row['destination'], operations=row['operations'],
                               phases=row['phases'], status='historical-donor; ExAV review pending')
    for row in rules['findings']:
        workflows.setdefault(row['workflow'], {}).update(
            candidateDestination=row['destination'], phases=row['phases'],
            status='ExAV finding; per-action closure pending', finding=row['id'])
    old_sources = previous['sourceReviews']
    paths = sorted({r['baselinePath'] for r in ledger['uiBindings'] + ledger['uiSurfaces']})
    sources = {}
    for qualified in paths:
        path = qualified.removeprefix('trunk/')
        donor_path = rules['pathAliases'].get(path, path)
        old = old_sources.get(donor_path)
        workflow = rules['newSourceWorkflows'].get(path, old['workflow'] if old else None)
        scope = ('excluded' if path.startswith('plugins/') and path.split('/')[1] in inv.EXCLUDED
                 else 'source-only-reference' if path.startswith('experiments/')
                 else 'authoring-reference' if path.startswith('tools/') else 'review-required')
        if workflow is None and scope == 'review-required':
            raise ValueError('Unassigned ExAV UI source: ' + path)
        findings = [r['id'] for r in rules['findings']
                    if any(e['path'] == path for e in r['evidence'])]
        sources[qualified] = dict(path=qualified, sha256=inv.digest(tree.read(path)),
            candidateWorkflow=workflow, scope=scope, findings=findings,
            historicalDonor=dict(path=donor_path, revision=historical.revision,
                sha256=inv.digest(historical.read(donor_path)),
                comparison='identical-bytes; dependency closure pending'
                if historical.read(donor_path) == tree.read(path) else 'changed; no automatic review transfer')
            if donor_path in historical.entries else None,
            status='explicit scope disposition' if scope != 'review-required'
            else 'ExAV delta reviewed; field/dependency closure pending' if findings
            else 'candidate mapping; ExAV field/dependency review pending')

    actions = []
    old_surfaces = {r['surfaceId'].removeprefix('base:surface:'): r for r in previous['surfaces']}
    for kind, rows in [('binding', ledger['uiBindings']), ('surface', ledger['uiSurfaces'])]:
        for row in rows:
            source = sources[row['baselinePath']]
            donor = None
            if kind == 'surface':
                identity = row['surfaceId'].split(':surface:', 1)[1].removeprefix('trunk/')
                donor = old_surfaces.get(identity)
            actions.append(dict(id=row['coverageId'] if kind == 'binding' else row['surfaceId'],
                kind=kind, path=row['baselinePath'], sourceSha256=source['sha256'],
                expression=row.get('expression', row.get('handler')),
                sourceProperties=row.get('sourceProperties', []),
                candidateWorkflow=donor['workflow'] if donor else source['candidateWorkflow'],
                historicalExactExpressionDonor=donor, scope=source['scope'],
                findings=source['findings'], status=source['status'], implementationStatus='inventoried'))

    translations = []
    for path in sorted(tree.entries):
        if not path.startswith(('core/', 'plugins/', 'lib/', 'tools/', 'experiments/')) or not path.endswith(('.cs', '.resx', '.html')):
            continue
        donor_path = rules['pathAliases'].get(path, path)
        donors = {r['key']: r for r in text_candidates(historical, donor_path)} if donor_path in historical.entries else {}
        for row in text_candidates(tree, path):
            donor = donors.get(row['key'])
            value = row['originalText']
            english = donor if donor and donor['originalText'].isascii() and re.search('[A-Za-z]', donor['originalText']) else None
            translations.append(dict(row, path='trunk/' + path, sourceSha256=inv.digest(tree.read(path)),
                originalTextSha256=inv.digest(value.encode()),
                candidateWorkflow=sources.get('trunk/' + path, {}).get('candidateWorkflow'),
                scope=next((r['scope'] for r in ledger['fileInventory'] if r['baselinePath'] == 'trunk/' + path), None),
                englishDonor=dict(path=donor_path, revision=historical.revision,
                    sourceSha256=inv.digest(historical.read(donor_path)), **english) if english else None,
                comparison='exact-text-carryover' if donor and donor['originalText'] == value else
                'changed-text; meaning-review-required' if donor else 'new-text; classification/translation-required',
                proposedEnglish=None, reviewStatus='inventoried; classification and wording review pending'))
    contributions = []
    content = json.loads((inv.PARITY_ROOT / 'exav-content-review.json').read_text())
    type_bindings = collections.defaultdict(list)
    for binding in content['typeBindings']:
        type_bindings[binding['owner']].append(binding)
    old_routes = {(r['coverageId'].split(':', 4)[2], r['coverageId'].split(':', 4)[4]): r
                  for r in previous['contributions']}
    for row in ledger['contributions']:
        donor = old_routes.get((row['pluginId'], row['contributionId']))
        contributions.append(dict(coverageId=row['coverageId'], path=row['baselinePath'],
            sourceSha256=row['sourceSha256'], contributionId=row['contributionId'],
            contributionType=row['contributionType'], generatedFrom=row.get('generatedFrom'),
            currentTypeBindings=type_bindings[row['coverageId']],
            scope=row['scope'], historicalRouteDonor=donor,
            candidateWorkflow=donor['workflow'] if donor else None,
            status='registration/consumer review pending; matching ID is not reachability'))
    findings = json.loads(json.dumps(rules['findings']))
    for row in findings:
        for evidence in row['evidence']:
            evidence['sha256'] = inv.digest(tree.read(evidence['path']))
            evidence['path'] = 'trunk/' + evidence['path']
    return dict(schemaVersion=1, source=ledger['source'], sourceId=config['sourceId'],
        status=rules['status'], metadataOnly=True, boundaries=rules['boundaries'],
        inputs={p: inv.digest((inv.ROOT / p).read_bytes()) for p in (
            'doc/parity/exav-baseline.json', 'doc/parity/exav-ledger.json',
            'doc/parity/exav-content-review.json', 'doc/parity/exav-action-rules.json',
            'doc/parity/historical/action-map.json', 'scripts/reconcile-exav-actions.py',
            'web/src/lib/game-ui.ts', 'web/src/lib/localization.ts')},
        workflows=workflows, sourceReviews=list(sources.values()), actions=actions,
        contributions=contributions, findings=findings,
        historicalManualWorkflowCandidates=previous['manualRequirements'],
        workflowReferences=rules['workflowReferences'],
        glossary=rules['glossary'], translationEntries=translations,
        remainingClosure=rules['remainingClosure'],
        counts=dict(uiSources=len(sources), bindings=len(ledger['uiBindings']), surfaces=len(ledger['uiSurfaces']),
            contributions=len(contributions), findings=len(findings), textCandidates=len(translations),
            japaneseTextCandidates=sum(bool(JAPANESE.search(r['originalText'])) for r in translations),
            englishDonorCandidates=sum(r['englishDonor'] is not None for r in translations),
            unclassifiedLiterals=sum(r['classification'].startswith('unclassified') for r in translations),
            scopeDispositionSources=sum(r['scope'] != 'review-required' for r in sources.values()),
            pendingPlayerSourceReviews=sum(r['scope'] == 'review-required' for r in sources.values())))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    read = lambda path: json.loads((inv.PARITY_ROOT / path).read_text())
    config = read('exav-baseline.json')
    tree = inv.Tree(args.source_root, config['revision'], config['subtree'])
    historical = inv.Tree(inv.ROOT, config['historicalComparisonRevision'])
    review = build(tree, historical, config, read('exav-ledger.json'),
                   read('historical/action-map.json'), read('exav-action-rules.json'))
    inv.write(OUTPUT, inv.dump_ledger(review), args.check)
    print(json.dumps(review['counts']))


if __name__ == '__main__':
    main()
