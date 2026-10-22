# W0-B requests (changes needed in files W0-B does not own)

| id | file (owner) | change | why |
|---|---|---|---|
| W0B-1 | src/Core/ModuleHost.cs `Cleanup(IModule)` (W0-A / A1) | call `Textures.Release(m)` (and `WorldIndex.RemoveAll(m)`, `WorldTiles.RemoveAll(m)`, `FsmPause.ReleaseAll(m)`, `PrefabCatalog`/`Factions` owner cleanup as listed in reports/W0-B.md) | owned resources are destroyed on module shutdown |
