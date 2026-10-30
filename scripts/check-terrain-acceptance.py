#!/usr/bin/env python3
"""Verify the integrated P3 WORLDGEN matrix with independent packed-state oracles."""
import argparse
import contextlib
import hashlib
import importlib.util
import io
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

def module(name):
    spec = importlib.util.spec_from_file_location(name, ROOT / f'scripts/check-{name}-topology.py')
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result

def check(directory):
    manifest = json.loads((ROOT / 'doc/parity/terrain-acceptance.json').read_text())
    for source in manifest['sourceEvidence']:
        assert (ROOT / 'doc/parity' / source).is_file(), source
    report = json.loads((directory / 'report.json').read_text())
    expected = {(case['id'], size['h'], size['v'], water)
                for case in manifest['scenarios'] for size in manifest['sizes'] for water in manifest['waterLevels']}
    assert len(report) == len(expected)
    actual = {(row['id'], row['h'], row['v'], row['water']) for row in report}
    assert actual == expected
    water, coast, forest = [module(name) for name in ('water', 'coast', 'forest')]
    rows = []
    for row in report:
        stem = f"{row['id']}-{row['h']}x{row['v']}-water{row['water']}"
        options = row['request']['Options']
        for phase in ('before', 'edited'):
            path = directory / f'{stem}.{phase}.json'
            assert hashlib.sha256(path.read_bytes()).hexdigest() == row[phase + 'Sha256']
            saved = json.loads(path.read_bytes())
            assert json.loads(saved['packedTerrain']['provenance']['requestJson']) == row['request']
            assert saved['packedTerrain']['geometryRevision'] == ('0' if phase == 'before' else '2')
            result = {'id': row['id'], 'phase': phase, 'water': water.check(path)}
            for key in ('SeaCells', 'LandCells', 'SeaComponents', 'LandComponents', 'SeaSides', 'LakeCells', 'LakeComponents'):
                assert result['water'][key[0].lower() + key[1:]] == row['geography'][key], (stem, key)
            if options['Coast']['Kind'] and not options['Lakes']['Count'] and not options['Rivers']['Direction']:
                result['coast'] = coast.check(path)
            # Forest generation's oracle requires natural surfaces; the edit
            # intentionally leaves a cut after terrace removal. Its saved-state
            # topology and full graph restoration are checked separately.
            if phase == 'before' and options['Forests']['CoveragePercent']:
                with contextlib.redirect_stdout(io.StringIO()):
                    result['forest'] = forest.check(path)
                for key in ('ForestCells', 'Trees', 'ReservedCells', 'LargestFlatArea'):
                    assert result['forest'][key[0].lower() + key[1:]] == row['forests'][key], (stem, key)
            rows.append(result)
    (directory / 'topology.json').write_text(json.dumps(rows, indent=2) + '\n')
    print(f'{len(report)} WORLDGEN requests / {len(rows)} before-and-edited packed-state topology oracles passed')

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    check(parser.parse_args().directory)
