#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Copies the original skin and bends only rigidly weighted finger tips after the knuckles.</summary>
public static class ProtagonistGripBuilder
{
    private const string Root = "Assets/Art/Characters/Protagonist01/Runtime";
    private const string SourcePath = Root + "/Protagonist01_Rig.fbx";
    private const string MeshPath = Root + "/Protagonist01_GripMesh.asset";
    // T-pose hand-space X axes face opposite ways. Flip either sign after side-view QA if needed.
    public const float RightCurlSign = -1f;
    public const float LeftCurlSign = 1f;
    public const float CurlDegrees = 110f;
    public const float KnuckleStartMetres = .115f;
    private const float ThumbSideMetres = .047f;

    [MenuItem("Project M/Art/Build Protagonist Hand Grips")]
    public static void Build()
    {
        GameObject sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
        if (sourceAsset == null) throw new InvalidOperationException("Original protagonist rig is missing.");
        GameObject instance = Object.Instantiate(sourceAsset);
        instance.hideFlags = HideFlags.HideAndDontSave;
        Mesh generated = null;
        try
        {
            SkinnedMeshRenderer skin = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skin == null || skin.sharedMesh == null) throw new InvalidOperationException("Original rig has no skin.");
            Mesh original = skin.sharedMesh;
            generated = Object.Instantiate(original);
            generated.name = "Protagonist01_GripMesh";
            generated.ClearBlendShapes();
            var reports = new List<HandReport>();
            reports.Add(AddGrip(generated, skin, "RightHand", "Grip_R", RightCurlSign));
            reports.Add(AddGrip(generated, skin, "LeftHand", "Grip_L", LeftCurlSign));
            ValidatePreservedStreams(original, generated);
            Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (saved == null) { AssetDatabase.CreateAsset(generated, MeshPath); saved = generated; generated = null; }
            else { EditorUtility.CopySerialized(generated, saved); EditorUtility.SetDirty(saved); }

            foreach (string prefabPath in new[] { Root + "/Protagonist01_Player.prefab", Root + "/Protagonist01_Visual.prefab" })
            {
                GameObject character = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    SkinnedMeshRenderer target = character.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    if (target == null || target.bones.Length != skin.bones.Length)
                        throw new InvalidOperationException("Unexpected target skin in " + prefabPath);
                    target.sharedMesh = saved;
                    var grip = character.GetComponent<ProtagonistHandGrip>() ?? character.AddComponent<ProtagonistHandGrip>();
                    grip.Configure(target);
                    PrefabUtility.SaveAsPrefabAsset(character, prefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(character); }
            }
            Directory.CreateDirectory("ArtSources/Stage01Kit");
            File.WriteAllText("ArtSources/Stage01Kit/hand-grip-build.json", JsonUtility.ToJson(new BuildReport
            {
                source = SourcePath, mesh = MeshPath, vertexCount = saved.vertexCount,
                triangles = saved.triangles.Length / 3, blendShapes = saved.blendShapeCount,
                curlDegrees = CurlDegrees, knuckleStartMetres = KnuckleStartMetres, hands = reports.ToArray()
            }, true));
            AssetDatabase.SaveAssets();
            Debug.Log("PROTAGONIST_GRIP_BUILD_SUCCESS: shared original skin + Grip_R/Grip_L; skinning/UV/material topology preserved.");
        }
        finally
        {
            if (generated != null) Object.DestroyImmediate(generated);
            Object.DestroyImmediate(instance);
        }
    }

    private static HandReport AddGrip(Mesh mesh, SkinnedMeshRenderer skin, string boneName, string shapeName, float sign)
    {
        int boneIndex = Array.FindIndex(skin.bones, bone => bone != null && bone.name == boneName);
        if (boneIndex < 0) throw new InvalidOperationException("Missing hand bone: " + boneName);
        Matrix4x4 toHand = mesh.bindposes[boneIndex];
        Matrix4x4 toMesh = toHand.inverse;
        Transform boneTransform = skin.bones[boneIndex];
        // FBX bones can retain a 0.01 scale. Evaluate thresholds in world metres, not raw FBX centimetres.
        float metresPerUnit = boneTransform.TransformVector(Vector3.up).magnitude;
        if (metresPerUnit < 1e-6f) throw new InvalidOperationException("Degenerate hand scale: " + boneName);
        float[] influence = BoneInfluences(mesh, boneIndex);
        Vector3[] vertices = mesh.vertices, normals = mesh.normals;
        Vector3[] delta = new Vector3[vertices.Length], normalDelta = new Vector3[vertices.Length];
        var hand = new Vector3[vertices.Length];
        float maximumY = KnuckleStartMetres;
        for (int i = 0; i < vertices.Length; i++)
        {
            hand[i] = toHand.MultiplyPoint3x4(vertices[i]) * metresPerUnit;
            if (influence[i] >= .98f && hand[i].z < ThumbSideMetres)
                maximumY = Mathf.Max(maximumY, hand[i].y);
        }
        float length = maximumY - KnuckleStartMetres;
        if (length < .035f || length > .18f)
            throw new InvalidOperationException("Unexpected finger length in metres (axis/unit audit required): " + boneName + " " + length);
        float radius = length / (CurlDegrees * Mathf.Deg2Rad);
        int changed = 0;
        float maximumDisplacement = 0, minimumChangedY = float.MaxValue;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 p = hand[i];
            // The wrist, palm, thumb, forearm and low-weight seams remain exactly unchanged.
            if (influence[i] < .98f || p.y <= KnuckleStartMetres || p.z >= ThumbSideMetres) continue;
            float angle = Mathf.Min(CurlDegrees * Mathf.Deg2Rad, (p.y - KnuckleStartMetres) / radius);
            float sin = Mathf.Sin(angle), cos = Mathf.Cos(angle);
            Vector3 curved = new Vector3(sign * radius * (1 - cos) + p.x * cos,
                KnuckleStartMetres + radius * sin - sign * p.x * sin, p.z);
            // Finger vertices are almost fully hand-weighted; a short smooth mask protects seam blends.
            float mask = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.98f, 1f, influence[i]));
            delta[i] = toMesh.MultiplyVector((curved - p) / metresPerUnit) * mask;
            if (delta[i].sqrMagnitude < 1e-16f) continue;
            changed++;
            minimumChangedY = Mathf.Min(minimumChangedY, p.y);
            float physicalDelta = skin.transform.TransformVector(delta[i]).magnitude;
            maximumDisplacement = Mathf.Max(maximumDisplacement, physicalDelta);
            if (normals.Length == vertices.Length)
            {
                Vector3 handNormal = toHand.MultiplyVector(normals[i]).normalized;
                Quaternion rotation = Quaternion.AngleAxis(-sign * angle * Mathf.Rad2Deg, Vector3.forward);
                Vector3 bentNormal = toMesh.MultiplyVector(rotation * handNormal).normalized;
                normalDelta[i] = (bentNormal - normals[i]) * mask;
            }
        }
        if (changed < 50) throw new InvalidOperationException("Too few finger vertices selected: " + boneName + " " + changed);
        mesh.AddBlendShapeFrame(shapeName, 100f, delta, normalDelta, null);
        return new HandReport { bone = boneName, shape = shapeName, sign = sign, changedVertices = changed,
            metresPerBoneUnit = metresPerUnit, fingerLengthMetres = length,
            minimumChangedYMetres = minimumChangedY, maximumDisplacementMetres = maximumDisplacement,
            thumbAndWristUntouched = true };
    }

    private static float[] BoneInfluences(Mesh mesh, int boneIndex)
    {
        float[] result = new float[mesh.vertexCount];
        using (var counts = mesh.GetBonesPerVertex())
        using (var weights = mesh.GetAllBoneWeights())
        {
            int offset = 0;
            for (int vertex = 0; vertex < counts.Length; vertex++)
                for (int j = 0; j < counts[vertex]; j++)
                {
                    var weight = weights[offset++];
                    if (weight.boneIndex == boneIndex) result[vertex] += weight.weight;
                }
        }
        return result;
    }

    private static void ValidatePreservedStreams(Mesh source, Mesh target)
    {
        if (!source.vertices.SequenceEqual(target.vertices) || !source.normals.SequenceEqual(target.normals) ||
            !source.tangents.SequenceEqual(target.tangents) || !source.bindposes.SequenceEqual(target.bindposes) ||
            !source.triangles.SequenceEqual(target.triangles) || source.subMeshCount != target.subMeshCount)
            throw new InvalidOperationException("Grip generation changed a source mesh stream.");
        for (int channel = 0; channel < 8; channel++)
        {
            var sourceUV = new List<Vector4>(); var targetUV = new List<Vector4>();
            source.GetUVs(channel, sourceUV); target.GetUVs(channel, targetUV);
            if (!sourceUV.SequenceEqual(targetUV)) throw new InvalidOperationException("Grip generation changed UV" + channel);
        }
        using (var sourceWeights = source.GetAllBoneWeights())
        using (var targetWeights = target.GetAllBoneWeights())
        {
            if (sourceWeights.Length != targetWeights.Length) throw new InvalidOperationException("Grip generation changed skin weights.");
            for (int i = 0; i < sourceWeights.Length; i++)
                if (sourceWeights[i].boneIndex != targetWeights[i].boneIndex || sourceWeights[i].weight != targetWeights[i].weight)
                    throw new InvalidOperationException("Grip generation changed skin weights.");
        }
    }

    [Serializable] private sealed class HandReport
    {
        public string bone, shape;
        public float sign, metresPerBoneUnit, fingerLengthMetres, minimumChangedYMetres, maximumDisplacementMetres;
        public int changedVertices;
        public bool thumbAndWristUntouched;
    }
    [Serializable] private sealed class BuildReport
    {
        public bool success = true, originalMeshStreamsPreserved = true, originalFbxUntouched = true;
        public string source, mesh;
        public int vertexCount, triangles, blendShapes;
        public float curlDegrees, knuckleStartMetres;
        public HandReport[] hands;
    }
}
#endif
