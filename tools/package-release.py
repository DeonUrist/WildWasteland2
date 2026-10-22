"""Package the current DLL/artwork with assets retained from a verified prior release."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import zipfile


def digest(data, algorithm="sha256"):
    return hashlib.new(algorithm, data).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base", required=True, type=Path)
    parser.add_argument("--base-sha256", required=True)
    parser.add_argument("--dll", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    repo = Path(__file__).resolve().parent.parent
    if args.output.exists():
        raise SystemExit("Output already exists; use a unique release filename")
    if digest(args.base.read_bytes()) != args.base_sha256.lower():
        raise SystemExit("Base archive does not match the published SHA256")

    # The same external payload paths as the Windows/cloud builds; no personal config/save files are collected.
    payloads, index, destinations = {}, [], set()
    aliases = (("NPC/WomenOfWasteland/", "NPC/"), ("CleanHUD/LoadingScreens/", "LoadingScreens/"),
               ("PlayerBody/Portraits/", "PlayerBody/LegacyPortraits/"),
               ("PlayerBody/CharacterPortraits/", "PlayerBody/Portraits/"),
               ("PlayerBody/Textures/female_kick.png", "PlayerBody/Models/Player2_female_kick.png"))
    for source in sorted((repo / "src/Modules").glob("*/Embedded/**/*")):
        if not source.is_file() or source.suffix.lower() == ".md":
            continue
        module, relative = source.relative_to(repo / "src/Modules").as_posix().split("/Embedded/", 1)
        key, target = module + "." + relative.replace("/", "."), module + "/" + relative
        for old, new in aliases:
            if target.startswith(old):
                target = new + target[len(old):]
                break
        name = "WildWasteland/" + target
        if name.lower() in destinations:
            raise ValueError("Duplicate runtime asset destination: " + name)
        destinations.add(name.lower())
        payloads[name] = source.read_bytes()
        index.append(key + "\t" + target)
    payloads["WildWasteland/asset-index.tsv"] = ("\n".join(index) + "\n").encode("utf-8")
    runtime_roots = {"theme", "PlayerBody", "NPC", "Guns", "HUD", "CleanHUD",
                     "Patrols", "Survival", "Weather", "LoadingScreens", "Quests", "Towns"}

    def runtime(name):
        path = PurePosixPath(name)
        if path.is_absolute() or ".." in path.parts or "\\" in name or ":" in name:
            raise ValueError("Unsafe archive path: " + name)
        if len(path.parts) < 2 or path.parts[0] != "WildWasteland":
            raise ValueError("Unexpected installation root: " + name)
        if name.endswith("/"):
            return False
        if name in payloads:
            return True
        if path.name in {"WildWasteland.dll", "icon.png"} and len(path.parts) == 2:
            return True
        if len(path.parts) < 3 or path.parts[1] not in runtime_roots:
            return False
        if any(part.lower() in {"notes", "docs", "documentation", "verification", "presets"} for part in path.parts):
            return False
        if path.suffix.lower() in {".md", ".markdown", ".rst", ".pdf", ".html", ".cfg", ".es3", ".pdb", ".dll"}:
            return False
        if path.name.lower().startswith(("readme", "release-notes", "changelog")):
            return False
        if path.suffix.lower() == ".txt" and not path.name.lower().startswith(("license", "licence", "notice")):
            return False
        return True

    dll = args.dll.read_bytes()
    replacements = {"WildWasteland/WildWasteland.dll": dll,
                    "WildWasteland/icon.png": (repo / "icon.png").read_bytes()}
    replacements.update(payloads)
    for path in sorted((repo / "src/Modules/CleanHUD/Icons").glob("*.png")):
        replacements["WildWasteland/CleanHUD/Icons/" + path.name] = path.read_bytes()
    artwork = repo / "src/Modules/CleanHUD/Embedded/LoadingScreens"
    screens = sorted(artwork.glob("Screen*.png"))
    if {p.name for p in screens} != {"Screen%d.png" % i for i in range(1, 19)}:
        raise ValueError("Expected the complete 18-screen artwork set")
    for path in screens + [artwork / "BG_MAIN.png", artwork / "screens.json"]:
        replacements["WildWasteland/LoadingScreens/" + path.name] = path.read_bytes()

    retained, removed, written = {}, [], set()
    with zipfile.ZipFile(args.base) as old, zipfile.ZipFile(args.output, "x", compression=zipfile.ZIP_DEFLATED) as new:
        if len(old.namelist()) != len(set(old.namelist())):
            raise ValueError("Duplicate names in base archive")
        for entry in old.infolist():
            if not runtime(entry.filename):
                removed.append(entry.filename)
                continue
            # Replace the numbered default set completely; never retain obsolete screens.
            if re.fullmatch(r"WildWasteland/LoadingScreens/Screen\d+\.png", entry.filename) and entry.filename not in replacements:
                removed.append(entry.filename)
                continue
            data = replacements.get(entry.filename)
            if data is None:
                data = old.read(entry)
                retained[entry.filename] = (entry.CRC, entry.file_size)
            new.writestr(entry, data)
            written.add(entry.filename)
        for name, data in replacements.items():
            if name not in written:
                new.writestr(name, data)

    with zipfile.ZipFile(args.output) as archive:
        if archive.testzip() is not None:
            raise ValueError("Archive CRC failure")
        names = archive.namelist()
        if len(names) != len(set(names)) or not all(runtime(name) for name in names):
            raise ValueError("Archive contains duplicate or non-runtime entries")
        for name, data in replacements.items():
            if archive.read(name) != data:
                raise ValueError("Replacement mismatch: " + name)
        for name, signature in retained.items():
            entry = archive.getinfo(name)
            if (entry.CRC, entry.file_size) != signature:
                raise ValueError("Retained asset mismatch: " + name)
        if [name for name in names if name.lower().endswith(".dll")] != ["WildWasteland/WildWasteland.dll"]:
            raise ValueError("Unexpected plugin DLLs")
        if "WildWasteland/Weather/LICENSE-JangaFX.txt" not in names:
            raise ValueError("Required weather asset license notice missing")

    metadata = {"archive": str(args.output), "entries": len(names), "retained_asset_entries": len(retained),
                "removed_documentation_entries": len(removed), "loading_screens": len(screens),
                "size": args.output.stat().st_size, "dll_md5": digest(dll, "md5").upper(),
                "sha256": digest(args.output.read_bytes())}
    args.output.with_suffix(".package.json").write_text(json.dumps(metadata, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(metadata, indent=2))


if __name__ == "__main__":
    main()
