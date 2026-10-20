#!/usr/bin/env python3
"""Reproduce reviewed ExAV geometry without repinning historical artifacts."""
import argparse
import importlib.util
import json
from pathlib import Path
import tempfile

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('rail_gallery', HERE / 'parity-rail-gallery.py')
rail = importlib.util.module_from_spec(spec)
spec.loader.exec_module(rail)
inv = rail.inventory
OUT = inv.PARITY_ROOT
expanded = rail.load('parity_exav_gallery', HERE / 'parity_exav_gallery.py')


def validate_dependencies(tree, rules, dependencies):
    if dependencies is None or any(dependencies[key] != rules[key]
                                   for key in ('sourceId', 'revision', 'subtree')):
        raise ValueError('Geometry dependency review does not match baseline')
    rows = dependencies['sourceReviews']
    paths = {row['path'] for row in rows}
    if len(paths) != len(rows):
        raise ValueError('Duplicate dependency source review')
    for row in rows:
        if inv.digest(tree.read(row['path'])) != row['sha256']:
            raise ValueError('Dependency source review hash drift: ' + row['path'])
    chains = dependencies['chains']
    if {chain['id'] for chain in chains} != {
            'EXAV-DEP-DRAW', 'EXAV-DEP-TERRAIN', 'EXAV-DEP-HOST', 'EXAV-DEP-TIMING'} or len(chains) != 4:
        raise ValueError('Missing geometry dependency chain')
    seen, covered = set(), set()
    for chain in chains:
        files = set(chain['files'])
        if not files <= paths or not chain['steps'] or not chain['handoffs']:
            raise ValueError('Incomplete dependency chain: ' + chain['id'])
        covered.update(files)
        for step in chain['steps']:
            if step['id'] in seen or not step['acceptanceCases'] or not step['phase']:
                raise ValueError('Invalid dependency step: ' + step['id'])
            seen.add(step['id'])
            for endpoint in ('caller', 'dependency'):
                if step[endpoint]['path'] not in files or not step[endpoint]['symbol']:
                    raise ValueError('Unreviewed dependency endpoint: ' + step['id'])
    if covered != paths:
        raise ValueError('Dependency sources lack chain dispositions')


def validate_gallery_review(gallery, review):
    if (review['status'] != 'source-gallery-review-complete' or
            review['sourceId'] != gallery['sourceId'] or review['revision'] != gallery['revision'] or
            review['gallerySha256'] != inv.digest(inv.dump_ledger(gallery)) or
            review['counts'] != gallery['counts'] or review['contactsReviewed'] != gallery['contacts']):
        raise ValueError('Expanded gallery review is missing or stale; inspect regenerated contacts before closure')


def build(tree, historical, rules, dependencies=None):
    if tree.revision != rules['revision']:
        raise ValueError('ExAV geometry requires a fresh source review')
    paths = [r['path'] for r in rules['sourceReviews']]
    if len(paths) != len(set(paths)) or not set(rail.semantics.SOURCES) <= set(paths):
        raise ValueError('Incomplete or duplicate ExAV source reviews')
    for row in rules['sourceReviews'] + rules['gallerySourceReviews']:
        if inv.digest(tree.read(row['path'])) != row['sha256']:
            raise ValueError('ExAV source review hash drift: ' + row['path'])
    validate_dependencies(tree, rules, dependencies)
    geometry = rail.extract(tree)
    geometry['behaviorTable'] = 'doc/parity/exav-geometry-behavior.json'
    geometry['sourceId'] = rules['sourceId']
    geometry['subtree'] = rules['subtree']
    behavior = rail.semantics.extract_reviewed_tables(tree, geometry, inv.digest)
    behavior.update(sourceId=rules['sourceId'], subtree=rules['subtree'],
                    reviewStatus=rules['status'], sourceReviews=rules['sourceReviews'],
                    exavDifferences=rules['differences'], openWork=rules['openWork'])
    behavior['dependencyReview'] = dependencies
    behavior['reviewInputs'] = {
        path: inv.digest((inv.ROOT / path).read_bytes()) for path in (
            'doc/parity/exav-geometry-rules.json', 'doc/parity/exav-baseline.json',
            'doc/parity/exav-geometry-dependency-review.json',
            'scripts/review-exav-geometry.py', 'scripts/parity-rail-gallery.py',
            'scripts/parity_geometry.py', 'scripts/parity_exav_gallery.py', 'scripts/import-web-content.py')}
    # Compare formulas and raw bytes separately: matching art does not validate
    # changed callers, money, capacities, host selection or native wrappers.
    old = rail.extract(historical)
    comparisons = []
    assets = ['core/res/' + n for n in ('RailRoads.bmp', 'RailRoads_n.bmp', 'ugslope.bmp', 'BridgePier.bmp')]
    assets += [p for p in tree.entries if p.endswith('.bmp') and (
        p.startswith('plugins/org.kohsuke.freetrain.trains/') or
        p.startswith('plugins/org.kohsuke.freetrain.rail/') or
        p.startswith('plugins/SpeedLimit/') or
        p.startswith('plugins/system/renraku') or
        p.startswith('plugins/system/ThinPlatform'))]
    for path in sorted(set(paths + assets + [r['path'] for r in dependencies['sourceReviews'] + rules['gallerySourceReviews']])):
        current = inv.digest(tree.read(path))
        previous = inv.digest(historical.read(path)) if path in historical.entries else None
        comparisons.append(dict(path='trunk/' + path, exavSha256=current,
                                historicalSha256=previous,
                                result='identical-bytes' if current == previous else
                                'changed' if previous else 'new-path'))
    behavior['historicalComparison'] = dict(revision=historical.revision,
        patternsEqual=geometry['patterns'] == old['patterns'],
        directionIndicesAndVectorsEqual=[(d['index'], d['offset']) for d in geometry['directions']] ==
                                      [(d['index'], d['offset']) for d in old['directions']], files=comparisons)
    behavior['speedLimitCases'] = [dict(nativeMinutes=n, low=4,
        medium=n if n == 4 else 3, fast=n if n in (3, 4) else 2,
        unrestricted=n) for n in (1, 2, 3, 4, 5)]
    # C# integer division truncates toward zero; Python // differs for negatives.
    trunc2 = lambda n: n // 2 if n >= 0 else -((-n) // 2)
    behavior['platformStopCases'] = [dict(platformLength=p, trainLength=t,
        along=trunc2(p+t)-1, against=trunc2(p-t),
        alongStops=0 <= trunc2(p+t)-1 < p, againstStops=0 <= trunc2(p-t) < p)
        for p, t in ((4, 3), (4, 4), (4, 5), (4, 6), (5, 4))]
    behavior['thinRoofCases'] = [dict(length=n, withRoof=roof,
        indices=[i for i in range(n) if roof and n//4 <= i < n-n//4])
        for n in (1, 3, 4, 5, 8) for roof in (False, True)]
    behavior['movementCostCases'] = [dict(length=3, passengers=39, successfulMove=i,
        charge=122000 if (i-1) % 4 == 0 else 0) for i in range(1, 10)]
    behavior['acceptanceBoundary'] = 'Source reassessment only; see reviewStatus/openWork. No runtime parity claim.'
    return geometry, behavior


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', required=True, type=Path)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--gallery', type=Path)
    args = parser.parse_args()
    if args.gallery and not args.gallery.resolve().is_relative_to((inv.ROOT / 'tmp').resolve()):
        parser.error('--gallery must be under this checkout\'s tmp/')
    config = json.loads((OUT / 'exav-baseline.json').read_text())
    rules = json.loads((OUT / 'exav-geometry-rules.json').read_text())
    dependencies = json.loads((OUT / 'exav-geometry-dependency-review.json').read_text())
    if (rules['sourceId'], rules['revision'], rules['subtree']) != (
            config['sourceId'], config['revision'], config['subtree']):
        raise ValueError('ExAV geometry rules do not match baseline')
    tree = inv.Tree(args.source_root, config['revision'], config['subtree'])
    historical = inv.Tree(inv.ROOT, config['historicalComparisonRevision'])
    geometry, behavior = build(tree, historical, rules, dependencies)
    with tempfile.TemporaryDirectory(prefix='exav-expanded-gallery-') as folder:
        target = args.gallery / 'expanded' if args.gallery else Path(folder)
        gallery = expanded.render(tree, geometry, rail, target)
    gallery['sourceId'] = config['sourceId']
    gallery['sourceReviews'] = rules['gallerySourceReviews']
    gallery['reviewInputs'] = behavior['reviewInputs']
    if args.check:
        validate_gallery_review(gallery, json.loads((inv.ROOT / rules['galleryReview']).read_text()))
    behavior['expandedGallery'] = dict(path='doc/parity/exav-geometry-gallery.json',
                                       counts=gallery['counts'], boundary=gallery['evidence'])
    inv.write(OUT / 'exav-geometry-gallery.json', inv.dump_ledger(gallery), args.check)
    for name, value in [('exav-geometry.json', geometry), ('exav-geometry-behavior.json', behavior)]:
        inv.write(OUT / name, inv.dump_ledger(value), args.check)
    if args.gallery:
        rail.gallery(tree, geometry, args.gallery)
        count = rail.semantics.gallery(tree, geometry, behavior, args.gallery, rail.images)
        # Add navigation without modifying the historical gallery implementation.
        index = args.gallery / 'index.html'
        index.write_text(index.read_text() + '<p><a href="expanded/index.html">Expanded ExAV girder/platform review</a></p>')
        print(f'Rendered {count} rail/car + {gallery["counts"]["compositions"]} expanded ExAV offline compositions')
    print(f'ExAV geometry: {len(geometry["patterns"])} patterns/supports, {len(rules["sourceReviews"])} source reviews')


if __name__ == '__main__':
    main()
