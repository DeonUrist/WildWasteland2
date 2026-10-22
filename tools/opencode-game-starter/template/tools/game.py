"""Project-scoped verification; no external Python packages required."""
import argparse
import json
from pathlib import Path
import shutil
import socket
import subprocess
import sys
import time
import urllib.request
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]


def check_report(path):
    root = ET.parse(path).getroot()
    cases = list(root.iter('test-case'))
    if not cases:
        raise RuntimeError(f'No test cases in {path}')
    passed = sum(c.get('result') in ('Passed', 'Success') for c in cases)
    failed = [c.get('fullname', c.get('name')) for c in cases
              if c.get('result') not in ('Passed', 'Success', 'Skipped', 'Ignored', 'Inconclusive')]
    if failed or passed == 0 or root.get('result') in ('Failed', 'Failure'):
        raise RuntimeError(f'Test run failed or executed no passing tests: {failed}; {path}')
    return {'passed': passed, 'total': len(cases), 'report': str(path)}


def invoke(command, output=None, timeout=900):
    result = subprocess.run([str(x) for x in command], cwd=ROOT, text=True,
                            capture_output=True, timeout=timeout)
    if output:
        output.write_text(result.stdout + '\n' + result.stderr, encoding='utf-8')
    if result.returncode:
        raise RuntimeError(f'Command failed ({result.returncode}): {command}\n{result.stdout}\n{result.stderr}')
    return result.stdout


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=['doctor', 'tests', 'build', 'play-check', 'run-player'])
    args = parser.parse_args()
    local = json.loads((ROOT / '.game-agent/local.json').read_text())
    unity = local['unity']
    evidence = ROOT / 'Artifacts' / (time.strftime('%Y%m%d-%H%M%S') + '-' + str(time.time_ns()))
    evidence.mkdir(parents=True)
    command = [unity, 'command', '--project-path', str(ROOT), '--caller', 'plugin', '--skill', 'game-project-workflow']
    if args.action == 'doctor':
        results = {}
        for name, cmd in [('version', [unity, '--version']),
                          ('unity', [unity, 'status', '--project-path', str(ROOT), '--format', 'json'])]:
            try:
                results[name] = invoke(cmd, evidence / (name + '.txt'), timeout=30)
            except (RuntimeError, subprocess.TimeoutExpired) as error:
                results[name] = {'error': str(error)}
        try:
            with urllib.request.urlopen('http://127.0.0.1:11434/api/tags', timeout=5) as response:
                models = json.load(response)['models']
            results['model_available'] = any(m['name'].split(':')[0] == local['model'] for m in models)
        except OSError as error:
            results['ollama'] = {'error': str(error)}
        settings = json.loads((ROOT / '.game-agent/bootstrap.json').read_text())['settings']
        try:
            with socket.create_connection(('127.0.0.1', settings['blender_port']), timeout=5):
                results['blender_tcp_listening'] = True
        except OSError as error:
            results['blender'] = {'error': str(error)}
        results['uvx'] = shutil.which('uvx')
        results['next'] = 'Run opencode mcp list from this project to inspect Unity and Blender connections.'
        (evidence / 'doctor.json').write_text(json.dumps(results, indent=2))
        print(json.dumps(results, indent=2))
        if any(isinstance(v, dict) and 'error' in v for v in results.values()) or not results.get('model_available'):
            raise RuntimeError('Doctor found missing model or disconnected Unity. See evidence above.')
    elif args.action == 'tests':
        summary = []
        for mode in ('EditMode', 'PlayMode'):
            report = evidence / (mode + '.xml')
            invoke([unity, 'test', str(ROOT), '--mode', mode, '--output', str(report),
                    '--report-format', 'nunit', '--timeout', '600'], evidence / (mode + '.log'))
            summary.append(check_report(report))
        (evidence / 'tests.json').write_text(json.dumps(summary, indent=2))
        print(json.dumps(summary, indent=2))
    elif args.action == 'build':
        invoke([unity, 'build', str(ROOT), '--target', 'StandaloneWindows64',
                '--execute-method', 'AgentBuild.Windows'], evidence / 'build.log', timeout=1800)
        if not (ROOT / 'Builds/Windows/Game.exe').is_file():
            raise RuntimeError('Build command returned without the expected executable.')
        print(ROOT / 'Builds/Windows/Game.exe')
    elif args.action == 'run-player':
        player = ROOT / 'Builds/Windows/Game.exe'
        if not player.is_file():
            raise RuntimeError('Build the Windows player first: python tools/game.py build')
        process = subprocess.Popen([str(player), '-logFile', str(evidence / 'player.log')], cwd=player.parent)
        (evidence / 'player.json').write_text(json.dumps({'pid': process.pid, 'executable': str(player)}, indent=2))
        print(f'Player started (PID {process.pid}). Startup is not a gameplay verification verdict.')
    else:
        invoke(command + ['--format', 'json'], evidence / 'catalog.json', timeout=30)
        invoke(command + ['set_autotick'], evidence / 'autotick.json', timeout=30)
        try:
            invoke(command + ['editor_play'], evidence / 'play.json', timeout=60)
            invoke([unity, 'status', '--until-ready', '--project-path', str(ROOT), '--format', 'json'],
                   evidence / 'ready.json', timeout=60)
            raw = invoke(command + ['wait_for', '--format', 'json', '--', '--condition',
                         json.dumps({'member': 'UnityEngine.Time.frameCount', 'op': 'changed'}),
                         '--timeout_s', '10'], evidence / 'frames.json', timeout=30)
            # Fail closed if the current Pipeline response shape differs from the documented one.
            result = json.loads(raw)
            def met(value):
                if isinstance(value, dict):
                    if value.get('met') is True and value.get('timedOut') is not True:
                        return True
                    return any(met(v) for v in value.values())
                if isinstance(value, list):
                    return any(met(v) for v in value)
                return False
            if not met(result):
                raise RuntimeError('No positive frame-advance verdict; inspect frames.json and the current wait_for schema.')
            capture = evidence / 'game.png'
            invoke(command + ['capture_game_view', '--', '--source', 'screen', '--save_path',
                              capture.relative_to(ROOT).as_posix()], evidence / 'capture.json', timeout=30)
            if not capture.is_file():
                raise RuntimeError('Game view capture did not produce the requested image.')
            # Discover the exact console query schema from catalog.json if this tool changes.
            invoke(command + ['console', '--', '--tail', '50', '--level', 'error'], evidence / 'console.json', timeout=30)
            print('Player loop advanced. Inspect game.png and console.json; feature behavior still needs assertions.')
        finally:
            invoke(command + ['editor_stop'], evidence / 'stop.json', timeout=60)
            invoke(command + ['set_autotick', '--', '--enable', 'false'], evidence / 'autotick-stop.json', timeout=30)
    print(f'Evidence: {evidence}')


if __name__ == '__main__':
    try:
        main()
    except (RuntimeError, OSError, ValueError, ET.ParseError, subprocess.TimeoutExpired) as error:
        print(f'ERROR: {error}', file=sys.stderr)
        sys.exit(1)
