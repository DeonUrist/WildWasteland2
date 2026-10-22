"""Preserve the native Camp_14 perimeter kit from the existing offline asset dump.

No game launch, asset modification, or new asset dump. Exports OBJ/MTL, exact PNGs,
native material data, source hierarchy and a reusable open-gate assembly recipe.
"""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import shutil


def export(raw: Path, sources: Path, output: Path):
    output.mkdir(parents=True, exist_ok=True)
    catalog = {}
    for manifest in sorted(raw.glob("manifest*.csv")):
        with manifest.open(encoding="utf-8-sig", newline="") as stream:
            for row in csv.DictReader(stream):
                if row.get("source_file") == "sharedassets1.assets":
                    catalog[(row["type"], row["name"], int(row["path_id"]))] = row
    selected = [
        ("Mesh", "fence_1", 4065), ("Mesh", "fence_2", 3929),
        ("Mesh", "fence_1.001", 2072), ("Mesh", "fence_2.002", 4681),
        ("Mesh", "flag_pole", 4088), ("Mesh", "flag_1sided.002", 2862),
        ("Material", "metal_shanty_1", 197),
        ("Material", "metal_rusted_1", 611), ("Material", "flag_scrapyard", 531),
    ]
    files = []

    def copy(relative):
        source = raw / relative
        if not source.is_file():
            raise FileNotFoundError(source)
        target = output / source.name
        if not target.exists():
            shutil.copyfile(source, target)
        elif target.read_bytes() != source.read_bytes():
            raise RuntimeError(f"Preserve customized asset; refusing overwrite: {target}")
        if source.suffix == ".obj":
            for mtl in source.parent.glob(source.stem + ".mtl"):
                copy(str(mtl.relative_to(raw)))
        files.append({"file": target.name, "sha256": hashlib.sha256(target.read_bytes()).hexdigest()})

    materials = []
    for key in selected:
        row = catalog[key]
        copy(row["output"])
        if key[0] == "Material":
            material = json.loads((raw / row["output"]).read_text())
            for name, data in material["m_SavedProperties"]["m_TexEnvs"]:
                ref = data["m_Texture"]
                if ref["m_PathID"]:
                    matches = [r for (kind, _, pid), r in catalog.items()
                               if kind == "Texture2D" and pid == ref["m_PathID"]]
                    if len(matches) != 1:
                        raise RuntimeError(f"Unresolved native texture {key}: {ref}")
                    copy(matches[0]["output"])
            materials.append(material["m_Name"])
    hierarchy = sources / "data/hierarchy/prefabs/C/Camp_14.txt"
    shutil.copyfile(hierarchy, output / "Camp_14-source-hierarchy.txt")
    recipe = {
        "format": "WW-NATIVE-PERIMETER-1", "source": "Camp_14",
        "sourceAssets": "sharedassets1.assets, game 2026-10-09",
        "wallPaths": ["decor_visible/fence_" + str(i) for i in range(1, 5)],
        "wallMeshes": ["fence_1", "fence_2", "fence_1.001", "fence_2.002"],
        "wallCollision": {"size": [.2794, 4.416, 9.3996], "center": [.0173, .1028, .0007858]},
        "gate": {
            "type": "open warehouse-camp entrance; no door mechanism",
            "bannerPath": "decor/flag_pole", "openingMetres": 15,
            "supportMesh": "flag_pole", "bannerMesh": "flag_1sided.002",
            "nativeRootScale": [4.1542, 4.1542, 4.1542],
            "prototypeRootScale": [14 / 1.8193, 4.1542, 4.1542],
            "bannerLocalPosition": [-.3312, .7567, -.3313],
            "postLocalPositions": [[-1.2428, -.9942, -.3127], [.5765, -.9942, -.3127]],
            "postCollision": {"size": [.0742, 3.6198, .0742], "center": [0, .5152, 0]},
            "signPolicy": "Original flag_scrapyard retained for both factions; faction replacement deferred.",
        },
        "materials": materials,
        "runtime": "Runtime shares meshes and native materials from the graph; these lossless source exports are reusable authoring assets, not a custom binary loader.",
        "files": list({row["file"]: row for row in files}.values()),
    }
    (output / "perimeter-kit.json").write_text(json.dumps(recipe, indent=2) + "\n", encoding="utf-8")
    print(f"Preserved {len(recipe['files'])} native asset files plus hierarchy/assembly recipe at {output}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--raw", type=Path, default=Path("V:/Repos/Apocalypter/Assets/raw"))
    parser.add_argument("--sources", type=Path, default=Path("V:/Repos/Apocalypter/Sources"))
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    export(args.raw, args.sources, args.output)
