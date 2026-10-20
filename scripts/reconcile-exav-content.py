#!/usr/bin/env python3
"""Reconcile stable bundled ExAV content from explicitly supplied pinned Git objects.

Never discover sibling checkouts, execute legacy code, or write historical outputs.
"""
import argparse
import collections
import importlib.util
import json
import re
from pathlib import Path
import xml.etree.ElementTree as ET

SPEC = importlib.util.spec_from_file_location('content', Path(__file__).with_name('reconcile-parity-content.py'))
content = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(content)
inv = content.inv
EXAV_SPEC = importlib.util.spec_from_file_location('parity_exav', Path(__file__).with_name('parity_exav.py'))
parity_exav = importlib.util.module_from_spec(EXAV_SPEC)
EXAV_SPEC.loader.exec_module(parity_exav)

OUTPUT = inv.PARITY_ROOT / 'exav-content-review.json'
RULES = inv.PARITY_ROOT / 'exav-content-rules.json'


def presentation_nodes(root):
    """Conservative presentation inventory; retain stable IDs separately from text."""
    fields = {'name', 'title', 'description', 'oneLineDescription', 'group',
              'subgroup', 'design', 'category', 'tooltip', 'help', 'caption'}
    result = {}

    def walk(node, key):
        value = (node.text or '').strip()
        if value and not list(node) and (node.tag in fields or re.search(r'[\u3040-\u30ff\u3400-\u9fff]', value)):
            result[key + '/text()'] = value
        for attr, value in node.attrib.items():
            if re.search(r'[\u3040-\u30ff\u3400-\u9fff]', value):
                result[key + '/@' + attr] = value
        counts = collections.Counter()
        for child in node:
            index = counts[child.tag]
            counts[child.tag] += 1
            identity = ('contribution:' + child.get('id') if child.tag == 'contribution'
                        and child.get('id') is not None else f'{child.tag}[{index}]')
            walk(child, key + '/' + identity)
    walk(root, root.tag)
    return result


def translations(tree, historical):
    rows = []
    for path in sorted(tree.entries):
        if not path.startswith('plugins/') or not path.lower().endswith('.xml'):
            continue
        is_manifest = path.lower().endswith('/plugin.xml')
        root = inv.manifest(tree.read(path), allow_cp932=True)[0] if is_manifest else ET.fromstring(tree.text(path)[0])
        donors = {}
        if path in historical.entries:
            old = inv.manifest(historical.read(path))[0] if is_manifest else ET.fromstring(historical.text(path)[0])
            donors = presentation_nodes(old)
        for key, value in presentation_nodes(root).items():
            donor = donors.get(key)
            rows.append(dict(path=path, sourceSha256=inv.digest(tree.read(path)), key=key,
                             originalText=value, originalTextSha256=inv.digest(value.encode()),
                             englishDonor=dict(revision=historical.revision, path=path, key=key,
                                               text=donor, sourceSha256=inv.digest(historical.read(path)))
                             if donor and donor.isascii() and re.search('[A-Za-z]', donor) else None,
                             comparison='exact-text-carryover' if donor == value else
                             'changed-text; meaning-review-required' if donor else 'new-text; translation-required',
                             proposedEnglish=None, reviewStatus='inventoried; wording review with implementing feature',
                             scope='excluded' if path.split('/')[1] in inv.EXCLUDED else 'in-scope'))
    return rows


def project_rows(tree, config):
    projects, edges, _ = parity_exav.project_inventory(inv, tree, config)
    return [dict(path=p['path'], sha256=p['sha256'], assemblies=p['assemblies'],
                 listedInSelectedSolution=p['listedInSelectedSolution'],
                 compile=[dict(paths=e['targets']) for e in edges
                          if e['project'] == p['path'] and e['kind'] == 'Compile'],
                 embeddedResources=[dict(include=e['value'], paths=e['targets'], resolution=e['resolution'])
                                    for e in edges if e['project'] == p['path'] and e['kind'] == 'EmbeddedResource'],
                 content=[dict(include=e['value'], paths=e['targets'], resolution=e['resolution'])
                          for e in edges if e['project'] == p['path'] and e['kind'] in ('Content', 'None')])
            for p in projects if p['path'].startswith(('core/', 'plugins/', 'lib/')) and p['path'].endswith('.csproj')]


def build(tree, config, rules, historical):
    if tree.revision != config['revision']:
        raise ValueError('ExAV revision differs from configuration')
    base = inv.build_pack(tree, 'base', 'plugins/', allow_cp932=True,
                          south_token=config['generatedRegistration']['southToken'])
    if (base['counts']['manifests'], base['counts']['declared']) != (config['expectedManifests'], config['expectedDeclarations']):
        raise ValueError('ExAV manifest/declaration denominator drift')
    review = content.reconcile(tree, base, rules, project_rows=project_rows(tree, config),
                               allow_cp932=True, south_token=config['generatedRegistration']['southToken'])
    if any(r['effectiveImplementation']['resolution'] != 'source-and-project' for r in review['recipes']):
        raise ValueError('Unresolved effective implementation')
    # The archive puts its distribution notices under doc/, not at the subtree root.
    notices = [dict(path=p, sha256=inv.digest(tree.read(p))) for p in sorted(tree.entries)
               if p.startswith('doc/') and re.search(r'/(?:readme[^/]*|copying[^/]*)$', p, re.I)]
    review['notices'].extend(notices)
    for asset in review['assets']:
        asset['notices'].extend(notices)
        asset['attributionDisposition'] = ('Manifest author declarations and stable distribution notices retained; '
                                           'no per-image authorship inferred; optional packs excluded')
    review['generatedChildren'] = [r for r in base['contributions'] if r.get('generatedFrom')]
    review['translationEntries'] = translations(tree, historical)
    review['counts']['translationEntries'] = len(review['translationEntries'])
    review['counts']['translationDonors'] = sum(bool(r['englishDonor']) for r in review['translationEntries'])
    # Preserve comparisons as evidence, never interpret matching bytes as dependency closure.
    for group in ('rules', 'stateRules'):
        for rule in review['rules'][group]:
            rule['historicalSourceComparison'] = [dict(path=p,
                historicalSha256=inv.digest(historical.read(p)) if p in historical.entries else None,
                result='same-normalized-code' if p in historical.entries and
                    re.sub(r'\s+', '', content.source_code(tree.text(p)[0])) ==
                    re.sub(r'\s+', '', content.source_code(historical.text(p)[0])) else 'changed-or-new; ExAV rules apply')
                for p in rule['sources']]
    review['registrationSites'] = []
    for path in sorted(tree.entries):
        if path.startswith(('core/', 'plugins/')) and path.endswith('.cs'):
            code = content.source_code(tree.text(path)[0])
            for match in re.finditer(r'\b(?:addContribution|PictureManager\.add)\s*\(', code):
                review['registrationSites'].append(dict(path=path, sha256=inv.digest(tree.read(path)),
                    line=code.count('\n', 0, match.start()) + 1, symbol=match[0],
                    disposition='generic generated children' if path.endswith('GenericStructureContribution.cs') else
                    'manifest registration' if path.endswith('/Plugin.cs') else
                    'registry implementation' if path.endswith('/PluginManager.cs') else 'picture registry'))
    # Reuse the historical pixel derivation only after checking its exact image payload.
    tat = 'plugins/org.kohsuke.freetrain.tc.tat/'
    resx = tat + 'src/JunctionConfigDialog.resx'
    payload = lambda data: ''.join(ET.fromstring(data).find("data[@name='imageList.ImageStream']/value").text.split())
    if payload(tree.read(resx)) != payload(historical.read(resx)) or any(
            tree.read(tat + 'res/' + name + '.gif') != historical.read(tat + 'res/' + name + '.gif')
            for name in ('Straight', 'Curve')):
        raise ValueError('TAT image evidence changed; redo visual derivation')
    review['tatImageEvidence'] = dict(path=resx, sha256=inv.digest(tree.read(resx)),
        normalizedBase64Sha256=inv.digest(payload(tree.read(resx)).encode()),
        historicalRevision=historical.revision, historicalEvidence='doc/parity/historical/content-rules.json#CONTENT-001',
        result='identical ImageStream payload and GIF bytes; historical pixel derivation reusable; project gap retained')
    review.update(packId='exav-stable-bundled', sourceId=config['sourceId'],
                  source=dict(repository=config['repository'], revision=tree.revision,
                              subtree=config['subtree'], engineVersion=config['engineVersion']),
                  generatorSources={p: inv.digest((inv.ROOT / p).read_bytes()) for p in (
                      'scripts/reconcile-exav-content.py', 'scripts/reconcile-parity-content.py',
                      'scripts/inventory-parity.py', 'scripts/parity_exav.py')},
                  rulesSha256=inv.digest(RULES.read_bytes()))
    # Archive paths and coverage identities remain distinct from historical outputs.
    def qualify(value, key=None):
        if isinstance(value, dict):
            if key == 'englishDonor':
                return value  # Donor paths belong to original FreeTrain, outside trunk/.
            return {k: qualify(v, k) for k, v in value.items()}
        if isinstance(value, list):
            return [qualify(v, key) for v in value]
        if isinstance(value, str):
            if value.startswith('base:'):
                return config['sourceId'] + ':' + value[5:]
            if key in ('path', 'baselinePath', 'sources', 'otherSources', 'projects', 'paths',
                       'targets', 'owner', 'evidence', 'source') and value in tree.entries:
                return config['subtree'] + '/' + value
        return value
    return qualify(review)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    config = json.loads((inv.PARITY_ROOT / 'exav-baseline.json').read_text())
    tree = inv.Tree(args.source_root, config['revision'], config['subtree'])
    rules = json.loads(RULES.read_text())
    historical = inv.Tree(inv.ROOT, config['historicalComparisonRevision'])
    review = build(tree, config, rules, historical)
    inv.write(OUTPUT, inv.dump_ledger(review), args.check)
    print(json.dumps(review['counts']))


if __name__ == '__main__':
    main()
