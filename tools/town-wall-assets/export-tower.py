"""Extract just the Camp_9 small tower from the existing offline dump, never the camp."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import re
import shutil

PARTS = ['container_3', 'tent_1_support', 'tent_1', 'decor/fence_small_4',
         'decor/fence_small_4 (1)', 'decor/fence_small_3', 'decor/ladder_L']
SELECTED = [('Mesh', 'shipping_container', 5048), ('Mesh', 'tent_support', 3464),
            ('Mesh', 'fabric_tent.001', 2638), ('Mesh', 'fence.007', 2512),
            ('Mesh', 'fence.003', 5451), ('Mesh', 'ladder_M', 3046),
            ('Material', 'metal_shanty_1', 197), ('Material', 'metal_rusted_1', 611),
            ('Material', 'fabric_torn_1', 526), ('Material', 'shipping_container_6m_3', 717)]


def export(raw, sources, output):
    output.mkdir(parents=True, exist_ok=True)
    catalog = {}
    for manifest in sorted(raw.glob('manifest*.csv')):
        for row in csv.DictReader(manifest.open(encoding='utf-8-sig', newline='')):
            if row.get('source_file') == 'sharedassets1.assets':
                catalog[(row['type'], row['name'], int(row['path_id']))] = row
    files = {}

    def copy(relative):
        source = raw / relative
        target = output / source.name
        if target.exists() and target.read_bytes() != source.read_bytes():
            raise RuntimeError(f'Refusing to replace customized asset: {target}')
        shutil.copyfile(source, target)
        files[target.name] = {'file': target.name, 'sha256': hashlib.sha256(target.read_bytes()).hexdigest()}
        if source.suffix == '.obj':
            for mtl in source.parent.glob(source.stem + '.mtl'):
                copy(str(mtl.relative_to(raw)))

    for key in SELECTED:
        row = catalog[key]
        copy(row['output'])
        if key[0] == 'Material':
            data = json.loads((raw / row['output']).read_text())
            for _, texture in data['m_SavedProperties']['m_TexEnvs']:
                pid = texture['m_Texture']['m_PathID']
                if pid:
                    matches = [r for (kind, _, ident), r in catalog.items() if kind == 'Texture2D' and ident == pid]
                    if len(matches) == 1:
                        copy(matches[0]['output'])
                    else:
                        # The older dump has this container normal PNG but omitted its manifest row.
                        missing = list((raw / 'Texture2D').glob(f'*__{pid}.png'))
                        if len(missing) != 1 or texture['m_Texture']['m_FileID'] != 0:
                            raise RuntimeError(f'Unresolved texture {pid}')
                        copy(str(missing[0].relative_to(raw)))
    # Save only the selected subtrees, including native transforms, solids and Ladder trigger.
    lines = (sources / 'data/hierarchy/prefabs/C/Camp_9.txt').read_text(encoding='utf-8').splitlines()
    stack, sections, selected = [], [], False
    for line in lines:
        match = re.match(r'^(\s*)- (.*?) (?:tag=.*? )?\[layer ', line)
        if match:
            depth = len(match[1]) // 2
            stack = stack[:depth] + [match[2]]
            path = '/'.join(stack[1:])
            selected = any(path == p or path.startswith(p + '/') for p in PARTS)
        if selected:
            sections.append(line)
    text = '\n'.join(sections) + '\n'
    if any(x in text for x in ('PlayMakerFSM', 'Rigidbody', 'ItemSpawner')):
        raise RuntimeError('Tower whitelist contains camp logic')
    if 'tag=Ladder' not in text or len([x for x in sections if '* MeshFilter' in x]) != 9:
        raise RuntimeError('Incomplete tower extraction')
    (output / 'tower-only-hierarchy.txt').write_text(text, encoding='utf-8')
    recipe = {'format': 'WW-NATIVE-SMALL-TOWER-1', 'source': 'Camp_9', 'rootName': 'TownsTower',
              'parts': PARTS, 'ladderSide': [2.63, 0, -.71], 'scale': [1, 1, 1],
              'navigation': 'Existing NPC sliced baker samples only this assembly on isolated flat ground. '
                            'First load publishes NPC NavMaps/Custom/TownsTower-<geometry hash>.wwnav; '
                            'later yaw-rotated instances reuse it. No camp or town-wide bake. '
                            'Ground navigation only; native player ladder retained, no new NPC ladder traversal.',
              'runtime': 'Shared original meshes/materials; static-only template assembled from these seven native subtrees.',
              'files': list(files.values())}
    (output / 'small-tower.json').write_text(json.dumps(recipe, indent=2) + '\n', encoding='utf-8')
    print(f'Extracted only small tower: {len(files)} asset files, seven subtrees, nine mesh instances -> {output}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=Path, default=Path('V:/Repos/Apocalypter/Assets/raw'))
    parser.add_argument('--sources', type=Path, default=Path('V:/Repos/Apocalypter/Sources'))
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    export(args.raw, args.sources, args.output)
