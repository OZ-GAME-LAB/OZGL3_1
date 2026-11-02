#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// Study-only assembly of an externally authored closed left hand. Removes only
/// the independently UV/color/connectivity-validated left skin faces; retains
/// every original vertex stream and every remaining source triangle verbatim.
/// </summary>
public static partial class MageStudyClosedHandAssembler
{
    // Existing cuff faces sharing the inspected nineteen-edge skin boundary.
    // Their UV edges contain peach pixels; geometry/weights stay intact.
    // See cuff-boundary-face-selection.json for source hashes and atlas samples.
    static readonly HashSet<int> CuffFaces=new HashSet<int>{737,739,742,745,2842,2844,2848,2875,3225,3230,3270,3274,3275,5634};
    public const string ShapeName = "StudyStaffGrip_L";
    public const float ShaftRadius = .01744f;
    public const float WeightQuantizationTolerance = 2f / 65535f;
    public static readonly Vector3 ShaftCenterMetres = new Vector3(.028f, .068f, 0f);
    public static Vector3 StaffLocalPosition => new Vector3(.028f, .069510215f, 0f);

    [Serializable] public sealed class HandGeometry
    {
        public Vector3[] vertices, normals;
        public Vector2[] uv;
        public int[] triangles;
    }

    [Serializable] sealed class SkinSelection
    {
        public int sourceVertexCount, sourceTriangleCount;
        public string sourcePositionSha256, sourceUv0Sha256, sourceIndexSha256;
        public string sourceAtlasPath, sourceAtlasSha256, selectionMethod;
        public int[] selectedTriangleIndices;
        public Vector2 skinUV;
    }

    [Serializable] public sealed class AssemblyReport
    {
        public string geometryPath, selectionPath, selectionMethod;
        public int originalVertices, removedSkinTriangles, addedVertices, addedTriangles;
        public Vector2 skinUV;
        public float maxAbsoluteWeightDelta, weightQuantizationTolerance = WeightQuantizationTolerance;
        public int verticesWithWeightLayoutChanges;
        public bool handUsesProvidedUV;
        public int subMeshCount = 3, recoloredCuffTriangles = 14;
        public bool originalStreamsPreserved, requiresVisualReview = true;
    }

    struct WeightAudit
    {
        public float maximumDelta;
        public int layoutChanges;
    }

    public static AssemblyReport LastReport { get; private set; }

    /// <summary>
    /// JSON vectors are {x,y,z} objects in LeftHand-local metres. A sibling
    /// left-wrist-boundary.json binds the inspected selection to exact source
    /// positions, UV0, indices, and atlas hashes. Returns an unsaved mesh clone.
    /// Set Grip_L=0. StudyStaffGrip_L is a zero compatibility shape, not a morph.
    /// </summary>
    public static Mesh Build(SkinnedMeshRenderer source, string jsonPath)
    {
        if (!source || !source.sharedMesh)
            throw new ArgumentException("A source skinned mesh is required.", nameof(source));
        if (string.IsNullOrEmpty(jsonPath) || !File.Exists(jsonPath))
            throw new FileNotFoundException("Closed left-hand geometry JSON is missing.", jsonPath);
        Mesh original = source.sharedMesh;
        if (original.subMeshCount != 1)
            throw new InvalidOperationException("Study assembly expects the native single atlas material.");
        int hand = Array.FindIndex(source.bones, bone => bone && bone.name == "LeftHand");
        if (hand < 0 || hand >= original.bindposes.Length)
            throw new InvalidOperationException("LeftHand bind pose is missing.");
        float metresPerUnit = source.bones[hand].TransformVector(Vector3.up).magnitude;
        if (Mathf.Abs(metresPerUnit - 1f) > .001f)
            throw new InvalidOperationException("Study staff placement requires native unit hand scale.");

        string selectionPath = Path.Combine(Path.GetDirectoryName(jsonPath), "left-wrist-boundary.json");
        if (!File.Exists(selectionPath))
            throw new FileNotFoundException("Inspected left skin selection JSON is missing.", selectionPath);
        SkinSelection selection = JsonUtility.FromJson<SkinSelection>(File.ReadAllText(selectionPath));
        HandGeometry handMesh = JsonUtility.FromJson<HandGeometry>(File.ReadAllText(jsonPath));
        ValidateSelection(original, selection);
        ValidateGeometry(handMesh);

        int originalCount = original.vertexCount;
        int count = originalCount + handMesh.vertices.Length;
        var removed = new HashSet<int>(selection.selectedTriangleIndices);
        Matrix4x4 toHand = original.bindposes[hand], toMesh = toHand.inverse;
        var vertices = original.vertices.ToList();
        var normals = original.normals.ToList();
        var tangents = original.tangents.ToList();
        if (normals.Count != originalCount || tangents.Count != originalCount)
            throw new InvalidOperationException("Native source normal/tangent streams are required.");
        for (int i = 0; i < handMesh.vertices.Length; i++)
        {
            vertices.Add(toMesh.MultiplyPoint3x4(handMesh.vertices[i] / metresPerUnit));
            Vector3 normal = toHand.transpose.MultiplyVector(handMesh.normals[i]).normalized;
            normals.Add(normal);
            Vector3 tangent = Vector3.Cross(normal, Vector3.up);
            if (tangent.sqrMagnitude < .01f) tangent = Vector3.Cross(normal, Vector3.right);
            tangent.Normalize();
            tangents.Add(new Vector4(tangent.x, tangent.y, tangent.z, 1f));
        }
        var bodyIndices = new List<int>();
        int[] sourceIndices = original.triangles;
        for (int face = 0; face < sourceIndices.Length / 3; face++)
            if (!removed.Contains(face)&&!CuffFaces.Contains(face))
                for (int corner = 0; corner < 3; corner++) bodyIndices.Add(sourceIndices[face * 3 + corner]);
        int[] handIndices = handMesh.triangles.Select(index => originalCount + index).ToArray();

        Mesh result = Object.Instantiate(original);
        result.name = original.name + "_MageStudyClosedLeftHand";
        try
        {
            result.ClearBlendShapes();
            if (count > 65535) result.indexFormat = IndexFormat.UInt32;
            result.vertices = vertices.ToArray();
            result.normals = normals.ToArray();
            result.tangents = tangents.ToArray();
            CopyUVs(original, result, count, selection.skinUV, handMesh.uv);
            CopyColors(original, result, count);
            CopyWeights(original, result, handMesh.vertices.Length, hand);
            // Body keeps its source atlas in slot zero; the new hand's baked map
            // belongs to material slot one. Caller supplies both renderer materials.
            result.subMeshCount = 3;
            result.SetTriangles(bodyIndices, 0);
            result.SetTriangles(handIndices, 1);
            result.SetTriangles(CuffFaces.OrderBy(i=>i).SelectMany(i=>new[]{sourceIndices[i*3],sourceIndices[i*3+1],sourceIndices[i*3+2]}).ToArray(),2);
            CopyBlendShapes(original, result, count);
            result.RecalculateBounds();
            WeightAudit weightAudit = ValidateOriginalPrefix(original, result);
            LastReport = new AssemblyReport
            {
                geometryPath = jsonPath, selectionPath = selectionPath,
                selectionMethod = selection.selectionMethod, originalVertices = originalCount,
                removedSkinTriangles = removed.Count, addedVertices = handMesh.vertices.Length,
                addedTriangles = handMesh.triangles.Length / 3, skinUV = selection.skinUV,
                maxAbsoluteWeightDelta = weightAudit.maximumDelta,
                verticesWithWeightLayoutChanges = weightAudit.layoutChanges,
                handUsesProvidedUV = handMesh.uv != null && handMesh.uv.Length != 0,
                originalStreamsPreserved = true
            };
            return result;
        }
        catch
        {
            Object.DestroyImmediate(result);
            throw;
        }
    }

    static void ValidateSelection(Mesh source, SkinSelection selection)
    {
        if (selection == null || selection.sourceVertexCount != source.vertexCount ||
            selection.sourceTriangleCount != source.triangles.Length / 3)
            throw new InvalidOperationException("Left skin selection does not match source counts.");
        // This is the inspected LEFT component, not a spatial cutoff or a mirrored
        // right-hand selection. Dark sleeve centroids were excluded before the
        // largest 1um-welded edge-connected skin component was selected.
        if (selection.selectedTriangleIndices == null || selection.selectedTriangleIndices.Length != 467 ||
            selection.selectedTriangleIndices.Distinct().Count() != 467 ||
            selection.selectedTriangleIndices.Any(i => i < 0 || i >= selection.sourceTriangleCount))
            throw new InvalidOperationException("Expected the inspected 467-face left skin component.");
        var positions = source.vertices.SelectMany(p => new[] { p.x, p.y, p.z }).ToArray();
        var uv = source.uv.SelectMany(p => new[] { p.x, p.y }).ToArray();
        if (!SameHash(Hash(positions), selection.sourcePositionSha256) ||
            !SameHash(Hash(uv), selection.sourceUv0Sha256) ||
            !SameHash(Hash(source.triangles), selection.sourceIndexSha256))
            throw new InvalidOperationException("Source geometry/UV/index streams differ from inspected left skin selection.");
        if (string.IsNullOrEmpty(selection.sourceAtlasPath) || !File.Exists(selection.sourceAtlasPath) ||
            !SameHash(Hash(File.ReadAllBytes(selection.sourceAtlasPath)), selection.sourceAtlasSha256))
            throw new InvalidOperationException("The inspected skin/sleeve atlas has changed.");
    }

    static void ValidateGeometry(HandGeometry hand)
    {
        if (hand == null || hand.vertices == null || hand.normals == null || hand.triangles == null ||
            hand.vertices.Length < 4 || hand.vertices.Length != hand.normals.Length ||
            hand.triangles.Length < 12 || hand.triangles.Length % 3 != 0)
            throw new InvalidOperationException("Closed hand JSON has incomplete geometry streams.");
        for (int i = 0; i < hand.vertices.Length; i++)
            if (!Finite(hand.vertices[i]) || !Finite(hand.normals[i]) || hand.normals[i].sqrMagnitude < .1f)
                throw new InvalidOperationException("Closed hand contains a non-finite vertex or invalid normal.");
        if (hand.triangles.Any(i => i < 0 || i >= hand.vertices.Length))
            throw new InvalidOperationException("Closed hand has an out-of-range triangle index.");
        if (hand.uv != null && hand.uv.Length != 0)
        {
            if (hand.uv.Length != hand.vertices.Length)
                throw new InvalidOperationException("Closed hand UV count must match its vertex count.");
            foreach (Vector2 uv in hand.uv)
                if (float.IsNaN(uv.x) || float.IsNaN(uv.y) || float.IsInfinity(uv.x) || float.IsInfinity(uv.y))
                    throw new InvalidOperationException("Closed hand contains a non-finite UV.");
        }
    }

    static void CopyUVs(Mesh source, Mesh target, int count, Vector2 skinUV, Vector2[] handUV)
    {
        for (int channel = 0; channel < 8; channel++)
        {
            var attribute = (VertexAttribute)((int)VertexAttribute.TexCoord0 + channel);
            if (!source.HasVertexAttribute(attribute)) continue;
            var values = new List<Vector4>();
            source.GetUVs(channel, values);
            for (int vertex = source.vertexCount; vertex < count; vertex++)
            {
                Vector2 uv = handUV != null && handUV.Length != 0 ? handUV[vertex - source.vertexCount] : skinUV;
                values.Add(channel == 0 ? new Vector4(uv.x, uv.y, 0f, 0f) : Vector4.zero);
            }
            int dimension = source.GetVertexAttributeDimension(attribute);
            if (dimension == 2) target.SetUVs(channel, values.Select(v => new Vector2(v.x, v.y)).ToList());
            else if (dimension == 3) target.SetUVs(channel, values.Select(v => new Vector3(v.x, v.y, v.z)).ToList());
            else target.SetUVs(channel, values);
        }
    }

    static void CopyColors(Mesh source, Mesh target, int count)
    {
        if (!source.HasVertexAttribute(VertexAttribute.Color)) return;
        if (source.GetVertexAttributeFormat(VertexAttribute.Color) == VertexAttributeFormat.UNorm8)
        {
            var colors = source.colors32.ToList();
            while (colors.Count < count) colors.Add(new Color32(255, 255, 255, 255));
            target.colors32 = colors.ToArray();
        }
        else
        {
            var colors = source.colors.ToList();
            while (colors.Count < count) colors.Add(Color.white);
            target.colors = colors.ToArray();
        }
    }

    static void CopyWeights(Mesh source, Mesh target, int addedCount, int hand)
    {
        using (var oldCounts = source.GetBonesPerVertex())
        using (var oldWeights = source.GetAllBoneWeights())
        {
            var counts = new NativeArray<byte>(source.vertexCount + addedCount, Allocator.Temp);
            var weights = new NativeArray<BoneWeight1>(oldWeights.Length + addedCount, Allocator.Temp);
            try
            {
                NativeArray<byte>.Copy(oldCounts, counts, oldCounts.Length);
                NativeArray<BoneWeight1>.Copy(oldWeights, weights, oldWeights.Length);
                for (int i = 0; i < addedCount; i++)
                {
                    counts[source.vertexCount + i] = 1;
                    weights[oldWeights.Length + i] = new BoneWeight1 { boneIndex = hand, weight = 1f };
                }
                target.SetBoneWeights(counts, weights);
            }
            finally { counts.Dispose(); weights.Dispose(); }
        }
    }

    static void CopyBlendShapes(Mesh source, Mesh target, int count)
    {
        bool hasCompatibilityShape = false;
        for (int shape = 0; shape < source.blendShapeCount; shape++)
        {
            string name = source.GetBlendShapeName(shape);
            bool compatibility = name == ShapeName;
            hasCompatibilityShape |= compatibility;
            for (int frame = 0; frame < source.GetBlendShapeFrameCount(shape); frame++)
            {
                var positions = new Vector3[source.vertexCount];
                var normals = new Vector3[source.vertexCount];
                var tangents = new Vector3[source.vertexCount];
                if (!compatibility) source.GetBlendShapeFrameVertices(shape, frame, positions, normals, tangents);
                Array.Resize(ref positions, count);
                Array.Resize(ref normals, count);
                Array.Resize(ref tangents, count);
                target.AddBlendShapeFrame(name, source.GetBlendShapeFrameWeight(shape, frame), positions, normals, tangents);
            }
        }
        if (!hasCompatibilityShape)
            target.AddBlendShapeFrame(ShapeName, 100f, new Vector3[count], new Vector3[count], new Vector3[count]);
    }

    static WeightAudit ValidateOriginalPrefix(Mesh source, Mesh target)
    {
        int n = source.vertexCount;
        if (!source.vertices.SequenceEqual(target.vertices.Take(n)) ||
            !source.normals.SequenceEqual(target.normals.Take(n)) ||
            !source.tangents.SequenceEqual(target.tangents.Take(n)) ||
            !source.bindposes.SequenceEqual(target.bindposes))
            throw new InvalidOperationException("Assembly changed an original base stream.");
        for (int channel = 0; channel < 8; channel++)
        {
            var a = new List<Vector4>(); var b = new List<Vector4>();
            source.GetUVs(channel, a); target.GetUVs(channel, b);
            if (a.Count != 0 && !a.SequenceEqual(b.Take(n)))
                throw new InvalidOperationException("Assembly changed an original UV stream.");
        }
        // SetBoneWeights can repack, reorder ties, discard zero entries and quantize
        // a weight. Flat-array offsets are therefore not a preservation criterion.
        // Compare the total influence of EACH bone at EACH original vertex instead.
        var audit = new WeightAudit();
        using (var sourceCounts = source.GetBonesPerVertex())
        using (var targetCounts = target.GetBonesPerVertex())
        using (var sourceWeights = source.GetAllBoneWeights())
        using (var targetWeights = target.GetAllBoneWeights())
        {
            if (sourceCounts.Length != n || targetCounts.Length < n)
                throw new InvalidOperationException("Assembly lost original per-vertex weight counts.");
            int sourceOffset = 0, targetOffset = 0;
            var a = new Dictionary<int, float>();
            var b = new Dictionary<int, float>();
            for (int vertex = 0; vertex < n; vertex++)
            {
                a.Clear(); b.Clear();
                int aCount = sourceCounts[vertex], bCount = targetCounts[vertex];
                if (sourceOffset + aCount > sourceWeights.Length || targetOffset + bCount > targetWeights.Length)
                    throw new InvalidOperationException("Assembly produced inconsistent bone-weight offsets.");
                bool sameLayout = aCount == bCount;
                for (int j = 0; j < aCount; j++)
                {
                    BoneWeight1 weight = sourceWeights[sourceOffset + j];
                    AccumulateWeight(a, weight);
                    if (sameLayout && weight.boneIndex != targetWeights[targetOffset + j].boneIndex)
                        sameLayout = false;
                }
                for (int j = 0; j < bCount; j++)
                    AccumulateWeight(b, targetWeights[targetOffset + j]);
                if (!sameLayout) audit.layoutChanges++;
                foreach (var influence in a)
                {
                    b.TryGetValue(influence.Key, out float actual);
                    audit.maximumDelta = Mathf.Max(audit.maximumDelta, Mathf.Abs(influence.Value - actual));
                }
                foreach (var influence in b)
                    if (!a.ContainsKey(influence.Key))
                        audit.maximumDelta = Mathf.Max(audit.maximumDelta, Mathf.Abs(influence.Value));
                if (audit.maximumDelta > WeightQuantizationTolerance)
                    throw new InvalidOperationException("Assembly changed original bone influence at vertex " +
                        vertex + ": maximum absolute delta " + audit.maximumDelta.ToString("R") +
                        " exceeds quantization tolerance " + WeightQuantizationTolerance.ToString("R") + ".");
                sourceOffset += aCount;
                targetOffset += bCount;
            }
        }
        return audit;
    }

    static void AccumulateWeight(Dictionary<int, float> influences, BoneWeight1 weight)
    {
        if (float.IsNaN(weight.weight) || float.IsInfinity(weight.weight) || weight.weight < 0f)
            throw new InvalidOperationException("Assembly encountered an invalid bone influence.");
        influences.TryGetValue(weight.boneIndex, out float existing);
        influences[weight.boneIndex] = existing + weight.weight;
    }

    static string Hash(Array values)
    {
        var bytes = new byte[Buffer.ByteLength(values)];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        return Hash(bytes);
    }
    static string Hash(byte[] bytes)
    {
        using (var hash = SHA256.Create())
            return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }
    static bool SameHash(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    static bool Finite(Vector3 v) => !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
        float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
}
#endif
