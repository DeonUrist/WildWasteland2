# Local development reliability

For Unity projects (Packages/manifest.json and ProjectSettings/ProjectVersion.txt), load the
`unity-verification` skill before editing or diagnosing Unity. It contains the console helper path
and the discover -> inspect -> change -> compile -> verify procedure. Do not invent a skill named unity.
Use PowerShell syntax on Windows; use Select-Object -Last instead of Unix tail.
Work on one testable feature at a time. After two failures of the same tool operation, inspect its
actual schema/error and change the approach instead of repeating it. Distinguish connection status,
command success, compilation success, test success, and gameplay verification in reports.
When a task is long, keep a concise PROGRESS.md with objective, constraints, changes, verified results,
remaining failures and next action. Do not copy full logs or tool catalogs into instructions.
Follow the user's chosen model. Custom Ollama aliases need accurate OpenCode context/input/output
limits matching the runtime; an alias name alone does not establish capabilities.
