# Editable town gate banners — 2026-10-20 (Testing)

Denis requested the Scrapyard and Coyote gate textures in the active mod folder,
so he can edit the Coyote version. Both gates previously borrowed the same native
Camp_14 `decor/flag_pole/flag_2_2_sided` cloth material `flag_scrapyard`.
The source hierarchy and material JSON identify diffuse texture path ID 1653.

Created missing-only copies of the existing lossless export in
`E:/SteamLibrary/steamapps/common/Apocalypter/BepInEx/plugins/WildWasteland/Towns/Perimeter/`:

- `gate_scrapyard.png`
- `gate_coyotes.png`

Both are 1024x1024, 1,724,806 bytes, initially SHA256
`8de7f1a3518ec72f7fa24c4a2ecb204c920f4501ed3164a4b949ed9c607783e9`.
The original native export and existing normal map are retained. Do not overwrite
the editable files when deploying or refreshing source exports.

`TownWalls.cs` loads each optional PNG once per kit into an owned texture and
shared instanced material; new town gates select the correct faction material
on the exact cloth child. Native shader, normal map, UV scale/offset and support
materials remain borrowed and unchanged. Missing or invalid PNGs retain the native
banner; invalid images warn with the path. No per-frame file IO, new scheduler job,
hook, config key or save format. Owned textures/materials are disposed with the kit.
Restart after editing; existing live gates do not hot-reload.

Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=false` passed with
zero warnings/errors. Unique stage:
`V:/Repos/Apocalypter/_releases/WildWasteland_gate-textures_20261020_054303_549.dll`,
MD5 `94B772DA5DBF720049534C8263BE8075`. Game process was running, so the new loader
DLL is not installed yet. PNGs are already available for editing. No game launch,
harness, live config/save edits, commit, push or release.

After installing with the game closed, Denis can restart and load a walled town.
Look for `[Towns] gate texture loaded: <path>` for each PNG; Coyote gates use only
the Coyote artwork and Scrapyard gates retain theirs. Verify correct orientation,
transparency, native cloth shading and reload behavior. Runtime visuals pending.

## Supplied defaults and deployment — 2026-10-20

Denis edited the Coyote PNG, then requested current town names and gate textures
as defaults, a commit and DLL build. Captured both live PNGs byte for byte in
`src/Modules/Towns/Embedded/Perimeter/`; the csproj's existing resource glob embeds
them as `Towns.Perimeter.gate_coyotes.png` and `Towns.Perimeter.gate_scrapyard.png`.
TownWalls extracts each default only when its editable file is absent, using
CreateNew to avoid replacing a file created concurrently. Existing overrides win.
No image processing or native material changes; the Coyote artwork is Denis's.

All four current editable names files already match the embedded defaults byte
for byte: 260 unique names per faction, 1,040 total. No name-source diff was needed.
Saved town names keep precedence; new towns sample from the current lists.

SHA256 hashes verified against all six resources in the built DLL:

| Resource suffix | SHA256 |
| --- | --- |
| Perimeter.gate_coyotes.png | A28AF9AD33EE3EB8D78B8A657C8CB3D212E81AEDDE19A67087F1708A0BB78F12 |
| Perimeter.gate_scrapyard.png | 8DE7F1A3518EC72F7FA24C4A2ECB204C920F4501ED3164A4B949ED9C607783E9 |
| Names.Animals.txt | 3C7ACCCEC385CE40C6CED9F9819A9AB2B67E66B6E14D1DFE3CC56F9FCC2FF5EA |
| Names.Coyotes.txt | AE3E8D5A7DE25D11E4A05B8EC5D2302F494FFE04384A69DAA3123E5E8DA6FF90 |
| Names.Mutants.txt | 701A6DE0B6E5E1A8284FFB50ABE3D706E02E43E78B8255896D535488CB88FB9A |
| Names.Scrapyard.txt | A8A57A62EEC62087D277F2FB2EFFF5303C184078567050160A9991E220E746A6 |

Windows Release/Deploy=true passed zero warnings/errors, with game closed,
WW enabled and active runtime asset folders present. Build, unique stage and
installed DLL match MD5 `FCFCFF15BEB5CE31DEB672EA27DFA4D8`.
Stage: `V:/Repos/Apocalypter/_releases/WildWasteland_town-defaults_20261020_102344_865.dll`.
Supersedes the earlier pending deployment. No live names/textures/configs/saves
were modified; no game launch, harness, push, tag or release. Runtime appearance
and missing-file extraction still require Denis's acceptance.
