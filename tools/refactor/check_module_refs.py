"""Reports references from one module to types declared only in another module (and from Core to any module).

A type is attributed to the module folder that declares it (src/Modules/<M>/ or src/Core). Names declared in
several modules are ambiguous and skipped (partial classes split across folders during the refactor, duplicate
utility names). Also reports reflection strings "WildWasteland.Modules.<X>..." naming another module.

Heuristic (regex over comment/string-stripped code). A false positive can be silenced with a trailing
comment "// refs-ok: <reason>" on that line; every such comment is reviewed in Wave C.

Usage: python tools/refactor/check_module_refs.py [--package P] [--baseline] [--verbose]
"""
import collections, re, sys
from _common import cs_files, module_of, owned, package_patterns, ROOT, strip_code

DECL = re.compile(r'\b(?:class|struct|interface|enum|delegate\s+[\w<>\[\],\s]+?)\s+([A-Z]\w*)')
WORD = re.compile(r'\b([A-Z]\w*)\b')
MEMBER = re.compile(r'(\w+|[>\]?])\s+([A-Z]\w*)\s*(?:=(?!=)|;|=>|\{\s*get|\()')
INITIALIZER = re.compile(r'[{,]\s*([A-Z]\w*)\s*=(?!=)')
KEYWORDS = {'new', 'return', 'is', 'as', 'case', 'throw', 'else', 'in', 'out', 'ref', 'await', 'typeof', 'using', 'static', 'nameof', 'default', 'sizeof', 'yield'}
QUALIFIED = re.compile(r'(?<![\w".])(?:WildWasteland\.)?Modules\.(\w+)\.\w')
SIBLING = re.compile(r'\b([A-Z]\w*)\.[A-Z]\w*')
REFLECT = re.compile(r'WildWasteland\.Modules\.(\w+)')
ALLOW_CORE = {'src/Core/Boot/ModuleRegistry.cs'}


def top_level_types(code):
    """Names of types declared directly in a namespace (or file scope); nested types are module-internal noise."""
    names, depth, ns_depths = [], 0, []
    for m in re.finditer(r'[{}]|\bnamespace\s+[\w.]+\s*\{|' + DECL.pattern, code):
        tok = m.group(0)
        if tok.startswith('namespace'):
            ns_depths.append(depth); depth += 1
        elif tok == '{':
            depth += 1
        elif tok == '}':
            depth -= 1
            if ns_depths and ns_depths[-1] == depth:
                ns_depths.pop()
        elif depth == len(ns_depths):
            names.append(m.group(1))
    return names


def is_reference(line, start, end):
    """True when the word at [start,end) is used as a type: not a member access, call or member declaration name."""
    before = line[:start].rstrip()
    after = line[end:].lstrip()
    if before.endswith('.'):
        return False
    if after.startswith('(') and not before.endswith('new'):
        return False
    if re.search(r'[\w>\]]$', before) and re.match(r'[;=,){]', after or ';') and not re.search(r'\b(new|is|as|typeof|return|case)$', before):
        return False
    return True


def read(path):
    """Reads a source file as text."""
    with open(ROOT + '/' + path, encoding='utf-8', errors='ignore') as f:
        return f.read()


def main():
    """Builds the type index, scans files and prints violations; exit 1 when the selected files have any."""
    args = sys.argv[1:]
    pkg = args[args.index('--package') + 1] if '--package' in args else None
    pats = package_patterns(pkg) if pkg else None
    files = cs_files('src')
    texts = {f: read(f) for f in files}
    code = {f: strip_code(t) for f, t in texts.items()}
    owners = collections.defaultdict(set)
    for f, c in code.items():
        m = module_of(f)
        for name in top_level_types(c):
            owners[name].add(m)
    unique = {n: next(iter(ms)) for n, ms in owners.items() if len(ms) == 1}
    module_names = {module_of(f) for f in files} - {'Core', None}
    declared_any = collections.defaultdict(set)
    for f, c in code.items():
        for name in DECL.findall(c):
            declared_any[module_of(f)].add(name)
        for prev, name in MEMBER.findall(c):
            if prev not in KEYWORDS:
                declared_any[module_of(f)].add(name)
        for name in INITIALIZER.findall(c):
            declared_any[module_of(f)].add(name)
    pairs = collections.Counter()
    hits = []
    for f, c in code.items():
        if pats and not owned(f, pats):
            continue
        me = module_of(f)
        if me is None:
            continue
        seen = set()
        raw_lines = texts[f].split('\n')
        for ln, line in enumerate(c.split('\n'), 1):
            if 'refs-ok' in raw_lines[ln - 1]:
                continue
            for wm in WORD.finditer(line):
                name = wm.group(1)
                other = unique.get(name)
                if other is not None and (len(name) <= 2 or not is_reference(line, wm.start(1), wm.end(1))):
                    continue
                if other is None or other == me or other == 'Core' or name in declared_any[me]:
                    continue
                if me == 'Core' and f in ALLOW_CORE:
                    continue
                key = (name, ln)
                if key in seen:
                    continue
                seen.add(key)
                pairs[(me, other)] += 1
                hits.append('%s:%d %s -> %s.%s' % (f, ln, me, other, name))
        for ln, line in enumerate(c.split('\n'), 1):
            if 'refs-ok' in raw_lines[ln - 1] or line.lstrip().startswith('namespace '):
                continue
            found = {q for q in QUALIFIED.findall(line) if q in module_names}
            for mm in SIBLING.finditer(line):
                if mm.group(1) in module_names and not line[:mm.start()].rstrip().endswith('.'):
                    found.add(mm.group(1))
            for other in found:
                if other != me and not (me == 'Core' and f in ALLOW_CORE):
                    pairs[(me, other + '(qualified)')] += 1
                    hits.append('%s:%d %s -> %s (qualified name or using alias)' % (f, ln, me, other))
        for ln, line in enumerate(strip_code(texts[f], keep_strings=True).split('\n'), 1):
            for other in REFLECT.findall(line):
                if other != me and not (me == 'Core' and f in ALLOW_CORE) and 'namespace ' not in line and '"' in line:
                    pairs[(me, other + '(reflection)')] += 1
                    hits.append('%s:%d %s -> %s (reflection string)' % (f, ln, me, other))
    if '--baseline' in args or ('--verbose' not in args and not pkg):
        for (a, b), n in sorted(pairs.items(), key=lambda kv: -kv[1]):
            print('%-16s -> %-28s %d' % (a, b, n))
    if '--verbose' in args or pkg:
        for h in hits:
            print(h)
    print('total violations: %d' % len(hits))
    return 1 if (pkg and hits) else 0


if __name__ == '__main__':
    sys.exit(main())
