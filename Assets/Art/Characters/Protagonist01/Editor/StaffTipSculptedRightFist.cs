#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class MageStudyClosedHandAssembler
{
    [Serializable]
    sealed class RightSkinSelection
    {
        public string sourceMesh;
        public int sourceVertexCount, sourceTriangleCount;
        public int[] removedTriangleIndices;
    }

    const string RightSelection = "ArtSources/Stage01Kit/research/right-hand-skin-selection.json";
    const string FistSculpt = "ArtSources/Characters/Protagonist01/Studies/StaffTipBolt20261102/closed-right-fist.json";
    const string HandMaterial = "Assets/Art/Characters/Protagonist01/AnimationStudies/SkeletonMageCast20261030/Study_Hand.mat";

    public static Mesh SaveStaffTipRightFist(SkinnedMeshRenderer skin, string assetPath)
    {
        Mesh original = skin.sharedMesh;
        var selection = JsonUtility.FromJson<RightSkinSelection>(File.ReadAllText(RightSelection));
        Mesh neutral = AssetDatabase.LoadAssetAtPath<Mesh>(selection.sourceMesh);
        if (!original || original.subMeshCount != 3 || !neutral || neutral.vertexCount != selection.sourceVertexCount)
            throw new InvalidOperationException("Expected the equipped left hand mesh and the native right hand selection.");
        int right = Array.FindIndex(skin.bones, bone => bone && bone.name == "RightHand");
        if (right < 0) throw new InvalidOperationException("Missing native RightHand.");
        var hand = JsonUtility.FromJson<HandGeometry>(File.ReadAllText(FistSculpt));
        ValidateGeometry(hand);

        int[] sourceFaces = neutral.GetTriangles(0);
        var remove = new HashSet<(int, int, int)>();
        if (sourceFaces.Length != selection.sourceTriangleCount * 3)
            throw new InvalidOperationException("The right-hand reference mesh topology has changed.");
        foreach (int face in selection.removedTriangleIndices)
            remove.Add((sourceFaces[3*face], sourceFaces[3*face+1], sourceFaces[3*face+2]));
        int[] bodyFaces = original.GetTriangles(0);
        var keep = new List<int>(bodyFaces.Length - remove.Count*3);
        int removedCount = 0;
        for (int i = 0; i < bodyFaces.Length; i += 3)
        {
            var face = (bodyFaces[i], bodyFaces[i+1], bodyFaces[i+2]);
            if (remove.Contains(face)) { removedCount++; continue; }
            keep.Add(face.Item1); keep.Add(face.Item2); keep.Add(face.Item3);
        }
        if (removedCount != 471 || removedCount != remove.Count)
            throw new InvalidOperationException("The equipped mesh must contain all 471 original right skin faces.");

        Mesh result = Object.Instantiate(original);
        try
        {
            result.ClearBlendShapes();
            result.name = "Study_StaffGrip_RightFist";
            Matrix4x4 toHand = original.bindposes[right], toMesh = toHand.inverse;
            var vertices = new List<Vector3>(original.vertices);
            var normals = new List<Vector3>(original.normals);
            var tangents = new List<Vector4>(original.tangents);
            for (int i = 0; i < hand.vertices.Length; i++)
            {
                vertices.Add(toMesh.MultiplyPoint3x4(hand.vertices[i]));
                Vector3 normal = toHand.transpose.MultiplyVector(hand.normals[i]).normalized;
                normals.Add(normal);
                Vector3 tangent = Vector3.Cross(normal, Mathf.Abs(normal.y) < .9f ? Vector3.up : Vector3.right).normalized;
                tangents.Add(new Vector4(tangent.x, tangent.y, tangent.z, 1));
            }
            result.SetVertices(vertices);
            result.SetNormals(normals);
            result.SetTangents(tangents);
            CopyUVs(original, result, vertices.Count, Vector2.zero, hand.uv);
            CopyColors(original, result, vertices.Count);
            CopyWeights(original, result, hand.vertices.Length, right);
            result.subMeshCount = 4;
            result.SetTriangles(keep, 0);
            result.SetTriangles(original.GetTriangles(1), 1);
            result.SetTriangles(original.GetTriangles(2), 2);
            result.SetTriangles(hand.triangles.Select(v => v + original.vertexCount).ToArray(), 3);
            CopyBlendShapes(original, result, vertices.Count);
            result.RecalculateBounds();
            ValidateOriginalPrefix(original, result);
            SaveRightFistMaterial(Path.GetDirectoryName(assetPath).Replace('\\', '/'));
            Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (saved)
            {
                EditorUtility.CopySerialized(result, saved);
                EditorUtility.SetDirty(saved);
                return saved;
            }
            AssetDatabase.CreateAsset(result, assetPath);
            saved = result;
            result = null;
            return saved;
        }
        finally { if (result) Object.DestroyImmediate(result); }
    }

    static void SaveRightFistMaterial(string directory)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(directory + "/Study_RightFist_BaseColor.png");
        if (!texture) throw new InvalidOperationException("Missing baked right fist skin texture.");
        string path = directory + "/Study_RightFist.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material)
        {
            material = new Material(AssetDatabase.LoadAssetAtPath<Material>(HandMaterial)) { name = "Study_RightFist" };
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetTexture("_BaseMap", texture);
        material.SetTexture("_MainTex", texture);
        EditorUtility.SetDirty(material);
    }
}
#endif
