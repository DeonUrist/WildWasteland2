"""Run with portable Blender --background --python bake-clouds.py -- [options]."""
import argparse
import json
import math
from pathlib import Path
import sys
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parent


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--size", type=int, default=512)
    parser.add_argument("--samples", type=int, default=128)
    parser.add_argument("--clouds", type=int, default=4)
    parser.add_argument("--views", type=int, default=2)
    parser.add_argument("--output", type=Path, default=ROOT / "Bakes" / "cloud-stamps")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    if min(args.size, args.samples, args.clouds, args.views) < 1:
        parser.error("All values must be positive")
    sources = sorted((ROOT / "Sources" / "cloud").rglob("*.vdb"))[:args.clouds]
    if not sources:
        parser.error("No VDBs found. Run fetch-source.py cloud first")
    args.output.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = args.samples
    scene.cycles.use_denoising = True
    scene.render.threads_mode = "FIXED"
    scene.render.threads = 8
    scene.render.resolution_x = args.size
    scene.render.resolution_y = args.size
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.view_settings.view_transform = "Standard"
    scene.world = bpy.data.worlds.new("BakeWorld")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (1, 1, 1, 1)
    scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.25
    camera_data = bpy.data.cameras.new("BakeCamera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 12
    camera = bpy.data.objects.new("BakeCamera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    light_data = bpy.data.lights.new("BakeSun", "SUN")
    light_data.energy = 2
    light = bpy.data.objects.new("BakeSun", light_data)
    scene.collection.objects.link(light)
    light.rotation_euler = (0.4, -0.6, -0.7)
    records = []
    for cloud_index, source in enumerate(sources):
        bpy.ops.object.volume_import(filepath=str(source))
        volume = bpy.context.object
        volume.data.grids.load()
        bpy.context.view_layer.update()
        corners = [Vector(corner) for corner in volume.bound_box]
        low = Vector(tuple(min(c[i] for c in corners) for i in range(3)))
        high = Vector(tuple(max(c[i] for c in corners) for i in range(3)))
        extent = max(high - low)
        if extent <= 0:
            raise RuntimeError("VDB bounds unavailable: " + str(source))
        factor = 10 / extent
        volume.scale = (factor,) * 3
        volume.location = -(low + high) * (0.5 * factor)
        material = bpy.data.materials.new("NeutralCloud")
        material.use_nodes = True
        nodes = material.node_tree.nodes
        nodes.clear()
        output = nodes.new("ShaderNodeOutputMaterial")
        scatter = nodes.new("ShaderNodeVolumePrincipled")
        scatter.inputs["Color"].default_value = (0.85, 0.85, 0.85, 1)
        attribute = nodes.new("ShaderNodeAttribute")
        attribute.attribute_name = "density"
        density = nodes.new("ShaderNodeMath")
        density.operation = "MULTIPLY"
        density.inputs[1].default_value = 0.65
        material.node_tree.links.new(attribute.outputs["Fac"], density.inputs[0])
        material.node_tree.links.new(density.outputs[0], scatter.inputs["Density"])
        material.node_tree.links.new(scatter.outputs["Volume"], output.inputs["Volume"])
        volume.data.materials.append(material)
        for view_index in range(args.views):
            angle = view_index * math.tau / args.views + 0.4
            camera.location = Vector((math.sin(angle) * 20, -math.cos(angle) * 20, 5))
            camera.rotation_euler = (-camera.location).to_track_quat("-Z", "Y").to_euler()
            filename = f"cloud{cloud_index + 1:02d}-view{view_index:02d}.png"
            scene.render.filepath = str(args.output / filename)
            print("BAKING", filename, "from", source.name, flush=True)
            bpy.ops.render.render(write_still=True)
            records.append({"png": filename, "vdb": source.name, "view": view_index})
        if cloud_index == 0:
            bpy.ops.wm.save_as_mainfile(filepath=str(args.output.parent / "cloud-bake.blend"))
        bpy.data.objects.remove(volume, do_unlink=True)
    (args.output.parent / "cloud-stamps.provenance.json").write_text(json.dumps({
        "source": "JangaFX Cloud Pack", "license": "CC0-1.0", "blender": bpy.app.version_string,
        "kind": "static variants, not animation frames", "size": args.size, "samples": args.samples,
        "views": records, "lighting": "single neutral baked lighting setup; six-way lighting not baked yet",
    }, indent=2) + "\n")
    print("WW CLOUD BAKE OK:", args.output, flush=True)


if __name__ == "__main__":
    main()
