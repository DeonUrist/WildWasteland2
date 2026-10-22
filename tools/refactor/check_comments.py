"""Checks that every method, constructor, operator, indexer and non-trivial property has a /// summary.

Heuristic line scanner (dense one-line members: put each member on its own line). Auto-properties
({ get; set; }) and fields are exempt. Lambdas and local functions are not members and are ignored.

Usage: python tools/refactor/check_comments.py [--package P | file ...] [--max N]
"""
import re, sys
from _common import cs_files, owned, package_patterns, ROOT

MODS = r'(?:(?:public|private|internal|protected|static|override|virtual|abstract|sealed|extern|unsafe|async|partial|new|readonly|implicit|explicit)\s+)'
TYPE = r'(?:[\w.]+(?:<[^()]*?>)?(?:\[\s*,*\s*\])*\??)'
METHOD = re.compile(r'^\s*' + MODS + r'*' + TYPE + r'\s+(?:operator\s*\S+|[A-Z_a-z]\w*(?:<[^()]*?>)?)\s*\(')
CTOR = re.compile(r'^\s*' + MODS + r'+[A-Z]\w*\s*\(')
PROP = re.compile(r'^\s*' + MODS + r'+' + TYPE + r'\s+(?:this\s*\[[^\]]*\]|[A-Z]\w*)\s*(\{|=>)')
SKIP = re.compile(r'^\s*(return|new|else|case|await|throw|using|goto|yield|var|if|for|foreach|while|switch|lock|catch|base|this|get|set|out|ref|in|is|as|typeof|nameof|default)\b')
TRIVIAL = re.compile(r'\{\s*(?:(?:private|internal|protected)\s+)?get;\s*(?:(?:private|internal|protected)\s+)?(?:set;)?\s*\}')


def missing(path):
    """Returns (line number, text) of member declarations without a preceding /// comment."""
    with open(ROOT + '/' + path, encoding='utf-8', errors='ignore') as f:
        lines = f.read().split('\n')
    out = []
    depth_comment = False
    for i, line in enumerate(lines):
        s = line.strip()
        if s.startswith('/*'):
            depth_comment = '*/' not in s
            continue
        if depth_comment:
            depth_comment = '*/' not in s
            continue
        if not s or s.startswith('//') or s.startswith('[') or s.startswith('#') or SKIP.match(line):
            continue
        if s.endswith(';') and '=>' not in s and '(' in s and not re.search(r'\b(abstract|extern|partial)\b', s) and 'interface' not in s:
            continue  # statement or field initialiser with a call
        is_member = METHOD.match(line) or CTOR.match(line) or (PROP.match(line) and not TRIVIAL.search(line))
        if not is_member or '=' in line.split('(')[0] and '=>' not in line.split('(')[0]:
            continue
        j = i - 1
        while j >= 0 and (lines[j].strip().startswith('[') or lines[j].strip() == ''):
            j -= 1
        if j < 0 or not lines[j].strip().startswith('///'):
            out.append((i + 1, s[:120]))
    return out


def main():
    """Prints members without summaries; exit 1 when any are found."""
    args = sys.argv[1:]
    limit = int(args[args.index('--max') + 1]) if '--max' in args else 0
    if '--package' in args:
        pats = package_patterns(args[args.index('--package') + 1])
        files = [f for f in cs_files('src') + cs_files('developer') if owned(f, pats)]
    else:
        files = [a for a in args if a.endswith('.cs')] or cs_files('src')
    total = 0
    for f in files:
        for ln, text in missing(f):
            total += 1
            if not limit or total <= limit:
                print('%s:%d  %s' % (f, ln, text))
    print('members without /// summary: %d in %d file(s)' % (total, len(files)))
    return 1 if total else 0


if __name__ == '__main__':
    sys.exit(main())
