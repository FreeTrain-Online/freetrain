#!/usr/bin/env python3
"""Separate pinned ExAV P0-05 inventory and opt-in inert sample inspection."""
import argparse
from collections import Counter
import importlib.util
import json
import re
from pathlib import Path

import parity_serialization

SPEC = importlib.util.spec_from_file_location('persistence_review', Path(__file__).with_name('review-parity-persistence.py'))
old = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(old)
inv = old.inventory
OUT = inv.PARITY_ROOT


def validate_rules(tree, config, rules, plan, implementation):
    for document in (rules, plan):
        if (document['sourceId'], document.get('revision', document.get('baseRevision')), document['subtree']) != (config['sourceId'], config['revision'], config['subtree']):
            raise ValueError('Persistence review source profile mismatch')
    if rules['implementationRevision'] != implementation.revision or plan['implementationRevision'] != implementation.revision:
        raise ValueError('Persistence implementation pin mismatch')
    for source, rows in ((tree, rules['sourceReviews']), (implementation, rules['implementationSourceReviews'])):
        if len({r['path'] for r in rows}) != len(rows):
            raise ValueError('Duplicate persistence source review')
        for row in rows:
            if inv.digest(source.read(row['path'])) != row['sha256']:
                raise ValueError('Persistence review hash drift: ' + row['path'])


def build(tree, ledger, config):
    if tree.revision != config['revision'] or ledger['revision'] != tree.revision:
        raise ValueError('ExAV persistence baseline mismatch')
    result = parity_serialization.inventory(tree, dict(serializationCandidates=[dict(path=r['path'].removeprefix(config['subtree'] + '/')) for r in ledger['serializationCandidates']]), inv.digest)
    result.update(sourceId=config['sourceId'], subtree=config['subtree'])
    historical = json.loads((OUT / 'historical/serialization-review.json').read_text())
    previous = {r['path']: r for r in historical['sourceReviews']}
    prior_types = {r['sourceTypeId']: r for r in historical['types']}
    files = {r['baselinePath'].removeprefix(config['subtree'] + '/'): r for r in ledger['fileInventory']}
    for row in result['sourceReviews']:
        prior = previous.get(row['path'])
        row['comparisonWithHistorical'] = ('new-path' if not prior else
            'identical-bytes' if prior['sha256'] == row['sha256'] else 'changed')
        row['projectMembership'] = files[row['path']]['projectMembership']
        row['scope'] = files[row['path']]['scope']
        # These attribute-driven hooks are additional to IDeserializationCallback.
        # Preserve lexical source evidence without invoking methods/initializers.
        source = tree.text(row['path'])[0]
        row['serializationAttributes'] = [dict(line=source[:match.start()].count('\n') + 1,
            declaration=match[0].strip()) for match in re.finditer(
                r'\[(?:OnDeserialized|OnDeserializing|OnSerialized|OnSerializing|OptionalField)\b[^\]]*\]\s*[^;{]+[;{]', source)]
    comparisons = []
    for row in result['types']:
        prior = prior_types.get(row['sourceTypeId'])
        keys = ('fields', 'bases', 'customKeys', 'substitutions', 'callbacks', 'serializationMode', 'customWriter')
        comparisons.append(dict(sourceTypeId=row['sourceTypeId'], changedFamilies=[k for k in keys if not prior or row[k] != prior[k]],
                                disposition='new-type' if not prior else 'lexical-comparison-only'))
    result['historicalComparison'] = dict(revision=historical['revision'], types=comparisons,
        absentPaths=sorted(set(previous) - {r['path'] for r in result['sourceReviews']}))
    result['counts'].update(staticOrConstantFields=sum(f['exclusion'] == 'static-or-constant' for t in result['types'] for f in t['fields']),
        nonserializedFields=sum(f['exclusion'] == 'nonserialized' for t in result['types'] for f in t['fields']),
        enums=sum(t['kind'] == 'enum' for t in result['types']))
    return result


def sample_summary(report, provenance):
    summary = {k: v for k, v in report.items() if k not in (
        'recordCounts', 'limits', 'roots', 'sourceLayoutComparison')}
    summary['provenance'] = provenance
    summary['roots'] = [{k: v for k, v in root.items() if k != 'classLayouts'} for root in report['roots']]
    summary['layoutComparisonCounts'] = dict(Counter(r['disposition'] for r in report['sourceLayoutComparison']))
    summary['fieldNameDifferences'] = [r for r in report['sourceLayoutComparison'] if 'difference' in r['disposition']]
    return summary


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', required=True, type=Path)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--sample', type=Path)
    parser.add_argument('--sample-report', type=Path)
    args = parser.parse_args()
    if bool(args.sample) != bool(args.sample_report):
        parser.error('--sample and --sample-report must be used together')
    if args.sample_report and (not args.sample_report.resolve().is_relative_to((inv.ROOT / 'tmp').resolve()) or args.sample_report.resolve() == args.sample.resolve()):
        parser.error('--sample-report must be a separate file under tmp/')
    config = json.loads((OUT / 'exav-baseline.json').read_text())
    ledger = json.loads((OUT / 'exav-ledger.json').read_text())
    tree = inv.Tree(args.source_root, config['revision'], config['subtree'])
    rules = json.loads((OUT / 'exav-persistence-rules.json').read_text())
    plan = json.loads((OUT / 'exav-domain-transition.json').read_text())
    implementation = inv.Tree(inv.ROOT, rules['implementationRevision'])
    validate_rules(tree, config, rules, plan, implementation)
    result = build(tree, ledger, config)
    result['reviewInputs'] = {name: inv.digest((OUT / name).read_bytes()) for name in (
        'exav-baseline.json', 'exav-persistence-rules.json', 'exav-domain-transition.json')}
    result['generatorSources'] = {name: inv.digest((inv.ROOT / 'scripts' / name).read_bytes()) for name in (
        'review-exav-persistence.py', 'parity_serialization.py', 'review-parity-persistence.py', 'parity_nrbf.py')}
    inv.write(OUT / 'exav-serialization-review.json', inv.dump_ledger(result), args.check)
    print(json.dumps(result['counts'], sort_keys=True))
    if args.sample:
        report = old.inspect_sample(args.sample, result, ledger)
        report.update(sourceId=config['sourceId'], subtree=config['subtree'])
        inv.write(args.sample_report, inv.dump_ledger(report), args.check)
        # Only the explicitly supplied, hash-identified sample updates its known
        # summary. Default generation never discovers or needs owner files.
        sample_index = json.loads((OUT / 'exav-save-samples.json').read_text())
        for index, known in enumerate(sample_index['samples']):
            if known['sampleSha256'] == report['sampleSha256']:
                sample_index['samples'][index] = sample_summary(report, known['provenance'])
                inv.write(OUT / 'exav-save-samples.json', inv.dump_ledger(sample_index), args.check)
                break
        print(json.dumps({k: report[k] for k in ('sampleSha256', 'sampleBytes', 'worldFacts')}, ensure_ascii=False))


if __name__ == '__main__':
    main()
