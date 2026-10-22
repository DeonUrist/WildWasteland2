# Parking beside gas stations (2026-10-20, Testing)

Denis reported merchant markings without merchants and requested parking immediately beside, rather than inside, gas stations. He also requested all current data committed and a build.

The town parking shell was aligned to `LaneCenter`; travel stops selected canopy lane parking. TownFactory now preserves the 24 x 40 m open apron, builds its concrete slab, and puts the station's measured mesh bounds beside its north edge with a 2 m gap. Towns supplies ordinary side-by-side vehicle parking. TownParking reserves a conservative 40 x 40 m station footprint in the flattened pad. Paths version 7 rebuilds the derived graph/access and pad radius for saved towns on reload; building positions and spawner records remain. The store accepts versions through 7, so older DLLs that only accept 6 cannot read a subsequently saved town record.

Merchant NPCs used their native player-entry trigger; InteriorPopulation previously prepared only EnemySpawn groups. CoyotesMerchantSpawn groups in Coyote merchant lots now enter that same sliced native-trigger queue after construction. Their authored selection/count, item registration, and completed spawn state remain authoritative. This addresses the preparation gap; an already completed/dead merchant is not respawned. The live log predates HEAD's Camp_10 bake matching fix and reports Camp_10 excluded, with Camp_15 selected instead. It does not prove a physical obstruction or successful runtime merchant creation.

Source: nine Towns files, 20 additions / 12 removals. Existing setting key/default retained; its help text describes separate parking. No live config or save edits. Read-only comparison of live authoring data found all PoiNav and vanilla exports already matched the repository. Included the two additional Custom bakes (`TownsGasStation-D3F499208C08CBC5.wwnav`, `TownsTower-4995763BD5C37F49.wwnav`) in the shipped map pack and the current station layout (adds table_1). Backups and personal settings are excluded.

Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=false`: zero errors, two existing PoiNav CS0649 warnings. Stage `V:/Repos/Apocalypter/_releases/WildWasteland_town-parking-beside_20261020_221815_001.dll`; MD5 `0739FEF34FF792CE9F064CCDBDF54543`. Matching `.asset-index.tsv` staged beside it; the full current runtime payload is in `bin/Release/RuntimeAssets`. Game process 39352 was running: no deployment, launch, harness, push, or release.

After closing the game, deploy the staged DLL and matching index together, adding missing indexed defaults without overwriting customized runtime assets. The live gas-station layout already matches the committed version.

Denis's checks:

1. Reload the world and visit the existing town. Expect a separate concrete parking apron immediately south of the station, a 2 m gap, and no station over the parked bus/visitor positions. Confirm `[Towns] lot ...: gas station beside open parking; 2 m gap outside the walls`.
2. Visit a Coyote town with a merchant camp. Expect the merchant after town preparation without entering its small trigger; confirm `[Towns] interior pre-spawn .../CoyotesMerchantSpawn...; native sequence retained.` Check trading and sale stock. Completed/dead merchants remain completed.
3. Check arrivals/departures, the east gate, station ground height/dressing, then save/reload and return to menu/new world. Expect stable building positions, usable parking and no preparation/cleanup exceptions. Gameplay, terrain and navigation remain unverified until Denis tests.
