#!/usr/bin/env python3
"""Regenerates the index pages of the Wild Wasteland wiki (docs/WIKI-FORMAT.md).

Usage:  python3 tools/wiki-index.py <wiki folder>      (default: ../WildWastelandWiki next to this repo, else ./wiki)

Reads every Markdown page under the wiki folder (recursively; GitHub wikis flatten folders, so file names must be unique),
parses the metadata table of each feature page (first table after the title: Module / Framework / Status / F6 toggle /
F7 tools / Source / Since), the module overview pages (<Id>-Module.md) and the backlog entries of Backlog-Candidates.md,
checks statuses and links, and writes:
  Home.md            intro, module table, feature index grouped by module / framework
  _Sidebar.md        guides, architecture, modules -> features
  Feature-Status.md  one row per feature: feature | module | framework | status | F6 toggle | F7 tools | page
  features.json      [{title, page, module, framework, status, f6, f7, since}, ...]  (project board export)
Exit code 1 on an unknown status, a duplicate file name, a missing metadata field or a link to a page that does not exist.
No dependencies beyond the Python 3 standard library.
"""
import json
import os
import re
import sys

STATUSES = ["Backlog", "Working on", "Testing", "Verified", "Done"]
FIELDS = ["Module", "Framework", "Status", "F6 toggle", "F7 tools", "Source", "Since"]
MODULE_ORDER = ["PlayerBody", "NPC", "Guns", "HUD", "Patrols"]
MODULE_NAMES = {
    "PlayerBody": "Player body & animation",
    "NPC": "NPC AI & navigation",
    "Guns": "Guns, ammo & tracers",
    "HUD": "Combat HUD",
    "Patrols": "Patrols, traffic & War Rig",
    "Future": "Future (candidates)",
}
GUIDE_ORDER = ["Installation", "F6-Settings", "F7-Tool-Window", "Migration-From-Standalone-Mods", "Troubleshooting",
               "Performance", "Saves-And-Compatibility"]
ARCH_ORDER = ["Overview", "Core-Services", "Scheduler-And-Frame-Budget", "Harmony-Hooks", "Config-And-Tools",
              "Adding-A-Module", "Release-Process"]
GENERATED = {"Home", "_Sidebar", "Feature-Status", "_Footer"}

LINK_RE = re.compile(r"\[\[([^\]|#]+)(?:[|#][^\]]*)?\]\]|\]\(([^)#\s]+)(?:#[^)]*)?\)")


def die(msg):
    sys.stderr.write("wiki-index: " + msg + "\n")
    sys.exit(1)


def read(path):
    with open(path, encoding="utf-8") as f:
        return f.read()


def write(path, text):
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def title_of(lines):
    for line in lines:
        if line.startswith("# "):
            return line[2:].strip()
    return None


def first_table(lines):
    """Returns (cells, end_index) of the first pipe table after the title, cells = list of row lists."""
    i = 0
    while i < len(lines) and not lines[i].startswith("|"):
        i += 1
    rows = []
    while i < len(lines) and lines[i].startswith("|"):
        cells = [c.strip() for c in lines[i].strip().strip("|").split("|")]
        rows.append(cells)
        i += 1
    return rows, i


def parse_meta(rows):
    meta = {}
    for cells in rows:
        if len(cells) < 2:
            continue
        key = cells[0]
        if key in ("Field", "") or set(key) <= set("-: "):
            continue
        meta[key] = cells[1]
    return meta


def parse_feature_table(text):
    """Order of the pages linked in a module overview's '## Features' table (first column links)."""
    order = []
    section = text.split("\n## Features", 1)
    if len(section) < 2:
        return order
    for line in section[1].split("\n")[1:]:
        if not line.startswith("|"):
            if line.strip() == "":
                continue
            break
        m = re.search(r"\]\(([^)]+)\)|\[\[([^\]|]+)", line)
        if m:
            order.append((m.group(1) or m.group(2)).strip())
    return order


def parse_backlog(text):
    entries = []
    current = None
    for line in text.split("\n"):
        if line.startswith("## "):
            if current:
                entries.append(current)
            current = {"title": line[3:].strip(), "Module": "Future", "Framework": "-", "Status": "Backlog",
                       "F6 toggle": "-", "F7 tools": "-", "Source": "", "Since": "-"}
        elif current and line.startswith("- ") and ":" in line:
            k, v = line[2:].split(":", 1)
            k = k.strip()
            if k in ("Module", "Framework", "Status", "Source", "Since", "F6 toggle", "F7 tools"):
                current[k] = v.strip()
    if current:
        entries.append(current)
    return entries


def main():
    if len(sys.argv) > 1:
        root = sys.argv[1]
    else:
        here = os.path.dirname(os.path.abspath(__file__))
        cand = os.path.normpath(os.path.join(here, "..", "..", "WildWastelandWiki"))
        root = cand if os.path.isdir(cand) else os.path.normpath(os.path.join(here, "..", "wiki"))
    if not os.path.isdir(root):
        die("wiki folder not found: " + root)
    # Player-guide navigation replaces the former development-board front page.
    if os.path.isfile(os.path.join(root, "_Navigation.json")):
        import runpy
        guide = runpy.run_path(os.path.join(os.path.dirname(os.path.abspath(__file__)), "wiki-gameplay.py"))
        guide["generate"](root)
        return

    pages = {}      # name -> {path, rel, folder, text, lines}
    errors = []
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if not d.startswith(".")]
        for fn in sorted(filenames):
            if not fn.endswith(".md"):
                continue
            name = fn[:-3]
            path = os.path.join(dirpath, fn)
            rel = os.path.relpath(path, root).replace(os.sep, "/")
            if name in pages:
                errors.append("duplicate page name '%s': %s and %s" % (name, pages[name]["rel"], rel))
                continue
            text = read(path)
            pages[name] = {"path": path, "rel": rel, "folder": os.path.dirname(rel), "text": text, "lines": text.split("\n")}

    features = []       # dicts with title, page, module, framework, status, f6, f7, since, source
    modules = {}        # id -> {page, title, meta, order}
    guides, arch, other = [], [], []
    for name, p in sorted(pages.items()):
        if name in GENERATED:
            continue
        folder = p["folder"]
        top = folder.split("/")[0] if folder else ""
        title = title_of(p["lines"]) or name
        if name == "Backlog-Candidates":
            for e in parse_backlog(p["text"]):
                if e["Status"] not in STATUSES:
                    errors.append("%s: entry '%s' has unknown status '%s'" % (p["rel"], e["title"], e["Status"]))
                features.append({"title": e["title"], "page": name, "module": e["Module"], "framework": e["Framework"],
                                 "status": e["Status"], "f6": e["F6 toggle"], "f7": e["F7 tools"], "since": e["Since"],
                                 "source": e["Source"], "backlog": True, "idx": len(features)})
            continue
        if top == "Guides":
            guides.append((name, title))
            continue
        if top == "Architecture":
            arch.append((name, title))
            continue
        if top == "Modules":
            rows, _ = first_table(p["lines"])
            meta = parse_meta(rows)
            missing = [f for f in FIELDS if f not in meta]
            if missing:
                errors.append("%s: metadata table missing %s" % (p["rel"], ", ".join(missing)))
                continue
            if meta["Status"] not in STATUSES:
                errors.append("%s: unknown status '%s' (allowed: %s)" % (p["rel"], meta["Status"], ", ".join(STATUSES)))
            mod = meta["Module"]
            if name.endswith("-Module"):
                modules[mod] = {"page": name, "title": title, "meta": meta, "order": parse_feature_table(p["text"])}
                continue
            features.append({"title": title, "page": name, "module": mod, "framework": meta["Framework"],
                             "status": meta["Status"], "f6": meta["F6 toggle"], "f7": meta["F7 tools"],
                             "since": meta["Since"], "source": meta["Source"], "backlog": False, "idx": len(features)})
            continue
        other.append((name, title))

    # link check (every page, including generated ones that exist)
    for name, p in pages.items():
        if name in GENERATED:
            continue
        plain = re.sub(r"```.*?```", "", p["text"], flags=re.S)
        plain = re.sub(r"`[^`\n]*`", "", plain)          # inline code is not a link
        for m in LINK_RE.finditer(plain):
            target = (m.group(1) or m.group(2) or "").strip()
            if not target or target.startswith(("http://", "https://", "mailto:")):
                continue
            if "/" in target or target.endswith(".md"):
                errors.append("%s: link '%s' must be a bare page name (no folder, no .md)" % (p["rel"], target))
                continue
            target = target.replace(" ", "-")
            if target not in pages and target not in GENERATED:
                errors.append("%s: link to missing page '%s'" % (p["rel"], target))

    for mod in sorted({f["module"] for f in features if not f["backlog"]}):
        if mod not in modules:
            errors.append("module '%s' has feature pages but no %s-Module.md overview" % (mod, mod))

    if errors:
        for e in errors:
            sys.stderr.write("wiki-index: " + e + "\n")
        sys.exit(1)

    # ordering
    def mod_key(m):
        return (MODULE_ORDER.index(m) if m in MODULE_ORDER else len(MODULE_ORDER), m)

    def feat_key(f):
        order = modules.get(f["module"], {}).get("order", [])
        if f["backlog"]:
            return (mod_key(f["module"]), f["idx"], "")        # file order of Backlog-Candidates.md
        return (mod_key(f["module"]), order.index(f["page"]) if f["page"] in order else len(order), f["title"].lower())

    features.sort(key=feat_key)
    module_ids = sorted(set(list(modules.keys()) + [f["module"] for f in features]), key=mod_key)

    def ordered(items, pref):
        return sorted(items, key=lambda it: (pref.index(it[0]) if it[0] in pref else len(pref), it[1].lower()))

    guides = ordered(guides, GUIDE_ORDER)
    arch = ordered(arch, ARCH_ORDER)

    def link(page, text=None):
        return "[%s](%s)" % (text or page, page)

    # ---------------- Home.md
    out = []
    out.append("# Wild Wasteland")
    out.append("")
    out.append("One BepInEx 5 plugin for **Apocalypter** that combines five former mods as modules with one load order, one set of Harmony "
               "patches, one save / scene lifecycle and one shared frame budget. Every feature keeps its own on/off switch (F6), every technical "
               "setting and editor is on the F7 tool window. This wiki is generated from one page per feature; the page index below and "
               "%s mirror the project board." % link("Feature-Status"))
    out.append("")
    out.append("**Start here**: " + " · ".join(link(n, t) for n, t in guides))
    out.append("")
    out.append("**Architecture** (for Denis and AI sessions): " + " · ".join(link(n, t) for n, t in arch))
    out.append("")
    out.append("## Modules")
    out.append("")
    out.append("| Module | Replaces | Features | Testing | Verified | Done | F7 pages |")
    out.append("|---|---|---|---|---|---|---|")
    for mod in module_ids:
        if mod not in modules:
            continue
        m = modules[mod]
        feats = [f for f in features if f["module"] == mod]
        cnt = lambda s: sum(1 for f in feats if f["status"] == s)
        src = m["meta"].get("Source", "")
        out.append("| %s | %s | %d | %d | %d | %d | %s |" % (
            link(m["page"], MODULE_NAMES.get(mod, mod) + " (`" + mod + "`)"), src, len(feats),
            cnt("Testing"), cnt("Verified"), cnt("Done"), m["meta"].get("F7 tools", "-")))
    backlog = [f for f in features if f["backlog"]]
    if backlog:
        out.append("| %s | - | %d | - | - | - | - |" % (link("Backlog-Candidates", MODULE_NAMES["Future"]), len(backlog)))
    out.append("")
    out.append("Status values: " + ", ".join("`%s`" % s for s in STATUSES) + " (the GitHub project columns).")
    out.append("")
    out.append("## Features")
    out.append("")
    for mod in module_ids:
        feats = [f for f in features if f["module"] == mod]
        if not feats:
            continue
        head = MODULE_NAMES.get(mod, mod)
        if mod in modules:
            head = link(modules[mod]["page"], head)
        out.append("### " + head)
        out.append("")
        fw_seen = []
        for f in feats:
            if f["framework"] not in fw_seen:
                fw_seen.append(f["framework"])
        for fw in fw_seen:
            fl = [f for f in feats if f["framework"] == fw]
            label = fw if fw not in ("-", "") else "General"
            out.append("- **%s**: " % label + ", ".join(
                "%s (%s)" % (link(f["page"], f["title"]) if not f["backlog"] else f["title"], f["status"]) for f in fl))
        out.append("")
    write(os.path.join(root, "Home.md"), "\n".join(out).rstrip("\n") + "\n")

    # ---------------- _Sidebar.md
    out = ["**[Home](Home)**", ""]
    out.append("**Guides**")
    for n, t in guides:
        out.append("- " + link(n, t))
    out.append("")
    out.append("**Architecture**")
    for n, t in arch:
        out.append("- " + link(n, t))
    out.append("")
    out.append("**Modules**")
    for mod in module_ids:
        feats = [f for f in features if f["module"] == mod and not f["backlog"]]
        if mod in modules:
            out.append("- " + link(modules[mod]["page"], MODULE_NAMES.get(mod, mod)))
        elif feats:
            out.append("- " + MODULE_NAMES.get(mod, mod))
        for f in feats:
            out.append("  - " + link(f["page"], f["title"]))
    out.append("- " + link("Backlog-Candidates", MODULE_NAMES["Future"]))
    out.append("- " + link("Feature-Status", "Feature status (board)"))
    for n, t in other:
        out.append("- " + link(n, t))
    write(os.path.join(root, "_Sidebar.md"), "\n".join(out) + "\n")

    # ---------------- Feature-Status.md
    out = ["# Feature Status", "",
           "Generated by `tools/wiki-index.py` from the metadata tables of the feature pages (and the entries of "
           + link("Backlog-Candidates") + "). Columns = the GitHub project board. Do not edit by hand.", ""]
    counts = {s: sum(1 for f in features if f["status"] == s) for s in STATUSES}
    out.append("Totals: " + ", ".join("%s %d" % (s, counts[s]) for s in STATUSES) + " (%d entries)." % len(features))
    out.append("")
    out.append("| Feature | Module | Framework | Status | F6 toggle | F7 tools | Page |")
    out.append("|---|---|---|---|---|---|---|")
    for f in features:
        page = link(f["page"]) if not f["backlog"] else link("Backlog-Candidates")
        out.append("| %s | %s | %s | %s | %s | %s | %s |" % (
            f["title"], f["module"], f["framework"], f["status"], f["f6"] or "-", f["f7"] or "-", page))
    write(os.path.join(root, "Feature-Status.md"), "\n".join(out) + "\n")

    # ---------------- features.json
    export = [{"title": f["title"], "page": f["page"], "module": f["module"], "framework": f["framework"],
               "status": f["status"], "f6": f["f6"], "f7": f["f7"], "since": f["since"]} for f in features]
    write(os.path.join(root, "features.json"), json.dumps(export, indent=2, ensure_ascii=False) + "\n")

    print("wiki-index: %d pages, %d modules, %d features (%s) -> Home.md, _Sidebar.md, Feature-Status.md, features.json in %s" % (
        len(pages), len(modules), len(features), ", ".join("%s %d" % (s, counts[s]) for s in STATUSES if counts[s]), root))


if __name__ == "__main__":
    main()
