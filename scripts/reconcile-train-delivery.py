#!/usr/bin/env python3
"""Reconcile P5-29 delivery with the immutable P5-01 source inventories."""
import argparse
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / 'doc/parity/train-delivery.json'


def read(path):
    return json.loads((ROOT / path).read_bytes())


def family(path):
    if '.tc.manual/' in path:
        return 'retired-manual'
    if '.tc.tat/' in path:
        return 'timetable'
    if '.colorTest/' in path:
        return 'colors'
    name = Path(path).stem
    return {'TrainControllerDialog': 'controllers', 'TrainPlacementController': 'deployment',
            'TrainTradingDialog': 'trading', 'TrainInformationWindow': 'information',
            'TrainTrackingWindow': 'information', 'TrainGroup': 'groups',
            'DelegationTrainControllerImpl': 'groups', 'SimpleTrainControllerImpl': 'controllers',
            'Train': 'information'}.get(name, 'stock')


def reconcile(catalog, profile, stocks):
    source = read('doc/parity/train-review.json')
    locale = read('doc/parity/train-localization-review.json')
    definitions = {x['id']: x for x in catalog['contributions']}
    recipes = {x['id']: x for x in catalog['renderRecipes']}
    registered = {x['reference']['contributionId'] for x in profile['cars']}
    registered |= {x['contributionId'] for x in profile['colorTemplates']}
    registered |= {x['binding']['contributionId'] for x in profile['trains']}
    expected = {x['id'] for x in source['contributions'] if x['type'] in {'train', 'trainCar', 'colorMapTrainPicture'}}
    assert registered == expected and len(registered) == 21, 'Stock registration closure'
    assert {x['id'] for x in stocks} == {x['binding']['contributionId'] for x in profile['trains']}, 'Purchase catalog closure'
    retired = '{5567FE11-FC2C-4a1d-836F-722EF9DC957C}'
    assert {x['binding']['contributionId'] for x in profile['providers']} == {
        x['id'] for x in source['contributions'] if x['type'] == 'trainController' and x['id'] != retired}, 'Provider closure'
    # Prepared source metadata retains the declaration; canonical providers do not.
    assert retired not in {x['binding']['contributionId'] for x in profile['providers']}, 'Retired Manual provider leaked'
    contributions = []
    for row in source['contributions']:
        identity = row['id']
        result = dict(coverageId=row['coverageId'], id=identity, type=row['type'], operation=row['operation'])
        if identity == retired:
            result['delivery'] = 'owner-removed-2026-11-07'
        else:
            assert identity in definitions, identity
            result['delivery'] = 'registered-gameplay' if identity in registered else 'adapted-browser-action-or-infrastructure'
            result['sourcePath'] = definitions[identity]['sourcePath']
            if identity in registered:
                recipe = recipes[identity]
                assert recipe['status'] == 'ready', identity
                result['recipeId'] = identity
                result['handler'] = recipe['handler']
        contributions.append(result)
    bindings = {
        'stock': ['web/src/lib/stock-catalog.ts', 'src/FreeTrain.Online.Content/RollingStockCatalog.cs', 'web/tests/stock-catalog.test.ts', 'web/tests/train-renderer.test.ts', 'tests/FreeTrain.Online.Tests/RollingStockTests.cs'],
        'trading': ['web/src/FleetControls.svelte', 'web/tests/browser/fleet-trading.spec.ts', 'tests/FreeTrain.Online.Tests/FleetTradingTests.cs'],
        'deployment': ['web/src/FleetDeploymentControls.svelte', 'web/tests/browser/fleet-deployment.spec.ts', 'tests/FreeTrain.Online.Tests/FleetDeploymentTests.cs'],
        'movement': ['src/FreeTrain.Online.Simulation/FleetMovement.cs', 'tests/FreeTrain.Online.Tests/FleetMovementTests.cs', 'web/tests/browser/fleet-deployment.spec.ts'],
        'reversal': ['src/FreeTrain.Online.Simulation/FleetReversal.cs', 'tests/FreeTrain.Online.Tests/FleetRecoveryTests.cs', 'web/tests/browser/fleet-deployment.spec.ts'],
        'timing': ['src/FreeTrain.Online.Simulation/FleetMovement.cs', 'tests/FreeTrain.Online.Tests/FleetTimingTests.cs'],
        'outside': ['src/FreeTrain.Online.Simulation/FleetMovement.cs', 'tests/FreeTrain.Online.Tests/FleetOutsideTests.cs'],
        'controllers': ['web/src/FleetControllerManager.svelte', 'web/tests/browser/fleet-controller-manager.spec.ts', 'tests/FreeTrain.Online.Tests/FleetControllerManagerTests.cs'],
        'groups': ['web/src/FleetGroupManager.svelte', 'web/tests/browser/fleet-groups.spec.ts', 'tests/FreeTrain.Online.Tests/FleetGroupTests.cs'],
        'timetable': ['web/src/FleetRuleEditor.svelte', 'web/tests/browser/fleet-rule-file.spec.ts', 'web/tests/browser/fleet-controller-manager.spec.ts', 'tests/FreeTrain.Online.Tests/FleetAdvancedTimetableTests.cs'],
        'information': ['web/src/TrainInformation.svelte', 'web/tests/browser/train-information.spec.ts', 'web/tests/train-information.test.ts'],
        'colors': ['web/src/TrainColorControls.svelte', 'web/tests/browser/fleet-controller-manager.spec.ts', 'tests/FreeTrain.Online.Tests/FleetColorTests.cs'],
        'retired-manual': ['doc/web-parity-fleet-manual.md', 'tests/FreeTrain.Online.Tests/FleetManualRetirementTests.cs'],
        'catalog-metadata': ['web/src/ObjectProvenance.svelte', 'web/tests/content.test.ts', 'web/tests/localization.test.ts'],
    }
    for paths in bindings.values():
        for path in paths:
            assert (ROOT / path).is_file(), path
    english = read('web/src/locales/en/app.json')
    japanese = read('web/src/locales/ja/app.json')
    ui_keys = {}
    for group, paths in bindings.items():
        keys = sorted({key for path in paths if path.endswith('.svelte')
                       for key in re.findall(r'\btext\.([A-Za-z][A-Za-z0-9_]*)', (ROOT/path).read_text())})
        assert all(key in english and key in japanese and english[key] and japanese[key] for key in keys), 'Missing bilingual UI wording'
        ui_keys[group] = keys
    operation_groups = dict(stock='stock', trade='trading', deploy='deployment', movement='movement',
                            reversal='reversal', outside='outside', simple='controllers', manual='retired-manual',
                            manager='controllers', groups='groups', diagram='timetable', turning='timetable',
                            **{'timing-cost': 'timing', 'tat-junction': 'timetable', 'tat-stop': 'timetable',
                               'tat-advanced': 'timetable', 'information-tracking': 'information', 'color-test': 'colors'})
    assert set(operation_groups) == {x['id'] for x in source['operations']}
    compositions = []
    translations = read('content/localization/exav-stable-bundled/stock-en.json')['entries']
    for stock in stocks:
        recipe = recipes[stock['id']]
        runtime = next(x for x in profile['trains'] if x['binding']['contributionId'] == stock['id'])
        for field in ('company', 'type', 'name', 'author', 'description'):
            assert translations[stock['id']+'/'+field]['original'] == stock[field], 'Stock translation source drift'
        for length in stock['lengths']:
            if runtime['patterns']:
                pattern = next(x for x in runtime['patterns'] if x['length'] == length)
                cars = pattern['cars']
            else:
                cars = [runtime['body']] * length
                if runtime['head']:
                    cars[0] = runtime['head']
                if runtime['tail']:
                    cars[-1] = runtime['tail']
            assert len(cars) == length and all(car in registered for car in cars), 'Formation reference closure'
        assert runtime['pricePerCar'] == int(recipe['fields']['price']), 'Formation price drift'
        assert all(runtime[k] == recipe['fields']['composition'].get(k) for k in ('head', 'body', 'tail')), 'Composition source drift'
        compositions.append(dict(id=stock['id'], lengths=stock['lengths'], head=runtime['head'], body=runtime['body'], tail=runtime['tail']))
    return dict(schemaVersion=1, slice='P5-29', authority=source['authority'],
                catalogHash=hashlib.sha256((ROOT/'src/FreeTrain.Online.Content/bundled-v2.json').read_bytes()).hexdigest(),
                bindings=bindings, uiKeys=ui_keys, contributions=contributions, compositions=compositions,
                operations=[dict(id=x['id'], requirements=x['requirements'], binding=operation_groups[x['id']]) for x in source['operations']],
                sourceTexts=[dict(id=x['id'], source=x['source'], sourceSha256=x['sourceSha256'],
                                  jaSha256=x['jaSha256'], classification=x['classification'], binding=family(x['source']))
                             for x in locale['sourceStrings']],
                catalogTexts=[dict(source=x['source'], sourceSha256=x['sourceSha256'], key=x['key'], jaSha256=x['jaSha256'],
                                   binding='catalog-metadata') for x in locale['catalogStrings']],
                qualifications=['Family bindings reconcile semantics, not verbatim desktop labels. See delivery report for wording adaptations.',
                                'P2 human gallery/native acceptance remains open.',
                                'P6 passenger behavior is not credited by capacity/status presentation.',
                                'Canonical optional V2 repairs and older suites: P2/P9/P10/P11; synthetic source/prepared registration is separate.',
                                'Manual provider removal is an owner decision; prototype manual code is outside canonical gameplay.'])


def generate():
    return reconcile(read('src/FreeTrain.Online.Content/bundled-v2.json'),
                     read('web/src/generated/fleet-profile.json'), read('web/src/generated/stock-catalog.json'))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    result = generate()
    data = json.dumps(result, ensure_ascii=False, indent=2) + '\n'
    if args.check:
        assert OUTPUT.read_text() == data, 'P5 delivery evidence drift; regenerate and review'
    else:
        OUTPUT.write_text(data)
    print(f"P5-29: {len(result['contributions'])} contributions, {len(result['sourceTexts'])} source texts, {len(result['catalogTexts'])} catalog texts reconciled")
