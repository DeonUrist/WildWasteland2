# Save-file-only persistence correction — 2026-10-22

Denis confirmed that all persistent world state must live inside the save file. D20 supersedes earlier plan instructions that retained module sidecars. This correction is implemented on `refactor/main`; it does not complete the remaining full-refactor packages.

## Changes

- Patrol cars/traffic retain the existing `Apocapatrol.Patrol` embedded payload; external binary writes, backup/fallback reads, registration and shutdown flushing are removed.
- NPC senses and generated nest positions, POI completion, HUD exploration/discovered locations and former quest crew identities now use module-owned embedded ES3 keys. Quest journal uses the shared writer instead of duplicated reflection/cache code.
- Existing town/gas-station and loot marker records remain embedded. Their save failures participate in the common transaction.
- Save names, world labels, lineage, recorded distance and thumbnail now live in the ES3 file too. Library listing, preview, backup labels and autosave retention consume embedded metadata.
- The common writer snapshots at SaveGame, reapplies queued records after native cache serialization, stores each slot once per batch and verifies every key before save publication. Writes outside the active save are rejected. Cancellation/load/new-world boundaries discard pending records.
- Unreadable or malformed present records block replacement instead of silently becoming empty state. Senses validation includes unmatched actors; pending/partial restores cannot be saved.
- Saves no longer discovers module config companions. The unused sidecar registry and central compatibility policy are removed. No sidecar migration/import is added for this fresh-start WW2 rebuild.

Authored content/layouts/settings and rebuildable geometry caches remain external. Durable undo journals and backups remain external recovery copies of the self-contained save, not required companions for loading it.

## Review and validation

Three parallel agents implemented/reviewed separate persistence owners. Coordinator review fixed premature Quests capture in `wait`, stale pending writes on new-world transitions and exact destination authorization during journaled snapshot metadata publication. Source searches found no remaining module world-state sidecar I/O or registrations. Final Windows Release/Deploy=false: **0 warnings, 0 errors**. Partial-method check: **183 declarations, 0 missing implementations**. Whitespace review passed.

No game, harness, deployment or push. In-game acceptance remains deferred until all refactor work is integrated. Acceptance should cover manual/autosave/overwrite/save-and-quit, loading a copied ES3 without module save folders, preservation of each owner’s state, and rollback after a failed embedded write/readback.

## Merge requirement

Older B10/B13 worktree checkpoints predate D20. Preserve these embedded readers/writers, failure latches, SaveGame-only capture and removal of registrations when integrating their renamed/moved files. Do not restore the sidecar implementations from those branches. Other refactor handoffs and package acceptance gates remain open.
