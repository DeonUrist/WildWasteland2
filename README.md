# Wild Wasteland 2.0

Wild Wasteland is a modular single-player overhaul for Apocalypter and BepInEx 5. Version 2.0 consolidates body animation, NPC behavior, weapons, vehicle crews, towns, quests, weather and interface features into 15 modules with shared typed integrations.

Install the complete `WildWasteland` folder under `BepInEx/plugins/`. The DLL requires its asset folders and `asset-index.tsv`; copying only the DLL is insufficient for a fresh installation. Animation bundles, sounds, loading artwork, navigation data and authored defaults ship as external files.

2.0 starts with fresh saves and settings. World records, save descriptions and thumbnails are contained in the selected ES3 save. Apocasaver is not required. Optional compatible mods remain separate installations.

Windows build and deployment:

```powershell
dotnet build WildWasteland.csproj -c Release -p:Deploy=true
```

Close the game before deployment. Ordinary deployment preserves existing editable assets. For a clean setup, back up the previous plugin folder and WildWasteland configuration first, then install all assets anew. Settings are regenerated with defaults on startup.

Create a self-contained local archive from a build:

```powershell
python tools/package-release.py --dll bin/Release/WildWasteland.dll --output ../_releases/WildWasteland-2.0.0.zip
```

[Module ownership](docs/modules.md) · [Transition handover](docs/refactor/reports/C.md) · [Refactor decisions](docs/refactor/README.md)

Source integration is complete. Build success and the reported menu load do not replace gameplay acceptance by Denis. No release publication is implied by a local build or archive.
