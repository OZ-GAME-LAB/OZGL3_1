#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Applies measured sideview playback and mirrored-hand refinements after extra-action setup.</summary>
public static class SideviewAnimationRefinement
{
    const string Hero = "Assets/Art/Characters/Protagonist01/Runtime";
    const string Actions = Hero + "/Protagonist01_Actions.fbx";
    const string Controller = Hero + "/Protagonist01_Unarmed.controller";
    const string SkillMotionPath = Hero + "/Protagonist01_SkillMotion.anim";
    const string Output = "ArtSources/Stage01Kit/SideviewReview/animation-refinement.json";
    const float WalkNaturalSpeed = 1.15181f, RunNaturalSpeed = 3.319889f;

    [Serializable] public sealed class ClipEntry
    {
        public string name, sourceTake;
        public float originalFirstFrame, originalLastFrame, importedFirstFrame, importedLastFrame, seconds;
        public bool humanoid, mirror;
    }

    [Serializable] public sealed class Report
    {
        public bool success, playerDriverConfigured, visualDriverConfigured;
        public string unity, controller, skillMotion, sourceNotes, playbackNotes;
        public float walkNaturalStrideSpeed, runNaturalStrideSpeed, walkTimeScale, runTimeScale, defaultAttackContactNormalized;
        public int mirroredStates;
        public List<ClipEntry> clips = new List<ClipEntry>();
        public string[] weaponOverrides;
    }

    [MenuItem("Tools/Project M/Refine Sideview Character Animation")]
    public static void Build()
    {
        var report = new Report
        {
            unity = Application.unityVersion, controller = Controller, skillMotion = SkillMotionPath,
            walkNaturalStrideSpeed = WalkNaturalSpeed, runNaturalStrideSpeed = RunNaturalSpeed,
            walkTimeScale = 2.2f / WalkNaturalSpeed, runTimeScale = 5f / RunNaturalSpeed,
            defaultAttackContactNormalized = ProtagonistAnimationDriver.UnarmedAttackContactNormalized,
            sourceNotes = "Punch is mirrored into a right-hand jab and imports source Left_Jab_from_Guard from 0 through .62 of its full default take. " +
                "AirRise/AirFall are one-frame-wide looping pose imports at Regular_Jump fractions .30/.65. All intervals are computed from default source takes each run, preventing cumulative trimming. " +
                "Jab and every Regular_Jump-derived import use keepOriginalOrientation=false/lockRootRotation=false to align body orientation with the movement axis while ignoring extracted root motion. " +
                "Existing clip names are retained; only the two new air pose names are appended. Extra-action import/trim is owned by ProtagonistExtendedAnimationSetup.",
            playbackNotes = "FacingLeft mirrors all states, paired with the near-hand socket selection. Air blends AirFall/AirApex/AirRise at VelocityY -2/0/+2. " +
                "Walk and Run time scales match 2.2/5 m/s to raw low-stance backwards-foot median speed measured in motion-diagnostics.json. " +
                "Skill uses a dedicated Humanoid clone so weapon skill overrides cannot replace Idle. SkillSpeed follows equipped skill CastDuration, fallback .5 s. " +
                "This setup exposes attack contact timing but does not alter attack damage execution or purchase rules."
        };
        var baseClips = ImportBasePoseRefinements(report);
        var punch = RequiredClip(baseClips, "Punch");
        var rise = RequiredClip(baseClips, "AirRise");
        var fall = RequiredClip(baseClips, "AirFall");
        var apex = RequiredClip(baseClips, "AirApex");
        var skillMotion = BuildSkillMotion(punch);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
        if (!controller) throw new InvalidOperationException("Missing base protagonist AnimatorController.");
        EnsureParameter(controller, "FacingLeft", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "SkillSpeed", AnimatorControllerParameterType.Float, 1);
        EnsureParameter(controller, "VelocityY", AnimatorControllerParameterType.Float);
        foreach (var layer in controller.layers) report.mirroredStates += MirrorStates(layer.stateMachine);
        var states = controller.layers[0].stateMachine.states.Select(child => child.state).ToArray();
        var air = states.FirstOrDefault(state => state.name == "Air");
        var locomotion = states.FirstOrDefault(state => state.name == "Locomotion");
        var skill = states.FirstOrDefault(state => state.name == "Skill");
        if (!air || !locomotion || !skill) throw new InvalidOperationException("Base Air, Locomotion and Skill states are required.");
        var airTree = air.motion as BlendTree;
        if (!airTree || airTree.name != "Sideview Air Rise Apex Fall")
        {
            airTree = new BlendTree { name = "Sideview Air Rise Apex Fall" };
            AssetDatabase.AddObjectToAsset(airTree, controller);
        }
        airTree.blendType = BlendTreeType.Simple1D;
        airTree.blendParameter = "VelocityY";
        airTree.useAutomaticThresholds = false;
        airTree.children = new[]
        {
            new ChildMotion { motion = fall, threshold = -2, timeScale = 1 },
            new ChildMotion { motion = apex, threshold = 0, timeScale = 1 },
            new ChildMotion { motion = rise, threshold = 2, timeScale = 1 }
        };
        air.motion = airTree;
        EditorUtility.SetDirty(airTree);
        var locomotionTree = locomotion.motion as BlendTree;
        if (!locomotionTree) throw new InvalidOperationException("Locomotion must use the existing Walk/Run BlendTree.");
        var children = locomotionTree.children;
        bool foundWalk = false, foundRun = false;
        for (int index = 0; index < children.Length; index++)
        {
            if (children[index].motion != null && children[index].motion.name == "Walk")
            {
                children[index].timeScale = report.walkTimeScale; foundWalk = true;
            }
            if (children[index].motion != null && children[index].motion.name == "Run")
            {
                children[index].timeScale = report.runTimeScale; foundRun = true;
            }
        }
        if (!foundWalk || !foundRun) throw new InvalidOperationException("Walk and Run children missing from locomotion tree.");
        locomotionTree.children = children;
        EditorUtility.SetDirty(locomotionTree);
        skill.motion = skillMotion;
        skill.speedParameter = "SkillSpeed"; skill.speedParameterActive = true;
        EditorUtility.SetDirty(skill);
        var extras = AssetDatabase.LoadAllAssetsAtPath(Hero + "/Protagonist01_ExtendedActions.fbx").OfType<AnimationClip>().ToArray();
        ConfigureOverride(controller, punch, skillMotion, "Sword", RequiredClip(extras, "SwordSlash"));
        ConfigureOverride(controller, punch, skillMotion, "Staff", RequiredClip(extras, "MagicCast"));
        report.weaponOverrides = new[] { Hero + "/Protagonist01_Sword.overrideController", Hero + "/Protagonist01_Staff.overrideController" };
        report.playerDriverConfigured = ConfigureExistingDriver(Hero + "/Protagonist01_Player.prefab", punch);
        report.visualDriverConfigured = ConfigureExistingDriver(Hero + "/Protagonist01_Visual.prefab", punch);
        if (!report.playerDriverConfigured) throw new InvalidOperationException("Player prefab has no animation driver to configure.");
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        report.success = true;
        Directory.CreateDirectory(Path.GetDirectoryName(Output));
        File.WriteAllText(Output, JsonUtility.ToJson(report, true));
        Debug.Log("SIDEVIEW_ANIMATION_REFINEMENT_SUCCESS " + Output);
    }

    static AnimationClip[] ImportBasePoseRefinements(Report report)
    {
        var importer = AssetImporter.GetAtPath(Actions) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("Base protagonist action FBX importer missing.");
        var defaultTakes = importer.defaultClipAnimations;
        var punchTake = defaultTakes.FirstOrDefault(take => take.name == "Left_Jab_from_Guard" || take.takeName == "Left_Jab_from_Guard");
        var jumpTake = defaultTakes.FirstOrDefault(take => take.name == "Regular_Jump" || take.takeName == "Regular_Jump");
        if (punchTake == null || jumpTake == null) throw new InvalidOperationException("Source Jab and Regular_Jump takes are required.");
        var definitions = importer.clipAnimations.ToList();
        var punch = definitions.FirstOrDefault(clip => clip.name == "Punch");
        var jump = definitions.FirstOrDefault(clip => clip.name == "JumpFull");
        if (punch == null || jump == null) throw new InvalidOperationException("Import the existing base action clips first.");
        foreach (var definition in definitions)
            if (definition.takeName == punchTake.takeName || definition.takeName == jumpTake.takeName)
            {
                definition.keepOriginalOrientation = false;
                definition.lockRootRotation = false;
                definition.rotationOffset = 0;
            }
        punch.takeName = punchTake.takeName;
        punch.firstFrame = punchTake.firstFrame;
        punch.lastFrame = Mathf.Lerp(punchTake.firstFrame, punchTake.lastFrame, .62f);
        punch.mirror = true;
        foreach (var pose in new[] { new KeyValuePair<string, float>("AirRise", .30f), new KeyValuePair<string, float>("AirFall", .65f) })
        {
            var definition = definitions.FirstOrDefault(clip => clip.name == pose.Key);
            if (definition == null) { definition = new ModelImporterClipAnimation { name = pose.Key }; definitions.Add(definition); }
            float frame = Mathf.Lerp(jumpTake.firstFrame, jumpTake.lastFrame, pose.Value);
            definition.takeName = jumpTake.takeName;
            definition.firstFrame = frame; definition.lastFrame = Mathf.Min(frame + 1, jumpTake.lastFrame);
            definition.loopTime = true; definition.loopPose = true; definition.mirror = false;
            definition.lockRootRotation = jump.lockRootRotation;
            definition.keepOriginalOrientation = jump.keepOriginalOrientation;
            definition.rotationOffset = jump.rotationOffset;
            definition.lockRootPositionXZ = jump.lockRootPositionXZ;
            definition.keepOriginalPositionXZ = jump.keepOriginalPositionXZ;
            definition.lockRootHeightY = false; definition.keepOriginalPositionY = true;
            definition.events = Array.Empty<AnimationEvent>();
        }
        importer.clipAnimations = definitions.ToArray();
        importer.SaveAndReimport();
        var imported = AssetDatabase.LoadAllAssetsAtPath(Actions).OfType<AnimationClip>().ToArray();
        foreach (var definition in definitions.Where(clip => clip.name == "Punch" || clip.name == "AirRise" || clip.name == "AirFall"))
        {
            var clip = RequiredClip(imported, definition.name);
            var source = definition.name == "Punch" ? punchTake : jumpTake;
            report.clips.Add(new ClipEntry
            {
                name = definition.name, sourceTake = definition.takeName,
                originalFirstFrame = source.firstFrame, originalLastFrame = source.lastFrame,
                importedFirstFrame = definition.firstFrame, importedLastFrame = definition.lastFrame,
                seconds = clip.length, humanoid = clip.humanMotion, mirror = definition.mirror
            });
            if (!clip.humanMotion || clip.length <= 0) throw new InvalidOperationException("Expected positive-length Humanoid refined clip: " + clip.name);
        }
        return imported;
    }

    static AnimationClip BuildSkillMotion(AnimationClip punch)
    {
        var clone = Object.Instantiate(punch);
        clone.name = "SkillMotion";
        AnimationUtility.SetAnimationEvents(clone, Array.Empty<AnimationEvent>());
        var settings = AnimationUtility.GetAnimationClipSettings(clone);
        settings.loopTime = false; settings.loopBlend = false;
        AnimationUtility.SetAnimationClipSettings(clone, settings);
        var saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(SkillMotionPath);
        if (!saved) { AssetDatabase.CreateAsset(clone, SkillMotionPath); saved = clone; }
        else { EditorUtility.CopySerialized(clone, saved); Object.DestroyImmediate(clone); }
        EditorUtility.SetDirty(saved);
        if (!saved.humanMotion) throw new InvalidOperationException("Dedicated skill motion clone must remain Humanoid.");
        return saved;
    }

    static void ConfigureOverride(AnimatorController controller, AnimationClip punch, AnimationClip skillMotion,
        string weapon, AnimationClip attack)
    {
        string path = Hero + "/Protagonist01_" + weapon + ".overrideController";
        var result = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
        if (!result) throw new InvalidOperationException("Build extra-action weapon overrides before refining: " + path);
        result.runtimeAnimatorController = controller;
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        result.GetOverrides(overrides);
        bool attackBound = false, skillBound = false;
        for (int index = 0; index < overrides.Count; index++)
        {
            var key = overrides[index].Key;
            if (key == punch || key == skillMotion)
            {
                overrides[index] = new KeyValuePair<AnimationClip, AnimationClip>(key, attack);
                attackBound |= key == punch; skillBound |= key == skillMotion;
            }
        }
        if (!attackBound || !skillBound) throw new InvalidOperationException("Both Attack and dedicated Skill override keys required: " + weapon);
        result.ApplyOverrides(overrides);
        EditorUtility.SetDirty(result);
    }

    static bool ConfigureExistingDriver(string path, AnimationClip punch)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var driver = root.GetComponent<ProtagonistAnimationDriver>();
            if (!driver) return false;
            driver.Configure(punch); driver.SetAttackContactNormalized(ProtagonistAnimationDriver.UnarmedAttackContactNormalized);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return true;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static AnimationClip RequiredClip(IEnumerable<AnimationClip> clips, string name)
    {
        var result = clips.FirstOrDefault(clip => clip.name == name);
        if (!result) throw new InvalidOperationException("Missing animation clip: " + name);
        return result;
    }

    static int MirrorStates(AnimatorStateMachine machine)
    {
        int count = 0;
        foreach (var child in machine.states)
        {
            child.state.mirror = false;
            child.state.mirrorParameter = "FacingLeft"; child.state.mirrorParameterActive = true;
            EditorUtility.SetDirty(child.state);
            count++;
        }
        foreach (var child in machine.stateMachines) count += MirrorStates(child.stateMachine);
        return count;
    }

    static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type, float value = 0)
    {
        var existing = controller.parameters.FirstOrDefault(parameter => parameter.name == name);
        if (existing != null)
        {
            if (existing.type != type) throw new InvalidOperationException("Animator parameter type mismatch: " + name);
            return;
        }
        controller.AddParameter(new AnimatorControllerParameter { name = name, type = type, defaultFloat = value });
    }
}
#endif
