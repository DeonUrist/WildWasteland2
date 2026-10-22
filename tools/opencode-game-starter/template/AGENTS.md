# Game development workflow

Use the local Ollama model selected in opencode.json. Do not silently switch to a hosted model.
Read .game-agent/bootstrap.json for the editor version and local tool configuration.
You may edit this project, run its tests/builds, enter Play mode, and create/export assets in Blender.
Use Unity MCP for scene/object/asset operations. Discover the current tools before calling them.
If the global unity-verification skill is installed, load it for Unity diagnosis and verification.
Use the installed command catalog: this Pipeline uses grouped commands such as console get/status
and script recompile, rather than assuming legacy underscore names exist. Never fabricate scene
YAML, .meta GUIDs or FileIDs; use Editor APIs for serialized assets.
Use Blender MCP for modeling, materials, rendering and exports. Save source .blend files under Art/
and export game assets under Assets/Art/. Inspect objects, dimensions, topology/materials and a render.
An MCP connection gives tool access; it does not guarantee the model's reasoning or visual accuracy.

## Verification

- Run `python tools/game.py doctor` first. Report missing tools/connectivity accurately.
- Run `python tools/game.py tests` with the Editor closed. This uses the project's exact Unity version.
  If the Editor is open, use its discovered live test tools instead of closing unsaved work.
- Tests must finish, include at least one executed test, and contain no failed tests. Keep XML reports.
  Starter tests verify wiring only. Add feature-specific behavior assertions for actual game changes.
- Run `python tools/game.py build` with the Editor closed to build enabled scenes for Windows.
  An empty enabled scene list fails. Author and save a game scene and enable it in Build Settings first.
- For interactive testing, open this exact project with Unity CLI, discover its live commands, then
  use `python tools/game.py play-check`. This enters Play mode and saves frame-advance evidence,
  a Game view screenshot and console output; it stops Play mode in cleanup.
- Inspect the screenshot AND console and exercise the feature's actual input/behavior. Frames advancing
  proves the loop is running, not that gameplay is correct. Add a repeatable PlayMode test for critical behavior.
- Never say verified when a command only started, a result is missing, or no behavior was asserted.
- Store evidence in Artifacts/. Do not commit Library/, Temp/, Builds/, or machine-local agent settings.

Use project-scoped Unity commands, e.g. `unity command <tool> --project-path <absolute project path>`.
Discover schemas with `unity command --format json` first; Pipeline tool names can change by version.
For every direct `unity command` invocation include `--caller plugin --skill game-project-workflow`.
Only commit, push, publish, or change other projects when the user requests it.
