using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Run after ProtagonistUnitySetup.Build to add optional actions and weapon motion overrides.</summary>
public static class ProtagonistExtendedAnimationSetup
{
    public const string ExtendedFbxPath = ProtagonistUnitySetup.Folder + "/Protagonist01_ExtendedActions.fbx";
    public const string SwordOverridePath = ProtagonistUnitySetup.Folder + "/Protagonist01_Sword.overrideController";
    public const string StaffOverridePath = ProtagonistUnitySetup.Folder + "/Protagonist01_Staff.overrideController";
    private const string ControllerPath = ProtagonistUnitySetup.Folder + "/Protagonist01_Unarmed.controller";
    private const string ReportPath = "ArtSources/Characters/Protagonist01/v003-extended/animation-import.json";
    private static readonly string[] SourceTakes =
    {
        "Stand_and_Drink", "Over_Shoulder_Throw", "Left_Slash", "mage_soell_cast", "open_door", "Male_Bend_Over_Pick_Up"
    };
    private static readonly float[] DefaultDurations = { 1.35f, 0.75f, 0.45f, 0.65f, 0.85f, 1f };
    private static readonly float[] Starts = { .20f, .12f, .04f, .12f, 0f, .05f };
    private static readonly float[] Ends = { .73f, .72f, .34f, .75f, .38f, .38f };
    private static readonly float[] CommitFractions = { .70f, .40f, .78f, .73f, .421f, .485f };

    [Serializable] private sealed class ClipReport
    {
        public string name;
        public string sourceTake;
        public float seconds;
        public float defaultPlaybackSeconds;
        public float commitNormalizedTime;
        public bool humanMotion;
        public bool loop;
    }

    [Serializable] private sealed class Report
    {
        public bool success;
        public string unity;
        public string avatar;
        public string controller;
        public List<ClipReport> clips = new List<ClipReport>();
        public string swordOverride;
        public string staffOverride;
        public string notes;
    }

    [MenuItem("Tools/Project M/Add Protagonist Extra Actions")]
    public static void Build()
    {
        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ProtagonistUnitySetup.Folder + "/Protagonist01_Rig.fbx")
            .OfType<Avatar>().FirstOrDefault(candidate => candidate.isHuman && candidate.isValid);
        if (avatar == null) throw new InvalidOperationException("Build the base protagonist Humanoid rig first.");
        Dictionary<ProtagonistExtraAction, AnimationClip> clips = ImportClips(avatar);
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) throw new InvalidOperationException("Build the base protagonist controller first.");
        ExtendController(controller, clips);
        ConfigurePrefab(ProtagonistUnitySetup.VisualPath, clips);
        ConfigurePrefab(ProtagonistUnitySetup.PlayerPath, clips);
        CreateOverride(SwordOverridePath, controller, clips[ProtagonistExtraAction.SwordSlash]);
        CreateOverride(StaffOverridePath, controller, clips[ProtagonistExtraAction.MagicCast]);
        AssetDatabase.SaveAssets();
        WriteReport(avatar, clips);
        Debug.Log("PROTAGONIST_EXTENDED_ANIMATIONS_READY: Drink, Throw, SwordSlash, MagicCast, Interact, Pickup");
    }

    private static Dictionary<ProtagonistExtraAction, AnimationClip> ImportClips(Avatar avatar)
    {
        AssetDatabase.ImportAsset(ExtendedFbxPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        ModelImporter importer = AssetImporter.GetAtPath(ExtendedFbxPath) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("Missing extended-action FBX: " + ExtendedFbxPath);
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
        importer.sourceAvatar = avatar;
        importer.importAnimation = true;
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        importer.resampleCurves = false;
        importer.optimizeGameObjects = false;
        importer.SaveAndReimport();

        ModelImporterClipAnimation[] takes = importer.defaultClipAnimations;
        var definitions = new List<ModelImporterClipAnimation>();
        foreach (ProtagonistExtraAction action in Enum.GetValues(typeof(ProtagonistExtraAction)))
        {
            string key = SourceTakes[(int)action];
            ModelImporterClipAnimation source = takes.FirstOrDefault(take => take.name == key || take.takeName == key);
            if (source == null) throw new InvalidOperationException("Missing Meshy action take: " + key);
            definitions.Add(new ModelImporterClipAnimation
            {
                name = action.ToString(),
                takeName = source.takeName,
                firstFrame = Mathf.Lerp(source.firstFrame,source.lastFrame,Starts[(int)action]),
                lastFrame = Mathf.Lerp(source.firstFrame,source.lastFrame,Ends[(int)action]),
                mirror = action==ProtagonistExtraAction.Drink,
                loopTime = false,
                loopPose = false,
                // Grounded actions retain vertical body motion; physics owns root translation and yaw.
                lockRootRotation = false,
                keepOriginalOrientation = false,
                lockRootPositionXZ = false,
                keepOriginalPositionXZ = true,
                lockRootHeightY = true,
                keepOriginalPositionY = false,
                heightFromFeet = true,
                events = Array.Empty<AnimationEvent>()
            });
        }
        importer.clipAnimations = definitions.ToArray();
        importer.SaveAndReimport();
        var result = new Dictionary<ProtagonistExtraAction, AnimationClip>();
        AnimationClip[] imported = AssetDatabase.LoadAllAssetsAtPath(ExtendedFbxPath).OfType<AnimationClip>().ToArray();
        foreach (ProtagonistExtraAction action in Enum.GetValues(typeof(ProtagonistExtraAction)))
        {
            AnimationClip clip = imported.FirstOrDefault(candidate => candidate.name == action.ToString());
            if (clip == null || !clip.humanMotion || clip.length <= 0f)
                throw new InvalidOperationException("Invalid imported Humanoid action: " + action);
            result[action] = clip;
            Debug.Log("[Protagonist Extra] " + action + " <- " + SourceTakes[(int)action] + ", " + clip.length + "s");
        }
        return result;
    }

    private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type, float value = 0f)
    {
        AnimatorControllerParameter existing = controller.parameters.FirstOrDefault(parameter => parameter.name == name);
        if (existing != null)
        {
            if (existing.type != type) throw new InvalidOperationException("Animator parameter type mismatch: " + name);
            return;
        }
        controller.AddParameter(new AnimatorControllerParameter { name = name, type = type, defaultFloat = value });
    }

    private static void ExtendController(AnimatorController controller, Dictionary<ProtagonistExtraAction, AnimationClip> clips)
    {
        EnsureParameter(controller, "ActionBusy", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "ActionSpeed", AnimatorControllerParameterType.Float, 1f);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idle = machine.states.First(child => child.state.name == "Idle").state;
        AnimatorState air = machine.states.First(child => child.state.name == "Air").state;
        string[] names = Enum.GetNames(typeof(ProtagonistExtraAction));
        foreach (AnimatorStateTransition transition in machine.anyStateTransitions.ToArray())
            if (transition.destinationState != null && names.Contains(transition.destinationState.name))
                machine.RemoveAnyStateTransition(transition);
        foreach (ChildAnimatorState child in machine.states.ToArray())
            if (names.Contains(child.state.name)) machine.RemoveState(child.state);

        foreach (var pair in clips)
        {
            string name = pair.Key.ToString();
            EnsureParameter(controller, name, AnimatorControllerParameterType.Trigger);
            AnimatorState state = machine.AddState(name, new Vector3(1100 + (int)pair.Key % 2 * 250, 80 + (int)pair.Key / 2 * 110));
            state.motion = pair.Value;
            state.writeDefaultValues = true;
            state.speedParameter = "ActionSpeed";
            state.speedParameterActive = true;
            state.AddStateMachineBehaviour<ProtagonistActionStateCallbacks>().action = pair.Key;
            AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
            enter.hasExitTime = false;
            enter.hasFixedDuration = true;
            enter.duration = 0.04f;
            enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0f, name);
            enter.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");
            foreach (bool airborne in new[] { false, true })
            {
                AnimatorStateTransition exit = state.AddTransition(airborne ? air : idle);
                exit.hasExitTime = true;
                exit.exitTime = 1f;
                exit.hasFixedDuration = true;
                exit.duration = 0.04f;
                exit.AddCondition(airborne ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, "Air");
            }
        }
        EditorUtility.SetDirty(controller);
    }

    private static void ConfigurePrefab(string path, Dictionary<ProtagonistExtraAction, AnimationClip> clips)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            ProtagonistActionAnimator component = root.GetComponent<ProtagonistActionAnimator>() ?? root.AddComponent<ProtagonistActionAnimator>();
            component.Configure(clips.Select(pair => new ProtagonistActionAnimator.ActionDefinition
            {
                action = pair.Key,
                clip = pair.Value,
                duration = DefaultDurations[(int)pair.Key],
                commitNormalizedTime = CommitFractions[(int)pair.Key]
            }).ToArray());
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void CreateOverride(string path, AnimatorController controller, AnimationClip attack)
    {
        AnimatorOverrideController result = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
        if (result == null)
        {
            result = new AnimatorOverrideController(controller);
            AssetDatabase.CreateAsset(result, path);
        }
        else result.runtimeAnimatorController = controller;
        var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        result.GetOverrides(pairs);
        bool replaced = false;
        for (int index = 0; index < pairs.Count; index++)
        {
            AnimationClip source = pairs[index].Key;
            pairs[index] = new KeyValuePair<AnimationClip, AnimationClip>(source, source.name == "Punch" ? attack : null);
            replaced |= source.name == "Punch";
        }
        if (!replaced) throw new InvalidOperationException("No Punch motion to override in " + controller.name);
        result.ApplyOverrides(pairs);
        EditorUtility.SetDirty(result);
    }

    private static void WriteReport(Avatar avatar, Dictionary<ProtagonistExtraAction, AnimationClip> clips)
    {
        var report = new Report
        {
            success = true, unity = Application.unityVersion, avatar = AssetDatabase.GetAssetPath(avatar),
            controller = ControllerPath, swordOverride = SwordOverridePath, staffOverride = StaffOverridePath,
            notes = "All six actions are finite, non-looping Humanoid clips. Root motion is extracted and not applied. " +
                "Timed callbacks are animation-only; the caller owns item consumption, projectile creation, gate completion and damage. " +
                "Weapon overrides require ProtagonistAnimationDriver.Configure(the equipped attack clip). " +
                "Urban kit setup subsequently calibrates Drink commit timing; final timing is recorded in ArtSources/Stage01Kit/consumable-grip.json."
        };
        foreach (var pair in clips)
            report.clips.Add(new ClipReport
            {
                name = pair.Key.ToString(), sourceTake = SourceTakes[(int)pair.Key], seconds = pair.Value.length,
                defaultPlaybackSeconds = DefaultDurations[(int)pair.Key], commitNormalizedTime = CommitFractions[(int)pair.Key],
                humanMotion = pair.Value.humanMotion, loop = pair.Value.isLooping
            });
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
    }
}
