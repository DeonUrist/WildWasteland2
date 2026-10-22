"""Run with python -m unittest discover -s tools/opencode-game-starter -v.
External installers, model downloads and Unity execution are mocked deliberately.
"""
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

HERE = Path(__file__).resolve().parent


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


setup = module('starter_setup', HERE / 'setup.py')
game = module('starter_game', HERE / 'template/tools/game.py')
unity_check = module('unity_check', HERE / 'global/skills/unity-verification/scripts/unity_check.py')
unity_mcp = module('unity_mcp', HERE / 'global/skills/unity-verification/scripts/unity_mcp.py')


class StarterTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='game-starter-')
        self.addCleanup(self.temp.cleanup)
        self.project = Path(self.temp.name) / 'Game With Spaces'

    def execute(self, *options):
        with patch.object(sys, 'argv', ['setup.py', '--project', str(self.project), *options]), contextlib.redirect_stdout(io.StringIO()):
            setup.main()

    def test_plan_never_creates_project_or_runs_commands(self):
        with patch.object(setup, 'run') as run:
            self.execute('--plan')
        run.assert_not_called()
        self.assertFalse(self.project.exists())

    def test_nonempty_unrelated_destination_is_preserved(self):
        self.project.mkdir()
        sentinel = self.project / 'existing.txt'
        sentinel.write_text('keep')
        with self.assertRaisesRegex(RuntimeError, 'not empty'):
            self.execute()
        self.assertEqual('keep', sentinel.read_text())

    def test_scaffold_resume_and_configuration(self):
        calls = []
        def fake_run(args, **kwargs):
            calls.append(args)
            if 'create' in args:
                (self.project / 'ProjectSettings').mkdir(parents=True)
                (self.project / 'ProjectSettings/ProjectVersion.txt').write_text('m_EditorVersion: 6000.6.6f1')
                (self.project / 'Packages').mkdir()
                (self.project / 'Packages/manifest.json').write_text(json.dumps({'dependencies': {'com.unity.test-framework': '1.6.0'}}))
                (self.project / '.gitignore').write_text('custom-rule\n')
            if 'editors' in args:
                output = json.dumps({'data': [{'version': '6000.6.6f1', 'location': 'Unity.exe'}]})
            else:
                output = 'opencode v2.1.3'
            return subprocess.CompletedProcess(args, 0, output, '')
        class Response(io.BytesIO):
            pass
        with patch.object(setup, 'dependency', side_effect=lambda name, *a: name), \
             patch.object(setup, 'run', side_effect=fake_run), \
             patch.object(setup.urllib.request, 'urlopen', return_value=Response(b'{"info":{"version":"1.2.3"}}')):
            self.execute('--skip-model')
            self.execute('--skip-model', '--resume')
        config = json.loads((self.project / 'opencode.json').read_text())
        self.assertIn('servers', config['mcp'])
        unity_command = config['mcp']['servers']['unity']['command']
        self.assertEqual(str(self.project), unity_command[unity_command.index('--project')+1])
        self.assertFalse(config['mcp']['servers']['unity']['codemode'])
        self.assertEqual('mcp-for-blender==1.2.3', config['mcp']['servers']['blender']['command'][1])
        self.assertTrue(config['model'].startswith('ollama/game-coder-'))
        alias = config['model'].removeprefix('ollama/')
        self.assertEqual(32768, config['providers']['ollama']['models'][alias]['limit']['context'])
        self.assertEqual(8192, config['providers']['ollama']['models'][alias]['limit']['output'])
        self.assertEqual(24576, config['providers']['ollama']['models'][alias]['limit']['input'])
        self.assertIn('600 words', config['agents']['compaction']['system'])
        self.assertIn('custom-rule', (self.project / '.gitignore').read_text())
        self.assertEqual(1, sum('create' in call for call in calls))
        create = next(call for call in calls if 'create' in call)
        self.assertNotIn('--no-initial-commit', create)
        self.assertNotIn('--vcs', create)
        self.assertTrue((self.project / 'Assets/Tests/PlayMode/StarterPlayTests.cs').exists())

    def test_report_rejects_empty_failed_and_missing(self):
        report = Path(self.temp.name) / 'result.xml'
        for xml in ['<test-run/>', '<test-run><test-case result="Failed"/></test-run>',
                    '<test-run><test-case result="Skipped"/></test-run>']:
            report.write_text(xml)
            with self.assertRaises(RuntimeError):
                game.check_report(report)
        with self.assertRaises(OSError):
            game.check_report(Path(self.temp.name) / 'missing.xml')
        report.write_text('<test-run result="Passed"><test-case result="Passed"/></test-run>')
        self.assertEqual(1, game.check_report(report)['passed'])

    def test_model_context_and_tool_capability(self):
        with patch.object(setup, 'api', side_effect=[{}, {'capabilities': ['tools', 'vision']}]), \
             patch.object(setup, 'run') as run:
            alias, metadata = setup.prepare_model('ollama', 'qwen3.5:9b', 16384, Path(self.temp.name))
        self.assertIn('PARAMETER num_ctx 16384', (Path(self.temp.name) / 'Modelfile').read_text())
        self.assertEqual('create', run.call_args.args[0][1])
        self.assertTrue(alias.startswith('game-coder-'))
        with patch.object(setup, 'api', side_effect=[{}, {'capabilities': ['completion']}]), patch.object(setup, 'run'):
            with self.assertRaisesRegex(RuntimeError, 'tool support'):
                setup.prepare_model('ollama', 'bad:1b', 16384, Path(self.temp.name))

    def test_existing_alias_reused_without_registry_pull(self):
        self.assertEqual('qwen3-coder:30b', setup.PROFILES['coding'])
        self.assertEqual('unity-coder:latest', setup.normalize_model('ollama/unity-coder:latest'))
        with patch.object(setup, 'api', side_effect=[{'models': [{'name': 'unity-coder:latest'}]},
                                                   {'capabilities': ['tools']}]), patch.object(setup, 'run') as run:
            setup.prepare_model('ollama', 'unity-coder:latest', 16384, Path(self.temp.name))
        self.assertFalse(any('pull' in call.args[0] for call in run.call_args_list))
        with patch.object(setup, 'api', return_value={'models': []}), patch.object(setup, 'run') as run:
            with self.assertRaisesRegex(RuntimeError, 'Custom local alias'):
                setup.prepare_model('ollama', 'unity-heavy:latest', 16384, Path(self.temp.name))
        run.assert_not_called()

    def test_blender_custom_environment_path(self):
        blender = Path(self.temp.name) / 'Blender' / 'blender.exe'
        blender.parent.mkdir()
        blender.touch()
        with patch.object(setup.shutil, 'which', return_value=None), \
             patch.dict(setup.os.environ, {'BLENDER_PATH': str(blender)}):
            self.assertEqual(str(blender), setup.executable('blender'))

    def test_missing_test_framework_installs_through_editor(self):
        manifest = self.project / 'Packages/manifest.json'
        manifest.parent.mkdir(parents=True)
        manifest.write_text('{"dependencies": {}}')
        def install(args, **kwargs):
            self.assertNotIn('-quit', args)
            self.assertIn('GameStarterBootstrap.PackageInstaller.Install', args)
            manifest.write_text('{"dependencies": {"com.unity.test-framework": "1.6.0"}}')
        with patch.object(setup, 'run', side_effect=install) as run:
            result = setup.ensure_test_framework(self.project, 'Unity.exe')
        self.assertIn('com.unity.test-framework', result['dependencies'])
        self.assertEqual(1, run.call_count)
        with patch.object(setup, 'run') as run:
            setup.ensure_test_framework(self.project, 'Unity.exe')
        run.assert_not_called()

    def test_package_install_requires_manifest_confirmation(self):
        manifest = self.project / 'Packages/manifest.json'
        manifest.parent.mkdir(parents=True)
        manifest.write_text('{"dependencies": {}}')
        with patch.object(setup, 'run'), self.assertRaisesRegex(RuntimeError, 'did not finish'):
            setup.ensure_test_framework(self.project, 'Unity.exe')

    def test_console_helper_places_cli_options_before_parameter_fence(self):
        result = subprocess.CompletedProcess([], 0, '{"success":true,"data":{"result":{}}}', '')
        with patch.object(unity_check.subprocess, 'run', return_value=result) as run:
            unity_check.invoke('unity', self.project, ['console', 'get', '--', '--tail', '3'],
                               Path(self.temp.name), 'console.json')
        command = run.call_args.args[0]
        self.assertLess(command.index('--project-path'), command.index('--'))
        self.assertEqual(['--tail', '3'], command[command.index('--')+1:])

    def test_console_helper_rejects_server_failure_and_bounds_output(self):
        result = subprocess.CompletedProcess([], 0, '{"success":false,"errors":[{"code":"NO_EDITOR"}]}', '')
        with patch.object(unity_check.subprocess, 'run', return_value=result):
            with self.assertRaisesRegex(RuntimeError, 'Unity returned failure'):
                unity_check.invoke('unity', self.project, ['console', 'status'], Path(self.temp.name), 'error.json')
        entry = unity_check.bounded_entries([{'message':'x'*2000,'stackTrace':'y'*50000}])[0]
        self.assertEqual(1200, len(entry['message']))
        self.assertNotIn('stackTrace', entry)

    def test_mcp_preserves_command_groups_and_dispatches_full_command(self):
        commands = [{'command': name, 'name': 'get', 'schema': {'type': 'object', 'properties': {}}}
                    for name in ['console get', 'settings.audio get']]
        response = subprocess.CompletedProcess([], 0, json.dumps({'success':True,'data':{'commands':commands}}), '')
        bridge = unity_mcp.Bridge('unity', self.project)
        with patch.object(unity_mcp.subprocess, 'run', return_value=response):
            names = [t['name'] for t in bridge.list_tools()['tools']]
        self.assertEqual(['console_get','settings_audio_get'], names)
        with patch.object(bridge, 'execute', return_value={'data':{}}) as execute:
            bridge.call('console_get', {})
        execute.assert_called_once_with('console get', {})

    def test_mcp_batch_schema_corrects_object_and_rejects_string(self):
        original = {'type':'object','properties':{'operations':{'type':'array','items':{
            'type':'object','properties':{'command':{'type':'string'},'params':{'type':'string'}}}}}}
        schema = unity_mcp.corrected_schema('job batch',original)
        unity_mcp.validate({'operations':[{'command':'console status','params':{}}]},schema)
        with self.assertRaisesRegex(ValueError, 'must be object'):
            unity_mcp.validate({'operations':[{'command':'console status','params':'{}'}]},schema)
        self.assertEqual('string', original['properties']['operations']['items']['properties']['params']['type'])

    def test_mcp_command_fence_and_unknown_tool(self):
        args = unity_mcp.cli_arguments('unity',self.project,'console get',{'tail':3})
        self.assertEqual(['command','console','get'],args[1:4])
        self.assertLess(args.index('--project-path'),args.index('--'))
        bridge=unity_mcp.Bridge('unity',self.project)
        bridge.commands={'console_get':('console get',{'type':'object'})}
        with self.assertRaisesRegex(ValueError,'Unknown tool'):
            bridge.call('get',{})


if __name__ == '__main__':
    unittest.main()
