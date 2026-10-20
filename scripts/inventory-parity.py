#!/usr/bin/env python3
"""Inventory pinned legacy sources without loading assemblies or converting artwork.

Stdlib only. Base generation never reads the optional sibling checkout. All paths
in ledgers are repository-relative; generated metadata contains no asset binaries.
"""
import argparse
import collections
import hashlib
import importlib.util
import json
import posixpath
import re
import subprocess
import sys
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
PARITY_ROOT = ROOT / 'doc/parity'
OUT = PARITY_ROOT / 'historical'
CONFIG = OUT / 'baseline.json'
ACTION_SPEC = importlib.util.spec_from_file_location('parity_actions', Path(__file__).with_name('parity_actions.py'))
action_review = importlib.util.module_from_spec(ACTION_SPEC)
ACTION_SPEC.loader.exec_module(action_review)
XML_LIMIT = 4 * 1024 * 1024
EXPANDED_LIMIT = 32 * 1024 * 1024
ASSETS = {'.bmp', '.png', '.wav', '.mid', '.midi', '.ico', '.cur', '.gif', '.jpg', '.jpeg'}
EXCLUDED = {
    'org.kohsuke.freetrain.music.a4mem': 'External A4 music and user-file equivalent excluded by owner scope.',
    'org.kohsuke.freetrain.tools.dbgwnd': 'Archived debug window/replacement excluded by owner scope.',
    'org.kohsuke.freetrain.tools.vcr': 'Archived video recorder/replacement excluded by owner scope.',
}


def git(repo, *args, data=None):
    return subprocess.run(['git', '-C', str(repo), *args], input=data,
                          stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True).stdout


def digest(data):
    return hashlib.sha256(data).hexdigest()


def dump(value):
    return (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode('utf-8')


def dump_ledger(value):
    # One record per line keeps large generated ledgers diffable without repeating
    # pretty-print indentation across thousands of nested recipe/variant records.
    lines = ['{']
    for index, (key, item) in enumerate(value.items()):
        prefix = '  ' + json.dumps(key) + ': '
        if isinstance(item, list) and item:
            lines.append(prefix + '[')
            lines.extend('    ' + json.dumps(row, ensure_ascii=False, separators=(',', ':')) +
                         (',' if i < len(item) - 1 else '') for i, row in enumerate(item))
            lines.append('  ]' + (',' if index < len(value) - 1 else ''))
        else:
            lines.append(prefix + json.dumps(item, ensure_ascii=False, separators=(',', ':')) +
                         (',' if index < len(value) - 1 else ''))
    return ('\n'.join([*lines, '}']) + '\n').encode('utf-8')


class Tree:
    """Read only the pinned Git objects; dirty working files cannot taint a baseline."""
    def __init__(self, repo, revision, subtree=None):
        self.revision = git(repo, 'rev-parse', revision + '^{commit}').decode().strip()
        target = self.revision + ':' + subtree if subtree else self.revision
        self.entries = {}
        for entry in git(repo, 'ls-tree', '-rz', '--full-tree', target).split(b'\0'):
            if not entry:
                continue
            info, path = entry.split(b'\t', 1)
            mode, kind, oid = info.decode().split()
            if kind == 'blob':
                self.entries[path.decode('utf-8')] = (mode, oid)
        oids = sorted({oid for mode, oid in self.entries.values()})
        data = git(repo, 'cat-file', '--batch', data=('\n'.join(oids) + '\n').encode())
        self.blobs = {}
        offset = 0
        for oid in oids:
            end = data.index(b'\n', offset)
            actual, kind, size = data[offset:end].decode().split()
            if actual != oid or kind != 'blob':
                raise ValueError('Unexpected Git object: ' + oid)
            offset = end + 1
            size = int(size)
            self.blobs[oid] = data[offset:offset + size]
            offset += size + 1

    def read(self, path):
        return self.blobs[self.entries[path][1]]

    def text(self, path):
        data = self.read(path)
        for encoding in ('utf-8-sig', 'cp932'):
            try:
                return data.decode(encoding), encoding
            except UnicodeDecodeError:
                pass
        raise ValueError('Unknown text encoding: ' + path)


def manifest(data, allow_cp932=False):
    """Bounded internal general entities, both quote forms; no external entities."""
    if len(data) > XML_LIMIT:
        raise ValueError('Manifest exceeds 4 MiB')
    encoding = 'utf-8-sig'
    declaration_bytes = re.search(br'<\?xml[^>]*encoding=[\'\"]([^\'\"]+)', data)
    if allow_cp932 and declaration_bytes and declaration_bytes[1].lower() in (b'shift_jis', b'cp932'):
        encoding = 'cp932'
    text = data.decode(encoding)
    declaration = re.search(r'<\?xml[^>]*encoding=[\'"]([^\'"]+)', text)
    if declaration and declaration[1].lower() not in (('utf-8', 'utf8', 'shift_jis', 'cp932') if allow_cp932 else ('utf-8', 'utf8')):
        raise ValueError('Unsupported manifest encoding: ' + declaration[1])
    text = re.sub(r'<!--.*?-->', '', text, flags=re.S)
    if re.search(r'<!DOCTYPE\s+[^\s>]+\s+(?:SYSTEM|PUBLIC)\b', text, re.I):
        raise ValueError('External DTD is unsupported')
    declarations = re.findall(r'<!ENTITY\s+([^>]+)>', text, re.S)
    if len(declarations) > 512:
        raise ValueError('Too many entities')
    entities = {}
    duplicate_entities = []
    for declaration in declarations:
        # XML entity values can contain > (including markup); use the full
        # declarations below instead of the preliminary lexical count.
        if declaration.lstrip().startswith('%') or re.search(r'^\s*[\w.-]+\s+(SYSTEM|PUBLIC)\b', declaration, re.I):
            raise ValueError('External/parameter entities are unsupported')
    for match in re.finditer(r'<!ENTITY\s+([\w.-]+)\s+([\'"])(.*?)\2\s*>', text, re.S):
        name, _, value = match.groups()
        if name in ('amp', 'lt', 'gt', 'quot', 'apos'):
            raise ValueError('Reserved entity: ' + name)
        if name in entities:
            # XML uses the first declaration. Retain a diagnostic, not an override.
            duplicate_entities.append(name)
            continue
        entities[name] = value
    if len(entities) + len(duplicate_entities) != len(declarations):
        raise ValueError('Unsupported entity declaration')
    resolved = {}
    resolved_bytes = 0

    def expand(name, stack=()):
        nonlocal resolved_bytes
        if name in resolved:
            return resolved[name]
        if name in stack or len(stack) >= 16:
            raise ValueError('Cyclic/deep entity: ' + name)
        value = entities[name]
        def replace(match):
            child = match[1]
            return expand(child, (*stack, name)) if child in entities else match[0]
        # Check the size before joining expansions (avoid an amplification allocation).
        parts = re.split(r'(&[\w.-]+;)', value)
        expanded = []
        size = 0
        for part in parts:
            part = re.sub(r'&([\w.-]+);', replace, part)
            size += len(part.encode('utf-8'))
            if size > XML_LIMIT:
                raise ValueError('Entity expansion limit: ' + name)
            expanded.append(part)
        resolved_bytes += size
        if resolved_bytes > EXPANDED_LIMIT:
            raise ValueError('Total entity expansion exceeds 32 MiB')
        resolved[name] = ''.join(expanded)
        return resolved[name]

    for name in entities:
        expand(name)
    text = re.sub(r'<!DOCTYPE[^\[]*\[.*?\]>', '', text, flags=re.S)
    size = len(text.encode('utf-8'))
    for match in re.finditer(r'&([\w.-]+);', text):
        if match[1] in resolved:
            size += len(resolved[match[1]].encode('utf-8')) - len(match[0])
            if size > EXPANDED_LIMIT:
                raise ValueError('Expanded manifest exceeds 32 MiB')
    text = re.sub(r'&([\w.-]+);', lambda m: resolved.get(m[1], m[0]), text)
    root = ET.fromstring(text)
    if root.tag not in ('plug-in', 'plugin'):
        raise ValueError('Expected plug-in/plugin root')
    if sum(1 for _ in root.iter()) > 200000:
        raise ValueError('Manifest node limit')
    return root, dict(declarations=len(declarations), duplicates=duplicate_entities)


def scope(plugin):
    return 'excluded' if plugin in EXCLUDED else 'in-scope'


def common(record_id, path, symbol, plugin=None):
    return dict(coverageId=record_id, baselinePath=path, symbol=symbol,
                dependencies=[], reachability='unreviewed', scope=scope(plugin),
                behavior='Source review pending.', modernDestinations=[], fixtures=[],
                evidence=[], status='inventoried', differences=[])


def resolve_path(tree, source, ref):
    ref = ref.replace('\\', '/')
    if re.match(r'^[a-z]+:', ref, re.I) or ref.startswith('/'):
        return [], 'external'
    target = posixpath.normpath(posixpath.join(posixpath.dirname(source), ref))
    if target in tree.entries:
        return [target], 'exact'
    matches = [p for p in tree.entries if p.casefold() == target.casefold()]
    return matches, 'case-folded' if len(matches) == 1 else 'ambiguous' if matches else 'missing'


def child_recipe(node):
    """GenericStructureContribution.loadSprites registers ID-color:direction.

    Heights and direction-table aliases are variants, not extra registrations.
    """
    colors = [n for n in node if n.tag in ('spriteType', 'colorVariation')]
    sprites = [n for n in node if n.tag in ('picture', 'sprite', 'pictures', 'sprites')]
    return max(1, len(colors)), sprites


def direction_aliases(sprites, south_token='SOUTH'):
    table = [-1] * 8
    default = len(sprites)
    named = {'NORTH': 0, 'EAST': 2, south_token: 4, 'WEST': 6}
    for index, sprite in enumerate(sprites):
        tokens = [token for node in sprite.findall('direction')
                  for token in node.get('front', '').upper().split('|')]
        # parseDirection counts every split token, even unknown/empty names.
        # Only a node with no direction elements can become the fallback.
        if not sprite.findall('direction'):
            default = min(default, index)
        for token in tokens:
            for direction in named.values() if token == 'ALL' else [named[token]] if token in named else []:
                table[direction] = index
    return [default if value == -1 else value for value in table]


def build_pack(tree, pack_id, prefix, current=None, base=None, base_tree=None, south_token='SOUTH', allow_cp932=False):
    # Historical Git inventory, not runtime discovery: the pinned extra revision
    # predates the owner's plugin.XML -> plugin.xml source correction.
    paths = sorted(p for p in tree.entries if p.startswith(prefix) and p.lower().endswith('/plugin.xml'))
    if pack_id == 'base':
        paths = sorted(p for p in tree.entries if p.startswith('plugins/') and p.lower().endswith('/plugin.xml'))
    plugins, contributions, edges, issues = [], [], [], []
    factories = {}
    parsed = {}
    plugin_names = {posixpath.basename(posixpath.dirname(p)) for p in paths}
    all_plugin_names = plugin_names | ({p['pluginId'] for p in base['plugins']} if base else set())
    all_ids = set()
    for path in paths:
        plugin = posixpath.basename(posixpath.dirname(path))
        try:
            root, entity_info = manifest(tree.read(path), allow_cp932=allow_cp932)
        except (ValueError, ET.ParseError) as error:
            raise ValueError(f'{path}: {error}') from error
        parsed[path] = root
        record = common(f'{pack_id}:plugin:{plugin}', path, plugin, plugin)
        record.update(pluginId=plugin, sha256=digest(tree.read(path)),
                      title=root.findtext('title', ''), author=root.findtext('author', ''),
                      homepage=root.findtext('homepage', ''), entities=entity_info,
                      behavior='Manifest contribution registration and declared dependencies.',
                      reachability='manifest-discovered',
                      currentPaths=current.get(tree.entries[path][1], []) if current else [])
        if plugin in EXCLUDED:
            record['differences'].append(EXCLUDED[plugin])
        for name in entity_info['duplicates']:
            issues.append(dict(kind='duplicate-entity-first-declaration-used', owner=record['coverageId'], target=name))
        depends = [n.get('on') for n in root.findall('depend')]
        if plugin != 'system':
            depends.insert(0, 'system')
        record['dependencies'] = sorted(set(depends))
        record['notices'] = sorted(p for p in tree.entries if p.startswith(posixpath.dirname(path) + '/')
                                  and re.search(r'(readme|copying|licen[cs]e|notice)', posixpath.basename(p), re.I))
        record['permissionStatus'] = 'owner-reported-separate-permission; browser-derivative terms unreviewed' if pack_id == 'extra' else 'per-file-notice review pending; core LGPL-2.1'
        for dependency in record['dependencies']:
            if dependency not in all_plugin_names:
                issues.append(dict(kind='missing-plugin', owner=record['coverageId'], target=dependency))
        plugins.append(record)
        for node in root.findall('contribution'):
            if node.get('type') == 'contribution':
                factories.setdefault(node.findtext('name'), []).append(dict(
                    plugin=plugin, factory=node.find('class').attrib if node.find('class') is not None else {},
                    implementation=node.find('implementation').attrib if node.find('implementation') is not None else {}))
    if base:
        for name, definitions in base['factories'].items():
            factories.setdefault(name, []).extend(definitions)
    for path, root in parsed.items():
        plugin = posixpath.basename(posixpath.dirname(path))
        for index, node in enumerate(root.findall('contribution')):
            original_id, kind = node.get('id'), node.get('type')
            if original_id is None or kind is None:
                raise ValueError('Contribution without ID/type: ' + path)
            record = common(f'{pack_id}:contribution:{plugin}:{index}:{original_id}', path, original_id, plugin)
            record.update(pluginId=plugin, contributionId=original_id, contributionType=kind,
                          name=node.findtext('name', ''), author=node.findtext('author', root.findtext('author', '')),
                          declarationIndex=index, reachability='factory-registration' if kind == 'contribution' else 'manifest-registered',
                          behavior='Factory definition' if kind == 'contribution' else f'{kind} contribution; behavior review pending.',
                          implementation=node.find('class').attrib if node.find('class') is not None else {},
                          factoryKey=kind if kind in factories else None, generatedFrom=None,
                          parameters={k: node.findtext(k) for k in ('size', 'height', 'minHeight', 'maxHeight', 'price', 'speed', 'capacity', 'fare') if node.find(k) is not None},
                          recipeNodes=[])
            for element in node.iter():
                if element.tag in ('sprite', 'sprites', 'picture', 'pictures', 'top', 'middle', 'bottom', 'override', 'spriteType', 'colorVariation', 'map', 'composition', 'car', 'direction', 'colorMap', 'config'):
                    record['recipeNodes'].append(dict(tag=element.tag, attributes=dict(element.attrib), text=(element.text or '').strip()))
                for key, value in element.attrib.items():
                    if (key == 'ref' or key.endswith('Ref') or key == 'picture' and element.tag in ('colorMap', 'sprite')) and value:
                        record['dependencies'].append(value)
                    if key in ('src', 'href', 'codebase'):
                        targets, resolution = resolve_path(tree, path, value)
                        target_pack = pack_id
                        if resolution == 'missing' and base_tree:
                            base_targets, base_resolution = resolve_path(base_tree, path.replace('PluginsExtra/', 'plugins/', 1), value)
                            if base_targets and base_resolution in ('exact', 'case-folded'):
                                targets, resolution, target_pack = base_targets, 'base-pack-reference', 'base'
                        edge = dict(owner=record['coverageId'], tag=element.tag, attribute=key,
                                    value=value, targets=targets, targetPack=target_pack, resolution=resolution)
                        edges.append(edge)
                        if resolution in ('missing', 'ambiguous', 'external'):
                            issues.append(dict(kind='resource-' + resolution, **edge))
            record['dependencies'] = sorted(set(record['dependencies']))
            if kind != 'contribution' and kind not in factories:
                issues.append(dict(kind='unknown-factory', owner=record['coverageId'], target=kind))
            contributions.append(record)
            if kind != 'contribution':
                all_ids.add(original_id)
            if kind == 'GenericStructure':
                color_count, sprites = child_recipe(node)
                record['generatedRecipe'] = dict(source='plugins/jp.co.tripod.chiname.lib.generic/GenericStructureContribution.cs',
                    symbol='loadSprites', colors=color_count, directions=len(sprites), idFormat='{parent}-{color}:{direction}',
                    directionAliases=direction_aliases(sprites, south_token), aliasesStatus='source-derived; runtime comparison pending',
                    heightStatus='height variants are not registered child IDs')
                for color in range(color_count):
                    for direction, sprite in enumerate(sprites):
                        child_id = f'{original_id}-{color}:{direction}'
                        child = common(f'{pack_id}:generated:{plugin}:{index}:{child_id}', path, child_id, plugin)
                        child.update(pluginId=plugin, contributionId=child_id, contributionType='generatedStructure',
                                     generatedFrom=record['coverageId'], colorIndex=color, directionIndex=direction,
                                     spriteAttributes=dict(sprite.attrib), dependencies=[original_id],
                                     reachability='source-derived-registration',
                                     behavior='Generic structure primitive registration; direction/color/height fidelity unverified.')
                        contributions.append(child)
                        all_ids.add(child_id)
    id_owners = collections.defaultdict(list)
    for record in contributions:
        if record['contributionType'] != 'contribution':
            id_owners[record['contributionId']].append(record['coverageId'])
    if base:
        for record in base['contributions']:
            if record['contributionType'] != 'contribution':
                id_owners[record['contributionId']].append(record['coverageId'])
    collisions = [dict(contributionId=key, owners=value) for key, value in sorted(id_owners.items()) if len(value) > 1]
    for record in contributions:
        for dependency in record['dependencies']:
            if dependency not in id_owners:
                issues.append(dict(kind='unresolved-contribution-reference', owner=record['coverageId'], target=dependency))
    owned_dirs = {posixpath.dirname(p): posixpath.basename(posixpath.dirname(p)) for p in paths}
    assets = []
    for path in sorted(tree.entries):
        if not (path.startswith(prefix) or pack_id == 'base' and path.startswith('core/res/')):
            continue
        if Path(path).suffix.lower() not in ASSETS:
            continue
        plugin = next((name for folder, name in owned_dirs.items() if path.startswith(folder + '/')), None)
        record = common(f'{pack_id}:asset:{path}', path, path, plugin)
        data = tree.read(path)
        record.update(sha256=digest(data), bytes=len(data), mediaType=Path(path).suffix.lower()[1:],
                      pluginId=plugin, authorSource='manifest/contribution and per-file notices; review pending',
                      decoding='BMP upper-left color key; recipe/effect/override review pending' if path.lower().endswith('.bmp') else 'format-specific review pending',
                      cropAnchorRules='owning contribution recipeNodes and loader source; not inferred from image dimensions',
                      outputIdentity=None, behavior='Source resource; no conversion performed.',
                      reachability='reference review pending')
        assets.append(record)
    counts = collections.Counter(r['contributionType'] for r in contributions)
    return dict(schemaVersion=1, packId=pack_id, revision=tree.revision,
                metadataOnly=True, status='inventory; parity unverified', plugins=plugins,
                factories=dict(sorted(factories.items())), contributions=contributions,
                assets=assets, resourceEdges=edges, collisions=collisions, issues=issues,
                counts=dict(manifests=len(plugins), declared=sum(counts.values()) - counts['generatedStructure'],
                            generated=counts['generatedStructure'], assets=len(assets),
                            byType=dict(sorted(counts.items()))))


def strip_comments(text):
    # Preserve string literals while removing comments; this is a lexical inventory,
    # not a C# parser or a reachability proof.
    pattern = r'@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|//[^\n]*|/\*.*?\*/'
    return re.sub(pattern, lambda m: '' if m[0].startswith(('//', '/*')) else m[0], text, flags=re.S)


def source_inventory(tree, current, all_sources=False):
    records, bindings, serializers, resources = [], [], [], []
    for path in sorted(tree.entries):
        if not path.endswith('.cs') or not all_sources and not path.startswith(('core/', 'plugins/', 'lib/')):
            continue
        text, encoding = tree.text(path)
        code = strip_comments(text)
        record = common('base:source:' + path, path, path)
        plugin = posixpath.basename(posixpath.dirname(path)) if path.startswith('plugins/') else None
        # Locate plugin root even when source is nested in src/.
        if path.startswith('plugins/'):
            plugin = path.split('/')[1]
            record['scope'] = scope(plugin)
        if path.startswith(('lib/DirectDraw', 'lib/DirectAudio')):
            record['scope'] = 'native-dependency-reference'
        symbols = re.findall(r'\b(?:class|struct|interface|enum)\s+(\w+)', code)
        flags = [flag for flag, pattern in {
            'Serializable': r'\[Serializable\b', 'ISerializable': r'\bISerializable\b',
            'IObjectReference': r'\bIObjectReference\b', 'GetObjectData': r'\bGetObjectData\s*\(',
            'OnDeserialization': r'\bOnDeserialization\s*\(', 'NonSerialized': r'\[NonSerialized\b',
        }.items() if re.search(pattern, code)]
        record.update(sha256=digest(tree.read(path)), encoding=encoding, symbols=symbols,
                      currentPaths=current.get(tree.entries[path][1], []), serializationFlags=flags,
                      stubSignals=len(re.findall(r'\bthrow\s+new\s+NotImplementedException\s*\(', code)),
                      behavior='Retained source; symbol, state and reachability review pending.')
        records.append(record)
        if flags:
            serializers.append(dict(coverageId=record['coverageId'], path=path, symbols=symbols,
                                    flags=flags, status='lexical-candidate; nested type/field graph pending'))
        for match in re.finditer(r'(?P<control>(?:this\.)?\w+)\.(?P<event>Click|CheckedChanged|SelectedIndexChanged|ButtonClick|ValueChanged|TextChanged)\s*\+=\s*(?:new\s+[\w.]+\s*\(\s*)?(?:this\.)?(?P<handler>\w+)', code):
            bindings.append(dict(coverageId=f'base:ui:{path}:{match.start()}:{match["control"]}:{match["event"]}:{match["handler"]}',
                                 baselinePath=path, control=match['control'], event=match['event'], handler=match['handler'],
                                 reachability='event-binding-found; caller review pending', scope=scope(plugin),
                                 modernOperations=[], status='inventoried', differences=['Action mapping review pending.']))
        for match in re.finditer(r'new\s+Command\s*\(\s*commands\s*\)(.*?);', code, re.S):
            block = match[1]
            controls = re.findall(r'commandInstances\.Add(?:All)?\s*\(\s*([^)]*)\)', block)
            for control in [v.strip() for args in controls for v in args.split(',') if v.strip()]:
                bindings.append(dict(coverageId=f'base:command:{path}:{control}', baselinePath=path,
                                     control=control, event='command', handler=' '.join(block.split()),
                                     reachability='command-registration-found', scope=scope(plugin),
                                     modernOperations=[], status='inventoried', differences=['Action mapping review pending.']))
        for match in re.finditer(r'(?:loadSystemPicture|loadSystemBitmap|findSystemResource)\(\s*((?:"[^"]+"\s*,?\s*)+)\)', code):
            for name in re.findall(r'"([^"]+)"', match[1]):
                targets = [p for p in tree.entries if p.startswith('core/res/') and posixpath.basename(p).casefold() == name.casefold()]
                resources.append(dict(owner=record['coverageId'], reference=name, targets=targets,
                                      resolution='system-resource' if len(targets) == 1 else 'unresolved'))
    return records, bindings, serializers, resources


def report(base, extra=None):
    lines = ['# P0 generated coverage inventory', '',
             'Generated from pinned Git objects. Counts describe inventory, not implemented features or verified parity.', '',
             f'Base revision: `{base["revision"]}`.', '',
             '| Pack | Manifests | Declared nodes (including factories) | Generated children | Resources | Open diagnostics |',
             '| --- | ---: | ---: | ---: | ---: | ---: |']
    for pack in [base] + ([extra] if extra else []):
        c = pack['counts']
        lines.append(f'| {pack["packId"]} | {c["manifests"]} | {c["declared"]} | {c["generated"]} | {c["assets"]} | {len(pack["issues"])} |')
    lines += ['', '## Scope and evidence', '',
              'All records start inventoried. Unreviewed reachability, recipes, aliases, indirect resources and serialization are explicit gaps.',
              'Excluded A4 music/debug/video manifests remain in the base denominator. No legacy assemblies were executed.',
              'Optional-suite output is metadata only; no non-free images, originals or converted derivatives are copied.', '',
              '## Base manifests', '', '| Plugin | Scope | Declared nodes | Generated children |', '| --- | --- | ---: | ---: |']
    for plugin in base['plugins']:
        owned = [r for r in base['contributions'] if r['pluginId'] == plugin['pluginId']]
        generated = sum(r.get('generatedFrom') is not None for r in owned)
        lines.append(f'| `{plugin["pluginId"]}` | {plugin["scope"]} | {len(owned)-generated} | {generated} |')
    lines += ['', '## Diagnostic counts', '']
    for pack in [base] + ([extra] if extra else []):
        lines += [f'### {pack["packId"]}', '']
        for kind, count in sorted(collections.Counter(i['kind'] for i in pack['issues']).items()):
            lines.append(f'- {kind}: {count}')
        lines.append(f'- Duplicate contribution IDs: {len(pack["collisions"])}')
        lines.append('')
    return ('\n'.join(lines) + '\n').encode()


def write(path, data, check=False):
    if check:
        if not path.exists() or path.read_bytes() != data:
            raise ValueError('Generated inventory drift: ' + str(path.relative_to(ROOT)))
    else:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', choices=('historical', 'exav'), default='historical',
                        help='Separate evidence families; default preserves historical outputs')
    parser.add_argument('--source-root', type=Path, help='Explicit ExAV archive Git repository; required for --source exav')
    parser.add_argument('--check', action='store_true', help='Check maintained inventories without rewriting them')
    parser.add_argument('--extra-root', type=Path, help='Explicit separate repository root (parent of PluginsExtra)')
    parser.add_argument('--report', type=Path, help='Temporary human-readable report path under tmp/')
    parser.add_argument('--action-report', type=Path, help='Temporary P0-03 workflow/difference report under tmp/')
    args = parser.parse_args()
    if args.source == 'exav':
        if not args.source_root or args.extra_root or args.action_report:
            parser.error('--source exav requires --source-root; extra/action review remains separate')
        spec = importlib.util.spec_from_file_location('parity_exav', Path(__file__).with_name('parity_exav.py'))
        exav = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(exav)
        exav.run(sys.modules[__name__], args)
        return
    if args.source_root:
        parser.error('--source-root requires --source exav')
    if args.report and not args.report.resolve().is_relative_to((ROOT / 'tmp').resolve()):
        parser.error('--report must be under this checkout\'s tmp/')
    if args.action_report and not args.action_report.resolve().is_relative_to((ROOT / 'tmp').resolve()):
        parser.error('--action-report must be under this checkout\'s tmp/')
    config = json.loads(CONFIG.read_text())
    tree = Tree(ROOT, config['baseRevision'])
    current = collections.defaultdict(list)
    for entry in git(ROOT, 'ls-tree', '-rz', '--full-tree', config['implementationRevision']).split(b'\0'):
        if entry:
            info, path = entry.split(b'\t', 1)
            current[info.decode().split()[2]].append(path.decode())
    base = build_pack(tree, 'base', 'plugins/', current=current)
    if base['counts']['manifests'] != config['expectedBaseManifests']:
        raise ValueError('Baseline manifest count differs from pinned expectation')
    source, actions, serializers, resources = source_inventory(tree, current)
    base.update(sourceFiles=source, uiBindings=actions, serializationCandidates=serializers,
                sourceResourceEdges=resources,
                roots=[dict(path=root, files=sum(p == root or p.startswith(root + '/') for p in tree.entries),
                            disposition=config['roots'].get(root, 'reference; review pending'))
                       for root in sorted({p.split('/')[0] for p in tree.entries})],
                historicalSaveExamples=[p for p in sorted(tree.entries) if p.lower().endswith(('.ftgd', '.ftgt'))])
    reviews = json.loads((OUT / 'action-map.json').read_text())
    implementation_revision = git(ROOT, 'rev-parse', reviews['implementationRevision'] + '^{commit}').decode().strip()
    if implementation_revision != reviews['implementationRevision']:
        raise ValueError('Action review implementation revision must be a full pinned commit')
    for path, expected_hash in reviews['implementationSources'].items():
        if digest(git(ROOT, 'show', implementation_revision + ':' + path)) != expected_hash:
            raise ValueError('Action review implementation source hash drift: ' + path)
    for action in actions:
        defaults = common(action['coverageId'], action['baselinePath'], action['handler'])
        for key, value in defaults.items():
            action.setdefault(key, value)
    ui_review = action_review.apply_reviews(tree, base, reviews, strip_comments, digest,
                                          (ROOT / 'web/src/lib/game-ui.ts').read_text())
    extra = None
    if args.extra_root:
        extra_tree = Tree(args.extra_root, config['extraRevision'])
        extra = build_pack(extra_tree, 'extra', 'PluginsExtra/', base=base, base_tree=tree)
        if extra['counts']['manifests'] != config['expectedExtraManifests']:
            raise ValueError('Extra-suite manifest count differs from pinned expectation')
        extra.update(permissionRecords=[dict(path=p, sha256=digest(extra_tree.read(p)),
                     status='collection permission/attribution evidence; derivative delivery terms unreviewed')
                     for p in sorted(extra_tree.entries) if re.search(r'(readme|copying|licen[cs]e|notice)', p, re.I)],
                     binaries=[dict(path=p, sha256=digest(extra_tree.read(p)), status='binary-only behavior review pending')
                               for p in sorted(extra_tree.entries) if p.lower().endswith('.dll')])
    write(OUT / 'base-ledger.json', dump_ledger(base), args.check)
    write(OUT / 'action-review.json', dump_ledger(ui_review), args.check)
    if extra:
        write(OUT / 'extra-suite-ledger.json', dump_ledger(extra), args.check)
    if args.report:
        write(args.report, report(base, extra))
    if args.action_report:
        write(args.action_report, action_review.report(ui_review, reviews['workflows']))
    print(f'Base: {base["counts"]["manifests"]} manifests, {base["counts"]["declared"]} declarations, {base["counts"]["generated"]} children, {len(source)} source files, {len(actions)} UI bindings, {len(serializers)} serializer candidates')
    if extra:
        print(f'Extra: {extra["counts"]["manifests"]} manifests, {extra["counts"]["declared"]} declarations, {extra["counts"]["generated"]} children')


if __name__ == '__main__':
    try:
        main()
    except (ValueError, ET.ParseError, subprocess.CalledProcessError) as error:
        sys.exit(str(error))
