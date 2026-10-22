"""Download explicitly selected CC0 authoring data; never executes archive contents."""
import argparse
import hashlib
from html.parser import HTMLParser
import json
from pathlib import Path
import shutil
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parent
ORIGIN = "https://jangafx.com/software/embergen/download/free-vdb-animations"
ASSETS = {
    "cloud": ("https://www.mediafire.com/file/879onggscuon7ew/CloudPackVDB.zip/file", "CloudPackVDB.zip"),
    "dust-devil": ("https://www.mediafire.com/file/wr3wt0szq656f0s/Dust_Devil.rar/file", "Dust_Devil.rar"),
}


class DownloadLink(HTMLParser):
    url = None

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag == "a" and attrs.get("id") == "downloadButton":
            self.url = attrs.get("href")


def response(url):
    request = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0 WWWeatherAssetPreparation"})
    return urllib.request.urlopen(request, timeout=60)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("asset", choices=ASSETS)
    args = parser.parse_args()
    page_url, filename = ASSETS[args.asset]
    downloads = ROOT / "Downloads"
    downloads.mkdir(exist_ok=True)
    destination = downloads / filename
    if not destination.exists():
        with response(page_url) as page:
            links = DownloadLink()
            links.feed(page.read().decode("utf-8"))
        if not links.url or not links.url.startswith("https://"):
            raise SystemExit("Download link unavailable. Use the named archive link on " + page_url)
        temporary = destination.with_suffix(destination.suffix + ".partial")
        digest = hashlib.sha256()
        total = 0
        with response(links.url) as source, temporary.open("wb") as target:
            if "text/html" in source.headers.get("Content-Type", ""):
                raise SystemExit("Host returned HTML instead of an archive; use the source page manually.")
            while True:
                chunk = source.read(1024 * 1024)
                if not chunk:
                    break
                target.write(chunk)
                digest.update(chunk)
                total += len(chunk)
                if total % (32 * 1024 * 1024) == 0:
                    print(f"{filename}: {total // (1024 * 1024)} MiB", flush=True)
        temporary.replace(destination)
    else:
        digest = hashlib.sha256()
        with destination.open("rb") as source:
            for chunk in iter(lambda: source.read(1024 * 1024), b""):
                digest.update(chunk)
    metadata = {
        "name": args.asset, "author": "JangaFX", "license": "CC0-1.0",
        "license_evidence": ORIGIN, "download_page": page_url,
        "filename": filename, "bytes": destination.stat().st_size,
        "sha256": digest.hexdigest(), "purpose": "Offline baking; do not ship raw VDBs with WW",
    }
    destination.with_suffix(destination.suffix + ".provenance.json").write_text(json.dumps(metadata, indent=2) + "\n")
    if destination.suffix == ".zip":
        output = (ROOT / "Sources" / args.asset).resolve()
        with zipfile.ZipFile(destination) as archive:
            # Validate all paths before writing anything from the archive.
            for entry in archive.infolist():
                target = (output / entry.filename.replace("\\", "/")).resolve()
                if not target.is_relative_to(output):
                    raise ValueError("Unsafe archive path: " + entry.filename)
            if not output.exists():
                output.mkdir(parents=True)
                for entry in archive.infolist():
                    target = output / entry.filename.replace("\\", "/")
                    if entry.is_dir():
                        target.mkdir(parents=True, exist_ok=True)
                    else:
                        target.parent.mkdir(parents=True, exist_ok=True)
                        with archive.open(entry) as source, target.open("wb") as sink:
                            shutil.copyfileobj(source, sink)
        print("Source folder:", output)
    print("Downloaded:", destination)
    print("SHA256:", digest.hexdigest())


if __name__ == "__main__":
    main()
