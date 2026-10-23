#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Creates independent hand morphs with a folded fingertip chain and opposed thumb.</summary>
public static class ChibiCapHandBuilder
{
    public const string Root = "Assets/Art/Characters/Protagonist01/ChibiCap_v006";
    public const string MeshPath = Root + "/Meshes/Protagonist_ChibiCap_Fists.asset";
    const string Source = "Assets/Art/Characters/Protagonist01/ChibiCap_v006/Rig/Protagonist_ChibiCap_Rig.fbx";
    const string ReportPath = "ArtSources/Characters/Protagonist01/v006-chibi-cap/hand-polish-build.json";
    // v006 GLB hand inspection: finger tips are .168/.176m from the wrist in bind space.
    // The .068m ridge preserves the wrist/palm and closes the distal pads.
    const float KnuckleY = .068f;
    const int ArcSamples = 128;

    [Serializable] public sealed class ShapeReport
    {
        public string hand, shape;
        public float frameWeight, totalFingerFlexionDegrees, maximumDisplacementMetres;
        public int changedVertices, thumbVertices;
        public Vector3 closedMinimum, closedMaximum, thumbTipTarget;
        public float thumbMinimumHandInfluence = 1f, thumbMaximumHandInfluence;
        public float[] fingerLengthsMetres;
    }

    [Serializable] public sealed class BuildReport
    {
        public bool success, sourceStreamsPreserved;
        public string sourceMesh, outputMesh, method, use;
        public int vertexCount, triangles, blendShapes;
        public List<ShapeReport> shapes = new List<ShapeReport>();
    }

    public static BuildReport LastReport { get; private set; }

    [MenuItem("Project M/Chibi Cap v006/Build Fist Morphs")]
    public static void Build()
    {
        AssetDatabase.ImportAsset(Source, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
        if (!asset) throw new InvalidOperationException("Original protagonist FBX is missing.");
        var instance = Object.Instantiate(asset);
        instance.hideFlags = HideFlags.HideAndDontSave;
        Mesh generated = null;
        try
        {
            generated = BuildMesh(instance.GetComponentInChildren<SkinnedMeshRenderer>(true));
            Directory.CreateDirectory(Root + "/Meshes");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (!saved) { AssetDatabase.CreateAsset(generated, MeshPath); saved = generated; generated = null; }
            else { EditorUtility.CopySerialized(generated, saved); EditorUtility.SetDirty(saved); }
            LastReport.outputMesh = MeshPath;
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, JsonUtility.ToJson(LastReport, true));
            AssetDatabase.SaveAssets();
            Debug.Log("CHIBI_CAP_HAND_BUILD_SUCCESS " + MeshPath);
        }
        finally
        {
            if (generated) Object.DestroyImmediate(generated);
            Object.DestroyImmediate(instance);
        }
    }

    /// <summary>Returns an unsaved mesh clone. The source renderer, mesh, rig and prefabs remain unchanged.</summary>
    public static Mesh BuildMesh(SkinnedMeshRenderer source)
    {
        if (!source || !source.sharedMesh) throw new ArgumentException("A source skinned mesh is required.");
        Mesh original = source.sharedMesh;
        var mesh = Object.Instantiate(original);
        mesh.name = "Protagonist_ChibiCap_Fists";
        mesh.ClearBlendShapes();
        LastReport = new BuildReport
        {
            sourceMesh = AssetDatabase.GetAssetPath(original), outputMesh = MeshPath,
            method = "Clone the original skin streams. Finger phalanges fold from the new-hand .068m metacarpal ridge (read-only GLB bind-space inspection) along a three-stage C1 centreline (MCP/PIP/DIP), " +
                "with separately measured lengths across four finger columns. Full fist flexion is 85+105+70=260 degrees, so the distal pads return to the palm instead of stopping in a C-shaped cup; " +
                "the weapon frame uses 65+70+25=160 degrees. The thumb is opposed and axially shortened to its actual target over the folded index/middle fingers, rather than using target direction alone. " +
                "Smooth spatial/influence masks preserve the wrist and soften the thumb web. Morph normal/tangent deltas follow the local numerical deformation Jacobian.",
            use = "Independent review mesh only. Set Grip_R and Grip_L to 100 for closed fists; 75 is a separately authored weapon-grip frame, not merely 75 percent of the fist vertex displacement. " +
                "The builder does not modify any renderer/prefab/controller or apply the mesh in game. The root review-prefab builder owns default weights and visual QA. " +
                "This 24-bone rig has no finger joints; these are bounded mesh morphs, not added finger animation or a new rig.",
            vertexCount = mesh.vertexCount, triangles = mesh.triangles.Length / 3
        };
        try
        {
            AddHand(mesh, source, "RightHand", "Grip_R", -1);
            AddHand(mesh, source, "LeftHand", "Grip_L", 1);
            ValidateStreams(original, mesh);
            LastReport.blendShapes = mesh.blendShapeCount;
            LastReport.sourceStreamsPreserved = true;
            LastReport.success = true;
            return mesh;
        }
        catch
        {
            Object.DestroyImmediate(mesh);
            throw;
        }
    }

    sealed class HandMap
    {
        public Matrix4x4 toHand, toMesh;
        public float metresPerUnit, sign;
        public Vector3[] points;
        public float[] influence, lengths;
        public Vector3 thumbPivot, originalThumbAxis;
        // Measured v006 finger region spans approximately -.060 to +.047m across hand Z.
        public readonly float[] fingerColumns = { -.040f, -.019f, .002f, .023f };

        public float FingerLength(float z)
        {
            if (z <= fingerColumns[0]) return lengths[0];
            for (int index = 1; index < fingerColumns.Length; index++)
                if (z <= fingerColumns[index])
                    return Mathf.Lerp(lengths[index - 1], lengths[index], Smooth((z - fingerColumns[index - 1]) /
                        (fingerColumns[index] - fingerColumns[index - 1])));
            return lengths[lengths.Length - 1];
        }
    }

    sealed class Flexion
    {
        public float[] x = new float[ArcSamples + 1], y = new float[ArcSamples + 1], angle = new float[ArcSamples + 1];
        public Quaternion thumbRotation;
        public Vector3 thumbTarget;
        public float totalDegrees, thumbAxialScale, compactLength, compactWidth;

        public Flexion(HandMap map, bool fist)
        {
            float mcp = fist ? 85 : 65, pip = fist ? 105 : 70, dip = fist ? 70 : 25;
            totalDegrees = mcp + pip + dip;
            compactLength = fist ? .65f : .25f;
            compactWidth = fist ? .88f : .95f;
            for (int index = 0; index <= ArcSamples; index++)
            {
                float t = index / (float)ArcSamples;
                angle[index] = (mcp * Smooth(t / .20f) + pip * Smooth((t - .25f) / .35f) +
                    dip * Smooth((t - .60f) / .40f)) * Mathf.Deg2Rad;
                if (index == 0) continue;
                float midpoint = (angle[index - 1] + angle[index]) * .5f;
                x[index] = x[index - 1] + Mathf.Sin(midpoint) / ArcSamples;
                y[index] = y[index - 1] + Mathf.Cos(midpoint) / ArcSamples;
            }
            // Full fist: oppose the thumb across the returned distal pads (.04-.07m),
            // not beside the MCP ridge. The separate 75-weight weapon opening is preserved.
            thumbTarget = fist ? new Vector3(map.sign * .025f, .055f, .005f) : new Vector3(map.sign * .043f, .090f, .020f);
            thumbRotation = Quaternion.FromToRotation(map.originalThumbAxis, thumbTarget - map.thumbPivot);
            thumbAxialScale = (thumbTarget-map.thumbPivot).magnitude / map.originalThumbAxis.magnitude;
        }

        public void Arc(float t, out float forward, out float side, out float radians)
        {
            float index = Mathf.Clamp01(t) * ArcSamples;
            int first = Mathf.Min(ArcSamples - 1, Mathf.FloorToInt(index));
            float fraction = index - first;
            side = Mathf.Lerp(x[first], x[first + 1], fraction);
            forward = Mathf.Lerp(y[first], y[first + 1], fraction);
            radians = Mathf.Lerp(angle[first], angle[first + 1], fraction);
        }
    }

    static void AddHand(Mesh mesh, SkinnedMeshRenderer source, string boneName, string shapeName, float sign)
    {
        int hand = Array.FindIndex(source.bones, bone => bone && bone.name == boneName);
        if (hand < 0) throw new InvalidOperationException("Source hand bone missing: " + boneName);
        var vertices = mesh.vertices;
        var map = new HandMap
        {
            toHand = mesh.bindposes[hand], sign = sign,
            metresPerUnit = source.bones[hand].TransformVector(Vector3.up).magnitude,
            influence = Influences(mesh, hand), points = new Vector3[vertices.Length], lengths = new float[4],
            thumbPivot = new Vector3(0, .052f, .036f)
        };
        map.toMesh = map.toHand.inverse;
        if (map.metresPerUnit < .000001f) throw new InvalidOperationException("Degenerate hand scale.");
        Vector3 thumbTip = new Vector3(0, .11f, .09f);
        float furthestThumb = float.NegativeInfinity;
        for (int index = 0; index < vertices.Length; index++)
        {
            Vector3 p = map.toHand.MultiplyPoint3x4(vertices[index]) * map.metresPerUnit;
            map.points[index] = p;
            if (map.influence[index] < .55f) continue;
            if (p.z > .065f && p.y > .065f && p.y < .15f && p.z > furthestThumb)
            { thumbTip = p; furthestThumb = p.z; }
            if (map.influence[index] < .9f) continue;
            if (p.y <= KnuckleY || p.z >= .047f) continue;
            int column = 0;
            for (int next = 1; next < map.fingerColumns.Length; next++)
                if (Mathf.Abs(p.z - map.fingerColumns[next]) < Mathf.Abs(p.z - map.fingerColumns[column])) column = next;
            map.lengths[column] = Mathf.Max(map.lengths[column], p.y - KnuckleY);
        }
        if (float.IsNegativeInfinity(furthestThumb))
        {
            DumpSelection(map, boneName, "Thumb opposition region was not found");
            throw new InvalidOperationException("Thumb opposition region was not found: " + boneName);
        }
        map.originalThumbAxis = thumbTip - map.thumbPivot;
        for (int index = 0; index < map.lengths.Length; index++)
            if (map.lengths[index] < .035f || map.lengths[index] > .14f)
            {
                DumpSelection(map, boneName, "Unexpected finger column length: " + index + " = " + map.lengths[index]);
                throw new InvalidOperationException("Unexpected finger column length on " + boneName + ": " + index + " = " + map.lengths[index]);
            }
        foreach (bool fist in new[] { false, true })
            AddFrame(mesh, map, new Flexion(map, fist), boneName, shapeName, fist ? 100 : 75);
    }

    static void AddFrame(Mesh mesh, HandMap map, Flexion flexion, string boneName, string shapeName, float weight)
    {
        var original = mesh.vertices; var normals = mesh.normals; var tangents = mesh.tangents;
        var delta = new Vector3[original.Length]; var deltaNormal = new Vector3[original.Length]; var deltaTangent = new Vector3[original.Length];
        var report = new ShapeReport
        {
            hand = boneName, shape = shapeName, frameWeight = weight, totalFingerFlexionDegrees = flexion.totalDegrees,
            fingerLengthsMetres = (float[])map.lengths.Clone(), thumbTipTarget = flexion.thumbTarget,
            closedMinimum = Vector3.one * float.PositiveInfinity, closedMaximum = Vector3.one * float.NegativeInfinity
        };
        for (int index = 0; index < original.Length; index++)
        {
            if (map.influence[index] < .55f) continue;
            Vector3 p = map.points[index];
            Vector3 q = Deform(p, map.influence[index], map, flexion);
            if (!Finite(q)) throw new InvalidOperationException("Non-finite hand deformation.");
            if (map.influence[index] > .9f)
            {
                report.closedMinimum = Vector3.Min(report.closedMinimum, q);
                report.closedMaximum = Vector3.Max(report.closedMaximum, q);
            }
            Vector3 displacement = q - p;
            if (displacement.sqrMagnitude < 1e-14f) continue;
            delta[index] = map.toMesh.MultiplyVector(displacement / map.metresPerUnit);
            report.changedVertices++;
            if (ThumbMask(p) > .5f)
            {
                report.thumbVertices++;
                report.thumbMinimumHandInfluence = Mathf.Min(report.thumbMinimumHandInfluence, map.influence[index]);
                report.thumbMaximumHandInfluence = Mathf.Max(report.thumbMaximumHandInfluence, map.influence[index]);
            }
            report.maximumDisplacementMetres = Mathf.Max(report.maximumDisplacementMetres, displacement.magnitude);
            const float step = .00005f;
            Vector3 dx = (Deform(p + Vector3.right * step, map.influence[index], map, flexion) - Deform(p - Vector3.right * step, map.influence[index], map, flexion)) / (2 * step);
            Vector3 dy = (Deform(p + Vector3.up * step, map.influence[index], map, flexion) - Deform(p - Vector3.up * step, map.influence[index], map, flexion)) / (2 * step);
            Vector3 dz = (Deform(p + Vector3.forward * step, map.influence[index], map, flexion) - Deform(p - Vector3.forward * step, map.influence[index], map, flexion)) / (2 * step);
            var jacobian = Matrix4x4.identity;
            jacobian.SetColumn(0, new Vector4(dx.x, dx.y, dx.z, 0));
            jacobian.SetColumn(1, new Vector4(dy.x, dy.y, dy.z, 0));
            jacobian.SetColumn(2, new Vector4(dz.x, dz.y, dz.z, 0));
            if (normals.Length == original.Length && Mathf.Abs(jacobian.determinant) > .00001f)
            {
                Vector3 handNormal = map.toMesh.transpose.MultiplyVector(normals[index]).normalized;
                Vector3 bent = jacobian.inverse.transpose.MultiplyVector(handNormal).normalized;
                Vector3 meshNormal = map.toHand.transpose.MultiplyVector(bent).normalized;
                deltaNormal[index] = meshNormal - normals[index];
            }
            if (tangents.Length == original.Length)
            {
                Vector3 tangent = new Vector3(tangents[index].x, tangents[index].y, tangents[index].z);
                Vector3 bent = jacobian.MultiplyVector(map.toHand.MultiplyVector(tangent));
                deltaTangent[index] = map.toMesh.MultiplyVector(bent).normalized - tangent;
            }
        }
        if (report.changedVertices < 150 || report.thumbVertices < 20)
            throw new InvalidOperationException("Finger/thumb selection too small: " + boneName);
        mesh.AddBlendShapeFrame(shapeName, weight, delta, deltaNormal, deltaTangent);
        LastReport.shapes.Add(report);
    }

    static Vector3 Deform(Vector3 p, float influence, HandMap map, Flexion flexion)
    {
        // The source right thumb is only ~.80-.845 hand weighted; the left is ~.855-.936.
        // Anatomical selection must not require the almost-rigid influence used for the fingers.
        float thumb = ThumbMask(p);
        float fullInfluence = Mathf.Lerp(.98f, .78f, thumb);
        float skin = Smooth(Mathf.InverseLerp(.55f, fullInfluence, influence)) * Smooth((p.y - .025f) / .035f);
        if (skin <= 0) return p;
        float finger = (1 - thumb) * Smooth((p.y - .050f) / .020f);
        Vector3 result = p;
        if (finger > 0 && p.y > KnuckleY)
        {
            float length = map.FingerLength(p.z), t = (p.y - KnuckleY) / length;
            float closedLength = Mathf.Lerp(length, .095f, flexion.compactLength);
            flexion.Arc(t, out float y, out float x, out float angle);
            Vector3 curled = new Vector3(map.sign * x * closedLength + p.x * Mathf.Lerp(1, .65f, Smooth(t)) * Mathf.Cos(angle),
                KnuckleY + y * closedLength - map.sign * p.x * Mathf.Lerp(1, .65f, Smooth(t)) * Mathf.Sin(angle),
                p.z * Mathf.Lerp(1, flexion.compactWidth, Smooth(t)));
            result += (curled - p) * finger;
        }
        if (thumb > 0)
        {
            Vector3 offset=p-map.thumbPivot, axis=map.originalThumbAxis.normalized;
            Vector3 axial=axis*Vector3.Dot(offset,axis);
            Vector3 opposed = map.thumbPivot + flexion.thumbRotation * (offset-axial+axial*flexion.thumbAxialScale);
            result += (opposed - p) * thumb;
        }
        return Vector3.LerpUnclamped(p, result, skin);
    }

    static float ThumbMask(Vector3 p) => Smooth((p.z - .038f) / .027f) * (1 - Smooth((p.y - .135f) / .025f));
    static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
    static bool Finite(Vector3 v) => !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));

    [Serializable] sealed class SelectionPoint { public Vector3 handMetres; public float handInfluence; public bool thumbCandidate; }
    [Serializable] sealed class SelectionDump { public string hand, reason; public float metresPerUnit; public List<SelectionPoint> points = new List<SelectionPoint>(); }
    static void DumpSelection(HandMap map, string bone, string reason)
    {
        var dump = new SelectionDump { hand = bone, reason = reason, metresPerUnit = map.metresPerUnit };
        for (int index = 0; index < map.points.Length; index++)
            if (map.influence[index] >= .5f)
            {
                Vector3 p = map.points[index];
                dump.points.Add(new SelectionPoint
                {
                    handMetres = p, handInfluence = map.influence[index],
                    thumbCandidate = p.z > .065f && p.y > .065f && p.y < .15f
                });
            }
        string path = "ArtSources/Characters/Protagonist01/v006-chibi-cap/hand-selection-" + bone + ".json";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, JsonUtility.ToJson(dump, true));
        Debug.Log("CHIBI_CAP_HAND_SELECTION_DIAGNOSTIC " + path);
    }

    static float[] Influences(Mesh mesh, int bone)
    {
        var result = new float[mesh.vertexCount];
        using (var counts = mesh.GetBonesPerVertex())
        using (var weights = mesh.GetAllBoneWeights())
        {
            int offset = 0;
            for (int vertex = 0; vertex < counts.Length; vertex++)
                for (int index = 0; index < counts[vertex]; index++)
                {
                    var weight = weights[offset++];
                    if (weight.boneIndex == bone) result[vertex] += weight.weight;
                }
        }
        return result;
    }

    static void ValidateStreams(Mesh source, Mesh target)
    {
        if (!source.vertices.SequenceEqual(target.vertices) || !source.normals.SequenceEqual(target.normals) ||
            !source.tangents.SequenceEqual(target.tangents) || !source.bindposes.SequenceEqual(target.bindposes) ||
            !source.triangles.SequenceEqual(target.triangles) || source.subMeshCount != target.subMeshCount)
            throw new InvalidOperationException("Hand polish changed an original mesh stream.");
        for (int channel = 0; channel < 8; channel++)
        {
            var a = new List<Vector4>(); var b = new List<Vector4>();
            source.GetUVs(channel, a); target.GetUVs(channel, b);
            if (!a.SequenceEqual(b)) throw new InvalidOperationException("Hand polish changed UV" + channel);
        }
        using (var a = source.GetAllBoneWeights())
        using (var b = target.GetAllBoneWeights())
        {
            if (a.Length != b.Length) throw new InvalidOperationException("Hand polish changed skin weight count.");
            for (int index = 0; index < a.Length; index++)
                if (a[index].boneIndex != b[index].boneIndex || a[index].weight != b[index].weight)
                    throw new InvalidOperationException("Hand polish changed skin weights.");
        }
    }
}
#endif
