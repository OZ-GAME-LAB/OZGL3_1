#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Measures real Mecanim attack poses without changing saved contact timings.</summary>
public static class SideviewContactCalibration
{
    const string Hero = "Assets/Art/Characters/Protagonist01/Runtime";
    const string Kit = "Assets/Art/Stage01Kit";
    const string Output = "ArtSources/Stage01Kit/SideviewReview/contact-diagnostics.json";
    const int Count = 101;

    [Serializable] public sealed class Sample
    {
        public float normalizedTime, seconds, actualStateNormalizedTime;
        public Vector3 rightHand, leftHand, head, hips, effectTip, bodyForward;
        public bool hasEffectTip;
        public float rightHandForward, leftHandForward, tipForward;
        public float rightHandForwardFromHips, leftHandForwardFromHips;
        public float rightHandForwardVelocity, leftHandForwardVelocity, tipForwardVelocity;
    }

    [Serializable] public sealed class Motion
    {
        public string configuration, state, actualClip, controller, socket;
        public float sourceSeconds, savedAttackContactNormalized;
        public float maximumRightHandForwardNormalized, maximumRightHandForwardFromHipsNormalized;
        public float maximumLeftHandForwardNormalized, maximumRightHandVelocityNormalized;
        public float maximumTipForwardNormalized, maximumTipVelocityNormalized;
        public float maximumRightHandForward, maximumRightHandForwardFromHips, maximumTipForward, maximumTipForwardVelocity;
        public List<Sample> samples = new List<Sample>();
    }

    [Serializable] public sealed class Report
    {
        public bool success;
        public string unity, prefab, method, limitation;
        public int samplesPerMotion;
        public List<Motion> motions = new List<Motion>();
    }

    [MenuItem("Tools/Project M/Measure Sideview Attack Contacts")]
    public static void Analyze()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Hero + "/Protagonist01_Player.prefab");
        if (!prefab) throw new InvalidOperationException("Missing generated player prefab.");
        var report = new Report
        {
            unity = Application.unityVersion, prefab = AssetDatabase.GetAssetPath(prefab), samplesPerMotion = Count,
            method = "A disposable real player instance per configuration, at origin/yaw90/facing +X. All MonoBehaviours, physics and colliders disabled; " +
                "nested Animators disabled so only the actual root Mecanim Animator evaluates the skeleton. " +
                "Real HandSocketEquipment.Equip attaches each weapon and assigns its saved override controller. " +
                "Mecanim states are sampled directly at 101 inclusive normalized positions with FacingLeft=false, AttackSpeed=ActionSpeed=1, Attacking=true and Air=false. " +
                "No custom Playable or capture-only pose rotation/grounding is used. World +X is forward. Central finite differences use the actual source clip duration.",
            limitation = "Pose outreach/velocity extrema are measurements for human review, not automatic hit-window recommendations. " +
                "A sword may contact during a swept arc before its maximum forward extension. No controller, source clip, saved timing, inventory, damage or projectile setting is modified."
        };
        report.motions.Add(Measure(prefab, "Unarmed", "Attack"));
        report.motions.Add(Measure(prefab, "Sword", "Attack"));
        report.motions.Add(Measure(prefab, "Staff", "Attack"));
        report.motions.Add(Measure(prefab, "Sword", "SwordSlash"));
        report.motions.Add(Measure(prefab, "Staff", "MagicCast"));
        report.success = true;
        Directory.CreateDirectory(Path.GetDirectoryName(Output));
        File.WriteAllText(Output, JsonUtility.ToJson(report, true));
        Debug.Log("SIDEVIEW_CONTACT_CALIBRATION_SUCCESS " + Output);
    }

    static Motion Measure(GameObject prefab, string configuration, string state)
    {
        var actor = Object.Instantiate(prefab);
        actor.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var facing = Quaternion.Euler(0, 90, 0);
            actor.transform.SetPositionAndRotation(Vector3.zero, facing);
            foreach (var behaviour in actor.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            foreach (var body in actor.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.useGravity = false; }
            foreach (var collider in actor.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            var animator = actor.GetComponent<Animator>();
            if (!animator || !animator.avatar || !animator.avatar.isHuman || !animator.avatar.isValid)
                throw new InvalidOperationException("A valid root Humanoid Animator is required.");
            foreach (var nested in actor.GetComponentsInChildren<Animator>(true))
                if (nested != animator) nested.enabled = false;
            var equipment = actor.GetComponent<HandSocketEquipment>();
            var driver = actor.GetComponent<ProtagonistAnimationDriver>();
            if (!equipment || !driver) throw new InvalidOperationException("Real equipment and animation driver required.");
            if (configuration != "Unarmed")
            {
                var weapon = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + "/Prefabs/Weapons/" + configuration + ".prefab");
                if (!weapon || !equipment.Equip(weapon.GetComponent<SocketWeapon>()))
                    throw new InvalidOperationException("Could not equip actual " + configuration + " prefab.");
            }
            else equipment.Unequip();
            animator.enabled = true; animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind(); animator.Update(0);
            SetParameters(animator);
            int stateHash = Animator.StringToHash("Base Layer." + state);
            if (!animator.HasState(0, stateHash)) throw new InvalidOperationException("Missing actual Mecanim state: " + state);
            var right = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (!right || !left || !head || !hips) throw new InvalidOperationException("Missing contact diagnostic bones.");
            Transform tip = equipment.Equipped != null ? equipment.Equipped.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(transform => transform.name == "EffectTip") : null;
            var result = new Motion
            {
                configuration = configuration, state = state,
                controller = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController),
                socket = equipment.Equipped != null && equipment.Equipped.transform.parent != null ? equipment.Equipped.transform.parent.name : "none",
                savedAttackContactNormalized = driver.AttackContactNormalized
            };
            for (int index = 0; index < Count; index++)
            {
                float fraction = index / (float)(Count - 1);
                actor.transform.SetPositionAndRotation(Vector3.zero, facing);
                SetParameters(animator);
                animator.Play(stateHash, 0, fraction); animator.Update(0);
                actor.transform.SetPositionAndRotation(Vector3.zero, facing);
                var info = animator.GetCurrentAnimatorStateInfo(0);
                if (!info.IsName(state)) throw new InvalidOperationException("Mecanim did not enter sampled state: " + configuration + "/" + state);
                if (index == 0)
                {
                    var clips = animator.GetCurrentAnimatorClipInfo(0);
                    var clip = clips.OrderByDescending(entry => entry.weight).Select(entry => entry.clip).FirstOrDefault();
                    if (!clip || !clip.humanMotion || clip.length <= 0) throw new InvalidOperationException("Actual sampled Mecanim motion is missing or invalid.");
                    result.actualClip = clip.name; result.sourceSeconds = clip.length;
                }
                result.samples.Add(new Sample
                {
                    normalizedTime = fraction, seconds = fraction * result.sourceSeconds, actualStateNormalizedTime = info.normalizedTime,
                    rightHand = right.position, leftHand = left.position, head = head.position, hips = hips.position,
                    hasEffectTip = tip != null, effectTip = tip != null ? tip.position : Vector3.zero,
                    bodyForward = animator.bodyRotation * Vector3.forward,
                    rightHandForward = right.position.x, leftHandForward = left.position.x, tipForward = tip != null ? tip.position.x : 0,
                    rightHandForwardFromHips = right.position.x - hips.position.x,
                    leftHandForwardFromHips = left.position.x - hips.position.x
                });
            }
            for (int index = 0; index < Count; index++)
            {
                int first = Mathf.Max(0, index - 1), last = Mathf.Min(Count - 1, index + 1);
                var a = result.samples[first]; var b = result.samples[last];
                float seconds = Mathf.Max(.000001f, b.seconds - a.seconds);
                var sample = result.samples[index];
                sample.rightHandForwardVelocity = (b.rightHandForward - a.rightHandForward) / seconds;
                sample.leftHandForwardVelocity = (b.leftHandForward - a.leftHandForward) / seconds;
                sample.tipForwardVelocity = tip != null ? (b.tipForward - a.tipForward) / seconds : 0;
            }
            var handPeak = result.samples.OrderByDescending(sample => sample.rightHandForward).First();
            var handRelativePeak = result.samples.OrderByDescending(sample => sample.rightHandForwardFromHips).First();
            var tipPeak = result.samples.OrderByDescending(sample => sample.tipForward).First();
            var tipVelocityPeak = result.samples.OrderByDescending(sample => sample.tipForwardVelocity).First();
            result.maximumRightHandForwardNormalized = handPeak.normalizedTime;
            result.maximumRightHandForwardFromHipsNormalized = handRelativePeak.normalizedTime;
            result.maximumRightHandForward = handPeak.rightHandForward;
            result.maximumRightHandForwardFromHips = handRelativePeak.rightHandForwardFromHips;
            result.maximumLeftHandForwardNormalized = result.samples.OrderByDescending(sample => sample.leftHandForward).First().normalizedTime;
            result.maximumRightHandVelocityNormalized = result.samples.OrderByDescending(sample => sample.rightHandForwardVelocity).First().normalizedTime;
            result.maximumTipForwardNormalized = tip != null ? tipPeak.normalizedTime : -1;
            result.maximumTipVelocityNormalized = tip != null ? tipVelocityPeak.normalizedTime : -1;
            result.maximumTipForward = tipPeak.tipForward;
            result.maximumTipForwardVelocity = tipVelocityPeak.tipForwardVelocity;
            return result;
        }
        finally { Object.DestroyImmediate(actor); }
    }

    static void SetParameters(Animator animator)
    {
        foreach (var parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger) animator.ResetTrigger(parameter.nameHash);
            else if (parameter.type == AnimatorControllerParameterType.Bool)
                animator.SetBool(parameter.nameHash, parameter.name == "Attacking");
            else if (parameter.type == AnimatorControllerParameterType.Float)
                animator.SetFloat(parameter.nameHash, parameter.name == "AttackSpeed" || parameter.name == "ActionSpeed" || parameter.name == "SkillSpeed" ? 1 : 0);
        }
    }
}
#endif
