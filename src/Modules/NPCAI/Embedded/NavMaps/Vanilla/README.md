# Vanilla navigation pack (Testing)

The current release asset pack has 51 grids. On 2026-10-23 Denis's three local Residential_01 signatures were added verbatim from
`config/WildWasteland/NPCAI/NavMaps/Custom/`, retaining the existing 48 grids. Despite this authoring folder's name, both build entry
points now stage these as external `NPCAI/NavMaps/Vanilla/` assets indexed by `asset-index.tsv`; release packaging includes them all.
Existing signature checks, sliced terrain validation and non-persistent per-instance rebaking remain unchanged.

Put accepted `<prefab>-<signature>.wwnav` files here. The existing asset glob includes them without project-file or release-installer
changes. They are read-only at runtime and survive the custom pathfinding reset. The installed pack lives at
`plugins/WildWasteland/NPCAI/NavMaps/Vanilla/` with the same file names.

The initial pack contains 41 version-1 maps (3,494,265 bytes), captured by Denis on 2026-10-17/18 and included at his request:
Camp_1-16, Building_1-7, Cave_1-7, Wreck_1-8 and Starter_Area: all 39 native POI types. Camp_10 and Building_7 each have two captured
signatures; both variants are retained and the runtime selects the exact matching filename. Source files came from the installed
NPC/NavMaps/Custom cache and the older loose plugin NPC/NavMaps folder. Binary headers/lengths match the format and filename keys.

The default installation receives these maps with the release's runtime assets. Custom/missing/incompatible variants still bake into the
separate custom cache, and the custom-reset button never deletes this shipped pack. The optional purple collection diagnostic defaults off;
existing saved preferences remain intact. Denis still verifies embedded loading, navigation and terrain joins in game.

For future captures, use default navigation settings with custom POI replacements disabled, then NPC Debug > Export vanilla pathfinding
maps. It logs missing locations and writes candidates into config/WildWasteland/NPCAI/NavMaps/VanillaExport. Test the candidates on other
rotations/elevations and terrain joins before adding them here. Export filters names, so capture must use native geometry.

See `../../../NavMaps/README.md` for file format, fallback, custom POI discovery and reset behavior.
