"""Reusable Windows OpenCode v2 / Unity 6 / Blender bootstrap. Python 3.10+, stdlib only."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import time
import urllib.request

HERE = Path(__file__).resolve().parent
PROFILES = {'balanced': 'qwen3.5:9b', 'coding': 'qwen3-coder:30b'}


def normalize_model(model):
    # OpenCode provider IDs are not part of Ollama's native model tag.
    return model.removeprefix('ollama/')


def run(args, **kwargs):
    print('+', subprocess.list2cmdline([str(x) for x in args]), flush=True)
    return subprocess.run([str(x) for x in args], check=True, **kwargs)


def executable(name):
    found = shutil.which(name)
    if found:
        return found
    candidates = {
        'unity': [Path.home() / 'AppData/Local/Unity/bin/unity.exe'],
        'ollama': [Path.home() / 'AppData/Local/Programs/Ollama/ollama.exe'],
        'uvx': [Path.home() / '.local/bin/uvx.exe', Path.home() / '.cargo/bin/uvx.exe'],
        'blender': [Path(os.environ.get('BLENDER_PATH', 'C:/Soft/Blender/blender.exe'))] +
                   sorted((Path(os.environ.get('ProgramFiles', 'C:/Program Files')) /
                           'Blender Foundation').glob('Blender */blender.exe'), reverse=True),
    }
    return next((str(p) for p in candidates.get(name, []) if p.is_file()), None)


def dependency(name, winget_id=None, install=False):
    found = executable(name)
    if not found and install and winget_id:
        run(['winget', 'install', '--id', winget_id, '--exact', '--source', 'winget',
             '--accept-source-agreements', '--accept-package-agreements', '--silent'])
        # Refresh PATH for packages installed in this process (uv's executable is a sibling of uvx).
        if os.name == 'nt':
            import winreg
            paths = []
            for hive, key in [(winreg.HKEY_LOCAL_MACHINE, r'SYSTEM\CurrentControlSet\Control\Session Manager\Environment'),
                              (winreg.HKEY_CURRENT_USER, 'Environment')]:
                with winreg.OpenKey(hive, key) as registry:
                    paths.append(os.path.expandvars(winreg.QueryValueEx(registry, 'Path')[0]))
            os.environ['PATH'] += os.pathsep + os.pathsep.join(paths)
        found = executable(name)
    if not found:
        raise RuntimeError(f'Missing {name}. Install it and reopen PowerShell, or use -InstallTools where supported.')
    return found


def write_new(path, content):
    path = Path(path)
    if path.exists():
        if path.read_text(encoding='utf-8-sig') == content:
            return
        raise RuntimeError(f'Preserving existing file; contents differ: {path}')
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding='utf-8', newline='\n')


def config(project, alias, unity, uvx, blender_version, port, context=32768, capabilities=None):
    inputs = ['text', 'image'] if capabilities and 'vision' in capabilities else ['text']
    output = min(8192, context // 4)
    return {
        '$schema': 'https://opencode.ai/config.json',
        'model': f'ollama/{alias}',
        'providers': {'ollama': {'models': {alias: {
            'limit': {'context': context, 'input': context - output, 'output': output},
            'capabilities': {'tools': True, 'input': inputs, 'output': ['text']},
        }}}},
        'compaction': {'auto': True, 'keep': {'tokens': 2048}, 'buffer': min(12288, context // 2)},
        'agents': {'compaction': {'system':
            'Write a concise handoff summary of the conversation in at most 600 words. '
            'Preserve the objective, explicit constraints, key decisions, changed files, '
            'actual test results, unresolved problems and next action. '
            'Do not copy code, full logs, tool catalogs, or repeat earlier summaries. '
            'Distinguish observed results from assumptions. Return the summary immediately.'}},
        'mcp': {'servers': {
            'unity': {'type': 'local', 'command': [sys.executable, str(project / '.game-agent/unity_mcp.py'),
                                                '--unity', unity, '--project', str(project), '--compact-tools'],
                      'codemode': False},
            'blender': {'type': 'local', 'command': [uvx, f'mcp-for-blender=={blender_version}'],
                        'environment': {'BLENDER_HOST': '127.0.0.1', 'BLENDER_PORT': str(port)}},
        }},
        'permissions': [
            {'action': a, 'resource': '*', 'effect': 'allow'}
            for a in ['read', 'edit', 'glob', 'grep', 'shell', 'execute', 'unity_*', 'blender_*']
        ],
    }


def api(path, data=None, timeout=10):
    body = None if data is None else json.dumps(data).encode()
    request = urllib.request.Request('http://127.0.0.1:11434' + path, data=body,
                                     headers={'Content-Type': 'application/json'})
    with urllib.request.urlopen(request, timeout=timeout) as response:
        return json.load(response)


def prepare_model(ollama, model, context, directory):
    try:
        inventory = api('/api/tags')
    except (OSError, ValueError):
        log = open(directory / 'ollama-server.log', 'ab')
        flags = getattr(subprocess, 'CREATE_NO_WINDOW', 0)
        subprocess.Popen([ollama, 'serve'], stdout=log, stderr=log, creationflags=flags)
        log.close()
        for _ in range(60):
            try:
                inventory = api('/api/tags')
                break
            except (OSError, ValueError):
                time.sleep(1)
        else:
            raise RuntimeError('Ollama did not start; inspect .game-agent/ollama-server.log')
    names = {m['name'] for m in inventory.get('models', [])}
    if model not in names and (model + ':latest') not in names:
        if model.startswith('unity-'):
            raise RuntimeError(f'Custom local alias {model} is missing. Create it in Ollama first; it will not be pulled from a registry.')
        run([ollama, 'pull', model])
    else:
        print(f'Reusing installed model: {model}')
    metadata = api('/api/show', {'model': model})
    if 'tools' not in metadata.get('capabilities', []):
        raise RuntimeError(f'{model} does not advertise tool support; choose a tool-capable local model.')
    alias = 'game-coder-' + hashlib.sha256(f'{model}:{context}'.encode()).hexdigest()[:12]
    modelfile = directory / 'Modelfile'
    modelfile.write_text(f'FROM {model}\nPARAMETER num_ctx {context}\n', encoding='utf-8')
    run([ollama, 'create', alias, '-f', str(modelfile)])
    return alias, metadata


def ensure_test_framework(project, editor):
    manifest_path = project / 'Packages/manifest.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8-sig'))
    if 'com.unity.test-framework' in manifest.get('dependencies', {}):
        return manifest
    print('Installing required package: com.unity.test-framework', flush=True)
    installer = project / 'Assets/Editor/GameStarterBootstrap/PackageInstaller.cs'
    write_new(installer, (HERE / 'PackageInstaller.cs').read_text(encoding='utf-8'))
    log = project / '.game-agent/test-framework-install.log'
    # UPM is asynchronous: no -quit, so EditorApplication.update can finish resolution.
    run([editor, '-batchmode', '-projectPath', str(project), '-executeMethod',
         'GameStarterBootstrap.PackageInstaller.Install', '-logFile', str(log)], timeout=900)
    manifest = json.loads(manifest_path.read_text(encoding='utf-8-sig'))
    if 'com.unity.test-framework' not in manifest.get('dependencies', {}):
        raise RuntimeError(f'Test Framework installation did not finish. Inspect {log}, close the project Editor, then resume.')
    return manifest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', required=True)
    parser.add_argument('--editor-version', default='6000.6.6f1')
    parser.add_argument('--template', choices=['2d', '3d'], default='3d')
    parser.add_argument('--profile', choices=PROFILES, default='coding')
    parser.add_argument('--model')
    parser.add_argument('--blender-path', help='Path to blender.exe (or its installation folder).')
    parser.add_argument('--context', type=int, default=32768)
    parser.add_argument('--blender-port', type=int, default=9876)
    for flag in ['install-tools', 'skip-model', 'resume', 'plan', 'open']:
        parser.add_argument('--' + flag, action='store_true')
    args = parser.parse_args()
    project = Path(args.project).expanduser().resolve()
    model = normalize_model(args.model or PROFILES[args.profile])
    if not re.fullmatch(r'[A-Za-z0-9_.:/-]+', model) or 'cloud' in model.lower():
        parser.error('Use a local Ollama model tag, without spaces or cloud suffixes.')
    if not re.fullmatch(r'6000\.\d+\.\d+[abfp]\d+', args.editor_version):
        parser.error('Supply an exact Unity 6 version, for example 6000.6.6f1.')
    if not 4096 <= args.context <= 131072 or not 1024 <= args.blender_port <= 65535:
        parser.error('Context must be 4096..131072 and Blender port 1024..65535.')
    if not project.name or project == project.parent or any(c in project.name for c in '<>:"|?*'):
        parser.error('Supply a valid project folder name.')
    settings = {'project': str(project), 'model': model, 'context': args.context,
                'editor_version': args.editor_version, 'template': args.template,
                'blender_port': args.blender_port}
    print(json.dumps(settings, indent=2))
    print('Plan: check dependencies; create Unity project; install Pipeline; reuse local model or download if missing;'
          ' install Blender addon; write OpenCode v2 config, tests and verification helpers.')
    if args.plan:
        return
    if args.blender_path:
        blender_path = Path(args.blender_path).expanduser().resolve()
        if blender_path.is_dir():
            blender_path /= 'blender.exe'
        if blender_path.name.lower() == 'blender-launcher.exe':
            blender_path = blender_path.with_name('blender.exe')
        if not blender_path.is_file() or blender_path.name.lower() != 'blender.exe':
            raise RuntimeError(f'Blender executable not found: {blender_path}. Supply -BlenderPath pointing to blender.exe.')
        os.environ['BLENDER_PATH'] = str(blender_path)
        os.environ['PATH'] = str(blender_path.parent) + os.pathsep + os.environ.get('PATH', '')
    marker = project / '.game-agent/bootstrap.json'
    resumed = marker.exists()
    if project.exists() and any(project.iterdir()):
        if not args.resume or not resumed:
            raise RuntimeError('Destination is not empty. Choose a new folder, or -Resume a project created by this tool.')
        old = json.loads(marker.read_text())
        if old['settings'] != settings:
            raise RuntimeError('Resume settings differ. Use the original options; edit generated config separately.')
    unity = dependency('unity')
    opencode = dependency('opencode')
    version = run([opencode, '--version'], capture_output=True, text=True).stdout
    if not re.search(r'\bv?2\.', version):
        raise RuntimeError(f'This starter targets OpenCode v2. Installed: {version.strip()}')
    uvx = dependency('uvx', 'astral-sh.uv', args.install_tools)
    blender = dependency('blender', 'BlenderFoundation.Blender', args.install_tools)
    # Make custom installations visible to the addon installer subprocess too.
    os.environ['PATH'] = str(Path(blender).parent) + os.pathsep + os.environ.get('PATH', '')
    ollama = dependency('ollama', 'Ollama.Ollama', args.install_tools) if not args.skip_model else None
    run([unity, 'self-update', '--check', '--format', 'json'])
    editors = json.loads(run([unity, 'editors', '--installed', '--format', 'json'], capture_output=True, text=True).stdout)
    if args.editor_version not in [e['version'] for e in editors.get('data', [])]:
        raise RuntimeError(f'Install Unity {args.editor_version} in Unity Hub first, including Windows build support and an active license.')
    if not resumed:
        project.parent.mkdir(parents=True, exist_ok=True)
        run([unity, 'projects', 'create', project.name, '--path', str(project.parent),
             '--editor-version', args.editor_version, '--template', args.template,
             '--bare', '--no-cloud', '--non-interactive'])
        marker.parent.mkdir(parents=True, exist_ok=True)
        marker.write_text(json.dumps({'settings': settings, 'complete': False}, indent=2))
    if not (project / 'ProjectSettings/ProjectVersion.txt').exists():
        raise RuntimeError('Unity project creation did not produce ProjectVersion.txt.')
    run([unity, 'pipeline', 'install', '--project-path', str(project), '--non-interactive'])
    editor = next(e['location'] for e in editors['data'] if e['version'] == args.editor_version)
    manifest = ensure_test_framework(project, editor)
    # Resolve once, then pin the server and installer to the SAME release.
    previous = json.loads(marker.read_text())
    blender_version = previous.get('blender_mcp_version')
    if not blender_version:
        request = urllib.request.Request('https://pypi.org/pypi/mcp-for-blender/json', headers={'User-Agent': 'GameProjectStarter/1.0'})
        with urllib.request.urlopen(request, timeout=30) as response:
            blender_version = json.load(response)['info']['version']
        previous['blender_mcp_version'] = blender_version
        marker.write_text(json.dumps(previous, indent=2))
    alias = 'game-coder-' + hashlib.sha256(f'{model}:{args.context}'.encode()).hexdigest()[:12]
    metadata = {}
    if not args.skip_model:
        alias, metadata = prepare_model(ollama, model, args.context, marker.parent)
    run([uvx, f'mcp-for-blender=={blender_version}', 'install-addon'])
    for source in (HERE / 'template').rglob('*'):
        if source.is_file() and '__pycache__' not in source.parts:
            target = project / source.relative_to(HERE / 'template')
            content = source.read_text(encoding='utf-8')
            if source.name == '.gitignore' and target.exists():
                existing = target.read_text(encoding='utf-8-sig')
                if content not in existing:
                    target.write_text(existing.rstrip() + '\n\n' + content, encoding='utf-8')
            else:
                write_new(target, content)
    write_new(project / '.game-agent/unity_mcp.py',
              (HERE / 'global/skills/unity-verification/scripts/unity_mcp.py').read_text(encoding='utf-8'))
    write_new(project / 'opencode.json', json.dumps(config(project, alias, unity, uvx, blender_version,
              args.blender_port, args.context, metadata.get('capabilities')), indent=2) + '\n')
    # The launcher keeps model discovery on a private OpenCode process with Ollama's loopback URL.
    write_new(project / 'Start-Agent.ps1',
              "$ErrorActionPreference = 'Stop'\nSet-Location -LiteralPath $PSScriptRoot\n"
              "$local = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '.game-agent/local.json') | ConvertFrom-Json\n"
              "$models = Invoke-RestMethod 'http://127.0.0.1:11434/api/tags'\n"
              "if (-not ($models.models | Where-Object { ($_.name -split ':')[0] -eq $local.model })) { throw 'Local model missing. Resume the bootstrap without -SkipModel.' }\n"
              "$env:OPENCODE_DISABLE_FREE_MODELS = '1'\n& opencode --standalone $PSScriptRoot\nexit $LASTEXITCODE\n")
    write_new(project / '.game-agent/local.json', json.dumps({'unity': unity, 'model': alias}, indent=2) + '\n')
    previous.update(complete=True, model_prepared=not args.skip_model,
                    model_capabilities=metadata.get('capabilities', []),
                    pipeline=manifest.get('dependencies', {}).get('com.unity.pipeline'))
    marker.write_text(json.dumps(previous, indent=2))
    print(f'Created {project}\nOpen Blender, enable MCP for Blender and start its server on port {args.blender_port}.')
    print('Run Start-Agent.ps1. Ask the agent to discover Unity/Blender tools, then verify tests and Play mode.')
    if args.skip_model:
        print('Model download was skipped. Rerun with -Resume without -SkipModel before starting the agent.')
    if args.open:
        run([unity, 'open', str(project)])


if __name__ == '__main__':
    try:
        main()
    except (RuntimeError, OSError, ValueError, subprocess.CalledProcessError, subprocess.TimeoutExpired) as error:
        print(f'ERROR: {error}', file=sys.stderr)
        sys.exit(1)
