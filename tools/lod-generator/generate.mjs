import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { MeshoptSimplifier } from 'meshoptimizer';

const options = {};
for (let i = 2; i < process.argv.length; i += 2) {
    if (!process.argv[i].startsWith('--') || !process.argv[i + 1]) throw new Error('Expected --option value');
    options[process.argv[i].slice(2)] = process.argv[i + 1];
}
const work = path.resolve(options.work || 'work');
const output = path.resolve(options.output || 'output');
const preset = options.preset || 'Balanced';
const presets = {
    Conservative: [[0.7, 0.001], [0.45, 0.004]],
    Balanced: [[0.55, 0.002], [0.25, 0.008]],
    Aggressive: [[0.4, 0.004], [0.12, 0.015]],
};
if (!presets[preset]) throw new Error('Unknown preset: ' + preset);
const rules = JSON.parse(fs.readFileSync(new URL('rules.json', import.meta.url), 'utf8'));
const exclusions = rules.exclude.map(expression => new RegExp(expression, 'i'));
const sha = buffer => crypto.createHash('sha256').update(buffer).digest('hex');
const extraction = JSON.parse(fs.readFileSync(path.join(work, 'extraction.json'), 'utf8'));
await MeshoptSimplifier.ready;
if (!MeshoptSimplifier.supported) throw new Error('This Node.js runtime does not support WebAssembly');
fs.mkdirSync(output, { recursive: true });
const report = { version: 1, preset, generated: [], unchanged: [], excluded: [...extraction.excluded], failures: [...extraction.failures] };
const cachePath = path.join(work, 'build-cache.json');
const cache = fs.existsSync(cachePath) ? JSON.parse(fs.readFileSync(cachePath, 'utf8')) : {};
const catalog = ['WWLOD\t1'];
for (const source of extraction.state.sources) {
    const stat = fs.statSync(source.path);
    if (stat.size !== source.size || Math.floor(stat.mtimeMs / 1000) !== source.time) throw new Error('Asset changed since extraction: ' + source.path);
    catalog.push(['source', Buffer.from(source.path).toString('base64'), source.size, source.time].join('\t'));
}

function attributes(mesh) {
    const selected = mesh.channels.filter(c => [1, 3, 4, 5, 12, 13].includes(c.kind));
    const stride = selected.reduce((sum, c) => sum + c.dimension, 0);
    const values = new Float32Array(mesh.count * stride);
    const weights = [];
    let offset = 0;
    for (const channel of selected) {
        // Keep skin influence changes expensive; endpoint vertices retain their exact source weights.
        const weight = channel.kind >= 12 ? 2 : channel.kind === 1 ? 0.5 : 0.25;
        for (let k = 0; k < channel.dimension; k++) weights.push(weight);
        for (let v = 0; v < mesh.count; v++) {
            for (let k = 0; k < channel.dimension; k++) values[v * stride + offset + k] = channel.data[v * channel.dimension + k];
        }
        offset += channel.dimension;
    }
    return { values, stride, weights };
}

function simplify(mesh, ratio, error, attr) {
    const positions = Float32Array.from(mesh.channels[0].data);
    const result = [];
    const errors = [];
    for (const original of mesh.indices) {
        const indices = Uint32Array.from(original);
        if (indices.length < 96) { result.push(indices); errors.push(0); continue; }
        const target = Math.max(3, Math.floor(indices.length * ratio / 3) * 3);
        const [reduced, achievedError] = attr.stride
            ? MeshoptSimplifier.simplifyWithAttributes(indices, positions, 3, attr.values, attr.stride, attr.weights, null, target, error, ['LockBorder'])
            : MeshoptSimplifier.simplify(indices, positions, 3, target, error, ['LockBorder']);
        if (reduced.length % 3 || !reduced.length || reduced.length > indices.length || !Number.isFinite(achievedError)) throw new Error('Invalid simplifier result');
        for (const i of reduced) if (i >= mesh.count) throw new Error('Invalid simplifier index');
        result.push(reduced);
        errors.push(achievedError);
    }
    return { indices: result, errors };
}

function encode(mesh, levels) {
    const chunks = [];
    function int(value) { const b = Buffer.allocUnsafe(4); b.writeInt32LE(value); chunks.push(b); }
    function floats(values) {
        const b = Buffer.allocUnsafe(values.length * 4);
        for (let i = 0; i < values.length; i++) b.writeFloatLE(values[i], i * 4);
        chunks.push(b);
    }
    int(0x314c5757); // WWL1
    int(levels.length);
    for (const level of levels) {
        const remap = new Map();
        const vertices = [];
        for (const sub of level.indices) for (const i of sub) {
            if (!remap.has(i)) { remap.set(i, vertices.length); vertices.push(i); }
        }
        int(vertices.length);
        int(mesh.channels.length);
        for (const channel of mesh.channels) {
            int(channel.kind); int(channel.dimension);
            const values = new Float32Array(vertices.length * channel.dimension);
            for (let v = 0; v < vertices.length; v++) for (let k = 0; k < channel.dimension; k++) {
                values[v * channel.dimension + k] = channel.data[vertices[v] * channel.dimension + k];
            }
            floats(values);
        }
        int(mesh.bindposes.length / 16);
        floats(mesh.bindposes);
        int(level.indices.length);
        for (const sub of level.indices) {
            int(sub.length);
            const b = Buffer.allocUnsafe(sub.length * 4);
            for (let i = 0; i < sub.length; i++) b.writeInt32LE(remap.get(sub[i]), i * 4);
            chunks.push(b);
        }
    }
    return Buffer.concat(chunks);
}

for (const entry of extraction.meshes) {
    if (exclusions.some(regex => regex.test(entry.name))) {
        report.excluded.push({ name: entry.name, reason: 'rules.json exclusion' });
        continue;
    }
    try {
        const raw = fs.readFileSync(path.join(work, entry.key + '.json'));
        const levels = entry.skinned ? presets.Conservative : presets[preset];
        const fingerprint = sha(Buffer.concat([raw, Buffer.from(JSON.stringify({ levels, version: 1, meshoptimizer: '1.1.1' }))]));
        const previous = cache[entry.key];
        if (previous?.fingerprint === fingerprint) {
            const existing = path.join(output, previous.payload + '.wwlod');
            if (fs.existsSync(existing) && sha(fs.readFileSync(existing)) === previous.payload) {
                catalog.push(['mesh', entry.key, previous.payload].join('\t'));
                report.generated.push(previous.stats);
                continue;
            }
        }
        const mesh = JSON.parse(raw);
        const attr = attributes(mesh);
        const variants = levels.map(([ratio, error]) => simplify(mesh, ratio, error, attr));
        const counts = variants.map(v => v.indices.reduce((sum, s) => sum + s.length / 3, 0));
        if (counts.every(n => n >= entry.triangles * 0.95)) {
            report.unchanged.push({ ...entry, reason: 'less than 5% reduction within the error/border limits' });
            continue;
        }
        const binary = encode(mesh, variants);
        const payload = sha(binary);
        const destination = path.join(output, payload + '.wwlod');
        if (!fs.existsSync(destination)) { fs.writeFileSync(destination + '.next', binary); fs.renameSync(destination + '.next', destination); }
        const stats = { ...entry, levels: counts, errors: variants.map(v => Math.max(...v.errors)), bytes: binary.length };
        report.generated.push(stats);
        catalog.push(['mesh', entry.key, payload].join('\t'));
        cache[entry.key] = { fingerprint, payload, stats };
        if (report.generated.length % 100 === 0) console.log('Generated ' + report.generated.length + ' meshes');
    } catch (error) {
        report.failures.push({ name: entry.name, reason: String(error) });
    }
}
if (!report.generated.length) throw new Error('No reduced meshes generated; previous catalog retained');
report.totals = {
    meshes: report.generated.length,
    skinned: report.generated.filter(m => m.skinned).length,
    originalTriangles: report.generated.reduce((n, m) => n + m.triangles, 0),
    lod1Triangles: report.generated.reduce((n, m) => n + m.levels[0], 0),
    lod2Triangles: report.generated.reduce((n, m) => n + m.levels[1], 0),
};
// A failed generation never replaces the last working catalog.
fs.writeFileSync(path.join(output, 'report.json'), JSON.stringify(report, null, 2));
fs.writeFileSync(cachePath, JSON.stringify(cache));
fs.writeFileSync(path.join(output, 'catalog.next'), catalog.join('\n') + '\n');
fs.renameSync(path.join(output, 'catalog.next'), path.join(output, 'catalog.tsv'));
console.log(JSON.stringify(report.totals, null, 2));
console.log(`Excluded ${report.excluded.length}; unchanged ${report.unchanged.length}; failures ${report.failures.length}. See report.json.`);
