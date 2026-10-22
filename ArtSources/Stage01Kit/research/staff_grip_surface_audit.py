"""Read-only geometry measurements of actual Unity BakeMesh probe exports; writes audit JSON only."""
from pathlib import Path
import hashlib
import json
import re
import numpy as np

ROOT = Path(__file__).resolve().parents[3]
RESEARCH = ROOT / 'ArtSources/Stage01Kit/research'
PROBE = RESEARCH / 'StaffGripProbe_Calibrated'


def vec(rows):
    return np.array([[p[k] for k in 'xyz'] for p in rows], dtype=float)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def rays_to_actual_faces(origins, directions, faces):
    """Two-sided Moller-Trumbore intersections. First positive actual triangle hit per ray."""
    v0 = faces[:, 0]
    e1 = faces[:, 1] - v0
    e2 = faces[:, 2] - v0
    answer = np.full(len(origins), np.nan)
    for begin in range(0, len(origins), 128):
        o = origins[begin:begin + 128]
        d = directions[begin:begin + 128]
        h = np.cross(d[:, None, :], e2[None, :, :])
        determinant = np.einsum('ijk,jk->ij', h, e1)
        inverse = np.divide(1., determinant, out=np.zeros_like(determinant), where=np.abs(determinant) > 1e-11)
        s = o[:, None, :] - v0[None, :, :]
        u = np.einsum('ijk,ijk->ij', s, h) * inverse
        q = np.cross(s, e1[None, :, :])
        v = np.einsum('ijk,ik->ij', q, d) * inverse
        t = np.einsum('ijk,jk->ij', q, e2) * inverse
        valid = (np.abs(determinant) > 1e-11) & (u >= -1e-7) & (v >= -1e-7) & (u + v <= 1 + 1e-7) & (t > 1e-7)
        nearest = np.where(valid, t, np.inf).min(axis=1)
        nearest[np.isinf(nearest)] = np.nan
        answer[begin:begin + len(o)] = nearest
    return answer


def sample_probe(path, grip):
    d = json.loads(path.read_text(encoding='utf8'))
    snapshot = next(s for s in d['snapshots'] if s['grip'] == grip)
    p = vec(snapshot['vertices'])
    base = vec(d['sourceBindHandMetres'])
    triangles = np.array(d['triangles']).reshape(-1, 3)
    centre = np.array([d['mountPositionHandMetres'][k] for k in 'xyz'])
    angle = np.deg2rad(d['mountEuler']['x'])
    axis = np.array([0., np.cos(angle), np.sin(angle)])
    radial_y = np.array([0., np.sin(angle), -np.cos(angle)])
    part = next(x for x in d['propParts'] if 'CrystalOrb' not in x['name'])
    prop_v = vec(part['vertices'])
    prop_faces = prop_v[np.array(part['triangles']).reshape(-1, 3)]
    bary = np.array([(i / 4, j / 4, 1 - (i + j) / 4) for i in range(5) for j in range(5 - i)])
    sampled = np.einsum('bk,fkc->fbc', bary, p[triangles]).reshape(-1, 3)
    sampled_base = np.einsum('bk,fkc->fbc', bary, base[triangles]).reshape(-1, 3)
    influence = np.einsum('bk,fk->fb', bary, np.array(d['handInfluence'])[triangles]).reshape(-1)
    axial = (sampled - centre) @ axis
    origins = centre + axial[:, None] * axis
    radial = sampled - origins
    radius = np.linalg.norm(radial, axis=1)
    directions = np.divide(radial, radius[:, None], out=np.zeros_like(radial), where=radius[:, None] > 1e-10)
    hit = rays_to_actual_faces(origins, directions, prop_faces)
    clearance = radius - hit
    finite = np.isfinite(clearance)
    # Also test the analytic closest point of each projected triangle to the weapon axis.
    # This catches a large flat face spanning the shaft between finite grid samples.
    plane = np.stack([(p - centre)[:, 0], (p - centre) @ radial_y], axis=1)
    closest_bary = []
    for face in triangles:
        q = plane[face]
        ab, ac = q[1] - q[0], q[2] - q[0]
        determinant = ab[0] * ac[1] - ab[1] * ac[0]
        bary_q = None
        if abs(determinant) > 1e-14:
            v = (-q[0, 0] * ac[1] + q[0, 1] * ac[0]) / determinant
            w = (-ab[0] * q[0, 1] + ab[1] * q[0, 0]) / determinant
            if v >= 0 and w >= 0 and v + w <= 1:
                bary_q = np.array([1 - v - w, v, w])
        if bary_q is None:
            choices = []
            for i in range(3):
                j = (i + 1) % 3
                edge = q[j] - q[i]
                u = np.clip(-np.dot(q[i], edge) / max(np.dot(edge, edge), 1e-20), 0, 1)
                b = np.zeros(3); b[i] = 1 - u; b[j] = u
                choices.append((np.linalg.norm(q[i] + edge * u), b))
            bary_q = min(choices, key=lambda item: item[0])[1]
        closest_bary.append(bary_q)
    closest_bary = np.array(closest_bary)
    closest_live = np.einsum('fk,fkc->fc', closest_bary, p[triangles])
    close_axial = (closest_live - centre) @ axis
    close_origins = centre + close_axial[:, None] * axis
    close_radial = closest_live - close_origins
    close_radius = np.linalg.norm(close_radial, axis=1)
    close_direction = np.divide(close_radial, close_radius[:, None], out=np.zeros_like(close_radial), where=close_radius[:, None] > 1e-10)
    close_direction[close_radius <= 1e-10] = np.array([1., 0., 0.])
    close_gap = close_radius - rays_to_actual_faces(close_origins, close_direction, prop_faces)
    close_finite = np.isfinite(close_gap)
    close_worst = np.where(close_finite)[0][np.argmin(close_gap[close_finite])]
    critical = {'triangles': int(close_finite.sum()), 'minimumRadialGapM': float(close_gap[close_finite].min()),
                'negativeTrianglesBelowMinus0p1mm': int((close_gap[close_finite] < -.0001).sum()),
                'worstSourceTriangle': [d['sourceVertexIds'][i] for i in triangles[close_worst]],
                'method': 'Analytic closest point to the axis in each projected hand triangle, followed by an actual prop-mesh radial ray at its interpolated axial coordinate. Additional critical-point check; not a proof for continuously varying prop profiles.'}
    masks = {
        'all_exported_hand_triangles': np.ones(len(clearance), dtype=bool),
        'palm_proxy': (sampled_base[:, 1] > .030) & (sampled_base[:, 1] < .068) & (sampled_base[:, 2] < .038) & (influence > .6),
        'finger_proxy': (sampled_base[:, 1] > .070) & (sampled_base[:, 2] < .065) & (influence > .8),
        'thumb_proxy': (sampled_base[:, 2] > .065) & (sampled_base[:, 1] > .060) & (sampled_base[:, 1] < .110) & (influence > .8),
    }
    rows = {}
    for name, mask in masks.items():
        valid = mask & finite
        values = clearance[valid]
        if not len(values):
            rows[name] = {'samples': 0}
            continue
        lowest = np.where(valid)[0][np.argmin(values)]
        face = lowest // len(bary)
        rows[name] = {
            'samples': int(valid.sum()), 'minimumRadialSurfaceGapM': float(values.min()),
            'medianRadialSurfaceGapM': float(np.median(values)),
            'penetratingSamplesBelowMinus0p1mm': int((values < -.0001).sum()),
            'nearContactSamplesWithin2mm': int(((values >= -.0001) & (values <= .002)).sum()),
            'worstSourceTriangle': [d['sourceVertexIds'][i] for i in triangles[face]],
            'worstSampleBindHandMetres': sampled_base[lowest].tolist(),
            'worstSampleLiveHandMetres': sampled[lowest].tolist(),
        }
    contact_mask = finite & (clearance >= -.0001) & (clearance <= .002) & masks['finger_proxy']
    angle_values = np.arctan2(directions[:, :] @ radial_y, directions[:, 0])
    columns = [-.040, -.019, .002, .023]
    assignment = np.argmin(np.abs(sampled_base[:, 2:3] - np.array(columns)[None, :]), axis=1)
    finger_columns = []
    for i, z in enumerate(columns):
        values = clearance[finite & masks['finger_proxy'] & (assignment == i)]
        finger_columns.append({'sourceColumnZ': z, 'samples': len(values), 'minimumRadialGapM': float(values.min()) if len(values) else None})
    rows['contactDistribution'] = {
        'fingerColumns': finger_columns,
        'occupiedFingerContactAngleBins30deg': sorted(set(np.floor((angle_values[contact_mask] + np.pi) / (np.pi / 6)).astype(int).tolist())),
    }
    return {'probe': path.name, 'probeSHA256': sha(path), 'clip': d['clip'], 'time': d['timeSeconds'], 'grip': grip,
            'mountEuler': d['mountEuler'], 'surface': part['mesh'], 'barycentricSamplesPerTriangle': len(bary),
            'axisClosestTriangleCriticalPoints': critical, 'rows': rows}


def frames(path, original_vertex_count=None):
    s = path.read_text(encoding='utf8')
    shape = s[s.index('  m_Shapes:'):s.index('  m_BindPose:')]
    before, after = shape.split('    shapes:\n')
    records = re.findall(r'    - vertex:.*?      index: \d+\n', before, flags=re.S)
    fs = re.findall(r'- firstVertex: (\d+)\s+vertexCount: (\d+)\s+hasNormals: (\d+)\s+hasTangents: (\d+)', after.split('    channels:')[0])
    weights = [float(x) for x in re.findall(r'^    - ([0-9.]+)$', after.split('    fullWeights:')[1], flags=re.M)]
    result = []
    for f, weight in zip(fs, weights):
        first, count = map(int, f[:2])
        frame_records = records[first:first + count]
        text = ''.join(frame_records)
        existing = [r for r in frame_records if original_vertex_count is None or
                    int(re.search(r'      index: (\d+)', r).group(1)) < original_vertex_count]
        result.append({'weight': weight, 'vertices': count, 'SHA256': hashlib.sha256(text.encode()).hexdigest(),
                       'existingVertexRecordCount': len(existing),
                       'existingVertexRecordSHA256': hashlib.sha256(''.join(existing).encode()).hexdigest()})
    data = bytes.fromhex(re.search(r'_typelessdata: ([0-9a-f]+)', s).group(1))
    count = int(re.search(r'm_VertexCount: (\d+)', s).group(1))
    stream1 = (count * 40 + 15) // 16 * 16
    prefix = original_vertex_count or count
    existing_streams = data[:prefix * 40] + data[stream1:stream1 + prefix * 8]
    return result, existing_streams, re.search(r'm_IndexBuffer: ([0-9a-f]+)', s).group(1), s[s.index('  m_BindPose:'):s.index('  m_BoneNameHashes:')]


def verify_weight_rule(original, variant):
    """Check the documented222-vertex correction, without falsely calling weights source-identical."""
    decoded = []
    for path in (original, variant):
        text = path.read_text(encoding='utf8')
        count = int(re.search(r'm_VertexCount: (\d+)', text).group(1))
        raw = bytes.fromhex(re.search(r'_typelessdata: ([0-9a-f]+)', text).group(1))
        stream2 = ((((count * 40 + 15) // 16 * 16) + count * 8 + 15) // 16 * 16)
        positions = np.ndarray((count, 3), dtype='<f4', buffer=raw, strides=(40, 4)).astype(float)
        weights = np.ndarray((count, 4), dtype='<f4', buffer=raw, offset=stream2, strides=(32, 4)).astype(float)
        bones = np.ndarray((count, 4), dtype='<u4', buffer=raw, offset=stream2 + 16, strides=(32, 4))
        influence = np.zeros((count, 24))
        for k in range(4):
            influence[np.arange(count), bones[:, k]] += weights[:, k]
        decoded.append((positions, influence, text))
    positions, before, text = decoded[0]
    after_full = decoded[1][1]
    after = after_full[:len(before)]
    matrix_text = text[text.index('  m_BindPose:'):text.index('  m_BoneNameHashes:')]
    matrices = re.findall(r'  - e00:.*?(?=  - e00:|\Z)', matrix_text, flags=re.S)
    numbers = {key: float(value) for key, value in re.findall(r'(e\d\d): ([^\n]+)', matrices[15])}
    matrix = np.array([[numbers[f'e{r}{c}'] for c in range(4)] for r in range(4)])
    bind = np.c_[positions, np.ones(len(positions))] @ matrix.T
    hand = before[:, 15]
    eligible = (hand >= .5) & (bind[:, 1] >= .025) & (bind[:, 1] <= .19)
    strength = np.clip((bind[:, 1] - .025) / .040, 0, 1)
    strength = strength * strength * (3 - 2 * strength)
    new_hand = hand + (1 - hand) * strength
    apply = eligible & ((new_hand - hand) >= .000001)
    expected = before.copy()
    factor = np.divide(1 - new_hand, 1 - hand, out=np.zeros_like(hand), where=hand < .999999)
    expected[apply] *= factor[apply, None]
    expected[apply, 15] = new_hand[apply]
    delta = np.max(np.abs(after - before), axis=1)
    return {
        'ruleMaximumAbsoluteWeightError': float(np.max(np.abs(after - expected))),
        'ruleWithinFloatTolerance5eMinus7': bool(np.max(np.abs(after - expected)) <= 5e-7),
        'changedVertexCountAbove1eMinus6': int((delta > 1e-6).sum()),
        'verticesOutsideRuleUnchangedWithin1eMinus7': bool(np.max(np.abs(after[~apply] - before[~apply])) < 1e-7),
        'allLeftHandBoneInfluencesExact': bool(np.array_equal(after[:, 11], before[:, 11])),
        'additionalInterpolatedVertices': int(len(after_full) - len(before)),
        'additionalWeightSumsWithin1eMinus6': bool(np.max(np.abs(after_full[len(before):].sum(axis=1) - 1)) < 1e-6) if len(after_full) > len(before) else True,
        'additionalMaximumBonesPerVertex': int(np.max((after_full[len(before):] > 0).sum(axis=1))) if len(after_full) > len(before) else 0,
        'statement': 'v007 weights are not identical to v006. The prior documented smooth distal right-hand correction is retained; this check verifies that exact rule and unchanged vertices outside it.'
    }


def main():
    requests = [('Idle_Fists_0000ms.json', 150), ('Staff_Cast_Forward_0000ms.json', 150),
                ('Staff_Cast_Forward_0240ms.json', 150), ('Sword_Horizontal_Slash_0000ms.json', 175),
                ('Sword_Horizontal_Slash_0240ms.json', 175)]
    rows = [sample_probe(PROBE / name, grip) for name, grip in requests]
    original = ROOT / 'Assets/Art/Characters/Protagonist01/ChibiCap_v006/Meshes/Protagonist_ChibiCap_Fists.asset'
    variant = ROOT / 'Assets/Art/Characters/Protagonist01/ChibiMotion_v007/Meshes/Protagonist_Chibi_CylinderGrip.asset'
    original_count = int(re.search(r'm_VertexCount: (\d+)', original.read_text()).group(1))
    variant_count = int(re.search(r'm_VertexCount: (\d+)', variant.read_text()).group(1))
    source, stream0, tr0, bp0 = frames(original, original_count)
    target, stream1, tr1, bp1 = frames(variant, original_count)
    compare = [('Right75', source[0], target[0]), ('Right100', source[1], target[1]),
               ('Left75', source[2], target[4]), ('Left100', source[3], target[5])]
    result = {
        'method': 'Actual Unity BakeMesh hand triangles sampled at15 barycentric positions each. At each point, cast a physical radial ray from the actual mounted weapon axis into its actual attached mesh triangles (two-sided first positive Moller-Trumbore hit), at that point axial coordinate. Report signed radial surface gap, not marker distance.',
        'limits': 'Finite surface sampling does not prove continuous collision freedom or Euclidean minimum distance. Whole exported wrist includes an artificial open selection cut. The bone-proxy region masks are stated in the research note and do not replace visual anatomical inspection. No full-game runtime or other unexported poses are certified.',
        'sourceOriginalMeshSHA256': sha(original), 'variantMeshSHA256': sha(variant),
        'unchangedOriginalFrameRecords': {name: a['existingVertexRecordCount'] == b['existingVertexRecordCount'] and
                                        a['existingVertexRecordSHA256'] == b['existingVertexRecordSHA256']
                                        for name, a, b in compare},
        'baseVertexNormalTangentUVStreamsExact': stream0 == stream1,
        'originalTriangleIndicesExact': tr0 == tr1, 'originalBindposesExact': bp0 == bp1,
        'topologyScope': {'originalVertexCount': original_count, 'variantVertexCount': variant_count,
                          'statement': ('Original vertex count and topology restored. No wrist subdivision or strong wrist corrective. Existing vertex streams, original75/100 records and index buffer are checked exactly.' if original_count == variant_count else
                                        'Checks refer to the original vertex prefix. A local wrist patch adds interpolated vertices and conforming triangles; index buffer and full frame counts intentionally differ. This topology candidate requires explicit actual visual/contact approval.')},
        'newFrames': target[2:4],
        'skinWeightRuleVerification': verify_weight_rule(original, variant),
        'staff150Isolation': 'The175 path clones target as Vector3[] before any changes; its fairing/contact and normal calculation operate on the clone. No archived pre175 frame record is available for an independent historical byte comparison.',
        'samples': rows,
    }
    report = RESEARCH / 'staff-grip-actual-surface-audit.json'
    report.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    sequence_files = sorted((PROBE / 'Sequence30').glob('*.json'))
    if sequence_files:
        sequence = [sample_probe(path, 175 if path.name.startswith('Sword') else 150) for path in sequence_files]
        summaries = []
        for clip in sorted({row['clip'] for row in sequence}):
            subset = [row for row in sequence if row['clip'] == clip]
            grid_worst = min(subset, key=lambda row: row['rows']['all_exported_hand_triangles']['minimumRadialSurfaceGapM'])
            critical_worst = min(subset, key=lambda row: row['axisClosestTriangleCriticalPoints']['minimumRadialGapM'])
            summaries.append({'clip': clip, 'sampleCount': len(subset),
                              'times': [row['time'] for row in subset],
                              'gridMinimumRadialGapM': grid_worst['rows']['all_exported_hand_triangles']['minimumRadialSurfaceGapM'],
                              'gridWorstTime': grid_worst['time'],
                              'gridWorstSourceTriangle': grid_worst['rows']['all_exported_hand_triangles']['worstSourceTriangle'],
                              'criticalMinimumRadialGapM': critical_worst['axisClosestTriangleCriticalPoints']['minimumRadialGapM'],
                              'criticalWorstTime': critical_worst['time'],
                              'criticalWorstSourceTriangle': critical_worst['axisClosestTriangleCriticalPoints']['worstSourceTriangle'],
                              'posesWithGridPenetrationBelowMinus0p1mm': sum(row['rows']['all_exported_hand_triangles']['penetratingSamplesBelowMinus0p1mm'] > 0 for row in subset),
                              'posesWithCriticalPenetrationBelowMinus0p1mm': sum(row['axisClosestTriangleCriticalPoints']['negativeTrianglesBelowMinus0p1mm'] > 0 for row in subset)})
        sequence_report = {'method': result['method'], 'limits': result['limits'],
                           'variantMeshSHA256': result['variantMeshSHA256'], 'sampleCount': len(sequence),
                           'summaries': summaries, 'samples': sequence}
        (RESEARCH / 'staff-grip-sequence-surface-audit.json').write_text(json.dumps(sequence_report, indent=2) + '\n', encoding='utf8')
    print(json.dumps({'framePreservation': result['unchangedOriginalFrameRecords'], 'baseStreamsExact': stream0 == stream1,
                      'surface': [{k: row[k] for k in ['clip', 'time', 'grip']} | {'rows': row['rows']} for row in rows]}, indent=2))


if __name__ == '__main__':
    main()
