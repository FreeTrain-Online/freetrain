#!/usr/bin/env python3
"""Check P5-03 stock accounting against pinned P0/P5 evidence and executable output."""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TYPES = {'train', 'trainCar', 'colorMapTrainPicture'}


def read(path):
    return json.loads((ROOT/path).read_bytes())


def check(runtime=None):
    report = read('doc/parity/train-stock-registration.json')
    review = read('doc/parity/train-review.json')
    catalog_bytes = (ROOT/'src/FreeTrain.Online.Content/bundled-v2.json').read_bytes()
    catalog = json.loads(catalog_bytes)
    assert report['schemaVersion'] == 1 and report['slice'] == 'P5-03'
    assert report['status'] == 'registration-complete-no-fleet-gameplay'
    assert report['authority'] == review['authority']
    assert report['catalogHash'] == hashlib.sha256(catalog_bytes).hexdigest()
    ledger = {r['baselinePath']: r['sha256'] for r in read('doc/parity/exav-ledger.json')['fileInventory']}
    expected = {r['coverageId']: r for r in review['contributions'] if r['type'] in TYPES}
    actual = {r['coverageId']: r for r in report['definitions']}
    prepared = {r['coverageId']: r for r in catalog['contributions'] if r['type'] in TYPES}
    assert len(actual) == len(report['definitions']) == 21 and actual.keys() == expected.keys() == prepared.keys()
    for identity, row in actual.items():
        d = prepared[identity]
        assert all(row[k] == d[k] for k in ('id', 'type', 'pluginId', 'sourcePath'))
        assert row['sourceSha256'] == ledger[row['sourcePath']] == expected[identity]['sourceSha256']
        assert row['implementation'] == d['implementation']['name']
        assert row['recipeId'] == row['id'] and row['status'] == 'registered'
    ids = {r['id'] for r in actual.values()}
    aliases = [s for s in catalog['selections'] if s.get('registeredAlias') and s['target'] in ids]
    assert aliases == report['reverseSelections'] and len(aliases) == 2
    assert report['generatedStockDefinitions'] == [d['id'] for d in prepared.values() if d.get('generatedFrom')]
    assert report['limits'] == dict(formationCars=100, quantity=100, requestCars=1024, fleetTrains=128, fleetCars=1024)
    assert report['sourceUi']['sha256'] == ledger['trunk/'+report['sourceUi']['path']]
    admission = report['admission']
    assert admission['scope'].endswith('moving-fleet-capacity-remains-P5-28') and admission['passed']
    assert admission['catalogHash'] == report['catalogHash'] and admission['coldCatalogAndRegistrationMs'] < 1000
    assert admission['registration']['p95Ms'] < 100 and admission['registration']['allocatedBytesPerOperation'] < 2*1024*1024
    for name in ('request100Trains1000Cars', 'maximumRequest64Trains1024Cars', 'detachedFleet128Trains1024Cars'):
        assert admission[name]['p95Ms'] < 10 and admission[name]['allocatedBytesPerOperation'] < 256*1024
    if runtime:
        result = json.loads(Path(runtime).read_bytes())
        assert result['catalogHash'] == report['catalogHash'] and result['diagnostics'] == []
        assert (result['trains'], result['cars'], result['templates']) == (6, 8, 7)
        registered = ({r['binding']['contributionId'] for r in result['bindings']} |
                      {r['reference']['contributionId'] for r in result['carBindings'] + result['templateBindings']})
        assert registered == ids
        assert result['limits'] == report['limits'] and result['admissionBudgetPassed']
        assert report['safeLengths'] == [dict(id=r['binding']['contributionId'], minimum=r['permittedLengths'][0],
                                             maximum=r['permittedLengths'][-1]) for r in result['bindings']]
    print('P5-03: 21 stock registrations, 2 reverse selections, exact pack/source bindings and bounded admission evidence pass.')
    print('Registration scope only; fleet gameplay and moving-fleet capacity remain later P5 steps.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--runtime-report', type=Path)
    check(parser.parse_args().runtime_report)
