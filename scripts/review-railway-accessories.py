#!/usr/bin/env python3
"""Reconcile every bundled P4 contribution and the stationary-art variants.

The P4-01 source review is immutable input. This separate implementation ledger
records shipped consumers and keeps the P6/P7/optional boundaries explicit.
"""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
LEDGER = ROOT / 'doc/parity/railway-accessories.json'
GROUPS = {'レンガ橋脚': 'Brick pier', 'コン橋脚': 'Concrete pier',
          'レンガ橋脚(側道付)': 'Brick pier with side path',
          'コン橋脚(側道付)': 'Concrete pier with side path',
          'コン橋脚(複合)': 'Composite concrete pier', '信号所': 'Signal box'}


def build():
    review = json.loads((ROOT / 'doc/parity/railway-review.json').read_text())
    catalog = json.loads((ROOT / 'src/FreeTrain.Online.Content/bundled-v2.json').read_text())
    recipes = {r['id']: r for r in catalog['renderRecipes']}
    declarations = {r['id']: r for r in catalog['contributions']}
    hashes = {r['path']: r['sha256'] for r in review['sourceReviews']}
    counts = {}
    rows = []
    for c in review['contributions']:
        recipe, declaration = recipes[c['id']], declarations[c['id']]
        assert recipe['status'] == 'ready'
        assert all(f['status'] in ('ready', 'invisible') for f in recipe['frames'])
        row = {k: c[k] for k in ('coverageId', 'source', 'id', 'type')}
        row.update(sourceSha256=hashes[c['source']], recipe=recipe['id'],
                   portableHandler={'railStationary': 'StationaryConstruction', 'electricPole': 'PoleRailwayConstruction',
                                    'railSignal': 'SignalRailwayConstruction', 'specialRail': {
                                        '{874B8BFD-1EF4-4828-9C5A-D0EA1049D28D}': 'BridgeRailwayConstruction', '{7FD493FF-D6A7-4a13-A471-FB737641D8A0}': 'GirderRailwayConstruction',
                                        '{3E66FC7A-2B58-4182-8A8E-2D5A01BE78E5}': 'TunnelRailwayConstruction', '{93481807-D998-46d7-B1BE-CB2B5D086D67}': 'GarageRailwayConstruction'} .get(c['id'])}[c['type']],
                   status='implemented-browser-portable', owner='P4-18' if c['type'] == 'railStationary' else c['owner'])
        if c['type'] != 'railStationary':
            kind = {'PoleRailwayConstruction': 'pole', 'SignalRailwayConstruction': 'signal',
                    'BridgeRailwayConstruction': 'bridge', 'GirderRailwayConstruction': 'girder',
                    'TunnelRailwayConstruction': 'tunnel', 'GarageRailwayConstruction': 'garage'}[row['portableHandler']]
            row.update(evidenceReport=f'doc/web-parity-{kind}-tool.md',
                       footprintKind='Traffic component; subtype-specific supports/backups remain with the existing handler',
                       orientation={'pole': 'pattern-derived N/S or E/W axis', 'signal': 'N/E/S/W cardinal direction',
                                    'bridge': 'cardinal axis and six endpoint/axis art variants',
                                    'girder': 'eight-way axis and four art variants',
                                    'tunnel': 'cardinal axis and two art variants',
                                    'garage': 'cardinal axis and two art variants'}[kind],
                       verification=[f'{kind.capitalize()}ConstructionTests', f'{kind}-construction.spec.ts',
                                     'railway-renderer.test.ts'])
        if c['type'] == 'railStationary':
            group = next(n['text'] for n in declaration['declaration']['children'] if n['tag'] == 'group')
            counts[group] = counts.get(group, 0) + 1
            row.update(groupJa=group, groupEn=GROUPS[group], variant=counts[group],
                       footprint=[int(n) for n in c['size'].split(',')] + [int(c['height'])],
                       orientation='baked into the exact contribution; no rotation or GUID normalization',
                       completionPrice=int(c['price']), completionGenre='RAIL_SERVICE',
                       removalPrice=500_000, removalGenre='SUBSIDIARIES',
                       cancellationPrice=0, construction='durable per-cell source states and P1 one-shot FIFO callbacks',
                       gameplay='decorative stationary structure; not elected track support or a train controller',
                       verification=['StationaryConstructionTests', 'StationarySessionTests',
                                     'railway-renderer.test.ts', 'stationary-construction.spec.ts'])
        rows.append(row)
    assert len(rows) == 64 and sum(r['type'] == 'railStationary' for r in rows) == 54
    return dict(schemaVersion=1, slice='P4-18', authority=review['authority'],
                sourceReviewSha256=review['reviewSha256'], contributions=rows,
                dispositions=[
                    dict(scope='rail-attached poles', owner='P4-17', disposition='four implemented ElectricPole variants; one Traffic accessory slot'),
                    dict(scope='automatic rail/road crossing', owner='P4-20/P7', disposition='conditional Traffic accessory; trusted boundary fixtures only, road authoring remains P7'),
                    dict(scope='GenericStructure and generic land/commercial structures', owner='P7', disposition='not railStationary; prepared art does not register gameplay'),
                    dict(scope='station, passage and stair contributions including HalfVoxelStructure', owner='P6', disposition='not railStationary; no passenger or platform workflow credit'),
                    dict(scope='roadAccessory extension group', owner='P7', disposition='no bundled declarations; not a P4 construction tool'),
                    dict(scope='optional packs and unresolved source crops', owner='P2/P9/P10/P11/POST-ASSETS-07', disposition='separate existing gates; no new runtime claim'),
                    dict(scope='ConstructionSite shared P7 consumer', owner='P7', disposition='P4 delivers the shared source lifecycle and construction random stream; P7 must reuse it for its own structures')])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    value = build()
    text = json.dumps(value, ensure_ascii=False, indent=2) + '\n'
    if args.check:
        assert LEDGER.read_text() == text, 'Railway accessory implementation ledger is stale'
        profile = json.loads((ROOT / 'web/src/generated/railway-profile.json').read_text())
        bindings = {b['reference']['contributionId']: b for b in profile['bindings']}
        for row in value['contributions']:
            assert bindings[row['id']]['reference']['recipeId'] == row['recipe']
            assert row['portableHandler']
        generated = json.loads((ROOT / 'web/src/generated/stationary-types.json').read_text())
        expected = [r for r in value['contributions'] if r['type'] == 'railStationary']
        assert [r['id'] for r in generated] == [r['id'] for r in expected]
        for actual, source in zip(generated, expected):
            assert [actual['sizeX'], actual['sizeY'], actual['height']] == source['footprint']
            assert actual['price'] == source['completionPrice']
            assert all(actual[k] == source[k] for k in ('groupEn', 'groupJa', 'variant'))
    else:
        LEDGER.write_text(text)
    print('P4 accessory bindings: 64; stationary variants: 54; explicit scope dispositions: 7')


if __name__ == '__main__':
    main()
