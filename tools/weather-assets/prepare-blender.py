"""Prepare a portable Blender authoring tool in this workspace; no system install."""
import hashlib
from pathlib import Path
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parent
NAME = "blender-4.5.3-windows-x64.zip"
BASE = "https://download.blender.org/release/Blender4.5/"


def response(url):
    request = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    return urllib.request.urlopen(request, timeout=60)


def main():
    target = ROOT / "Downloads" / NAME
    target.parent.mkdir(exist_ok=True)
    with response(BASE + "blender-4.5.3.sha256") as source:
        sums = source.read().decode("utf-8")
    expected = next(line.split()[0] for line in sums.splitlines() if line.rstrip().endswith(NAME))
    if not target.exists():
        temporary = target.with_suffix(".zip.partial")
        with response(BASE + NAME) as source, temporary.open("wb") as output:
            total = 0
            for chunk in iter(lambda: source.read(1024 * 1024), b""):
                output.write(chunk); total += len(chunk)
                if total % (64 * 1024 * 1024) == 0:
                    print(f"Blender: {total // (1024 * 1024)} MiB", flush=True)
        temporary.replace(target)
    digest = hashlib.sha256()
    with target.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    if digest.hexdigest().lower() != expected.lower():
        raise SystemExit("Official checksum mismatch. Do not execute this archive.")
    tools = (ROOT / "Tools").resolve()
    binary = tools / NAME.removesuffix(".zip") / "blender.exe"
    if not binary.exists():
        with zipfile.ZipFile(target) as archive:
            for entry in archive.infolist():
                if not (tools / entry.filename).resolve().is_relative_to(tools):
                    raise SystemExit("Unexpected path in Blender archive")
            archive.extractall(tools)
    (target.with_suffix(".zip.sha256.txt")).write_text(expected + "  " + NAME + "\n")
    print("Portable Blender:", binary)
    print("Official SHA256 verified:", expected)


if __name__ == "__main__":
    main()
