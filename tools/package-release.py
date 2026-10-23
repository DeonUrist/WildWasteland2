"""Package a self-contained WW2 build from its canonical runtime asset index."""
import argparse
import csv
import hashlib
import json
from pathlib import Path, PurePosixPath
import zipfile

def digest(data, algorithm="sha256"):
    return hashlib.new(algorithm, data).hexdigest()

def runtime_payloads(root):
    """Reject missing, escaping, duplicate and incomplete runtime payloads."""
    index = root / "asset-index.tsv"
    payloads, keys, paths = {}, set(), set()
    for row in csv.reader(index.read_text(encoding="utf-8-sig").splitlines(), delimiter="\t"):
        if not row:
            continue
        if len(row) != 2:
            raise ValueError("Malformed runtime asset row")
        key, relative = row[0], row[1].replace("\\", "/")
        path = PurePosixPath(relative)
        if path.is_absolute() or ".." in path.parts or ":" in relative:
            raise ValueError("Unsafe runtime asset path: " + relative)
        if key in keys or relative.lower() in paths:
            raise ValueError("Duplicate runtime asset: " + relative)
        keys.add(key)
        paths.add(relative.lower())
        data = (root / relative).read_bytes()
        if path.suffix.lower() == ".bundle" and not data.startswith(b"UnityFS\x00"):
            raise ValueError("Invalid UnityFS bundle header: " + relative)
        payloads[relative] = data
    required = {
        "Presentation/Models/apocaplayer_anims.bundle",
        "Presentation/Models/apocaplayer_camera.bundle",
        "Presentation/Models/apocaplayer_climbing.bundle",
        "Presentation/Models/npcai_dogs.bundle",
        "Presentation/Models/npcai_wildhound.bundle",
        "Presentation/Crawl/low-crawl.bundle",
        "Presentation/Sounds/witness-me.wav",
        "HUD/Sounds/headshot.mp3", "Gunplay/Sounds/knifestab.wav",
        "Survival/weather.bundle", "Survival/sound/sandstorm.wav",
        "Survival/sound/thunder.mp3", "Survival/LICENSE-JangaFX.txt",
    }
    missing = required - payloads.keys()
    if missing:
        raise ValueError("Required runtime assets are not indexed: " + ", ".join(sorted(missing)))
    screens = {p for p in payloads if p.startswith("HUD/LoadingScreens/Screen") and p.endswith(".png")}
    if screens != {"HUD/LoadingScreens/Screen%d.png" % i for i in range(1, 19)}:
        raise ValueError("Incomplete loading artwork")
    payloads["asset-index.tsv"] = index.read_bytes()
    return payloads

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dll", type=Path, required=True)
    parser.add_argument("--runtime-assets", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise SystemExit("Output exists; use a unique package filename")
    payloads = runtime_payloads(args.runtime_assets or args.dll.parent / "RuntimeAssets")
    payloads["WildWasteland.dll"] = args.dll.read_bytes()
    payloads["icon.png"] = (Path(__file__).resolve().parent.parent / "icon.png").read_bytes()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(args.output, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for relative, data in sorted(payloads.items()):
            archive.writestr("WildWasteland/" + relative, data)
    with zipfile.ZipFile(args.output) as archive:
        if archive.testzip() is not None:
            raise ValueError("Archive CRC failure")
        assert len(archive.namelist()) == len(payloads)
        for relative, data in payloads.items():
            if archive.read("WildWasteland/" + relative) != data:
                raise ValueError("Package verification failed: " + relative)
        assert [p for p in archive.namelist() if p.lower().endswith(".dll")] == ["WildWasteland/WildWasteland.dll"]
    metadata = {
        "archive": str(args.output), "version": "2.0.2", "entries": len(payloads),
        "runtime_assets": len(payloads) - 3, "size": args.output.stat().st_size,
        "dll_md5": digest(payloads["WildWasteland.dll"], "md5").upper(),
        "sha256": digest(args.output.read_bytes()),
        "assets": {name: digest(data) for name, data in sorted(payloads.items())},
    }
    args.output.with_suffix(".package.json").write_text(json.dumps(metadata, indent=2) + "\n")
    print(json.dumps({k: v for k, v in metadata.items() if k != "assets"}, indent=2))

if __name__ == "__main__":
    main()
