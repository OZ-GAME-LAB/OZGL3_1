#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class UrbanConsumableBuilder
{
    const string Hero = "Assets/Art/Characters/Protagonist01/Runtime";
    const string Props = "Assets/Art/Stage01Kit/Prefabs/Props";
    public const string ReportPath = "ArtSources/Stage01Kit/consumable-grip.json";
    const float GripHeight = .083f, BottleMouthHeight = .237f;
    static readonly Vector3 GripLocal = new Vector3(0, .045f, 0);

    [Serializable] public sealed class Report
    {
        public bool success;
        public string method, clip, socket;
        public float normalizedTime, handToMouthDistance, targetDistance, bottleMouthError;
        public Vector3 localPosition, gripLocal, mouthTargetWorld, gripWorld, bottleMouthWorld;
        public Quaternion localRotation;
        public string[] potionPrefabs;
        public bool mirroredPoseChecked;
        public string mirroredSocket;
        public float mirroredBottleMouthError;
        public Vector3 mirroredLocalPosition, mirroredMouthTargetWorld, mirroredBottleMouthWorld;
        public Quaternion mirroredLocalRotation;
    }

    [MenuItem("Tools/Project M/Calibrate Potion Hand Grip")]
    public static void Calibrate()
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Hero + "/Protagonist01_Player.prefab");
        var clip = AssetDatabase.LoadAllAssetsAtPath(Hero + "/Protagonist01_ExtendedActions.fbx")
            .OfType<AnimationClip>().FirstOrDefault(c => c.name == "Drink");
        if (!asset || !clip || !clip.humanMotion) throw new InvalidOperationException("Calibrate requires the player and Drink Humanoid clip.");
        var actor = Object.Instantiate(asset);
        actor.hideFlags = HideFlags.HideAndDontSave;
        actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var report = new Report
        {
            clip = clip.name, socket = "RightHand/WeaponSocket", gripLocal = GripLocal,
            targetDistance = BottleMouthHeight - GripHeight,
            potionPrefabs = new[] { "Potion_Health", "Potion_Attack", "Potion_Defense", "Potion_Speed" },
            method = "Both hands evaluated through the actual root Mecanim Animator controller, never a standalone AnimationClipPlayable. Right-facing Drink .22-.65 sampled at 173 normalized poses with FacingLeft=false. Head world position + up*.05 + forward*.15 estimates mouth. Minimize abs(socket grip-to-mouth distance-.154m). A fixed right mount plus hand-local X reflection is stored. Contact is rechecked with FacingLeft=true at the same normalized pose; bones, wrist animation and IK remain unchanged."
        };
        try
        {
            foreach (var behavior in actor.GetComponentsInChildren<MonoBehaviour>(true)) behavior.enabled = false;
            foreach (var body in actor.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.useGravity = false; }
            foreach (var collider in actor.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            var animator = actor.GetComponent<Animator>();
            if (!animator || !animator.avatar || !animator.avatar.isHuman || !animator.avatar.isValid)
                throw new InvalidOperationException("Valid root Humanoid Animator required.");
            foreach (var nested in actor.GetComponentsInChildren<Animator>(true))
                if (nested != animator) nested.enabled = false;
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Hero + "/Protagonist01_Unarmed.controller");
            if (!controller) throw new InvalidOperationException("Drink calibration requires the configured unarmed controller.");
            animator.enabled = true; animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind(); animator.Update(0);
            PrepareDrink(animator, false);
            var right = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            var socket = right.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "WeaponSocket") ?? right;
            float best = float.PositiveInfinity;
            const int samples = 172;
            for (int i = 0; i <= samples; i++)
            {
                float fraction = Mathf.Lerp(.22f, .65f, i / (float)samples);
                animator.Play("Base Layer.Drink", 0, fraction); animator.Update(0);
                Vector3 mouth = MouthTarget(head);
                Vector3 grip = socket.TransformPoint(GripLocal);
                float difference = Mathf.Abs(Vector3.Distance(mouth, grip) - report.targetDistance);
                if (difference < best) { best = difference; report.normalizedTime = fraction; }
            }
            animator.Play("Base Layer.Drink", 0, report.normalizedTime); animator.Update(0);
            report.mouthTargetWorld = MouthTarget(head);
            report.gripWorld = socket.TransformPoint(GripLocal);
            Vector3 direction = (report.mouthTargetWorld - report.gripWorld).normalized;
            Vector3 localAxis = socket.InverseTransformDirection(direction).normalized;
            report.localRotation = Quaternion.FromToRotation(Vector3.up, localAxis);
            report.localPosition = GripLocal - report.localRotation * (Vector3.up * GripHeight);
            report.handToMouthDistance = Vector3.Distance(report.gripWorld, report.mouthTargetWorld);
            report.bottleMouthWorld = socket.TransformPoint(report.localPosition + report.localRotation * (Vector3.up * BottleMouthHeight));
            report.bottleMouthError = Vector3.Distance(report.bottleMouthWorld, report.mouthTargetWorld);
            CheckMirroredContact(animator, report);
        }
        finally
        {
            Object.DestroyImmediate(actor);
        }
        foreach (string key in report.potionPrefabs)
        {
            string path = Props + "/" + key + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var mount = root.GetComponent<ConsumableHandProp>() ?? root.AddComponent<ConsumableHandProp>();
                mount.Configure(report.localPosition, report.localRotation);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (string name in new[] { "Protagonist01_Player", "Protagonist01_Visual" })
        {
            string path = Hero + "/" + name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var actions = root.GetComponent<ProtagonistActionAnimator>();
                if (!actions) throw new InvalidOperationException("Missing action component on " + path);
                var serialized = new SerializedObject(actions);
                var definitions = serialized.FindProperty("_actions");
                bool found = false;
                for (int i = 0; i < definitions.arraySize; i++)
                {
                    var definition = definitions.GetArrayElementAtIndex(i);
                    if (definition.FindPropertyRelative("action").enumValueIndex != (int)ProtagonistExtraAction.Drink) continue;
                    definition.FindPropertyRelative("commitNormalizedTime").floatValue = report.normalizedTime;
                    found = true; break;
                }
                if (!found) throw new InvalidOperationException("Missing Drink action definition on " + path);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        report.success = true;
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
        AssetDatabase.SaveAssets();
        Debug.Log("URBAN_CONSUMABLE_CALIBRATION_SUCCESS " + JsonUtility.ToJson(report));
    }

    public static Vector3 MouthTarget(Transform head) => head.position + head.up * .05f + head.forward * .15f;

    private static void CheckMirroredContact(Animator animator, Report report)
    {
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Hero + "/Protagonist01_Unarmed.controller");
        if (!controller) throw new InvalidOperationException("Mirrored Drink check requires the configured unarmed controller.");
        animator.runtimeAnimatorController = controller;
        animator.Rebind(); animator.Update(0);
        PrepareDrink(animator, true);
        animator.Play("Base Layer.Drink", 0, report.normalizedTime);
        animator.Update(0);
        Transform hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        Transform socket = hand.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "WeaponSocket_Left") ?? hand;
        report.mirroredLocalPosition = ConsumableHandProp.ReflectLocalPosition(report.localPosition);
        report.mirroredLocalRotation = ConsumableHandProp.ReflectLocalRotation(report.localRotation);
        report.mirroredMouthTargetWorld = MouthTarget(head);
        report.mirroredBottleMouthWorld = socket.TransformPoint(report.mirroredLocalPosition +
            report.mirroredLocalRotation * (Vector3.up * BottleMouthHeight));
        report.mirroredBottleMouthError = Vector3.Distance(report.mirroredBottleMouthWorld, report.mirroredMouthTargetWorld);
        report.mirroredSocket = "LeftHand/WeaponSocket_Left";
        report.mirroredPoseChecked = true;
    }

    private static void PrepareDrink(Animator animator, bool mirrored)
    {
        if (!animator.parameters.Any(p => p.name == "FacingLeft" && p.type == AnimatorControllerParameterType.Bool))
            throw new InvalidOperationException("Mirrored Drink check requires the FacingLeft Animator parameter.");
        animator.SetBool("FacingLeft", mirrored);
        foreach (var parameter in animator.parameters)
        {
            if (parameter.name == "ActionBusy" && parameter.type == AnimatorControllerParameterType.Bool) animator.SetBool(parameter.nameHash, true);
            if (parameter.name == "ActionSpeed" && parameter.type == AnimatorControllerParameterType.Float) animator.SetFloat(parameter.nameHash, 1f);
            if ((parameter.name == "IsDead" || parameter.name == "IsDashing" || parameter.name == "Air" || parameter.name == "Attacking") &&
                parameter.type == AnimatorControllerParameterType.Bool) animator.SetBool(parameter.nameHash, false);
        }
    }

    public static float ReadContactTime(float fallback = .45f)
    {
        if (!File.Exists(ReportPath)) return fallback;
        var report = JsonUtility.FromJson<Report>(File.ReadAllText(ReportPath));
        return report != null && report.success ? report.normalizedTime : fallback;
    }
}
#endif
