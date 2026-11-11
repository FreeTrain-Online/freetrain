#!/usr/bin/env python3
"""Reconcile P6-27 delivery without rewriting the historical P6-01 source review."""
import argparse
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / 'doc/parity/station-delivery.json'


def read(path):
    return json.loads((ROOT / path).read_bytes())


def sha(path):
    return hashlib.sha256((ROOT / path).read_bytes()).hexdigest()


# Explicit runtime and independently executable witnesses: existence is binding
# closure, never a claim that a test ran. Fresh execution is recorded in the report.
BINDINGS = {
    'station-building': ['src/FreeTrain.Online.Simulation/StationBuildingConstruction.cs', 'web/src/StationBuildingControls.svelte', 'tests/FreeTrain.Online.Tests/StationBuildingConstructionTests.cs', 'web/tests/browser/station-building-domain.spec.ts'],
    'thin-platform': ['src/FreeTrain.Online.Simulation/ThinPlatformConstruction.cs', 'web/src/PlatformControls.svelte', 'tests/FreeTrain.Online.Tests/ThinPlatformConstructionTests.cs', 'web/tests/browser/thin-platform.spec.ts'],
    'wide-platform': ['src/FreeTrain.Online.Simulation/WidePlatformConstruction.cs', 'web/src/WidePlatformLanes.svelte', 'tests/FreeTrain.Online.Tests/WidePlatformLaneTests.cs', 'web/tests/browser/platform-lanes.spec.ts'],
    'host-graph': ['src/FreeTrain.Online.Simulation/StationHostConnections.cs', 'src/FreeTrain.Online.Simulation/PlatformProperties.cs', 'web/src/PlatformProperties.svelte', 'tests/FreeTrain.Online.Tests/StationHostConnectionTests.cs', 'web/tests/browser/station-hosts.spec.ts', 'web/tests/browser/platform-properties.spec.ts'],
    'passage-stairs': ['src/FreeTrain.Online.Simulation/PassageConstruction.cs', 'web/src/PassageControls.svelte', 'tests/FreeTrain.Online.Tests/PassageConstructionTests.cs', 'web/tests/browser/passage.spec.ts'],
    'providers': ['src/FreeTrain.Online.Simulation/StationCatchment.cs', 'tests/FreeTrain.Online.Tests/StationDemandTests.cs', 'doc/web-parity-station-demand.md'],
    'listeners': ['src/FreeTrain.Online.Simulation/StationCatchment.cs', 'tests/FreeTrain.Online.Tests/StationDemandTests.cs', 'doc/web-parity-station-demand.md'],
    'timers-logs': ['src/FreeTrain.Online.Simulation/StationStatistics.cs', 'src/FreeTrain.Online.Simulation/StationLifecycle.cs', 'web/src/StationInformation.svelte', 'tests/FreeTrain.Online.Tests/StationStatisticsTests.cs', 'web/tests/browser/station-information.spec.ts'],
    'passenger-fare': ['src/FreeTrain.Online.Simulation/StationPassengerExchange.cs', 'tests/FreeTrain.Online.Tests/StationPassengerExchangeTests.cs', 'tests/FreeTrain.Online.Tests/StationPassengerServiceTests.cs', 'web/tests/browser/passenger-service.spec.ts'],
    'yard-controller': ['src/FreeTrain.Online.Simulation/StationYardStop.cs', 'web/src/FleetRuleEditor.svelte', 'tests/FreeTrain.Online.Tests/StationControllerMatrixTests.cs', 'web/tests/browser/station-controller.spec.ts'],
    'departure-events': ['src/FreeTrain.Online.Simulation/StationYardStop.cs', 'web/src/lib/browser-audio.ts', 'web/src/PlatformProperties.svelte', 'tests/FreeTrain.Online.Tests/StationDepartureBellTests.cs', 'web/tests/browser/departure-bells.spec.ts'],
    'information-list': ['web/src/StationList.svelte', 'web/src/StationInformation.svelte', 'web/src/lib/station-information.ts', 'tests/FreeTrain.Online.Tests/StationInformationTests.cs', 'web/tests/browser/station-information.spec.ts'],
    'p4-decoration': ['src/FreeTrain.Online.Simulation/StationaryConstruction.cs', 'tests/FreeTrain.Online.Tests/StationaryConstructionTests.cs', 'web/tests/browser/stationary-construction.spec.ts'],
}


def text_binding(path):
    name = Path(path).stem
    if '/population/' in path:
        return 'providers'
    return {'PlatformController': 'station-building', 'PlatformPropertyDialog': 'host-graph',
            'StationPropertyDialog': 'station-building', 'StationInformationWindow': 'information-list',
            'StationPassagewayController': 'passage-stairs', 'DepartureBellContribution': 'departure-events',
            'PassagewayRail': 'passage-stairs', 'FatPlatform': 'wide-platform', 'ThinPlatform': 'thin-platform',
            'Platform': 'host-graph', 'Station': 'timers-logs', 'GlobalTrafficMonitor': 'passenger-fare',
            'Train': 'passenger-fare', 'SimpleTrainControllerImpl': 'yard-controller',
            'DelegationTrainControllerImpl': 'yard-controller'}[name]


def reconcile(catalog, profile, english, japanese):
    source = read('doc/parity/station-review.json')
    locale = read('doc/parity/station-localization-review.json')
    assert set(BINDINGS) - {'p4-decoration'} == {x['id'] for x in source['operations']}, 'Operation closure'
    definitions = {x['id']: x for x in catalog['contributions']}
    recipes = {x['id']: x for x in catalog['renderRecipes']}
    expected = {x['id'] for x in source['contributions'] if x['type'] == 'station'}
    registered = {x['reference']['contributionId'] for x in profile['definitions']}
    assert registered == expected and len(registered) == 35, 'Station registration closure'
    assert {x['contributionId'] for x in profile['bells']} == {
        x['id'] for x in source['contributions'] if x['type'] == 'trainDepartureBell'}, 'Bell registration closure'
    contributions = []
    for row in source['contributions']:
        identity = row['id']
        assert identity in definitions, identity
        kind = row['type']
        binding = {'station': 'station-building', 'trainDepartureBell': 'departure-events',
                   'dockingContent': 'information-list', 'railStationary': 'p4-decoration', 'bgm': None}[kind]
        result = dict(id=identity, coverageId=row['coverageId'], type=kind, binding=binding,
                      delivery='deferred-P2-audio' if kind == 'bgm' else 'runtime-bound', sourceSha256=row['sourceSha256'])
        if kind in {'station', 'railStationary'}:
            assert recipes[identity]['status'] == 'ready', identity
            result['recipeId'] = identity
        if kind == 'trainDepartureBell':
            definition = definitions[identity]
            href = next(x['attributes']['href'] for x in definition['declaration']['children'] if x['tag'] == 'sound')
            path = str(Path(definition['sourcePath']).parent / href)
            asset = next(x for x in catalog['assets'] if x['path'] == path)
            assert asset['mediaType'] == 'wav' and asset['native']['sha256'], 'Bell WAV closure'
            result['media'] = asset['native']
        contributions.append(result)
    # All selectors used by slim/roof/wide/passage/stair rendering, not a sample.
    ids = [f'system:thin-{i}' for i in range(8)] + [f'system:fat-{i}' for i in range(2)] + ['system:yard']
    ids += [f'system:{material}-{part}-{i}' for material in ('concrete', 'brown', 'green')
            for part, count in (('passage', 14), ('stair', 32)) for i in range(count)]
    geometry = []
    for identity in ids:
        recipe = recipes[identity]
        assert recipe['status'] == 'ready' and recipe['frames'], identity
        for frame in recipe['frames']:
            assert frame['status'] == 'ready' and len(frame['masks']) == 8, identity
            assert frame['surfaceSet'] in catalog['surfaceSets'], identity
            assert all(value in catalog['masks'] for value in frame['masks'].values()), identity
        correction = recipe.get('sourceCorrection')
        if identity.endswith('-stair-23'):
            assert correction['issue'] == 'EXAV-GAL-001' and correction['originalRectangle'] == [388, 4, 32, 36]
            assert recipe['frames'][0]['rectangle'] == [288, 4, 32, 36], 'Correction provenance drift'
        geometry.append(dict(id=identity, sourcePath=recipe['sourcePath'], sourceSymbol=recipe['sourceSymbol'],
                             correction=correction, frames=len(recipe['frames'])))
    bindings = {key: [dict(path=p, sha256=sha(p)) for p in paths] for key, paths in BINDINGS.items()}
    ui_keys = {}
    for key, paths in BINDINGS.items():
        keys = sorted({k for p in paths if p.endswith('.svelte') for k in (re.findall(r'\btext\.([A-Za-z][A-Za-z0-9_]*)', (ROOT / p).read_text()) +
                re.findall(r"['\"](stationDiagnosis[A-Za-z]+)['\"]", (ROOT / p).read_text()))})
        assert all(english.get(k) and japanese.get(k) for k in keys), 'Missing bilingual UI wording'
        ui_keys[key] = keys
    source_texts = []
    for row in locale['sourceStrings']:
        binding = text_binding(row['source'])
        technical = row['classification'] == 'technical-or-format-literal'
        source_texts.append(dict(id=row['id'], source=row['source'], sourceSha256=row['sourceSha256'],
            jaSha256=row['jaSha256'], classification=row['classification'], binding=binding,
            donor=row['englishDonor'], disposition='invariant-technical-reference' if technical else 'semantic-browser-adaptation'))
    # Catalog metadata retains original source hashes; published English entries
    # and original Japanese are validated by the existing content/locale importer.
    catalog_texts = [dict(source=x['source'], sourceSha256=x['sourceSha256'], key=x['key'],
                         jaSha256=x['jaSha256'], classification=x['classification'], donor=x['englishDonor'],
                         disposition='source-bound-catalog-metadata; BGM playback deferred') for x in locale['catalogStrings']]
    scenario_operations = {
        '008': ['yard-controller'], '009': ['yard-controller'], '010': ['passenger-fare'],
        '011': ['passenger-fare'], '012': ['passenger-fare'], '013': ['passenger-fare'], '014': ['passenger-fare'],
        '015': ['timers-logs'], '016': ['timers-logs'], '017': ['providers', 'listeners'],
        '018': ['providers', 'listeners'], '019': ['providers'], '020': ['station-building'],
        '030': ['timers-logs'], '031': ['passage-stairs']}
    scenarios = [dict(x, bindings=scenario_operations[x['id'][-3:]],
        credit=({'EXAV-SCN-016': 'Aligned/cancelled canonical timers only; legacy migration remains P10.',
                'EXAV-SCN-020': 'Negative station dependency only: immediate construction; generic ConstructionSite remains P7.',
                'EXAV-SCN-009': 'P5 TURNING and diagram witnesses; no new P9/P10 acceptance.',
                'EXAV-SCN-030': 'Daily/weekly station aggregates only; development consumer remains P7/P9.'}.get(x['id'],
                'station-facing runtime only; cross-phase companions retain their own acceptance')))
        for x in source['scenarioBindings']]
    return dict(schemaVersion=1, slice='P6-27', authority=source['authority'],
        catalogHash=sha('src/FreeTrain.Online.Content/bundled-v2.json'),
        inputs={p: sha(p) for p in ('doc/parity/station-review.json', 'doc/parity/station-localization-review.json',
                'doc/parity/station-fixtures.json', 'web/src/generated/station-profile.json',
                'web/src/locales/en/app.json', 'web/src/locales/ja/app.json',
                'content/localization/exav-stable-bundled/source.json', 'content/localization/exav-stable-bundled/en.json')},
        bindings=bindings, uiKeys=ui_keys, contributions=contributions, geometryRecipes=geometry,
        operations=[dict(id=x['id'], requirements=x['requirements'], controls=x['controls'], binding=x['id']) for x in source['operations']],
        actions=[dict(id=x['sourceAction']['id'], binding=x['operation']) for x in source['actions']],
        decisions=[dict(id=x['id'], bindings=x['operations'], disposition=x['disposition']) for x in source['decisions']],
        dependencies=[dict(id=x['id'], bindings=x['operations'], contract=x['contract']) for x in source['dependencies']],
        acceptance=[dict(id=x['id'], binding=x['operation'], expected=x['expected'],
                         credit='see runtime witnesses and execution report; fixture existence alone grants no gameplay credit') for x in source['acceptanceMatrix']],
        scenarios=scenarios, sourceTexts=source_texts, catalogTexts=catalog_texts, meaningDecisions=locale['meaningDecisions'],
        qualifications=['P7 city producers/land evolution; P8 full reports; P10 historical imports; P11 capacity misses remain open.',
                        'EXAV-GAL-001 portable correction delivered; original-runtime/native/human fidelity remains P2/P6-28 qualification.',
                        'Synthetic source/prepared mounts do not accept real optional suites.',
                        'Bindings identify executable witnesses; fresh runs and historical evidence are separately recorded in the delivery report.'])


def generate():
    return reconcile(read('src/FreeTrain.Online.Content/bundled-v2.json'), read('web/src/generated/station-profile.json'),
                     read('web/src/locales/en/app.json'), read('web/src/locales/ja/app.json'))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    result = generate()
    data = json.dumps(result, ensure_ascii=False, indent=2) + '\n'
    if args.check:
        assert OUTPUT.read_text() == data, 'P6 delivery evidence drift; regenerate and review'
    else:
        OUTPUT.write_text(data)
    print('P6-27: %d declarations, %d geometry recipes, %d actions, %d source texts, %d catalog texts reconciled' %
          tuple(len(result[k]) for k in ('contributions', 'geometryRecipes', 'actions', 'sourceTexts', 'catalogTexts')))
