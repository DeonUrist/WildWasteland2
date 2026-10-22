"""Bounded read-only Unity diagnostics with full local evidence. Python stdlib only."""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time


def invoke(unity, project, words, evidence, filename):
    split = words.index('--') if '--' in words else len(words)
    args = [unity, 'command', *words[:split], '--project-path', str(project),
            '--caller', 'plugin', '--skill', 'unity-verification', '--format', 'json', *words[split:]]
    result = subprocess.run(args, text=True, capture_output=True, timeout=60)
    (evidence / filename).write_text(result.stdout + '\n' + result.stderr, encoding='utf-8')
    if result.returncode:
        raise RuntimeError(f'Unity command failed. See {evidence / filename}')
    payload = json.loads(result.stdout)
    if payload.get('success') is not True:
        raise RuntimeError(f"Unity returned failure: {payload.get('errors')}; evidence {evidence / filename}")
    return payload.get('data', {})


def bounded_entries(entries):
    return [{k: (str(e.get(k, ''))[:1200] if k == 'message' else e.get(k))
             for k in ['seq', 'timestampUtc', 'logType', 'message']} for e in entries]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', required=True)
    parser.add_argument('--tail', type=int, default=10)
    parser.add_argument('--query')
    args = parser.parse_args()
    project = Path(args.project).expanduser().resolve()
    if not (project / 'ProjectSettings/ProjectVersion.txt').is_file():
        parser.error('Project must be a Unity project root with ProjectVersion.txt.')
    if not 1 <= args.tail <= 50:
        parser.error('--tail must be 1..50')
    unity = shutil.which('unity') or str(Path.home() / 'AppData/Local/Unity/bin/unity.exe')
    evidence = project / 'Artifacts' / ('unity-diagnostics-' + str(time.time_ns()))
    evidence.mkdir(parents=True)
    catalog = invoke(unity, project, ['--query', args.query or 'console'], evidence, 'catalog.json')
    commands = catalog.get('commands', [])
    if args.query:
        print(json.dumps({'commands': [{'command': c['command'], 'description': c.get('description')}
                                        for c in commands], 'evidence': str(evidence)}, indent=2))
        return 0 if commands else 2
    names = {c['command'] for c in commands}
    if not {'console get', 'console status'}.issubset(names):
        raise RuntimeError(f'Expected console get/status absent. Inspect current catalog: {evidence / "catalog.json"}')
    status = invoke(unity, project, ['console', 'status'], evidence, 'status.json').get('result', {})
    console = invoke(unity, project, ['console', 'get', '--', '--tail', str(args.tail), '--level', 'error'],
                     evidence, 'errors.json').get('result', {})
    ground = status.get('groundTruth', {})
    print(json.dumps({'groundTruth': ground, 'bufferCounts': status.get('counts'),
                      'recentErrors': bounded_entries(console.get('entries', [])),
                      'evidence': str(evidence)}, indent=2))
    if not ground or ground.get('compilationFailed') is not False or ground.get('compiling') is not False:
        return 2
    # Nonzero console count can include old failures: preserve it for the agent to compare with a fresh compile.
    return 1 if ground.get('consoleErrors', 0) else 0


if __name__ == '__main__':
    try:
        sys.exit(main())
    except (RuntimeError, OSError, ValueError, subprocess.TimeoutExpired) as error:
        print(f'ERROR: {error}', file=sys.stderr)
        sys.exit(2)
