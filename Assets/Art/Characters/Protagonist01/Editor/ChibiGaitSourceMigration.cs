#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>One-time, editor-only preservation of gait curves before retiring the old visual assets.</summary>
public static class ChibiGaitSourceMigration
{
    const string Legacy = "Assets/Art/Characters/Protagonist01/MotionNatural_v005";
    const string Destination = "Assets/Art/Characters/Protagonist01/ChibiMotion_v007/SourceGait";
    const string LegacyPrefab = Legacy + "/Prefabs/Protagonist01_MotionReview.prefab";
    const string SkeletonPath = Destination + "/Skeleton.prefab";
    const string PreviewScene = "Assets/Art/Stage01Kit/UrbanKit_Preview.unity";
    const string PlayerGuid = "dc099bc5ec699084abe2e3bb9cf36915";
    const string CameraScriptGuid = "30a64c383aef2824ead1df3555aaeb69";
    const string ReportPath = "ArtSources/Characters/Protagonist01/v007-compact-action/source-gait-migration.json";
    static readonly string[] GaitNames = { "Idle_Fists", "Walk_Fists", "Run_Fists" };
    static readonly HashSet<string> RetiredWeaponAnimationGuids = new HashSet<string>
    {
        "9567914dd7b826f43bbb37a0599ec8be", // old ExtendedActions FBX, including its embedded clips
        "236f9fd6f6989744991931a77e48e6f5", // old sword override
        "fc18c44014680e94e9108f1bb63d7688", // old staff override
    };

    [Serializable] public sealed class ClipCopy
    {
        public string name, source, target, sourceSha256, targetSha256;
        public long bytes;
        public bool byteIdentical;
    }
    [Serializable] public sealed class ClearedReference
    {
        public string prefab, componentPath, property, retiredGuid;
    }
    [Serializable] public sealed class MigrationReport
    {
        public string method, sourcePrefab, skeleton, skeletonSha256, utc;
        public string sourceRootLabel, preservedRootLabel;
        public int transformCount, removedComponentCount, previewPlayersRemoved, previewCameraComponentsRemoved;
        public bool exactHierarchyAfterStripping, exactHierarchyAfterReload, skeletonHasOnlyTransforms;
        public bool weaponVisualsAndMountMetadataPreserved, previewEnvironmentComponentsPreserved, previewLightingSettingsPreserved;
        public string[] skeletonDependencies;
        public ClipCopy[] clips;
        public ClearedReference[] clearedWeaponReferences;
        public string previewSceneBeforeSha256, previewSceneAfterSha256;
        public bool gameplayScenesModified, runtimeAnimationConnected;
    }
    sealed class TransformState
    {
        public string path, name;
        public Vector3 position, scale;
        public Quaternion rotation;
        public bool active;
    }

    [MenuItem("Tools/OZ/Chibi/Preserve Source Gait And Retire Art Preview Links")]
    public static void Prepare()
    {
        var report = new MigrationReport
        {
            method = "Preserve only the exact legacy transform hierarchy and three unchanged generic gait clips; remove only explicitly identified retired animation links from two weapon props and old player/follow-camera from the art preview scene.",
            sourcePrefab = LegacyPrefab, skeleton = SkeletonPath,
            utc = DateTime.UtcNow.ToString("O"), gameplayScenesModified = false, runtimeAnimationConnected = false,
        };
        Directory.CreateDirectory(Full(Destination + "/Clips"));
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        PreserveSkeleton(report);
        report.clips = GaitNames.Select(CopyClip).ToArray();
        var cleared = new List<ClearedReference>();
        CleanWeapon("Assets/Art/Stage01Kit/Prefabs/Weapons/Sword.prefab", cleared);
        CleanWeapon("Assets/Art/Stage01Kit/Prefabs/Weapons/Staff.prefab", cleared);
        report.clearedWeaponReferences = cleared.ToArray();
        report.weaponVisualsAndMountMetadataPreserved = true;
        CleanArtPreview(report);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory(Path.GetDirectoryName(Full(ReportPath)));
        File.WriteAllText(Full(ReportPath), JsonUtility.ToJson(report, true) + "\n", new UTF8Encoding(false));
        Debug.Log("CHIBI_SOURCE_GAIT_MIGRATION_SUCCESS " + report.transformCount + " transforms; " +
                  report.clips.Length + " byte-identical source clips; " + cleared.Count + " retired weapon references cleared.");
    }

    static void PreserveSkeleton(MigrationReport report)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LegacyPrefab);
        if (!prefab) throw new FileNotFoundException("Run this preservation step before deleting the legacy source prefab.", LegacyPrefab);
        var scene = EditorSceneManager.NewPreviewScene();
        GameObject actor = null;
        try
        {
            actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            // Unity names a saved prefab root after its destination filename.
            // Normalize only that label before the exact hierarchy/TRS snapshot;
            // no bone name, transform, active state or child order is relaxed.
            report.sourceRootLabel = actor.name;
            actor.name = Path.GetFileNameWithoutExtension(SkeletonPath);
            report.preservedRootLabel = actor.name;
            var before = Snapshot(actor.transform);
            if (PrefabUtility.IsPartOfPrefabInstance(actor))
                PrefabUtility.UnpackPrefabInstance(actor, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var animator in actor.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            var components = actor.GetComponentsInChildren<Component>(true).Where(c => c && !(c is Transform)).Reverse().ToArray();
            report.removedComponentCount = components.Length;
            foreach (var component in components) if (component) Object.DestroyImmediate(component);
            foreach (var transform in actor.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
            AssertSnapshot(actor.transform, before);
            AssertOnlyTransforms(actor);
            report.exactHierarchyAfterStripping = true;
            report.transformCount = before.Length;
            if (!PrefabUtility.SaveAsPrefabAsset(actor, SkeletonPath))
                throw new InvalidOperationException("Could not save the transform-only gait source.");
            AssetDatabase.ImportAsset(SkeletonPath, ImportAssetOptions.ForceSynchronousImport);
            var saved = PrefabUtility.LoadPrefabContents(SkeletonPath);
            try
            {
                AssertSnapshot(saved.transform, before);
                AssertOnlyTransforms(saved);
            }
            finally { PrefabUtility.UnloadPrefabContents(saved); }
            report.exactHierarchyAfterReload = true;
            report.skeletonHasOnlyTransforms = true;
            report.skeletonSha256 = Hash(SkeletonPath);
            report.skeletonDependencies = AssetDatabase.GetDependencies(SkeletonPath, true)
                .Where(p => p != SkeletonPath).OrderBy(p => p).ToArray();
            if (report.skeletonDependencies.Length != 0)
                throw new InvalidOperationException("The gait skeleton still has asset dependencies: " + string.Join(", ", report.skeletonDependencies));
        }
        finally
        {
            if (actor) Object.DestroyImmediate(actor);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static ClipCopy CopyClip(string name)
    {
        string source = Legacy + "/Clips/" + name + ".anim", target = Destination + "/Clips/" + name + ".anim";
        string sourceHash = Hash(source);
        if (File.Exists(Full(target)))
        {
            if (Hash(target) != sourceHash)
                throw new InvalidOperationException("An existing preserved gait clip differs; refusing to overwrite it: " + target);
        }
        else if (!AssetDatabase.CopyAsset(source, target)) throw new IOException("Could not preserve " + source);
        AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
        string targetHash = Hash(target);
        if (sourceHash != targetHash) throw new InvalidOperationException("Gait clip bytes changed during copying: " + name);
        return new ClipCopy { name = name, source = source, target = target, sourceSha256 = sourceHash,
            targetSha256 = targetHash, bytes = new FileInfo(Full(target)).Length, byteIdentical = true };
    }

    static void CleanWeapon(string prefabPath, List<ClearedReference> cleared)
    {
        if (!File.Exists(Full(prefabPath))) throw new FileNotFoundException("Expected the existing rigid weapon prop.", prefabPath);
        var actor = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var transformState = Snapshot(actor.transform);
            var protectedComponents = actor.GetComponentsInChildren<Component>(true)
                .Where(c => c && !(c is SocketWeapon)).ToArray();
            string protectedBefore = Fingerprint(protectedComponents);
            bool changed = false;
            foreach (var weapon in actor.GetComponentsInChildren<SocketWeapon>(true))
            {
                var serialized = new SerializedObject(weapon);
                serialized.Update();
                var kind = serialized.FindProperty("_kind");
                var position = serialized.FindProperty("_gripPosition");
                var euler = serialized.FindProperty("_gripEuler");
                var contact = serialized.FindProperty("_attackContactNormalized");
                int originalKind = kind.intValue;
                Vector3 originalPosition = position.vector3Value, originalEuler = euler.vector3Value;
                float originalContact = contact.floatValue;
                foreach (string field in new[] { "_animationOverride", "_basicAttackClip" })
                {
                    var property = serialized.FindProperty(field);
                    if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
                        throw new InvalidOperationException("Unexpected weapon serialization field: " + field);
                    var value = property.objectReferenceValue;
                    if (!value) continue;
                    string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value));
                    if (!RetiredWeaponAnimationGuids.Contains(guid)) continue;
                    property.objectReferenceValue = null;
                    cleared.Add(new ClearedReference { prefab = prefabPath, componentPath = IndexedPath(weapon.transform, actor.transform),
                        property = field, retiredGuid = guid });
                    changed = true;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                serialized.Update();
                if (kind.intValue != originalKind || !position.vector3Value.Equals(originalPosition) ||
                    !euler.vector3Value.Equals(originalEuler) || !contact.floatValue.Equals(originalContact))
                    throw new InvalidOperationException("Weapon mount metadata changed unexpectedly: " + prefabPath);
            }
            AssertSnapshot(actor.transform, transformState);
            if (Fingerprint(protectedComponents) != protectedBefore)
                throw new InvalidOperationException("Weapon geometry/material/orb components changed unexpectedly: " + prefabPath);
            if (changed && !PrefabUtility.SaveAsPrefabAsset(actor, prefabPath))
                throw new IOException("Could not save the two cleared animation references: " + prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(actor); }
    }

    static void CleanArtPreview(MigrationReport report)
    {
        report.previewSceneBeforeSha256 = Hash(PreviewScene);
        string settingsBefore = SceneSettingsFingerprint(PreviewScene);
        var scene = SceneManager.GetSceneByPath(PreviewScene);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(PreviewScene, OpenSceneMode.Additive);
        else if (scene.isDirty) throw new InvalidOperationException("Art preview has unsaved changes; refusing to save unrelated edits.");
        try
        {
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            var players = all.Select(t => t.gameObject).Where(g => PrefabUtility.IsAnyPrefabInstanceRoot(g) &&
                AssetDatabase.AssetPathToGUID(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g)) == PlayerGuid).ToArray();
            if (players.Any(g => g.transform.parent))
                throw new InvalidOperationException("Expected the old preview player to be a scene-root prefab instance.");
            var playerIds = new HashSet<int>(players.SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject.GetInstanceID()));
            var followComponents = all.SelectMany(t => t.GetComponents<MonoBehaviour>()).Where(c => c &&
                AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(c))) == CameraScriptGuid).ToArray();
            var followIds = new HashSet<int>(followComponents.Select(c => c.GetInstanceID()));
            var protectedComponents = all.SelectMany(t => t.GetComponents<Component>()).Where(c => c &&
                !playerIds.Contains(c.gameObject.GetInstanceID()) && !followIds.Contains(c.GetInstanceID())).ToArray();
            string protectedBefore = Fingerprint(protectedComponents);
            foreach (var follow in followComponents) if (follow) Object.DestroyImmediate(follow);
            foreach (var player in players) if (player) Object.DestroyImmediate(player);
            if (Fingerprint(protectedComponents) != protectedBefore)
                throw new InvalidOperationException("An environment, light, or camera component changed while retiring the old preview player.");
            report.previewPlayersRemoved = players.Length;
            report.previewCameraComponentsRemoved = followComponents.Length;
            report.previewEnvironmentComponentsPreserved = true;
            if ((players.Length > 0 || followComponents.Length > 0) && !EditorSceneManager.SaveScene(scene, PreviewScene))
                throw new IOException("Could not save the explicitly scoped art-preview cleanup.");
        }
        finally { if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true); }
        report.previewSceneAfterSha256 = Hash(PreviewScene);
        report.previewLightingSettingsPreserved = settingsBefore == SceneSettingsFingerprint(PreviewScene);
        if (!report.previewLightingSettingsPreserved)
            throw new InvalidOperationException("Serialized preview lighting/render/navmesh settings changed; inspect before retiring assets.");
    }

    static TransformState[] Snapshot(Transform root)
    {
        return root.GetComponentsInChildren<Transform>(true).Select(t => new TransformState
        {
            path = IndexedPath(t, root), name = t.name, position = t.localPosition, rotation = t.localRotation,
            scale = t.localScale, active = t.gameObject.activeSelf,
        }).ToArray();
    }
    static string IndexedPath(Transform current, Transform root)
    {
        if (current == root) return "root";
        var indexes = new List<int>();
        while (current != root)
        {
            if (!current) throw new InvalidOperationException("Transform is outside the expected source hierarchy.");
            indexes.Add(current.GetSiblingIndex()); current = current.parent;
        }
        indexes.Reverse(); return "root/" + string.Join("/", indexes);
    }
    static void AssertSnapshot(Transform root, TransformState[] expected)
    {
        var actual = Snapshot(root);
        if (actual.Length != expected.Length) throw new InvalidOperationException("Source transform count changed.");
        for (int i = 0; i < actual.Length; i++)
        {
            var a = actual[i]; var b = expected[i];
            if (a.path != b.path || a.name != b.name || !a.position.Equals(b.position) ||
                !a.rotation.Equals(b.rotation) || !a.scale.Equals(b.scale) || a.active != b.active)
                throw new InvalidOperationException("Exact local transform/hierarchy preservation failed at " + b.path + ":" + b.name);
        }
    }
    static void AssertOnlyTransforms(GameObject actor)
    {
        if (actor.GetComponentsInChildren<Component>(true).Any(c => !c || !(c is Transform)))
            throw new InvalidOperationException("The preserved gait skeleton contains a non-transform or missing component.");
    }
    static string Fingerprint(IEnumerable<Component> components)
    {
        string text = string.Join("\n", components.Select(c => c.GetInstanceID() + ":" + EditorJsonUtility.ToJson(c)));
        using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
    }
    static string SceneSettingsFingerprint(string path)
    {
        string text = File.ReadAllText(Full(path)).Replace("\r\n", "\n");
        var sections = Regex.Matches(text, @"(?ms)^--- !u!(?:29|104|157|196) &[^\n]*\n.*?(?=^--- !u!|\z)")
            .Cast<Match>().Select(m => m.Value.TrimEnd()).ToArray();
        if (sections.Length == 0) throw new InvalidOperationException("No serialized art-preview render settings found.");
        return string.Join("\n", sections);
    }
    static string Full(string relative) { return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), relative)); }
    static string Hash(string path)
    {
        using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(Full(path)))).Replace("-", "").ToLowerInvariant();
    }
}
#endif
