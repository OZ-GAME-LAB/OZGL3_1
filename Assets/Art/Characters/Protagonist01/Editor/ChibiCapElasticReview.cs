#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>One isolated elastic punch direction test. No gameplay integration or v005 migration.</summary>
public static class ChibiCapElasticReview
{
    const string Root = "Assets/Art/Characters/Protagonist01/ChibiCap_v006";
    const string RigPath = Root + "/Rig/Protagonist_ChibiCap_Rig.fbx";
    const string MaterialPath = Root + "/Materials/Protagonist_ChibiCap_Unlit.mat";
    const string FistsPath = Root + "/Meshes/Protagonist_ChibiCap_Fists.asset";
    const string PrefabPath = Root + "/Prefabs/Protagonist_ChibiCap_Rig.prefab";
    const string ClipPath = Root + "/Clips/Punch_Elastic_Preview.anim";
    const string Output = "ArtSources/Characters/Protagonist01/v006-chibi-cap/ElasticReview";
    const float Duration = 1.2f;
    const int BakeRate = 60, RenderRate = 30, ImageSize = 720;

    [Serializable] sealed class BoneRow
    {
        public string name, path, parent;
        public Vector3 localPosition, localScale, worldPosition;
        public Quaternion localRotation, worldRotation;
    }
    [Serializable] sealed class BindReport
    {
        public string rig, method;
        public float importedHeight, uniformNormalization;
        public BoneRow[] bones;
    }
    [Serializable] sealed class FrameRow
    {
        public float time, leftSoleY, rightSoleY, gripL, gripR;
        public Vector3 hips, head, faceForward, leftHand, rightHand, leftFoot, rightFoot;
    }
    [Serializable] sealed class ReviewReport
    {
        public string scope, method, rig, prefab, clip, fistsMesh, outputDirectory;
        public string[] notes;
        public bool fistsMeshApplied, gameplayConnected;
        public float clipDurationSeconds, bindHeightMetres, neutralHeightMetres, maximumRightHandDisplacementMetres;
        public int clipBakeRate, captureRate, renderedFrames, width, height;
        public float orthographicSize;
        public Vector3 cameraPosition, cameraTarget;
        public FrameRow[] frames;
    }
    sealed class Pose
    {
        public readonly Vector3[] p, s;
        public readonly Quaternion[] q;
        public Pose(Transform[] t)
        {
            p = t.Select(x => x.localPosition).ToArray(); q = t.Select(x => x.localRotation).ToArray();
            s = t.Select(x => x.localScale).ToArray();
        }
        public void Apply(Transform[] t)
        {
            for (int i = 0; i < t.Length; i++) { t[i].localPosition = p[i]; t[i].localRotation = q[i]; t[i].localScale = s[i]; }
        }
    }

    [MenuItem("Project M/Chibi Cap v006/Build Elastic Punch Direction Test")]
    public static void Build()
    {
        try
        {
            if (!File.Exists(RigPath)) throw new FileNotFoundException("Download the new Meshy rig first: " + RigPath);
            Directory.CreateDirectory(Root + "/Prefabs"); Directory.CreateDirectory(Root + "/Clips");
            Directory.CreateDirectory(Output + "/Frames30");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(RigPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var importer = (ModelImporter)AssetImporter.GetAtPath(RigPath);
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false; importer.animationType = ModelImporterAnimationType.Generic;
            importer.importCameras = false; importer.importLights = false; importer.isReadable = true;
            importer.meshCompression = ModelImporterMeshCompression.Off; importer.SaveAndReimport();
            ChibiCapHandBuilder.Build();
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (!material) throw new InvalidOperationException("Build the isolated static v006 material first.");
            using (var stage = new Stage())
            {
                var actor = stage.New("Protagonist_ChibiCap_Rig");
                var visual = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RigPath));
                SceneManager.MoveGameObjectToScene(visual, stage.scene); visual.name = "Visual";
                visual.transform.SetParent(actor.transform, false);
                foreach (var a in visual.GetComponentsInChildren<Animator>(true)) a.enabled = false;
                foreach (var b in visual.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
                var skins = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (skins.Length == 0) throw new InvalidOperationException("The new rig contains no skinned renderer.");
                bool fistsApplied = false;
                Mesh fists = AssetDatabase.LoadAssetAtPath<Mesh>(FistsPath);
                foreach (var skin in skins)
                {
                    if (fists && fists.vertexCount == skin.sharedMesh.vertexCount && fists.bindposes.Length == skin.sharedMesh.bindposes.Length)
                    { skin.sharedMesh = fists; fistsApplied = true; }
                    skin.sharedMaterials = Enumerable.Repeat(material, Mathf.Max(1, skin.sharedMaterials.Length)).ToArray();
                    skin.updateWhenOffscreen = true; skin.shadowCastingMode = ShadowCastingMode.Off;
                    skin.receiveShadows = false; SetGrip(skin, "Grip_R", 100); SetGrip(skin, "Grip_L", 100);
                }
                if (fists && !fistsApplied) throw new InvalidOperationException("The optional fists mesh does not match the new rig's vertex/bindpose counts.");
                Bounds sourceBounds = SkinBounds(skins);
                float scale = 1.2f / sourceBounds.size.y; visual.transform.localScale *= scale;
                Bounds normalized = SkinBounds(skins);
                visual.transform.localPosition += new Vector3(-normalized.center.x, -normalized.min.y, -normalized.center.z);
                var animator = actor.AddComponent<Animator>(); animator.avatar = null;
                animator.runtimeAnimatorController = null; animator.applyRootMotion = false; animator.enabled = false;
                Transform[] transforms = actor.GetComponentsInChildren<Transform>(true).Where(t => t != actor.transform).ToArray();
                var bind = new BindReport { rig = RigPath, importedHeight = sourceBounds.size.y, uniformNormalization = scale,
                    method = "Original imported bind transforms after only uniform 1.2m normalization and XZ centering/sole grounding. No Animator.Rebind or AI idle pose used.",
                    bones = transforms.Select(t => new BoneRow { name = t.name, path = AnimationUtility.CalculateTransformPath(t, actor.transform),
                        parent = t.parent ? t.parent.name : "", localPosition = t.localPosition, localRotation = t.localRotation,
                        localScale = t.localScale, worldPosition = t.position, worldRotation = t.rotation }).ToArray() };
                File.WriteAllText(Output + "/original-bind-bones.json", JsonUtility.ToJson(bind, true));
                var rig = new Rig(actor, skins, transforms);
                rig.MakeNeutral();
                bool saved; PrefabUtility.SaveAsPrefabAsset(actor, PrefabPath, out saved);
                if (!saved) throw new InvalidOperationException("Could not save the new rig-only preview prefab.");
                AnimationClip clip = Bake(actor, transforms, skins, rig);
                SaveClip(clip); AssetDatabase.SaveAssets();
                clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
                var snapshots = skins.Select(s => new CpuSkin(s)).ToArray();
                try
                {
                    actor.transform.rotation = Quaternion.Euler(0, 90, 0);
                    var rows = new List<FrameRow>(); Bounds allBounds = new Bounds(); bool first = true;
                    Vector3 neutralHand = Vector3.zero; float maxHand = 0;
                    for (int i = 0; i <= Mathf.RoundToInt(Duration * BakeRate); i++)
                    {
                        float time = i / (float)BakeRate;
                        clip.SampleAnimation(actor, time); foreach (var snapshot in snapshots) snapshot.Bake();
                        Bounds b = SnapshotBounds(snapshots);
                        if (first) { allBounds = b; neutralHand = rig.rh.position; first = false; } else allBounds.Encapsulate(b);
                        maxHand = Mathf.Max(maxHand, Vector3.Distance(neutralHand, rig.rh.position));
                        rows.Add(rig.Frame(time));
                    }
                    if (maxHand < .05f) throw new InvalidOperationException("The saved Generic clip did not move the hand sufficiently; verify transform bindings.");
                    Vector3 center = new Vector3(0, .60f, 0);
                    stage.Frame(allBounds, center, new Vector3(1.05f, 1.05f, -10));
                    var cameraPosition = stage.camera.transform.position; float ortho = stage.camera.orthographicSize;
                    int renderCount = Mathf.RoundToInt(Duration * RenderRate);
                    for (int i = 0; i < renderCount; i++)
                    {
                        clip.SampleAnimation(actor, i / (float)RenderRate);
                        foreach (var snapshot in snapshots) snapshot.Bake();
                        stage.Save(Output + "/Frames30/" + i.ToString("D4") + ".png");
                    }
                    clip.SampleAnimation(actor, Duration); foreach (var snapshot in snapshots) snapshot.Bake();
                    stage.Save(Output + "/Recovery_End.png");
                    clip.SampleAnimation(actor, 0); foreach (var snapshot in snapshots) snapshot.Bake();
                    stage.Save(Output + "/Neutral_Side_2p5D.png");
                    Bounds neutralBounds = SnapshotBounds(snapshots);
                    actor.transform.rotation = Quaternion.Euler(0, 180, 0);
                    foreach (var snapshot in snapshots) snapshot.Bake();
                    stage.SetView(center, new Vector3(0, 1, -10), ortho);
                    stage.Save(Output + "/Neutral_Front.png");
                    var report = new ReviewReport { scope = "One elastic action direction test; not migration or rework of the previous fourteen motions.",
                        method = "60Hz Generic clip baked offline, saved/reloaded from the new .anim, then only AnimationClip.SampleAnimation is used. Actual source skins are CPU BakeMesh snapshots rendered by Unity at 30fps.",
                        rig = RigPath, prefab = PrefabPath, clip = ClipPath, fistsMesh = fistsApplied ? FistsPath : "",
                        fistsMeshApplied = fistsApplied, gameplayConnected = false, outputDirectory = Path.GetFullPath(Output),
                        clipDurationSeconds = Duration, bindHeightMetres = 1.2f, neutralHeightMetres = neutralBounds.size.y,
                        maximumRightHandDisplacementMetres = maxHand, clipBakeRate = BakeRate, captureRate = RenderRate,
                        renderedFrames = renderCount, width = ImageSize, height = ImageSize, cameraPosition = cameraPosition,
                        cameraTarget = center, orthographicSize = ortho, frames = rows.ToArray(),
                        notes = new[] { "Anticipation/compression0-.22s; rapid extension.22-.32s; impact hold.32-.39s; damped recoil/overshoot.39-.75s; neutral recovery by1.2s.",
                            "Original bind torso plus anatomical-length leg/arm IK forms neutral. The headfront marker sets horizontal forward gaze. No AI idle frame is used as neutral.",
                            "Pelvis initiates translation/yaw; three spine joints follow with18/36/54ms offsets. Rear heel roll uses the actual neutral skinned sole/toe contact point.",
                            "No whole-body squash/stretch scaling is applied. This is a pose, spacing and timing direction test.",
                            "If fistsMeshApplied is false the source hand geometry was preserved; closed fists then require the separate new hand asset before final delivery.",
                            "Preview floor is a contact reference. No player/controller/input/combat logic or existing motion assets are modified.",
                            "The rig FBX has Read/Write enabled for offline CPU skin/sole inspection; this is not a measured runtime performance configuration." } };
                    File.WriteAllText(Output + "/elastic-review.json", JsonUtility.ToJson(report, true));
                    File.WriteAllText(Output + "/README_ko.txt", "탄성 액션 방향을 확인하는 주먹 공격 1개 테스트입니다. 기존14개 모션 전체 이식이나 게임 적용이 아닙니다.\n실제 새 리그·저장된 Generic 클립의 Unity CPU 스키닝 렌더입니다.\n기본 준비압축→빠른 신전→짧은 타격hold→감쇠 overshoot→중립 복귀를 확인합니다.\n최종 손 메시 적용 여부, 실제 접지와 카메라/샘플링 정보는 elastic-review.json을 확인하세요.\n");
                }
                finally { foreach (var snapshot in snapshots) snapshot.Dispose(); }
                rig.Dispose();
            }
            AssetDatabase.SaveAssets(); Debug.Log("CHIBI_CAP_ELASTIC_REVIEW_SUCCESS " + Path.GetFullPath(Output));
        }
        catch (Exception exception)
        { Debug.LogException(exception); if (Application.isBatchMode) EditorApplication.Exit(1); else throw; }
    }

    static AnimationClip Bake(GameObject actor, Transform[] transforms, SkinnedMeshRenderer[] skins, Rig rig)
    {
        int count = Mathf.RoundToInt(Duration * BakeRate) + 1;
        var poses = new Pose[count];
        for (int i = 0; i < count; i++) { rig.Evaluate(i / (float)BakeRate); poses[i] = new Pose(transforms); }
        for (int j = 0; j < transforms.Length; j++) for (int i = 1; i < count; i++)
            if (Quaternion.Dot(poses[i - 1].q[j], poses[i].q[j]) < 0)
            { Quaternion q = poses[i].q[j]; poses[i].q[j] = new Quaternion(-q.x, -q.y, -q.z, -q.w); }
        var clip = new AnimationClip { name = "Punch_Elastic_Preview", frameRate = BakeRate, legacy = false, wrapMode = WrapMode.ClampForever };
        for (int j = 0; j < transforms.Length; j++)
        {
            int bone = j; string path = AnimationUtility.CalculateTransformPath(transforms[j], actor.transform);
            for (int axis = 0; axis < 3; axis++)
            {
                int component = axis; string suffix = "." + "xyz"[axis];
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition" + suffix), Curve(poses.Select(p => p.p[bone][component]).ToArray()));
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalScale" + suffix), Curve(poses.Select(p => p.s[bone][component]).ToArray()));
            }
            for (int axis = 0; axis < 4; axis++)
            {
                int component = axis;
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation." + "xyzw"[axis]), Curve(poses.Select(p => p.q[bone][component]).ToArray()));
            }
        }
        foreach (var skin in skins) foreach (string grip in new[] { "Grip_R", "Grip_L" })
            if (skin.sharedMesh.GetBlendShapeIndex(grip) >= 0)
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(skin.transform, actor.transform), typeof(SkinnedMeshRenderer), "blendShape." + grip), AnimationCurve.Constant(0, Duration, 100));
        clip.EnsureQuaternionContinuity();
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = false; AnimationUtility.SetAnimationClipSettings(clip, settings);
        rig.neutral.Apply(transforms); return clip;
    }
    static AnimationCurve Curve(float[] values)
    {
        if (values.All(v => Mathf.Abs(v - values[0]) < .0000001f)) return AnimationCurve.Constant(0, Duration, values[0]);
        var keys = new Keyframe[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            float tangent = i == 0 ? (values[1] - values[0]) * BakeRate : i == values.Length - 1 ? (values[i] - values[i - 1]) * BakeRate : (values[i + 1] - values[i - 1]) * BakeRate * .5f;
            keys[i] = new Keyframe(i / (float)BakeRate, values[i], tangent, tangent);
        }
        return new AnimationCurve(keys);
    }
    static void SaveClip(AnimationClip clip)
    {
        var previous = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (!previous) AssetDatabase.CreateAsset(clip, ClipPath);
        else { EditorUtility.CopySerialized(clip, previous); EditorUtility.SetDirty(previous); Object.DestroyImmediate(clip); }
    }
    static void SetGrip(SkinnedMeshRenderer skin, string name, float value)
    { int i = skin.sharedMesh.GetBlendShapeIndex(name); if (i >= 0) skin.SetBlendShapeWeight(i, value); }
    static float Grip(SkinnedMeshRenderer skin, string name)
    { int i = skin.sharedMesh.GetBlendShapeIndex(name); return i >= 0 ? skin.GetBlendShapeWeight(i) : -1; }
    static Bounds SkinBounds(SkinnedMeshRenderer[] skins)
    {
        var mesh = new Mesh(); bool first = true; Bounds result = new Bounds();
        try { foreach (var skin in skins) { skin.BakeMesh(mesh); foreach (var v in mesh.vertices)
            { Vector3 p = skin.transform.TransformPoint(v); if (first) { result = new Bounds(p, Vector3.zero); first = false; } else result.Encapsulate(p); } } }
        finally { Object.DestroyImmediate(mesh); } return result;
    }
    static Bounds SnapshotBounds(CpuSkin[] snapshots)
    { Bounds b = snapshots[0].renderer.bounds; foreach (var s in snapshots.Skip(1)) b.Encapsulate(s.renderer.bounds); return b; }
    static float Track(float t, params float[] keys)
    {
        if (t <= keys[0]) return keys[1];
        for (int i = 0; i < keys.Length - 2; i += 2)
            if (t <= keys[i + 2]) return Mathf.LerpUnclamped(keys[i + 1], keys[i + 3], Mathf.SmoothStep(0, 1, Mathf.InverseLerp(keys[i], keys[i + 2], t)));
        return keys[keys.Length - 1];
    }

    sealed class Rig : IDisposable
    {
        public readonly Transform hips, head, lh, rh, lf, rf;
        readonly Transform neck, headfront, lua, lla, rua, rla, lul, lll, rul, rll, ls, rs;
        readonly Transform[] spine, transforms;
        readonly SkinnedMeshRenderer[] skins;
        readonly SoleProbe soles;
        public Pose neutral;
        Vector3 leftFootTarget, rightFootTarget, rearPivot, lHandNeutral, rHandNeutral, lShoulder, rShoulder, lForearmNeutral, rForearmNeutral;
        Quaternion lfRotation, rfRotation, lhRotation, rhRotation, headRotation;
        float leftArmLength, rightArmLength, rightSign, leftSign;
        public Rig(GameObject actor, SkinnedMeshRenderer[] skins, Transform[] transforms)
        {
            this.skins = skins; this.transforms = transforms;
            Transform Find(params string[] names)
            {
                foreach (string name in names) { var t = transforms.FirstOrDefault(x => string.Equals(x.name, name, StringComparison.OrdinalIgnoreCase)); if (t) return t; }
                throw new InvalidOperationException("Missing new rig bone: " + string.Join("/", names));
            }
            hips = Find("Hips", "Pelvis"); head = Find("Head"); neck = Find("neck", "Neck");
            headfront = transforms.FirstOrDefault(t => t.name == "headfront");
            spine = hips.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Spine", StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => Depth(t, hips)).Take(3).ToArray();
            if (spine.Length != 3) throw new InvalidOperationException("Expected the three actual parent-ordered Meshy spine joints.");
            lua = Find("LeftArm", "UpperArm_L"); lla = Find("LeftForeArm", "LowerArm_L"); lh = Find("LeftHand", "Hand_L");
            rua = Find("RightArm", "UpperArm_R"); rla = Find("RightForeArm", "LowerArm_R"); rh = Find("RightHand", "Hand_R");
            lul = Find("LeftUpLeg", "UpperLeg_L"); lll = Find("LeftLeg", "LowerLeg_L"); lf = Find("LeftFoot", "Foot_L");
            rul = Find("RightUpLeg", "UpperLeg_R"); rll = Find("RightLeg", "LowerLeg_R"); rf = Find("RightFoot", "Foot_R");
            ls = Find("LeftShoulder"); rs = Find("RightShoulder"); soles = new SoleProbe(skins, lf, rf);
        }
        static int Depth(Transform t, Transform parent) { int n = 0; while (t && t != parent) { n++; t = t.parent; } return n; }
        public void MakeNeutral()
        {
            hips.rotation = Quaternion.FromToRotation((spine[0].position - hips.position).normalized, Vector3.up) * hips.rotation;
            lfRotation = lf.rotation; rfRotation = rf.rotation;
            Quaternion sourceLH = lh.rotation, sourceRH = rh.rotation;
            Vector3 sourceLDir = (lh.position - lla.position).normalized, sourceRDir = (rh.position - rla.position).normalized;
            soles.Bake();
            float leftOffset = lf.position.y - soles.Minimum(true), rightOffset = rf.position.y - soles.Minimum(false);
            float meanZ = (lf.position.z + rf.position.z) * .5f;
            leftSign = Mathf.Sign(lh.position.x - hips.position.x); rightSign = Mathf.Sign(rh.position.x - hips.position.x);
            float leftX = Mathf.Sign(lf.position.x - hips.position.x) * Mathf.Clamp(Mathf.Abs(lf.position.x), .075f, .14f);
            float rightX = Mathf.Sign(rf.position.x - hips.position.x) * Mathf.Clamp(Mathf.Abs(rf.position.x), .075f, .14f);
            leftFootTarget = new Vector3(leftX, leftOffset + .003f, meanZ + .025f);
            rightFootTarget = new Vector3(rightX, rightOffset + .003f, meanZ - .025f);
            Vector3 p = hips.position; p.x = (leftX + rightX) * .5f; p.z = meanZ;
            hips.position = p;
            float allowedY = Mathf.Min(MaxHipY(lul, lll, lf, leftFootTarget), MaxHipY(rul, rll, rf, rightFootTarget));
            p = hips.position; p.y = Mathf.Min(p.y, allowedY); hips.position = p;
            Legs(leftFootTarget, rightFootTarget, lfRotation, rfRotation);
            leftArmLength = Vector3.Distance(lua.position, lla.position) + Vector3.Distance(lla.position, lh.position);
            rightArmLength = Vector3.Distance(rua.position, rla.position) + Vector3.Distance(rla.position, rh.position);
            Vector3 leftTarget = lua.position + Vector3.down * (leftArmLength * .90f) + Vector3.forward * (leftArmLength * .18f) + Vector3.right * (leftSign * leftArmLength * .14f);
            Vector3 rightTarget = rua.position + Vector3.down * (rightArmLength * .90f) + Vector3.forward * (rightArmLength * .18f) + Vector3.right * (rightSign * rightArmLength * .14f);
            IK(lua, lla, lh, leftTarget, lua.position + Vector3.down * leftArmLength + Vector3.right * leftSign * .1f - Vector3.forward * .08f);
            IK(rua, rla, rh, rightTarget, rua.position + Vector3.down * rightArmLength + Vector3.right * rightSign * .1f - Vector3.forward * .08f);
            lh.rotation = Quaternion.FromToRotation(sourceLDir, (lh.position - lla.position).normalized) * sourceLH;
            rh.rotation = Quaternion.FromToRotation(sourceRDir, (rh.position - rla.position).normalized) * sourceRH;
            if (headfront && (headfront.position - head.position).sqrMagnitude > .000001f)
                head.rotation = Quaternion.FromToRotation((headfront.position - head.position).normalized, Vector3.forward) * head.rotation;
            headRotation = head.rotation; lhRotation = lh.rotation; rhRotation = rh.rotation;
            lForearmNeutral = (lh.position - lla.position).normalized; rForearmNeutral = (rh.position - rla.position).normalized;
            lHandNeutral = lh.position; rHandNeutral = rh.position; lShoulder = lua.position; rShoulder = rua.position;
            soles.Bake(); rearPivot = soles.FrontContact(false);
            neutral = new Pose(transforms);
        }
        float MaxHipY(Transform a, Transform b, Transform c, Vector3 foot)
        {
            float reach = (Vector3.Distance(a.position, b.position) + Vector3.Distance(b.position, c.position)) * .985f;
            float horizontal = new Vector2(a.position.x - foot.x, a.position.z - foot.z).magnitude;
            return foot.y + Mathf.Sqrt(Mathf.Max(.0001f, reach * reach - horizontal * horizontal)) - (a.position.y - hips.position.y);
        }
        static float Pulse(float t) => Track(t, 0, 0, .20f, -.30f, .29f, 1, .39f, 1, .49f, -.20f, .62f, .07f, .82f, 0, 1.2f, 0);
        public void Evaluate(float t)
        {
            neutral.Apply(transforms);
            float pulse = Pulse(t), drive = .04f * pulse;
            float compression = .035f * Track(t, 0, 0, .22f, 1, .30f, .12f, .39f, .12f, .48f, .35f, .65f, 0, 1.2f, 0);
            hips.position += new Vector3(leftSign * .010f * Mathf.Max(0, pulse), -compression, drive);
            hips.rotation = Quaternion.AngleAxis(-rightSign * 8 * pulse, Vector3.up) * Quaternion.AngleAxis(2 * Mathf.Max(0, pulse) + compression * 65, Vector3.right) * hips.rotation;
            for (int i = 0; i < 3; i++)
            {
                float delayed = Pulse(t - .018f * (i + 1));
                spine[i].rotation = Quaternion.AngleAxis(-rightSign * (3 + 2 * i) * delayed, Vector3.up) * Quaternion.AngleAxis((1 + i * .5f) * delayed, Vector3.right) * spine[i].rotation;
            }
            rs.rotation = Quaternion.AngleAxis(-rightSign * 4 * Pulse(t - .035f), Vector3.up) * rs.rotation;
            ls.rotation = Quaternion.AngleAxis(rightSign * 1.5f * Pulse(t - .045f), Vector3.up) * ls.rotation;
            head.rotation = Quaternion.AngleAxis(1.1f * Track(t, 0, 0, .20f, 1, .32f, -.5f, .39f, -.5f, .55f, .35f, .80f, 0, 1.2f, 0), Vector3.right) * headRotation;

            float step = .025f * Track(t, 0, 0, .13f, 0, .29f, 1, .45f, 1, .76f, 0, 1.2f, 0);
            float lift = t > .13f && t < .29f ? .009f * Mathf.Sin(Mathf.PI * (t - .13f) / .16f) : 0;
            float heel = 8 * Mathf.Max(0, Pulse(t - .015f));
            Quaternion roll = Quaternion.AngleAxis(heel, Vector3.right);
            Vector3 leftGoal = leftFootTarget + new Vector3(0, lift, step);
            Vector3 rightGoal = rearPivot + roll * (rightFootTarget - rearPivot);
            Legs(leftGoal, rightGoal, lfRotation, roll * rfRotation);

            Vector3 rightGuard = rShoulder + new Vector3(rightSign * rightArmLength * .05f, -rightArmLength * .40f, rightArmLength * .45f);
            Vector3 rightWind = rightGuard + new Vector3(rightSign * rightArmLength * .07f, .015f, -rightArmLength * .10f);
            // Strike from the evaluated shoulder. Using the cached neutral shoulder
            // lost reach when hip/chest rotation carried the shoulder forward.
            Vector3 rightStrike = rua.position + Vector3.forward * (rightArmLength * .99f);
            Vector3 rightTarget = MotionPath(t, new[] { 0f, .16f, .22f, .32f, .39f, .50f, .62f, .76f, 1.05f, 1.2f },
                new[] { rHandNeutral, rightGuard, rightWind, rightStrike, rightStrike, rightGuard - Vector3.forward * .025f,
                    rightGuard + Vector3.forward * .010f, rightGuard, rHandNeutral, rHandNeutral });
            Vector3 leftGuard = lShoulder + new Vector3(leftSign * leftArmLength * .05f, -leftArmLength * .40f, leftArmLength * .45f);
            Vector3 leftTarget = MotionPath(t, new[] { 0f, .17f, .25f, .35f, .50f, .78f, 1.05f, 1.2f },
                new[] { lHandNeutral, leftGuard, leftGuard, leftGuard - Vector3.forward * .018f + Vector3.up * .008f,
                    leftGuard + Vector3.forward * .008f, leftGuard, lHandNeutral, lHandNeutral });
            float fullExtension = Track(t, 0, 0, .22f, 0, .32f, 1, .39f, 1, .50f, 0, 1.2f, 0);
            rightTarget += new Vector3(leftSign * .006f * Mathf.Max(0, pulse), -compression * .35f, drive) * (1 - fullExtension);
            leftTarget += new Vector3(leftSign * .006f * Mathf.Max(0, pulse), -compression * .35f, drive * .8f);
            IK(rua, rla, rh, rightTarget, rua.position + Vector3.down * rightArmLength + Vector3.right * rightSign * .14f - Vector3.forward * .10f);
            IK(lua, lla, lh, leftTarget, lua.position + Vector3.down * leftArmLength + Vector3.right * leftSign * .14f - Vector3.forward * .10f);
            rh.rotation = Quaternion.FromToRotation(rForearmNeutral, (rh.position - rla.position).normalized) * rhRotation;
            lh.rotation = Quaternion.FromToRotation(lForearmNeutral, (lh.position - lla.position).normalized) * lhRotation;
            for (int iteration = 0; iteration < 2; iteration++)
            {
                soles.Bake(); float l = soles.Minimum(true), r = soles.Minimum(false);
                if (l >= -.0005f && r >= -.0005f) break;
                leftGoal.y += Mathf.Max(0, -l + .001f); rightGoal.y += Mathf.Max(0, -r + .001f);
                Legs(leftGoal, rightGoal, lfRotation, roll * rfRotation);
            }
        }
        void Legs(Vector3 left, Vector3 right, Quaternion leftRot, Quaternion rightRot)
        {
            IK(lul, lll, lf, left, lul.position + Vector3.forward * .5f + Vector3.right * Mathf.Sign(left.x) * .04f);
            lf.rotation = leftRot;
            IK(rul, rll, rf, right, rul.position + Vector3.forward * .5f + Vector3.right * Mathf.Sign(right.x) * .04f);
            rf.rotation = rightRot;
        }
        public FrameRow Frame(float t)
        {
            soles.Bake(); return new FrameRow { time = t, hips = hips.position, head = head.position,
                faceForward = headfront ? (headfront.position - head.position).normalized : Vector3.zero,
                leftHand = lh.position, rightHand = rh.position, leftFoot = lf.position, rightFoot = rf.position,
                leftSoleY = soles.Minimum(true), rightSoleY = soles.Minimum(false),
                gripL = skins.Max(s => Grip(s, "Grip_L")), gripR = skins.Max(s => Grip(s, "Grip_R")) };
        }
        public void Dispose() { soles.Dispose(); }
    }
    static Vector3 MotionPath(float t, float[] times, Vector3[] positions)
    {
        if (t <= times[0]) return positions[0];
        for (int i = 0; i < times.Length - 1; i++) if (t <= times[i + 1])
            return Vector3.LerpUnclamped(positions[i], positions[i + 1], Mathf.SmoothStep(0, 1, Mathf.InverseLerp(times[i], times[i + 1], t)));
        return positions[positions.Length - 1];
    }
    static void IK(Transform a, Transform b, Transform c, Vector3 target, Vector3 hint)
    {
        Vector3 origin = a.position, vector = target - origin;
        float first = Vector3.Distance(origin, b.position), second = Vector3.Distance(b.position, c.position);
        if (first < .00001f || second < .00001f || vector.sqrMagnitude < .0000001f) return;
        Vector3 direction = vector.normalized;
        float distance = Mathf.Clamp(vector.magnitude, Mathf.Abs(first - second) + .0001f, first + second - .0001f);
        Vector3 bend = Vector3.ProjectOnPlane(hint - origin, direction).normalized;
        if (bend.sqrMagnitude < .01f) bend = Vector3.ProjectOnPlane(Vector3.forward, direction).normalized;
        float cosine = Mathf.Clamp((first * first + distance * distance - second * second) / (2 * first * distance), -1, 1);
        Vector3 elbow = origin + direction * (first * cosine) + bend * (first * Mathf.Sqrt(Mathf.Max(0, 1 - cosine * cosine)));
        a.rotation = Quaternion.FromToRotation(b.position - origin, elbow - origin) * a.rotation;
        b.rotation = Quaternion.FromToRotation(c.position - b.position, target - b.position) * b.rotation;
    }

    sealed class SoleProbe : IDisposable
    {
        readonly SkinnedMeshRenderer[] skins;
        readonly Mesh[] meshes;
        readonly int[][] left, right;
        readonly List<Vector3> l = new List<Vector3>(), r = new List<Vector3>();
        public SoleProbe(SkinnedMeshRenderer[] skins, Transform lf, Transform rf)
        {
            this.skins = skins; meshes = skins.Select(s => new Mesh()).ToArray();
            left = skins.Select(s => Indices(s, lf)).ToArray(); right = skins.Select(s => Indices(s, rf)).ToArray();
        }
        static int[] Indices(SkinnedMeshRenderer skin, Transform foot)
        {
            var bones = skin.bones; var weights = skin.sharedMesh.boneWeights; var result = new List<int>();
            bool Match(int i) => i >= 0 && i < bones.Length && bones[i] && (bones[i] == foot || bones[i].IsChildOf(foot));
            for (int i = 0; i < weights.Length; i++)
            {
                var w = weights[i]; float sum = (Match(w.boneIndex0) ? w.weight0 : 0) + (Match(w.boneIndex1) ? w.weight1 : 0) + (Match(w.boneIndex2) ? w.weight2 : 0) + (Match(w.boneIndex3) ? w.weight3 : 0);
                if (sum > .35f) result.Add(i);
            }
            return result.ToArray();
        }
        public void Bake()
        {
            l.Clear(); r.Clear();
            for (int i = 0; i < skins.Length; i++)
            {
                skins[i].BakeMesh(meshes[i]); var vertices = meshes[i].vertices;
                foreach (int j in left[i]) l.Add(skins[i].transform.TransformPoint(vertices[j]));
                foreach (int j in right[i]) r.Add(skins[i].transform.TransformPoint(vertices[j]));
            }
            if (l.Count == 0 || r.Count == 0) throw new InvalidOperationException("Cannot identify actual foot-weighted skin vertices.");
        }
        public float Minimum(bool isLeft) => (isLeft ? l : r).Min(p => p.y);
        public Vector3 FrontContact(bool isLeft)
        {
            var list = isLeft ? l : r; float min = list.Min(p => p.y);
            Vector3 p = list.Where(v => v.y <= min + .012f).OrderByDescending(v => v.z).First(); p.y = min; return p;
        }
        public void Dispose() { foreach (var mesh in meshes) Object.DestroyImmediate(mesh); }
    }
    sealed class CpuSkin : IDisposable
    {
        readonly SkinnedMeshRenderer source;
        readonly GameObject obj;
        readonly Mesh mesh;
        readonly bool enabled;
        public readonly MeshRenderer renderer;
        public CpuSkin(SkinnedMeshRenderer source)
        {
            this.source = source; enabled = source.enabled;
            obj = new GameObject("Disposable CPU Skin Snapshot", typeof(MeshFilter), typeof(MeshRenderer));
            SceneManager.MoveGameObjectToScene(obj, source.gameObject.scene); obj.transform.SetParent(source.transform, false);
            mesh = new Mesh(); mesh.MarkDynamic(); obj.GetComponent<MeshFilter>().sharedMesh = mesh;
            renderer = obj.GetComponent<MeshRenderer>(); renderer.sharedMaterials = source.sharedMaterials;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; source.enabled = false;
        }
        public void Bake() { source.BakeMesh(mesh); mesh.RecalculateBounds(); mesh.UploadMeshData(false); }
        public void Dispose() { if (source) source.enabled = enabled; Object.DestroyImmediate(obj); Object.DestroyImmediate(mesh); }
    }
    sealed class Stage : IDisposable
    {
        public readonly Scene scene;
        public readonly Camera camera;
        readonly Scene previous;
        readonly RenderTexture target, previousTarget;
        readonly Texture2D pixels;
        readonly Material floorMaterial;
        readonly bool asyncCompilation;
        public Stage()
        {
            previous = SceneManager.GetActiveScene(); previousTarget = RenderTexture.active;
            asyncCompilation = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
            scene = EditorSceneManager.NewPreviewScene(); camera = New("Elastic Direction Review Camera").AddComponent<Camera>();
            camera.scene = scene; camera.orthographic = true; camera.aspect = 1;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.22f, .25f, .29f);
            camera.nearClipPlane = .02f; camera.farClipPlane = 50; camera.allowHDR = false;
            target = new RenderTexture(ImageSize, ImageSize, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            target.Create(); camera.targetTexture = target;
            pixels = new Texture2D(ImageSize, ImageSize, TextureFormat.RGB24, false);
            floorMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            floorMaterial.SetColor("_BaseColor", new Color(.16f, .185f, .215f));
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); SceneManager.MoveGameObjectToScene(floor, scene);
            Object.DestroyImmediate(floor.GetComponent<Collider>()); floor.transform.localScale = new Vector3(.8f, 1, .6f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = floorMaterial;
        }
        public GameObject New(string name) { var o = new GameObject(name); SceneManager.MoveGameObjectToScene(o, scene); return o; }
        public void Frame(Bounds bounds, Vector3 center, Vector3 offset)
        {
            SetView(center, offset, .80f); float maxX = 0, maxY = 0;
            foreach (int x in new[] { -1, 1 }) foreach (int y in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
            {
                Vector3 d = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z)) - center;
                maxX = Mathf.Max(maxX, Mathf.Abs(Vector3.Dot(d, camera.transform.right))); maxY = Mathf.Max(maxY, Mathf.Abs(Vector3.Dot(d, camera.transform.up)));
            }
            camera.orthographicSize = Mathf.Max(.80f, maxX * 1.15f, maxY * 1.15f);
        }
        public void SetView(Vector3 center, Vector3 offset, float size)
        { camera.transform.position = center + offset; camera.transform.LookAt(center); camera.orthographicSize = size; }
        public void Save(string path)
        {
            camera.Render(); camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, ImageSize, ImageSize), 0, 0); pixels.Apply(false, false); File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        public void Dispose()
        {
            RenderTexture.active = previousTarget; camera.targetTexture = null;
            Object.DestroyImmediate(target); Object.DestroyImmediate(pixels); Object.DestroyImmediate(floorMaterial);
            EditorSceneManager.ClosePreviewScene(scene); if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            ShaderUtil.allowAsyncCompilation = asyncCompilation;
        }
    }
}
#endif
