#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Original hand-keyed blocking for the native Generic skeleton. Positions are in
// the equipped preview actor's metres (+Z facing), angles in degrees.
public static class StaffTipBoltStudy
{
    const string Equipment = "Assets/Art/Characters/Protagonist01/AnimationStudies/SkeletonMageCast20261030/Study_Mage_Cast_Review.prefab";
    const string Studies = "Assets/Art/Characters/Protagonist01/AnimationStudies";
    const string Folder = Studies + "/StaffTipBolt20261102";
    public const string Output = Folder + "/Study_StaffTipBolt_Chibi.anim";
    public const string IdleOutput = Folder + "/Study_StaffTipIdle_Chibi.anim";
    const int Rate = 60, Last = 54, IdleFrames = 144;
    static readonly int[] Beats = {0, 6, 13, 17, 21, 23, 26, 29, 34, 41, 49, Last};

    public static GameObject AttachStaff(GameObject actor)
    {
        var equipped = AssetDatabase.LoadAssetAtPath<GameObject>(Equipment);
        var source = CombatRetargetUtility.Find(equipped, "Study_LeftStaff");
        var hand = CombatRetargetUtility.Find(actor, "LeftHand");
        var staff = Object.Instantiate(source.gameObject, hand, false);
        staff.name = source.name;
        return staff;
    }

    // Clamped cubic tangents keep anticipation and overshoot extrema intact.
    static AnimationCurve Track(params float[] values)
    {
        if (values.Length != Beats.Length) throw new ArgumentException("Missing blocking beat.");
        var c = new AnimationCurve(Beats.Select((f, i) => new Keyframe(f, values[i])).ToArray());
        for (int i = 0; i < values.Length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
        }
        return c;
    }

    [MenuItem("Project M/Studies/Build Staff Tip Bolt")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(Studies, "StaffTipBolt20261102");
        var equipped = AssetDatabase.LoadAssetAtPath<GameObject>(Equipment);
        if (!equipped) throw new InvalidOperationException("Missing equipped Chibi study.");
        var actor = Object.Instantiate(equipped);
        try
        {
            actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var animator in actor.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            var clip = Bake(actor);
            var idle = BakeIdle(actor, clip);
            Save(clip, Output);
            Save(idle, IdleOutput);
            AssetDatabase.SaveAssets();
            Debug.Log("Staff tip study saved: " + Output);
        }
        finally { Object.DestroyImmediate(actor); }
    }

    static void Save(AnimationClip clip, string path)
    {
        var saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (saved)
        {
            EditorUtility.CopySerialized(clip, saved);
            EditorUtility.SetDirty(saved);
            Object.DestroyImmediate(clip);
        }
        else AssetDatabase.CreateAsset(clip, path);
    }

    static AnimationClip Bake(GameObject actor)
    {
        Transform Bone(string name) => CombatRetargetUtility.Find(actor, name);
        var visual = Bone("Visual");
        var hips = Bone("Hips"); var chest = Bone("Spine"); var pelvisSpine = Bone("Spine02"); var head = Bone("Head");
        var arms = new[] { new Limb(actor,"LeftArm","LeftForeArm","LeftHand"), new Limb(actor,"RightArm","RightForeArm","RightHand") };
        var legs = new[] { new Limb(actor,"LeftUpLeg","LeftLeg","LeftFoot"), new Limb(actor,"RightUpLeg","RightLeg","RightFoot") };
        var left = arms[0]; var right = arms[1];
        var staff = Bone("Study_LeftStaff"); var tip = Bone("EffectTip");
        var initialAxis = (tip.position - staff.position).normalized;
        if (Vector3.Dot(initialAxis, left.end.forward) < .999f)
            throw new InvalidOperationException("Unexpected grip/staff axis. Inspect the equipped hand.");
        const float readyPitch = 42f;
        const float readyYaw = -5f;
        var headRotation = head.rotation;
        var rightClavicle = Bone("RightShoulder");
        float readyShoulderDepth = right.upper.position.z - hips.position.z;
        var handRotation = left.end.rotation;
        var armature = Bone("Armature");
        var bones = armature.GetComponentsInChildren<Transform>(true)
            .Where(n => n != staff && !n.IsChildOf(staff)).Prepend(visual).ToArray();
        var restRot = bones.Select(n => n.localRotation).ToArray();
        var restPos = bones.Select(n => n.localPosition).ToArray();
        var restScale = bones.Select(n => n.localScale).ToArray();
        var sampled = new Quaternion[bones.Length][];
        for (int i=0;i<bones.Length;i++) sampled[i] = new Quaternion[Last+1];
        var hipKeys = new Vector3[Last+1];

        // Wind-up, presentation of the tip, two aligned beats around the shot,
        // then a shorter recoil and a longer settle. Chest and hands need not
        // arrive at their extrema on the same frame.
        var pitch = Track(readyPitch, 47, 55, 43, 8, 0, 0, 11, 21, 32, 40, readyPitch);
        var yaw = Track(readyYaw, -7, -9, -6, -2, 0, 0, -4, -6, -7, -6, readyYaw);
        var weight = Track(0, -.30f, -1, -.72f, .74f, .98f, 1, .17f, -.30f, -.10f, .04f, 0);
        var turn = Track(0, -5, -23, -12, 18, 25, 26, 13, 0, -5, 1, 0);
        var sink = Track(0, -.008f, -.025f, -.032f, -.015f, -.010f, -.009f, -.027f, -.020f, -.009f, .002f, 0);
        var incline = Track(0, -2, -7, -3, 7, 10, 10, 0, -4, -1, 1, 0);
        var gaze = Track(0, 1, -5, -3, 3, 4, 4, -2, -2, 1, 1, 0);
        var reach = Track(.35f, .28f, .13f, .22f, .76f, .92f, 1, .56f, .29f, .26f, .32f, .35f);
        var lift = Track(0, .014f, .043f, .046f, .054f, .059f, .059f, .065f, .055f, .028f, .007f, 0);
        var elbowLift = Track(0, .13f, .32f, .44f, .88f, 1, 1, .90f, .65f, .31f, .04f, 0);
        // The shoulder reverses first. The forearm and knuckles continue
        // travelling briefly, before settling in the opposite direction.
        var armSwing = Track(0, 1.0f, -1.8f, -3.8f, -3.0f, -1.2f, 1.8f, 3.8f, 4.7f, 1.2f, -.8f, 0);
        var forearmSwing = Track(0, .1f, -.15f, -.9f, -2.5f, -3.0f, -1.5f, .4f, 2.9f, 2.7f, -.45f, 0);
        var fistSwing = Track(0, 0, .2f, .2f, -1.1f, -2.3f, -2.7f, -1.4f, 1.4f, 2.8f, .3f, 0);
        var handLag = Track(0, -.10f, -.58f, -.76f, -.24f, .27f, .75f, 1f, .58f, -.25f, -.05f, 0);
        var leftSwivel = Track(0, -12, -38, -54, -80, -88, -88, -90, -72, -40, -8, 0);
        for (int f=0; f<=Last; f++)
        {
            for (int b=0;b<bones.Length;b++) { bones[b].localPosition = restPos[b]; bones[b].localRotation = restRot[b]; }
            float w=weight.Evaluate(f), twist=turn.Evaluate(f), lean=incline.Evaluate(f);
            hips.position += new Vector3(-.010f*w, sink.Evaluate(f), .029f*w);
            hips.rotation = Quaternion.AngleAxis(twist*.24f, Vector3.up) * hips.rotation;
            pelvisSpine.rotation = Quaternion.AngleAxis(lean*.40f, Vector3.right) *
                                   Quaternion.AngleAxis(twist*.35f, Vector3.up) * pelvisSpine.rotation;
            chest.rotation = Quaternion.AngleAxis(lean*.60f, Vector3.right) *
                               Quaternion.AngleAxis(twist*.41f, Vector3.up) * chest.rotation;
            head.rotation = Quaternion.AngleAxis(gaze.Evaluate(f), Vector3.right) *
                            Quaternion.AngleAxis(twist*.22f, Vector3.up) * headRotation;
            foreach (var leg in legs) leg.Plant();

            float r = reach.Evaluate(f), e = elbowLift.Evaluate(f);
            Vector3 leftGoal = left.endStart + new Vector3(-.021f*e, lift.Evaluate(f), .122f*r);
            left.Place(left.SoftReach(leftGoal), leftSwivel.Evaluate(f));
            float p=pitch.Evaluate(f)*Mathf.Deg2Rad, y=yaw.Evaluate(f)*Mathf.Deg2Rad;
            Vector3 axis = new Vector3(Mathf.Sin(y)*Mathf.Cos(p), Mathf.Sin(p), Mathf.Cos(y)*Mathf.Cos(p));
            Quaternion wrist = Quaternion.FromToRotation(initialAxis,axis) * handRotation;
            // Share pronation with the forearm around the elbow-to-wrist axis.
            // The actual palm bore stays aligned with the staff throughout.
            Vector3 forearm = (left.end.position-left.mid.position).normalized;
            float roll = Vector3.SignedAngle(Vector3.ProjectOnPlane(left.end.forward,forearm),
                                             Vector3.ProjectOnPlane(axis,forearm),forearm);
            left.mid.rotation = Quaternion.AngleAxis(Mathf.Clamp(roll,-75,75),forearm) * left.mid.rotation;
            left.end.rotation = wrist;

            // A relaxed supporting side: lower the clavicle a little, then let
            // the elbow trail the hanging fist. Lag is measured in centimetres.
            rightClavicle.rotation = Quaternion.AngleAxis(-4f, Vector3.forward) * rightClavicle.rotation;
            float lag = handLag.Evaluate(f);
            float shoulderDepth = right.upper.position.z - hips.position.z;
            float relaxedDepth = Mathf.Lerp(readyShoulderDepth, shoulderDepth, .67f);
            Vector3 rightGoal = new Vector3(right.upper.position.x + .117f,
                                            right.upper.position.y - .235f,
                                            hips.position.z + relaxedDepth + .015f - .009f * lag);
            Vector3 rightPole = right.upper.position + new Vector3(.09f, -.12f, -.19f);
            CombatRetargetUtility.TwoBone(right.upper, right.mid, right.end, rightGoal, rightPole);

            // On this skin +Y runs down the fingers, +Z toward the thumb and
            // +X out of the palm. Keep the fist's thumb forward and palm medial.
            Vector3 rightForearm = (right.end.position - right.mid.position).normalized;
            Vector3 fingers = Vector3.Slerp(Vector3.down, rightForearm, .45f);
            Vector3 thumb = Vector3.ProjectOnPlane(Vector3.forward, fingers).normalized;
            float pronation = Vector3.SignedAngle(Vector3.ProjectOnPlane(right.end.forward, rightForearm),
                                                  Vector3.ProjectOnPlane(thumb, rightForearm), rightForearm);
            right.mid.rotation = Quaternion.AngleAxis(pronation, rightForearm) * right.mid.rotation;
            right.end.rotation = Quaternion.LookRotation(thumb, fingers);
            Swing(right, armSwing.Evaluate(f), forearmSwing.Evaluate(f), fistSwing.Evaluate(f));

            if (f>=23 && f<=26 && Vector3.Dot((tip.position-staff.position).normalized,Vector3.forward)<.9999f)
                throw new InvalidOperationException("The held staff left its sightline.");
            for(int b=0;b<bones.Length;b++)
            {
                var q=bones[b].localRotation;
                if(f>0 && Quaternion.Dot(sampled[b][f-1],q)<0) q=new Quaternion(-q.x,-q.y,-q.z,-q.w);
                sampled[b][f]=q;
            }
            hipKeys[f]=hips.localPosition;
        }

        return MakeClip(actor, "Study_StaffTipBolt_Chibi", false, bones, hips, sampled, hipKeys, restPos, restScale);
    }

    static void Swing(Limb arm, float upperAngle, float forearmAngle, float handAngle)
    {
        // World X is across the unrotated source actor. Local curves are baked
        // afterwards, so the same pendulum arc works for both gameplay facings.
        arm.upper.rotation = Quaternion.AngleAxis(upperAngle, Vector3.right) * arm.upper.rotation;
        arm.mid.rotation = Quaternion.AngleAxis(forearmAngle, Vector3.right) * arm.mid.rotation;
        arm.end.rotation = Quaternion.AngleAxis(handAngle, Vector3.right) * arm.end.rotation;
    }

    static AnimationClip BakeIdle(GameObject actor, AnimationClip cast)
    {
        // The shared ready pose includes the closed fist, the staff grip, and
        // the final arm spacing. Sample the actual newly authored pose once.
        cast.SampleAnimation(actor, 0f);
        Transform Bone(string name) => CombatRetargetUtility.Find(actor, name);
        var visual = Bone("Visual"); var hips = Bone("Hips"); var staff = Bone("Study_LeftStaff");
        var spine = Bone("Spine02"); var chest = Bone("Spine"); var neck = Bone("neck");
        var clavicle = Bone("RightShoulder");
        var right = new Limb(actor, "RightArm", "RightForeArm", "RightHand");
        var legs = new[] {new Limb(actor, "LeftUpLeg", "LeftLeg", "LeftFoot"),
                          new Limb(actor, "RightUpLeg", "RightLeg", "RightFoot")};
        var bones = Bone("Armature").GetComponentsInChildren<Transform>(true)
            .Where(n => n != staff && !n.IsChildOf(staff)).Prepend(visual).ToArray();
        var restRot = bones.Select(n => n.localRotation).ToArray();
        var restPos = bones.Select(n => n.localPosition).ToArray();
        var restScale = bones.Select(n => n.localScale).ToArray();
        var rotations = bones.Select(n => new Quaternion[IdleFrames + 1]).ToArray();
        var hipKeys = new Vector3[IdleFrames + 1];
        for (int f = 0; f <= IdleFrames; f++)
        {
            for (int b = 0; b < bones.Length; b++)
            {
                bones[b].localRotation = restRot[b];
                bones[b].localPosition = restPos[b];
            }
            if (f > 0 && f < IdleFrames)
            {
                // One complete breath with a slower change around the shared
                // ready pose. Different phases carry weight into the trailing hand.
                float phase = 2f * Mathf.PI * f / IdleFrames;
                phase -= .42f * Mathf.Sin(phase);
                float Wave(float lag) => Mathf.Sin(phase - lag) + Mathf.Sin(lag);
                float inhale = 1f - Mathf.Cos(phase);
                hips.position += new Vector3(.006f * Wave(0), -.0043f * inhale, -.004f * Wave(.03f));
                hips.rotation = Quaternion.AngleAxis(1.8f * Wave(.04f), Vector3.up) * hips.rotation;
                spine.rotation = Quaternion.AngleAxis(1.2f * Wave(.14f), Vector3.right) * spine.rotation;
                chest.rotation = Quaternion.AngleAxis(2.4f * Wave(.13f), Vector3.up)
                               * Quaternion.AngleAxis(-1.0f * inhale, Vector3.right) * chest.rotation;
                neck.rotation = Quaternion.AngleAxis(-1.5f * Wave(.20f), Vector3.up)
                              * Quaternion.AngleAxis(.45f * inhale, Vector3.right) * neck.rotation;
                foreach (var leg in legs) leg.Plant();

                clavicle.rotation = Quaternion.AngleAxis(1.3f * inhale, Vector3.forward)
                                  * Quaternion.AngleAxis(1.4f * Wave(.18f), Vector3.up) * clavicle.rotation;
                Swing(right, 3.25f * Wave(.73f), 3.35f * Wave(1.23f), 2.2f * Wave(1.50f));
            }
            for (int b = 0; b < bones.Length; b++)
            {
                var q = bones[b].localRotation;
                if (f > 0 && Quaternion.Dot(rotations[b][f-1], q) < 0)
                    q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                rotations[b][f] = q;
            }
            hipKeys[f] = hips.localPosition;
        }
        return MakeClip(actor, "Study_StaffTipIdle_Chibi", true, bones, hips, rotations, hipKeys, restPos, restScale);
    }

    static AnimationClip MakeClip(GameObject actor, string name, bool loop, Transform[] bones, Transform hips,
        Quaternion[][] sampled, Vector3[] hipKeys, Vector3[] restPos, Vector3[] restScale)
    {
        float duration = (hipKeys.Length - 1) / (float)Rate;
        var clip = new AnimationClip {name = name, frameRate = Rate, legacy = false};
        var bindings=new List<EditorCurveBinding>(); var curves=new List<AnimationCurve>();
        for(int b=0;b<bones.Length;b++)
        {
            string path=AnimationUtility.CalculateTransformPath(bones[b],actor.transform);
            void Add(string property,AnimationCurve curve)
            {
                bindings.Add(EditorCurveBinding.FloatCurve(path,typeof(Transform),property)); curves.Add(curve);
            }
            for(int a=0;a<4;a++)
            {
                int axis=a; Add("m_LocalRotation."+"xyzw"[a],SampledCurve(sampled[b].Select(q=>q[axis]).ToArray(), loop));
            }
            for(int a=0;a<3;a++)
            {
                int axis=a;
                Add("m_LocalPosition."+"xyz"[a],bones[b]==hips ? SampledCurve(hipKeys.Select(v=>v[axis]).ToArray(), loop) :
                    AnimationCurve.Constant(0,duration,restPos[b][a]));
                Add("m_LocalScale."+"xyz"[a],AnimationCurve.Constant(0,duration,restScale[b][a]));
            }
        }
        var skin=actor.GetComponentInChildren<SkinnedMeshRenderer>(true);
        foreach(string shape in new[]{"Grip_L","Grip_R"})
        {
            bindings.Add(EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(skin.transform,actor.transform),
                typeof(SkinnedMeshRenderer),"blendShape."+shape));
            curves.Add(AnimationCurve.Constant(0,duration,shape == "Grip_R" ? 100f : 0f));
        }
        AnimationUtility.SetEditorCurves(clip,bindings.ToArray(),curves.ToArray());
        clip.EnsureQuaternionContinuity();
        var settings=AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime=loop;
        AnimationUtility.SetAnimationClipSettings(clip,settings);
        return clip;
    }

    static AnimationCurve SampledCurve(float[] v, bool loop)
    {
        float duration=(v.Length-1)/(float)Rate;
        if(v.All(x=>Mathf.Abs(x-v[0])<1e-6f)) return AnimationCurve.Constant(0,duration,v[0]);
        var keys=new Keyframe[v.Length];
        for(int i=0;i<v.Length;i++)
        {
            float slope=(i==0 || i==v.Length-1)?0:(v[i+1]-v[i-1])*Rate*.5f;
            if (loop && (i == 0 || i == v.Length - 1))
                slope = (v[1] - v[v.Length-2]) * Rate * .5f;
            keys[i]=new Keyframe(i/(float)Rate,v[i],slope,slope);
        }
        return new AnimationCurve(keys);
    }

    sealed class Limb
    {
        public readonly Transform upper,mid,end;
        public readonly Vector3 endStart;
        readonly Quaternion endRotation;
        readonly Vector3 reach, bend;
        public Limb(GameObject actor,string a,string b,string c)
        {
            upper=CombatRetargetUtility.Find(actor,a); mid=CombatRetargetUtility.Find(actor,b); end=CombatRetargetUtility.Find(actor,c);
            endStart=end.position; endRotation=end.rotation;
            reach=end.position-upper.position; bend=Vector3.ProjectOnPlane(mid.position-upper.position,reach).normalized;
        }
        // Transport the ready bend around the new reach axis. A world-space
        // pole can cross that axis during wind-up, flipping the short elbow.
        public void Place(Vector3 goal, float swivel)
        {
            Vector3 axis = goal - upper.position;
            Vector3 pole = Quaternion.AngleAxis(swivel, axis) * (Quaternion.FromToRotation(reach, axis) * bend);
            CombatRetargetUtility.TwoBone(upper, mid, end, goal, upper.position + pole);
        }
        // As the torso twists away in anticipation, allow the holding hand to
        // yield towards the shoulder before the arm approaches a locked elbow.
        // The fire pose is already comfortably inside the unsaturated region.
        public Vector3 SoftReach(Vector3 goal)
        {
            Vector3 delta = goal - upper.position;
            float length = Vector3.Distance(upper.position, mid.position) + Vector3.Distance(mid.position, end.position);
            float start = .80f * length, falloff = .13f * length;
            float over = delta.magnitude - start;
            if (over <= 0) return goal;
            return upper.position + delta.normalized * (start + falloff * (1f - Mathf.Exp(-over / falloff)));
        }
        public void Plant()
        {
            Place(endStart, 0); end.rotation=endRotation;
        }
    }
}
#endif
