"""Checks the file map against the working tree.

Wave A: every source file owned by the package must be gone from its source folder (moved/split/deleted),
unless its primary target is the module it already lives in, or it is the old module entry file that stays.
Wave C: no file remains in a retired module folder and no _from* folder remains.

Usage: python tools/refactor/filemap_coverage.py --wave A [--package A4] | --wave C | --unmapped
"""
import os, sys
from _common import cs_files, load_json, ROOT

RETIRED = ['PlayerBody', 'NPC', 'Guns', 'CleanHUD', 'Towns', 'Weather', 'Performance', 'EngineSimCompat', 'QuestsCompat']
STAYS_IN_A = {'Modules/PlayerBody/PlayerBodyModule.cs', 'Modules/NPC/NpcModule.cs', 'Modules/NPC/NpcModule.Config.cs',
              'Modules/Patrols/PatrolsModule.cs', 'Modules/Guns/GunsModule.cs', 'Modules/HUD/HudModule.cs',
              'Modules/CleanHUD/CleanHudModule.cs', 'Modules/Towns/TownsModule.cs', 'Modules/POI/PoiModule.cs',
              'Modules/Survival/SurvivalModule.cs', 'Modules/Weather/WeatherModule.cs', 'Modules/Quests/QuestsModule.cs',
              'Modules/EngineSimCompat/EngineSimCompatModule.cs', 'Core/Plugin.cs'}
SAME = {'Patrols': 'Patrols', 'HUD': 'HUD', 'POI': 'POI', 'Survival': 'Survival', 'Quests': 'Quests'}


def exists(p):
    """True when the src-relative (or developer/) path exists in the tree."""
    return os.path.exists(os.path.join(ROOT, p if p.startswith('developer/') else 'src/' + p))


def main():
    """Prints open items for the requested wave; exit 1 when any."""
    args = sys.argv[1:]
    fm = load_json('filemap.json')
    bad = []
    if '--unmapped' in args:
        mapped = {'src/' + x['path'] if not x['path'].startswith('developer/') else x['path'] for x in fm}
        for f in cs_files('src'):
            if f not in mapped and '/_from' not in f:
                bad.append('not in file map (new file? add a row or it is fine if created by the refactor): ' + f)
    elif '--wave' in args and args[args.index('--wave') + 1] == 'A':
        pkg = args[args.index('--package') + 1] if '--package' in args else None
        for x in fm:
            if pkg and not x['owner'].startswith(pkg):
                continue
            p = x['path']
            if p.startswith('developer/') or p in STAYS_IN_A:
                continue
            src_mod = p.split('/')[1] if p.startswith('Modules/') else 'Core'
            if x['action'] != 'DELETE' and SAME.get(src_mod) == x['primary']:
                continue
            if src_mod == 'Core' and x['primary'] == 'Core':
                continue
            if exists(p):
                bad.append('%s still at source (%s -> %s, owner %s)' % (p, x['action'], x['primary'], x['owner']))
    elif '--wave' in args and args[args.index('--wave') + 1] == 'C':
        for f in cs_files('src'):
            parts = f.split('/')
            if len(parts) > 2 and parts[1] == 'Modules' and parts[2] in RETIRED:
                bad.append('retired module file remains: ' + f)
            if '/_from' in f or '/_incoming' in f or '/_pages' in f:
                bad.append('transition folder remains: ' + f)
    else:
        print(__doc__); return 2
    for b in bad:
        print(b)
    print('open items: %d' % len(bad))
    return 1 if bad else 0


if __name__ == '__main__':
    sys.exit(main())
