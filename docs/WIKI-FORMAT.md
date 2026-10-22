# Wild Wasteland player wiki

Denis requested a full rewrite for released single-player 1.0 on 2026-10-19,
explicitly as a wiki of **gameplay features, not technical terms**. This replaces
the former module/framework/status-table format for the public wiki.

## Write for players

Each main page explains what players can do, how it works, relevant controls,
where to change a setting, and practical limitations. Use the current UI labels.
Explain effects in ordinary language; do not expose class names, save schemas,
patch hooks, scheduler work, source commits or test handovers in feature pages.

Organize topics around character/movement, combat, enemies, road encounters,
exploration, quests, survival, saving and support. Use a table only when it helps
compare actions or choices. There is no required technical metadata table.

Describe the current released state. Read the code and embedded tuned defaults:
the latter may override an ordinary Bind fallback. Existing player settings and
bindings survive updates. Distinguish included features from optional external
mods, and do not turn unverified work into a verified claim. Keep project-board
status tracking and implementation records in the repository, not the player index.

## Source and navigation

The editable snapshot is `V:/Repos/Apocalypter/WildWastelandWiki`. Canonical guides
are in `Guides/` and `Features/`; a few retained canonical filenames remain in
their original folders. Old public slugs remain as short pointers to the relevant
current guide. They contain no old technical tables or RC history and are not
listed in the sidebar.

`_Navigation.json` contains the release version, homepage introduction/scope,
ordered groups of `[page-slug, gameplay-summary]` pairs and legacy-to-current
page mappings. Add every canonical page to one group. Register every legacy page
as an alias. Never reuse a basename, even in a different folder.

Links use `[visible words](Page-Slug)` or `[[Page-Slug]]`: no folder, no `.md`.
GitHub wikis flatten directories. External links use their full HTTPS URL.

Run from the main repository:

```powershell
python tools/wiki-index.py ../WildWastelandWiki
```

With `_Navigation.json` present, `wiki-index.py` delegates to `wiki-gameplay.py`.
It validates unique names, navigation membership, legacy destinations and internal
links, then generates `Home.md`, `_Sidebar.md`, `Feature-Status.md` and
`features.json`. Never edit those generated files by hand. `Feature-Status` is now
the player feature overview; its old URL is retained. The JSON export now describes
player guides (title/page/category/summary/version), not project-board rows.
The old generator remains available for historical snapshots without a navigation
manifest. The new mode also works on a flattened wiki checkout.

## Publish

If the snapshot has no `.git`, clone
`https://github.com/DeonUrist/WildWasteland.wiki.git` to a separate staging folder.
Check its recent history and compare it with the snapshot before editing. Generate
the indexes, then copy Markdown files by unique basename plus `_Navigation.json`
and `features.json` into that checkout. Inspect the diff and run link validation
before committing/pushing an authorized wiki update. Never push runtime code,
create a release or change game files as a side effect of a documentation task.
