# OpenCode local game project starter

Reusable Windows bootstrap for **new Unity 6 projects**, OpenCode **v2**, Ollama and Blender.
Copy this entire folder anywhere; it has no dependency on Wild Wasteland or this repository.
It does not modify Apocalypter or its game installation. No commits, pushes or cloud projects are created.

## Model choice for this machine

Hardware inspected on 2026-10-22: RTX 4060 Ti **16 GB VRAM**, about **32 GB system RAM**;
OpenCode **2.1.3**, Unity CLI **1.0.0-beta.14**, Unity **6000.6.6f1** are installed.

There is no demonstrated universal best model for this workflow. These are practical starting profiles,
not the result of a local coding benchmark:

| Profile | Ollama model | Intended use |
| --- | --- | --- |
| `coding` (default) | `qwen3-coder:30b` | Your preferred coding model, reused from Ollama. Approximately 19 GB of weights; expect CPU offload on the 16 GB GPU. Text and tool use. |
| `balanced` | `qwen3.5:9b` | Tool calling and vision, with more memory left for Unity/Blender. |

Use `-Model <local-ollama-tag>` to select another model. An `ollama/` prefix is also accepted.
Installed models are reused without pulling/updating them. Your custom `unity-*` aliases must
already exist locally and are never pulled from a registry. The setup derives a separate context-sized
alias and preserves the source model/alias, including its existing template, system prompt and parameters
except for the derived alias's explicit `num_ctx` setting.

```powershell
# Default: your already-installed qwen3-coder:30b.
.\New-Game.ps1 -ProjectPath 'V:\GameProjects\MyGame' -Open

# Use any of your custom aliases, with or without the OpenCode provider prefix.
.\New-Game.ps1 -ProjectPath 'V:\GameProjects\AnotherGame' -Model 'ollama/unity-heavy:latest'

# Smaller vision-capable model for workflows that inspect images.
.\New-Game.ps1 -ProjectPath 'V:\GameProjects\VisualGame' -Profile balanced
```

Your reported models are `gpt-oss:20b`, `qwen3-coder:30b`, `qwen3.5:9b`, `unity-coder:latest`,
`unity-heavy:latest`, `unity-parallel:latest`, and `unity-reasoner:latest`. They remain available
through OpenCode's local Ollama discovery. Alias names do not by themselves establish capabilities
or enable parallel agents. Qwen3-Coder is text-only: it can operate Blender and Unity tools, but
use a vision-capable model such as Qwen3.5 9B when direct screenshot inspection is required.

For example, `qwen3.5:27b` is a larger
alternative, but its 17–20 GB weights exceed this GPU's VRAM before context and editor memory.
The script checks advertised tool support after downloading. It creates an Ollama alias with
`num_ctx=32768` rather than relying on an implicit context default. OpenCode gets explicit context,
input and output limits, reserving 8192 output tokens at the default size. Compaction keeps 2048
recent tokens and requests a concise handoff. Increase context only after measuring memory use
and responsiveness. On memory pressure, use `ollama ps` to inspect
offload and unload unused models with `ollama stop <name>`.

MCP supplies Unity and Blender capabilities; model reasoning, tool reliability and visual judgment
still determine how well it uses them. This setup does not promise SOL-level performance or arbitrary
mouse/keyboard control. It provides editor automation, C#/Python execution through the bridges,
tests, scene inspection, screenshots, asset operations, builds and player launch.

## First setup

Required: Windows PowerShell 5.1+ or PowerShell 7, Python 3.10+, OpenCode v2, Unity CLI,
and a licensed Unity 6 Editor with Windows build support. Blender, uv and Ollama are also required;
`-InstallTools` installs missing **Blender, uv and Ollama** through winget. It does not upgrade or
install Unity Editors, change licenses, or replace OpenCode. Install a different exact Editor version
in Unity Hub first, then pass `-EditorVersion`.

Official Unity CLI installation if missing (download and inspect the script before running):

```powershell
Invoke-WebRequest https://unity.com/install.ps1 -OutFile "$env:TEMP\unity-cli-install.ps1"
notepad "$env:TEMP\unity-cli-install.ps1"
& "$env:TEMP\unity-cli-install.ps1"
```

Open a new PowerShell after installing tools. Custom installation locations must be on PATH.
Alternatively pass `-BlenderPath 'C:\Soft\Blender\blender.exe'`. A folder or the adjacent
`blender-launcher.exe` path is accepted too; the script resolves it to `blender.exe`.
The `BLENDER_PATH` environment variable and `C:\Soft\Blender\blender.exe` are also checked.
Unity Hub authentication/licensing may require an initial interactive sign-in.

## Create a project

Install the reusable Unity diagnosis/verification workflow for **all OpenCode projects on this
Windows account** once:

```powershell
.\Install-GlobalWorkflow.ps1
```

This adds a short conditional global AGENTS.md section, preserving existing guidance, and the
`unity-verification` skill under `~/.config/opencode/skills/`. Existing skill files are backed up.
Reload OpenCode after installing. The skill includes a tested console helper that uses the live
catalog, reads compilation ground truth and returns bounded errors with full evidence on disk.
It reduces command guessing; it cannot guarantee a local model follows instructions or writes correct code.
Use Build mode for implementation and keep tasks small enough to compile/test after each change.

From the starter folder:

```powershell
# Preview only: no installs, downloads or file writes.
.\New-Game.ps1 -ProjectPath 'V:\GameProjects\MyGame' -Plan

# Create the project, download the local model, configure tools, then open Unity.
.\New-Game.ps1 -ProjectPath 'V:\GameProjects\MyGame' -InstallTools -Open

# Another project, using the larger model and a 2D template.
.\New-Game.ps1 -ProjectPath 'V:\GameProjects\My2DGame' -Template 2d -Profile coding -Open
```

The actual run downloads a Unity template/Pipeline package, the selected model if missing, and a Blender MCP
package. Models are cached globally by Ollama and reused across projects. Blender MCP is resolved
once from PyPI and pinned to that exact release for this project; the addon installer uses the same
release. Pipeline is installed by Unity CLI, and its manifest reference is recorded in the receipt.
The default editor is explicitly 6000.6.6f1, not a moving `latest` alias.

The destination must be new/empty. To resume a failed or completed starter run, supply `-Resume`
with the **same settings**. Existing edited generated files are preserved and reported as conflicts.
There is no automatic rollback/deletion of a partially created project. If Unity itself fails before
the starter receipt exists, inspect that directory and choose a new destination for the retry.
`-SkipModel` supports scaffolding without a model download; resume without it before starting OpenCode.

## Connect Blender and start OpenCode

1. Open Blender. In Preferences → Add-ons, enable **MCP for Blender**. The installer copies the addon;
   enabling it is an interactive first-time action. Restart Blender if the addon is not listed.
2. In the 3D View sidebar (`N`) → MCP for Blender, start its server. Use port **9876** by default.
   For simultaneous projects, pass a different `-BlenderPort` when creating each project and use a matching
   port in each Blender instance. Blender is otherwise shared by every client connecting to its port.
3. Open the Unity project and wait for imports and compilation to finish. The starter automatically
   installs Unity Test Framework through the Editor's Package Manager API if the template lacks it.
   Keep the project's Editor closed during bootstrap/resume; installation logs are saved in
   `.game-agent/test-framework-install.log`. The bootstrap installer stays under `Assets/Editor/`
   and does nothing unless explicitly invoked.
4. Run `Start-Agent.ps1` in the generated project. It checks that the local model exists and launches
   a private OpenCode instance, avoiding shared-server environment surprises.
5. From that project, run `opencode mcp list` or inspect `/mcps` to confirm both bridges connect.

The generated OpenCode v2 config uses `mcp.servers`, not the v1 format. It grants shell, file editing,
Unity and Blender tool execution for the development workflow. Existing global/system OpenCode
permission rules can still apply. Machine-specific config is ignored by Git.

Suggested first prompt:

> Read AGENTS.md. Discover the Unity and Blender tools and confirm this project's connections.
> Run the starter tests, then create and save a minimal playable scene with a camera and one object.
> Enable that scene for building. Enter Play mode, prove frames advance, inspect the Game view and
> console, build Windows, and report the evidence paths. Distinguish wiring tests from gameplay checks.

## Verification commands in a generated project

When switching to a different custom Ollama alias, set matching limits for that model in
`providers.ollama.models.<tag>.limit`. For `unity-coder:latest` with `num_ctx 32768`, use
`{"context":32768,"input":24576,"output":8192}`. Without accurate limits, OpenCode may assume
too much room and Ollama may fill its whole context with input, leaving only a few output tokens.
Existing sessions retain their selected model even when the project default changes.

```powershell
python tools/game.py doctor       # Model availability and live Unity readiness; writes diagnostics
python tools/game.py tests        # Editor CLOSED: EditMode + PlayMode, XML reports, nonempty verdicts
python tools/game.py build        # Editor CLOSED: enabled scenes → Builds/Windows/Game.exe
python tools/game.py play-check   # Editor OPEN: advance frames, capture game, collect console, stop play
python tools/game.py run-player   # Launch the built game; record PID and player log location
```

Evidence is written into uniquely named `Artifacts/` directories. `play-check` uses the currently
documented Pipeline commands and fails if a required result or capture is missing. On a later package
with different schemas, inspect its saved command catalog and update the helper. Review console
errors and screenshots; the helper does not declare gameplay correct from a screenshot or frame tick.
It enables background ticking for its Play session and turns it off afterward. Do not run it during
an existing unsaved Play session. A Windows build needs at least one enabled saved scene.

The two sample tests are connectivity checks. Real verification needs feature-specific tests and
input/behavior checks. Add test assembly references when the game's runtime code moves into asmdefs.

## Bootstrap checks performed

`python -m unittest discover -s tools/opencode-game-starter -v` from this repository, or
`python -m unittest discover -s . -v` from this folder. Covers preview without side effects,
preservation of existing projects, mocked project creation/resume, OpenCode v2 configuration,
explicit model context/tool support, and rejecting missing/empty/failing test reports.

These checks do **not** claim a model download, Unity project compile, live MCP connection,
Blender session or gameplay run was completed. Those require running the bootstrap for a real new project.

## Primary references

The starter now runs its Unity MCP entry through `.game-agent/unity_mcp.py`. This adapter uses
the same Unity CLI, but registers full grouped command names (`console_get`, `scene_list_open`,
`gameobject_create`, `job_batch`) instead of beta.14's duplicate leaf names. It also corrects
Pipeline's batch `params` schema from string to object and validates input before dispatch.
Full CLI command schemas come from the running Editor. It saves oversized tool results under
Artifacts/unity-mcp/ and returns a bounded preview. No global Unity installation or package code is patched.

- [OpenCode v2 models and local discovery](https://opencode.ai/v2/docs/models/)
- [OpenCode v2 MCP configuration](https://opencode.ai/v2/docs/mcp-servers/)
- [OpenCode v2 permissions](https://opencode.ai/v2/docs/permissions/)
- [Qwen3.5 9B Ollama model](https://ollama.com/library/qwen3.5:9b)
- [Qwen3-Coder 30B Ollama model](https://ollama.com/library/qwen3-coder:30b)
- [Devstral Small 2 Ollama model](https://ollama.com/library/devstral-small-2)
- [Mistral's Devstral 2 announcement](https://mistral.ai/news/devstral-2-vibe-cli/)
- [Unity CLI](https://unity.com/)
- [MCP for Blender: addon and OpenCode setup](https://github.com/ahujasid/mcp-for-blender)
