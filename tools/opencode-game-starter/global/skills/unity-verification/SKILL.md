---
name: Unity verification
description: Diagnose Unity Console and compilation errors, discover live Unity MCP tools, and verify Unity code or scene changes using the correct project and current Pipeline command schemas.
---

# Unity verification

Use this for Unity development, console errors, scene editing, compilation and PlayMode checks.
This skill is installed globally for OpenCode; it does not require a Codex or Unity-specific skill loader.

## Diagnose before editing

1. Resolve the absolute Unity project root from the active workspace. Never select another Editor
   merely because it is running. Include --project-path on every Unity CLI call.
2. Read the project's instructions and discover available MCP tools. Resources are documents;
   an empty resource list does not mean executable tools are absent. Code Mode exposes names under
   tools.<server>; use only names actually advertised. Connection status is not a compile verdict.
3. Run this helper from PowerShell, replacing PROJECT with the absolute project folder:

   `python "$env:USERPROFILE\.config\opencode\skills\unity-verification\scripts\unity_check.py" --project "PROJECT"`

   Use OpenCode's shell tool to execute this command. A Python script is not an HTTP endpoint.
   Do not replace it with fetch(), a web request, or a guessed localhost health check.
   The helper discovers the Editor through Unity CLI; no server on port 3000 is required.

   The local `unity_mcp.py` adapter exposes the same CLI as uniquely named MCP tools: console_get,
   console_status, scene_list_open, scene_get_hierarchy, gameobject_create, job_batch, etc.
   It preserves command groups and corrects the batch params schema to object. Do not call the old
   bare get/status/create tools. Batch operation params are objects, e.g.
   {"operations":[{"command":"console status","params":{}}],"dry_run":true}.
   A failed inspection is not evidence of an empty scene; do not replace scenes to work around it.
   With compact native tools enabled, call unity_console_get, unity_console_status or
   unity_scene_list_open directly through the real tool interface. Do not print XML tool markup
   or use tools.unity.* JavaScript. For other operations, use unity_command_catalog to obtain
   the full command schema, then unity_command_run with command and parameters as an object.

   It checks the live command catalog, reads console ground truth and retrieves bounded error text.
   It stores full responses in the project's ignored Artifacts/ folder. Its exit code is nonzero on
   compile failure, busy compilation, console errors, or unavailable/incompatible commands.
   Errors retained in the log can be historical: compare timestamps with current compilation status.
4. If the helper reports disconnected/Safe Mode, use the project's Unity Editor.log (on Windows,
   $env:LOCALAPPDATA\Unity\Editor\Editor.log; this can belong to another Editor, so check the project
   in the log). Report this fallback and read only the relevant error region. Do not close unsaved work.

## Change and verify

- Inspect the actual file and the first relevant current error. Fix one root cause and recompile.
- For unfamiliar Unity APIs, inspect project usage, installed package source or official API docs.
  Components are created with AddComponent<T>(), not new Component(...). MeshFilter owns mesh;
  MeshCollider uses sharedMesh. Verify the exact API before editing instead of assuming this is exhaustive.
- Use discovered Unity tools to author scenes/assets. Do not fabricate scene/prefab YAML, GUIDs,
  .meta files or serialized FileIDs. Preserve existing asset identities.
- Discover `script recompile` and its status tool in the current catalog. Trigger recompilation,
  wait for completion and query console status again. Do not claim compilation succeeded while busy.
- Do not clear the Console to make a failing check pass. Preserve diagnostics. Historical messages
  need timestamps/recompile evidence to classify; they are not automatically current failures.
- When the Editor is already open, use its live test tools if available. Batch test/build helpers
  launch another Editor and can fail with a project lock. Never close the user's Editor without consent.
- For PlayMode checks, discover the current play, background-tick, frame-wait, capture and stop tools.
  Do not assume legacy names such as editor_play or set_autotick exist. Prove frames advance,
  inspect runtime errors, and assert the feature's actual behavior. A screenshot alone is not proof.
- Plan mode is for investigation. If implementation is blocked by the selected mode, tell the user
  to switch to Build; do not claim a proposed change was applied.

## Keep requests small

Use targeted catalog queries and console tails. Keep full logs in Artifacts/, return only relevant
messages and evidence paths. Use a vision-capable model for direct image inspection. Never claim a
text-only model inspected a screenshot. Update PROGRESS.md before handing a long task to a new chat.

## Helper options

`--tail 10` controls returned errors (1..50). `--query "script recompile"` prints matching command
names/descriptions only, without modifying the Editor. Commands may change across Pipeline versions:
if expected tools are absent, inspect the saved catalog and adapt, rather than guessing replacements.
