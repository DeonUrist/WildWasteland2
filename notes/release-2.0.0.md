# Wild Wasteland 2.0.0 — local transition build

Consolidates the overhaul into 15 modules and shared typed integrations. Player and humanoid NPC plans share the character driver; obsolete module facades and cross-owner partial implementations are retired. Persistent world data, descriptions and thumbnails live in ES3; Apocasaver and legacy imports are removed.

The complete runtime package includes canonical animation/audio/artwork/data paths. Earlier missing climbing/headshot payloads are now included directly in the source build; packaging no longer borrows assets from a previous release.

Fresh saves/settings are required. The requested clean installation archives existing data for recovery before resetting it. This is a local build, not a published release or a claim of gameplay verification. See docs/refactor/reports/C.md for installation details.
