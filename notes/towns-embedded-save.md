# Embedded Towns saves and expanded name defaults — 2026-10-20 (Testing)

Denis requested Towns data inside the robust world save/backups and explicitly said no sidecars are needed. He also supplied expanded faction name lists and requested them hardcoded.

## Save authority and migration

All new Towns save state is one ES3 string record, `WildWasteland.Towns.World.v1`, inside the actual `.es3` file. Its base64 envelope contains a versioned header, the unchanged TownStore6/gen9/layout8/paths6 binary layout/spawner record, the sorted interior identity list, and SHA-256 of the whole envelope payload. Native saved actors/items still retain their existing native identities and health keys. TownStore serializes/deserializes memory, and InteriorPopulation captures/restores arrays; neither writes config files. No `.towns.bin`, `.interiors.txt`, temporary or `.bak` sidecars are created by Towns anymore.

`OnSaveBegin` captures state while the normal robust transaction/native save freeze is active and writes the existing ES3 cache. A `SaveBus.AfterSave` callback repeats that write after native serialization/flush, stores the same cache and verifies exact record equality from disk. It reports failure through `SaveBus.ReportFailure` so robust publication aborts/rolls back, rather than silently accepting a missing town record. This callback finishes before SinglePlayerSaving calls SaveSystem.Complete. Journal rollback, immutable manual snapshots, autosaves, backup retention and restore protect the same world file containing the Towns record; no separate Towns backup path is needed. No new save job or patch.

Core/SaveSystem.cs has one necessary integration: PublishSnapshot skips discovered old `Towns/Saves` companions when the source has the embedded record, so the generic companion copier cannot keep generating Towns sidecars for new manual/autosave snapshots. Other modules' companion behavior is unchanged. This supports Denis's explicit direct-save integration request.

Loading reads the selected file's embedded record directly from disk, validates its checksum/header/lengths and existing TownStore invariants, then restores interior identities. A malformed/unsupported embedded record blocks Towns and saving; it never silently falls back to older config state. Only a save **without** the embedded key may read its existing `.towns.bin` and `.interiors.txt` as a read-only import. The next successful save contains that data directly and creates no new sidecar. The agent does not edit/delete/migrate any live save/config files; legacy files remain solely to prevent losing existing towns when older saves are opened. Existing named town identities and building placements are retained. Disabling Towns retains existing embedded keys through the normal world-save preservation rules.

## Names embedded exactly

Copied Denis's four prepared files byte-for-byte into `src/Modules/Towns/Embedded/Names/`, using the existing resource wildcard without csproj changes. Each has **260 unique usable names** (1,040 total). Verified source/export SHA-256 equality:

| Faction | SHA-256 |
| --- | --- |
| Animals | 3C7ACCCEC385CE40C6CED9F9819A9AB2B67E66B6E14D1DFE3CC56F9FCC2FF5EA |
| Mutants | 701A6DE0B6E5E1A8284FFB50ABE3D706E02E43E78B8255896D535488CB88FB9A |
| Scrapyard | A8A57A62EEC62087D277F2FB2EFFF5303C184078567050160A9991E220E746A6 |
| Coyotes | AE3E8D5A7DE25D11E4A05B8EC5D2302F494FFE04384A69DAA3123E5E8DA6FF90 |

DLL resource metadata contains all four `Towns.Names.<Faction>.txt` names. Replaced the old twelve-name literal defaults with these embedded lists. Missing editable files are seeded from the embedded lists; existing customized name files retain precedence. New unnamed towns still randomly sample unused names; named save towns retain their names. Names are optional editable configuration, not save-state sidecars.

## Build and acceptance

Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=false` succeeded with zero warnings/errors. Unique stage `V:/Repos/Apocalypter/_releases/WildWasteland_towns-embedded-save_20261020_041621.dll`; built/staged/installed MD5 **C9B8DE46D12DF9760656794AE3A430C0**. Denis closed Apocalypter after the concrete build was ready; process absence, both disabled-manifest roots and active runtime asset directories checked. DLL-only installed into standard WW. No agent game launch, harness, commit/push/release or live save/config/name edits. Embedded-only new snapshots need a matching DLL: older DLLs read only the old config format and cannot restore these new embedded records by themselves.

Denis verifies: load an older town save, save under a new name, reload that new save and confirm unchanged buildings/population/loot/name/traffic relationships. New snapshot must have the embedded key and no new Towns companion files. Check `embedded save verified` / `loaded ... directly from` logs. Exercise autosave and backup restore of an embedded save, including crew/native identity and staged town construction; failed-save/corrupt-record recovery remains unverified at runtime. Confirm expanded names are available on a clean install without external default files and that customized lists/saved names retain precedence. Earlier town contour/loading fixes remain included and Testing.
