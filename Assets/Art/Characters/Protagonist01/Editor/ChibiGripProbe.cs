#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Read-only scene instances and exports. Never creates or changes a Unity asset.</summary>
public static class ChibiGripProbe
{
    public static void Run() { ChibiMotionSet.ExportStaffGripForensics(); }
}

public static partial class ChibiMotionSet
{
    const string GripProbeOutput = "ArtSources/Stage01Kit/research/StaffGripProbe_Calibrated";
    const string GripProbeStaff = "Assets/Art/Stage01Kit/Prefabs/Weapons/Staff.prefab";

    public static void ExportStaffGripForensics()
    {
        Directory.CreateDirectory(GripProbeOutput);
        foreach (var entry in new[] { ("Idle_Fists", 0f), ("Staff_Cast_Forward", 0f), ("Staff_Cast_Forward", .24f),
            ("Sword_Horizontal_Slash", 0f), ("Sword_Horizontal_Slash", .24f) })
            ExportGripSample(entry.Item1, entry.Item2);
        foreach (string clipName in new[] { "Staff_Cast_Forward", "Sword_Horizontal_Slash" })
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "/Clips/" + clipName + ".anim");
            for (int frame = 0; frame <= 20; frame++) ExportGripSample(clipName, Mathf.Min(frame / 30f, clip.length), true);
        }
        Debug.Log("CHIBI_GRIP_PROBE_SUCCESS " + GripProbeOutput);
    }

    static void ExportGripSample(string clipName, float seconds, bool sequence = false)
    {
        using (var stage = new Stage())
        {
            // The diagnostic is local to the hand; a floor would hide the underside in orthographic closeups.
            foreach (var sceneRoot in stage.scene.GetRootGameObjects())
                if (sceneRoot.name == "Plane") sceneRoot.SetActive(false);
            var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            SceneManager.MoveGameObjectToScene(actor, stage.scene);
            actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var skin = actor.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var calibrated = AssetDatabase.LoadAssetAtPath<Mesh>(ChibiCylinderGripBuilder.MeshPath);
            if (!calibrated) throw new InvalidOperationException("Calibrated grip mesh must be built before this diagnostic.");
            skin.sharedMesh = calibrated;
            var original = skin.sharedMesh;
            var rh = skin.bones.First(b => b.name == "RightHand");
            int handIndex = Array.IndexOf(skin.bones, rh);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "/Clips/" + clipName + ".anim");
            if (!clip) throw new InvalidOperationException("Missing forensic source clip " + clipName);
            clip.SampleAnimation(actor, seconds);

            float[] influence = new float[original.vertexCount];
            var allWeights = new List<GripProbeWeight>[original.vertexCount];
            using (var counts = original.GetBonesPerVertex())
            using (var weights = original.GetAllBoneWeights())
            {
                int offset = 0;
                for (int i = 0; i < counts.Length; i++)
                {
                    allWeights[i] = new List<GripProbeWeight>();
                    for (int j = 0; j < counts[i]; j++)
                    {
                        var w = weights[offset++];
                        allWeights[i].Add(new GripProbeWeight { bone = w.boneIndex, weight = w.weight });
                        if (w.boneIndex == handIndex) influence[i] += w.weight;
                    }
                }
            }
            int[] sourceTriangles = original.triangles;
            int originalVertexCount = AssetDatabase.LoadAssetAtPath<Mesh>(
                "Assets/Art/Characters/Protagonist01/ChibiCap_v006/Meshes/Protagonist_ChibiCap_Fists.asset").vertexCount;
            var allBind = original.vertices.Select(v => original.bindposes[handIndex].MultiplyPoint3x4(v)).ToArray();
            bool Thenar(int i) => influence[i] >= .30f && allBind[i].y > .005f && allBind[i].y < .050f &&
                allBind[i].z > .035f && allBind[i].z < .100f;
            var selectedTriangles = new List<int>();
            for (int t = 0; t < sourceTriangles.Length; t += 3)
                if (Enumerable.Range(0, 3).Any(k => influence[sourceTriangles[t + k]] >= .50f || sourceTriangles[t + k] >= originalVertexCount || Thenar(sourceTriangles[t + k])))
                    selectedTriangles.AddRange(new[] { sourceTriangles[t], sourceTriangles[t + 1], sourceTriangles[t + 2] });
            int[] selected = selectedTriangles.Distinct().OrderBy(i => i).ToArray();
            var remap = selected.Select((v, i) => new { v, i }).ToDictionary(x => x.v, x => x.i);
            int[] localTriangles = selectedTriangles.Select(i => remap[i]).ToArray();
            Quaternion toHand = Quaternion.Inverse(rh.rotation);
            Vector3 HandPoint(Vector3 world) => toHand * (world - rh.position);
            Vector3 HandNormal(Vector3 world) => (toHand * world).normalized;
            float unitsToMetres = rh.TransformVector(Vector3.up).magnitude;
            var binds = selected.Select(i => original.bindposes[handIndex].MultiplyPoint3x4(original.vertices[i]) * unitsToMetres).ToArray();

            var snapshots = new List<GripProbeSnapshot>();
            var baked = new Mesh();
            var displayMeshes = new List<Mesh>();
            var displayObjects = new List<GameObject>();
            var orange = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            orange.SetColor("_BaseColor", new Color(1f, .35f, .03f, 1));
            string weaponPath = clipName.StartsWith("Sword", StringComparison.Ordinal) ?
                "Assets/Art/Stage01Kit/Prefabs/Weapons/Sword.prefab" : GripProbeStaff;
            var prop = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(weaponPath));
            SceneManager.MoveGameObjectToScene(prop, stage.scene);
            try
            {
                // Reproduce the newly calibrated ReviewProp mount; the old wrist offset remains only in baseline JSONs.
                prop.transform.SetParent(rh, false);
                Vector3 s = rh.lossyScale;
                Vector3 mount = ChibiCylinderGripBuilder.GripCenterMetres;
                float scale = ChibiCylinderGripBuilder.PropScale;
                prop.transform.localPosition = new Vector3(mount.x / s.x, mount.y / s.y, mount.z / s.z);
                prop.transform.localRotation = Quaternion.Euler(ChibiCylinderGripBuilder.MountRotationEuler);
                prop.transform.localScale = new Vector3(scale / s.x, scale / s.y, scale / s.z);
                var parts = prop.GetComponentsInChildren<MeshFilter>(true).Select(filter => new GripProbePart
                {
                    name = filter.name, mesh = AssetDatabase.GetAssetPath(filter.sharedMesh),
                    vertices = filter.sharedMesh.vertices.Select(v => HandPoint(filter.transform.TransformPoint(v))).ToArray(),
                    normals = filter.sharedMesh.normals.Select(v => HandNormal(filter.transform.TransformDirection(v))).ToArray(),
                    triangles = filter.sharedMesh.triangles
                }).ToArray();
                prop.SetActive(false);
                float[] gripWeights = sequence ? new[] { clipName.StartsWith("Sword", StringComparison.Ordinal) ?
                    ChibiCylinderGripBuilder.SwordGripWeight : ChibiCylinderGripBuilder.CylinderGripWeight } :
                    new[] { 0f, 75f, 100f, ChibiCylinderGripBuilder.CylinderGripWeight, ChibiCylinderGripBuilder.SwordGripWeight };
                foreach (float grip in gripWeights)
                {
                    int shape = original.GetBlendShapeIndex("Grip_R");
                    if (shape < 0) throw new InvalidOperationException("Grip_R blendshape missing.");
                    skin.SetBlendShapeWeight(shape, grip);
                    skin.BakeMesh(baked);
                    var bv = baked.vertices; var bn = baked.normals;
                    snapshots.Add(new GripProbeSnapshot
                    {
                        grip = grip,
                        vertices = selected.Select(i => HandPoint(skin.transform.TransformPoint(bv[i]))).ToArray(),
                        normals = selected.Select(i => HandNormal(skin.transform.TransformDirection(bn[i]))).ToArray()
                    });
                }
                string name = clipName + "_" + Mathf.RoundToInt(seconds * 1000).ToString("D4") + "ms";
                var report = new GripProbeSample
                {
                    clip = clipName, timeSeconds = seconds, prefab = PrefabPath,
                    sourceMesh = AssetDatabase.GetAssetPath(original), handBoneIndex = handIndex,
                    bones = skin.bones.Select(b => b.name).ToArray(),
                    method = "Actual saved clip sampled, Grip_R overridden only on disposable instance, SkinnedMeshRenderer.BakeMesh. Hand-metre coordinates = inverse(RightHand.worldRotation)*(worldVertex-RightHand.worldPosition), including actual skin weights. Selected triangles have a vertex with RightHand influence>=.50, or the corrected thenar region(sourceY.005..050/Z.035..100/influence>=.30), or additional topology; adjacent vertices retained. No mesh or clip assets are changed.",
                    sourceVertexIds = selected, sourceBindHandMetres = binds,
                    sourceWeights = selected.Select(i => new GripProbeWeightSet { values = allWeights[i].ToArray() }).ToArray(),
                    handInfluence = selected.Select(i => influence[i]).ToArray(), triangles = localTriangles,
                    snapshots = snapshots.ToArray(), propParts = parts,
                    mountPositionHandMetres = ChibiCylinderGripBuilder.GripCenterMetres,
                    mountEuler = ChibiCylinderGripBuilder.MountRotationEuler, propScale = ChibiCylinderGripBuilder.PropScale,
                    handWorldPosition = rh.position, handWorldRotation = rh.rotation, handLossyScale = rh.lossyScale
                };
                string outputDirectory = GripProbeOutput + (sequence ? "/Sequence30" : "");
                Directory.CreateDirectory(outputDirectory);
                File.WriteAllText(outputDirectory + "/" + name + ".json", JsonUtility.ToJson(report, true));
                if (sequence) return; // JSON-only actual used grip; preserve the five detailed diagnostic views.
                actor.SetActive(false);
                foreach (var snapshot in snapshots.Where(x => x.grip > 0))
                {
                    var handMesh = new Mesh { name = "ReadOnlyGripSnapshot" };
                    handMesh.vertices = snapshot.vertices; handMesh.normals = snapshot.normals;
                    handMesh.uv = selected.Select(i => original.uv[i]).ToArray();
                    handMesh.triangles = localTriangles; handMesh.RecalculateBounds();
                    displayMeshes.Add(handMesh);
                    var handObject = stage.New("ActualHandGeometry"); displayObjects.Add(handObject);
                    handObject.AddComponent<MeshFilter>().sharedMesh = handMesh;
                    handObject.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                    foreach (var part in parts.Where(x => !x.name.Contains("CrystalOrb")))
                    {
                        var mesh = new Mesh { name = "CurrentStaffGeometry_OrangeDiagnostic" };
                        mesh.vertices = part.vertices; mesh.normals = part.normals; mesh.triangles = part.triangles; mesh.RecalculateBounds();
                        displayMeshes.Add(mesh);
                        var obj = stage.New("CurrentStaffGeometry_OrangeDiagnostic"); displayObjects.Add(obj);
                        obj.AddComponent<MeshFilter>().sharedMesh = mesh;
                        var renderer = obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial = orange;
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                    }
                    Vector3 center = new Vector3(-.015f, .075f, 0);
                    foreach (var view in new[] { ("XY", new Vector3(0, 0, -.7f), Vector3.up),
                        ("YZ", new Vector3(.7f, 0, 0), Vector3.up), ("XZ", new Vector3(0, .7f, 0), Vector3.forward) })
                    {
                        stage.camera.transform.position = center + view.Item2;
                        stage.camera.transform.LookAt(center, view.Item3);
                        stage.camera.orthographicSize = .15f;
                        stage.Save(GripProbeOutput + "/" + name + "_Grip" + snapshot.grip + "_" + view.Item1 + ".png");
                    }
                    foreach (var obj in displayObjects) Object.DestroyImmediate(obj);
                    displayObjects.Clear();
                }
            }
            finally
            {
                foreach (var obj in displayObjects) if (obj) Object.DestroyImmediate(obj);
                foreach (var mesh in displayMeshes) if (mesh) Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(baked); Object.DestroyImmediate(orange);
                Object.DestroyImmediate(prop); Object.DestroyImmediate(actor);
            }
        }
    }

    [Serializable] sealed class GripProbeWeight { public int bone; public float weight; }
    [Serializable] sealed class GripProbeWeightSet { public GripProbeWeight[] values; }
    [Serializable] sealed class GripProbeSnapshot { public float grip; public Vector3[] vertices, normals; }
    [Serializable] sealed class GripProbePart { public string name, mesh; public Vector3[] vertices, normals; public int[] triangles; }
    [Serializable] sealed class GripProbeSample
    {
        public string clip, prefab, sourceMesh, method;
        public float timeSeconds, propScale;
        public int handBoneIndex;
        public string[] bones;
        public int[] sourceVertexIds, triangles;
        public Vector3[] sourceBindHandMetres;
        public GripProbeWeightSet[] sourceWeights;
        public float[] handInfluence;
        public GripProbeSnapshot[] snapshots;
        public GripProbePart[] propParts;
        public Vector3 mountPositionHandMetres, mountEuler, handWorldPosition, handLossyScale;
        public Quaternion handWorldRotation;
    }
}
#endif
