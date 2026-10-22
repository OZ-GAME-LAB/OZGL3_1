#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Preserves the source skin and builds weapon-only closed-hand mesh variants.</summary>
public static class ChibiCylinderGripBuilder
{
    public const string MeshPath = "Assets/Art/Characters/Protagonist01/ChibiMotion_v007/Meshes/Protagonist_Chibi_CylinderGrip.asset";
    public const string SwordMeshPath = "Assets/Art/Characters/Protagonist01/ChibiMotion_v007/Meshes/Protagonist_Chibi_SwordClosedGrip.asset";
    public const string StaffMeshPath = "Assets/Art/Characters/Protagonist01/ChibiMotion_v007/Meshes/Protagonist_Chibi_StaffClosedGrip.asset";
    public const float PropScale = .70f, CylinderGripWeight = 150f, SwordGripWeight = 175f;
    public static readonly Vector3 GripCenterMetres = new Vector3(-.024f, .068f, -.0205f);
    public static readonly Vector3 MountRotationEuler = new Vector3(45, 0, 0);
    public static Vector3 CylinderAxis => Quaternion.Euler(MountRotationEuler) * Vector3.up;
    public static Vector3 RadialY => Quaternion.Euler(MountRotationEuler) * Vector3.back;
    const string SourceMesh = "Assets/Art/Characters/Protagonist01/ChibiCap_v006/Meshes/Protagonist_ChibiCap_Fists.asset";
    const string SourcePrefab = "Assets/Art/Characters/Protagonist01/ChibiCap_v006/Prefabs/Protagonist_ChibiCap_Rig.prefab";
    const string SelectionPath = "ArtSources/Stage01Kit/research/right-hand-skin-selection.json";
    const string ReportPath = "ArtSources/Stage01Kit/research/staff-cylinder-grip-build.json";

    public static string MeshForProp(string prop) => prop == "Sword" ? SwordMeshPath : prop == "Staff" ? StaffMeshPath : MeshPath;

    [MenuItem("Art/Characters/Build v007 Closed Weapon Hands")]
    public static void Build()
    {
        Mesh source = AssetDatabase.LoadAssetAtPath<Mesh>(SourceMesh);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefab);
        if (!source || !prefab) throw new InvalidOperationException("Chibi source mesh or rig is missing.");
        if (!File.Exists(SelectionPath)) throw new FileNotFoundException("Validated UV skin selection is required.", SelectionPath);
        string sourceSha = Hash(SourceMesh);
        var mask = JsonUtility.FromJson<SkinSelection>(File.ReadAllText(SelectionPath));
        if (mask == null || mask.sourceVertexCount != source.vertexCount || mask.sourceTriangleCount != source.triangles.Length / 3 ||
            !string.Equals(mask.sourceMeshSHA256, sourceSha, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Right-hand skin selection does not match the unchanged source mesh.");
        if (mask.removedTriangleIndices == null || mask.removedTriangleIndices.Length < 300 || mask.removedTriangleIndices.Length > 700)
            throw new InvalidOperationException("Unexpected hand skin selection size.");
        var actor = Object.Instantiate(prefab);
        Mesh baseMesh = null, sword = null, staff = null;
        try
        {
            var skin = actor.GetComponentInChildren<SkinnedMeshRenderer>(true);
            int hand = Array.FindIndex(skin.bones, b => b.name == "RightHand");
            if (hand < 0 || skin.bones.Length != source.bindposes.Length) throw new InvalidOperationException("Chibi hand/bindposes mismatch.");
            if (Mathf.Abs(skin.bones[hand].TransformVector(Vector3.up).magnitude - 1) > .001f)
                throw new InvalidOperationException("Expected hand-local physical metres.");
            Matrix4x4 toHand = source.bindposes[hand];
            var weights = source.boneWeights; var positions = source.vertices;
            int changedWeights = 0; float maximumAddedWeight = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                float old = Influence(weights[i], hand); Vector3 p = toHand.MultiplyPoint3x4(positions[i]);
                if (old < .5f || p.y < .025f || p.y > .19f) continue;
                float amount = Mathf.Lerp(old, 1, Smooth((p.y - .025f) / .040f));
                if (amount - old < .000001f) continue;
                weights[i] = Reweight(weights[i], hand, amount);
                changedWeights++; maximumAddedWeight = Mathf.Max(maximumAddedWeight, amount - old);
            }
            // Base uses the original hand and utility75/fist100 records. Failed cylindrical
            // warps, thumb rotations, wrist correctives and subdivision are all removed.
            baseMesh = Object.Instantiate(source); baseMesh.name = "Protagonist_Chibi_BaseFists_v007";
            baseMesh.boneWeights = weights; CopyFrames(baseMesh, source, source.vertexCount);
            int rightShape = source.GetBlendShapeIndex("Grip_R");
            int fullFrame = Enumerable.Range(0, source.GetBlendShapeFrameCount(rightShape)).First(f => Mathf.Abs(source.GetBlendShapeFrameWeight(rightShape, f) - 100) < .01f);
            var fistDelta = new Vector3[source.vertexCount]; source.GetBlendShapeFrameVertices(rightShape, fullFrame, fistDelta, null, null);
            if (mask.boundaryOrderedSourceVertexIDs == null || mask.boundaryOrderedSourceVertexIDs.Length != 19)
                throw new InvalidOperationException("Validated nineteen-edge closed wrist loop is required.");
            ChibiClosedGripGeometry.ConfigureWristBoundary(
                mask.boundaryOrderedSourceVertexIDs.Select(i => toHand.MultiplyPoint3x4(positions[i] + fistDelta[i])).ToArray(),
                mask.boundaryOrderedSourceVertexIDs.Select(i => weights[i]).ToArray());
            var removed = new HashSet<int>(mask.removedTriangleIndices);
            if (removed.Any(i => i < 0 || i >= source.triangles.Length / 3)) throw new InvalidOperationException("Skin face index out of bounds.");
            sword = WeaponVariant(baseMesh, source, removed, toHand.inverse, hand, mask.skinUv, false);
            staff = WeaponVariant(baseMesh, source, removed, toHand.inverse, hand, mask.skinUv, true);
            Mesh savedBase = Save(baseMesh, MeshPath); baseMesh = null;
            Mesh savedSword = Save(sword, SwordMeshPath); sword = null;
            Mesh savedStaff = Save(staff, StaffMeshPath); staff = null;
            AssetDatabase.SaveAssets();
            if (sourceSha != Hash(SourceMesh)) throw new InvalidOperationException("Original chibi source mesh changed unexpectedly.");
            var report = new BuildReport
            {
                source = SourceMesh, sourceSha256 = sourceSha, output = MeshPath, swordOutput = SwordMeshPath, staffOutput = StaffMeshPath,
                sourceUnchanged = true, requiresVisualApproval = true,
                method = "Base preserves original7101 streams/topology and original75/100 records;150/175 use sourceRight100 without failed hand warps. Weapon variants remove only the independently UV-validated right-hand skin component and append a compact authored closed hand with an independently closed dark cuff overlapping inside the source sleeve. Original sleeve/body/left-hand indices remain. Source wrist-boundary data supplies only averaged-weight metadata; its nonplanar skin topology is not stitched into the new cuff. Distal hand uses RightHand only. Fixed neutral, SwordGrip and StaffGrip prefabs select their respective mesh; no animation object-reference mesh keys or gameplay code are connected.",
                skinSelection = SelectionPath, skinSelectionSha256 = Hash(SelectionPath), removedRightHandSkinTriangles = removed.Count,
                skinSelectionMethod = mask.method, skinUv = mask.skinUv,
                originalVertexCount = source.vertexCount, originalTriangleCount = source.triangles.Length / 3,
                baseVertexCount = savedBase.vertexCount, baseTriangleCount = savedBase.triangles.Length / 3,
                swordVertexCount = savedSword.vertexCount, swordTriangleCount = savedSword.triangles.Length / 3,
                staffVertexCount = savedStaff.vertexCount, staffTriangleCount = savedStaff.triangles.Length / 3,
                changedWeightVertices = changedWeights, maximumAddedHandWeight = maximumAddedWeight,
                weightRule = "Retain the prior222-vertex distal RightHand correction: original influence>=.5, bindY.025..19, smoothstepY.025..065 to weight1; other influences renormalize proportionally. Vertices outside this rule and all left-hand weights unchanged. Closed cuff blends averaged original final source-cuff weights toward RightHand; no exact seam stitching. Distal closed-hand vertices use RightHand only.",
                gripCenterMetres = GripCenterMetres, mountRotationEuler = MountRotationEuler,
                axisHandMetres = CylinderAxis, propScale = PropScale,
                cylindricalGripFrameWeight = CylinderGripWeight, swordGripFrameWeight = SwordGripWeight,
                baseMeshSha256 = Hash(MeshPath), swordMeshSha256 = Hash(SwordMeshPath), staffMeshSha256 = Hash(StaffMeshPath)
            };
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)); File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            Debug.Log("CHIBI_CLOSED_GRIP_BUILD_SUCCESS base=" + savedBase.vertexCount + " sword=" + savedSword.vertexCount + " staff=" + savedStaff.vertexCount);
        }
        finally
        {
            if (baseMesh) Object.DestroyImmediate(baseMesh); if (sword) Object.DestroyImmediate(sword); if (staff) Object.DestroyImmediate(staff);
            Object.DestroyImmediate(actor);
        }
    }

    static Mesh WeaponVariant(Mesh baseMesh, Mesh source, HashSet<int> removed, Matrix4x4 handToMesh, int hand, Vector2 skinUv, bool staff)
    {
        if (source.subMeshCount != 1) throw new InvalidOperationException("Expected a single atlas material.");
        var v = baseMesh.vertices.ToList(); var n = baseMesh.normals.ToList(); var uv = baseMesh.uv.ToList();
        var w = baseMesh.boneWeights.ToList(); var triangles = new List<int>(); int[] original = source.triangles;
        for (int f = 0; f < original.Length / 3; f++) if (!removed.Contains(f))
            for (int k = 0; k < 3; k++) triangles.Add(original[f * 3 + k]);
        ChibiClosedGripGeometry.Append(v, n, uv, w, triangles, handToMesh, hand, skinUv, staff);
        if (v.Count <= source.vertexCount || v.Count != n.Count || v.Count != uv.Count || v.Count != w.Count)
            throw new InvalidOperationException("Authored hand has inconsistent streams.");
        var tangent = baseMesh.tangents.ToList();
        for (int i = tangent.Count; i < v.Count; i++)
        {
            Vector3 t = Vector3.Cross(n[i], Vector3.up); if (t.sqrMagnitude < .01f) t = Vector3.Cross(n[i], Vector3.right);
            t.Normalize(); tangent.Add(new Vector4(t.x, t.y, t.z, 1));
        }
        Mesh mesh = Object.Instantiate(baseMesh); mesh.name = staff ? "Protagonist_Chibi_StaffClosedGrip_v007" : "Protagonist_Chibi_SwordClosedGrip_v007";
        mesh.ClearBlendShapes(); if (v.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = v.ToArray(); mesh.normals = n.ToArray(); mesh.tangents = tangent.ToArray();
        mesh.uv = uv.ToArray(); mesh.boneWeights = w.ToArray(); mesh.triangles = triangles.ToArray();
        CopyFrames(mesh, source, v.Count); mesh.RecalculateBounds(); return mesh;
    }

    static void CopyFrames(Mesh target, Mesh source, int count)
    {
        target.ClearBlendShapes(); int right = source.GetBlendShapeIndex("Grip_R");
        int full = Enumerable.Range(0, source.GetBlendShapeFrameCount(right)).First(f => Mathf.Abs(source.GetBlendShapeFrameWeight(right, f) - 100) < .01f);
        for (int s = 0; s < source.blendShapeCount; s++)
        {
            for (int f = 0; f < source.GetBlendShapeFrameCount(s); f++) AddFrame(source, target, s, f, count, source.GetBlendShapeFrameWeight(s, f));
            if (s == right) { AddFrame(source, target, s, full, count, CylinderGripWeight); AddFrame(source, target, s, full, count, SwordGripWeight); }
        }
    }
    static void AddFrame(Mesh source, Mesh target, int shape, int frame, int count, float weight)
    {
        var a = new Vector3[source.vertexCount]; var b = new Vector3[source.vertexCount]; var c = new Vector3[source.vertexCount];
        source.GetBlendShapeFrameVertices(shape, frame, a, b, c);
        if (count != a.Length) { Array.Resize(ref a, count); Array.Resize(ref b, count); Array.Resize(ref c, count); }
        target.AddBlendShapeFrame(source.GetBlendShapeName(shape), weight, a, b, c);
    }
    static Mesh Save(Mesh mesh, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)); var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (!saved) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
        EditorUtility.CopySerialized(mesh, saved); EditorUtility.SetDirty(saved); Object.DestroyImmediate(mesh); return saved;
    }
    static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3 - 2 * x); }
    static float Influence(BoneWeight w, int b) => (w.boneIndex0 == b ? w.weight0 : 0) + (w.boneIndex1 == b ? w.weight1 : 0) + (w.boneIndex2 == b ? w.weight2 : 0) + (w.boneIndex3 == b ? w.weight3 : 0);
    static BoneWeight Reweight(BoneWeight w, int hand, float value)
    {
        float old = Influence(w, hand), scale = old < .999999f ? (1 - value) / (1 - old) : 0;
        w.weight0 = w.boneIndex0 == hand ? value : w.weight0 * scale; w.weight1 = w.boneIndex1 == hand ? value : w.weight1 * scale;
        w.weight2 = w.boneIndex2 == hand ? value : w.weight2 * scale; w.weight3 = w.boneIndex3 == hand ? value : w.weight3 * scale; return w;
    }
    static string Hash(string p) { using (var s = SHA256.Create()) return BitConverter.ToString(s.ComputeHash(File.ReadAllBytes(p))).Replace("-", "").ToLowerInvariant(); }
    [Serializable] sealed class SkinSelection
    {
        public string sourceMeshSHA256, method; public int sourceVertexCount, sourceTriangleCount;
        public int[] removedTriangleIndices, boundaryOrderedSourceVertexIDs; public Vector2 skinUv;
    }
    [Serializable] sealed class BuildReport
    {
        public string source, sourceSha256, output, swordOutput, staffOutput, method, skinSelection, skinSelectionSha256, skinSelectionMethod, weightRule;
        public string baseMeshSha256, swordMeshSha256, staffMeshSha256; public bool sourceUnchanged, requiresVisualApproval;
        public int originalVertexCount, originalTriangleCount, baseVertexCount, baseTriangleCount, swordVertexCount, swordTriangleCount, staffVertexCount, staffTriangleCount;
        public int removedRightHandSkinTriangles, changedWeightVertices; public float maximumAddedHandWeight;
        public Vector2 skinUv; public Vector3 gripCenterMetres, mountRotationEuler, axisHandMetres;
        public float propScale, cylindricalGripFrameWeight, swordGripFrameWeight;
    }
}
#endif
