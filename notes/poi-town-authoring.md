# POI and town authoring — Testing

Developer begins with POI Editor, Navigation Editor and Patrol Editor buttons.
POI Editor offers Create POI, a searchable list of vanilla/custom types, Open
existing POI, and per-type Spawns checkboxes. Opening an existing type uses a live
instance when available; otherwise it creates a static authoring copy in clear
desert. The editor camera moves to that instance; the player remains in place.
Vanilla static scenery can be moved/deleted alongside added props. Native scripts,
animated/dynamic objects and gameplay triggers remain native. Close/discard restores
the committed scenery. SAVE POI queues geometry preparation and the existing sliced
isolated navigation baker; inspect the page's status for final success or failure.
NPC navigation must be enabled. Layouts support up to 8192 entries, positions within
500 m of the POI origin and footprints up to 1000 m; footprints grow to fit solids.

Town templates retain four faction defaults. Add town type copies the selected
saved template, then allows an independent Animals/Mutants/Scrapyard/Coyotes faction,
walls, merchant requirement and scrolling checkbox selections for interior/exterior
POIs. No interior checks retains the normal pool. Parking companions remain outside
walls; existing limits of 29 companions and 32 total lots apply. Native POIs need
valid navigation maps/entrances; unsafe boss camps remain excluded.
Construction places interior buildings, completes the perimeter, then places parking
and external companion POIs. Exterior companions use independent POI navigation,
outside the interior town overlay. Isolated tower preparation accepts shipped maps
without requiring a duplicate in the writable custom cache.

Spawns affects newly generated terrain only. Existing native types keep their
default enabled behavior; new authored types default to reserve/off. Checked custom
types use the existing POI world spawn chance and roadside siting; no site is forced
when clearance/terrain/road checks fail. Unchecked types remain selectable in town
templates. Existing committed custom desert placements survive save/load and streaming.

Portable authoring files beside the installed DLL:

- `POI/Custom/<name>.json`: custom footprint, props and baked map together.
- `POI/Layouts/<type>-<hash>.json`: vanilla/custom scenery overrides; edited native
  layouts also carry navigation data and measured planning bounds.
- `POI/spawns.json`: explicit per-type random spawn choices.

Town templates are in `BepInEx/config/WildWasteland/POI/Templates/<id>.json`.
World records use the additive ES3 key `WildWasteland.POI.Authored.v1`, including
authored definitions, layout snapshots and committed desert placements. Existing
town record keys/versions remain unchanged. An older world without the key loads
the installed authoring definitions. Loading a world with snapshots uses those
definitions without replacing local authoring files; editing and saving a POI is
an explicit publication back to its portable file.

For later bundling, copy finalized custom JSON to
`src/Modules/POI/Embedded/Custom/`, native overrides to `Embedded/Layouts/`, and town
template JSON to `Embedded/Templates/`. The existing runtime asset index/build
packages these files. Reusable custom files include their bake; no separate manual
navigation-cache copy is needed. Runtime world snapshots and configuration are not
release assets. The source's native POI assets remain required.

Manual acceptance: create/save a named POI; wait for saved status; reopen and edit
it; add it to a new town type and an exterior list; save/load the world and revisit
the desert instance and a town containing it. Open a vanilla Building type, move or
remove a static prop, save and wait for its bake, then load/revisit it. Verify
discard restores the last saved state. Check/uncheck Spawns and generate new terrain
at an elevated POI spawn chance; reserve types must not generate independently but
must remain usable in towns. Confirm the three Developer buttons and Navigation/
Patrol editor sessions. Logs: `[POI] custom POI saved:`, `[POI] edited native POI
saved:`, `[POI] authored world restored:`, `[POI] random custom POI ... placed:`.

No in-game acceptance has been performed by the agent.
