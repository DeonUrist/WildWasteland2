# Wild Wasteland 0.1.37 RC34

Experimental prerelease. Gameplay verification remains Testing.

## Traveller hearing

Foot travellers previously bypassed sound assignment and gunshot responder selection, and their simple brain path skipped sound investigation. Active dismounted travellers now investigate gunshots, engines, horns and shouts, then search the heard location through the existing NPC senses and movement paths. Normal hearing distance, storms, faction rules and gunshot responder limits still apply. A newly heard shot does not grant fought-memory or reveal the player's live position.

Also includes the committed retirement of obsolete QuestsCompat registration since RC33: native Quests alone owns its module switch and optional Talk Open/Close hooks, removing duplicate postfix conflicts.

## Installation and testing

The full ZIP retains RC33's assets and defaults. Extract into `BepInEx/plugins` when ready to update, preserving customized assets. No personal configs or saves are bundled; this release adds no config entries or save format.

To retest hearing, enable NPC AI & navigation > Debug > VerboseLog (global WW VerboseLog alone is insufficient). Find active foot travellers, stay outside their sight cone and within the configured hearing range. Test a gunshot, a running engine and the configured player shout separately. They should investigate the heard spot and search without immediately knowing your new position after you move unseen. Existing near-range and nearest-responder limits mean a shot need not attract every traveller.

Confirm `investigates Engine at ghost #...`, `investigates Gunshot at ghost #...` or `investigates Shout at ghost #...` in the NPC log. Check the log no longer reports duplicate Quests/QuestsCompat Talk Open/Close postfix ownership when optional Talk is installed.

Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=false` passed with zero warnings and zero errors. Built and release-staged DLL MD5: `C196502CBC09EBB8DD7C74B415B17F44`. Per Denis's instruction, release staging stays under `V:\Repos\Apocalypter\_releases`; no files are deployed or staged in the game folder while MP testing continues. No runtime test was performed by the agent.
