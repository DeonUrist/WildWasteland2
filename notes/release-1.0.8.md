# Wild Wasteland 1.0.8 — Weapons Pack, BloodFX and native IK

- BloodFX 0.2.0 compatibility keeps its existing managed updater running after Apocalypter destroys its plugin object during loading, and preserves spray collision handling. BloodFX's F8 debug hit is suppressed while WW's Guns compatibility is active, leaving F8 available to other features. Saved BloodFX bindings/configuration are unchanged; other debug bindings and a test key rebound away from F8 retain their behavior. BloodFX remains a separate optional installation.
- Weapons Pack 1.6.4 receives matching white WW weapon silhouettes, chainsaw machete-slash contact audio and first-person shadows using the actual chainsaw/flamethrower meshes. Existing pack animations remain.
- The burning debuff sends eligible ground NPCs to nearby cover until it ends. Chainsaw killing blows frighten nearby visible hostile humans for six seconds; animals and mutants do not receive chainsaw witness fear. Existing cover/navigation eligibility applies, including reachable-cover requirements; flying/seated bodies and followers are not given a new movement driver.
- A drawn chainsaw triples the existing footstep hearing radius; holstering restores normal range. Movement and obstruction requirements remain.
- Performance no longer patches or skips native FastIK updates based on distance or visibility. The off-screen NPC animation feature retains its animator-culling behavior.
- Experimental chainsaw dismemberment is disabled and excluded from compilation.

Extract the complete ZIP into `BepInEx/plugins/`, preserving customized external files and backing up saves. Use the included DLL with its asset folders. Optional generated LOD packs, the developer profiler, Weapons Pack and BloodFX are not bundled.

Denis confirmed BloodFX effects work. A log review of 25 developer samples measured the recovered BloodFX updater at 3.1 ms/s average and 6.1 ms/s maximum; these scheduler averages do not establish peak frame cost, long-session memory behavior or universal performance. No new bridge performance problem was found in the reviewed scope. Other visual/gameplay changes remain Testing. Check weapon icons/shadows/switching, normal hits and burning/witness retreats, footstep range and Juggernaut movement after updating.
