"""MCP stdio adapter retaining full Unity Pipeline command groups.

Works around Unity CLI beta.14's duplicate leaf-name MCP tools. Uses the CLI's
working project-scoped command path; no packages, server tokens or scene edits.
"""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
import time


def tool_name(command):
    name = re.sub(r'[^A-Za-z0-9_-]', '_', command)
    if len(name) > 64:
        name = name[:51] + '_' + hashlib.sha256(command.encode()).hexdigest()[:12]
    return name


def cli_arguments(unity, project, command, arguments):
    args = [unity, 'command', *command.split(), '--project-path', str(project),
            '--caller', 'plugin', '--skill', 'unity-verification', '--format', 'json']
    if arguments:
        args.append('--')
        for key, value in arguments.items():
            if not re.fullmatch(r'[A-Za-z_][A-Za-z0-9_-]*', key):
                raise ValueError(f'Invalid parameter name: {key}')
            encoded = json.dumps(value, separators=(',', ':')) if not isinstance(value, str) else value
            args.extend(['--' + key, encoded])
    return args


def corrected_schema(command, source):
    schema = copy.deepcopy(source)
    schema.pop('$schema', None)
    schema.pop('x-command-metadata', None)
    if command == 'job batch':
        properties = schema.get('properties', {})
        items = properties.get('operations', {}).get('items', {}).get('properties', {})
        # Pipeline 0.9 incorrectly advertises JObject fields as strings.
        if 'params' in items:
            items['params']['type'] = 'object'
            items['params']['additionalProperties'] = True
        if 'result_fields' in properties:
            properties['result_fields']['type'] = 'object'
            properties['result_fields']['additionalProperties'] = {'type': 'array', 'items': {'type': 'string'}}
    return schema


def validate(value, schema, path='arguments'):
    kind = schema.get('type')
    checks = {'object': lambda v: isinstance(v, dict), 'array': lambda v: isinstance(v, list),
              'string': lambda v: isinstance(v, str), 'boolean': lambda v: isinstance(v, bool),
              'integer': lambda v: isinstance(v, int) and not isinstance(v, bool),
              'number': lambda v: isinstance(v, (int, float)) and not isinstance(v, bool),
              'null': lambda v: v is None}
    if isinstance(kind, str) and kind in checks and not checks[kind](value):
        raise ValueError(f'{path} must be {kind}, not {type(value).__name__}. Pass structured JSON objects directly; do not stringify them.')
    if isinstance(value, dict):
        missing = set(schema.get('required', [])) - set(value)
        if missing:
            raise ValueError(f'{path}: missing parameters {sorted(missing)}')
        properties = schema.get('properties', {})
        extra = set(value) - set(properties)
        additional = schema.get('additionalProperties', True)
        if additional is False and extra:
            raise ValueError(f'{path}: unknown parameters {sorted(extra)}')
        for key, child in value.items():
            spec = properties.get(key, additional if isinstance(additional, dict) else {})
            validate(child, spec, path + '.' + key)
    if isinstance(value, list) and 'items' in schema:
        for i, child in enumerate(value):
            validate(child, schema['items'], f'{path}[{i}]')


class Bridge:
    def __init__(self, unity, project, compact=False):
        self.unity = unity
        self.project = Path(project).resolve()
        self.commands = {}
        self.compact = compact

    def execute(self, command, arguments=None):
        result = subprocess.run(cli_arguments(self.unity, self.project, command, arguments or {}),
                                capture_output=True, text=True, timeout=180)
        if result.returncode:
            raise RuntimeError(result.stderr.strip() or result.stdout.strip() or 'Unity CLI failed')
        payload = json.loads(result.stdout)
        if payload.get('success') is not True:
            raise RuntimeError(json.dumps(payload.get('errors', payload)))
        return payload

    def list_tools(self):
        # The full CLI catalog has both command (group + leaf) and name (leaf).
        # Use command for BOTH registration and dispatch, never name.
        result = subprocess.run([self.unity, 'command', '--group-by', 'flat', '--detail', 'full', '--project-path', str(self.project),
                                 '--caller', 'plugin', '--skill', 'unity-verification', '--format', 'json'],
                                capture_output=True, text=True, timeout=60)
        if result.returncode:
            raise RuntimeError(result.stderr.strip() or 'Unity catalog unavailable')
        payload = json.loads(result.stdout)
        if payload.get('success') is not True:
            raise RuntimeError(json.dumps(payload.get('errors', [])))
        commands = {}
        tools = []
        for entry in payload.get('data', {}).get('commands', []):
            command = entry['command']
            name = tool_name(command)
            if name in commands:
                raise RuntimeError(f'Duplicate normalized command: {command}')
            schema = corrected_schema(command, entry.get('schema') or {'type': 'object', 'properties': {}})
            commands[name] = (command, schema)
            tools.append({'name': name, 'description': command + ': ' + entry.get('description', ''),
                          'inputSchema': schema})
        self.commands = commands
        if not tools:
            raise RuntimeError('Unity returned no commands. Open the target project, wait for Pipeline readiness, then reconnect.')
        return {'tools': tools}

    def call(self, name, arguments):
        if not self.commands:
            self.list_tools()
        if name not in self.commands:
            raise ValueError(f'Unknown tool {name}. Refresh tools/list; full names include console_get and scene_list_open.')
        command, schema = self.commands[name]
        validate(arguments, schema)
        result = self.execute(command, arguments)
        text = json.dumps(result.get('data'), ensure_ascii=True, separators=(',', ':'))
        if len(text) > 16000:
            evidence = self.project / 'Artifacts' / 'unity-mcp'
            evidence.mkdir(parents=True, exist_ok=True)
            path = evidence / (name + '-' + str(time.time_ns()) + '.json')
            path.write_text(text, encoding='utf-8')
            text = text[:12000] + '\n[Output shortened. Full result: ' + str(path) + ']'
        return {'content': [{'type': 'text', 'text': text}]}

    def handle(self, method, params):
        if method == 'initialize':
            return {'protocolVersion': '2024-11-05', 'capabilities': {'tools': {}},
                    'serverInfo': {'name': 'unity-grouped-cli', 'version': '1.0.0'},
                    'instructions': 'Use full grouped tool names. Inspect scenes with scene_list_open and '
                    'scene_get_hierarchy; diagnose with console_status and console_get. A failed inspection '
                    'does not establish that a scene is empty. Do not create/replace a scene unless requested.'}
        if method == 'ping':
            return {}
        if method == 'tools/list':
            catalog = self.list_tools()
            if not self.compact:
                return catalog
            direct = [t for t in catalog['tools'] if t['name'] in ['console_get','console_status','scene_list_open']]
            direct.extend([
                {'name':'command_catalog','description':'Find Unity commands and their exact parameter schemas before calling command_run.',
                 'inputSchema':{'type':'object','properties':{'query':{'type':'string'}},'required':['query'],'additionalProperties':False}},
                {'name':'command_run','description':'Execute a full grouped Unity command discovered with command_catalog. Pass parameters as a JSON object.',
                 'inputSchema':{'type':'object','properties':{'command':{'type':'string'},'parameters':{'type':'object','additionalProperties':True}},
                                'required':['command','parameters'],'additionalProperties':False}}
            ])
            return {'tools':direct}
        if method == 'tools/call':
            try:
                if self.compact and params['name'] == 'command_catalog':
                    self.list_tools()
                    query = params.get('arguments', {}).get('query','').lower()
                    matches=[{'command':command,'schema':schema} for command,schema in self.commands.values()
                             if query in command.lower()][:10]
                    return {'content':[{'type':'text','text':json.dumps(matches)}]}
                if self.compact and params['name'] == 'command_run':
                    args=params.get('arguments',{})
                    return self.call(tool_name(args['command']),args['parameters'])
                return self.call(params['name'], params.get('arguments', {}))
            except (RuntimeError, ValueError, OSError, subprocess.TimeoutExpired) as error:
                return {'isError': True, 'content': [{'type': 'text', 'text': str(error)[:4000]}]}
        if method in ['resources/list', 'resources/templates/list', 'prompts/list']:
            return {{'resources/list': 'resources', 'resources/templates/list': 'resourceTemplates',
                     'prompts/list': 'prompts'}[method]: []}
        raise ValueError('Unsupported method: ' + method)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--unity', required=True)
    parser.add_argument('--project', required=True)
    parser.add_argument('--compact-tools', action='store_true')
    args = parser.parse_args()
    bridge = Bridge(args.unity, args.project, compact=args.compact_tools)
    for line in sys.stdin:
        try:
            request = json.loads(line)
            if 'id' not in request:
                continue
            try:
                result = bridge.handle(request['method'], request.get('params', {}))
                reply = {'jsonrpc': '2.0', 'id': request['id'], 'result': result}
            except (RuntimeError, ValueError, OSError, KeyError, subprocess.TimeoutExpired) as error:
                reply = {'jsonrpc': '2.0', 'id': request['id'],
                         'error': {'code': -32603, 'message': str(error)[:4000]}}
            print(json.dumps(reply), flush=True)
        except ValueError:
            print(json.dumps({'jsonrpc': '2.0', 'id': None,
                              'error': {'code': -32700, 'message': 'Invalid JSON'}}), flush=True)


if __name__ == '__main__':
    main()
