#!/usr/bin/env python3
"""Read-only source witness/matrix check; never imports gameplay or writes expectations."""
import argparse
import hashlib
import itertools
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
PIN = '883cf7d81cf49db25ff38cd3a25d0f140afe31a0'


def check(source_root=None):
    data = json.loads((ROOT / 'doc/parity/train-placement-fixtures.json').read_text())
    reviews = [json.loads((ROOT / 'doc/parity' / name).read_text())
               for name in ('railway-review.json', 'train-review.json')]
    pinned = {r['path']: r['sha256'] for review in reviews for r in review['sourceReviews']}
    assert len(data['sources']) == 7
    for source in data['sources']:
        assert pinned[source['path']] == source['sha256']
        if source_root:
            raw = subprocess.check_output(['git', '-C', str(source_root), 'show', PIN + ':trunk/' + source['path']])
            assert hashlib.sha256(raw).hexdigest() == source['sha256']
    axes = {10: (0, 0, -1), 1: (1, 1, -1), 4: (2, 1, 0), 7: (3, 1, 1)}
    required = set(itertools.product(axes, (False, True), (False, True), (1, 3, 9)))
    assert len(data['cases']) == len(required)
    for row in data['cases']:
        i, e = row['input'], row['expected']
        pattern, initial, reverse, length = i['pattern'], i['initialReversed'], i['reverse'], i['length']
        required.remove((pattern, initial, reverse, length))
        d, dx, dy = axes[pattern]
        expected = []
        for n in range(length):
            expected.append({'originalIndex': length - 1 - n if initial else n,
                             'location': [i['tail'][0] + (length - 1 - n) * dx,
                                          i['tail'][1] + (length - 1 - n) * dy, i['tail'][2]],
                             'heading': d + (4 if reverse else 0)})
        if reverse:
            expected.reverse()
        assert e == {'isReversed': initial != reverse, 'cars': expected}, row['id']
    assert not required
    print('P5-07: 48 independent placement poses, eight headings, both orientations, seven pinned sources pass.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path)
    check(parser.parse_args().source_root)
