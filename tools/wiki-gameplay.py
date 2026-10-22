"""Generate the player wiki indexes; validate pages, navigation and legacy links."""
import json
import re
from pathlib import Path


def generate(folder):
    root = Path(folder)
    nav = json.loads((root / "_Navigation.json").read_text(encoding="utf-8"))
    generated = {"Home", "_Sidebar", "Feature-Status"}
    pages = {}
    errors = []
    for path in sorted(root.rglob("*.md")):
        if any(part.startswith(".") for part in path.relative_to(root).parts):
            continue
        if path.stem in pages:
            errors.append("Duplicate page: " + path.stem)
        pages[path.stem] = path
    entries = []
    for group in nav["groups"]:
        for slug, summary in group["pages"]:
            if slug not in pages:
                errors.append("Missing navigation page: " + slug)
                continue
            text = pages[slug].read_text(encoding="utf-8")
            title = re.search(r"^# (.+)$", text, re.M)
            if not title:
                errors.append("Missing title: " + slug)
                continue
            entries.append({"title": title.group(1), "page": slug, "category": group["title"],
                            "summary": summary, "version": nav["version"]})
    indexed = [e["page"] for e in entries]
    if len(indexed) != len(set(indexed)):
        errors.append("Duplicate navigation entry")
    for alias, target in nav["aliases"].items():
        if alias not in pages or target not in indexed:
            errors.append("Invalid legacy link: %s -> %s" % (alias, target))
    for slug, path in pages.items():
        if slug in generated:
            continue
        text = path.read_text(encoding="utf-8")
        if slug not in indexed and slug not in nav["aliases"]:
            errors.append("Page absent from navigation/aliases: " + slug)
        for target in re.findall(r"\]\(([^)#\s]+)(?:#[^)]*)?\)", text):
            if target.startswith(("https://", "http://", "mailto:")):
                continue
            if "/" in target or target.endswith(".md") or target not in pages:
                errors.append("Broken wiki link in %s: %s" % (slug, target))
        for target in re.findall(r"\[\[([^\]|#]+)(?:[|#][^\]]*)?\]\]", text):
            if target.replace(" ", "-") not in pages:
                errors.append("Broken wiki link in %s: %s" % (slug, target))
    if errors:
        raise SystemExit("\n".join(errors))

    def link(slug):
        entry = next(e for e in entries if e["page"] == slug)
        return "[%s](%s)" % (entry["title"], slug)

    home = ["# Wild Wasteland 1.0", "", nav["intro"], "",
            "[Download %s](https://github.com/DeonUrist/WildWasteland/releases/tag/v%s) · " % (nav["version"], nav["version"]) +
            "[Install](Installation) · [First steps](Getting-Started) · [Settings and controls](Settings-And-Controls)", "",
            "## Explore the features", ""]
    for group in nav["groups"]:
        home += ["### " + group["title"], "", "| Guide | What you can do |", "| --- | --- |"]
        home += ["| %s | %s |" % (link(slug), summary) for slug, summary in group["pages"]]
        home.append("")
    home += ["## About this guide", "", nav["scope"], "",
             "[Feature overview](Feature-Status) · [Report a problem](https://github.com/DeonUrist/WildWasteland/issues)", ""]
    sidebar = ["**[Wild Wasteland 1.0](Home)**", ""]
    for group in nav["groups"]:
        sidebar += ["**" + group["title"] + "**", ""]
        sidebar += ["- " + link(slug) for slug, _ in group["pages"]]
        sidebar.append("")
    overview = ["# Features in Wild Wasteland 1.0", "",
                "The current single-player feature guide. Follow a topic for how it works, controls and limitations. "
                "A release version does not mean every combination has completed in-game testing.", ""]
    for group in nav["groups"]:
        overview += ["## " + group["title"], ""]
        overview += ["- %s — %s" % (link(slug), summary) for slug, summary in group["pages"]]
        overview.append("")
    for name, lines in (("Home.md", home), ("_Sidebar.md", sidebar), ("Feature-Status.md", overview)):
        (root / name).write_text("\n".join(lines), encoding="utf-8", newline="\n")
    (root / "features.json").write_text(json.dumps(entries, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    print("wiki-index: %d player guides, %d legacy links; all links valid; generated Home, sidebar, feature overview and JSON" %
          (len(entries), len(nav["aliases"])))
