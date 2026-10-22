#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// <summary>Small, closed, faceted skin volumes for the calibrated right-hand power grip.</summary>
public static class ChibiClosedGripGeometry
{
    static Vector3[] wristBoundary;
    static BoneWeight[] wristBoundaryWeights;

    /// <summary>Supply source cuff weights for a smooth sleeve overlap; no source rim is stitched.</summary>
    public static void ConfigureWristBoundary(Vector3[] handPoints, BoneWeight[] weights)
    {
        bool valid = handPoints != null && weights != null && handPoints.Length >= 3 &&
            handPoints.Length == weights.Length;
        wristBoundary = valid ? (Vector3[])handPoints.Clone() : null;
        wristBoundaryWeights = valid ? (BoneWeight[])weights.Clone() : null;
    }

    public static void Append(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uv,
        List<BoneWeight> weights, List<int> triangles, Matrix4x4 handToMesh,
        int handBoneIndex, Vector2 skinUv, bool staff)
    {
        new Grip(vertices, normals, uv, weights, triangles, handToMesh, handBoneIndex, skinUv,
            staff ? .0189f : .01335f, staff, wristBoundary, wristBoundaryWeights).Build();
    }

    sealed class Grip
    {
        static readonly Vector3 Mount = new Vector3(-.024f, .068f, -.0205f);
        static readonly Vector3 Axis = Quaternion.Euler(45f, 0f, 0f) * Vector3.up;
        static readonly Vector3 Across = Quaternion.Euler(45f, 0f, 0f) * Vector3.back;
        // A square pad with its four corners bevelled, rather than a circular sausage.
        static readonly Vector2[] Section = {
            new Vector2(.62f, 1f), new Vector2(-.62f, 1f),
            new Vector2(-1f, .62f), new Vector2(-1f, -.62f),
            new Vector2(-.62f, -1f), new Vector2(.62f, -1f),
            new Vector2(1f, -.62f), new Vector2(1f, .62f)
        };

        readonly List<Vector3> vertices;
        readonly List<Vector3> normals;
        readonly List<Vector2> uv;
        readonly List<BoneWeight> weights;
        readonly List<int> triangles;
        readonly Matrix4x4 toMesh;
        readonly Matrix4x4 normalToMesh;
        readonly BoneWeight weight;
        Vector2 surfaceUv;
        readonly float shaftRadius;
        readonly bool staff;
        readonly BoneWeight[] boundaryWeights;

        public Grip(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uv,
            List<BoneWeight> weights, List<int> triangles, Matrix4x4 toMesh,
            int bone, Vector2 skinUv, float shaftRadius, bool staff, Vector3[] boundary, BoneWeight[] boundaryWeights)
        {
            this.vertices = vertices;
            this.normals = normals;
            this.uv = uv;
            this.weights = weights;
            this.triangles = triangles;
            this.toMesh = toMesh;
            normalToMesh = toMesh.inverse.transpose;
            weight = new BoneWeight { boneIndex0 = bone, weight0 = 1f };
            surfaceUv = skinUv;
            this.shaftRadius = shaftRadius;
            this.staff = staff;
            this.boundaryWeights = boundaryWeights;
        }

        public void Build()
        {
            // The back of the hand supports all four proximal pads. The broad surfaces
            // are rounded and broken into short facets, including along the knuckles.
            BevelBox(new Vector3(shaftRadius + .014f, -.010f, -.002f),
                new Vector3(.012f, .021f, .037f), .007f);

            Finger(-.029f, .0078f, .0080f, 3.52f);
            Finger(-.010f, .0085f, .0085f, 3.65f);
            Finger(.009f, .0085f, .0086f, 3.69f);
            Finger(.028f, .0082f, .0084f, 3.55f);
            Thumb();
            Wrist();
        }

        Vector3 At(Vector3 p) => Mount + Vector3.right * p.x + Across * p.y + Axis * p.z;

        void Finger(float axial, float halfWidth, float pad, float endAngle)
        {
            const int segments = 9;
            var centres = new Vector3[segments + 1];
            var rings = new Vector3[segments + 1][];
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = Mathf.Lerp(-.60f, endAngle, t);
                Vector3 radial = Vector3.right * Mathf.Cos(angle) + Across * Mathf.Sin(angle);
                centres[i] = Mount + radial * (shaftRadius + .0095f) + Axis * axial;
                float bevel = i == 0 ? .78f : i == segments ? .72f : 1f;
                rings[i] = Ring(centres[i], radial, Axis, pad * bevel, halfWidth * bevel);
            }
            Sweep(centres, rings);
        }

        void Thumb()
        {
            const int segments = 7;
            var centres = new Vector3[segments + 1];
            var radial = new Vector3[segments + 1];
            var rings = new Vector3[segments + 1][];
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = Mathf.Lerp(-.62f, -2.75f, t);
                radial[i] = Vector3.right * Mathf.Cos(angle) + Across * Mathf.Sin(angle);
                float radius = shaftRadius + Mathf.Lerp(.023f, .016f, t * t * (3f - 2f * t));
                float axial = .030f + .012f * Mathf.Sin(t * Mathf.PI) - .020f * t;
                centres[i] = Mount + radial[i] * radius + Axis * axial;
            }
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                Vector3 tangent = (centres[Mathf.Min(i + 1, segments)] - centres[Mathf.Max(i - 1, 0)]).normalized;
                Vector3 n = Vector3.ProjectOnPlane(radial[i], tangent).normalized;
                Vector3 across = Vector3.Cross(tangent, n).normalized;
                float bevel = i == 0 ? .80f : i == segments ? .72f : 1f;
                rings[i] = Ring(centres[i], n, across,
                    Mathf.Lerp(.011f, .0085f, t) * bevel, .0095f * bevel);
            }
            Sweep(centres, rings);
        }

        void Wrist()
        {
            // This is an independent closed cuff, overlapped into the original sleeve.
            // Never stitch a regular new volume to the source's nonplanar skin rim.
            var centres = new[] {
                new Vector3(.006f, -.012f, .003f),
                new Vector3(.006f, .008f, .003f),
                new Vector3(.006f, .025f, -.001f),
                new Vector3(.006f, .037f, -.008f),
                new Vector3(.006f, .044f, -.015f),
                At(new Vector3(shaftRadius + .015f, -.014f, -.014f))
            };
            float[] radiusX = { .050f, .054f, .038f, .028f, .023f, .020f };
            float[] radiusZ = { .064f, .068f, .044f, .029f, .022f, .018f };
            float[] handBlend = { 0f, 0f, .15f, .50f, .85f, 1f };
            BoneWeight proximal = weight;
            if (boundaryWeights != null)
            {
                var average = new Dictionary<int, float>();
                foreach (BoneWeight w in boundaryWeights)
                    AddWeights(average, w, 1f / boundaryWeights.Length);
                proximal = PackedWeights(average);
            }
            var rings = new Vector3[centres.Length][];
            var ringWeights = new BoneWeight[centres.Length][];
            for (int j = 0; j < centres.Length; j++)
            {
                Vector3 tangent = (centres[Mathf.Min(j + 1, centres.Length - 1)] -
                    centres[Mathf.Max(j - 1, 0)]).normalized;
                Vector3 side = Vector3.ProjectOnPlane(Vector3.right, tangent).normalized;
                Vector3 front = Vector3.Cross(tangent, side).normalized;
                const int sides = 12;
                rings[j] = new Vector3[sides];
                ringWeights[j] = new BoneWeight[sides];
                BoneWeight w = BlendWeights(proximal, weight, handBlend[j]);
                for (int k = 0; k < sides; k++)
                {
                    float angle = 2f * Mathf.PI * k / sides;
                    rings[j][k] = centres[j] + side * (Mathf.Cos(angle) * radiusX[j]) +
                        front * (Mathf.Sin(angle) * radiusZ[j]);
                    if (!staff && j <= 2)
                    {
                        // The sword pommel is wider than its grip shaft. Round off
                        // this rear cuff corner with a 2mm trim and smooth neighbours.
                        float back = Mathf.Max(0f, Mathf.Sin(angle));
                        float falloff = back * back; falloff *= falloff;
                        float row = j == 1 ? 1f : j == 0 ? .45f : .25f;
                        rings[j][k] -= front * (.002f * row * falloff);
                    }
                    ringWeights[j][k] = w;
                }
            }
            // Validated source face1908 cuff patch: atlas pixel1694,488 is RGB43,43,55;
            // its surrounding33px square contains only the same dark cuff fabric.
            Vector2 previousUv = surfaceUv;
            surfaceUv = new Vector2(.8273589969f, .7614722908f);
            Sweep(centres, rings, ringWeights);
            surfaceUv = previousUv;
        }

        static void AddInfluence(Dictionary<int, float> result, int bone, float value)
        {
            if (value <= 0f) return;
            result[bone] = result.TryGetValue(bone, out float old) ? old + value : value;
        }

        static void AddWeights(Dictionary<int, float> result, BoneWeight w, float scale)
        {
            AddInfluence(result, w.boneIndex0, w.weight0 * scale);
            AddInfluence(result, w.boneIndex1, w.weight1 * scale);
            AddInfluence(result, w.boneIndex2, w.weight2 * scale);
            AddInfluence(result, w.boneIndex3, w.weight3 * scale);
        }

        static BoneWeight PackedWeights(Dictionary<int, float> values)
        {
            var sorted = new List<KeyValuePair<int, float>>(values);
            sorted.Sort((a, b) => b.Value.CompareTo(a.Value));
            float total = 0f;
            for (int i = 0; i < Mathf.Min(4, sorted.Count); i++) total += sorted[i].Value;
            var result = new BoneWeight();
            if (total <= 0f) return result;
            if (sorted.Count > 0) { result.boneIndex0 = sorted[0].Key; result.weight0 = sorted[0].Value / total; }
            if (sorted.Count > 1) { result.boneIndex1 = sorted[1].Key; result.weight1 = sorted[1].Value / total; }
            if (sorted.Count > 2) { result.boneIndex2 = sorted[2].Key; result.weight2 = sorted[2].Value / total; }
            if (sorted.Count > 3) { result.boneIndex3 = sorted[3].Key; result.weight3 = sorted[3].Value / total; }
            return result;
        }

        static BoneWeight BlendWeights(BoneWeight a, BoneWeight b, float t)
        {
            var result = new Dictionary<int, float>();
            AddWeights(result, a, 1f - t);
            AddWeights(result, b, t);
            return PackedWeights(result);
        }

        static Vector3[] Ring(Vector3 centre, Vector3 x, Vector3 y, float rx, float ry)
        {
            var result = new Vector3[Section.Length];
            for (int i = 0; i < result.Length; i++)
                result[i] = centre + x * (Section[i].x * rx) + y * (Section[i].y * ry);
            return result;
        }

        static BoneWeight? RingWeight(BoneWeight[][] ringWeights, int ring, int vertex)
            => ringWeights != null && ringWeights[ring] != null ?
                ringWeights[ring][vertex] : (BoneWeight?)null;

        void Sweep(Vector3[] centres, Vector3[][] rings, BoneWeight[][] ringWeights = null,
            bool capStart = true, bool exactFirstRing = false)
        {
            int sides = rings[0].Length;
            for (int j = 0; j < rings.Length - 1; j++)
            {
                Vector3 centre = (centres[j] + centres[j + 1]) * .5f;
                for (int k = 0; k < sides; k++)
                {
                    int next = (k + 1) % sides;
                    Vector3 a = rings[j][k], b = rings[j + 1][k];
                    Vector3 c = rings[j + 1][next], d = rings[j][next];
                    Vector3 outward = (a + b + c + d) * .25f - centre;
                    Face(a, b, c, outward, RingWeight(ringWeights, j, k),
                        RingWeight(ringWeights, j + 1, k), RingWeight(ringWeights, j + 1, next), exactFirstRing && j == 0);
                    Face(a, c, d, outward, RingWeight(ringWeights, j, k),
                        RingWeight(ringWeights, j + 1, next), RingWeight(ringWeights, j, next),
                        exactFirstRing && j == 0, false, exactFirstRing && j == 0);
                }
            }
            int last = rings.Length - 1;
            for (int k = 0; k < sides; k++)
            {
                int next = (k + 1) % sides;
                if (capStart)
                    Face(centres[0], rings[0][k], rings[0][next], centres[0] - centres[1], RingWeight(ringWeights, 0, k),
                        RingWeight(ringWeights, 0, k), RingWeight(ringWeights, 0, next),
                        false, exactFirstRing, exactFirstRing);
                Face(centres[last], rings[last][k], rings[last][next], centres[last] - centres[last - 1], RingWeight(ringWeights, last, k),
                    RingWeight(ringWeights, last, k), RingWeight(ringWeights, last, next));
            }
        }

        void BevelBox(Vector3 centre, Vector3 half, float bevel)
        {
            float[][] grid = {
                new[] { -half.x, -half.x + bevel, half.x - bevel, half.x },
                new[] { -half.y, -half.y + bevel, half.y - bevel, half.y },
                new[] { -half.z, -half.z + bevel, 0f, half.z - bevel, half.z }
            };
            for (int axis = 0; axis < 3; axis++)
            {
                int u = (axis + 1) % 3, v = (axis + 2) % 3;
                for (int sign = -1; sign <= 1; sign += 2)
                    for (int i = 0; i < grid[u].Length - 1; i++)
                        for (int j = 0; j < grid[v].Length - 1; j++)
                        {
                            Vector3 a = BoxPoint(centre, half, bevel, axis, sign, u, grid[u][i], v, grid[v][j]);
                            Vector3 b = BoxPoint(centre, half, bevel, axis, sign, u, grid[u][i + 1], v, grid[v][j]);
                            Vector3 c = BoxPoint(centre, half, bevel, axis, sign, u, grid[u][i + 1], v, grid[v][j + 1]);
                            Vector3 d = BoxPoint(centre, half, bevel, axis, sign, u, grid[u][i], v, grid[v][j + 1]);
                            Vector3 outward = (a + b + c + d) * .25f - At(centre);
                            Face(a, b, c, outward);
                            Face(a, c, d, outward);
                        }
            }
        }

        Vector3 BoxPoint(Vector3 centre, Vector3 half, float bevel,
            int axis, int sign, int u, float su, int v, float sv)
        {
            Vector3 p = Vector3.zero;
            p[axis] = half[axis] * sign;
            p[u] = su;
            p[v] = sv;
            Vector3 inner = half - Vector3.one * bevel;
            var nearest = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x),
                Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
            return At(centre + nearest + (p - nearest).normalized * bevel);
        }

        Vector3 ClearShaft(Vector3 p)
        {
            Vector3 d = p - Mount;
            Vector3 radial = d - Axis * Vector3.Dot(d, Axis);
            // A small clearance also protects the interiors of the low-poly faces.
            // Only the hidden inside of the wrist and the closest finger corners need
            // this adjustment; the palm and opposed thumb already clear the handle.
            float minimum = shaftRadius + .0013f;
            if (radial.sqrMagnitude < minimum * minimum)
                p += radial.normalized * (minimum - radial.magnitude);
            return p;
        }

        void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 outward,
            BoneWeight? aWeight = null, BoneWeight? bWeight = null, BoneWeight? cWeight = null,
            bool exactA = false, bool exactB = false, bool exactC = false)
        {
            a = toMesh.MultiplyPoint3x4(exactA ? a : ClearShaft(a));
            b = toMesh.MultiplyPoint3x4(exactB ? b : ClearShaft(b));
            c = toMesh.MultiplyPoint3x4(exactC ? c : ClearShaft(c));
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude < 1e-16f) return;
            if (Vector3.Dot(normal, normalToMesh.MultiplyVector(outward)) < 0f)
            {
                Vector3 swap = b; b = c; c = swap;
                BoneWeight? swapWeight = bWeight; bWeight = cWeight; cWeight = swapWeight;
                normal = -normal;
            }
            normal.Normalize();
            int first = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            for (int i = 0; i < 3; i++)
            {
                normals.Add(normal);
                uv.Add(surfaceUv);
                weights.Add(i == 0 ? (aWeight ?? weight) : i == 1 ? (bWeight ?? weight) : (cWeight ?? weight));
                triangles.Add(first + i);
            }
        }
    }
}
#endif
