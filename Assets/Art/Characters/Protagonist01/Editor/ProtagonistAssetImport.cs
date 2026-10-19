using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Explicit, manually invoked import setup for the protagonist FBX assets.</summary>
public static class ProtagonistAssetImport
{
    private const string RuntimeDirectory = "Assets/Art/Characters/Protagonist01/Runtime/";
    private const string RigPath = RuntimeDirectory + "Protagonist01_Rig.fbx";
    private const string WalkPath = RuntimeDirectory + "Protagonist01_Walk.fbx";
    private const string RunPath = RuntimeDirectory + "Protagonist01_Run.fbx";
    private const string ActionsPath = RuntimeDirectory + "Protagonist01_Actions.fbx";

    public static void Import()
    {
        ModelImporter rigImporter = GetImporter(RigPath);
        GameObject sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
        if (sourceModel == null)
        {
            throw new InvalidOperationException("Cannot load protagonist rig hierarchy: " + RigPath);
        }

        ApplyCommonSettings(rigImporter);
        rigImporter.animationType = ModelImporterAnimationType.Human;
        rigImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        rigImporter.importAnimation = false;
        rigImporter.humanDescription = CreateHumanDescription(rigImporter, sourceModel);
        rigImporter.SaveAndReimport();

        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(RigPath).OfType<Avatar>()
            .FirstOrDefault(candidate => candidate.isValid && candidate.isHuman);
        if (avatar == null)
        {
            throw new InvalidOperationException("Protagonist rig did not produce a valid Humanoid Avatar.");
        }
        Debug.Log("[Protagonist Import] Humanoid Avatar valid: " + avatar.name);

        ImportLocomotion(WalkPath, "Walk", avatar);
        ImportLocomotion(RunPath, "Run", avatar);
        ImportActions(avatar);
        AssetDatabase.SaveAssets();
    }

    private static ModelImporter GetImporter(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
        {
            throw new InvalidOperationException("Missing protagonist FBX: " + path);
        }
        return importer;
    }

    private static void ApplyCommonSettings(ModelImporter importer)
    {
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        importer.resampleCurves = false;
        importer.optimizeGameObjects = false;
    }

    private static HumanDescription CreateHumanDescription(ModelImporter importer, GameObject sourceModel)
    {
        Transform[] transforms = sourceModel.GetComponentsInChildren<Transform>(true);
        Transform Find(string name)
        {
            return transforms.FirstOrDefault(transform => transform.name == name);
        }

        Transform hips = Find("Hips");
        if (hips == null)
        {
            throw new InvalidOperationException("Meshy rig has no Hips transform.");
        }

        // Meshy names this chain Spine02 -> Spine01 -> Spine. Read its actual hierarchy.
        Transform[] spine = hips.GetComponentsInChildren<Transform>(true)
            .Where(transform => transform.name.StartsWith("Spine", StringComparison.OrdinalIgnoreCase))
            .OrderBy(transform => DepthBelow(transform, hips)).ToArray();
        if (spine.Length == 0)
        {
            throw new InvalidOperationException("Meshy rig has no spine transforms below Hips.");
        }

        var human = new List<HumanBone>();
        void Map(string humanName, Transform bone, bool required = true)
        {
            if (bone == null)
            {
                if (required)
                {
                    throw new InvalidOperationException("Missing Meshy bone for " + humanName);
                }
                return;
            }
            string canonicalName = HumanTrait.BoneName.FirstOrDefault(name => name.Replace(" ", "") == humanName);
            if (canonicalName == null)
            {
                throw new InvalidOperationException("Unknown Unity Humanoid bone: " + humanName);
            }
            human.Add(new HumanBone
            {
                humanName = canonicalName,
                boneName = bone.name,
                limit = new HumanLimit { useDefaultValues = true }
            });
        }

        Map("Hips", hips);
        Map("Spine", spine[0]);
        if (spine.Length > 1) Map("Chest", spine[1]);
        if (spine.Length > 2) Map("UpperChest", spine[2]);
        Map("Neck", Find("neck") ?? Find("Neck"), false);
        Map("Head", Find("Head"));
        foreach (string side in new[] { "Left", "Right" })
        {
            Map(side + "Shoulder", Find(side + "Shoulder"), false);
            Map(side + "UpperArm", Find(side + "Arm"));
            Map(side + "LowerArm", Find(side + "ForeArm"));
            Map(side + "Hand", Find(side + "Hand"));
            Map(side + "UpperLeg", Find(side + "UpLeg"));
            Map(side + "LowerLeg", Find(side + "Leg"));
            Map(side + "Foot", Find(side + "Foot"));
            Map(side + "Toes", Find(side + "ToeBase"), false);
        }

        HumanDescription description = importer.humanDescription;
        description.human = human.ToArray();
        description.skeleton = transforms.Select(transform => new SkeletonBone
        {
            name = transform.name,
            position = transform.localPosition,
            rotation = transform.localRotation,
            scale = transform.localScale
        }).ToArray();
        Debug.Log("[Protagonist Import] Spine mapping: " + string.Join(" -> ", spine.Select(bone => bone.name)));
        return description;
    }

    private static int DepthBelow(Transform transform, Transform ancestor)
    {
        int depth = 0;
        while (transform != null && transform != ancestor)
        {
            depth++;
            transform = transform.parent;
        }
        return depth;
    }

    private static ModelImporter PrepareAnimation(string path, Avatar avatar)
    {
        ModelImporter importer = GetImporter(path);
        ApplyCommonSettings(importer);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
        importer.sourceAvatar = avatar;
        importer.importAnimation = true;
        importer.SaveAndReimport();
        return importer;
    }

    private static void ImportLocomotion(string path, string name, Avatar avatar)
    {
        ModelImporter importer = PrepareAnimation(path, avatar);
        ModelImporterClipAnimation source = importer.defaultClipAnimations.FirstOrDefault();
        if (source == null)
        {
            throw new InvalidOperationException("No default animation take in " + path);
        }
        importer.clipAnimations = new[] { CreateClip(source, name, 0f, 1f, true, false) };
        importer.SaveAndReimport();
        LogClips(importer);
    }

    private static void ImportActions(Avatar avatar)
    {
        ModelImporter importer = PrepareAnimation(ActionsPath, avatar);
        ModelImporterClipAnimation[] source = importer.defaultClipAnimations;
        ModelImporterClipAnimation Take(string name)
        {
            ModelImporterClipAnimation take = source.FirstOrDefault(clip => clip.name == name || clip.takeName == name);
            if (take == null)
            {
                throw new InvalidOperationException("Missing action take '" + name + "'. Available: " +
                    string.Join(", ", source.Select(clip => clip.name + " [" + clip.takeName + "]")));
            }
            return take;
        }

        ModelImporterClipAnimation idle = Take("Idle");
        ModelImporterClipAnimation punch = Take("Left_Jab_from_Guard");
        ModelImporterClipAnimation jump = Take("Regular_Jump");
        importer.clipAnimations = new[]
        {
            CreateClip(idle, "Idle", 0f, 1f, true, false),
            CreateClip(punch, "Punch", 0f, 1f, false, false),
            CreateClip(jump, "JumpFull", 0f, 1f, false, true),
            CreateClip(jump, "JumpTakeoff", 0f, 0.25f, false, true),
            CreateClip(jump, "AirApex", 0.45f, 0.55f, true, true),
            CreateClip(jump, "Land", 0.75f, 1f, false, true)
        };
        importer.SaveAndReimport();
        LogClips(importer);
    }

    private static ModelImporterClipAnimation CreateClip(ModelImporterClipAnimation source, string name,
        float firstFraction, float lastFraction, bool loop, bool jump)
    {
        return new ModelImporterClipAnimation
        {
            name = name,
            takeName = source.takeName,
            firstFrame = Mathf.Lerp(source.firstFrame, source.lastFrame, firstFraction),
            lastFrame = Mathf.Lerp(source.firstFrame, source.lastFrame, lastFraction),
            loopTime = loop,
            loopPose = loop,
            lockRootRotation = true,
            keepOriginalOrientation = true,
            lockRootPositionXZ = true,
            keepOriginalPositionXZ = true,
            lockRootHeightY = !jump,
            keepOriginalPositionY = true
        };
    }

    private static void LogClips(ModelImporter importer)
    {
        foreach (ModelImporterClipAnimation clip in importer.clipAnimations)
        {
            Debug.Log("[Protagonist Import] " + importer.assetPath + ": " + clip.name + " <- " +
                clip.takeName + " frames " + clip.firstFrame + "–" + clip.lastFrame +
                ", loop=" + clip.loopTime + ", bakeY=" + clip.lockRootHeightY);
        }
    }
}
