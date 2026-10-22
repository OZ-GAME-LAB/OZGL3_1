#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Marker = ProtagonistVisualGrounding.SkinMarker;

/// <summary>Extracts small sole/body landmark sets once; runtime never reads or bakes the entire mesh.</summary>
public static class ProtagonistGroundingBuilder
{
    private const string Root = "Assets/Art/Characters/Protagonist01/Runtime";
    private const int MarkersPerFoot = 20;
    private static readonly Vector3[] BodyDirections = Directions(false);
    private static readonly Vector3[] SoleDirections = Directions(true);

    [MenuItem("Project M/Art/Build Protagonist Visual Grounding")]
    public static void Build()
    {
        var reports = new List<PrefabReport>();
        // Save Visual first, then suppress its inherited driver inside the Player prefab.
        foreach (string name in new[] { "Protagonist01_Visual", "Protagonist01_Player" })
        {
            string path = Root + "/" + name + ".prefab";
            GameObject actor = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var skin = actor.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (skin == null || skin.sharedMesh == null) throw new InvalidOperationException("Missing character skin in " + path);
                Mesh mesh = skin.sharedMesh;
                var vertices = mesh.vertices;
                var weights = ReadWeights(mesh);
                var bindposes = mesh.bindposes;
                var bones = skin.bones;
                Vector3[] gripR = GripDeltas(mesh, "Grip_R"), gripL = GripDeltas(mesh, "Grip_L");
                var soleIndices = new List<int>();
                var footCounts = new List<int>();
                foreach (string side in new[] { "Left", "Right" })
                {
                    int foot = Array.FindIndex(bones, bone => bone != null && bone.name == side + "Foot");
                    int toe = Array.FindIndex(bones, bone => bone != null && bone.name == side + "ToeBase");
                    if (foot < 0 || toe < 0) throw new InvalidOperationException("Missing foot/toe bones on " + side);
                    int[] candidates = Enumerable.Range(0, vertices.Length)
                        .Where(i => WeightFor(weights[i], foot) + WeightFor(weights[i], toe) > .7f).ToArray();
                    Vector3[] points = vertices.Select(v => bindposes[foot].MultiplyPoint3x4(v)).ToArray();
                    var selected = SelectExtrema(candidates, points, SoleDirections, MarkersPerFoot, true);
                    if (selected.Count < 8) throw new InvalidOperationException("Too few distinct sole landmarks for " + side);
                    soleIndices.AddRange(selected); footCounts.Add(selected.Count);
                }
                var bodyIndices = new HashSet<int>(soleIndices);
                for (int bone = 0; bone < bones.Length; bone++)
                {
                    int currentBone = bone;
                    int[] candidates = Enumerable.Range(0, vertices.Length).Where(i => DominantBone(weights[i]) == currentBone).ToArray();
                    if (candidates.Length == 0) continue;
                    Vector3[] points = vertices.Select(v => bindposes[currentBone].MultiplyPoint3x4(v)).ToArray();
                    foreach (int selected in SelectExtrema(candidates, points, BodyDirections, BodyDirections.Length, false))
                        bodyIndices.Add(selected);
                }
                Marker[] soleMarkers = soleIndices.Distinct().Select(i => MakeMarker(i, vertices, weights, gripR, gripL)).ToArray();
                Marker[] bodyMarkers = bodyIndices.OrderBy(i => i).Select(i => MakeMarker(i, vertices, weights, gripR, gripL)).ToArray();
                if (soleMarkers.Length > 40 || bodyMarkers.Length > 500)
                    throw new InvalidOperationException("Grounding marker budget exceeded.");
                Transform visualRoot = FindVisualOffsetRoot(actor, skin);
                float groundOffset = CapsuleBottomOffset(actor);
                var grounding = actor.GetComponent<ProtagonistVisualGrounding>() ?? actor.AddComponent<ProtagonistVisualGrounding>();
                grounding.enabled = true;
                grounding.Configure(visualRoot, skin, soleMarkers, bodyMarkers, groundOffset);
                foreach (var nested in actor.GetComponentsInChildren<ProtagonistVisualGrounding>(true))
                    if (nested != grounding) nested.enabled = false;
                PrefabUtility.SaveAsPrefabAsset(actor, path);
                reports.Add(new PrefabReport
                {
                    prefab = path, visualOffsetRoot = visualRoot.name,
                    soleMarkers = soleMarkers.Length, leftSoleMarkers = footCounts[0], rightSoleMarkers = footCounts[1],
                    bodyMarkers = bodyMarkers.Length, bones = bones.Length, groundOffsetMetres = groundOffset,
                    maximumCorrectionMetres = .25f, worstCaseMatrixVectorMultipliesPerFrame = bodyMarkers.Length * 4,
                    standingMatrixVectorMultipliesPerFrame = soleMarkers.Length * 4
                });
            }
            finally { PrefabUtility.UnloadPrefabContents(actor); }
        }
        Directory.CreateDirectory("ArtSources/Stage01Kit");
        File.WriteAllText("ArtSources/Stage01Kit/visual-grounding-build.json", JsonUtility.ToJson(new BuildReport
        {
            prefabs = reports.ToArray(), method = "40 sole support/extreme landmarks and 14 directional extrema per dominant-bone group. " +
            "Per marker: sum(bone.localToWorld * bindpose * (original position + active grip deltas) * skin weight). " +
            "Reset visual branch to its serialized base, align landmark minimum with physics capsule bottom, clamp world offset to +/-0.25m. " +
            "Airborne poses retain zero height correction. Dead uses whole-body landmarks. Dash/Hit fallback poses receive -/+40 degree visual yaw " +
            "for right/left facing respectively. Original mesh, avatar, UVs, collider and physics root are unchanged."
        }, true));
        AssetDatabase.SaveAssets();
        Debug.Log("PROTAGONIST_GROUNDING_BUILD_SUCCESS: " + JsonUtility.ToJson(new BuildReport { prefabs = reports.ToArray() }));
    }

    private static Transform FindVisualOffsetRoot(GameObject actor, SkinnedMeshRenderer skin)
    {
        Transform branch = actor.GetComponent<PlayerController>() != null ? skin.transform : skin.rootBone;
        if (branch == null) throw new InvalidOperationException("No visual branch to offset.");
        while (branch.parent != null && branch.parent != actor.transform) branch = branch.parent;
        if (branch == actor.transform || branch.parent != actor.transform || bonesOutside(branch, skin.bones))
            throw new InvalidOperationException("The visual offset branch must contain every skinned bone and be below the physics root.");
        return branch;
    }

    private static bool bonesOutside(Transform branch, Transform[] bones)
        => bones.Any(bone => bone != null && bone != branch && !bone.IsChildOf(branch));

    private static float CapsuleBottomOffset(GameObject actor)
    {
        var capsule = actor.GetComponent<CapsuleCollider>();
        if (capsule == null) return 0f;
        Vector3 scale = capsule.transform.lossyScale;
        Vector3 axis = capsule.direction == 0 ? Vector3.right : capsule.direction == 1 ? Vector3.up : Vector3.forward;
        float axisScale = capsule.direction == 0 ? Mathf.Abs(scale.x) : capsule.direction == 1 ? Mathf.Abs(scale.y) : Mathf.Abs(scale.z);
        float radiusScale = capsule.direction == 0 ? Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)) :
            capsule.direction == 1 ? Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) : Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        float radius = capsule.radius * radiusScale;
        float height = Mathf.Max(capsule.height * axisScale, 2 * radius);
        float extent = Mathf.Abs(capsule.transform.TransformDirection(axis).y) * (height * .5f - radius) + radius;
        return capsule.transform.TransformPoint(capsule.center).y - extent - actor.transform.position.y;
    }

    private static List<int> SelectExtrema(int[] candidates, Vector3[] positions, Vector3[] directions, int budget, bool fill)
    {
        var selected = new List<int>();
        var coordinates = new HashSet<Vector3>();
        foreach (Vector3 direction in directions)
        {
            if (selected.Count >= budget || candidates.Length == 0) break;
            int best = candidates[0]; float score = float.NegativeInfinity;
            foreach (int index in candidates)
            {
                float value = Vector3.Dot(positions[index], direction);
                if (value > score) { score = value; best = index; }
            }
            if (coordinates.Add(positions[best])) selected.Add(best);
        }
        // Spread remaining sole markers over the convex support region, avoiding duplicated UV-seam vertices.
        while (fill && selected.Count < budget && selected.Count < candidates.Length)
        {
            int best = -1; float distance = -1;
            foreach (int candidate in candidates)
            {
                if (coordinates.Contains(positions[candidate])) continue;
                float nearest = float.PositiveInfinity;
                foreach (int existing in selected)
                    nearest = Mathf.Min(nearest, (positions[candidate] - positions[existing]).sqrMagnitude);
                if (nearest > distance) { distance = nearest; best = candidate; }
            }
            if (best < 0) break;
            coordinates.Add(positions[best]); selected.Add(best);
        }
        return selected;
    }

    private static Vector3[] Directions(bool full)
    {
        var result = new List<Vector3> { Vector3.down, Vector3.up, Vector3.left, Vector3.right, Vector3.back, Vector3.forward };
        for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
                for (int z = -1; z <= 1; z++)
                {
                    int nonzero = (x == 0 ? 0 : 1) + (y == 0 ? 0 : 1) + (z == 0 ? 0 : 1);
                    if (nonzero == 3 || full && nonzero == 2) result.Add(new Vector3(x, y, z).normalized);
                }
        return result.ToArray();
    }

    private static BoneWeight[] ReadWeights(Mesh mesh)
    {
        var result = new BoneWeight[mesh.vertexCount];
        using (var counts = mesh.GetBonesPerVertex())
        using (var weights = mesh.GetAllBoneWeights())
        {
            int offset = 0;
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] > 4) throw new InvalidOperationException("Grounding expects this rig's imported maximum of four influences per vertex.");
                BoneWeight w = new BoneWeight();
                for (int j = 0; j < counts[i]; j++)
                {
                    var entry = weights[offset++];
                    if (j == 0) { w.boneIndex0 = entry.boneIndex; w.weight0 = entry.weight; }
                    if (j == 1) { w.boneIndex1 = entry.boneIndex; w.weight1 = entry.weight; }
                    if (j == 2) { w.boneIndex2 = entry.boneIndex; w.weight2 = entry.weight; }
                    if (j == 3) { w.boneIndex3 = entry.boneIndex; w.weight3 = entry.weight; }
                }
                result[i] = w;
            }
        }
        return result;
    }

    private static int DominantBone(BoneWeight w)
    {
        float maximum = w.weight0; int bone = w.boneIndex0;
        if (w.weight1 > maximum) { maximum = w.weight1; bone = w.boneIndex1; }
        if (w.weight2 > maximum) { maximum = w.weight2; bone = w.boneIndex2; }
        if (w.weight3 > maximum) bone = w.boneIndex3;
        return bone;
    }

    private static float WeightFor(BoneWeight w, int bone)
        => (w.boneIndex0 == bone ? w.weight0 : 0) + (w.boneIndex1 == bone ? w.weight1 : 0) +
            (w.boneIndex2 == bone ? w.weight2 : 0) + (w.boneIndex3 == bone ? w.weight3 : 0);

    private static Marker MakeMarker(int index, Vector3[] vertices, BoneWeight[] weights, Vector3[] gripR, Vector3[] gripL)
    {
        BoneWeight w = weights[index];
        return new Marker { vertexIndex = index, position = vertices[index], rightGripDelta = gripR[index], leftGripDelta = gripL[index],
            bone0 = w.boneIndex0, bone1 = w.boneIndex1, bone2 = w.boneIndex2, bone3 = w.boneIndex3,
            weights = new Vector4(w.weight0, w.weight1, w.weight2, w.weight3) };
    }

    private static Vector3[] GripDeltas(Mesh mesh, string name)
    {
        var result = new Vector3[mesh.vertexCount];
        int index = mesh.GetBlendShapeIndex(name);
        if (index >= 0) mesh.GetBlendShapeFrameVertices(index, mesh.GetBlendShapeFrameCount(index) - 1, result, null, null);
        return result;
    }

    [Serializable] private sealed class PrefabReport
    {
        public string prefab, visualOffsetRoot;
        public int soleMarkers, leftSoleMarkers, rightSoleMarkers, bodyMarkers, bones;
        public int standingMatrixVectorMultipliesPerFrame, worstCaseMatrixVectorMultipliesPerFrame;
        public float groundOffsetMetres, maximumCorrectionMetres;
    }
    [Serializable] private sealed class BuildReport
    {
        public bool success = true, runtimeBakeMesh = false, originalSkinMeshUnchanged = true, physicsRootUnchanged = true;
        public string method;
        public PrefabReport[] prefabs;
    }
}
#endif
