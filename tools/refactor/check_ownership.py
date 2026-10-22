"""Fails when the current branch changed files outside the package's ownership globs.

Usage: python tools/refactor/check_ownership.py <package> [base-ref=refactor/main]
"""
import sys
from _common import changed_files, owned, package_patterns, load_json, ROOT


def asset_sources(pats):
    """Source paths of Wave A asset-manifest rows whose target path this package owns (D13)."""
    import glob, os
    out = set()
    for tsv in glob.glob(os.path.join(ROOT, 'docs', 'refactor', 'assets', '*.tsv')):
        for line in open(tsv, encoding='utf-8'):
            cols = line.rstrip('\n').split('\t')
            if len(cols) >= 3 and not line.startswith('#') and owned(cols[2], pats):
                out.add(cols[0])
    return out


def main():
    """Lists changed files not owned by the package; exit code 1 if any."""
    if len(sys.argv) < 2:
        print(__doc__); return 2
    package = sys.argv[1]
    base = sys.argv[2] if len(sys.argv) > 2 else 'refactor/main'
    pats = package_patterns(package)
    # Coordinator-approved integration edits are exact files, not broader source ownership.
    # Keep them separate from package_patterns so reference/comment gates retain their package scope.
    exceptions = load_json('ownership.json').get('approved_exceptions', {}).get(package, [])
    for path in exceptions:
        if any(c in path for c in '*?[') or path.startswith('!'):
            raise ValueError('Ownership exceptions must be exact paths: ' + path)
    allowed = asset_sources(pats)
    bad = [f for f in changed_files(base) if not owned(f, pats) and f not in allowed and f not in exceptions]
    for f in bad:
        print('NOT OWNED by %s: %s' % (package, f))
    print('%s: %d changed file(s) outside ownership' % (package, len(bad)))
    return 1 if bad else 0


if __name__ == '__main__':
    sys.exit(main())
