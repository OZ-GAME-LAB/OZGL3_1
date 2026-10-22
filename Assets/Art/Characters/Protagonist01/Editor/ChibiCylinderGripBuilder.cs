#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Creates a v007-only cylindrical grip variant. Does not replace a prefab or connect gameplay.</summary>
public static class ChibiCylinderGripBuilder
{
    public const string MeshPath = "Assets/Art/Characters/Protagonist01/ChibiMotion_v007/Meshes/Protagonist_Chibi_CylinderGrip.asset";
    public const float PropScale = .70f;
    public const float CylinderGripWeight = 150f;
    public const float SwordGripWeight = 175f;
    public static readonly Vector3 GripCenterMetres = new Vector3(-.024f, .068f, -.0205f);
    public static readonly Vector3 MountRotationEuler = new Vector3(45, 0, 0);
    public static Vector3 CylinderAxis => Quaternion.Euler(MountRotationEuler) * Vector3.up;
    public static Vector3 RadialY => Quaternion.Euler(MountRotationEuler) * Vector3.back;
    const string SourceMesh = "Assets/Art/Characters/Protagonist01/ChibiCap_v006/Meshes/Protagonist_ChibiCap_Fists.asset";
    const string SourcePrefab = "Assets/Art/Characters/Protagonist01/ChibiCap_v006/Prefabs/Protagonist_ChibiCap_Rig.prefab";
    const string ShaftMesh = "Assets/Art/Stage01Kit/Meshes/Weapons/StaffOrb/Staff_ShaftHardware.asset";
    const string SwordShaftMesh = "Assets/Art/Stage01Kit/Meshes/Weapons/Sword.asset";
    const string ReportPath = "ArtSources/Stage01Kit/research/staff-cylinder-grip-build.json";
    const float Ridge = .068f;
    const float PadHalfThickness = .012f;
    const float SurfaceTolerance = .00055f;
    static float lastBeforeContactDepth;
    static int[] lastBeforeContactFace = Array.Empty<int>();
    static readonly float[] Columns = { -.040f, -.019f, .002f, .023f };

    [MenuItem("Art/Characters/Build v007 Calibrated Cylindrical Grip")]
    public static void Build()
    {
        Mesh source = AssetDatabase.LoadAssetAtPath<Mesh>(SourceMesh);
        Mesh shaft = AssetDatabase.LoadAssetAtPath<Mesh>(ShaftMesh);
        if (!source || !shaft) throw new InvalidOperationException("Original fist mesh or actual staff shaft is missing.");
        string sourceSha = Hash(SourceMesh);
        GameObject actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefab));
        Mesh output = null;
        try
        {
            var skin = actor.GetComponentInChildren<SkinnedMeshRenderer>(true);
            int hand = Array.FindIndex(skin.bones, b => b.name == "RightHand");
            if (hand < 0 || skin.bones.Length != source.bindposes.Length) throw new InvalidOperationException("Hand/bind pose correspondence is invalid.");
            Matrix4x4 toHand = source.bindposes[hand], toMesh = toHand.inverse;
            float metresPerUnit = skin.bones[hand].TransformVector(Vector3.up).magnitude;
            if (Mathf.Abs(metresPerUnit - 1) > .001f) throw new InvalidOperationException("Expected normalized physical-metre v006 prefab.");
            var vertices = source.vertices;
            var normals = source.normals;
            var tangents = source.tangents;
            var weights = source.boneWeights;
            var influences = new float[vertices.Length];
            var bind = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                bind[i] = toHand.MultiplyPoint3x4(vertices[i]);
                influences[i] = Influence(weights[i], hand);
            }
            int shape = source.GetBlendShapeIndex("Grip_R");
            int frame75 = Enumerable.Range(0, source.GetBlendShapeFrameCount(shape)).First(i => Mathf.Abs(source.GetBlendShapeFrameWeight(shape, i) - 75) < .01f);
            var oldDelta = new Vector3[vertices.Length];
            source.GetBlendShapeFrameVertices(shape, frame75, oldDelta, null, null);
            var oldClosed = vertices.Select((v, i) => toHand.MultiplyPoint3x4(v + oldDelta[i])).ToArray();
            int[] triangles = source.triangles;
            int[] sourceHandTriangles = Enumerable.Range(0, triangles.Length / 3)
                .Where(t => Enumerable.Range(0, 3).Any(k => influences[triangles[t * 3 + k]] >= .55f)).ToArray();
            List<Vector2[]> profile = ShaftEnvelope(shaft, -.08f, .10f);
            float maxShaftRadius = profile.SelectMany(e => e).Max(p => p.magnitude);
            float centrelineRadius = maxShaftRadius + PadHalfThickness;
            var ranges = MeasureColumns(bind, influences);
            // CMC / thenar root is near the wrist, not halfway along the palm.
            Vector3 thumbPivot = new Vector3(0, .017f, .035f);
            int thumbTipIndex = Enumerable.Range(0, vertices.Length)
                .Where(i => influences[i] >= .55f && bind[i].z > .065f && bind[i].y > .065f && bind[i].y < .15f)
                .OrderByDescending(i => bind[i].z).First();
            Vector3 thumbAxis = bind[thumbTipIndex] - thumbPivot;
            Vector2 thumbRadial = new Vector2(-.64f, -.768375f).normalized;
            float thumbRadius = ProfileRadius(profile, thumbRadial) + .0065f;
            Vector3 thumbTip = GripCenterMetres + Vector3.right * (thumbRadial.x * thumbRadius) +
                RadialY * (thumbRadial.y * thumbRadius) + CylinderAxis * (.030f - GripCenterMetres.z);
            Quaternion thumbRotation = Quaternion.FromToRotation(thumbAxis, thumbTip - thumbPivot);
            float thumbAxialScale = (thumbTip - thumbPivot).magnitude / thumbAxis.magnitude;
            var target = (Vector3[])oldClosed.Clone();
            var movable = new float[vertices.Length];
            int localSections = 0; float sectionMinimum = float.PositiveInfinity, sectionMaximum = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = bind[i];
                float thumb = Thumb(p);
                bool thumbBase = thumb > .02f && p.y >= .005f && influences[i] >= .30f;
                if ((!thumbBase && (influences[i] < .5f || p.y < .025f)) || p.y > .19f) continue;
                float rootBlend = Smooth((p.y - .025f) / .030f);
                float[] range = RangeAt(ranges, p.z);
                if (thumb < .5f && LocalTransverseRange(p, bind, triangles, sourceHandTriangles, out Vector2 section))
                {
                    range = new[] { section.x, section.y }; localSections++;
                    sectionMinimum = Mathf.Min(sectionMinimum, section.y - section.x);
                    sectionMaximum = Mathf.Max(sectionMaximum, section.y - section.x);
                }
                float transverse = Mathf.Clamp01((p.x - range[0]) / Mathf.Max(.01f, range[1] - range[0]));
                float angle = Mathf.Clamp((p.y - Ridge) / centrelineRadius, -.60f, 3.30f);
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float tipTaper = Mathf.Lerp(1, .70f, Smooth((p.y - .140f) / .030f));
                float radius = ProfileRadius(profile, direction) + SurfaceTolerance + transverse * PadHalfThickness * 2 * tipTaper;
                Vector3 curled = GripCenterMetres + Vector3.right * (direction.x * radius) +
                    RadialY * (direction.y * radius) + CylinderAxis * ((p.z - GripCenterMetres.z) * .98f);
                Vector3 q = Vector3.LerpUnclamped(oldClosed[i], curled, rootBlend * (1 - thumb));
                if (thumb > 0)
                {
                    Vector3 offset = p - thumbPivot, axis = thumbAxis.normalized;
                    Vector3 axial = axis * Vector3.Dot(offset, axis);
                    Vector3 opposed = thumbPivot + thumbRotation * (offset - axial + axial * thumbAxialScale);
                    float basalContinuity = Smooth((p.y - .005f) / .025f);
                    q = Vector3.LerpUnclamped(q, opposed, thumb * basalContinuity);
                }
                movable[i] = Smooth((p.y - .027f) / .030f) * Smooth((influences[i] - .45f) / .35f);
                Vector2 radial = Relative(q);
                if (radial.sqrMagnitude < .0000001f) radial = Vector2.right * .001f;
                float minimum = ProfileRadius(profile, radial.normalized) + SurfaceTolerance;
                if (radial.magnitude < minimum && movable[i] > .05f)
                {
                    Vector2 safe = radial.normalized * minimum;
                    q += Vector3.right * (safe.x - radial.x) + RadialY * (safe.y - radial.y);
                }
                target[i] = q;
            }
            int[] handTriangles = Enumerable.Range(0, triangles.Length / 3)
                .Where(t => Enumerable.Range(0, 3).Any(k => movable[triangles[t * 3 + k]] > .05f)).ToArray();
            var welded = WeldedVertices(bind);
            foreach (var group in welded.Distinct())
            {
                if (!group.Any(i => movable[i] > .05f)) continue;
                Vector3 mean = Vector3.zero; foreach (int i in group) mean += target[i]; mean /= group.Count;
                foreach (int i in group) target[i] = mean;
            }
            FairThumbTip(target, bind, triangles, handTriangles, profile, welded);
            float correctionMaximum = ConstrainFaces(target, movable, triangles, handTriangles, profile, welded, bind);
            float staffBeforeContactDepth = lastBeforeContactDepth;
            int[] staffBeforeContactFace = (int[])lastBeforeContactFace.Clone();
            float finalIntrusion = MaximumIntrusion(target, triangles, handTriangles, profile);
            var swordSource = AssetDatabase.LoadAssetAtPath<Mesh>(SwordShaftMesh);
            if (!swordSource) throw new InvalidOperationException("Sword grip geometry is missing.");
            List<Vector2[]> swordProfile = ShaftEnvelope(swordSource, -.08f, .10f);
            var swordTarget = (Vector3[])target.Clone();
            for (int i = 0; i < swordTarget.Length; i++)
            {
                if (movable[i] <= .05f) continue;
                Vector2 radial = Relative(target[i]);
                if (radial.sqrMagnitude < 1e-10f) continue;
                Vector2 n = radial.normalized;
                float reduction = ProfileRadius(profile, n) - ProfileRadius(swordProfile, n);
                swordTarget[i] -= (Vector3.right * n.x + RadialY * n.y) * reduction * movable[i];
            }
            foreach (var group in welded.Distinct())
            {
                if (!group.Any(i => movable[i] > .05f)) continue;
                Vector3 mean = Vector3.zero; foreach (int i in group) mean += swordTarget[i]; mean /= group.Count;
                foreach (int i in group) swordTarget[i] = mean;
            }
            FairThumbTip(swordTarget, bind, triangles, handTriangles, swordProfile, welded);
            float swordCorrectionMaximum = ConstrainFaces(swordTarget, movable, triangles, handTriangles, swordProfile, welded, bind);
            float swordIntrusion = MaximumIntrusion(swordTarget, triangles, handTriangles, swordProfile);
            var wristAffected = new HashSet<int>();
            // Retired after actual surface/visual rejection: a30degree axis crossed wrist volume.
            // Correct the weapon basis instead of pushing its real skin into a spiky folded surface.
            float wristCorrection = 0;
            // Actual ready-pose BakeMesh revealed 0.84mm inward skin drift on the partial-weight
            // palm and its adjacent root (source450/449/425,457/456/458,1828/1831/1832). Keep weights and the sword pose fixed;
            // compensate only this new staff frame, with a smooth1.6mm maximum local displacement.
            CompensateReadyPalm(target, bind, welded);

            var newDelta = new Vector3[vertices.Length];
            int changedMorph = 0; float maximumMorphDifference = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                newDelta[i] = toMesh.MultiplyPoint3x4(target[i]) - vertices[i];
                float change = (newDelta[i] - oldDelta[i]).magnitude;
                if (change > .000001f) changedMorph++;
                maximumMorphDifference = Mathf.Max(maximumMorphDifference, change);
            }
            output = Object.Instantiate(source);
            output.name = "Protagonist_Chibi_CylinderGrip_v007";
            output.ClearBlendShapes();
            var affected = new HashSet<int>();
            foreach (int t in handTriangles) for (int k = 0; k < 3; k++) affected.Add(triangles[t * 3 + k]);
            NormalDeltas(source, newDelta, affected, out Vector3[] newNormals, out Vector3[] newTangents);
            var swordDelta = swordTarget.Select((p, i) => toMesh.MultiplyPoint3x4(p) - vertices[i]).ToArray();
            var swordAffected = new HashSet<int>(affected); swordAffected.UnionWith(wristAffected);
            NormalDeltas(source, swordDelta, swordAffected, out Vector3[] swordNormals, out Vector3[] swordTangents);
            for (int s = 0; s < source.blendShapeCount; s++)
            {
                for (int f = 0; f < source.GetBlendShapeFrameCount(s); f++)
                {
                    var dv = new Vector3[vertices.Length]; var dn = new Vector3[vertices.Length]; var dt = new Vector3[vertices.Length];
                    source.GetBlendShapeFrameVertices(s, f, dv, dn, dt);
                    output.AddBlendShapeFrame(source.GetBlendShapeName(s), source.GetBlendShapeFrameWeight(s, f),
                        dv, dn, dt);
                }
                if (s == shape)
                {
                    output.AddBlendShapeFrame(source.GetBlendShapeName(s), CylinderGripWeight, newDelta, newNormals, newTangents);
                    output.AddBlendShapeFrame(source.GetBlendShapeName(s), SwordGripWeight, swordDelta, swordNormals, swordTangents);
                }
            }

            int changedWeights = 0; float largestWeightChange = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                if (influences[i] < .5f || bind[i].y < .025f || bind[i].y > .19f) continue;
                // The fingers and opposed distal thumb should belong to the hand, not shear toward the forearm.
                float strength = Smooth((bind[i].y - .025f) / .040f);
                float handWeight = Mathf.Lerp(influences[i], 1, strength);
                if (handWeight - influences[i] < .000001f) continue;
                weights[i] = Reweight(weights[i], hand, handWeight);
                changedWeights++; largestWeightChange = Mathf.Max(largestWeightChange, handWeight - influences[i]);
            }
            output.boneWeights = weights;
            int originalVertexCount = output.vertexCount;
            var refinement = new RefinementResult(); // Original7101 topology; no wrist subdivision/corrective.
            output.RecalculateBounds();
            Directory.CreateDirectory(Path.GetDirectoryName(MeshPath)); AssetDatabase.Refresh();
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (!saved) { AssetDatabase.CreateAsset(output, MeshPath); saved = output; output = null; }
            else { EditorUtility.CopySerialized(output, saved); EditorUtility.SetDirty(saved); }
            AssetDatabase.SaveAssets();
            if (sourceSha != Hash(SourceMesh)) throw new InvalidOperationException("v006 source mesh changed unexpectedly.");
            var report = new GripBuildReport
            {
                source = SourceMesh, sourceSha256 = sourceSha, output = MeshPath, sourceUnchanged = true,
                method = "Source(Y,Z) triangle-ray cross-sections measure each finger's local X centre and thickness instead of flattening its full longitudinal X envelope. Fingers fold around the actual staff profile in a45degree mount basis so the shaft exits beside the palm rather than through wrist volume; thumb opposes the shaft. Anatomical thumb mask stops before the long index finger; distal thumb fairing and source-edge length constraints limit stretched tips. Welded-position groups move together across UV seams. Grip_R150 is new; original75/100 and all other frame deltas are copied exactly. Skin weights use the same prior smooth distal-hand correction. Requires actual3-view and animated approval; no numeric naturalness claim.",
                gripCenterMetres = GripCenterMetres, mountRotationEuler = MountRotationEuler, propScale = PropScale,
                axisHandMetres = CylinderAxis, maximumActualShaftRadius = maxShaftRadius,
                fingerCentrelineRadius = centrelineRadius, padHalfThickness = PadHalfThickness,
                surfaceTolerance = SurfaceTolerance, columnMinimumMaximumX = ranges.Select(r => new Vector2(r[0], r[1])).ToArray(),
                originalFingerColumnsZ = Columns, thumbTargetMetres = thumbTip,
                cylindricalGripFrameWeight = CylinderGripWeight, changedGrip150VerticesRelativeTo75 = changedMorph, maximumDifferenceFromOriginal75Metres = maximumMorphDifference,
                triangleConstraintMaximumCorrection = correctionMaximum, finalConservativeProfileIntrusionMetres = finalIntrusion,
                changedWeightVertices = changedWeights, maximumAddedHandWeight = largestWeightChange,
                weightRule = "Only original RightHand influence >=.5 and bind Y .025..19m. RightHand weight lerps to1 with smoothstep(Y=.025..065m); other original bone influences renormalize proportionally. Under .025m unchanged. v006 source, left hand, bind poses, UVs, base vertices and full-fist100 deltas preserved. Unarmed thumb skinning changes locally in v007 and must be visually rechecked.",
                requiresVisualApproval = true
            };
            report.localXraySections = localSections; report.localXrayMinimumSourceThickness = sectionMinimum;
            report.localXrayMaximumSourceThickness = sectionMaximum;
            report.swordGripFrameWeight = SwordGripWeight;
            report.swordMaximumShaftRadius = swordProfile.SelectMany(e => e).Max(p => p.magnitude);
            report.swordAdditionalConstraintCorrection = swordCorrectionMaximum;
            report.swordProfileIntrusion = swordIntrusion;
            report.swordMethod = "New175 is derived from unchanged staff150: preserve axial position and pad thickness, subtract actual Staff-vs-Sword radial profile difference, then apply identical welded fairing/contact/edge constraints. Original75/100, staff150 and the skin weight formula are unchanged by adding175.";
            report.profileEnvelopeSourceYMinimum = -.08f; report.profileEnvelopeSourceYMaximum = .10f;
            report.staffDepthBeforeFinalContactOnlyPass = staffBeforeContactDepth;
            report.staffSourceFaceBeforeFinalContactOnlyPass = staffBeforeContactFace;
            report.staffOriginalHandInfluencesOnThatFace = staffBeforeContactFace.Select(i => influences[i]).ToArray();
            report.staffReadyPalmCompensationMaximumMetres = .0016f;
            report.staffReadyPalmCompensation = "Staff150 only: smooth local radial offset on palm sourceY .014..076, sourceZ below .040; maximum1.6mm. Compensates measured actual ready skin drift on450/449/425 and adjacent457/456/458 and1828/1831/1832. Sword175 and all weights unchanged.";
            report.swordWristCorrectiveMaximumMetres = wristCorrection;
            report.swordWristCorrectiveVertexCount = wristAffected.Count;
            report.swordWristCorrective = "Disabled. The rejected30degree basis and strong16-vertex skin corrective were removed;45degree shaft placement is evaluated directly on original7101 topology.";
            report.originalVertexCount = originalVertexCount; report.refinedVertexCount = output ? output.vertexCount : saved.vertexCount;
            report.swordWristRefinementAddedVertices = refinement.addedVertices;
            report.swordWristRefinedCorrectiveMaximumMetres = refinement.correction;
            report.swordWristRefinement = "Disabled. Original7101 topology and index buffer restored; no additional vertices or midpoint skin corrections.";
            report.thumbBasalPivotMetres = thumbPivot;
            report.thumbWebSculpt = "Grip150/175 only: CMC pivot moved from palmY.052 to basalY.017/Z.035. Anatomical thumb/web mask starts sourceZ.026..064 and permits a smooth basal rotation from sourceY.005..030, including original hand influence>=.30 in that thumb-only region. Ordinary wrist/weights unchanged. Palm-to-MCP curl blends over sourceY.025..055 instead of.040..070, avoiding stationary75 base connected to a rotated tip. Preserves transverse thumb volume with a rigid basal rotation and longitudinal scale; no additional contact iteration loops.";
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)); File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            Debug.Log("CHIBI_CYLINDER_GRIP_BUILD_SUCCESS " + MeshPath + " intrusion=" + finalIntrusion);
        }
        finally { if (output) Object.DestroyImmediate(output); Object.DestroyImmediate(actor); }
    }

    static List<Vector2[]> ShaftEnvelope(Mesh mesh, float minimum, float maximum)
    {
        var sections = new List<List<Vector2[]>>();
        for (int i = 0; i <= 18; i++) sections.Add(ShaftProfile(mesh, Mathf.Lerp(minimum, maximum, i / 18f)));
        var p = new Vector2[128];
        for (int i = 0; i < p.Length; i++)
        {
            float angle = i * Mathf.PI * 2 / p.Length;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            p[i] = direction * sections.Max(s => ProfileRadius(s, direction));
        }
        return Enumerable.Range(0, p.Length).Select(i => new[] { p[i], p[(i + 1) % p.Length] }).ToList();
    }
    static List<Vector2[]> ShaftProfile(Mesh mesh, float axialY = 0)
    {
        var v = mesh.vertices; var tr = mesh.triangles; var result = new List<Vector2[]>();
        Quaternion rotation = Quaternion.Euler(MountRotationEuler);
        for (int t = 0; t < tr.Length; t += 3)
        {
            var hits = new List<Vector2>();
            for (int e = 0; e < 3; e++)
            {
                Vector3 a = v[tr[t + e]], b = v[tr[t + (e + 1) % 3]];
                if ((a.y - axialY) * (b.y - axialY) >= 0) continue;
                Vector3 p = rotation * (Vector3.Lerp(a, b, (axialY - a.y) / (b.y - a.y)) * PropScale);
                hits.Add(new Vector2(Vector3.Dot(p, Vector3.right), Vector3.Dot(p, RadialY)));
            }
            if (hits.Count == 2 && (hits[1] - hits[0]).sqrMagnitude > 1e-12f) result.Add(hits.ToArray());
        }
        if (result.Count < 6) throw new InvalidOperationException("No complete actual shaft cross-section at its grip.");
        return result;
    }
    static void NormalDeltas(Mesh source, Vector3[] deltas, HashSet<int> affected, out Vector3[] dn, out Vector3[] dt)
    {
        var normalMesh = Object.Instantiate(source);
        try
        {
            var vertices = source.vertices; var normals = source.normals; var tangents = source.tangents;
            normalMesh.vertices = vertices.Select((v, i) => v + deltas[i]).ToArray();
            normalMesh.RecalculateNormals(); normalMesh.RecalculateTangents();
            var nn = normalMesh.normals; var nt = normalMesh.tangents;
            dn = new Vector3[vertices.Length]; dt = new Vector3[vertices.Length];
            foreach (int i in affected)
            {
                dn[i] = nn[i] - normals[i];
                if (nt.Length == vertices.Length && tangents.Length == vertices.Length)
                    dt[i] = new Vector3(nt[i].x - tangents[i].x, nt[i].y - tangents[i].y, nt[i].z - tangents[i].z);
            }
        }
        finally { Object.DestroyImmediate(normalMesh); }
    }

    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    static float ProfileRadius(List<Vector2[]> profile, Vector2 direction)
    {
        float best = float.PositiveInfinity;
        foreach (var e in profile)
        {
            Vector2 d = e[1] - e[0]; float denominator = Cross(direction, d);
            if (Mathf.Abs(denominator) < 1e-9f) continue;
            float distance = Cross(e[0], d) / denominator, u = Cross(e[0], direction) / denominator;
            if (distance > 0 && u >= -.00001f && u <= 1.00001f) best = Mathf.Min(best, distance);
        }
        if (float.IsInfinity(best)) throw new InvalidOperationException("Staff cross-section ray missed its boundary.");
        return best;
    }

    static float[][] MeasureColumns(Vector3[] p, float[] influence)
    {
        var result = new float[Columns.Length][];
        for (int c = 0; c < Columns.Length; c++)
        {
            var xs = Enumerable.Range(0, p.Length).Where(i => influence[i] >= .9f && p[i].y > .080f && p[i].y < .17f &&
                p[i].z < .047f && Mathf.Abs(p[i].z - Columns[c]) < .013f).Select(i => p[i].x).OrderBy(x => x).ToArray();
            if (xs.Length < 10) throw new InvalidOperationException("Finger column sample is insufficient.");
            result[c] = new[] { xs.First(), xs.Last() };
        }
        return result;
    }
    static float[] RangeAt(float[][] ranges, float z)
    {
        if (z <= Columns[0]) return ranges[0];
        for (int c = 1; c < Columns.Length; c++) if (z <= Columns[c])
        {
            float u = Smooth((z - Columns[c - 1]) / (Columns[c] - Columns[c - 1]));
            return new[] { Mathf.Lerp(ranges[c - 1][0], ranges[c][0], u), Mathf.Lerp(ranges[c - 1][1], ranges[c][1], u) };
        }
        return ranges.Last();
    }
    static bool LocalTransverseRange(Vector3 p, Vector3[] source, int[] triangles, int[] selected, out Vector2 section)
    {
        float minimum = float.PositiveInfinity, maximum = float.NegativeInfinity;
        Vector2 location = new Vector2(p.y, p.z);
        foreach (int face in selected)
        {
            Vector3 a = source[triangles[face * 3]], b = source[triangles[face * 3 + 1]], c = source[triangles[face * 3 + 2]];
            Vector2 ayz = new Vector2(a.y, a.z), ab = new Vector2(b.y - a.y, b.z - a.z), ac = new Vector2(c.y - a.y, c.z - a.z);
            float determinant = Cross(ab, ac);
            if (Mathf.Abs(determinant) < 1e-10f) continue;
            Vector2 offset = location - ayz;
            float v = Cross(offset, ac) / determinant, w = Cross(ab, offset) / determinant, u = 1 - v - w;
            if (u < -.00002f || v < -.00002f || w < -.00002f) continue;
            float x = a.x * u + b.x * v + c.x * w;
            minimum = Mathf.Min(minimum, x); maximum = Mathf.Max(maximum, x);
        }
        section = new Vector2(minimum, maximum);
        return maximum - minimum >= .008f && maximum - minimum < .10f;
    }
    static List<int>[] WeldedVertices(Vector3[] bind)
    {
        var map = new Dictionary<Vector3Int, List<int>>(); var result = new List<int>[bind.Length];
        for (int i = 0; i < bind.Length; i++)
        {
            Vector3 p = bind[i]; var key = new Vector3Int(Mathf.RoundToInt(p.x * 1000000), Mathf.RoundToInt(p.y * 1000000), Mathf.RoundToInt(p.z * 1000000));
            if (!map.TryGetValue(key, out var group)) { group = new List<int>(); map[key] = group; }
            group.Add(i); result[i] = group;
        }
        return result;
    }
    static float Thumb(Vector3 p) => Smooth((p.z - .026f) / .038f) * (1 - Smooth((p.y - .105f) / .025f));
    static void CompensateReadyPalm(Vector3[] points, Vector3[] bind, List<int>[] welded)
    {
        foreach (var group in welded.Distinct())
        {
            int i = group[0]; Vector3 p = bind[i];
            float mask = Smooth((p.y - .014f) / .024f) * (1 - Smooth((p.y - .058f) / .018f)) *
                (1 - Smooth((p.z - .020f) / .020f));
            if (mask <= 0) continue;
            Vector2 radial = Relative(points[i]);
            if (radial.sqrMagnitude < 1e-10f) continue;
            Vector2 n = radial.normalized;
            Vector3 delta = (Vector3.right * n.x + RadialY * n.y) * (.0016f * mask);
            foreach (int j in group) points[j] += delta;
        }
    }
    sealed class RefinementResult { public int addedVertices; public float correction; }
    sealed class MorphRecords
    {
        public string name; public float weight;
        public List<Vector3> delta, normal, tangent;
    }
    static RefinementResult RefineSwordWrist(Mesh mesh, int handIndex)
    {
        if (mesh.subMeshCount != 1) throw new InvalidOperationException("Expected one source material submesh.");
        int originalCount = mesh.vertexCount;
        var vertices = mesh.vertices.ToList(); var normals = mesh.normals.ToList();
        var tangents = mesh.tangents.ToList(); var uv = mesh.uv.ToList(); var weights = mesh.boneWeights.ToList();
        var morphs = new List<MorphRecords>();
        for (int s = 0; s < mesh.blendShapeCount; s++) for (int f = 0; f < mesh.GetBlendShapeFrameCount(s); f++)
        {
            var dv = new Vector3[originalCount]; var dn = new Vector3[originalCount]; var dt = new Vector3[originalCount];
            mesh.GetBlendShapeFrameVertices(s, f, dv, dn, dt);
            morphs.Add(new MorphRecords { name = mesh.GetBlendShapeName(s), weight = mesh.GetBlendShapeFrameWeight(s, f),
                delta = dv.ToList(), normal = dn.ToList(), tangent = dt.ToList() });
        }
        Matrix4x4 toHand = mesh.bindposes[handIndex];
        var originalBind = vertices.Select(v => toHand.MultiplyPoint3x4(v)).ToArray();
        var originalWelded = WeldedVertices(originalBind);
        var seeds = new HashSet<int>(new[] { 533, 532, 530 });
        var tr = mesh.triangles.ToList();
        var selected = new List<bool>();
        for (int f = 0; f < tr.Count / 3; f++)
            selected.Add(Enumerable.Range(0, 3).Any(k => originalWelded[tr[f * 3 + k]].Any(seeds.Contains)));
        long Key(int a, int b) { if (a > b) { int t = a; a = b; b = t; } return ((long)a << 32) | (uint)b; }
        int Midpoint(int a, int b)
        {
            int result = vertices.Count;
            Vector3 n = (normals[a] + normals[b]).normalized;
            vertices.Add((vertices[a] + vertices[b]) * .5f); normals.Add(n);
            Vector4 t = (tangents[a] + tangents[b]) * .5f;
            Vector3 xyz = new Vector3(t.x, t.y, t.z).normalized;
            tangents.Add(new Vector4(xyz.x, xyz.y, xyz.z, t.w >= 0 ? 1 : -1));
            uv.Add((uv[a] + uv[b]) * .5f); weights.Add(BlendWeights(weights[a], weights[b]));
            foreach (var m in morphs)
            {
                m.delta.Add((m.delta[a] + m.delta[b]) * .5f);
                m.normal.Add((normals[a] + m.normal[a] + normals[b] + m.normal[b]).normalized - n);
                m.tangent.Add((m.tangent[a] + m.tangent[b]) * .5f);
            }
            return result;
        }
        for (int level = 0; level < 3; level++)
        {
            var middle = new Dictionary<long, int>();
            for (int f = 0; f < selected.Count; f++) if (selected[f])
                for (int k = 0; k < 3; k++)
                {
                    int a = tr[f * 3 + k], b = tr[f * 3 + (k + 1) % 3]; long key = Key(a, b);
                    if (!middle.ContainsKey(key)) middle.Add(key, Midpoint(a, b));
                }
            var next = new List<int>(); var nextSelected = new List<bool>();
            void Face(int a, int b, int c, bool flag) { next.Add(a); next.Add(b); next.Add(c); nextSelected.Add(flag); }
            for (int f = 0; f < selected.Count; f++)
            {
                int a = tr[f * 3], b = tr[f * 3 + 1], c = tr[f * 3 + 2]; bool flag = selected[f];
                bool ab = middle.TryGetValue(Key(a, b), out int x), bc = middle.TryGetValue(Key(b, c), out int y), ca = middle.TryGetValue(Key(c, a), out int z);
                if (ab && bc && ca) { Face(a, x, z, flag); Face(x, b, y, flag); Face(z, y, c, flag); Face(x, y, z, flag); }
                else if (ab && bc) { Face(b, y, x, flag); Face(a, x, c, flag); Face(x, y, c, flag); }
                else if (ab && ca) { Face(a, x, z, flag); Face(x, b, c, flag); Face(x, c, z, flag); }
                else if (bc && ca) { Face(c, z, y, flag); Face(a, b, z, flag); Face(b, y, z, flag); }
                else if (ab) { Face(a, x, c, flag); Face(x, b, c, flag); }
                else if (bc) { Face(b, y, a, flag); Face(y, c, a, flag); }
                else if (ca) { Face(c, z, b, flag); Face(z, a, b, flag); }
                else Face(a, b, c, flag);
            }
            tr = next; selected = nextSelected;
        }
        mesh.ClearBlendShapes(); mesh.vertices = vertices.ToArray(); mesh.normals = normals.ToArray();
        mesh.tangents = tangents.ToArray(); mesh.uv = uv.ToArray(); mesh.boneWeights = weights.ToArray(); mesh.triangles = tr.ToArray();
        var bind = vertices.Select(v => toHand.MultiplyPoint3x4(v)).ToArray(); var welded = WeldedVertices(bind);
        var boundary = new HashSet<int>();
        for (int f = 0; f < selected.Count; f++) if (!selected[f])
            for (int k = 0; k < 3; k++) foreach (int i in welded[tr[f * 3 + k]]) boundary.Add(i);
        var allowed = new HashSet<int>(Enumerable.Range(originalCount, vertices.Count - originalCount).Where(i => !boundary.Contains(i)));
        var sword = morphs.First(m => m.name == "Grip_R" && Mathf.Abs(m.weight - SwordGripWeight) < .01f);
        var closed = vertices.Select((v, i) => toHand.MultiplyPoint3x4(v + sword.delta[i])).ToArray();
        var affected = new HashSet<int>();
        float correction = CorrectActualSwordWrist(closed, bind, welded, mesh, weights.ToArray(), handIndex,
            toHand.inverse, tr.ToArray(), ShaftEnvelope(AssetDatabase.LoadAssetAtPath<Mesh>(SwordShaftMesh), -.08f, .10f),
            affected, Enumerable.Range(0, selected.Count).Where(i => selected[i]).ToArray(), allowed, true);
        sword.delta = closed.Select((p, i) => toHand.inverse.MultiplyPoint3x4(p) - vertices[i]).ToList();
        NormalDeltas(mesh, sword.delta.ToArray(), affected, out Vector3[] dn175, out Vector3[] dt175);
        sword.normal = dn175.ToList(); sword.tangent = dt175.ToList();
        foreach (var m in morphs) mesh.AddBlendShapeFrame(m.name, m.weight, m.delta.ToArray(), m.normal.ToArray(), m.tangent.ToArray());
        return new RefinementResult { addedVertices = vertices.Count - originalCount, correction = correction };
    }
    static BoneWeight BlendWeights(BoneWeight a, BoneWeight b)
    {
        var sum = new Dictionary<int, float>();
        void Add(BoneWeight w)
        {
            int[] ids = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
            float[] v = { w.weight0, w.weight1, w.weight2, w.weight3 };
            for (int k = 0; k < 4; k++) if (v[k] > 0) { if (!sum.ContainsKey(ids[k])) sum[ids[k]] = 0; sum[ids[k]] += v[k] * .5f; }
        }
        Add(a); Add(b);
        var pairs = sum.OrderByDescending(p => p.Value).Take(4).ToArray();
        float total = pairs.Sum(p => p.Value);
        var r = new BoneWeight();
        if (pairs.Length > 0) { r.boneIndex0 = pairs[0].Key; r.weight0 = pairs[0].Value / total; }
        if (pairs.Length > 1) { r.boneIndex1 = pairs[1].Key; r.weight1 = pairs[1].Value / total; }
        if (pairs.Length > 2) { r.boneIndex2 = pairs[2].Key; r.weight2 = pairs[2].Value / total; }
        if (pairs.Length > 3) { r.boneIndex3 = pairs[3].Key; r.weight3 = pairs[3].Value / total; }
        return r;
    }
    static float CorrectActualSwordWrist(Vector3[] points, Vector3[] bind, List<int>[] welded,
        Mesh source, BoneWeight[] sourceWeights, int handIndex, Matrix4x4 toMesh, int[] triangles,
        List<Vector2[]> profile, HashSet<int> affected, int[] selectedOverride = null,
        HashSet<int> allowedVertices = null, bool weightsAlreadyFinal = false)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChibiMotionSet.PrefabPath);
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            "Assets/Art/Characters/Protagonist01/ChibiMotion_v007/Clips/Sword_Horizontal_Slash.anim");
        if (!prefab || !clip) throw new InvalidOperationException("Actual saved Sword pose is required for wrist corrective.");
        var seed = new HashSet<int>(new[] { 533, 532, 530 });
        var selected = new List<int>();
        for (int f = 0; f < triangles.Length / 3; f++)
        {
            if (selectedOverride != null && !selectedOverride.Contains(f)) continue;
            if (selectedOverride == null && !Enumerable.Range(0, 3).Any(k => welded[triangles[f * 3 + k]].Any(seed.Contains))) continue;
            {
                selected.Add(f);
                for (int k = 0; k < 3; k++) foreach (int i in welded[triangles[f * 3 + k]]) affected.Add(i);
            }
        }
        var initial = (Vector3[])points.Clone();
        var freedom = new float[points.Length];
        foreach (int i in affected)
            freedom[i] = bind[i].y < .040f && (allowedVertices == null || allowedVertices.Contains(i)) ?
                (allowedVertices != null || welded[i].Any(seed.Contains) ? 1 : .55f) : 0;
        var finalWeights = (BoneWeight[])sourceWeights.Clone();
        if (!weightsAlreadyFinal) for (int i = 0; i < finalWeights.Length; i++)
        {
            float influence = Influence(finalWeights[i], handIndex);
            if (influence < .5f || bind[i].y < .025f || bind[i].y > .19f) continue;
            finalWeights[i] = Reweight(finalWeights[i], handIndex,
                Mathf.Lerp(influence, 1, Smooth((bind[i].y - .025f) / .040f)));
        }
        var actor = Object.Instantiate(prefab);
        try
        {
            actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var skin = actor.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Transform rh = skin.bones.First(b => b.name == "RightHand");
            var samples = new List<Vector3>();
            for (int u = 0; u <= 4; u++) for (int v = 0; v <= 4 - u; v++)
                samples.Add(new Vector3(u / 4f, v / 4f, 1 - (u + v) / 4f));
            var poses = new List<Dictionary<int, Matrix4x4>>();
            foreach (float t in new[] { 0f, .08f, .16f, .24f, .32f, .40f, .48f, .56f, .65f })
            {
                clip.SampleAnimation(actor, t);
                Matrix4x4 rotate = Matrix4x4.Rotate(Quaternion.Inverse(rh.rotation));
                var matrices = new Dictionary<int, Matrix4x4>();
                foreach (int i in affected)
                {
                    BoneWeight w = finalWeights[i];
                    int[] bones = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
                    float[] weights = { w.weight0, w.weight1, w.weight2, w.weight3 };
                    Matrix4x4 m = Matrix4x4.zero;
                    for (int j = 0; j < 4; j++)
                    {
                        if (weights[j] <= 0) continue;
                        Matrix4x4 term = rotate * skin.bones[bones[j]].localToWorldMatrix * source.bindposes[bones[j]] * toMesh;
                        for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++) m[r, c] += term[r, c] * weights[j];
                    }
                    Vector3 shift = rotate.MultiplyVector(rh.position);
                    m.m03 -= shift.x; m.m13 -= shift.y; m.m23 -= shift.z;
                    matrices.Add(i, m);
                }
                poses.Add(matrices);
            }
            for (int iteration = 0; iteration < 28; iteration++)
            {
                bool changed = false;
                foreach (var pose in poses) foreach (int f in selected)
                {
                    int[] ids = { triangles[f * 3], triangles[f * 3 + 1], triangles[f * 3 + 2] };
                    foreach (Vector3 bary in samples)
                    {
                        Vector3 live = Vector3.zero;
                        for (int k = 0; k < 3; k++) live += pose[ids[k]].MultiplyPoint3x4(points[ids[k]]) * bary[k];
                        Vector2 radial = Relative(live);
                        if (radial.sqrMagnitude < 1e-10f) continue;
                        Vector2 n = radial.normalized;
                        float deficit = ProfileRadius(profile, n) + .00065f - radial.magnitude;
                        if (deficit <= .00004f) continue;
                        float denominator = Enumerable.Range(0, 3).Sum(k => bary[k] * bary[k] * freedom[ids[k]]);
                        if (denominator < 1e-5f) continue;
                        Vector3 direction = Vector3.right * n.x + RadialY * n.y;
                        for (int k = 0; k < 3; k++)
                        {
                            int i = ids[k];
                            if (freedom[i] <= 0 || bary[k] <= 0) continue;
                            Vector3 liveDelta = direction * Mathf.Min(.0015f, deficit) * bary[k] * freedom[i] / denominator;
                            Vector3 delta = pose[i].inverse.MultiplyVector(liveDelta);
                            Vector3 next = points[i] + delta;
                            Vector3 offset = next - initial[i];
                            if (offset.magnitude > .010f) next = initial[i] + offset.normalized * .010f;
                            Vector3 applied = next - points[i];
                            foreach (int j in welded[i]) points[j] += applied;
                            changed |= applied.sqrMagnitude > 1e-12f;
                        }
                    }
                }
                if (!changed) break;
            }
        }
        finally { Object.DestroyImmediate(actor); }
        return affected.Select(i => (points[i] - initial[i]).magnitude).DefaultIfEmpty(0).Max();
    }
    static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3 - 2 * x); }
    static float Influence(BoneWeight w, int bone) => (w.boneIndex0 == bone ? w.weight0 : 0) + (w.boneIndex1 == bone ? w.weight1 : 0) + (w.boneIndex2 == bone ? w.weight2 : 0) + (w.boneIndex3 == bone ? w.weight3 : 0);
    static BoneWeight Reweight(BoneWeight w, int hand, float handWeight)
    {
        float old = Influence(w, hand), factor = old < .999999f ? (1 - handWeight) / (1 - old) : 0;
        w.weight0 = w.boneIndex0 == hand ? handWeight : w.weight0 * factor;
        w.weight1 = w.boneIndex1 == hand ? handWeight : w.weight1 * factor;
        w.weight2 = w.boneIndex2 == hand ? handWeight : w.weight2 * factor;
        w.weight3 = w.boneIndex3 == hand ? handWeight : w.weight3 * factor;
        return w;
    }

    static Vector2 Relative(Vector3 p) { Vector3 q = p - GripCenterMetres; return new Vector2(q.x, Vector3.Dot(q, RadialY)); }
    static Vector2 ClosestTriangle(Vector2 a, Vector2 b, Vector2 c, out Vector3 bary)
    {
        float denominator = Cross(b - a, c - a);
        if (Mathf.Abs(denominator) > 1e-10f)
        {
            float v = Cross(-a, c - a) / denominator, w = Cross(b - a, -a) / denominator, u = 1 - v - w;
            if (u >= 0 && v >= 0 && w >= 0) { bary = new Vector3(u, v, w); return Vector2.zero; }
        }
        Vector2[] p = { a, b, c }; float best = float.PositiveInfinity; Vector2 point = Vector2.zero; bary = Vector3.zero;
        for (int i = 0; i < 3; i++)
        {
            int j = (i + 1) % 3; Vector2 d = p[j] - p[i];
            float t = d.sqrMagnitude > 1e-12f ? Mathf.Clamp01(-Vector2.Dot(p[i], d) / d.sqrMagnitude) : 0;
            Vector2 q = p[i] + d * t;
            if (q.sqrMagnitude >= best) continue;
            best = q.sqrMagnitude; point = q; bary = Vector3.zero; bary[i] = 1 - t; bary[j] = t;
        }
        return point;
    }
    static void FairThumbTip(Vector3[] p, Vector3[] bind, int[] tr, int[] selected, List<Vector2[]> profile, List<int>[] welded)
    {
        var neighbors = new Dictionary<int, HashSet<int>>();
        foreach (int f in selected) for (int k = 0; k < 3; k++)
        {
            int a = welded[tr[f * 3 + k]][0], b = welded[tr[f * 3 + (k + 1) % 3]][0];
            if (a == b) continue;
            if (!neighbors.ContainsKey(a)) neighbors[a] = new HashSet<int>();
            if (!neighbors.ContainsKey(b)) neighbors[b] = new HashSet<int>();
            neighbors[a].Add(b); neighbors[b].Add(a);
        }
        for (int iteration = 0; iteration < 3; iteration++)
        {
            var before = (Vector3[])p.Clone();
            foreach (var pair in neighbors)
            {
                int i = pair.Key;
                float influence = Thumb(bind[i]) * Smooth((bind[i].z - .080f) / .025f);
                if (influence < .1f) continue;
                Vector3 mean = Vector3.zero; foreach (int j in pair.Value) mean += before[j]; mean /= pair.Value.Count;
                Vector3 q = Vector3.LerpUnclamped(before[i], mean, .22f * influence);
                Vector2 radial = Relative(q);
                if (radial.sqrMagnitude > 1e-10f)
                {
                    float minimum = ProfileRadius(profile, radial.normalized) + SurfaceTolerance;
                    if (radial.magnitude < minimum)
                    {
                        Vector2 correction = radial.normalized * minimum - radial;
                        q += Vector3.right * correction.x + RadialY * correction.y;
                    }
                }
                foreach (int j in welded[i]) p[j] = q;
            }
        }
    }
    static float ConstrainFaces(Vector3[] points, float[] freedom, int[] tr, int[] selected, List<Vector2[]> profile, List<int>[] welded, Vector3[] bind)
    {
        lastBeforeContactDepth = 0; lastBeforeContactFace = Array.Empty<int>();
        var initial = (Vector3[])points.Clone();
        var edges = new Dictionary<long, int[]>();
        foreach (int f in selected) for (int k = 0; k < 3; k++)
        {
            int a = welded[tr[f * 3 + k]][0], b = welded[tr[f * 3 + (k + 1) % 3]][0];
            if (a > b) { int swap = a; a = b; b = swap; }
            if (a != b) edges[((long)a << 32) | (uint)b] = new[] { a, b };
        }
        void MoveGroup(int index, Vector3 delta)
        {
            Vector3 q = points[index] + delta, correction = q - initial[index];
            if (correction.magnitude > .018f) q = initial[index] + correction.normalized * .018f;
            Vector3 move = q - points[index];
            foreach (int i in welded[index]) points[i] += move;
        }
        for (int iteration = 0; iteration < 60; iteration++)
        {
            bool changed = false;
            foreach (int face in selected)
            {
                int a = tr[face * 3], b = tr[face * 3 + 1], c = tr[face * 3 + 2];
                Vector2 near = ClosestTriangle(Relative(points[a]), Relative(points[b]), Relative(points[c]), out Vector3 bary);
                Vector2 normal = near.sqrMagnitude > 1e-12f ? near.normalized : (Relative(points[a]) + Relative(points[b]) + Relative(points[c])).normalized;
                if (normal.sqrMagnitude < .1f) normal = Vector2.right;
                float deficit = ProfileRadius(profile, normal) + SurfaceTolerance - near.magnitude;
                if (deficit <= .00005f) continue;
                float denominator = bary.x * bary.x * freedom[a] + bary.y * bary.y * freedom[b] + bary.z * bary.z * freedom[c];
                if (denominator < .00001f) continue;
                int[] ids = { a, b, c };
                for (int k = 0; k < 3; k++)
                {
                    int i = ids[k]; float distance = Mathf.Min(.002f, deficit) * bary[k] * freedom[i] / denominator;
                    MoveGroup(i, (Vector3.right * normal.x + RadialY * normal.y) * distance);
                }
                changed = true;
            }
            // Fairing/edge regularity finishes first; it must not undo the final surface-contact solve.
            if (iteration < 36) foreach (var edge in edges.Values)
            {
                int a = edge[0], b = edge[1]; Vector3 delta = points[b] - points[a];
                float allowed = (initial[b] - initial[a]).magnitude * 1.45f + .0008f;
                if (bind[a].y > .06f && bind[b].y > .06f)
                    allowed = Mathf.Min(allowed, (bind[b] - bind[a]).magnitude * 1.65f + .0012f);
                if (delta.magnitude <= allowed || freedom[a] + freedom[b] < .0001f) continue;
                Vector3 correction = delta.normalized * (delta.magnitude - allowed) * .40f;
                MoveGroup(a, correction * freedom[a] / (freedom[a] + freedom[b]));
                MoveGroup(b, -correction * freedom[b] / (freedom[a] + freedom[b]));
                changed = true;
            }
            if (iteration == 35)
                lastBeforeContactDepth = MaximumIntrusion(points, tr, selected, profile, out lastBeforeContactFace);
            if (!changed) break;
        }
        return points.Select((p, i) => (p - initial[i]).magnitude).Max();
    }
    static float MaximumIntrusion(Vector3[] p, int[] tr, int[] selected, List<Vector2[]> profile)
    { return MaximumIntrusion(p, tr, selected, profile, out _); }
    static float MaximumIntrusion(Vector3[] p, int[] tr, int[] selected, List<Vector2[]> profile, out int[] sourceFace)
    {
        float maximum = 0;
        sourceFace = Array.Empty<int>();
        foreach (int f in selected)
        {
            Vector2 near = ClosestTriangle(Relative(p[tr[f * 3]]), Relative(p[tr[f * 3 + 1]]), Relative(p[tr[f * 3 + 2]]), out _);
            Vector2 n = near.sqrMagnitude > 1e-12f ? near.normalized : Vector2.right;
            float intrusion = ProfileRadius(profile, n) - near.magnitude;
            if (intrusion > maximum) { maximum = intrusion; sourceFace = new[] { tr[f * 3], tr[f * 3 + 1], tr[f * 3 + 2] }; }
        }
        return maximum;
    }
    static string Hash(string path) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }

    [Serializable] sealed class GripBuildReport
    {
        public string source, sourceSha256, output, method, weightRule;
        public bool sourceUnchanged, requiresVisualApproval;
        public Vector3 gripCenterMetres, mountRotationEuler, axisHandMetres, thumbTargetMetres;
        public float propScale, maximumActualShaftRadius, fingerCentrelineRadius, padHalfThickness, surfaceTolerance;
        public Vector2[] columnMinimumMaximumX; public float[] originalFingerColumnsZ;
        public int changedGrip150VerticesRelativeTo75, changedWeightVertices;
        public int localXraySections; public float localXrayMinimumSourceThickness, localXrayMaximumSourceThickness;
        public float swordGripFrameWeight, swordMaximumShaftRadius, swordAdditionalConstraintCorrection, swordProfileIntrusion;
        public string swordMethod;
        public float profileEnvelopeSourceYMinimum, profileEnvelopeSourceYMaximum, staffDepthBeforeFinalContactOnlyPass;
        public int[] staffSourceFaceBeforeFinalContactOnlyPass; public float[] staffOriginalHandInfluencesOnThatFace;
        public float staffReadyPalmCompensationMaximumMetres; public string staffReadyPalmCompensation;
        public float swordWristCorrectiveMaximumMetres; public int swordWristCorrectiveVertexCount; public string swordWristCorrective;
        public int originalVertexCount, refinedVertexCount, swordWristRefinementAddedVertices;
        public float swordWristRefinedCorrectiveMaximumMetres; public string swordWristRefinement;
        public Vector3 thumbBasalPivotMetres; public string thumbWebSculpt;
        public float cylindricalGripFrameWeight, maximumDifferenceFromOriginal75Metres, triangleConstraintMaximumCorrection, finalConservativeProfileIntrusionMetres, maximumAddedHandWeight;
    }
}
#endif
