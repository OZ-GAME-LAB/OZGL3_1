#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Offline conversion only. The caller owns the source/target instances and saves
// returned clips. This utility never changes an AnimatorController or source asset.
public static class CombatRetargetUtility
{
    public sealed class Options
    {
        public bool preserveTwoHandGrip = false;
        public bool preserveSourceRootTranslation = true;
        public float rootMotionScale = 1f;
        public bool solveLegEndpoints = true;
        public bool animateWeaponSocket = true;
        // The approved source and native target body meshes are both 1.2 m tall.
        // Bone-chain length differences must not resize the rigid weapon. For a
        // different-sized character, set this to its target/source body-size ratio.
        public float weaponScale = 1f;
        public Vector3 rightPalmLocal = new Vector3(-.024f, .068f, -.0205f);
        public Vector3 leftPalmLocal = new Vector3(.024f, .068f, -.0205f);
        public string targetWeaponSocketName = "WeaponSocket";
    }

    [Serializable] public sealed class FrameMetrics
    {
        public float time, rightGripError, leftGripError, weaponReachCorrection;
        public float rightArmExtension, leftArmExtension, rightForearmCorrectionDegrees, leftForearmCorrectionDegrees;
        public float rightUpperDirectionError, leftUpperDirectionError, rightForearmDirectionError, leftForearmDirectionError;
        public Vector3 weaponPosition, bladeAxis, rightWrist, leftWrist, rightElbow, leftElbow;
    }

    sealed class LocalPose
    {
        readonly Transform[] transforms;
        readonly Vector3[] positions, scales;
        readonly Quaternion[] rotations;
        public LocalPose(Transform root)
        {
            transforms = root.GetComponentsInChildren<Transform>(true);
            positions = transforms.Select(t => t.localPosition).ToArray();
            rotations = transforms.Select(t => t.localRotation).ToArray();
            scales = transforms.Select(t => t.localScale).ToArray();
        }
        public void Apply()
        {
            for (int i = 0; i < transforms.Length; ++i)
            {
                transforms[i].localPosition = positions[i];
                transforms[i].localRotation = rotations[i];
                transforms[i].localScale = scales[i];
            }
        }
    }

    public static Transform Find(GameObject actor, string name)
    {
        var found = actor.GetComponentsInChildren<Transform>(true).Where(t => t.name == name).ToArray();
        if (found.Length != 1) throw new InvalidOperationException(actor.name + ": expected one bone named " + name + ", found " + found.Length);
        return found[0];
    }

    static int Depth(Transform bone)
    {
        int n = 0;
        for (var t = bone; t.parent; t = t.parent) n++;
        return n;
    }

    // A prefab can contain an arbitrary saved animation pose. Its transform values
    // are not the mesh bind pose. Reconstruct the latter from the actual skin.
    static Dictionary<Transform, Matrix4x4> BindMatrices(GameObject actor)
    {
        var result = new Dictionary<Transform, Matrix4x4>();
        foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!skin.sharedMesh) continue;
            var bones = skin.bones;
            var bind = skin.sharedMesh.bindposes;
            if (bones.Length != bind.Length) throw new InvalidOperationException("Skin/bind matrix count mismatch: " + skin.name);
            for (int i = 0; i < bones.Length; i++)
                if (bones[i] && !result.ContainsKey(bones[i]))
                    result.Add(bones[i], skin.transform.localToWorldMatrix * bind[i].inverse);
        }
        if (result.Count == 0) throw new InvalidOperationException("No skinned bind matrices on " + actor.name);
        return result;
    }

    static void ApplyBindMatrices(Dictionary<Transform, Matrix4x4> matrices)
    {
        foreach (var pair in matrices.OrderBy(k => Depth(k.Key)))
            pair.Key.SetPositionAndRotation(pair.Value.GetColumn(3), pair.Value.rotation);
    }

    public static void RestoreBindPose(GameObject actor) { ApplyBindMatrices(BindMatrices(actor)); }

    public sealed class Retargeter : IDisposable
    {
        readonly GameObject source, target;
        readonly Options options;
        readonly Dictionary<string, Transform> s, t;
        readonly Dictionary<string, Vector3> sp, tp;
        readonly Dictionary<string, Quaternion> sq, tq;
        readonly LocalPose sourcePrefabPose, targetBindPose;
        readonly Quaternion alignment;
        readonly float legScale, armScale, rightLength, leftLength;
        readonly Transform sourceWeapon, sourceGripR, sourceGripL, socket;
        readonly Vector3 sourceRightGrip, sourceLeftGrip;
        readonly Transform[] bakeTransforms;
        readonly SkinnedMeshRenderer[] targetSkins;
        public FrameMetrics LastMetrics { get; private set; }
        public Quaternion WeaponRotation { get; private set; }
        public Vector3 WeaponPosition { get; private set; }
        public float WeaponScale { get { return options.weaponScale; } }

        public Retargeter(GameObject source, GameObject target, Options options = null)
        {
            if (!source || !target) throw new ArgumentNullException("source/target");
            this.source = source; this.target = target; this.options = options ?? new Options();
            if (this.options.weaponScale <= 0 || float.IsNaN(this.options.weaponScale) || float.IsInfinity(this.options.weaponScale))
                throw new ArgumentOutOfRangeException("weaponScale", "Rigid weapon scale must be finite and positive.");
            foreach (var a in source.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            foreach (var a in target.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            sourcePrefabPose = new LocalPose(source.transform);
            s = source.GetComponentsInChildren<Transform>(true).GroupBy(b => b.name).ToDictionary(g => g.Key, g => g.First());
            t = target.GetComponentsInChildren<Transform>(true).GroupBy(b => b.name).ToDictionary(g => g.Key, g => g.First());
            foreach (string name in new[] { "hips", "spine", "chest", "head", "upperarm.l", "lowerarm.l", "wrist.l", "hand.l", "upperarm.r", "lowerarm.r", "wrist.r", "hand.r", "upperleg.l", "lowerleg.l", "foot.l", "upperleg.r", "lowerleg.r", "foot.r" })
                if (!s.ContainsKey(name)) throw new InvalidOperationException("Source KayKit bone missing: " + name);
            foreach (string name in new[] { "Hips", "Spine02", "Spine01", "Spine", "neck", "Head", "LeftShoulder", "RightShoulder", "LeftArm", "LeftForeArm", "LeftHand", "RightArm", "RightForeArm", "RightHand", "LeftUpLeg", "LeftLeg", "LeftFoot", "RightUpLeg", "RightLeg", "RightFoot" })
                if (!t.ContainsKey(name)) throw new InvalidOperationException("Target Chibi24 bone missing: " + name);
            RestoreBindPose(source); RestoreBindPose(target);
            sp = s.ToDictionary(p => p.Key, p => p.Value.position);
            tp = t.ToDictionary(p => p.Key, p => p.Value.position);
            sq = s.ToDictionary(p => p.Key, p => p.Value.rotation);
            tq = t.ToDictionary(p => p.Key, p => p.Value.rotation);
            targetBindPose = new LocalPose(target.transform);
            var sourceBasis = Basis(sp["upperarm.r"] - sp["upperarm.l"], sp["head"] - sp["hips"]);
            var targetBasis = Basis(tp["RightArm"] - tp["LeftArm"], tp["Head"] - tp["Hips"]);
            alignment = targetBasis * Quaternion.Inverse(sourceBasis);
            legScale = .5f * (ChainLength(t["LeftUpLeg"], t["LeftLeg"], t["LeftFoot"]) / ChainLength(s["upperleg.l"], s["lowerleg.l"], s["foot.l"])
                + ChainLength(t["RightUpLeg"], t["RightLeg"], t["RightFoot"]) / ChainLength(s["upperleg.r"], s["lowerleg.r"], s["foot.r"]));
            rightLength = ChainLength(t["RightArm"], t["RightForeArm"], t["RightHand"]);
            leftLength = ChainLength(t["LeftArm"], t["LeftForeArm"], t["LeftHand"]);
            armScale = .5f * (leftLength / ChainLength(s["upperarm.l"], s["lowerarm.l"], s["wrist.l"])
                + rightLength / ChainLength(s["upperarm.r"], s["lowerarm.r"], s["wrist.r"]));
            targetSkins = target.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var visual = t.ContainsKey("Visual") ? t["Visual"] : target.transform;
            bakeTransforms = visual.GetComponentsInChildren<Transform>(true).Where(x => x != target.transform).ToArray();
            if (this.options.preserveTwoHandGrip)
            {
                sourceWeapon = Find(source, "WeaponRoot");
                sourceGripR = Find(source, "Grip_R"); sourceGripL = Find(source, "Grip_L");
                sourceRightGrip = sourceWeapon.InverseTransformPoint(sourceGripR.position);
                sourceLeftGrip = sourceWeapon.InverseTransformPoint(sourceGripL.position);
                if (this.options.animateWeaponSocket) socket = Find(target, this.options.targetWeaponSocketName);
            }
            sourcePrefabPose.Apply();
        }

        static Quaternion Basis(Vector3 right, Vector3 up)
        {
            up.Normalize(); right = Vector3.ProjectOnPlane(right, up).normalized;
            return Quaternion.LookRotation(Vector3.Cross(right, up).normalized, up);
        }
        static float ChainLength(Transform a, Transform b, Transform c) { return Vector3.Distance(a.position, b.position) + Vector3.Distance(b.position, c.position); }
        Quaternion Delta(string sourceBone)
        {
            return alignment * (s[sourceBone].rotation * Quaternion.Inverse(sq[sourceBone])) * Quaternion.Inverse(alignment);
        }
        void Rotation(string targetBone, string sourceBone) { t[targetBone].rotation = Delta(sourceBone) * tq[targetBone]; }
        void BodyRotation(string targetBone, Quaternion delta) { t[targetBone].rotation = delta * tq[targetBone]; }
        Vector3 Endpoint(string sourceBone, string targetBone) { return tp[targetBone] + alignment * (s[sourceBone].position - sp[sourceBone]) * legScale * options.rootMotionScale; }

        public void Sample(AnimationClip clip, float time)
        {
            if (!clip) throw new ArgumentNullException("clip");
            sourcePrefabPose.Apply(); targetBindPose.Apply();
            clip.SampleAnimation(source, Mathf.Clamp(time, 0, clip.length));
            Rotation("Hips", "hips");
            if (options.preserveSourceRootTranslation)
                t["Hips"].position = Endpoint("hips", "Hips");
            Quaternion lower = Delta("spine"), upper = Delta("chest");
            BodyRotation("Spine02", lower);
            BodyRotation("Spine01", Quaternion.Slerp(lower, upper, .5f));
            BodyRotation("Spine", upper);
            BodyRotation("neck", upper); Rotation("Head", "head");
            BodyRotation("LeftShoulder", upper); BodyRotation("RightShoulder", upper);
            foreach (bool right in new[] { false, true })
            {
                string ts = right ? "Right" : "Left", ss = right ? ".r" : ".l";
                Rotation(ts + "UpLeg", "upperleg" + ss);
                Rotation(ts + "Leg", "lowerleg" + ss);
                Rotation(ts + "Foot", "foot" + ss);
                if (s.ContainsKey("toes" + ss) && t.ContainsKey(ts + "ToeBase")) Rotation(ts + "ToeBase", "toes" + ss);
                Rotation(ts + "Arm", "upperarm" + ss);
                Rotation(ts + "ForeArm", "lowerarm" + ss);
                Rotation(ts + "Hand", "hand" + ss);
                if (!options.preserveTwoHandGrip) MatchSourceArmDirections(ts, ss);
                if (options.solveLegEndpoints)
                {
                    var footRotation = t[ts + "Foot"].rotation;
                    Vector3 goal = Endpoint("foot" + ss, ts + "Foot");
                    Vector3 pole = SourcePole("upperleg" + ss, "lowerleg" + ss, "foot" + ss, t[ts + "UpLeg"], goal);
                    TwoBone(t[ts + "UpLeg"], t[ts + "Leg"], t[ts + "Foot"], goal, pole);
                    t[ts + "Foot"].rotation = footRotation;
                }
            }
            foreach (var skin in targetSkins)
                foreach (var name in new[] { "Grip_L", "Grip_R" })
                { int id = skin.sharedMesh.GetBlendShapeIndex(name); if (id >= 0) skin.SetBlendShapeWeight(id, 100); }
            LastMetrics = new FrameMetrics { time = time };
            if (options.preserveTwoHandGrip) SolveSharedGrip();
            else
            {
                LastMetrics.rightWrist = t["RightHand"].position; LastMetrics.leftWrist = t["LeftHand"].position;
                LastMetrics.rightElbow = t["RightForeArm"].position; LastMetrics.leftElbow = t["LeftForeArm"].position;
                LastMetrics.rightUpperDirectionError = DirectionError("RightArm", "RightForeArm", "upperarm.r", "lowerarm.r");
                LastMetrics.leftUpperDirectionError = DirectionError("LeftArm", "LeftForeArm", "upperarm.l", "lowerarm.l");
                LastMetrics.rightForearmDirectionError = DirectionError("RightForeArm", "RightHand", "lowerarm.r", "wrist.r");
                LastMetrics.leftForearmDirectionError = DirectionError("LeftForeArm", "LeftHand", "lowerarm.l", "wrist.l");
            }
        }

        void MatchSourceArmDirections(string targetSide, string sourceSide)
        {
            // Rotation deltas alone preserve the *target* bind-pose limb direction.
            // That is incorrect when the native KayKit A-pose and the Meshy Chibi
            // bind pose differ: a forward cast can stay down beside the waist.
            // Transfer the actual anatomical directions, retaining the source
            // rotational twist and the target bone lengths. These swing corrections
            // are rest-frame offsets carried by the source rotation, not IK poles.
            MatchSegmentDirection(targetSide + "Arm", targetSide + "ForeArm", "upperarm" + sourceSide, "lowerarm" + sourceSide);
            // Changing the upper arm carries its descendants; restore the lower
            // arm's intended world orientation before calibrating its own axis.
            Rotation(targetSide + "ForeArm", "lowerarm" + sourceSide);
            MatchSegmentDirection(targetSide + "ForeArm", targetSide + "Hand", "lowerarm" + sourceSide, "wrist" + sourceSide);
            Rotation(targetSide + "Hand", "hand" + sourceSide);
            var hand = t[targetSide + "Hand"];
            // Both native KayKit hand bones and this Chibi hand use local +Y
            // along the fingers. Keep the source wrist articulation; correcting
            // only the arm segments would leave the fist pointing at the floor.
            Vector3 desiredFingerAxis = alignment * (s["hand" + sourceSide].rotation * Vector3.up);
            hand.rotation = Quaternion.FromToRotation(hand.rotation * Vector3.up, desiredFingerAxis) * hand.rotation;
        }

        void MatchSegmentDirection(string targetBone, string targetEnd, string sourceBone, string sourceEnd)
        {
            var bone = t[targetBone];
            Vector3 current = t[targetEnd].position - bone.position;
            Vector3 desired = alignment * (s[sourceEnd].position - s[sourceBone].position);
            if (current.sqrMagnitude < 1e-10f || desired.sqrMagnitude < 1e-10f)
                throw new InvalidOperationException("Degenerate arm segment while retargeting " + targetBone);
            bone.rotation = Quaternion.FromToRotation(current, desired) * bone.rotation;
        }

        float DirectionError(string targetBone, string targetEnd, string sourceBone, string sourceEnd)
        {
            return Vector3.Angle(t[targetEnd].position - t[targetBone].position,
                alignment * (s[sourceEnd].position - s[sourceBone].position));
        }

        void SolveSharedGrip()
        {
            Quaternion rq = t["RightHand"].rotation, lq = t["LeftHand"].rotation;
            Quaternion rforearm = t["RightForeArm"].rotation, lforearm = t["LeftForeArm"].rotation;
            WeaponRotation = alignment * sourceWeapon.rotation;
            Vector3 original = t["Spine"].position + alignment * (sourceWeapon.position - s["chest"].position) * armScale;
            WeaponPosition = original;
            Vector3 rbias = WeaponRotation * sourceRightGrip * WeaponScale - rq * options.rightPalmLocal;
            Vector3 lbias = WeaponRotation * sourceLeftGrip * WeaponScale - lq * options.leftPalmLocal;
            // Move the shared hilt if proportions make it unreachable. Never stretch
            // the arm, change elbow length, or force the wrist to solve a position.
            for (int i = 0; i < 10; i++)
            {
                WeaponPosition += ProjectReach(t["RightArm"].position, WeaponPosition + rbias, rightLength * .985f);
                WeaponPosition += ProjectReach(t["LeftArm"].position, WeaponPosition + lbias, leftLength * .985f);
            }
            Vector3 rhint = SourcePole("upperarm.r", "lowerarm.r", "wrist.r", t["RightArm"], WeaponPosition + rbias);
            Vector3 lhint = SourcePole("upperarm.l", "lowerarm.l", "wrist.l", t["LeftArm"], WeaponPosition + lbias);
            TwoBone(t["RightArm"], t["RightForeArm"], t["RightHand"], WeaponPosition + rbias, rhint);
            TwoBone(t["LeftArm"], t["LeftForeArm"], t["LeftHand"], WeaponPosition + lbias, lhint);
            t["RightHand"].rotation = rq; t["LeftHand"].rotation = lq;
            if (socket)
            {
                socket.SetPositionAndRotation(WeaponPosition, WeaponRotation);
                var scale = socket.parent.lossyScale;
                socket.localScale = new Vector3(WeaponScale / scale.x, WeaponScale / scale.y, WeaponScale / scale.z);
            }
            LastMetrics.weaponPosition = WeaponPosition; LastMetrics.bladeAxis = WeaponRotation * Vector3.forward;
            LastMetrics.weaponReachCorrection = Vector3.Distance(original, WeaponPosition);
            LastMetrics.rightWrist = t["RightHand"].position; LastMetrics.leftWrist = t["LeftHand"].position;
            LastMetrics.rightElbow = t["RightForeArm"].position; LastMetrics.leftElbow = t["LeftForeArm"].position;
            LastMetrics.rightGripError = Vector3.Distance(t["RightHand"].position + rq * options.rightPalmLocal, WeaponPosition + WeaponRotation * sourceRightGrip * WeaponScale);
            LastMetrics.leftGripError = Vector3.Distance(t["LeftHand"].position + lq * options.leftPalmLocal, WeaponPosition + WeaponRotation * sourceLeftGrip * WeaponScale);
            LastMetrics.rightArmExtension = Vector3.Distance(t["RightArm"].position, t["RightHand"].position) / rightLength;
            LastMetrics.leftArmExtension = Vector3.Distance(t["LeftArm"].position, t["LeftHand"].position) / leftLength;
            LastMetrics.rightForearmCorrectionDegrees = Quaternion.Angle(rforearm, t["RightForeArm"].rotation);
            LastMetrics.leftForearmCorrectionDegrees = Quaternion.Angle(lforearm, t["LeftForeArm"].rotation);
        }
        Vector3 SourcePole(string upperName, string lowerName, string endName, Transform targetUpper, Vector3 targetGoal)
        {
            // A chest-relative elbow point is not a stable IK pole after limb
            // proportions change: it can cross the new shoulder-to-wrist axis and
            // flip an elbow. Transfer the source *bend plane*, then parallel-
            // transport it onto the target reach axis instead.
            Vector3 sourceAxis = alignment * (s[endName].position - s[upperName].position);
            Vector3 sourceBend = alignment * Vector3.ProjectOnPlane(s[lowerName].position - s[upperName].position, s[endName].position - s[upperName].position);
            if (sourceBend.sqrMagnitude < 1e-10f)
                sourceBend = Vector3.ProjectOnPlane(alignment * (s[upperName].rotation * Vector3.forward), sourceAxis);
            Vector3 targetAxis = targetGoal - targetUpper.position;
            Vector3 bend = Quaternion.FromToRotation(sourceAxis, targetAxis) * sourceBend.normalized;
            return targetUpper.position + bend * .5f;
        }
        static Vector3 ProjectReach(Vector3 origin, Vector3 goal, float maximum)
        {
            Vector3 delta = goal - origin;
            return delta.magnitude > maximum ? origin + delta.normalized * maximum - goal : Vector3.zero;
        }

        public AnimationClip Bake(AnimationClip sourceClip, string outputName, int sampleRate = 60, bool loop = false)
        {
            int count = Mathf.RoundToInt(sourceClip.length * sampleRate) + 1;
            var p = new Vector3[count][]; var q = new Quaternion[count][]; var scale = new Vector3[count][];
            for (int frame = 0; frame < count; frame++)
            {
                Sample(sourceClip, frame * sourceClip.length / (count - 1));
                p[frame] = bakeTransforms.Select(b => b.localPosition).ToArray();
                q[frame] = bakeTransforms.Select(b => b.localRotation).ToArray();
                scale[frame] = bakeTransforms.Select(b => b.localScale).ToArray();
                if (frame > 0) for (int bone = 0; bone < bakeTransforms.Length; bone++)
                    if (Quaternion.Dot(q[frame - 1][bone], q[frame][bone]) < 0) q[frame][bone] = Negate(q[frame][bone]);
            }
            var clip = new AnimationClip { name = outputName, frameRate = sampleRate, legacy = false };
            for (int bone = 0; bone < bakeTransforms.Length; bone++)
            {
                int b = bone; string path = AnimationUtility.CalculateTransformPath(bakeTransforms[b], target.transform);
                for (int axis = 0; axis < 3; axis++)
                {
                    int a = axis;
                    Curve(clip, path, "m_LocalPosition." + "xyz"[a], p.Select(v => v[b][a]).ToArray(), sourceClip.length, loop);
                    Curve(clip, path, "m_LocalScale." + "xyz"[a], scale.Select(v => v[b][a]).ToArray(), sourceClip.length, loop);
                }
                for (int axis = 0; axis < 4; axis++)
                { int a = axis; Curve(clip, path, "m_LocalRotation." + "xyzw"[a], q.Select(v => v[b][a]).ToArray(), sourceClip.length, loop); }
            }
            foreach (var skin in targetSkins) foreach (var shape in new[] { "Grip_L", "Grip_R" })
                if (skin.sharedMesh.GetBlendShapeIndex(shape) >= 0)
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(skin.transform, target.transform), typeof(SkinnedMeshRenderer), "blendShape." + shape), AnimationCurve.Constant(0, sourceClip.length, 100));
            clip.EnsureQuaternionContinuity();
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, settings);
            return clip;
        }
        public void Dispose() { /* Actor and asset lifetimes belong to the caller. */ }
    }

    static Quaternion Negate(Quaternion q) { return new Quaternion(-q.x, -q.y, -q.z, -q.w); }
    static void Curve(AnimationClip clip, string path, string property, float[] v, float duration, bool loop)
    {
        AnimationCurve curve;
        if (v.All(x => Mathf.Abs(x - v[0]) < 1e-7f)) curve = AnimationCurve.Constant(0, duration, v[0]);
        else
        {
            float dt = duration / (v.Length - 1); var keys = new Keyframe[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                float tangent = i == 0 ? (v[1] - v[0]) / dt : i == v.Length - 1 ? (v[i] - v[i - 1]) / dt : (v[i + 1] - v[i - 1]) / (2 * dt);
                if (loop && (i == 0 || i == v.Length - 1)) tangent = (v[1] - v[v.Length - 2]) / (2 * dt);
                keys[i] = new Keyframe(i * dt, v[i], tangent, tangent);
            }
            curve = new AnimationCurve(keys);
        }
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
    }

    public static void TwoBone(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 elbowHint)
    {
        Vector3 origin = upper.position, delta = target - origin;
        float a = Vector3.Distance(origin, lower.position), b = Vector3.Distance(lower.position, end.position);
        if (a < 1e-6f || b < 1e-6f || delta.sqrMagnitude < 1e-10f) return;
        Vector3 axis = delta.normalized;
        float d = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + .0001f, a + b - .0001f);
        Vector3 pole = Vector3.ProjectOnPlane(elbowHint - origin, axis);
        if (pole.sqrMagnitude < 1e-8f) pole = Vector3.ProjectOnPlane(lower.position - origin, axis);
        if (pole.sqrMagnitude < 1e-8f) pole = Vector3.ProjectOnPlane(upper.right, axis);
        pole.Normalize();
        float along = Mathf.Clamp((a * a + d * d - b * b) / (2 * d), -a, a);
        Vector3 bend = origin + axis * along + pole * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
        upper.rotation = Quaternion.FromToRotation(lower.position - origin, bend - origin) * upper.rotation;
        lower.rotation = Quaternion.FromToRotation(end.position - lower.position, origin + axis * d - lower.position) * lower.rotation;
    }
}
#endif
