# Wild Wasteland 0.1.40 RC37

Experimental prerelease. Gameplay and visual verification remains Testing.

## Ignition default

The default Ignition key is now **I**. This updates initial key selection, the pre-Controls input mirror, invalid-file fallback, missing/invalid legacy-setting fallback and the setting description. Existing valid ignition-key.txt bindings and legacy bindings are retained, including E; change Ignition in the game's Controls screen if you want I on an existing install. No new config entry, file format or input action.

## Female kick texture

The female first-person kick previously reused the old arm atlas. It now uses WWMP's corrected trousers/boots artwork through a separate embedded atlas, extracted as Models/Player2_female_kick.png only when missing. Existing custom kick textures and custom ArmsTexture paths are preserved. Arm artwork, Male, third-person body, native kick timing/damage and crawl positioning are unchanged. See [texture backport](female-kick-texture.md).

## Installation and checks

Extract the full ZIP into BepInEx/plugins, preserving customized assets. Full RC36 assets and improved male/female player models are retained. No personal configs or saves are bundled; no new config entries or save-format changes.

1. Check startup says `Loading [Wild Wasteland 0.1.40]`, enabled modules start and no patch conflicts or initialization failures appear.
2. With no saved or legacy Ignition binding in a separate test setup, Controls > Ignition should show I. Enter a standard car and press I for its native ignition/start/stop sequence. With an existing saved binding, confirm it is retained; rebind in Controls and restart to check persistence. Existing Engine Sim click/hold behavior should remain intact when installed.
3. Select Female with FirstPersonArmSkin on and the default ArmsTexture path. Kick in first person: expect current trousers/boots instead of the old exposed upper-leg artwork. Check seams, stretching, weapon/tool arms, third-person body, Male and PlayerBody off/on. Preserve customized kick PNGs.

With F3 > Debug > VerboseLog enabled, fresh ignition setup reports `Controls: added "Ignition" (I)`; loading the kick atlas reports `Texture Player2_female_kick.png (2048x2048)`.

Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=false` passed with zero warnings and zero errors. Built/release-staged DLL MD5: `F5766835B86C6A7CA99FEDCBBC7A65B0`.

No game launch or harness. Standing no-base-game-deployment policy remains in force while MP testing continues. No Apocasetter-Index update or promotion to Verified.
