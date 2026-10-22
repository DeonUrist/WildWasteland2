# Vanilla navigation pack (Testing)

Put accepted `<prefab>-<signature>.wwnav` files here. The existing embedded-resource glob includes them in WildWasteland.dll as
`NPC.NavMaps.Vanilla.<filename>`; no project-file or release-installer change is required. They are read-only at runtime and survive the
custom pathfinding reset. An optional external pack can use `plugins/WildWasteland/NPC/NavMaps/Vanilla/` with the same file names.

The initial pack contains 41 version-1 maps (3,494,265 bytes), captured by Denis on 2026-10-17/18 and included at his request:
Camp_1-16, Building_1-7, Cave_1-7, Wreck_1-8 and Starter_Area: all 39 native POI types. Camp_10 and Building_7 each have two captured
signatures; both variants are retained and the runtime selects the exact matching filename. Source files came from the installed
NPC/NavMaps/Custom cache and the older loose plugin NPC/NavMaps folder. Binary headers/lengths match the format and filename keys.

The default installation needs only the DLL to receive these maps. Custom/missing/incompatible variants still bake into the separate
custom cache, and the custom-reset button never deletes this embedded pack. The optional purple collection diagnostic defaults off;
existing saved preferences remain intact. Denis still verifies embedded loading, navigation and terrain joins in game.

For future captures, use default navigation settings with custom POI replacements disabled, then NPC Debug > Export vanilla pathfinding
maps. It logs missing locations and writes candidates into config/WildWasteland/NPC/NavMaps/VanillaExport. Test the candidates on other
rotations/elevations and terrain joins before adding them here. Export filters names, so capture must use native geometry.

See `../../../NavMaps/README.md` for file format, fallback, custom POI discovery and reset behavior.
