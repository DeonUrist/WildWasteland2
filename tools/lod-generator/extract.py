"""Read-only Unity asset extraction. No Unity editor, game launch, or source-asset changes."""
import argparse
import base64
import gc
import hashlib
import json
import math
import os
from pathlib import Path

import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler

VERSION = 3


def save_json(path, value):
    temporary = path.with_suffix(path.suffix + '.next')
    temporary.write_text(json.dumps(value, separators=(',', ':'), allow_nan=False), encoding='utf-8')
    temporary.replace(path)


def signature(name, count, bounds, counts):
    # Matches GeneratedLodPack.Key. Quantization tolerates serialization float noise.
    values = [math.floor(float(v) * 10000 + 0.5) for v in bounds]
    text = name + '\0' + '|'.join(map(str, [count, *values, len(counts), *counts]))
    return hashlib.sha256(text.encode('utf-8')).hexdigest()


def flatten(rows):
    return [v for row in rows for v in row]


def extract(mesh):
    if any(s.topology != 0 for s in mesh.m_SubMeshes):
        raise ValueError('non-triangle topology')
    if mesh.m_Shapes and mesh.m_Shapes.channels:
        raise ValueError('blend shapes: original retained')
    handler = MeshHandler(mesh)
    handler.process()
    count = handler.m_VertexCount
    if count < 3 or count > 2_000_000:
        raise ValueError('empty or oversized mesh')
    triangles = handler.get_triangles()
    # UnityPy does not apply baseVertex when decoding the index buffer.
    indices = [[int(i) + int(s.baseVertex or 0) for t in ts for i in t]
               for s, ts in zip(mesh.m_SubMeshes, triangles)]
    if sum(map(len, indices)) < 600:
        raise ValueError('under 200 triangles: original retained')
    if any(i < 0 or i >= count for sub in indices for i in sub):
        raise ValueError('out-of-range source index')
    channels = []
    names = ['m_Vertices', 'm_Normals', 'm_Tangents', 'm_Colors'] + ['m_UV' + str(i) for i in range(8)] + ['m_BoneWeights', 'm_BoneIndices']
    for kind, name in enumerate(names):
        rows = getattr(handler, name, None)
        if rows is None or len(rows) == 0:
            continue
        dimension = len(rows[0])
        if len(rows) != count or dimension < 1 or dimension > 4 or any(len(row) != dimension for row in rows):
            raise ValueError('unsupported vertex channel: ' + name)
        # Unity commonly stores float16 normals padded to four components; Mesh.normals exposes XYZ.
        if kind in (0, 1) and dimension == 4:
            rows = [row[:3] for row in rows]
            dimension = 3
        if kind >= 12 and dimension < 4:
            rows = [tuple(row) + (0,) * (4 - dimension) for row in rows]
            dimension = 4
        if kind >= 4 and kind <= 11 and dimension == 1:
            rows = [tuple(row) + (0,) for row in rows]
            dimension = 2
        if (kind in (0, 1) and dimension != 3) or (kind in (2, 3) and dimension != 4):
            raise ValueError('unsupported vertex channel dimension: ' + name)
        values = flatten(rows)
        if not all(math.isfinite(v) for v in values):
            raise ValueError('non-finite vertex channel')
        channels.append({'kind': kind, 'dimension': dimension, 'data': values})
    if not channels or channels[0]['kind'] != 0 or channels[0]['dimension'] != 3:
        raise ValueError('missing positions')
    skin = [c for c in channels if c['kind'] >= 12]
    if skin and (len(skin) != 2 or any(c['dimension'] != 4 for c in skin)):
        raise ValueError('unsupported skin influences')
    bindposes = []
    for matrix in mesh.m_BindPose or []:
        bindposes.extend(getattr(matrix, 'e' + str(r) + str(c)) for r in range(4) for c in range(4))
    if skin:
        if not bindposes:
            raise ValueError('missing skin bindposes')
        for k, bone in enumerate(skin[1]['data']):
            if skin[0]['data'][k] == 0:
                skin[1]['data'][k] = 0
            elif bone < 0 or bone >= len(bindposes) // 16:
                raise ValueError('invalid skin bindposes')
    aabb = mesh.m_LocalAABB
    bounds = [aabb.m_Center.x, aabb.m_Center.y, aabb.m_Center.z,
              aabb.m_Extent.x, aabb.m_Extent.y, aabb.m_Extent.z]
    key = signature(mesh.m_Name, count, bounds, [s.indexCount for s in mesh.m_SubMeshes])
    return {'key': key, 'name': mesh.m_Name, 'count': count, 'bounds': bounds,
            'channels': channels, 'bindposes': bindposes, 'indices': indices}


def inputs(game, extra):
    data = game / 'Apocalypter_Data'
    # Bundled native data, loose serialized assets, and streaming asset bundles.
    for path in sorted(data.rglob('*')):
        if not path.is_file():
            continue
        if path.name == 'data.unity3d' or path.suffix.lower() in ('.assets', '.bundle', '.assetbundle', '.unity3d') or (path.name.startswith('level') and not path.suffix):
            yield path
    # Active installed mod bundles are included automatically; disabled mod archives are outside plugins.
    for number, root in enumerate([game / 'BepInEx' / 'plugins', *extra]):
        root = Path(root).resolve()
        if root.is_file():
            yield root
        elif root.is_dir():
            yield from sorted(p for p in root.rglob('*') if p.is_file() and p.suffix.lower() in ('.bundle', '.assetbundle', '.unity3d', '.assets'))
        elif number > 0:
            raise ValueError('Include path does not exist: ' + str(root))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--game', type=Path, required=True)
    parser.add_argument('--work', type=Path, required=True)
    parser.add_argument('--include', action='append', default=[])
    parser.add_argument('--force', action='store_true')
    args = parser.parse_args()
    args.game = args.game.resolve()
    args.work.mkdir(parents=True, exist_ok=True)
    source_files = list(dict.fromkeys(p.resolve() for p in inputs(args.game.resolve(), args.include)))
    if not source_files:
        raise ValueError('No Unity asset files found')
    exports = sorted((args.game / 'BepInEx' / 'plugins' / 'WildWasteland' / 'Performance' / 'LODInput').glob('*.json'))
    dependencies = sorted(p for p in (args.game / 'Apocalypter_Data').rglob('*') if p.is_file() and p.suffix.lower() in ('.resource', '.ress'))
    dependency_roots = {p.parent for p in source_files}
    dependencies += [p for root in dependency_roots for p in root.glob('*') if p.is_file() and p.suffix.lower() in ('.resource', '.ress')]
    stamp_files = list(dict.fromkeys([*source_files, *dependencies, *exports]))
    sources = [{'path': str(p), 'size': p.stat().st_size, 'time': int(p.stat().st_mtime)} for p in stamp_files]
    state = {'version': VERSION, 'sources': sources, 'unitypy': UnityPy.__version__}
    cache = args.work / 'extraction.json'
    if not args.force and cache.exists():
        old = json.loads(cache.read_text(encoding='utf-8'))
        if old.get('state') == state and all((args.work / (m['key'] + '.json')).exists() for m in old['meshes']):
            print('Extraction unchanged; reusing cached meshes.', flush=True)
            return
    catalog, seen, excluded, failures = {}, {}, [], []
    collisions = set()
    for source_file in source_files:
        source_path = str(source_file)
        print('Scanning ' + source_path, flush=True)
        environment = UnityPy.load(source_path)
        for obj in environment.objects:
            if obj.type.name != 'Mesh':
                continue
            name = str(obj.path_id)
            try:
                mesh = obj.read()
                name = mesh.m_Name
                model = extract(mesh)
                payload = json.dumps(model, separators=(',', ':'), allow_nan=False)
                digest = hashlib.sha256(payload.encode()).hexdigest()
                key = model['key']
                if key in seen and seen[key] != digest:
                    collisions.add(key)
                    catalog.pop(key, None)
                    excluded.append({'name': name, 'reason': 'ambiguous runtime identity', 'source': source_path})
                    continue
                if key in collisions or key in seen:
                    continue
                seen[key] = digest
                save_json(args.work / (key + '.json'), model)
                catalog[key] = {'key': key, 'name': name, 'triangles': sum(map(len, model['indices'])) // 3, 'skinned': bool(model['bindposes'])}
            except ValueError as exc:
                excluded.append({'name': name, 'reason': str(exc), 'source': source_path})
            except Exception as exc:
                failures.append({'name': name, 'reason': type(exc).__name__ + ': ' + str(exc), 'source': source_path})
        del environment
        gc.collect()
        print(f'  {len(catalog)} unique candidates, {len(excluded)} exclusions, {len(failures)} decode failures', flush=True)
    native_keys = set(seen)
    for exported in exports:
        try:
            model = json.loads(exported.read_text(encoding='utf-8-sig'))
            if model.get('version') != 1:
                raise ValueError('unsupported loaded-mesh export version')
            key = model['key']
            if key in native_keys or key in collisions:
                continue  # Native assets are decoded directly, with their original channel dimensions.
            model['indices'] = [s['indices'] for s in model.pop('submeshes')]
            model.pop('version')
            counts = [len(s) for s in model['indices']]
            if signature(model['name'], model['count'], model['bounds'], counts) != key:
                raise ValueError('loaded-mesh export identity mismatch')
            if any(len(c['data']) != model['count'] * c['dimension'] for c in model['channels']):
                raise ValueError('loaded-mesh export channel mismatch')
            if any(i < 0 or i >= model['count'] for sub in model['indices'] for i in sub):
                raise ValueError('loaded-mesh export index mismatch')
            payload = json.dumps(model, separators=(',', ':'), allow_nan=False)
            digest = hashlib.sha256(payload.encode()).hexdigest()
            if key in seen and seen[key] != digest:
                collisions.add(key)
                catalog.pop(key, None)
                excluded.append({'name': model['name'], 'reason': 'ambiguous loaded-mesh identity; clear obsolete LODInput exports', 'source': str(exported)})
                continue
            seen[key] = digest
            save_json(args.work / (key + '.json'), model)
            catalog[key] = {'key': key, 'name': model['name'], 'triangles': sum(counts) // 3, 'skinned': bool(model['bindposes'])}
        except Exception as exc:
            failures.append({'name': exported.name, 'reason': str(exc), 'source': str(exported)})
    if not catalog:
        raise ValueError('No eligible meshes decoded; no pack published')
    save_json(cache, {'state': state, 'meshes': list(catalog.values()), 'excluded': excluded, 'failures': failures})


if __name__ == '__main__':
    main()
