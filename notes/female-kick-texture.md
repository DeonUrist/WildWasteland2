# Female first-person kick texture — Testing

2026-10-19. Denis requested the corrected female leg texture from WWMP. He separately reports crawling looks fine without Vista worldgen and will keep testing; crawl positioning is unchanged.

Cause: native Player_leg received the older Player2_female_arms.png through Arms.Tick. WWMP already fixes this with a separate kick material and atlas baked from the current female trousers/boots. Backport only that texture selection/loading path from PlayerBody/Body/Arms.cs and Model.cs. Do not import MP native-character selection or remote kick timing.

The 394,360-byte Embedded/Textures/female_kick.png is copied byte for byte from WWMP, SHA256 `3212666E7D3CE74E00108D33B401DAD4ADE2443CE2D3B19917DB379206767257`. Existing resource glob embeds it as PlayerBody.Textures.female_kick.png. Model.Kick extracts only a missing Models/Player2_female_kick.png, caches it through the normal texture loader and resets on character changes. Custom ArmsTexture paths retain their original leg artwork. FemaleArms/Female gates and original-material restoration remain intact; arms, male/third-person body and native kick timing/damage are unchanged.

Two C# files, 30 added/6 removed lines, plus one PNG. No Core, config entries, save keys or formats changed. Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=false` passed with zero warnings/errors. Built/staged DLL MD5 `418E9003EAB2812A7B37102E77066E4C`; unique stage `V:\Repos\Apocalypter\_releases\WildWasteland_female-kick_20261019_020813.dll`. Not deployed under the standing base policy. No game launch/harness, commit/push or release; published RC36 is unchanged.

Denis's test: restart with the staged DLL when ready, select Female with FirstPersonArmSkin on and the default ArmsTexture path, then kick in first person. Expect the current trousers/boots instead of the old exposed upper-leg artwork. Check seams and stretching, weapon/tool arms, third-person body and Male. Toggle PlayerBody off/on or change characters to verify normal restoration. Preserve any custom kick PNG: existing files are intentionally not overwritten.

With F3 > Debug > VerboseLog enabled, loading reports `Texture Player2_female_kick.png (2048x2048)`. Runtime appearance remains pending Denis's test.
