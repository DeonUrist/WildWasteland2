# Wild Wasteland 1.0.6 — Towns on Vistas terrain

When Vistas World Gen / World Amp is loaded, Towns and added POIs now leave terrain heights to Vistas. Towns no longer flatten pads, grade access tracks, restore earlier terrain snapshots or add parking pavement. Buildings, walls and dressing use existing ground heights. The separate added-POI system also stops inserting its flattening step into the native height output.

Towns can appear without a road connection and use naturally flatter sites. This can produce fewer towns, even with Ensure selected towns enabled. Town bus/convoy visits that require access routes are unavailable in this mode. Roadside gas stations use stricter natural-ground checks. Loose object clearance remains, and normal terrain preparation is retained when Vistas is absent.

Existing town names, horizontal layouts and spawner progress remain; vertical placement and derived access paths update on load. No new settings or save format. The save countdown/warning text is also smaller and has no background plate.

Install the complete ZIP into `BepInEx/plugins/`, preserving customized external files. This supersedes 1.0.5's post-grade town-pad restoration. The starting-square seam was traced to a starting tile at 600 m height scale beside 1,500 m neighbours; the exact startup cause is not yet established. This release removes WW's Towns/POI terrain-writing paths with Vistas, but seam resolution and natural-ground placement remain **Testing**. Read-only starting-tile diagnostics remain available in the log.

After updating, start a Vistas world and check the starting tile's four borders in 3D. Visit a new town, check entrances/walls and absence of access tracks, then save/reload. Startup reports `[Towns] Vistas natural-terrain mode`. Check a world without Vistas retains its normal town pads. Compilation does not verify these gameplay results.
