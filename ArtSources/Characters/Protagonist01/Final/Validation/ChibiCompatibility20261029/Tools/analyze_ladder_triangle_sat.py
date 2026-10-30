"""Independent triangle SAT/topology QA of exported, already-saved animation.

This reads actual baked world-space triangles only; no Unity/source edits.
Unlike a fixed set of 26 hull support directions, each triangle pair uses its
face normals, nine edge-cross-edge axes, and six in-plane edge normals.
"""
from pathlib import Path
import collections
import json
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
SOURCE = Path.home() / 'Desktop/OZGL3_ArtReview/Characters/Protagonist01/Final/Review/Compatibility20261029/Diagnostics/LadderNativeGeometry'
OUTPUT = ROOT / 'QA/ladder-triangle-sat-independent.json'
EPS = 1e-7


def array(data):
    return np.array([[v['x'], v['y'], v['z']] for v in data], dtype=np.float64)


def intersect_sat(a, b):
    """Return intersection mask for fixed triangle a against N triangles b."""
    if len(b) == 0:
        return np.zeros(0, dtype=bool)
    ae = np.roll(a, -1, axis=0) - a
    be = np.roll(b, -1, axis=1) - b
    an = np.cross(ae[0], ae[1])
    bn = np.cross(be[:, 0], be[:, 1])
    axes = np.concatenate([
        np.broadcast_to(an, (len(b), 1, 3)),
        bn[:, None, :],
        np.cross(ae[None, :, None, :], be[:, None, :, :]).reshape(-1, 9, 3),
        np.broadcast_to(np.cross(ae, an), (len(b), 3, 3)),
        np.cross(be, bn[:, None, :]),
    ], axis=1)
    length = np.linalg.norm(axes, axis=2)
    pa = np.einsum('aj,nkj->nka', a, axes)
    pb = np.einsum('naj,nkj->nka', b, axes)
    separated = ((pa.min(axis=2) > pb.max(axis=2) + EPS * length)
                 | (pb.min(axis=2) > pa.max(axis=2) + EPS * length))
    separated &= length > 1e-14
    return ~separated.any(axis=1)


def topology(tri):
    # Quantize duplicate UV seam vertices before testing topological closure.
    points, inverse = np.unique(np.round(tri.reshape(-1, 3) / 1e-6).astype(np.int64), axis=0, return_inverse=True)
    ids = inverse.reshape(-1, 3)
    edges = np.concatenate([ids[:, [0, 1]], ids[:, [1, 2]], ids[:, [2, 0]]])
    edges.sort(axis=1)
    unique_edges, counts = np.unique(edges, axis=0, return_counts=True)
    degenerate = np.linalg.norm(np.cross(tri[:, 1]-tri[:, 0], tri[:, 2]-tri[:, 0]), axis=1) < 1e-12
    parent = list(range(len(points)))
    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x
    for x, y in unique_edges:
        rx, ry = find(int(x)), find(int(y))
        if rx != ry:
            parent[ry] = rx
    components = collections.Counter(find(i) for i in range(len(points)))
    return {
        'triangles': len(tri), 'uniqueVerticesAt1um': len(points),
        'boundaryEdges': int((counts == 1).sum()),
        'nonManifoldEdges': int((counts > 2).sum()),
        'degenerateTriangles': int(degenerate.sum()),
        'componentVertexCounts': sorted(components.values(), reverse=True),
        'closedManifoldAt1um': bool((counts == 2).all() and not degenerate.any()),
    }


def intersections(head, hand):
    hmin, hmax = head.min(axis=1), head.max(axis=1)
    pair_count, examples, involved_head, involved_fist = 0, [], set(), set()
    for i, tri in enumerate(hand):
        broad = np.nonzero(((hmax >= tri.min(axis=0) - EPS) & (hmin <= tri.max(axis=0) + EPS)).all(axis=1))[0]
        hits = broad[intersect_sat(tri, head[broad])]
        pair_count += len(hits)
        if len(hits):
            involved_fist.add(i)
            involved_head.update(map(int, hits))
        for j in hits:
            if len(examples) < 8:
                examples.append({'headTriangle': int(j), 'fistTriangle': i,
                                 'headVertices': head[j].tolist(), 'fistVertices': tri.tolist()})
    return {'intersectingTrianglePairs': int(pair_count), 'involvedHeadTriangles': len(involved_head),
            'involvedFistTriangles': len(involved_fist), 'examples': examples}


def main():
    # Sanity cases include coplanar overlaps, piercing, and disjoint triangles.
    a = np.array([[0., 0., 0.], [1., 0., 0.], [0., 1., 0.]])
    cases = np.array([a, a + [0., 0., .01], a + [2., 0., 0.],
                      [[.2,.2,-1.],[.2,.2,1.],[.4,.2,0.]],
                      [[.8,.8,0.],[1.5,.8,0.],[.8,1.5,0.]]])
    assert intersect_sat(a, cases).tolist() == [True, False, False, True, False]
    files = sorted(SOURCE.glob('*.json'))
    if len(files) != 25:
        raise RuntimeError(f'Expected 25 exported frames, found {len(files)}')
    out = {'method': 'Actual world triangle AABB broadphase and per-pair 17-axis triangle SAT, including coplanar axes. Independent of fixed 26-direction convex support scoring.',
           'coordinateSpace': 'World positions from Skin.BakeMesh then skin.transform.TransformPoint; triangle lists flattened, every 3 entries form one triangle.',
           'toleranceMeters': EPS,
           'groupingLimitation': 'Vertices selected by dominant bone weight; only triangles with all vertices in the same Head/LeftHand/RightHand group are exported. Open group boundaries require separate containment interpretation; surface crossings are still conclusive.',
           'frames': []}
    for file in files:
        data = json.loads(file.read_text())
        head = array(data['head']).reshape(-1,3,3)
        left = array(data['leftTriangles']).reshape(-1,3,3)
        right = array(data['rightTriangles']).reshape(-1,3,3)
        assert np.isfinite(head).all() and np.isfinite(left).all() and np.isfinite(right).all()
        if 'topology' not in out:
            out['topology'] = {'head': topology(head), 'leftFist': topology(left), 'rightFist': topology(right)}
        row = {'frame': file.stem, 'time': data['time'], 'left': intersections(head,left), 'right': intersections(head,right)}
        out['frames'].append(row)
        print(file.stem, round(data['time'],5), row['left']['intersectingTrianglePairs'], row['right']['intersectingTrianglePairs'], flush=True)
    out['framesWithSurfaceIntersections'] = sum(r['left']['intersectingTrianglePairs'] + r['right']['intersectingTrianglePairs'] > 0 for r in out['frames'])
    out['totalIntersectingTrianglePairsAcrossSamples'] = sum(r['left']['intersectingTrianglePairs'] + r['right']['intersectingTrianglePairs'] for r in out['frames'])
    OUTPUT.write_text(json.dumps(out, indent=2)+'\n')
    print('SAVED', OUTPUT, flush=True)


if __name__ == '__main__':
    main()
