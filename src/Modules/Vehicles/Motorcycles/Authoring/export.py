"""Blender --background --disable-autoexec --python export.py -- MotoRack.blend output.glb"""
import bpy
import bmesh
import json
import sys
from pathlib import Path
from mathutils import Matrix, Vector

source, destination = map(Path, sys.argv[sys.argv.index('--') + 1:])
bpy.ops.wm.open_mainfile(filepath=str(source))
collider = bpy.data.objects['Collider']
assert len(collider.data.vertices) == 128, 'Authored Collider topology changed; update hull partitions.'
selected = [bpy.data.objects['Rack'], bpy.data.objects['Bags']]
textures = {'Rack': ('metal_rusted_31', 'metal_rusted_31__1707.png'),
            'Bags': ('fabric_10', 'fabric_10__1630.png')}
for obj in selected:
    name, filename = textures[obj.name]
    material = obj.active_material
    material.name = name
    for node in material.node_tree.nodes:
        if node.type == 'TEX_IMAGE':
            node.image = bpy.data.images.load(str(Path(r'V:\Repos\Apocalypter\Assets\raw\Texture2D') / filename))
            node.image.name = name
            node.image.source = 'FILE'

# Each open container has an outer bottom/rim and an inner bottom/rim.
# Five convex wall/bottom hulls retain the cavity; one hull of the container would fill it.
partitions = []
containers = []
for offset in (0, 96, 112):
    bottom = [offset + i for i in (0, 2, 6, 4)]
    rim = [offset + i for i in (8, 9, 10, 11)]
    inner_bottom = [offset + i for i in (12, 13, 14, 15)]
    inner_rim = [offset + i for i in (1, 3, 7, 5)]
    partitions.append(bottom + inner_bottom)
    for i in range(4):
        j = (i + 1) % 4
        partitions.append([ring[k] for ring in (bottom, rim, inner_bottom, inner_rim) for k in (i, j)])
    containers.append(inner_bottom + inner_rim)
for offset in (16, 56):
    partitions.extend([[offset + i for i in group] for group in (
        range(8), (0, 2, 4, 6, 8, 9, 10, 11), range(8, 16), range(12, 20), range(16, 24))])
for offset in (40, 80):
    partitions.extend([[offset + i for i in group] for group in (
        range(8), (0, 1, 4, 5, 8, 9, 10, 11), range(8, 16))])

for i, indices in enumerate(partitions):
    mesh = bpy.data.meshes.new('rack_hull_' + str(i))
    vertices = [collider.matrix_world @ collider.data.vertices[k].co for k in indices]
    hull = bmesh.new()
    for vertex in vertices:
        hull.verts.new(vertex)
    result = bmesh.ops.convex_hull(hull, input=list(hull.verts))
    unused = [v for v in result['geom_interior'] + result['geom_unused'] if isinstance(v, bmesh.types.BMVert)]
    if unused:
        bmesh.ops.delete(hull, geom=unused, context='VERTS')
    bmesh.ops.recalc_face_normals(hull, faces=list(hull.faces))
    hull.to_mesh(mesh)
    hull.free()
    obj = bpy.data.objects.new('COL_Rack_' + str(i), mesh)
    bpy.context.scene.collection.objects.link(obj)
    selected.append(obj)

# Convert the Sigma-imported Blender frame to Unity: (-x, z, -y).
conversion = Matrix(((-1, 0, 0), (0, 0, 1), (0, -1, 0)))
def pose(position, rotation):
    p = conversion @ position
    q = (conversion @ rotation @ conversion.transposed()).to_quaternion()
    return {'position': list(p), 'rotation': [q.x, q.y, q.z, q.w]}

layout = {'tools': [], 'locks': []}
for name in ('toolset_1', 'toolset_2'):
    obj = bpy.data.objects[name]
    entry = pose(obj.location, obj.matrix_basis.to_3x3().normalized())
    entry['name'] = name
    layout['tools'].append(entry)
for indices in containers:
    points = [collider.matrix_world @ collider.data.vertices[i].co for i in indices]
    center = sum(points, Vector()) / len(points)
    right = (points[3] - points[0]).normalized()
    forward = Vector((0, 1, 0))
    up = right.cross(forward).normalized()
    basis = Matrix((right, forward, up)).transposed()
    local = [basis.transposed() @ (p - center) for p in points]
    # Inset from all inner surfaces, including the slightly tapered bag walls.
    size = [2 * min(-min(p[i] for p in local), max(p[i] for p in local)) - .024 for i in range(3)]
    entry = pose(center, basis)
    entry['size'] = [size[0], size[2], size[1]]
    layout['locks'].append(entry)

for obj in bpy.context.selected_objects:
    obj.select_set(False)
for obj in selected:
    obj.hide_set(False)
    for collection in obj.users_collection:
        collection.hide_viewport = False
        collection.hide_render = False
    obj.select_set(True)
destination.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.export_scene.gltf(filepath=str(destination), export_format='GLB', use_selection=True,
                          export_yup=True, export_animations=False, export_cameras=False,
                          export_lights=False, export_extras=False)
destination.with_suffix('.layout.json').write_text(json.dumps(layout, indent=2) + '\n')
print('MOTO_LAYOUT', json.dumps(layout))
print('MOTO_EXPORT', len(partitions), 'convex hulls;', destination)
