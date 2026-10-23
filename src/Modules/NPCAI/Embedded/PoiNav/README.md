# Shipped POI navigation graphs

On 2026-10-23 Denis's 37 current local `config/WildWasteland/NPCAI/PoiNav/*.txt` bakes were copied here verbatim,
including the previously missing Starter_Area. Building_2, Building_3 and TownsGasStation retain their existing shipped graphs.
The pack now contains 40 graphs: all 39 native POI types and TownsGasStation. Only complete `.txt` captures ship; `.bak` files stay local.

Both build entry points collect this folder automatically into `RuntimeAssets/NPCAI/PoiNav/` and `asset-index.tsv`.
`tools/package-release.py` includes those indexed files in every release package alongside the DLL. The `Embedded` authoring folder
name is retained, but the current asset system ships these as external files. Keep this folder in source control for future releases.

PoiNavStore still prefers a user's config graph over the shipped default. Every live placement retains the existing sliced terrain
validation. A mismatch uses the Tools baker for that instance only; it never replaces the shared shipped or authored graph.
With Debug VerboseLevel 2, a matching placement logs `shared bake terrain validated`; a mismatch logs `terrain mismatch` followed
by `instance terrain bake ready` when the instance bake succeeds. Gameplay verification remains Denis's responsibility.
