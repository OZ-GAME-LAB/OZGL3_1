"""Compare actual Unity BakeMesh samples before/after the bounded body skin fix."""
from pathlib import Path
import gzip, hashlib, json, struct
import numpy as np

HERE = Path(__file__).resolve().parent
ROOT = next(p for p in HERE.parents if (p / 'Assets').is_dir())
LEGACY = HERE.parent / 'Legacy'

def read_mesh(path):
    raw = gzip.decompress(path.read_bytes())
    frames, meshes = struct.unpack_from('<ii', raw)
    counts = struct.unpack_from('<' + 'i' * meshes, raw, 8)
    values = np.frombuffer(raw, dtype='<f4', offset=8 + 4 * meshes)
    values = values.reshape(frames, 1 + sum(counts) * 3)
    cursor = 1
    result = []
    for count in counts:
        result.append(values[:, cursor:cursor + count * 3].reshape(frames, count, 3))
        cursor += count * 3
    return values[:, 0], result

before = json.loads((LEGACY / 'hero-audit.json').read_text())
after = json.loads((LEGACY / 'combat-retouched-audit.json').read_text())
patch = json.loads((ROOT / 'Logs/AssetAudit20261027/SkinSolver/unity-weight-patch.json').read_text())
patch_indices = [p.get('vertex', p.get('vertexIndex', p.get('index'))) for p in patch['patches']]
records = []
for current in after['clips']:
    previous = next(c for c in before['clips'] if c['set'] == 'Combat' and c['clip'] == current['clip'])
    old_path = ROOT / previous['rawMeshFile']
    new_path = ROOT / current['rawMeshFile']
    old_times, old_meshes = read_mesh(old_path)
    new_times, new_meshes = read_mesh(new_path)
    old_schema = json.loads(Path(str(old_path) + '.schema.json').read_text())
    new_schema = json.loads(Path(str(new_path) + '.schema.json').read_text())
    meshes = []
    for i, (a, b) in enumerate(zip(old_meshes, new_meshes)):
        delta = np.linalg.norm(b - a, axis=2)
        entry = {'name': current['meshes'][i]['name'], 'vertices': a.shape[1],
                 'maxWorldSurfaceDifferenceMetres': float(delta.max()),
                 'allPositionsFinite': bool(np.isfinite(b).all()),
                 'trianglesIdentical': old_schema['meshes'][i]['triangles'] == new_schema['meshes'][i]['triangles']}
        if i == 0:
            unchanged = np.ones(a.shape[1], dtype=bool)
            unchanged[patch_indices] = False
            entry['maxUntouchedVertexDifferenceMetres'] = float(delta[:, unchanged].max())
        meshes.append(entry)
    records.append({'clip': current['clip'], 'samples': len(new_times),
        'timesIdentical': bool(np.array_equal(old_times, new_times)),
        'clipBytesUnchanged': previous['clipSHA256'] == current['clipSHA256'],
        'durationUnchanged': previous['duration'] == current['duration'],
        'missingBindingTargets': current['missingBindingTargets'],
        'loopSeamVertexDistanceMetres': current['loopSeamVertexDistance'],
        'baselineRawSHA256': hashlib.sha256(old_path.read_bytes()).hexdigest(),
        'correctedRawSHA256': hashlib.sha256(new_path.read_bytes()).hexdigest(),
        'meshes': meshes})
assert all(r['timesIdentical'] and r['clipBytesUnchanged'] and r['durationUnchanged'] and r['missingBindingTargets'] == 0 for r in records)
assert all(m['allPositionsFinite'] and m['trianglesIdentical'] for r in records for m in r['meshes'])
assert all(m['maxWorldSurfaceDifferenceMetres'] == 0 for r in records for m in r['meshes'][1:])
assert max(r['meshes'][0]['maxWorldSurfaceDifferenceMetres'] for r in records) < .014
assert max(r['meshes'][0]['maxUntouchedVertexDifferenceMetres'] for r in records) < 1e-6
report = {'method': 'Fresh real-project Unity prefab reload + 60 Hz AnimationClip.SampleAnimation and BakeMesh, compared at identical times against original audit. No controller or in-game integration.', 'changedBodyVertices': len(patch_indices), 'clips': records, 'passed': True}
(HERE / 'unity-skin-regression.json').write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))
