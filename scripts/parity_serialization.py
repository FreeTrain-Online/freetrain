"""Lexical, nested-type/field inventory; not a C# compiler or CLR serializer.

Retains declaring-type identity, declaration ordinals, exclusions and custom
serialization evidence for the pinned historical source. Never activates code.
"""
import re


TOKEN = re.compile(r'//[^\n]*|/\*.*?\*/|@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|[A-Za-z_]\w*|\d+|[^\s]', re.S)
MODIFIERS = {'public', 'private', 'internal', 'protected', 'static', 'readonly',
             'const', 'volatile', 'new', 'unsafe', 'event'}


def declarations(text):
    # Region/conditional directives are not field tokens. This inventory keeps
    # declarations from all branches; compile reachability is a separate review.
    text = re.sub(r'^\s*#.*$', '', text, flags=re.M)
    tokens = [m[0] for m in TOKEN.finditer(text) if not m[0].startswith(('//', '/*'))]
    pairs, stack = {}, []
    for i, t in enumerate(tokens):
        if t in ('{', '(', '['):
            stack.append((t, i))
        elif t in ('}', ')', ']'):
            if not stack or stack[-1][0] != {'}': '{', ')': '(', ']': '['}[t]:
                raise ValueError('Unbalanced source delimiters')
            _, start = stack.pop()
            pairs[start] = i
    if stack:
        raise ValueError('Unbalanced source delimiters')
    namespace = re.search(r'\bnamespace\s+([\w.]+)', ' '.join(tokens).replace(' . ', '.'))
    namespace = namespace[1] if namespace else ''
    types = []
    for i, kind in enumerate(tokens):
        if kind not in ('class', 'struct', 'enum', 'interface') or i + 1 >= len(tokens):
            continue
        # Tokens in string literals are atomic. Ignore generic constraints.
        if not re.fullmatch(r'[A-Za-z_]\w*', tokens[i + 1]):
            continue
        start = i
        while start and tokens[start - 1] not in ('{', '}', ';'):
            start -= 1
        end = i + 2
        while end < len(tokens) and tokens[end] not in ('{', ';'):
            end += 1
        if end == len(tokens) or tokens[end] != '{':
            continue
        parent = next((t for t in reversed(types) if t['_open'] < i < t['_close']), None)
        name = (parent['name'] + '+' if parent else namespace + '.') + tokens[i + 1]
        header = tokens[start:end]
        attrs = header[:header.index(kind)]
        base_tokens = tokens[i + 2:end]
        bases = []
        if ':' in base_tokens:
            bases = ''.join(base_tokens[base_tokens.index(':') + 1:]).split(',')
        types.append(dict(name=name, kind=kind, serializable='Serializable' in attrs or kind == 'enum',
                          bases=bases, fields=[], customKeys=[], substitutions=[], callbacks=[],
                          _open=end, _close=pairs[end]))
    for t in types:
        if t['kind'] in ('enum', 'interface'):
            continue
        i, ordinal = t['_open'] + 1, 0
        while i < t['_close']:
            start = i
            has_initializer = False
            while i < t['_close']:
                token = tokens[i]
                if token == '=' and tokens[i - 1] not in ('!', '<', '>', '=') and tokens[i + 1] != '=':
                    has_initializer = True
                if token in ('[', '('):
                    i = pairs[i] + 1
                    continue
                if token == '{':
                    if has_initializer:
                        i = pairs[i] + 1
                        continue
                    prefix = tokens[start:i]
                    for callback in ('GetObjectData', 'GetRealObject', 'OnDeserialization'):
                        if callback in prefix and '(' in prefix:
                            t['callbacks'].append(callback)
                    if 'GetObjectData' in prefix and '(' in prefix:
                        body = ' '.join(tokens[i + 1:pairs[i]])
                        t['customKeys'] = re.findall(r'AddValue\s*\(\s*"([^"]+)"', body)
                        t['substitutions'] = re.findall(r'SetType\s*\(\s*typeof\s*\(\s*([\w.]+)', body)
                    i = pairs[i] + 1
                    break
                if token == ';':
                    field = tokens[start:i]
                    i += 1
                    # Attributes belong to the member, not its field type.
                    attrs = []
                    while field and field[0] == '[':
                        closing = field.index(']')
                        attrs.extend(field[:closing + 1])
                        field = field[closing + 1:]
                    mods = []
                    while field and field[0] in MODIFIERS:
                        mods.append(field.pop(0))
                    if not field:
                        break
                    # Methods/delegates with no body are not fields.
                    before = field[:field.index('=')] if '=' in field else field
                    if '(' in before or 'delegate' in before or any(k in before for k in ('class', 'struct', 'enum', 'interface')):
                        break
                    # First identifier followed by =, comma or end is a name;
                    # array rank commas and generic arguments are skipped.
                    depth = 0
                    first = None
                    for j, v in enumerate(field):
                        if v in ('[', '<'):
                            depth += 1
                        elif v in (']', '>'):
                            depth -= 1
                        elif depth == 0 and re.fullmatch(r'[A-Za-z_]\w*', v) and (j + 1 == len(field) or field[j + 1] in ('=', ',')):
                            first = j
                            break
                    if first is None or first == 0:
                        raise ValueError('Unreviewed field syntax: ' + ' '.join(field))
                    field_type = ''.join(field[:first])
                    names = [field[first]]
                    j = first + 1
                    while j < len(field):
                        v = field[j]
                        if v in ('(', '[', '{'):
                            # Count locally; initializer commas are not fields.
                            close = {'(': ')', '[': ']', '{': '}'}[v]
                            nesting = 1
                            j += 1
                            while j < len(field) and nesting:
                                nesting += (field[j] == v) - (field[j] == close)
                                j += 1
                            continue
                        if v == ',' and j + 1 < len(field):
                            names.append(field[j + 1])
                        j += 1
                    exclusion = 'static-or-constant' if {'static', 'const'} & set(mods) else 'nonserialized' if 'NonSerialized' in attrs else None
                    for name in names:
                        t['fields'].append(dict(ordinal=ordinal, name=name, declaredType=field_type,
                                                event='event' in mods, exclusion=exclusion))
                        ordinal += 1
                    break
                i += 1
    return [{k: v for k, v in t.items() if not k.startswith('_')} for t in types]


def destination(path):
    if '/population/' in path:
        return 'population', 'P6/P7', 'population definitions by contribution; demand/state by world identity'
    if '/accounting/' in path or '/subsidiaries/' in path or any(s in path for s in ('.financial.', '.soccerStadium/')):
        return 'economy', 'P8', 'typed accounts/history/debt/deposit/stock/stadium state and semantic events'
    if '/rail/' in path or any(s in path for s in ('.tc.', '.rail.')) or path.endswith(('Car.cs', 'CarState.cs')):
        return 'rail-train-station', 'P4/P5/P6', 'rail entities, per-car tagged state, groups/controllers/platforms/stations and stable references'
    if path.endswith(('Clock.cs', 'Time.cs', 'TimeLength.cs', 'PriorityQueue.cs')):
        return 'time-scheduler', 'P1', 'integer source minutes and ordered semantic actions; never delegate activation'
    if '/graphics/' in path:
        return 'presentation', 'P2', 'catalog recipe/picture identity and authored crop/anchor/effects; native handles rebuilt'
    if '/contributions/' in path or '/framework/plugin/' in path:
        return 'content-reference', 'P2', 'pack-qualified contribution identity; custom serializer takes precedence over fields'
    if '/controllers/' in path or path.endswith(('WindowState.cs', 'MultiSelectorController.cs', 'ChartControl.cs')):
        return 'local-tool-ui', 'P9', 'configuration/reachability review; no native controls or transient tool state in portable saves'
    if '/world/' in path or path.endswith('HVStructure.cs'):
        return 'world-entities', 'P3/P7', 'terrain/ownership/structures/development/roads with stable entity references'
    return 'support-or-catalog', 'P1/P2/P9', 'explicit value/container/catalog mapping; unsupported reachable state blocks import'


def inventory(tree, ledger, digest):
    candidate_paths = {r['path'] for r in ledger['serializationCandidates']}
    records, reviews = [], []
    for path in sorted(candidate_paths):
        text, encoding = tree.text(path)
        group, phase, target = destination(path)
        types = declarations(text)
        for t in types:
            t.update(path=path, sourceTypeId=path + ':' + t['name'], group=group, phase=phase, target=target,
                     importStatus='mapping-required' if t['serializable'] else 'not-directly-serializable',
                     evidence='lexical declaration; custom serialization/inheritance and reachability require the explicit source rules')
            records.append(t)
        reviews.append(dict(path=path, sha256=digest(tree.read(path)), encoding=encoding,
                            types=[t['name'] for t in types], group=group, phase=phase, target=target))
    by_name = {}
    for t in records:
        by_name.setdefault(t['name'], []).append(t)
    def resolve(t, base, seen=None):
        seen = seen or set()
        key = (t['sourceTypeId'], base)
        if key in seen:
            return None
        seen = seen | {key}
        namespace = t['name'].split('+')[0].rsplit('.', 1)[0]
        names = [base, namespace + '.' + base, namespace + '.' + base.replace('.', '+')]
        outer = namespace
        while '.' in outer:
            outer = outer.rsplit('.', 1)[0]
            names.append(outer + '.' + base.replace('.', '+'))
        parent = t['name']
        while '+' in parent:
            parent = parent.rsplit('+', 1)[0]
            names.append(parent + '+' + base.replace('.', '+'))
        for using in re.findall(r'\busing\s+([\w.]+)\s*;', tree.text(t['path'])[0]):
            names.append(using + '.' + base.replace('.', '+'))
        for name in names:
            choices = by_name.get(name, [])
            local = [p for p in choices if p['path'] == t['path']]
            if not local and t['path'].startswith('plugins/'):
                local = [p for p in choices if p['path'].split('/')[:2] == t['path'].split('/')[:2]]
            choices = local or choices
            if len(choices) == 1:
                return choices[0]
        # C# nested types can inherit a protected nested type from an enclosing
        # type's base (Structure.StructureVoxel, PThreeDimStructure.VoxelImpl).
        enclosing = t['name']
        while '+' in enclosing:
            enclosing = enclosing.rsplit('+', 1)[0]
            for parent in by_name.get(enclosing, []):
                pending = [parent]
                visited = set()
                while pending:
                    p = pending.pop()
                    if p['sourceTypeId'] in visited:
                        continue
                    visited.add(p['sourceTypeId'])
                    for b in p['bases']:
                        ancestor = resolve(p, b, seen)
                        if ancestor:
                            choices = by_name.get(ancestor['name'] + '+' + base, [])
                            if len(choices) == 1:
                                return choices[0]
                            pending.append(ancestor)
        return None
    def lineage(t, seen):
        if t['name'] in seen:
            raise ValueError('Cyclic source inheritance')
        parents = [p for b in t['bases'] if (p := resolve(t, b))]
        return [t] + [a for p in parents for a in lineage(p, seen | {t['name']})]
    for t in records:
        chain = lineage(t, set())
        t['sourceAncestors'] = [p['name'] for p in chain[1:]]
        t['externalOrUnresolvedBases'] = sorted({b for p in chain for b in p['bases'] if not resolve(p, b)})
        custom = next((p for p in chain if 'ISerializable' in p['bases']), None)
        # An override without another ISerializable declaration still wins.
        writer = next((p for p in chain if 'GetObjectData' in p['callbacks']), None)
        t['serializationMode'] = 'not-directly-serializable' if not t['serializable'] else 'custom' if custom else 'enum-value' if t['kind'] == 'enum' else 'default-fields'
        t['customWriter'] = writer['name'] if custom and writer else None
        t['fieldSlots'] = [dict(declaringType=p['name'], declaringTypeId=p['sourceTypeId'], **f) for p in chain for f in p['fields']]
        t['scope'] = 'excluded-external-a4' if '/org.kohsuke.freetrain.music.a4mem/' in t['path'] else 'baseline-source'
    return dict(schemaVersion=1, revision=tree.revision, status='source-inventory-not-an-importer',
                sourceReviews=reviews, types=records,
                counts=dict(candidateFiles=len(reviews), declaredTypes=len(records),
                            serializableDeclarations=sum(t['serializable'] for t in records),
                            fields=sum(len(t['fields']) for t in records)))
