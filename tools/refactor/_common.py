"""Shared helpers for the refactor checks: repo root, ownership globs, file listing."""
import fnmatch, json, os, re, subprocess

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))


def rel(path):
    """Repository-relative path with forward slashes."""
    return os.path.relpath(path, ROOT).replace('\\', '/')


def load_json(name):
    """Loads docs/refactor/<name>."""
    with open(os.path.join(ROOT, 'docs', 'refactor', name), encoding='utf-8') as f:
        return json.load(f)


def glob_match(path, pattern):
    """fnmatch with '**' spanning folders and '*' staying inside one folder."""
    rx = ''
    i = 0
    while i < len(pattern):
        c = pattern[i]
        if pattern.startswith('**/', i):
            rx += '(?:.*/)?'; i += 3; continue
        if pattern.startswith('**', i):
            rx += '.*'; i += 2; continue
        if c == '*':
            rx += '[^/]*'
        elif c == '?':
            rx += '[^/]'
        else:
            rx += re.escape(c)
        i += 1
    return re.fullmatch(rx, path) is not None


def owned(path, patterns):
    """True when path matches a positive pattern and no '!' exclusion."""
    ok = False
    for p in patterns:
        if p.startswith('!'):
            if glob_match(path, p[1:]):
                return False
        elif glob_match(path, p):
            ok = True
    return ok


def package_patterns(package):
    """Ownership globs of a package plus the common report/request files."""
    data = load_json('ownership.json')
    pats = list(data['packages'][package])
    pats += [c.replace('{package}', package) for c in data.get('common', [])]
    return pats


def cs_files(base='src'):
    """All .cs files under base (repo-relative)."""
    out = []
    for d, _, files in os.walk(os.path.join(ROOT, base)):
        for f in files:
            if f.endswith('.cs'):
                out.append(rel(os.path.join(d, f)))
    return sorted(out)


def changed_files(base_ref):
    """Files changed on this branch since base_ref, plus untracked files."""
    a = subprocess.run(['git', '-C', ROOT, 'diff', '--name-only', base_ref + '...HEAD'], capture_output=True, text=True).stdout.split()
    b = subprocess.run(['git', '-C', ROOT, 'diff', '--name-only'], capture_output=True, text=True).stdout.split()
    c = subprocess.run(['git', '-C', ROOT, 'ls-files', '--others', '--exclude-standard'], capture_output=True, text=True).stdout.split()
    return sorted(set(a) | set(b) | set(c))


def module_of(path):
    """Module folder a source file belongs to: 'Core' or the folder under src/Modules."""
    m = re.match(r'src/Modules/([^/]+)/', path)
    if m:
        return m.group(1)
    if path.startswith('src/Core/'):
        return 'Core'
    return None


def strip_code(text, keep_strings=False):
    """Removes comments (and string literals unless keep_strings) so regex scans see code only."""
    out, i, n = [], 0, len(text)
    while i < n:
        if text.startswith('//', i):
            j = text.find('\n', i); j = n if j < 0 else j
            out.append(' ' * (j - i)); i = j
        elif text.startswith('/*', i):
            j = text.find('*/', i + 2); j = n if j < 0 else j + 2
            out.append(re.sub(r'[^\n]', ' ', text[i:j])); i = j
        elif text[i] == '"' or text.startswith('@"', i) or text.startswith('$"', i):
            verbatim = text.startswith('@"', i)
            start = i
            i += 2 if text[i] in '@$' else 1
            while i < n:
                if verbatim and text.startswith('""', i):
                    i += 2; continue
                if not verbatim and text[i] == '\\':
                    i += 2; continue
                if text[i] == '"':
                    i += 1; break
                i += 1
            seg = text[start:i]
            out.append(seg if keep_strings else '"' + re.sub(r'[^\n]', ' ', seg[1:-1]) + '"' if len(seg) >= 2 else seg)
        elif text[i] == "'" and i + 2 < n:
            j = text.find("'", i + 1 if text[i + 1] != '\\' else i + 3)
            j = i + 1 if j < 0 else j + 1
            out.append(' ' * (j - i)); i = j
        else:
            out.append(text[i]); i += 1
    return ''.join(out)
