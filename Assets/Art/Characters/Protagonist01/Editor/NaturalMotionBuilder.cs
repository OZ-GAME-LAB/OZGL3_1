#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

// Offline asset preparation only. Never touches the game's controllers, prefabs or scenes.
public static class NaturalMotionBuilder
{
    public const string Root = "Assets/Art/Characters/Protagonist01/MotionNatural_v005";
    public const string Original = "Assets/Art/Characters/Protagonist01/Runtime";
    public const string Report = "ArtSources/Characters/Protagonist01/v005-natural-motion";

    [MenuItem("Project M/Natural Motion v005/Import AI Candidates")]
    public static void Build()
    {
        Directory.CreateDirectory(Root + "/Clips");
        Directory.CreateDirectory(Root + "/Prefabs");
        Directory.CreateDirectory(Report);
        AssetDatabase.Refresh();
        var logs = new List<string>();
        foreach (var path0 in Directory.GetFiles(Root + "/Sources", "*.fbx"))
        {
            string path = path0.Replace('\\', '/');
            string name = Path.GetFileNameWithoutExtension(path);
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = true;
            imp.animationCompression = ModelImporterAnimationCompression.Off;
            imp.resampleCurves = true;
            imp.optimizeGameObjects = false;
            imp.importCameras = false;
            imp.importLights = false;
            var hd = imp.humanDescription;
            hd.human = HumanMapping();
            hd.upperArmTwist = hd.lowerArmTwist = hd.upperLegTwist = hd.lowerLegTwist = .5f;
            hd.armStretch = hd.legStretch = .01f;
            hd.feetSpacing = 0;
            imp.humanDescription = hd;
            imp.SaveAndReimport();
            var takes = imp.defaultClipAnimations;
            if (takes.Length == 0) throw new Exception("Missing motion take " + path);
            bool loop = name.StartsWith("Idle") || name.StartsWith("Walk") || name.StartsWith("Run");
            var take = takes[0];
            take.name = name;
            take.loopTime = loop;
            take.loopPose = false;
            take.lockRootRotation = true;
            take.keepOriginalOrientation = true;
            take.lockRootPositionXZ = true;
            take.keepOriginalPositionXZ = true;
            take.lockRootHeightY = true;
            take.keepOriginalPositionY = true;
            take.heightFromFeet = false;
            take.events = Array.Empty<AnimationEvent>();
            imp.clipAnimations = new[] { take };
            imp.SaveAndReimport();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
            if (avatar == null || !avatar.isHuman || !avatar.isValid || clip == null || !clip.humanMotion)
                throw new Exception("Invalid humanoid import " + path);
            logs.Add(name + " | " + clip.length.ToString("F3") + " sec | humanoid=" + clip.humanMotion + " | source FPS=" + clip.frameRate);
        }
        BuildReviewPrefab();
        AssetDatabase.SaveAssets();
        File.WriteAllLines(Report + "/import-report.txt", logs);
        Debug.Log("NATURAL_MOTION_IMPORT_SUCCESS " + logs.Count + " candidates");
    }

    public static void BuildReviewPrefab()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Original + "/Protagonist01_Rig.fbx");
        var instance = Object.Instantiate(source);
        try
        {
            instance.name = "Protagonist01_MotionReview";
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
            animator.avatar = AssetDatabase.LoadAllAssetsAtPath(Original + "/Protagonist01_Rig.fbx").OfType<Avatar>().First();
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var material = AssetDatabase.LoadAssetAtPath<Material>(Original + "/Protagonist01_Unlit.mat");
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Characters/Protagonist01/MotionPolish_v004/Meshes/Protagonist01_PolishedHands.asset");
            if (mesh == null) mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Original + "/Protagonist01_GripMesh.asset");
            foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skin.sharedMaterials = Enumerable.Repeat(material, skin.sharedMaterials.Length).ToArray();
                skin.updateWhenOffscreen = true;
                if (mesh != null)
                {
                    skin.sharedMesh = mesh;
                    for (int i = 0; i < mesh.blendShapeCount; i++) skin.SetBlendShapeWeight(i, 100);
                }
            }
            foreach (string side in new[] { "RightHand", "LeftHand" })
            {
                var hand = instance.GetComponentsInChildren<Transform>().First(t => t.name == side);
                var socket = new GameObject(side == "RightHand" ? "WeaponSocket" : "WeaponSocket_Left").transform;
                socket.SetParent(hand, false);
            }
            PrefabUtility.SaveAsPrefabAsset(instance, Root + "/Prefabs/Protagonist01_MotionReview.prefab");
        }
        finally { Object.DestroyImmediate(instance); }
    }

    static HumanBone[] HumanMapping()
    {
        var m = new Dictionary<string,string> {
            {"Hips","Pelvis"},{"Spine","Spine1"},{"Chest","Spine2"},{"UpperChest","Spine3"},{"Neck","Neck"},{"Head","Head"},
            {"LeftUpperLeg","L_Hip"},{"LeftLowerLeg","L_Knee"},{"LeftFoot","L_Ankle"},{"LeftToes","L_Foot"},
            {"RightUpperLeg","R_Hip"},{"RightLowerLeg","R_Knee"},{"RightFoot","R_Ankle"},{"RightToes","R_Foot"},
            {"LeftShoulder","L_Collar"},{"LeftUpperArm","L_Shoulder"},{"LeftLowerArm","L_Elbow"},{"LeftHand","L_Wrist"},
            {"RightShoulder","R_Collar"},{"RightUpperArm","R_Shoulder"},{"RightLowerArm","R_Elbow"},{"RightHand","R_Wrist"}
        };
        foreach (var side in new[] { "Left", "Right" })
            foreach (var finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
                for (int j=0;j<3;j++)
                {
                    string part = new[] { "Proximal", "Intermediate", "Distal" }[j];
                    m[side + finger + part] = (side == "Left" ? "L_" : "R_") + (finger == "Little" ? "Pinky" : finger) + (j+1);
                }
        return m.Select(p => new HumanBone { humanName=p.Key, boneName=p.Value, limit=new HumanLimit { useDefaultValues=true } }).ToArray();
    }

    public static void SaveAsset(Object value, string path)
    {
        var old = AssetDatabase.LoadAssetAtPath<Object>(path);
        if (old == null) AssetDatabase.CreateAsset(value,path);
        else { EditorUtility.CopySerialized(value,old); EditorUtility.SetDirty(old); Object.DestroyImmediate(value); }
    }
}
#endif

