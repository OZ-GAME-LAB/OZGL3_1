using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

/// <summary>Rebuilds the imported character, controller and a playable isolated scene.</summary>
public static class ProtagonistUnitySetup
{
    public const string Folder = "Assets/Art/Characters/Protagonist01/Runtime";
    public const string VisualPath = Folder + "/Protagonist01_Visual.prefab";
    public const string PlayerPath = Folder + "/Protagonist01_Player.prefab";
    public const string ScenePath = Folder + "/Protagonist01_Playground.unity";
    private const string ControllerPath = Folder + "/Protagonist01_Unarmed.controller";
    private const string SourceFolder = "ArtSources/Characters/Protagonist01/v002-rigged";

    [Serializable] private class ClipReport
    {
        public string name;
        public float seconds;
        public bool humanMotion;
        public bool loop;
        public float maximumVertexDisplacement;
    }

    [Serializable] private class Report
    {
        public bool success;
        public string unity;
        public bool humanoidAvatarValid;
        public int skinnedRenderers;
        public int weightedBones;
        public int triangles;
        public bool rootMotion;
        public bool playerInputBound;
        public bool groundedLayerBound;
        public bool weaponSocketPresent;
        public string[] animatorChecks;
        public List<ClipReport> clips = new List<ClipReport>();
        public string playablePrefab;
        public string previewScene;
        public string notes;
    }

    [MenuItem("Tools/Project M/Build Protagonist 01")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        try
        {
            Directory.CreateDirectory(SourceFolder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ProtagonistAssetImport.Import();
            var clips = LoadClips();
            AnimatorController controller = BuildController(clips);
            Material material = BuildMaterial();
            BuildVisual(controller, material);
            BuildPlayer(clips["Punch"], controller);
            BuildScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateAndRender(clips);
            Debug.Log("PROTAGONIST_SETUP_SUCCESS " + PlayerPath);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else throw;
        }
    }

    private static Dictionary<string, AnimationClip> LoadClips()
    {
        var clips = new Dictionary<string, AnimationClip>();
        foreach (string path in new[] { "/Protagonist01_Walk.fbx", "/Protagonist01_Run.fbx", "/Protagonist01_Actions.fbx" })
        {
            foreach (AnimationClip clip in AssetDatabase.LoadAllAssetsAtPath(Folder + path).OfType<AnimationClip>())
                if (!clip.name.StartsWith("__preview__", StringComparison.Ordinal)) clips[clip.name] = clip;
        }
        foreach (string name in new[] { "Idle", "Walk", "Run", "Punch", "JumpFull", "JumpTakeoff", "AirApex", "Land" })
            if (!clips.ContainsKey(name)) throw new InvalidOperationException("Missing imported clip: " + name + ". Available: " + string.Join(", ", clips.Keys));
        return clips;
    }

    private static AnimatorController BuildController(Dictionary<string, AnimationClip> clips)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        foreach (var transition in machine.anyStateTransitions) machine.RemoveAnyStateTransition(transition);
        foreach (var state in machine.states) machine.RemoveState(state.state);
        controller.parameters = Array.Empty<AnimatorControllerParameter>();
        foreach (string name in new[] { "Walk", "Air", "Attacking", "IsDashing", "IsDead" }) controller.AddParameter(name, AnimatorControllerParameterType.Bool);
        foreach (string name in new[] { "Jump", "Land", "Attack", "Skill", "Hit", "Dead" }) controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
        controller.AddParameter("VelocityY", AnimatorControllerParameterType.Float);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter(new AnimatorControllerParameter { name = "AttackSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });

        var states = new Dictionary<string, AnimatorState>();
        AnimatorState Add(string name, Motion motion, int index)
        {
            AnimatorState state = machine.AddState(name, new Vector3(260 + index % 3 * 260, 80 + index / 3 * 110, 0));
            state.motion = motion;
            state.writeDefaultValues = true;
            states[name] = state;
            return state;
        }

        Add("Idle", clips["Idle"], 0);
        var locomotion = new BlendTree { name = "Walk Run", blendType = BlendTreeType.Simple1D, blendParameter = "Speed", useAutomaticThresholds = false };
        AssetDatabase.AddObjectToAsset(locomotion, controller);
        locomotion.AddChild(clips["Walk"], 2.2f);
        locomotion.AddChild(clips["Run"], 5f);
        Add("Locomotion", locomotion, 1);
        Add("Jump", clips["JumpTakeoff"], 2).speed = clips["JumpTakeoff"].length / 0.16f;
        Add("Air", clips["AirApex"], 3);
        Add("Land", clips["Land"], 4).speed = clips["Land"].length / 0.1f;
        var attack = Add("Attack", clips["Punch"], 5);
        attack.speedParameter = "AttackSpeed";
        attack.speedParameterActive = true;
        foreach (string name in new[] { "Dash", "Hit", "Dead" })
        {
            AnimationClip fallback = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Anim/Clips/" + name + ".anim");
            Add(name, fallback != null ? fallback : clips["Idle"], states.Count);
        }
        states["Dash"].speed = states["Dash"].motion.averageDuration / 0.18f;
        states["Hit"].speed = states["Hit"].motion.averageDuration / 0.3f;
        Add("Skill", clips["Idle"], states.Count);
        machine.defaultState = states["Idle"];

        AnimatorStateTransition Link(string from, string to, float duration = 0.04f)
        {
            var transition = states[from].AddTransition(states[to]);
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = duration;
            transition.canTransitionToSelf = false;
            return transition;
        }
        void Condition(AnimatorStateTransition transition, string parameter, bool value)
        {
            transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
        }
        void GroundReturn(string from)
        {
            var air = Link(from, "Air"); Condition(air, "Air", true);
            if (from == "Attack") Condition(air, "Attacking", false);
            if (from == "Dash") Condition(air, "IsDashing", false);
            var idle = Link(from, "Idle"); Condition(idle, "Air", false);
            if (from == "Attack") Condition(idle, "Attacking", false);
            else if (from == "Dash") Condition(idle, "IsDashing", false);
            else { idle.hasExitTime = true; idle.exitTime = 1f; }
        }

        Condition(Link("Idle", "Locomotion"), "Walk", true);
        Condition(Link("Locomotion", "Idle"), "Walk", false);
        foreach (string from in new[] { "Idle", "Locomotion" }) Condition(Link(from, "Air"), "Air", true);
        var takeoff = Link("Jump", "Air"); takeoff.hasExitTime = true; takeoff.exitTime = 0.85f; Condition(takeoff, "Air", true);
        var failedJump = Link("Jump", "Idle"); failedJump.hasExitTime = true; failedJump.exitTime = 1f; Condition(failedJump, "Air", false);
        Condition(Link("Air", "Idle"), "Air", false);
        foreach (string from in new[] { "Land", "Attack", "Dash", "Hit", "Skill" }) GroundReturn(from);
        Condition(Link("Dead", "Idle", 0.05f), "IsDead", false);

        // Priority is death, hurt, dodge, attack, jump/land, then skill. No damage events.
        foreach (string name in new[] { "Dead", "Hit", "Dash", "Attack", "Jump", "Land", "Skill" })
        {
            var transition = machine.AddAnyStateTransition(states[name]);
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = name == "Attack" ? 0.015f : 0.025f;
            transition.canTransitionToSelf = false;
            if (name == "Dash") Condition(transition, "IsDashing", true);
            else transition.AddCondition(AnimatorConditionMode.If, 0f, name);
            if (name != "Dead") Condition(transition, "IsDead", false);
        }
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static Material BuildMaterial()
    {
        const string path = Folder + "/Protagonist01_Unlit.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) throw new InvalidOperationException("URP Unlit shader missing");
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.shader = shader;
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Characters/Protagonist01/protagonist01-v001_base_color.png");
        if (texture == null) throw new InvalidOperationException("Character base color missing");
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void BuildVisual(AnimatorController controller, Material material)
    {
        GameObject imported = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Protagonist01_Rig.fbx");
        var visual = Object.Instantiate(imported);
        try
        {
            visual.name = "Protagonist01_Visual";
            visual.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Animator animator = visual.GetComponent<Animator>() ?? visual.AddComponent<Animator>();
            animator.avatar = AssetDatabase.LoadAllAssetsAtPath(Folder + "/Protagonist01_Rig.fbx").OfType<Avatar>().First();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                renderer.updateWhenOffscreen = true;
            }
            foreach (string side in new[] { "RightHand", "LeftHand" })
            {
                Transform hand = visual.GetComponentsInChildren<Transform>().First(t => t.name == side);
                new GameObject(side == "RightHand" ? "WeaponSocket" : "WeaponSocket_Left").transform.SetParent(hand, false);
            }
            PrefabUtility.SaveAsPrefabAsset(visual, VisualPath);
        }
        finally { Object.DestroyImmediate(visual); }
    }

    private static void BuildPlayer(AnimationClip punch, AnimatorController controller)
    {
        GameObject root = PrefabUtility.LoadPrefabContents("Assets/2.Prefab/Player/Player.prefab");
        try
        {
            root.name = "Protagonist01_Player";
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (Transform child in root.transform.Cast<Transform>().ToArray())
                if (child.name != "GroundCheck") Object.DestroyImmediate(child.gameObject);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath), root.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.GetComponent<Animator>().enabled = false;
            Animator animator = root.GetComponent<Animator>();
            animator.avatar = visual.GetComponent<Animator>().avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            InputManager input = root.GetComponent<InputManager>() ?? root.AddComponent<InputManager>();
            InputManager sourceInput = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/2.Prefab/Player/InputManager.prefab").GetComponent<InputManager>();
            EditorUtility.CopySerialized(sourceInput, input);
            var playerProperties = new SerializedObject(root.GetComponent<PlayerController>());
            playerProperties.FindProperty("_inputManager").objectReferenceValue = input;
            playerProperties.FindProperty("_groundLayer").intValue = 1 << 6;
            playerProperties.ApplyModifiedPropertiesWithoutUndo();
            CapsuleCollider capsule = root.GetComponent<CapsuleCollider>();
            capsule.height = 1.65f;
            capsule.radius = 0.29f;
            capsule.center = new Vector3(0, 0.825f, 0);
            root.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
            var driver = root.GetComponent<ProtagonistAnimationDriver>() ?? root.AddComponent<ProtagonistAnimationDriver>();
            driver.Configure(punch);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void BuildScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.layer = 6;
        ground.transform.position = new Vector3(0,-0.25f,0);
        ground.transform.localScale = new Vector3(30,0.5f,5);
        var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath));
        player.transform.position = new Vector3(0,0.02f,0);
        player.transform.rotation = Quaternion.LookRotation(Vector3.right);
        var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0,3.3f,-11);
        camera.transform.LookAt(new Vector3(0,1.3f,0));
        camera.orthographic = true;
        camera.orthographicSize = 4f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.14f,0.17f,0.21f);
        camera.gameObject.AddComponent<ProtagonistPlaygroundCamera>().Configure(player.transform);
        var light = new GameObject("Directional Light",typeof(Light)).GetComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(50,-35,0);
        light.intensity = 1.3f;
        var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        target.name = "Punch Target (20 damage)";
        target.layer = 7;
        target.transform.position = new Vector3(3,0.8f,0);
        target.transform.localScale = new Vector3(0.65f,1.6f,0.65f);
        target.AddComponent<TestDummy>();
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    private static void ValidateAndRender(Dictionary<string, AnimationClip> clips)
    {
        var report = new Report { unity = Application.unityVersion, playablePrefab = PlayerPath, previewScene = ScenePath };
        // Exercise the actual gameplay hierarchy, including its nested skeleton.
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
        GameObject instance = Object.Instantiate(prefab);
        try
        {
            Animator animator = instance.GetComponent<Animator>();
            report.humanoidAvatarValid = animator.avatar != null && animator.avatar.isHuman && animator.avatar.isValid;
            if (!report.humanoidAvatarValid) throw new InvalidOperationException("Invalid Humanoid avatar");
            report.rootMotion = animator.applyRootMotion;
            var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>();
            report.skinnedRenderers = renderers.Length;
            report.weightedBones = renderers.Sum(r => r.bones.Count(b => b != null));
            report.triangles = renderers.Sum(r => r.sharedMesh.triangles.Length / 3);
            report.weaponSocketPresent = instance.GetComponentsInChildren<Transform>().Any(t => t.name == "WeaponSocket");
            animator.Rebind();
            animator.Update(0);
            foreach (string name in new[] { "Idle", "Walk", "Run", "Punch", "JumpFull", "JumpTakeoff", "AirApex", "Land" })
            {
                AnimationClip clip = clips[name];
                if (!clip.humanMotion || clip.length <= 0) throw new InvalidOperationException("Invalid humanoid animation: " + name);
                Mesh first = new Mesh(); Mesh second = new Mesh();
                clip.SampleAnimation(instance, clip.length * 0.15f);
                renderers[0].BakeMesh(first);
                clip.SampleAnimation(instance, clip.length * 0.65f);
                renderers[0].BakeMesh(second);
                var a = first.vertices; var b = second.vertices;
                float movement = 0;
                for (int i = 0; i < a.Length; i++) movement = Mathf.Max(movement, Vector3.Distance(a[i],b[i]));
                Object.DestroyImmediate(first); Object.DestroyImmediate(second);
                report.clips.Add(new ClipReport { name = name, seconds = clip.length, humanMotion = clip.humanMotion, loop = clip.isLooping, maximumVertexDisplacement = movement });
                if (movement < 0.00001f) throw new InvalidOperationException("No skin deformation in clip: " + name);
            }
            animator.Rebind(); animator.Update(0);
            var checks = new List<string>();
            void Check(string expected)
            {
                bool matches = false;
                for (int step = 0; step < 12; step++)
                {
                    animator.Update(0.02f);
                    if (animator.GetCurrentAnimatorStateInfo(0).IsName(expected)) { matches = true; break; }
                }
                if (!matches) throw new InvalidOperationException("Animator did not reach " + expected +
                    "; current=" + animator.GetCurrentAnimatorStateInfo(0).fullPathHash +
                    ", next=" + animator.GetNextAnimatorStateInfo(0).fullPathHash +
                    ", normalized=" + animator.GetCurrentAnimatorStateInfo(0).normalizedTime +
                    ", Air=" + animator.GetBool("Air"));
                checks.Add(expected);
            }
            Check("Idle");
            animator.SetBool("Walk",true); animator.SetFloat("Speed",2.2f); Check("Locomotion");
            animator.SetFloat("Speed",5f); Check("Locomotion");
            animator.SetBool("Walk",false); animator.SetBool("Attacking",true); animator.SetTrigger("Attack"); Check("Attack");
            animator.SetBool("Attacking",false); animator.Update(0.12f);
            animator.SetTrigger("Jump"); Check("Jump"); animator.SetBool("Air",true); animator.Update(0.3f); Check("Air");
            animator.SetBool("Air",false); animator.SetTrigger("Land"); Check("Land");
            report.animatorChecks = checks.ToArray();
            RenderSheet(instance, clips);
        }
        finally { Object.DestroyImmediate(instance); }
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
        var serialized = new SerializedObject(player.GetComponent<PlayerController>());
        report.playerInputBound = serialized.FindProperty("_inputManager").objectReferenceValue == player.GetComponent<InputManager>();
        report.groundedLayerBound = serialized.FindProperty("_groundLayer").intValue == 64;
        if (!report.playerInputBound || !report.groundedLayerBound) throw new InvalidOperationException("Player input or ground mask not bound");
        report.success = true;
        report.notes = "Meshy rig/walk/run plus Idle, left jab and regular jump. In-place controller; physics drives position. Jump split into takeoff/apex/landing. Existing dash/hit/death clips reused. Shift remains dash. Weapons attach to hand sockets; no weapon shop logic changed.";
        File.WriteAllText(SourceFolder + "/unity-validation.json", JsonUtility.ToJson(report,true));
        Debug.Log(JsonUtility.ToJson(report,true));
    }

    private static void RenderSheet(GameObject instance, Dictionary<string, AnimationClip> clips)
    {
        Animator animator = instance.GetComponent<Animator>();
        animator.enabled = true;
        animator.runtimeAnimatorController = null;
        var camera = new GameObject("Protagonist Capture",typeof(Camera)).GetComponent<Camera>();
        var target = new RenderTexture(480,640,24);
        var sheet = new Texture2D(2400,640,TextureFormat.RGB24,false);
        try
        {
            instance.transform.position = new Vector3(0,100,0);
            instance.transform.rotation = Quaternion.Euler(0,155,0);
            camera.transform.position = new Vector3(0,100.9f,-4);
            camera.transform.LookAt(new Vector3(0,100.85f,0));
            camera.orthographic = true;
            camera.orthographicSize = 1.08f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.88f,0.9f,0.92f);
            camera.targetTexture = target;
            string[] names = { "Idle", "Walk", "Run", "Punch", "JumpFull" };
            for (int i=0;i<names.Length;i++)
            {
                animator.Rebind();
                PlayableGraph graph = PlayableGraph.Create("Protagonist pose capture");
                try
                {
                    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    var playable = AnimationClipPlayable.Create(graph,clips[names[i]]);
                    var output = AnimationPlayableOutput.Create(graph,"Pose",animator);
                    output.SetSourcePlayable(playable);
                    graph.Play();
                    float fraction = 0.45f;
                    if (names[i] == "Punch")
                    {
                        Transform hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                        Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
                        float furthest = float.NegativeInfinity;
                        for (int sample=0;sample<=52;sample++)
                        {
                            playable.SetTime(clips[names[i]].length*sample/52f);
                            graph.Evaluate(0);
                            float extension = Vector3.Dot(hand.position-chest.position,instance.transform.forward);
                            if (extension > furthest) { furthest=extension; fraction=sample/52f; }
                        }
                        File.WriteAllText(SourceFolder+"/punch-contact.txt","Maximum left hand forward extension at normalized time="+fraction+", meters="+furthest);
                    }
                    playable.SetTime(clips[names[i]].length*fraction);
                    graph.Evaluate(0);
                    instance.transform.position = new Vector3(0,100,0);
                    instance.transform.rotation = Quaternion.Euler(0,155,0);
                    Mesh baked = new Mesh();
                    SkinnedMeshRenderer renderer = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                    renderer.BakeMesh(baked);
                    Bounds posedBounds = new Bounds(renderer.transform.TransformPoint(baked.vertices[0]),Vector3.zero);
                    foreach (Vector3 vertex in baked.vertices) posedBounds.Encapsulate(renderer.transform.TransformPoint(vertex));
                    Object.DestroyImmediate(baked);
                    camera.transform.position = posedBounds.center + new Vector3(0,0.05f,-4f);
                    camera.transform.LookAt(posedBounds.center);
                    camera.Render();
                    RenderTexture.active = target;
                    sheet.ReadPixels(new Rect(0,0,480,640),i*480,0);
                }
                finally { graph.Destroy(); }
            }
            sheet.Apply();
            File.WriteAllBytes(SourceFolder + "/protagonist01-animation-poses.png",sheet.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.DestroyImmediate(camera.gameObject); Object.DestroyImmediate(target); Object.DestroyImmediate(sheet);
        }
    }

    public static void CapturePreviews()
    {
        GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath));
        try { RenderSheet(instance,LoadClips()); }
        finally { Object.DestroyImmediate(instance); }
    }

    public static void RefreshPlayground()
    {
        BuildScene();
        AssetDatabase.SaveAssets();
    }
}
