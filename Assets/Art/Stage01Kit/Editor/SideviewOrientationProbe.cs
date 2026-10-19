#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

/// <summary>Temporarily imports orientation variants, samples them, then restores the original importer clips.</summary>
public static class SideviewOrientationProbe
{
    const string Hero = "Assets/Art/Characters/Protagonist01/Runtime";
    const string Fbx = Hero + "/Protagonist01_ExtendedActions.fbx";
    const string Prefix = "__SIDEVIEW_ORIENTATION_PROBE__";
    const string Output = "ArtSources/Stage01Kit/SideviewReview/orientation-probe.json";

    [Serializable] public sealed class Sample
    {
        public float normalizedTime, seconds;
        public Quaternion humanPoseBodyRotation, animatorBodyRotation;
        public Vector3 actorForward, humanPoseForward, humanPoseForwardIfRootLocal, animatorBodyForward;
        public float humanPoseYawFromActorForward, humanPoseRootLocalYawFromActorForward, animatorBodyYawFromActorForward;
        public Vector3 hips, rightHand, leftHand, actorPositionAfterEvaluation;
    }

    [Serializable] public sealed class Variant
    {
        public string action, sourceTake, name;
        public bool keepOriginalOrientation, lockRootRotation;
        public float rotationOffset, firstFrame, lastFrame, clipSeconds;
        public float humanPoseYawMean, humanPoseYawMin, humanPoseYawMax;
        public float humanPoseRootLocalYawMean, animatorBodyYawMean, animatorBodyYawMin, animatorBodyYawMax;
        public List<Sample> samples = new List<Sample>();
    }

    [Serializable] public sealed class OriginalReference
    {
        public string clip, guid;
        public long localFileId;
        public bool restored;
    }

    [Serializable] public sealed class Report
    {
        public bool success, originalClipSettingsRestored, originalReferencesStable, temporaryClipsRemoved;
        public string unity, source, prefab, method, variantSelection, humanPoseInterpretation, error;
        public string[] originalClipNames;
        public List<OriginalReference> originalReferences = new List<OriginalReference>();
        public List<Variant> variants = new List<Variant>();
    }

    [MenuItem("Tools/Project M/Probe Humanoid Sideview Orientation")]
    public static void Analyze()
    {
        var importer = AssetImporter.GetAtPath(Fbx) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("Missing extended actions importer.");
        var original = importer.clipAnimations;
        if (original.Any(clip => clip.name.StartsWith(Prefix, StringComparison.Ordinal)))
            throw new InvalidOperationException("Temporary orientation probes already exist; restore the original importer before retrying.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Hero + "/Protagonist01_Player.prefab");
        if (!prefab) throw new InvalidOperationException("Missing player prefab for orientation sampling.");
        var report = new Report
        {
            unity = Application.unityVersion, source = Fbx, prefab = AssetDatabase.GetAssetPath(prefab),
            originalClipNames = original.Select(clip => clip.name).ToArray(),
            method = "Retain every original imported clip and append 18 uniquely named full-source-take variants in one temporary import. " +
                "Three source takes, six variants each, sampled at normalized .2/.5/.8 using a disposable root Humanoid Animator and HumanPoseHandler. " +
                "Actor is origin, yaw 90 (+X), no controller/root motion/foot IK/playable IK/physics. Nested Animators removed. " +
                "Finally restore the original ModelImporterClipAnimation array, reimport and verify original clip GUID/local IDs and removal of all temporary clips.",
            variantSelection = "For each take: the four keepOriginalOrientation true/false x lockRootRotation true/false combinations at offset 0; " +
                "then keepOriginalOrientation=false, lockRootRotation=true with offsets +representativeYaw and -representativeYaw. " +
                "RepresentativeYaw is +57 degrees for SwordSlash/MagicCast, -24 for Throw, based on prior raw sideview diagnostics. " +
                "All variants use each default take's full first/last frames, rather than any subsequently trimmed import interval.",
            humanPoseInterpretation = "humanPoseBodyRotation and humanPoseForward store HumanPoseHandler.GetHumanPose output directly. " +
                "humanPoseYawFromActorForward treats that rotation as world space. An explicit root-local interpretation (actor.rotation * pose.bodyRotation) is also recorded, " +
                "alongside Animator.bodyRotation yaw, so coordinate-space differences are visible rather than silently assumed. Yaw is SignedAngle(actor.forward, planar body forward, up)."
        };
        var originalAssets = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<AnimationClip>().ToArray();
        foreach (string name in report.originalClipNames)
        {
            var clip = originalAssets.FirstOrDefault(candidate => candidate.name == name);
            if (!clip || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out string guid, out long id))
                throw new InvalidOperationException("Cannot record original clip reference: " + name);
            report.originalReferences.Add(new OriginalReference { clip = name, guid = guid, localFileId = id });
        }
        var definitions = new List<ModelImporterClipAnimation>(original);
        AddVariants(importer, definitions, report, "SwordSlash", "Left_Slash", 57);
        AddVariants(importer, definitions, report, "MagicCast", "mage_soell_cast", 57);
        AddVariants(importer, definitions, report, "Throw", "Over_Shoulder_Throw", -24);
        Exception failure = null;
        try
        {
            importer.clipAnimations = definitions.ToArray();
            importer.SaveAndReimport();
            var probeClips = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<AnimationClip>().ToArray();
            foreach (var variant in report.variants)
            {
                var clip = probeClips.FirstOrDefault(candidate => candidate.name == variant.name);
                if (!clip || !clip.humanMotion) throw new InvalidOperationException("Temporary Humanoid clip not imported: " + variant.name);
                Measure(prefab, clip, variant);
            }
        }
        catch (Exception exception)
        {
            failure = exception;
            report.error = exception.ToString();
        }
        finally
        {
            try
            {
                importer = AssetImporter.GetAtPath(Fbx) as ModelImporter;
                if (importer == null) throw new InvalidOperationException("Importer disappeared before restoring orientation probes.");
                importer.clipAnimations = original;
                importer.SaveAndReimport();
                report.originalClipSettingsRestored = SameSettings(original, importer.clipAnimations);
                var restored = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<AnimationClip>().ToArray();
                report.temporaryClipsRemoved = restored.All(clip => !clip.name.StartsWith(Prefix, StringComparison.Ordinal));
                foreach (var reference in report.originalReferences)
                {
                    var clip = restored.FirstOrDefault(candidate => candidate.name == reference.clip);
                    reference.restored = clip && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out string guid, out long id) &&
                        guid == reference.guid && id == reference.localFileId;
                }
                report.originalReferencesStable = report.originalReferences.All(reference => reference.restored);
                if (!report.originalClipSettingsRestored || !report.originalReferencesStable || !report.temporaryClipsRemoved)
                    throw new InvalidOperationException("Orientation probe restoration verification failed; inspect the report.");
            }
            catch (Exception restoreException)
            {
                failure = failure == null ? restoreException : new AggregateException(failure, restoreException);
                report.error = failure.ToString();
            }
            report.success = failure == null;
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            File.WriteAllText(Output, JsonUtility.ToJson(report, true));
        }
        if (failure != null) throw new InvalidOperationException("Sideview orientation probe failed; importer restoration was attempted. " + Output, failure);
        Debug.Log("SIDEVIEW_ORIENTATION_PROBE_SUCCESS " + Output);
    }

    static void AddVariants(ModelImporter importer, List<ModelImporterClipAnimation> definitions, Report report,
        string action, string takeName, float representativeYaw)
    {
        var take = importer.defaultClipAnimations.FirstOrDefault(candidate => candidate.name == takeName || candidate.takeName == takeName);
        if (take == null) throw new InvalidOperationException("Missing original default take: " + takeName);
        int variantIndex = 0;
        foreach (bool keep in new[] { true, false })
            foreach (bool locked in new[] { false, true }) Add(keep, locked, 0);
        Add(false, true, representativeYaw);
        Add(false, true, -representativeYaw);
        void Add(bool keep, bool locked, float offset)
        {
            string name = Prefix + action + "_" + variantIndex++;
            definitions.Add(new ModelImporterClipAnimation
            {
                name = name, takeName = take.takeName,
                firstFrame = take.firstFrame, lastFrame = take.lastFrame,
                loopTime = false, loopPose = false, mirror = false,
                lockRootRotation = locked, keepOriginalOrientation = keep, rotationOffset = offset,
                lockRootPositionXZ = false, keepOriginalPositionXZ = true,
                lockRootHeightY = false, keepOriginalPositionY = true,
                events = Array.Empty<AnimationEvent>()
            });
            report.variants.Add(new Variant
            {
                action = action, sourceTake = take.takeName, name = name,
                keepOriginalOrientation = keep, lockRootRotation = locked, rotationOffset = offset,
                firstFrame = take.firstFrame, lastFrame = take.lastFrame
            });
        }
    }

    static void Measure(GameObject prefab, AnimationClip clip, Variant result)
    {
        var actor = Object.Instantiate(prefab);
        actor.hideFlags = HideFlags.HideAndDontSave;
        PlayableGraph graph = default;
        HumanPoseHandler handler = null;
        try
        {
            var facing = Quaternion.Euler(0, 90, 0);
            actor.transform.SetPositionAndRotation(Vector3.zero, facing);
            foreach (var behaviour in actor.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            foreach (var body in actor.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.useGravity = false; }
            foreach (var collider in actor.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            var animator = actor.GetComponent<Animator>();
            if (!animator || !animator.avatar || !animator.avatar.isHuman || !animator.avatar.isValid)
                throw new InvalidOperationException("Valid root Humanoid Animator required.");
            foreach (var nested in actor.GetComponentsInChildren<Animator>(true))
                if (nested != animator) nested.enabled = false;
            animator.enabled = true; animator.runtimeAnimatorController = null; animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind(); animator.Update(0);
            handler = new HumanPoseHandler(animator.avatar, actor.transform);
            var pose = new HumanPose();
            var right = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            graph = PlayableGraph.Create("Sideview orientation " + result.name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(false); playable.SetApplyPlayableIK(false);
            AnimationPlayableOutput.Create(graph, "Probe pose", animator).SetSourcePlayable(playable);
            graph.Play();
            result.clipSeconds = clip.length;
            foreach (float fraction in new[] { .2f, .5f, .8f })
            {
                actor.transform.SetPositionAndRotation(Vector3.zero, facing);
                playable.SetTime(clip.length * fraction); graph.Evaluate(0);
                Vector3 rootAfter = actor.transform.position;
                actor.transform.SetPositionAndRotation(Vector3.zero, facing);
                handler.GetHumanPose(ref pose);
                Vector3 rawForward = pose.bodyRotation * Vector3.forward;
                Vector3 localForward = actor.transform.rotation * rawForward;
                Vector3 animatorForward = animator.bodyRotation * Vector3.forward;
                result.samples.Add(new Sample
                {
                    normalizedTime = fraction, seconds = fraction * clip.length,
                    humanPoseBodyRotation = pose.bodyRotation, animatorBodyRotation = animator.bodyRotation,
                    actorForward = actor.transform.forward, humanPoseForward = rawForward,
                    humanPoseForwardIfRootLocal = localForward, animatorBodyForward = animatorForward,
                    humanPoseYawFromActorForward = Yaw(actor.transform.forward, rawForward),
                    humanPoseRootLocalYawFromActorForward = Yaw(actor.transform.forward, localForward),
                    animatorBodyYawFromActorForward = Yaw(actor.transform.forward, animatorForward),
                    hips = hips ? hips.position : Vector3.zero,
                    rightHand = right ? right.position : Vector3.zero, leftHand = left ? left.position : Vector3.zero,
                    actorPositionAfterEvaluation = rootAfter
                });
            }
            result.humanPoseYawMean = result.samples.Average(sample => sample.humanPoseYawFromActorForward);
            result.humanPoseYawMin = result.samples.Min(sample => sample.humanPoseYawFromActorForward);
            result.humanPoseYawMax = result.samples.Max(sample => sample.humanPoseYawFromActorForward);
            result.humanPoseRootLocalYawMean = result.samples.Average(sample => sample.humanPoseRootLocalYawFromActorForward);
            result.animatorBodyYawMean = result.samples.Average(sample => sample.animatorBodyYawFromActorForward);
            result.animatorBodyYawMin = result.samples.Min(sample => sample.animatorBodyYawFromActorForward);
            result.animatorBodyYawMax = result.samples.Max(sample => sample.animatorBodyYawFromActorForward);
        }
        finally
        {
            if (graph.IsValid()) graph.Destroy();
            if (handler != null) handler.Dispose();
            Object.DestroyImmediate(actor);
        }
    }

    static float Yaw(Vector3 forward, Vector3 bodyForward) =>
        Vector3.SignedAngle(forward, Vector3.ProjectOnPlane(bodyForward, Vector3.up), Vector3.up);

    static bool SameSettings(ModelImporterClipAnimation[] before, ModelImporterClipAnimation[] after)
    {
        if (before.Length != after.Length) return false;
        for (int index = 0; index < before.Length; index++)
        {
            var a = before[index]; var b = after[index];
            if (a.name != b.name || a.takeName != b.takeName || a.firstFrame != b.firstFrame || a.lastFrame != b.lastFrame ||
                a.keepOriginalOrientation != b.keepOriginalOrientation || a.lockRootRotation != b.lockRootRotation ||
                a.rotationOffset != b.rotationOffset || a.mirror != b.mirror || a.loopTime != b.loopTime ||
                a.lockRootHeightY != b.lockRootHeightY || a.lockRootPositionXZ != b.lockRootPositionXZ) return false;
        }
        return true;
    }
}
#endif
