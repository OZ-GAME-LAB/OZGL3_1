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

public static class StaffTipBoltVerification
{
    const string Folder = "Assets/Art/Characters/Protagonist01/AnimationStudies/StaffTipBolt20261102/";
    const int ReleaseFrame = 26;

    // EffectTip in the right-facing view of the approved staff motion (30 fps review,
    // converted to the clip's 60 fps frame indices). The opposite view is a 180° yaw.
    static readonly Dictionary<int, Vector3> StaffTipBaseline = new Dictionary<int, Vector3>
    {
        { 0, new Vector3(.569659f, .862937f, .354462f) },
        { 14, new Vector3(.434385f, .993811f, .356263f) },
        { 26, new Vector3(.769103f, .533207f, .342400f) },
        { 38, new Vector3(.627219f, .776961f, .385016f) },
        { 54, new Vector3(.569659f, .862937f, .354462f) }
    };

    [Serializable] class FreeHand
    {
        public float armLength, minGripR = float.PositiveInfinity, maxGripR = float.NegativeInfinity;
        public float minElbowAboveWrist = float.PositiveInfinity;
        public Vector3 wristFromRightHipMin, wristFromRightHipMax;
        public float maxStepRelativeToHip, maxStaffTipDifference;
        public int sampledFrames, comparedStaffFrames;
        public bool passed;
    }

    [Serializable] class Span
    {
        public float min = float.PositiveInfinity, max = float.NegativeInfinity, range;
        public int firstMinFrame, firstMaxFrame;
        public void Record(float value, int frame)
        {
            if (value < min) { min = value; firstMinFrame = frame; }
            if (value > max) { max = value; firstMaxFrame = frame; }
            range = max - min;
        }
    }

    [Serializable] class Sway
    {
        public Span shoulderFromPelvis = new Span(), elbowFromShoulder = new Span();
        public Span wristFromShoulder = new Span(), wristFromPelvis = new Span();
        public float maxWristStepFromShoulder, maxElbowStepFromShoulder;
        public int wristMinAfterElbowMinFrames, wristMaxAfterElbowMaxFrames;
        public bool passed;
    }

    class Pose
    {
        public readonly Vector3[] positions;
        public readonly Quaternion[] rotations;
        public Pose(Transform[] joints)
        {
            positions = joints.Select(joint => joint.position).ToArray();
            rotations = joints.Select(joint => joint.rotation).ToArray();
        }
    }

    [Serializable] class Seam
    {
        public string name;
        public int facingX;
        public float maxJointDistance, maxJointAngle;
        public bool passed;

        public Seam(string name, int facingX, Pose from, Pose to)
        {
            this.name = name;
            this.facingX = facingX;
            for (int i = 0; i < from.positions.Length; i++)
            {
                maxJointDistance = Mathf.Max(maxJointDistance, Vector3.Distance(from.positions[i], to.positions[i]));
                maxJointAngle = Mathf.Max(maxJointAngle, Quaternion.Angle(from.rotations[i], to.rotations[i]));
            }
            passed = maxJointDistance <= .015f && maxJointAngle <= 5f;
        }
    }

    [Serializable] class View
    {
        public int facingX;
        public float worstAimDegreesFrames21To26, releaseAimDegrees, releaseTipBeyondHeadMetres;
        public float maxFootMovementFrames21To26, maxSoleHeightChangeMetres, lowestVertexY, highestFloorY;
        public float maxTipMovementMetres;
        public Vector3 releaseTip, releaseDirection;
        public FreeHand freeHand = new FreeHand();
        public Sway sway = new Sway();
        [NonSerialized] public Pose start, end;
        public bool passed;
    }

    [Serializable] class Report
    {
        public string unity, prefab, clip;
        public string method = "Saved Generic .anim through a manual AnimationClipPlayable, on independent saved-prefab clones facing world +X and -X. Staff direction is measured from the physical grip pivot to its actual EffectTip. Sole height is the minimum world Y of the deformed character skin.";
        public string scope = "Clip and equipped study prefab only. Horizontal release at frame 26/60. This does not simulate skill input, cursor elevation, damage, or a runtime projectile.";
        public int bindings, animatedTransformPaths, releaseFrame = ReleaseFrame;
        public bool generic, lengthMatches, gripIsOnLeftHand, passed;
        public string[] unresolvedBindings;
        public List<View> views = new List<View>();
        public string idleClip = Folder + "Study_StaffTipIdle_Chibi.anim";
        public bool idleLoops;
        public string[] unresolvedIdleBindings;
        public List<View> idleViews = new List<View>();
        public List<Seam> seams = new List<Seam>();
        public string swayUnits = "Forward projections are horizontal metres in the gameplay side view. Peak indices are first extrema at 60 fps. The previous cast's shoulder-relative wrist and elbow excursions were 0.0236 and 0.0147 m, with synchronous extrema at frames 12 and 22. Seams compare the two neutral endpoints using all skinned bones and the effect tip.";
    }

    public static void Run()
    {
        var report = new Report { unity = Application.unityVersion, prefab = Folder + "Player_Chibi_StaffTipBolt.prefab", clip = Folder + "Study_StaffTipBolt_Chibi.anim" };
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(report.prefab);
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(report.clip);
        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(report.idleClip);
        if (!prefab || !clip || !idle) throw new InvalidOperationException("Save the staff-tip prefab, cast and idle clips before verification.");
        var bindings = AnimationUtility.GetCurveBindings(clip);
        report.bindings = bindings.Length;
        report.unresolvedBindings = bindings.Where(binding => AnimationUtility.GetAnimatedObject(prefab, binding) == null)
            .Select(binding => binding.path + " :: " + binding.propertyName).ToArray();
        report.animatedTransformPaths = bindings.Where(binding => binding.type == typeof(Transform)).Select(binding => binding.path).Distinct().Count();
        report.generic = !clip.humanMotion && !prefab.GetComponent<Animator>().avatar;
        report.lengthMatches = Mathf.Abs(clip.length - .9f) < .01f && Mathf.Abs(clip.frameRate - 60f) < .01f;
        var transforms = prefab.GetComponentsInChildren<Transform>(true);
        report.gripIsOnLeftHand = transforms.Single(t => t.name == "Study_LeftStaff").IsChildOf(transforms.Single(t => t.name == "LeftHand"));
        report.idleLoops = AnimationUtility.GetAnimationClipSettings(idle).loopTime && !idle.humanMotion &&
            Mathf.Abs(idle.frameRate - 60f) < .01f && Mathf.Abs(idle.length - 2.4f) < .01f;
        var idleBindings = AnimationUtility.GetCurveBindings(idle);
        report.unresolvedIdleBindings = idleBindings.Where(binding => AnimationUtility.GetAnimatedObject(prefab, binding) == null)
            .Select(binding => binding.path + " :: " + binding.propertyName).ToArray();
        foreach (int facingX in new[] { 1, -1 })
        {
            var castView = Measure(prefab, clip, facingX, false);
            var idleView = Measure(prefab, idle, facingX, true);
            report.views.Add(castView);
            report.idleViews.Add(idleView);
            report.seams.Add(new Seam("idle loop", facingX, idleView.end, idleView.start));
            report.seams.Add(new Seam("idle neutral to cast", facingX, idleView.start, castView.start));
            report.seams.Add(new Seam("cast to idle neutral", facingX, castView.end, idleView.start));
        }
        report.passed = report.generic && report.lengthMatches && report.gripIsOnLeftHand && report.animatedTransformPaths > 0 &&
            report.unresolvedBindings.Length == 0 && report.views.All(view => view.passed) &&
            report.idleLoops && idleBindings.Length > 0 && report.unresolvedIdleBindings.Length == 0 &&
            report.idleViews.All(view => view.passed) && report.seams.All(seam => seam.passed);
        string output = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "OZGL3_ArtReview/Characters/Protagonist01/Studies/ChibiStaffAimCast20261102/idle-sway/verification.json");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        File.WriteAllText(output, JsonUtility.ToJson(report, true));
        if (!report.passed) throw new InvalidOperationException("STAFF_TIP_VERIFICATION_FAILED. See " + output);
        Debug.Log("STAFF_TIP_VERIFICATION_PASSED " + output);
    }

    static View Measure(GameObject prefab, AnimationClip clip, int facingX, bool isIdle)
    {
        var holder = new GameObject("Temporary cast verification") { hideFlags = HideFlags.HideAndDontSave };
        holder.SetActive(false);
        var mesh = new Mesh();
        var graph = PlayableGraph.Create("Independent cast evaluation");
        try
        {
            var actor = Object.Instantiate(prefab, holder.transform, false);
            foreach (var behaviour in actor.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            var animator = actor.GetComponent<Animator>();
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.fireEvents = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            actor.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.LookRotation(Vector3.right * facingX));
            actor.SetActive(true);
            animator.enabled = true;
            holder.SetActive(true);
            var parts = actor.GetComponentsInChildren<Transform>(true);
            var handle = parts.Single(t => t.name == "Study_LeftStaff");
            var tip = handle.Find("EffectTip");
            var head = parts.Single(t => t.name == "Head");
            var upperArm = parts.Single(t => t.name == "RightArm");
            var elbow = parts.Single(t => t.name == "RightForeArm");
            var wrist = parts.Single(t => t.name == "RightHand");
            var rightHip = parts.Single(t => t.name == "RightUpLeg");
            var pelvis = parts.Single(t => t.name == "Hips");
            var feet = parts.Where(t => t.name == "LeftFoot" || t.name == "RightFoot").ToArray();
            if (!tip || feet.Length != 2) throw new InvalidOperationException("Missing staff muzzle or either foot.");
            var skin = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(s => s.sharedMesh.vertexCount).First();
            var seamJoints = skin.bones.Concat(new[] { tip }).ToArray();
            int gripIndex = skin.sharedMesh.GetBlendShapeIndex("Grip_R");
            if (gripIndex < 0) throw new InvalidOperationException("The equipped skin has no Grip_R blend shape.");
            var vertices = new List<Vector3>(skin.sharedMesh.vertexCount);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(false);
            playable.SetApplyPlayableIK(false);
            AnimationPlayableOutput.Create(graph, "Equipped study actor", animator).SetSourcePlayable(playable);
            graph.Play();
            var view = new View { facingX = facingX, lowestVertexY = float.PositiveInfinity, highestFloorY = float.NegativeInfinity };
            var footStart = new Vector3[2];
            var firstTip = Vector3.zero;
            var lastWristOffset = Vector3.zero;
            var lastWristFromShoulder = Vector3.zero;
            var lastElbowFromShoulder = Vector3.zero;
            float initialFloor = 0f;
            int lastFrame = Mathf.RoundToInt(clip.length * 60);
            for (int frame = 0; frame <= lastFrame; frame++)
            {
                // Inspect the actual last pose just before the looping playable wraps to zero.
                playable.SetTime(isIdle && frame == lastFrame ? clip.length - .00001 : frame / 60.0);
                graph.Evaluate(0);
                if (frame == 0) view.start = new Pose(seamJoints);
                if (frame == lastFrame) view.end = new Pose(seamJoints);
                var sway = view.sway;
                var wristFromShoulder = wrist.position - upperArm.position;
                var elbowFromShoulder = elbow.position - upperArm.position;
                sway.shoulderFromPelvis.Record((upperArm.position.x - pelvis.position.x) * facingX, frame);
                sway.wristFromPelvis.Record((wrist.position.x - pelvis.position.x) * facingX, frame);
                sway.elbowFromShoulder.Record(elbowFromShoulder.x * facingX, frame);
                sway.wristFromShoulder.Record(wristFromShoulder.x * facingX, frame);
                if (frame > 0)
                {
                    sway.maxWristStepFromShoulder = Mathf.Max(sway.maxWristStepFromShoulder, Vector3.Distance(wristFromShoulder, lastWristFromShoulder));
                    sway.maxElbowStepFromShoulder = Mathf.Max(sway.maxElbowStepFromShoulder, Vector3.Distance(elbowFromShoulder, lastElbowFromShoulder));
                }
                lastWristFromShoulder = wristFromShoulder;
                lastElbowFromShoulder = elbowFromShoulder;
                var hand = view.freeHand;
                float grip = skin.GetBlendShapeWeight(gripIndex);
                hand.minGripR = Mathf.Min(hand.minGripR, grip);
                hand.maxGripR = Mathf.Max(hand.maxGripR, grip);
                hand.minElbowAboveWrist = Mathf.Min(hand.minElbowAboveWrist, elbow.position.y - wrist.position.y);
                // World-metre offsets in anatomical right / up / facing axes. Excluding
                // pelvis travel keeps torso anticipation from masquerading as an arm swing.
                var offset = Quaternion.Inverse(actor.transform.rotation) * (wrist.position - rightHip.position);
                if (frame == 0)
                {
                    hand.armLength = Vector3.Distance(upperArm.position, elbow.position) + Vector3.Distance(elbow.position, wrist.position);
                    hand.wristFromRightHipMin = hand.wristFromRightHipMax = offset;
                }
                else
                {
                    hand.wristFromRightHipMin = Vector3.Min(hand.wristFromRightHipMin, offset);
                    hand.wristFromRightHipMax = Vector3.Max(hand.wristFromRightHipMax, offset);
                    hand.maxStepRelativeToHip = Mathf.Max(hand.maxStepRelativeToHip, Vector3.Distance(offset, lastWristOffset));
                }
                lastWristOffset = offset;
                hand.sampledFrames++;
                if (!isIdle && StaffTipBaseline.TryGetValue(frame, out var expected))
                {
                    var expectedWorld = new Vector3(facingX * expected.x, expected.y, facingX * expected.z);
                    hand.maxStaffTipDifference = Mathf.Max(hand.maxStaffTipDifference, Vector3.Distance(expectedWorld, tip.position));
                    hand.comparedStaffFrames++;
                }
                skin.BakeMesh(mesh);
                mesh.GetVertices(vertices);
                float floor = vertices.Min(v => skin.transform.TransformPoint(v).y);
                if (frame == 0) { initialFloor = floor; firstTip = tip.position; }
                view.lowestVertexY = Mathf.Min(view.lowestVertexY, floor);
                view.highestFloorY = Mathf.Max(view.highestFloorY, floor);
                view.maxSoleHeightChangeMetres = Mathf.Max(view.maxSoleHeightChangeMetres, Mathf.Abs(floor - initialFloor));
                view.maxTipMovementMetres = Mathf.Max(view.maxTipMovementMetres, Vector3.Distance(tip.position, firstTip));
                if (isIdle || frame < 21 || frame > ReleaseFrame) continue;
                var axis = (tip.position - handle.position).normalized;
                float error = Vector3.Angle(axis, Vector3.right * facingX);
                view.worstAimDegreesFrames21To26 = Mathf.Max(view.worstAimDegreesFrames21To26, error);
                for (int i = 0; i < feet.Length; i++)
                {
                    if (frame == 21) footStart[i] = feet[i].position;
                    view.maxFootMovementFrames21To26 = Mathf.Max(view.maxFootMovementFrames21To26, Vector3.Distance(feet[i].position, footStart[i]));
                }
                if (frame != ReleaseFrame) continue;
                view.releaseAimDegrees = error;
                view.releaseTip = tip.position;
                view.releaseDirection = axis;
                view.releaseTipBeyondHeadMetres = (tip.position.x - head.position.x) * facingX;
            }
            var fist = view.freeHand;
            float length = fist.armLength;
            var min = fist.wristFromRightHipMin;
            var max = fist.wristFromRightHipMax;
            fist.passed = fist.sampledFrames == lastFrame + 1 && fist.minGripR >= 99.5f && fist.maxGripR <= 100.5f &&
                fist.minElbowAboveWrist > .08f * length && min.x >= -.12f * length && max.x <= .76f * length &&
                min.y >= -.60f * length && max.y <= .35f * length && min.z >= -.60f * length && max.z <= .50f * length &&
                max.z - min.z <= .52f * length && fist.maxStepRelativeToHip <= .12f * length &&
                (isIdle || (fist.comparedStaffFrames == StaffTipBaseline.Count && fist.maxStaffTipDifference <= .005f));
            var motion = view.sway;
            motion.wristMinAfterElbowMinFrames = motion.wristFromShoulder.firstMinFrame - motion.elbowFromShoulder.firstMinFrame;
            motion.wristMaxAfterElbowMaxFrames = motion.wristFromShoulder.firstMaxFrame - motion.elbowFromShoulder.firstMaxFrame;
            motion.passed = motion.wristFromShoulder.range >= (isIdle ? .018f : .028f) &&
                motion.wristFromShoulder.range <= .42f * length && motion.elbowFromShoulder.range >= .004f &&
                motion.wristFromPelvis.range >= .008f && motion.maxWristStepFromShoulder <= .08f * length &&
                motion.maxElbowStepFromShoulder <= .08f * length;
            bool castPassed = isIdle || (view.maxTipMovementMetres > .08f && view.worstAimDegreesFrames21To26 < 12f &&
                view.releaseTipBeyondHeadMetres > .10f && view.maxFootMovementFrames21To26 < .05f &&
                view.releaseAimDegrees < 12f);
            view.passed = fist.passed && motion.passed && castPassed && view.maxSoleHeightChangeMetres < .08f &&
                view.lowestVertexY > -.04f && view.highestFloorY < .04f;
            return view;
        }
        finally
        {
            graph.Destroy();
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(holder);
        }
    }
}
#endif
