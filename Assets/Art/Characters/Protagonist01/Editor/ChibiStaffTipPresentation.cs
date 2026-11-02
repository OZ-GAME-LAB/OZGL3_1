#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class ChibiMotionSet
{
    const string TipStudyDir = "Assets/Art/Characters/Protagonist01/AnimationStudies/StaffTipBolt20261102/";
    const string OlderGripDir = "Assets/Art/Characters/Protagonist01/AnimationStudies/SkeletonMageCast20261030/";
    const string BodyTake = TipStudyDir + "Study_StaffTipBolt_Chibi.anim";
    const string IdleTake = TipStudyDir + "Study_StaffTipIdle_Chibi.anim";
    const string ShotTake = TipStudyDir + "Study_StaffTipBolt_FX.anim";
    const string ReviewController = TipStudyDir + "StaffTipBolt_Review.controller";
    const string ReviewActor = TipStudyDir + "Player_Chibi_StaffTipBolt.prefab";
    const float TipRelease = 26f / 60f;
    const float BoltSpeed = 3.5f;

    [MenuItem("Project M/Studies/Build and capture staff tip bolt")]
    public static void BuildStaffTipPresentation()
    {
        StaffTipBoltStudy.Build();
        BuildTipPrefab();
        CaptureStaffAimStudy();
        VerifyTipController();
        StaffTipBoltVerification.Run();
        Debug.Log("STAFF_TIP_PRESENTATION_READY");
    }

    // The visual variant keeps the gameplay model and skeleton as its prefab source.
    // Its closed gripping hand matches the diameter and axis of the project's crystal staff.
    static void BuildTipPrefab()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(BodyTake);
        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleTake);
        if (!clip || !idle) throw new FileNotFoundException("Missing the staff idle or cast take.");
        using (var stage = new Stage())
        {
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayChibi);
            var actor = (GameObject)PrefabUtility.InstantiatePrefab(original, stage.scene);
            try
            {
                var skin = actor.GetComponentInChildren<SkinnedMeshRenderer>(true);
                skin.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(OlderGripDir + "Study_Mage_StaffGrip.asset");
                skin.sharedMesh = MageStudyClosedHandAssembler.SaveStaffTipRightFist(skin, TipStudyDir + "Study_StaffGrip_RightFist.asset");
                skin.sharedMaterials = new[] { skin.sharedMaterial,
                    AssetDatabase.LoadAssetAtPath<Material>(OlderGripDir + "Study_Hand.mat"),
                    AssetDatabase.LoadAssetAtPath<Material>(OlderGripDir + "Study_Cuff_Edge.mat"),
                    AssetDatabase.LoadAssetAtPath<Material>(TipStudyDir + "Study_RightFist.mat") };
                if (skin.sharedMesh.subMeshCount != skin.sharedMaterials.Length || skin.sharedMaterials.Any(m => !m))
                    throw new InvalidOperationException("Equipped skin is missing a submesh or material slot.");
                var staff = StaffTipBoltStudy.AttachStaff(actor);
                DisableGameplayForPreview(actor);
                actor.name = "Player_Chibi_StaffTipBolt";
                actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.LookRotation(Vector3.right));
                var animator = actor.GetComponent<Animator>();
                animator.runtimeAnimatorController = null;
                var tip = staff.transform.Find("EffectTip");
                clip.SampleAnimation(actor, TipRelease);
                Vector3 start = actor.transform.InverseTransformPoint(tip.position);
                Vector3 forward = actor.transform.InverseTransformDirection(staff.transform.up).normalized;
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ReviewController);
                if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(ReviewController);
                var bolt = new GameObject("PreviewBolt");
                bolt.transform.SetParent(actor.transform, false);
                bolt.transform.localPosition = start;
                bolt.transform.localRotation = Quaternion.LookRotation(forward, Vector3.up);
                bolt.transform.localScale = Vector3.zero;
                AddBoltShape(bolt.transform, controller, "Tail", -.037f, new Vector3(.038f,.038f,.125f), new Color(.04f,.65f,.64f));
                AddBoltShape(bolt.transform, controller, "Core", .019f, new Vector3(.053f,.053f,.075f), new Color(.47f,1f,.93f));
                var shot = BuildTipFX(start, forward, clip.length);
                SetTipStates(controller, idle, clip, shot);
                idle.SampleAnimation(actor, 0f);
                animator.enabled = true;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.runtimeAnimatorController = controller;
                foreach (var t in actor.GetComponentsInChildren<Transform>(true))
                    if (PrefabUtility.IsPartOfPrefabInstance(t)) PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                foreach (var component in new Component[]{actor.transform, skin, animator, actor.GetComponent<Rigidbody>()})
                    if (component) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                foreach (var collider in actor.GetComponentsInChildren<Collider>(true))
                    if (PrefabUtility.IsPartOfPrefabInstance(collider)) PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
                PrefabUtility.RecordPrefabInstancePropertyModifications(actor);
                var view = PrefabUtility.SaveAsPrefabAsset(actor, ReviewActor, out bool saved);
                if (!saved || !view) throw new InvalidOperationException("Failed to save equipped staff preview.");
                AssetDatabase.SaveAssets();
            }
            finally { Object.DestroyImmediate(actor); }
        }
    }

    static void AddBoltShape(Transform parent, AnimatorController owner, string name, float offset, Vector3 size, Color color)
    {
        string materialName = "Staff bolt " + name;
        var mat = AssetDatabase.LoadAllAssetsAtPath(ReviewController).OfType<Material>().FirstOrDefault(m => m.name == materialName);
        if (!mat)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = materialName };
            AssetDatabase.AddObjectToAsset(mat, owner);
        }
        mat.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(mat);
        var shape = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(shape.GetComponent<Collider>());
        shape.name = name;
        shape.transform.SetParent(parent, false);
        shape.transform.localPosition = Vector3.forward * offset;
        shape.transform.localScale = size;
        var renderer = shape.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    static AnimationClip BuildTipFX(Vector3 start, Vector3 direction, float duration)
    {
        var shot = AssetDatabase.LoadAssetAtPath<AnimationClip>(ShotTake);
        if (!shot) { shot = new AnimationClip(); AssetDatabase.CreateAsset(shot, ShotTake); }
        shot.ClearCurves();
        shot.frameRate = 60;
        Vector3 end = start + direction * ((duration - TipRelease) * BoltSpeed);
        string[] axes = { "x", "y", "z" };
        for (int axis = 0; axis < axes.Length; axis++)
        {
            var position = AnimationCurve.Linear(TipRelease, start[axis], duration, end[axis]);
            shot.SetCurve("PreviewBolt", typeof(Transform), "m_LocalPosition." + axes[axis], position);
            var on = new AnimationCurve(new Keyframe(0,0), new Keyframe(TipRelease,1), new Keyframe(duration,0));
            for (int i = 0; i < on.length; i++)
                AnimationUtility.SetKeyRightTangentMode(on, i, AnimationUtility.TangentMode.Constant);
            shot.SetCurve("PreviewBolt", typeof(Transform), "m_LocalScale." + axes[axis], on);
        }
        EditorUtility.SetDirty(shot);
        return shot;
    }

    static void SetTipStates(AnimatorController controller, AnimationClip idle, AnimationClip cast, AnimationClip shot)
    {
        if (!controller.parameters.Any(p => p.name == "Cast"))
            controller.AddParameter("Cast", AnimatorControllerParameterType.Trigger);
        if (controller.layers.Length == 1) controller.AddLayer("Bolt from tip");
        var layers = controller.layers;
        var machine = layers[0].stateMachine;
        var castState = machine.states.Select(c => c.state).FirstOrDefault(s => s.name == "Cast" || s.name == "Staff cast");
        if (!castState) castState = machine.AddState("Cast", new Vector3(490f, 80f));
        castState.name = "Cast";
        var idleState = machine.states.Select(c => c.state).FirstOrDefault(s => s.name == "Idle");
        if (!idleState) idleState = machine.AddState("Idle", new Vector3(260f, 150f));
        machine.defaultState = idleState;
        idleState.motion = idle;
        castState.motion = cast;
        idleState.writeDefaultValues = castState.writeDefaultValues = false;
        foreach (var legacy in castState.transitions.Where(t => t.destinationState == castState).ToArray())
            castState.RemoveTransition(legacy);
        var enter = idleState.transitions.FirstOrDefault(t => t.destinationState == castState);
        if (!enter)
        {
            enter = idleState.AddTransition(castState);
            enter.AddCondition(AnimatorConditionMode.If, 0f, "Cast");
        }
        enter.hasExitTime = false;
        enter.hasFixedDuration = true;
        enter.duration = .10f;
        var recover = castState.transitions.FirstOrDefault(t => t.destinationState == idleState);
        if (!recover) recover = castState.AddTransition(idleState);
        recover.hasExitTime = true;
        recover.exitTime = 1f;
        recover.hasFixedDuration = true;
        recover.duration = .10f;

        // Synchronized layers share one state clock, so only the cast can fire the bolt.
        var hidden = AssetDatabase.LoadAllAssetsAtPath(ReviewController).OfType<AnimationClip>()
            .FirstOrDefault(a => a.name == "Bolt hidden in idle");
        if (!hidden)
        {
            hidden = new AnimationClip { name = "Bolt hidden in idle", frameRate = idle.frameRate };
            AssetDatabase.AddObjectToAsset(hidden, controller);
        }
        foreach (char axis in "xyz")
            hidden.SetCurve("PreviewBolt", typeof(Transform), "m_LocalScale." + axis, AnimationCurve.Constant(0f, idle.length, 0f));
        layers[1].defaultWeight = 1f;
        var previousFxMachine = layers[1].stateMachine;
        if (layers[1].syncedLayerIndex < 0 && previousFxMachine && previousFxMachine != machine)
            foreach (var legacy in previousFxMachine.states) previousFxMachine.RemoveState(legacy.state);
        layers[1].syncedLayerIndex = 0;
        layers[1].syncedLayerAffectsTiming = false;
        layers[1].SetOverrideMotion(idleState, hidden);
        layers[1].SetOverrideMotion(castState, shot);
        controller.layers = layers;
        EditorUtility.SetDirty(hidden);
        EditorUtility.SetDirty(controller);
    }

    // Exercise two different trigger phases and the return to a freely looping idle.
    static void VerifyTipController()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ReviewActor);
        var source = PrefabUtility.GetCorrespondingObjectFromSource(prefab);
        if (AssetDatabase.GetAssetPath(source) != GameplayChibi)
            throw new InvalidOperationException("Review actor lost its Player_Chibi source.");
        using (var stage = new Stage())
        {
            foreach (int facing in new[]{1,-1})
            {
                var actor = Object.Instantiate(prefab);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(actor, stage.scene);
                try
                {
                    actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.LookRotation(Vector3.right * facing));
                    var animator = actor.GetComponent<Animator>();
                    var bolt = actor.transform.Find("PreviewBolt");
                    var tip = actor.GetComponentsInChildren<Transform>(true).Single(t => t.name == "EffectTip");
                    var wrist = actor.GetComponentsInChildren<Transform>(true).Single(t => t.name == "RightHand");
                    var shoulder = actor.GetComponentsInChildren<Transform>(true).Single(t => t.name == "RightArm");
                    animator.Rebind();
                    animator.Update(0);
                    if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash != Animator.StringToHash("Idle"))
                        throw new InvalidOperationException("Equipped caster must start in Idle.");
                    int launches = 0;
                    int lastTrigger = -1000;
                    bool visible = false;
                    Vector3 last = default;
                    float minIdle = float.PositiveInfinity, maxIdle = float.NegativeInfinity;
                    float maxTransitionWristStep = 0f;
                    Vector3 lastWrist = wrist.position;
                    for (int step = 0; step < 1030; step++)
                    {
                        // Wait longer than one idle cycle before firing. Trigger the
                        // second shot at another point in the recovered idle phase.
                        if (step == 383 || step == 818)
                        {
                            if (animator.IsInTransition(0) || animator.GetCurrentAnimatorStateInfo(0).shortNameHash != Animator.StringToHash("Idle"))
                                throw new InvalidOperationException("Idle did not settle before the next cast.");
                            animator.SetTrigger("Cast");
                            lastTrigger = step;
                        }
                        animator.Update(1f/120f);
                        if (step < 383)
                        {
                            float depth = Vector3.Dot(wrist.position - shoulder.position, actor.transform.forward);
                            minIdle = Mathf.Min(minIdle, depth);
                            maxIdle = Mathf.Max(maxIdle, depth);
                        }
                        if (animator.IsInTransition(0))
                            maxTransitionWristStep = Mathf.Max(maxTransitionWristStep, Vector3.Distance(wrist.position, lastWrist));
                        lastWrist = wrist.position;
                        bool now = bolt.localScale.x > .5f;
                        if (now && !visible)
                        {
                            float elapsed = (step - lastTrigger) / 120f;
                            if (Mathf.Abs(elapsed - TipRelease) > .035f)
                                throw new InvalidOperationException("Unexpected release time after Cast trigger: " + elapsed);
                            if (Vector3.Distance(bolt.position, tip.position) > .065f ||
                                Vector3.Dot(tip.parent.up, Vector3.right * facing) < .95f)
                                throw new InvalidOperationException("Live Animator bolt and staff tip misaligned at release.");
                            launches++;
                        }
                        if (now && visible && Vector3.Dot(bolt.position - last, Vector3.right * facing) < -.0002f)
                            throw new InvalidOperationException("Bolt reversed in flight.");
                        last = bolt.position;
                        visible = now;
                    }
                    if (launches != 2 || maxIdle - minIdle < .03f || maxTransitionWristStep > .035f ||
                        animator.GetCurrentAnimatorStateInfo(0).shortNameHash != Animator.StringToHash("Idle"))
                        throw new InvalidOperationException($"Idle/cast playback mismatch: {launches} shots, {maxIdle-minIdle:F4}m idle swing, {maxTransitionWristStep:F4}m blend step.");
                    Debug.Log($"STAFF_TIP_LIVE_PLAYBACK_OK facing={facing} casts={launches} idleSwingMetres={maxIdle-minIdle:F4} maxBlendStep={maxTransitionWristStep:F4}");
                }
                finally { Object.DestroyImmediate(actor); }
            }
        }
    }
}
#endif
