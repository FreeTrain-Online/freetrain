"""Bounded inert manifest XML parser, shared semantics with the P0 source scanner.

Only internal general entities are expanded; no external DTD, entity resolver or
code activation. The optional-set layer expands explicitly declared local includes
before calling this parser.
"""
import re
import xml.etree.ElementTree as ET
XML_LIMIT = 4 * 1024 * 1024
EXPANDED_LIMIT = 32 * 1024 * 1024

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

