"""Fails when a `partial void` method is declared but never implemented.

C# silently removes calls to unimplemented partial methods, so a lost implementation file would drop behaviour
without a compile error. Run after every merge and in every Wave B gate.

Usage: python3 -B tools/refactor/check_partials.py
"""
import collections, glob, re, sys
from _common import ROOT


def main():
    """Pairs partial method declarations with implementations per containing partial type; exit 1 on any gap."""
    decl, impl = collections.defaultdict(list), collections.defaultdict(list)
    for f in glob.glob(ROOT + '/src/**/*.cs', recursive=True):
        t = open(f, encoding='utf-8', errors='ignore').read()
        for m in re.finditer(r'partial\s+void\s+(\w+)\s*\(([^)]*)\)\s*(;|\{)', t):
            cls = re.findall(r'partial\s+(?:class|struct)\s+(\w+)', t[:m.start()])
            key = ((cls[-1] if cls else '?'), m.group(1))
            (decl if m.group(3) == ';' else impl)[key].append(f.replace(ROOT + '/', ''))
    missing = [k for k in decl if k not in impl]
    for k in missing:
        print('NOT IMPLEMENTED: %s.%s declared in %s' % (k[0], k[1], ', '.join(decl[k])))
    print('partial methods: %d declared, %d without implementation' % (len(decl), len(missing)))
    return 1 if missing else 0


if __name__ == '__main__':
    sys.exit(main())
