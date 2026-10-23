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

/// <summary>Measures sampled Humanoid poses independently from controller playback speed.</summary>
public static class SideviewMotionDiagnostics
{
    const string Hero = "Assets/Art/Characters/Protagonist01/Runtime";
    const string ReportPath = "ArtSources/Stage01Kit/SideviewReview/motion-diagnostics.json";
    const int Samples = 120;
    static readonly Quaternion FacingRight = Quaternion.Euler(0, 90, 0);
    static readonly Vector3 GameplayForward = Vector3.right;
    static readonly string[] ClipNames =
    {
        "Drink", "Throw", "SwordSlash", "MagicCast", "Interact", "Pickup",
        "Punch", "Walk", "Run", "JumpFull", "Dash", "Hit", "Dead"
    };

    [Serializable] public sealed class Pose
    {
        public int index;
        public float normalizedTime, seconds, sourceNormalizedTime, sourceSeconds;
        public Vector3 rightHand, leftHand, rightFoot, leftFoot, head, hips;
        public Vector3 bodyPosition, rootPositionAfterEvaluation, rootForwardAfterEvaluation, bodyForward;
        public float rootHeight, hipsHeight, bodyHeight;
        public float rightHandForward, leftHandForward, rightFootForward, leftFootForward, headForward, hipsForward;
        public float rightHandForwardFromHips, leftHandForwardFromHips;
        public float rightFootForwardVelocity, leftFootForwardVelocity, bodyYawFromGameplay;
        public bool meshBaked;
        public float meshMinY, meshMaxY;
    }

    [Serializable] public sealed class FootSummary
    {
        public float minY, maxY, lowStanceThresholdY;
        public int backwardsLowStanceSamples;
        public float backwardsLowStanceVelocityMedian;
    }

    [Serializable] public sealed class ClipSummary
    {
        public string name, assetPath, sourceTake;
        public float sampledDurationSeconds, frameRate, defaultSourceDurationSeconds;
        public float sourceFirstFrame, sourceLastFrame, importedFirstFrame, importedLastFrame;
        public bool humanoid, loop, importRangeMatchesFullSource;
        public float minHipsY, maxHipsY, minBodyY, maxBodyY, minMeshY, maxMeshY;
        public float minBodyYawFromGameplay, maxBodyYawFromGameplay;
        public float maxRightHandForwardFromHips, maxLeftHandForwardFromHips;
        public float maxRightHandForwardNormalizedTime, maxLeftHandForwardNormalizedTime;
        public float naturalStrideSpeed;
        public int strideCandidateCount;
        public FootSummary rightFoot = new FootSummary();
        public FootSummary leftFoot = new FootSummary();
        public List<Pose> poses = new List<Pose>();
    }

    [Serializable] public sealed class Report
    {
        public bool success;
        public string unity, sampledPrefab, method, strideMethod, sourceNotes, limitations;
        public int samplesPerClip, meshBakeSampleStep;
        public Vector3 actorPosition, actorEuler, gameplayForward;
        public List<ClipSummary> clips = new List<ClipSummary>();
    }

    [MenuItem("Tools/Project M/Analyze Sideview Character Motion")]
    public static void Analyze()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Hero + "/Protagonist01_Player.prefab");
        if (!prefab) throw new InvalidOperationException("Missing protagonist player prefab.");
        var clips = LoadClips();
        var report = new Report
        {
            unity = Application.unityVersion,
            sampledPrefab = AssetDatabase.GetAssetPath(prefab),
            samplesPerClip = Samples, meshBakeSampleStep = 4,
            actorPosition = Vector3.zero, actorEuler = new Vector3(0, 90, 0), gameplayForward = GameplayForward,
            method = "A disposable player instance per clip. All MonoBehaviours disabled, physics and colliders disabled, nested Animators removed. " +
                "The valid root Humanoid Animator is evaluated by a manual AnimationClipPlayable, no controller, root motion, foot IK or playable IK. " +
                "120 inclusive samples cover the imported clip's own duration, independently of controller state speed. Actor origin and Y=90 facing +X are reset before/after each evaluation. " +
                "Coordinates and body direction are recorded after evaluation; mesh world bounds are baked every fourth sample and the last sample.",
            strideMethod = "For each foot, low stance is minY + 0.25*(maxY-minY). Central finite differences of world +X foot position give forward velocity. " +
                "Samples with foot at/below that threshold and velocity < -0.01 m/s are collected across both feet. The median of their negated velocities estimates naturalStrideSpeed. " +
                "Walk/Run only; it is an in-place visual stride estimate, not measured game movement speed or an automatic playback recommendation.",
            sourceNotes = "Walk and Run: Protagonist01_Walk.fbx / Protagonist01_Run.fbx. Punch and JumpFull: Protagonist01_Actions.fbx. " +
                "Six extra actions: Protagonist01_ExtendedActions.fbx (Stand_and_Drink, Over_Shoulder_Throw, Left_Slash, mage_soell_cast, open_door, Male_Bend_Over_Pick_Up). " +
                "Dash, Hit and Dead: Assets/Anim/Clips/*.anim. Source-take/import frame intervals are included when a ModelImporter exists.",
            limitations = "Source-take duration assumes the imported clip frame rate. If an FBX import interval was trimmed before this analysis, only that imported interval can be sampled without reimporting; " +
                "sourceNormalizedTime/sourceSeconds map poses back to the original default take and importRangeMatchesFullSource explicitly reports this. " +
                "No collision, foot planting IK, physics trajectory, equipped props or controller transitions are applied. Mesh minY includes the full character silhouette and is not solely a foot sole. " +
                "Resetting the actor root intentionally exposes pose height and side-to-side/depth motion retained in the imported pose."
        };
        foreach (string name in ClipNames)
        {
            if (!clips.TryGetValue(name, out var clip) || !clip)
                throw new InvalidOperationException("Missing diagnostic clip: " + name);
            report.clips.Add(AnalyzeClip(prefab, clip));
        }
        report.success = true;
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
        Debug.Log("SIDEVIEW_MOTION_DIAGNOSTICS_SUCCESS " + ReportPath + " clips=" + report.clips.Count);
    }

    static Dictionary<string, AnimationClip> LoadClips()
    {
        var result = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
        foreach (string file in new[] { "Protagonist01_Walk.fbx", "Protagonist01_Run.fbx", "Protagonist01_Actions.fbx", "Protagonist01_ExtendedActions.fbx" })
            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(Hero + "/" + file).OfType<AnimationClip>())
                if (!clip.name.StartsWith("__preview__", StringComparison.Ordinal)) result[clip.name] = clip;
        foreach (string name in new[] { "Dash", "Hit", "Dead" })
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Anim/Clips/" + name + ".anim");
            if (clip) result[name] = clip;
        }
        return result;
    }

    static ClipSummary AnalyzeClip(GameObject prefab, AnimationClip clip)
    {
        if (!clip.humanMotion || clip.length <= 0) throw new InvalidOperationException("Expected finite Humanoid clip: " + clip.name);
        var result = SourceSummary(clip);
        var actor = Object.Instantiate(prefab);
        actor.hideFlags = HideFlags.HideAndDontSave;
        var baked = new Mesh { name = "Sideview diagnostic bake" };
        PlayableGraph graph = default;
        try
        {
            actor.transform.SetPositionAndRotation(Vector3.zero, FacingRight);
            foreach (var behaviour in actor.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            foreach (var body in actor.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.useGravity = false; }
            foreach (var collider in actor.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            var animator = actor.GetComponent<Animator>();
            if (!animator || !animator.avatar || !animator.avatar.isHuman || !animator.avatar.isValid)
                throw new InvalidOperationException("Valid root Humanoid Animator required for diagnostic sampling.");
            foreach (var nested in actor.GetComponentsInChildren<Animator>(true))
                if (nested != animator) nested.enabled = false;
            animator.enabled = true;
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind(); animator.Update(0);
            var rightHand = RequiredBone(animator, HumanBodyBones.RightHand);
            var leftHand = RequiredBone(animator, HumanBodyBones.LeftHand);
            var rightFoot = RequiredBone(animator, HumanBodyBones.RightFoot);
            var leftFoot = RequiredBone(animator, HumanBodyBones.LeftFoot);
            var head = RequiredBone(animator, HumanBodyBones.Head);
            var hips = RequiredBone(animator, HumanBodyBones.Hips);
            var renderers = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var renderer in renderers) renderer.updateWhenOffscreen = true;
            graph = PlayableGraph.Create("Sideview diagnostic " + clip.name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(false); playable.SetApplyPlayableIK(false);
            AnimationPlayableOutput.Create(graph, "Diagnostic pose", animator).SetSourcePlayable(playable);
            graph.Play();
            for (int index = 0; index < Samples; index++)
            {
                float fraction = index / (float)(Samples - 1);
                actor.transform.SetPositionAndRotation(Vector3.zero, FacingRight);
                playable.SetTime(clip.length * fraction);
                graph.Evaluate(0);
                var rootAfter = actor.transform.position;
                var forwardAfter = actor.transform.forward;
                actor.transform.SetPositionAndRotation(Vector3.zero, FacingRight);
                float frame = Mathf.Lerp(result.importedFirstFrame, result.importedLastFrame, fraction);
                float sourceFrames = result.sourceLastFrame - result.sourceFirstFrame;
                var pose = new Pose
                {
                    index = index, normalizedTime = fraction, seconds = clip.length * fraction,
                    sourceNormalizedTime = sourceFrames > .0001f ? (frame - result.sourceFirstFrame) / sourceFrames : fraction,
                    sourceSeconds = result.frameRate > .0001f ? (frame - result.sourceFirstFrame) / result.frameRate : clip.length * fraction,
                    rightHand = rightHand.position, leftHand = leftHand.position,
                    rightFoot = rightFoot.position, leftFoot = leftFoot.position,
                    head = head.position, hips = hips.position, bodyPosition = animator.bodyPosition,
                    rootPositionAfterEvaluation = rootAfter, rootForwardAfterEvaluation = forwardAfter,
                    rootHeight = rootAfter.y, hipsHeight = hips.position.y, bodyHeight = animator.bodyPosition.y,
                    bodyForward = animator.bodyRotation * Vector3.forward,
                    rightHandForward = Vector3.Dot(rightHand.position, GameplayForward),
                    leftHandForward = Vector3.Dot(leftHand.position, GameplayForward),
                    rightFootForward = Vector3.Dot(rightFoot.position, GameplayForward),
                    leftFootForward = Vector3.Dot(leftFoot.position, GameplayForward),
                    headForward = Vector3.Dot(head.position, GameplayForward),
                    hipsForward = Vector3.Dot(hips.position, GameplayForward),
                    rightHandForwardFromHips = Vector3.Dot(rightHand.position - hips.position, GameplayForward),
                    leftHandForwardFromHips = Vector3.Dot(leftHand.position - hips.position, GameplayForward)
                };
                pose.bodyYawFromGameplay = Vector3.SignedAngle(GameplayForward,
                    Vector3.ProjectOnPlane(pose.bodyForward, Vector3.up), Vector3.up);
                if (index % 4 == 0 || index == Samples - 1)
                {
                    pose.meshBaked = true;
                    pose.meshMinY = float.PositiveInfinity; pose.meshMaxY = float.NegativeInfinity;
                    foreach (var renderer in renderers)
                    {
                        baked.Clear(); renderer.BakeMesh(baked);
                        foreach (var vertex in baked.vertices)
                        {
                            float y = renderer.transform.TransformPoint(vertex).y;
                            pose.meshMinY = Mathf.Min(pose.meshMinY, y);
                            pose.meshMaxY = Mathf.Max(pose.meshMaxY, y);
                        }
                    }
                    if (float.IsInfinity(pose.meshMinY)) throw new InvalidOperationException("Empty character mesh during diagnostic sampling.");
                }
                result.poses.Add(pose);
            }
            CompleteSummary(result);
            return result;
        }
        finally
        {
            if (graph.IsValid()) graph.Destroy();
            Object.DestroyImmediate(baked);
            Object.DestroyImmediate(actor);
        }
    }

    static Transform RequiredBone(Animator animator, HumanBodyBones bone)
    {
        var transform = animator.GetBoneTransform(bone);
        if (!transform) throw new InvalidOperationException("Missing diagnostic bone: " + bone);
        return transform;
    }

    static ClipSummary SourceSummary(AnimationClip clip)
    {
        var result = new ClipSummary
        {
            name = clip.name, assetPath = AssetDatabase.GetAssetPath(clip), sampledDurationSeconds = clip.length,
            frameRate = clip.frameRate, humanoid = clip.humanMotion, loop = clip.isLooping,
            sourceTake = clip.name, sourceFirstFrame = 0, sourceLastFrame = clip.length * clip.frameRate,
            importedFirstFrame = 0, importedLastFrame = clip.length * clip.frameRate,
            defaultSourceDurationSeconds = clip.length, importRangeMatchesFullSource = true
        };
        var importer = AssetImporter.GetAtPath(result.assetPath) as ModelImporter;
        if (importer != null)
        {
            var imported = importer.clipAnimations.FirstOrDefault(candidate => candidate.name == clip.name);
            if (imported != null)
            {
                result.sourceTake = imported.takeName;
                result.importedFirstFrame = imported.firstFrame; result.importedLastFrame = imported.lastFrame;
                var original = importer.defaultClipAnimations.FirstOrDefault(candidate =>
                    candidate.takeName == imported.takeName || candidate.name == imported.takeName);
                if (original != null)
                {
                    result.sourceFirstFrame = original.firstFrame; result.sourceLastFrame = original.lastFrame;
                    result.defaultSourceDurationSeconds = (original.lastFrame - original.firstFrame) / Mathf.Max(.0001f, clip.frameRate);
                    result.importRangeMatchesFullSource = Mathf.Abs(original.firstFrame - imported.firstFrame) < .01f &&
                        Mathf.Abs(original.lastFrame - imported.lastFrame) < .01f;
                }
            }
        }
        return result;
    }

    static void CompleteSummary(ClipSummary result)
    {
        var poses = result.poses;
        for (int index = 0; index < poses.Count; index++)
        {
            int first = Mathf.Max(0, index - 1), last = Mathf.Min(poses.Count - 1, index + 1);
            float delta = poses[last].seconds - poses[first].seconds;
            poses[index].rightFootForwardVelocity = (poses[last].rightFootForward - poses[first].rightFootForward) / Mathf.Max(.000001f, delta);
            poses[index].leftFootForwardVelocity = (poses[last].leftFootForward - poses[first].leftFootForward) / Mathf.Max(.000001f, delta);
        }
        result.minHipsY = poses.Min(p => p.hipsHeight); result.maxHipsY = poses.Max(p => p.hipsHeight);
        result.minBodyY = poses.Min(p => p.bodyHeight); result.maxBodyY = poses.Max(p => p.bodyHeight);
        result.minMeshY = poses.Where(p => p.meshBaked).Min(p => p.meshMinY);
        result.maxMeshY = poses.Where(p => p.meshBaked).Max(p => p.meshMaxY);
        result.minBodyYawFromGameplay = poses.Min(p => p.bodyYawFromGameplay);
        result.maxBodyYawFromGameplay = poses.Max(p => p.bodyYawFromGameplay);
        var rightPeak = poses.OrderByDescending(p => p.rightHandForwardFromHips).First();
        var leftPeak = poses.OrderByDescending(p => p.leftHandForwardFromHips).First();
        result.maxRightHandForwardFromHips = rightPeak.rightHandForwardFromHips;
        result.maxLeftHandForwardFromHips = leftPeak.leftHandForwardFromHips;
        result.maxRightHandForwardNormalizedTime = rightPeak.normalizedTime;
        result.maxLeftHandForwardNormalizedTime = leftPeak.normalizedTime;
        var rightSpeeds = FootStats(poses, true, result.rightFoot);
        var leftSpeeds = FootStats(poses, false, result.leftFoot);
        if (result.name == "Walk" || result.name == "Run")
        {
            rightSpeeds.AddRange(leftSpeeds);
            result.strideCandidateCount = rightSpeeds.Count;
            result.naturalStrideSpeed = Median(rightSpeeds);
        }
    }

    static List<float> FootStats(List<Pose> poses, bool right, FootSummary summary)
    {
        summary.minY = poses.Min(p => right ? p.rightFoot.y : p.leftFoot.y);
        summary.maxY = poses.Max(p => right ? p.rightFoot.y : p.leftFoot.y);
        summary.lowStanceThresholdY = Mathf.Lerp(summary.minY, summary.maxY, .25f);
        var candidates = new List<float>();
        foreach (var pose in poses)
        {
            float y = right ? pose.rightFoot.y : pose.leftFoot.y;
            float velocity = right ? pose.rightFootForwardVelocity : pose.leftFootForwardVelocity;
            if (y <= summary.lowStanceThresholdY && velocity < -.01f) candidates.Add(-velocity);
        }
        summary.backwardsLowStanceSamples = candidates.Count;
        summary.backwardsLowStanceVelocityMedian = Median(candidates);
        return candidates;
    }

    static float Median(List<float> values)
    {
        if (values.Count == 0) return 0;
        values.Sort();
        int middle = values.Count / 2;
        return values.Count % 2 == 0 ? (values[middle - 1] + values[middle]) * .5f : values[middle];
    }
}
#endif
