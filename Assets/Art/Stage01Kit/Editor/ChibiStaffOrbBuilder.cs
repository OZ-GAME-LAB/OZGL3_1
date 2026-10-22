#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Replaces only the staff's detached head; the original Meshy mesh stays intact.</summary>
public static class ChibiStaffOrbBuilder
{
    private const string Root = "Assets/Art/Stage01Kit";
    private const string PrefabPath = Root + "/Prefabs/Weapons/Staff.prefab";
    private const string SourceMeshPath = Root + "/Meshes/Weapons/Staff.asset";
    private const string MeshFolder = Root + "/Meshes/Weapons/StaffOrb";
    private const string MaterialFolder = Root + "/Materials/StaffOrb";
    private const string ShaderPath = MaterialFolder + "/StaffOrb_FacetedTransparent.shader";
    private const string ReportPath = "ArtSources/Stage01Kit/staff-orb-refinement.json";
    private const float OrbDiameter = .190f;
    private const float FloatingGap = .045f;

    [MenuItem("Art/Stage 01/Replace Staff Head With Floating Crystal Orb")]
    public static void Build()
    {
        Directory.CreateDirectory(MeshFolder);
        Directory.CreateDirectory(MaterialFolder);
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        AssetDatabase.Refresh();
        var source = AssetDatabase.LoadAssetAtPath<Mesh>(SourceMeshPath);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (!source || !shader || ShaderUtil.ShaderHasError(shader))
            throw new InvalidOperationException("Staff source mesh or transparent orb shader is unavailable or invalid.");
        string sourceHash = Hash(SourceMeshPath);
        Vector3[] vertices = source.vertices;
        Vector3[] normals = source.normals;
        Color[] colors = source.colors;
        int[] triangles = source.triangles;
        if (vertices.Length != colors.Length || vertices.Length != normals.Length)
            throw new InvalidOperationException("The original staff requires vertex colors and normals for exact head isolation.");

        var bodyVertices = new List<Vector3>();
        var bodyNormals = new List<Vector3>();
        var bodyColors = new List<Color>();
        var bodyTriangles = new List<int>();
        Bounds oldHead = default;
        bool hasHead = false;
        int removedHeadTriangles = 0;
        for (int t = 0; t < triangles.Length; t += 3)
        {
            bool first = colors[triangles[t]].a < .9f;
            for (int corner = 1; corner < 3; corner++)
                if ((colors[triangles[t + corner]].a < .9f) != first)
                    throw new InvalidOperationException("Mixed body/head colors in one staff face; refusing ambiguous destructive replacement.");
            if (first)
            {
                removedHeadTriangles++;
                for (int corner = 0; corner < 3; corner++)
                {
                    Vector3 p = vertices[triangles[t + corner]];
                    if (!hasHead) { oldHead = new Bounds(p, Vector3.zero); hasHead = true; }
                    else oldHead.Encapsulate(p);
                }
                continue;
            }
            for (int corner = 0; corner < 3; corner++)
            {
                int i = triangles[t + corner];
                bodyTriangles.Add(bodyVertices.Count);
                bodyVertices.Add(vertices[i]);
                bodyNormals.Add(normals[i]);
                bodyColors.Add(colors[i]);
            }
        }
        if (!hasHead || removedHeadTriangles == 0 || bodyVertices.Count == 0)
            throw new InvalidOperationException("Could not isolate the original colored staff crystal.");

        var bodyMesh = new Mesh { name = "Staff_ShaftHardware_OriginalGeometry" };
        bodyMesh.SetVertices(bodyVertices);
        bodyMesh.SetNormals(bodyNormals);
        bodyMesh.SetColors(bodyColors);
        bodyMesh.SetTriangles(bodyTriangles, 0);
        bodyMesh.RecalculateBounds();
        float orbBottom = bodyMesh.bounds.max.y + FloatingGap;
        Vector3 center = new Vector3(oldHead.center.x, orbBottom + OrbDiameter * .5f, oldHead.center.z);
        Mesh bodySaved = SaveAsset(bodyMesh, MeshFolder + "/Staff_ShaftHardware.asset");
        Mesh orbSaved = SaveAsset(CreateOrb(OrbDiameter * .5f), MeshFolder + "/Staff_CrystalOrb_80Faces.asset");
        var material = new Material(shader) { name = "Staff_CrystalOrb_TransparentTeal" };
        material.SetColor("_BaseColor", new Color(.20f, .73f, .81f, .40f));
        material.SetColor("_RimColor", new Color(.62f, .94f, .97f, 1f));
        material.SetFloat("_RimStrength", .30f);
        material.renderQueue = (int)RenderQueue.Transparent;
        material.enableInstancing = true;
        Material materialSaved = SaveAsset(material, MaterialFolder + "/Staff_CrystalOrb_TransparentTeal.mat");

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        string metadataBefore;
        string metadataAfter;
        int renderers;
        try
        {
            var weapon = root.GetComponent<SocketWeapon>();
            var shaftFilter = root.GetComponent<MeshFilter>();
            var shaftRenderer = root.GetComponent<MeshRenderer>();
            if (!weapon || !shaftFilter || !shaftRenderer)
                throw new InvalidOperationException("Staff prefab's original grip components are missing.");
            metadataBefore = EditorJsonUtility.ToJson(weapon);
            shaftFilter.sharedMesh = bodySaved;
            Transform oldOrb = root.transform.Find("CrystalOrb_Floating");
            if (oldOrb) Object.DestroyImmediate(oldOrb.gameObject);
            var orb = new GameObject("CrystalOrb_Floating");
            orb.transform.SetParent(root.transform, false);
            orb.transform.localPosition = center;
            orb.AddComponent<MeshFilter>().sharedMesh = orbSaved;
            var renderer = orb.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = materialSaved;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
            Transform tip = root.transform.Find("EffectTip");
            if (!tip) throw new InvalidOperationException("Staff's existing EffectTip marker is missing.");
            tip.localPosition = center + Vector3.up * OrbDiameter * .5f;
            metadataAfter = EditorJsonUtility.ToJson(weapon);
            if (metadataBefore != metadataAfter)
                throw new InvalidOperationException("SocketWeapon metadata changed unexpectedly.");
            renderers = root.GetComponentsInChildren<MeshRenderer>(true).Length;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        if (sourceHash != Hash(SourceMeshPath))
            throw new InvalidOperationException("Original shared staff mesh changed unexpectedly.");
        File.WriteAllText(ReportPath, JsonUtility.ToJson(new Report
        {
            sourceMesh = SourceMeshPath, sourceMeshSha256 = sourceHash,
            originalSharedMeshUnchanged = true, socketWeaponMetadataUnchanged = metadataBefore == metadataAfter,
            sourceTriangles = triangles.Length / 3, removedHeadTriangles = removedHeadTriangles,
            shaftTriangles = bodyTriangles.Count / 3, orbTriangles = orbSaved.triangles.Length / 3,
            originalHeadSize = oldHead.size, originalHeadBottom = oldHead.min.y,
            originalHeadTop = oldHead.max.y, orbDiameter = OrbDiameter, orbCenter = center,
            floatingGap = FloatingGap, shaftTop = bodySaved.bounds.max.y,
            orbBottom = orbBottom, orbTop = center.y + OrbDiameter * .5f,
            alphaAtFaceCenter = .40f, maximumRimAlpha = .61f,
            meshRenderers = renderers, transparentPasses = 1,
            transparentShader = ShaderPath, prefab = PrefabPath,
            refractiveScreenRead = false, extraTextures = 0, effectTipFollowsNewOrbTop = true
        }, true));
        Debug.Log("[ChibiStaffOrbBuilder] Replaced pointed head with a 190 mm transparent faceted orb; 45 mm floating gap. " + ReportPath);
    }

    private static T SaveAsset<T>(T created, string path) where T : Object
    {
        T saved = AssetDatabase.LoadAssetAtPath<T>(path);
        if (!saved) { AssetDatabase.CreateAsset(created, path); return created; }
        EditorUtility.CopySerialized(created, saved);
        EditorUtility.SetDirty(saved);
        Object.DestroyImmediate(created);
        return saved;
    }

    private static Mesh CreateOrb(float radius)
    {
        float phi = (1f + Mathf.Sqrt(5f)) * .5f;
        var p = new[]
        {
            new Vector3(-1,phi,0), new Vector3(1,phi,0), new Vector3(-1,-phi,0), new Vector3(1,-phi,0),
            new Vector3(0,-1,phi), new Vector3(0,1,phi), new Vector3(0,-1,-phi), new Vector3(0,1,-phi),
            new Vector3(phi,0,-1), new Vector3(phi,0,1), new Vector3(-phi,0,-1), new Vector3(-phi,0,1)
        };
        for (int i = 0; i < p.Length; i++) p[i] = p[i].normalized;
        int[] f = { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var colors = new List<Color>();
        var indices = new List<int>();
        for (int i = 0; i < f.Length; i += 3)
        {
            Vector3 a = p[f[i]], b = p[f[i + 1]], c = p[f[i + 2]];
            Vector3 ab = (a + b).normalized, bc = (b + c).normalized, ca = (c + a).normalized;
            Face(a, ab, ca); Face(b, bc, ab); Face(c, ca, bc); Face(ab, bc, ca);
        }
        var result = new Mesh { name = "Staff_CrystalOrb_Faceted80" };
        result.SetVertices(vertices); result.SetNormals(normals); result.SetColors(colors); result.SetTriangles(indices, 0);
        result.RecalculateBounds();
        return result;

        void Face(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            if (Vector3.Dot(n, a + b + c) < 0) { Vector3 swap = b; b = c; c = swap; n = -n; }
            float facet = 1f + ((indices.Count / 3) % 5 - 2) * .018f;
            foreach (Vector3 v in new[] { a, b, c })
            {
                indices.Add(vertices.Count); vertices.Add(v * radius); normals.Add(n);
                colors.Add(new Color(facet, facet, facet, 1));
            }
        }
    }

    private static string Hash(string path)
    {
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
    }

    [Serializable] private sealed class Report
    {
        public string sourceMesh, sourceMeshSha256, transparentShader, prefab;
        public bool originalSharedMeshUnchanged, socketWeaponMetadataUnchanged, refractiveScreenRead, effectTipFollowsNewOrbTop;
        public int sourceTriangles, removedHeadTriangles, shaftTriangles, orbTriangles, meshRenderers, transparentPasses, extraTextures;
        public Vector3 originalHeadSize, orbCenter;
        public float originalHeadBottom, originalHeadTop, orbDiameter, floatingGap, shaftTop, orbBottom, orbTop, alphaAtFaceCenter, maximumRimAlpha;
    }
}
#endif
