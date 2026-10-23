#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Reproducible game-axis review renders of the actual imported Humanoid and kit prefabs.</summary>
public static class SideviewReviewCapture
{
    const string Kit = "Assets/Art/Stage01Kit";
    const string Hero = "Assets/Art/Characters/Protagonist01/Runtime";
    const string Output = "ArtSources/Stage01Kit/SideviewReview";
    const int Width = 768, Height = 432, Fps = 16;
    const float Hold = .25f;
    // The camera remains on the -Z gameplay side. A small X offset reveals thickness without showing a 3/4 character.
    static readonly Vector3 CameraOffset = new Vector3(1.2f, 1.25f, -10f);

    sealed class MotionSpec
    {
        public string name, state, weapon, prop;
        public AnimationClip clip;
        public float duration, speed, commit;
        public int facing = 1;
        public bool jump, loop, utilityFixture;
    }
    [Serializable] sealed class FrameReport
    {
        public int frame;
        public float seconds, normalized, rootHeight, minimumFootHeight, minimumSkinHeight, velocityY, throwElapsed, visualGroundOffset;
        public string clip, state;
        public bool weaponVisible, propVisible, rightGrip, leftGrip, throwReleased, grounded;
        public Vector3 rootPosition, cameraPosition, cameraTarget, rightHand, leftHand, head, propPosition;
    }
    [Serializable] sealed class MotionReport
    {
        public string name, state, clip, weapon, prop, directory, durationSource;
        public float sourceSeconds, playbackSeconds, totalSeconds, maximumRootHeight, minimumSoleClearance;
        public int frames, fps, facing;
        public bool humanoid, previewOnlyProjectile;
        public string previewProjectileSemantics;
        public FrameReport[] samples;
    }
    [Serializable] sealed class ReviewReport
    {
        public string unity, method, camera, notes;
        public int width, height, fps;
        public float jumpVelocity, gravity, jumpFlightSeconds;
        public MotionReport[] motions;
    }
    [Serializable] sealed class EnvironmentShot
    {
        public string name, image;
        public float orthographicSize;
        public Vector3 cameraPosition, cameraTarget;
        public string[] prefabs;
    }
    [Serializable] sealed class EnvironmentReport
    {
        public string method;
        public EnvironmentShot[] shots;
    }

    // Camera.Render may retain an old edit-time GPU skin buffer despite correct Mecanim bones.
    // The review renders each exact CPU-baked pose through a disposable static renderer instead.
    sealed class SkinnedPoseSnapshot : IDisposable
    {
        public readonly SkinnedMeshRenderer source;
        public readonly Mesh mesh;
        readonly GameObject renderObject;
        readonly MeshRenderer renderer;
        readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        readonly bool wasEnabled;

        public SkinnedPoseSnapshot(SkinnedMeshRenderer source)
        {
            this.source = source;
            wasEnabled = source.enabled;
            mesh = new Mesh { name = source.name + "_DisposableReviewPose" };
            mesh.MarkDynamic();
            renderObject = new GameObject(source.name + "_BakedReviewSnapshot", typeof(MeshFilter), typeof(MeshRenderer));
            SceneManager.MoveGameObjectToScene(renderObject, source.gameObject.scene);
            renderObject.transform.SetParent(source.transform, false);
            renderObject.layer = source.gameObject.layer;
            renderObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            renderer = renderObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = source.sharedMaterials;
            renderer.shadowCastingMode = source.shadowCastingMode;
            renderer.receiveShadows = source.receiveShadows;
            renderer.lightProbeUsage = source.lightProbeUsage;
            renderer.reflectionProbeUsage = source.reflectionProbeUsage;
            renderer.probeAnchor = source.probeAnchor;
            renderer.renderingLayerMask = source.renderingLayerMask;
            renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = source.sortingOrder;
            renderer.enabled = wasEnabled;
            source.enabled = false;
        }
        public void Refresh()
        {
            // BakeMesh computes the current CPU skin even while its GPU renderer is disabled.
            source.BakeMesh(mesh);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            source.GetPropertyBlock(properties);
            renderer.SetPropertyBlock(properties);
        }
        public float MinimumWorldY()
        {
            float minimum = float.PositiveInfinity;
            foreach (var vertex in mesh.vertices)
                minimum = Mathf.Min(minimum, source.transform.TransformPoint(vertex).y);
            return minimum;
        }
        public void Dispose()
        {
            if (source) source.enabled = wasEnabled;
            if (renderObject) Object.DestroyImmediate(renderObject);
            if (mesh) Object.DestroyImmediate(mesh);
        }
    }

    sealed class CaptureWorld : IDisposable
    {
        public Scene previous, scene;
        public Camera camera;
        public RenderTexture target;
        public Texture2D pixels;
        readonly RenderTexture previousTarget;
        readonly bool previousAsync;
        readonly Color previousAmbient;
        readonly AmbientMode previousAmbientMode;
        readonly bool previousFog;
        readonly SphericalHarmonicsL2 previousProbe;
        readonly List<SkinnedPoseSnapshot> retainedSnapshots = new List<SkinnedPoseSnapshot>();
        public CaptureWorld(int width, int height)
        {
            previous = SceneManager.GetActiveScene();
            previousTarget = RenderTexture.active;
            previousAsync = ShaderUtil.allowAsyncCompilation;
            previousAmbient = RenderSettings.ambientLight;
            previousAmbientMode = RenderSettings.ambientMode;
            previousFog = RenderSettings.fog;
            previousProbe=RenderSettings.ambientProbe;
            ShaderUtil.allowAsyncCompilation = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.36f, .41f, .47f);
            var probe=new SphericalHarmonicsL2();probe.AddAmbientLight(RenderSettings.ambientLight);RenderSettings.ambientProbe=probe;
            RenderSettings.fog = false;
            scene = EditorSceneManager.NewPreviewScene();
            camera = NewObject("Sideview Review Camera").AddComponent<Camera>();
            camera.scene = scene;
            camera.orthographic = true;
            camera.nearClipPlane = .03f; camera.farClipPlane = 200;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.10f, .125f, .155f);
            camera.allowHDR = false; camera.allowMSAA = true;
            var light = NewObject("Cloudy Directional Key").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = .85f;
            light.color = new Color(.84f, .89f, .96f);
            light.transform.rotation = Quaternion.Euler(55, -35, 0);
            light.shadows = LightShadows.Soft;
            var fill = NewObject("Cloudy Soft Fill").AddComponent<Light>();
            fill.type = LightType.Directional; fill.intensity = .16f;
            fill.color = new Color(.60f, .68f, .76f);
            fill.transform.rotation = Quaternion.Euler(30, 150, 0);
            target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            target.Create(); camera.targetTexture = target;
            pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        }
        public GameObject NewObject(string name)
        {
            var obj = new GameObject(name);
            SceneManager.MoveGameObjectToScene(obj, scene);
            return obj;
        }
        public GameObject Place(string group, string name, Vector3 position, Transform parent = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + "/Prefabs/" + group + "/" + name + ".prefab");
            if (!prefab) throw new InvalidOperationException("Missing sideview review prefab " + group + "/" + name);
            var obj = Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(obj, scene);
            if (parent) obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            foreach (var collider in obj.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            return obj;
        }
        public void LookAt(Vector3 point, float orthographicSize)
        {
            camera.transform.position = point + CameraOffset;
            camera.transform.LookAt(point);
            camera.orthographicSize = orthographicSize;
        }
        public void Save(string path)
        {
            // Warm only this concrete variant. Global shader warmup can retain unrelated project variants.
            camera.Render(); camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply(false, false);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        public void RetainSnapshot(SkinnedPoseSnapshot snapshot) { retainedSnapshots.Add(snapshot); }
        public void ReleaseSnapshots()
        {
            foreach (var snapshot in retainedSnapshots) snapshot.Dispose();
            retainedSnapshots.Clear();
        }
        public void Dispose()
        {
            ReleaseSnapshots();
            RenderTexture.active = previousTarget;
            ShaderUtil.allowAsyncCompilation = previousAsync;
            RenderSettings.ambientMode = previousAmbientMode;
            RenderSettings.ambientLight = previousAmbient;
            RenderSettings.ambientProbe=previousProbe;
            RenderSettings.fog = previousFog;
            camera.targetTexture = null;
            Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            EditorSceneManager.ClosePreviewScene(scene);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    [MenuItem("Tools/Project M/Review/Render All Sideview Motion Frames")]
    public static void Capture()
    {
        Directory.CreateDirectory(Output);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Hero + "/Protagonist01_Player.prefab");
        if (!prefab) throw new InvalidOperationException("Player prefab missing.");
        var controller = prefab.GetComponent<Animator>().runtimeAnimatorController as AnimatorController;
        if (!controller) throw new InvalidOperationException("A root AnimatorController is required.");
        var states = AllStates(controller.layers[0].stateMachine).ToDictionary(s => s.name);
        var clips = new Dictionary<string, AnimationClip>();
        foreach (var state in states.Values)
            foreach (var clip in MotionClips(state.motion)) clips[clip.name] = clip;
        var player = prefab.GetComponent<PlayerController>();
        var playerProperties = new SerializedObject(player);
        float jumpVelocity = playerProperties.FindProperty("_jumpForce").floatValue;
        float gravity = Mathf.Max(.01f, -Physics.gravity.y);
        float flight = jumpVelocity * 2 / gravity;
        float punchDuration = prefab.GetComponent<IPlayerAttack>()?.Duration ?? player.AttackDuration;
        float takeoffDuration = ClipOf(states, "Jump").length / Mathf.Max(.01f, states["Jump"].speed);
        float landDuration = player.LandDuration;
        float slowWalk = .44f;
        var driver = prefab.GetComponent<ProtagonistAnimationDriver>();
        if (driver)
        {
            var slow = new SerializedObject(driver).FindProperty("_slowWalkMultiplier");
            if (slow != null) slowWalk = slow.floatValue;
        }
        var specs = new List<MotionSpec>();
        void Add(string name, string state, string clipName, float duration, string weapon = "", string prop = "", float speed = 0)
        {
            if (!clips.TryGetValue(clipName, out var clip)) throw new InvalidOperationException("Missing controller clip " + clipName);
            specs.Add(new MotionSpec { name = name, state = state, clip = clip, duration = duration > 0 ? duration : clip.length,
                weapon = weapon, prop = prop, speed = speed, commit = .5f });
        }
        Add("Idle", "Idle", ClipOf(states, "Idle").name, 0);
        specs.Last().loop = true;
        Add("Walk", "Locomotion", "Walk", clips["Walk"].length / ChildTimeScale(states["Locomotion"].motion, "Walk"), speed: player.WalkSpeed * slowWalk);
        specs.Last().loop = true;
        Add("Run", "Locomotion", "Run", clips["Run"].length / ChildTimeScale(states["Locomotion"].motion, "Run"), speed: player.WalkSpeed);
        specs.Last().loop = true;
        Add("Punch", "Attack", ClipOf(states, "Attack").name, punchDuration);
        specs.Add(new MotionSpec { name = "Jump", state = "Jump > Air > Land", clip = ClipOf(states, "Jump"),
            duration = flight + landDuration, speed = player.JumpSpeed, jump = true });
        Add("Land", "Land", ClipOf(states, "Land").name, landDuration);
        Add("Dash", "Dash", ClipOf(states, "Dash").name, player.DashDuration, speed: player.DashSpeed);
        Add("Hit", "Hit", ClipOf(states, "Hit").name, player.HitStunTime);
        Add("Dead", "Dead", ClipOf(states, "Dead").name, 0);
        var definitions = new SerializedObject(prefab.GetComponent<ProtagonistActionAnimator>()).FindProperty("_actions");
        for (int i = 0; i < definitions.arraySize; i++)
        {
            var definition = definitions.GetArrayElementAtIndex(i);
            string name = ((ProtagonistExtraAction)definition.FindPropertyRelative("action").enumValueIndex).ToString();
            var clip = definition.FindPropertyRelative("clip").objectReferenceValue as AnimationClip;
            if (!clip) throw new InvalidOperationException("Extra action clip missing " + name);
            string weapon = name == "SwordSlash" ? "Sword" : name == "MagicCast" ? "Staff" : "";
            specs.Add(new MotionSpec { name = name, state = name, clip = clip,
                duration = definition.FindPropertyRelative("duration").floatValue,
                commit = definition.FindPropertyRelative("commitNormalizedTime").floatValue,
                weapon = weapon, prop = name == "Drink" ? "Potion_Health" : name == "Throw" ? "Potion_Attack" : "" });
        }
        foreach (var spec in specs.Where(s => new[] { "Punch", "SwordSlash", "MagicCast", "Throw", "Drink", "Jump" }.Contains(s.name)).ToArray())
            specs.Add(new MotionSpec { name = spec.name + "_Left", state = spec.state, clip = spec.clip, duration = spec.duration,
                weapon = spec.weapon, prop = spec.prop, speed = spec.speed, commit = spec.commit, jump = spec.jump, loop = spec.loop, facing = -1 });
        foreach (string name in new[] { "Throw", "Interact", "Pickup" })
        {
            var source = specs.First(s => s.name == name);
            specs.Add(new MotionSpec { name = name + "_WithStaff", state = source.state, clip = source.clip, duration = source.duration,
                weapon = "Staff", prop = source.prop, commit = source.commit, utilityFixture = true });
        }
        foreach (string weapon in new[] { "Sword", "Staff" })
        {
            var source = specs.First(s => s.name == (weapon == "Sword" ? "SwordSlash" : "MagicCast"));
            foreach (int facing in new[] { 1, -1 })
                specs.Add(new MotionSpec { name = weapon + "BasicAttack" + (facing < 0 ? "_Left" : ""), state = "Attack",
                    clip = source.clip, duration = punchDuration, weapon = weapon, facing = facing });
        }
        var reports = new List<MotionReport>();
        using (var world = new CaptureWorld(Width, Height))
        {
            BuildMotionBackdrop(world);
            foreach (var spec in specs)
            {
                reports.Add(CaptureMotion(world, prefab, states, spec, jumpVelocity, gravity, flight, takeoffDuration, landDuration));
                Debug.Log("SIDEVIEW_MOTION_FRAMES " + spec.name + " " + reports.Last().frames);
            }
        }
        File.WriteAllText(Output + "/motion-review.json", JsonUtility.ToJson(new ReviewReport
        {
            unity = Application.unityVersion, width = Width, height = Height, fps = Fps,
            jumpVelocity = jumpVelocity, gravity = gravity, jumpFlightSeconds = flight, motions = reports.ToArray(),
            camera = "Orthographic -Z gameplay side; camera offset +1.2 X, +1.25 Y, -10 Z = 6.84 degree yaw and 7.07 degree downward pitch. Actor faces exactly +/-X. X/Y camera follows motion; Z remains fixed.",
            method = "Actual saved root Humanoid Avatar and Mecanim controller/weapon override controller, Animator.Play at explicit normalized times and Animator.Update(0). FacingLeft state mirroring and actual blend trees are evaluated. After pose, grip and runtime grounding evaluation, every SkinnedMeshRenderer.BakeMesh generates a disposable MeshFilter/MeshRenderer snapshot at the identical transform with identical shared materials; the original GPU skinned renderer is disabled only in the disposable review instance. This prevents stale edit-time Camera.Render GPU skin poses. Nested Animator remains disabled, matching the saved Player prefab. Source equipment AttachTo and saved potion calibration preserved. Idle/Walk/Run have no start/end hold. Finite actions have .25 second Idle segments before/after, except Dead retains its final pose after the action; Jump returns to Idle after Land. Utility-with-staff fixtures hide the weapon only during the action and restore it during post-Idle. No imported animation, prefab, material or gameplay scene is altered.",
            notes = "Walk/Run cycle duration includes the saved BlendTree child timeScale. X trajectories use prefab speeds; camera follows. Jump Y is v*t-.5*g*t^2 from prefab jump force/project gravity; actual Jump/Air/Land states evaluate VelocityY and FacingLeft. Finite action boundaries are directly sampled state cuts; the actual controller's approximately .025-.04 second transitions are shorter than one 16fps frame and are not artificially lengthened. The shipped ProtagonistVisualGrounding marker-based grounding is evaluated after pose/grip evaluation, with its actual offset recorded; no additional capture-only pose-height correction is added. Actor physics root is lifted only .002m to avoid z-fighting. Mesh penetration warnings cover grounded frames only; airborne pose clearance is recorded without reporting it as ground contact. Throw shows the art-preview-only UrbanPreviewThrownProp trajectory after the actual saved commit pose; it has no collision, damage or inventory effects. These are reproducible visualization frames, not an input-driven play-mode test. VFX and item effects are not fabricated."
        }, true));
        Debug.Log("SIDEVIEW_MOTION_CAPTURE_SUCCESS " + Output);
    }

    static IEnumerable<AnimatorState> AllStates(AnimatorStateMachine machine)
    {
        foreach (var child in machine.states) yield return child.state;
        foreach (var child in machine.stateMachines)
            foreach (var state in AllStates(child.stateMachine)) yield return state;
    }
    static IEnumerable<AnimationClip> MotionClips(Motion motion)
    {
        if (motion is AnimationClip clip) yield return clip;
        else if (motion is BlendTree tree)
            foreach (var child in tree.children)
                foreach (var nested in MotionClips(child.motion)) yield return nested;
    }
    static AnimationClip ClipOf(Dictionary<string, AnimatorState> states, string state)
    {
        if (!states.TryGetValue(state, out var value) || !(value.motion is AnimationClip clip))
            throw new InvalidOperationException("No AnimationClip in required controller state " + state);
        return clip;
    }
    static float ChildTimeScale(Motion motion, string clipName)
    {
        if (motion is BlendTree tree)
            foreach (var child in tree.children)
            {
                if (child.motion is AnimationClip clip && clip.name == clipName) return Mathf.Max(.01f, child.timeScale);
                if (child.motion is BlendTree nested && MotionClips(nested).Any(c => c.name == clipName))
                    return Mathf.Max(.01f, child.timeScale) * ChildTimeScale(nested, clipName);
            }
        return 1;
    }
    static void BuildMotionBackdrop(CaptureWorld world)
    {
        var root = world.NewObject("Actual Urban Kit Motion Background");
        for (int i = -3; i <= 3; i++)
        {
            world.Place("Modules", "Road_Straight_12m", new Vector3(i * 12, 0, 0), root.transform);
            world.Place("Modules", "Sidewalk_Straight_8m", new Vector3(i * 8, .02f, 3.5f), root.transform);
        }
        // Rear facades remain behind the gameplay plane rather than hiding the actor.
        world.Place("Modules", "Building_ShopLow", new Vector3(-7, 0, 6), root.transform);
        world.Place("Modules", "Building_ShopLow", new Vector3(2, 0, 6), root.transform);
        world.Place("Modules", "Building_OfficeMid", new Vector3(13, 0, 7), root.transform);
        world.Place("Modules", "Building_ApartmentTall", new Vector3(-18, 0, 8), root.transform);
        world.Place("Props", "StreetLamp", new Vector3(-4, 0, 2.6f), root.transform);
        world.Place("Props", "VendingMachine", new Vector3(5.5f, 0, 2.3f), root.transform);
        world.Place("Props", "StreetTree", new Vector3(10, 0, 3.2f), root.transform);
    }

    static MotionReport CaptureMotion(CaptureWorld world, GameObject prefab, Dictionary<string, AnimatorState> states,
        MotionSpec spec, float jumpVelocity, float gravity, float flight, float takeoffDuration, float landDuration)
    {
        var actor = Object.Instantiate(prefab);
        SceneManager.MoveGameObjectToScene(actor, world.scene);
        var snapshots = new List<SkinnedPoseSnapshot>();
        GameObject prop = null;
        try
        {
            actor.name = "Review_" + spec.name;
            foreach (var behavior in actor.GetComponentsInChildren<MonoBehaviour>(true)) behavior.enabled = false;
            foreach (var body in actor.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.useGravity = false; }
            foreach (var collider in actor.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            var animator = actor.GetComponent<Animator>();
            foreach (var nested in actor.GetComponentsInChildren<Animator>(true)) if (nested != animator) nested.enabled = false;
            animator.enabled = true;
            animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            actor.transform.rotation = Quaternion.LookRotation(spec.facing > 0 ? Vector3.right : Vector3.left);
            foreach (var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)) renderer.updateWhenOffscreen = true;
            if (!animator.isHuman || !animator.avatar || !animator.avatar.isValid) throw new InvalidOperationException("Invalid review Humanoid");
            animator.Rebind(); animator.Update(0);
            Transform right = animator.GetBoneTransform(HumanBodyBones.RightHand), left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform rightSocket = right.GetComponentsInChildren<Transform>(true).First(t => t.name == "WeaponSocket");
            Transform leftSocket = left.GetComponentsInChildren<Transform>(true).First(t => t.name == "WeaponSocket_Left");
            Transform nearSocket = spec.facing < 0 ? leftSocket : rightSocket;
            SocketWeapon equipped = null;
            if (!string.IsNullOrEmpty(spec.weapon))
            {
                equipped = world.Place("Weapons", spec.weapon, Vector3.zero).GetComponent<SocketWeapon>();
                equipped.AttachTo(nearSocket, spec.facing < 0);
                if (equipped.AnimationOverride) animator.runtimeAnimatorController = equipped.AnimationOverride;
                animator.Rebind(); animator.Update(0);
            }
            if (!string.IsNullOrEmpty(spec.prop))
            {
                prop = world.Place("Props", spec.prop, Vector3.zero);
                if (spec.name.StartsWith("Drink", StringComparison.Ordinal))
                {
                    var mount = prop.GetComponent<ConsumableHandProp>();
                    if (!mount) throw new InvalidOperationException("Saved calibrated potion hand mount missing");
                    mount.AttachTo(nearSocket, spec.facing < 0);
                }
                else
                {
                    var mount = prop.GetComponent<ConsumableHandProp>();
                    if (!mount) throw new InvalidOperationException("Saved throw hand mount missing");
                    mount.AttachForThrow(nearSocket, spec.facing < 0);
                }
            }
            var handGrip = actor.GetComponent<ProtagonistHandGrip>();
            var grounding = actor.GetComponent<ProtagonistVisualGrounding>();
            foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                snapshots.Add(new SkinnedPoseSnapshot(skin));
            string directory = Output + "/Frames/" + spec.name;
            Directory.CreateDirectory(directory);
            foreach (string stale in Directory.GetFiles(directory, "*.png")) File.Delete(stale);
            float hold = spec.loop ? 0 : Hold;
            int count = Mathf.Max(2, Mathf.CeilToInt((spec.duration + hold * 2) * Fps));
            var samples = new List<FrameReport>();
            float highest = 0, minimumSole = float.PositiveInfinity;
            bool throwMotion = spec.name.StartsWith("Throw", StringComparison.Ordinal);
            bool throwReleased = false;
            Vector3 throwOrigin = Vector3.zero;
            Quaternion throwRotation = Quaternion.identity;
            float releaseSeconds = hold + spec.duration * spec.commit;
            for (int i = 0; i < count; i++)
            {
                float seconds = i / (float)Fps;
                float time = Mathf.Clamp(seconds - hold, 0, spec.duration);
                float normalized = Mathf.Clamp01(time / spec.duration);
                float stateNormalized = normalized;
                float height = 0, velocityY = 0;
                string currentClip = spec.clip.name;
                string currentState = spec.state;
                if (spec.jump)
                {
                    height = time < flight ? Mathf.Max(0, jumpVelocity * time - .5f * gravity * time * time) : 0;
                    velocityY = time < flight ? jumpVelocity - gravity * time : 0;
                    if (time < takeoffDuration)
                    {
                        currentState = "Jump";
                        stateNormalized = Mathf.Clamp01(time / takeoffDuration);
                    }
                    else if (time < flight)
                    {
                        currentState = "Air";
                        AnimationClip referenceAir = MotionClips(states["Air"].motion).FirstOrDefault();
                        stateNormalized = (time - takeoffDuration) / Mathf.Max(.01f, referenceAir ? referenceAir.length : .25f);
                        currentClip = states["Air"].motion.name;
                    }
                    else
                    {
                        currentState = "Land";
                        stateNormalized = Mathf.Clamp01((time - flight) / Mathf.Max(.01f, landDuration));
                        currentClip = states["Land"].motion.name;
                    }
                }
                bool duringAction = seconds >= hold && seconds < hold + spec.duration;
                bool beforeAction = !spec.loop && seconds < hold;
                bool afterAction = !spec.loop && seconds >= hold + spec.duration;
                if (beforeAction || (afterAction && spec.state != "Dead"))
                {
                    currentState = "Idle"; currentClip = ClipOf(states, "Idle").name;
                    float idleSeconds = beforeAction ? seconds : seconds - hold - spec.duration;
                    stateNormalized = Mathf.Repeat(.42f + idleSeconds / Mathf.Max(.01f, ClipOf(states, "Idle").length), 1f);
                }
                float x = spec.speed * Mathf.Min(time, spec.jump ? flight : spec.duration) * spec.facing;
                actor.transform.SetPositionAndRotation(new Vector3(x, height + .002f, 0),
                    Quaternion.LookRotation(spec.facing > 0 ? Vector3.right : Vector3.left));
                SetBool(animator, "FacingLeft", spec.facing < 0);
                SetBool(animator, "Walk", currentState == "Locomotion");
                SetBool(animator, "Air", currentState == "Jump" || currentState == "Air");
                SetBool(animator, "Attacking", currentState == "Attack");
                SetBool(animator, "IsDashing", currentState == "Dash");
                SetBool(animator, "IsDead", currentState == "Dead");
                SetBool(animator, "ActionBusy", duringAction && states.ContainsKey(spec.state) && spec.state != "Idle");
                SetFloat(animator, "Speed", spec.state == "Locomotion" ? spec.speed : 0);
                SetFloat(animator, "VelocityY", velocityY);
                SetFloat(animator, "AttackSpeed", spec.clip.length / Mathf.Max(.01f, spec.duration));
                SetFloat(animator, "ActionSpeed", spec.clip.length / Mathf.Max(.01f, spec.duration));
                if (throwMotion && prop && !throwReleased && seconds >= releaseSeconds)
                {
                    // Resolve the release at its exact authored contact time, independently of render fps.
                    float releaseX = spec.speed * spec.duration * spec.commit * spec.facing;
                    actor.transform.SetPositionAndRotation(new Vector3(releaseX, .002f, 0),
                        Quaternion.LookRotation(spec.facing > 0 ? Vector3.right : Vector3.left));
                    if (grounding) grounding.ResetPreview();
                    animator.Play("Base Layer." + spec.state, 0, Mathf.Min(.9999f, spec.commit));
                    animator.Update(0);
                    actor.transform.SetPositionAndRotation(new Vector3(releaseX, .002f, 0),
                        Quaternion.LookRotation(spec.facing > 0 ? Vector3.right : Vector3.left));
                    if (handGrip) handGrip.ApplyPreview(spec.facing > 0, spec.facing < 0);
                    if (grounding) grounding.ApplyPreview(true, false);
                    throwOrigin = prop.transform.position;
                    throwRotation = prop.transform.rotation;
                    prop.transform.SetParent(null, true);
                    throwReleased = true;
                }
                if (grounding) grounding.ResetPreview();
                animator.Play("Base Layer." + currentState, 0, currentState == "Air" ? stateNormalized : Mathf.Min(.9999f, stateNormalized));
                animator.Update(0);
                actor.transform.SetPositionAndRotation(new Vector3(x, height + .002f, 0),
                    Quaternion.LookRotation(spec.facing > 0 ? Vector3.right : Vector3.left));
                if (equipped) equipped.gameObject.SetActive(!spec.utilityFixture || !duringAction);
                if (prop)
                {
                    if (throwReleased)
                    {
                        float elapsed = Mathf.Max(0, seconds - releaseSeconds);
                        prop.transform.position = throwOrigin + new Vector3(spec.facing * 7, 3.2f, 0) * elapsed + Physics.gravity * (.5f * elapsed * elapsed);
                        prop.transform.rotation = Quaternion.AngleAxis(300 * elapsed, Vector3.forward) * throwRotation;
                        prop.SetActive(elapsed <= 1.4f);
                    }
                    else prop.SetActive(duringAction);
                }
                bool closeNear = equipped && equipped.gameObject.activeSelf;
                closeNear |= currentState == "Attack" || currentState == "SwordSlash" || currentState == "MagicCast";
                closeNear |= prop && prop.activeSelf && !throwReleased && (currentState == "Drink" || currentState == "Throw");
                bool closeRight = closeNear && spec.facing > 0, closeLeft = closeNear && spec.facing < 0;
                if (currentState == "Attack" && !equipped) closeRight = closeLeft = true;
                if (handGrip) handGrip.ApplyPreview(closeRight, closeLeft);
                bool grounded = (currentState != "Air" && currentState != "Jump") || (currentState == "Jump" && height < .01f);
                if (grounding) grounding.ApplyPreview(grounded, currentState == "Dead");
                foreach (var snapshot in snapshots) snapshot.Refresh();
                highest = Mathf.Max(highest, height);
                // Follow horizontally and vertically as a platformer camera would, preserving readable character scale.
                Vector3 target = new Vector3(x + spec.facing * .45f, 1.15f + Mathf.Max(0, height - .35f), 0);
                world.LookAt(target, 1.95f);
                world.Save(directory + "/" + i.ToString("000") + ".png");
                float skinY = float.PositiveInfinity;
                foreach (var snapshot in snapshots) skinY = Mathf.Min(skinY, snapshot.MinimumWorldY());
                if (grounded) minimumSole = Mathf.Min(minimumSole, skinY - height);
                samples.Add(new FrameReport { frame = i, seconds = seconds, normalized = normalized, clip = currentClip,
                    state = currentState, velocityY = velocityY, minimumSkinHeight = skinY,
                    weaponVisible = equipped && equipped.gameObject.activeSelf, propVisible = prop && prop.activeSelf,
                    throwReleased = throwReleased, throwElapsed = throwReleased ? Mathf.Max(0, seconds - releaseSeconds) : 0,
                    propPosition = prop ? prop.transform.position : Vector3.zero,
                    rightGrip = closeRight, leftGrip = closeLeft,
                    grounded = grounded, visualGroundOffset = grounding ? grounding.LastAppliedOffset : 0,
                    rootHeight = height, rootPosition = actor.transform.position, cameraPosition = world.camera.transform.position,
                    cameraTarget = target, rightHand = right.position, leftHand = left.position,
                    head = animator.GetBoneTransform(HumanBodyBones.Head).position,
                    minimumFootHeight = Mathf.Min(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position.y,
                        animator.GetBoneTransform(HumanBodyBones.RightFoot).position.y) });
            }
            if (minimumSole < -.03f)
                Debug.LogWarning("SIDEVIEW_GROUND_PENETRATION " + spec.name + " min mesh clearance relative to physics root height=" + minimumSole);
            return new MotionReport { name = spec.name, state = spec.state, clip = spec.clip.name, weapon = spec.weapon,
                prop = spec.prop, sourceSeconds = spec.clip.length, playbackSeconds = spec.duration,
                totalSeconds = count / (float)Fps, frames = count, fps = Fps, facing = spec.facing, humanoid = animator.isHuman,
                maximumRootHeight = highest, minimumSoleClearance = minimumSole, directory = directory, samples = samples.ToArray(),
                previewOnlyProjectile = throwMotion,
                previewProjectileSemantics = throwMotion ? "UrbanPreviewThrownProp art preview only: detach at exact saved commit hand pose; world velocity=(facing*7,3.2,0), project Physics.gravity, world-Z spin +300 degrees/second, lifetime 1.4 seconds. No collision, damage, inventory or skill gameplay is attached." : "",
                durationSource = spec.jump ? "Player jump force/project gravity flight plus Player land duration" :
                    spec.loop ? "One saved Mecanim clip cycle including locomotion child timeScale; no start/end hold" :
                    "Saved prefab gameplay attack/action/dash/hit/land duration" };
        }
        finally
        {
            foreach (var snapshot in snapshots) snapshot.Dispose();
            if (prop) Object.DestroyImmediate(prop);
            Object.DestroyImmediate(actor);
        }
    }

    static void SetBool(Animator animator, string name, bool value)
    {
        if (animator.parameters.Any(p => p.name == name && p.type == AnimatorControllerParameterType.Bool)) animator.SetBool(name, value);
    }
    static void SetFloat(Animator animator, string name, float value)
    {
        if (animator.parameters.Any(p => p.name == name && p.type == AnimatorControllerParameterType.Float)) animator.SetFloat(name, value);
    }

    [MenuItem("Tools/Project M/Review/Render Sideview Environment Shots")]
    public static void CaptureEnvironment()
    {
        Directory.CreateDirectory(Output + "/Environment");
        var reports = new List<EnvironmentShot>();
        using (var world = new CaptureWorld(1600, 900))
        {
            Action<string, Vector3, float, Action<GameObject, List<string>>> shot = (name, target, size, build) =>
            {
                var root = world.NewObject(name);
                var assets = new List<string>();
                try
                {
                    build(root, assets);
                    world.LookAt(target, size);
                    string path = Output + "/Environment/" + name + ".png";
                    world.Save(path);
                    reports.Add(new EnvironmentShot { name = name, image = path, orthographicSize = size,
                        cameraPosition = world.camera.transform.position, cameraTarget = target, prefabs = assets.ToArray() });
                }
                finally { world.ReleaseSnapshots(); Object.DestroyImmediate(root); }
            };
            Action<GameObject, List<string>, string, string, Vector3> place = (root, assets, group, name, p) =>
            { world.Place(group, name, p, root.transform); assets.Add(group + "/" + name); };
            Action<GameObject, List<string>, int> roads = (root, assets, length) =>
            {
                for (int x = -length; x <= length; x += 12) place(root, assets, "Modules", "Road_Straight_12m", new Vector3(x, 0, 0));
            };
            shot("01_Street_Props", new Vector3(0, 2.25f, 0), 4.5f, (root, assets) =>
            {
                roads(root, assets, 24);
                place(root, assets, "Modules", "Building_ShopLow", new Vector3(-5, 0, 6));
                place(root, assets, "Modules", "Building_OfficeMid", new Vector3(5, 0, 7));
                place(root, assets, "Props", "StreetLamp", new Vector3(-6, 0, 1.2f));
                place(root, assets, "Props", "StreetTree", new Vector3(5.5f, 0, 1.8f));
                place(root, assets, "Props", "VendingMachine", new Vector3(-3.4f, 0, .8f));
                place(root, assets, "Props", "Bench", new Vector3(-.7f, 0, .8f));
                place(root, assets, "Props", "TrashBin", new Vector3(1, 0, .8f));
                place(root, assets, "Modules", "Planter_Shrubs", new Vector3(3.4f, 0, 1.5f));
            });
            shot("02_Platform_Buildings", new Vector3(0, 5.6f, 0), 8.2f, (root, assets) =>
            {
                roads(root, assets, 36);
                place(root, assets, "Modules", "Building_ShopLow", new Vector3(-11, 0, 0));
                place(root, assets, "Modules", "Building_OfficeMid", new Vector3(-2.5f, 0, 0));
                place(root, assets, "Modules", "Building_ApartmentTall", new Vector3(7, 0, 0));
                place(root, assets, "Modules", "Rooftop_Bridge_6m", new Vector3(-7, 4, 0));
                place(root, assets, "Modules", "Rooftop_Bridge_6m", new Vector3(2, 8, 0));
                place(root, assets, "Props", "GatePylon", new Vector3(7, 12, 1.2f));
            });
            shot("03_Rooftop_Utilities", new Vector3(0, 4.65f, 0), 5.7f, (root, assets) =>
            {
                roads(root, assets, 24);
                place(root, assets, "Modules", "Building_RooftopArena", Vector3.zero);
                place(root, assets, "Modules", "Exterior_Stairs_Rise3m", new Vector3(-11, 0, 0));
                place(root, assets, "Modules", "Ladder_4m", new Vector3(-7, 0, -3.15f));
                place(root, assets, "Modules", "Rooftop_Hut", new Vector3(-5, 4, 1.2f));
                place(root, assets, "Modules", "Rooftop_WaterTank", new Vector3(-.5f, 4, 1.2f));
                place(root, assets, "Modules", "Rooftop_Billboard", new Vector3(5, 4, 1.9f));
                place(root, assets, "Props", "ACUnit", new Vector3(2.1f, 4, .8f));
                place(root, assets, "Modules", "Utility_Box", new Vector3(-2.7f, 4, 1.1f));
                place(root, assets, "Modules", "Guardrail_4m", new Vector3(5, 4, 2.8f));
                place(root, assets, "Props", "GatePylon", new Vector3(7, 4, .9f));
            });
            shot("04_Tutorial_Gameplay", new Vector3(13, 2, 0), 4.3f, (root, assets) =>
            {
                place(root, assets, "Blocks", "Tutorial_Block", Vector3.zero);
                AddScaleActor(world, root, new Vector3(11, 1.5f, 0));
                assets.Add("Protagonist01_Player (actual Idle Humanoid pose)");
            });
            shot("05_Stage01_Rooftops", new Vector3(40, 11.6f, 0), 5.3f, (root, assets) =>
            {
                place(root, assets, "Blocks", "Stage01_Block", Vector3.zero);
                AddScaleActor(world, root, new Vector3(38, 12.002f, 0));
                assets.Add("Protagonist01_Player (actual Idle Humanoid pose)");
            });
        }
        File.WriteAllText(Output + "/environment-review.json", JsonUtility.ToJson(new EnvironmentReport
        {
            method = "Actual saved Unity kit prefabs, orthographic game-axis camera (-Z, 6.84 degree yaw / 7.07 degree pitch), shared cloudy lighting, no 3/4 catalogue camera. Character scale references use actual Mecanim Idle plus runtime grip/grounding, rendered from disposable CPU BakeMesh snapshots at identical renderer transforms/materials to prevent stale edit-time GPU skin poses. Environment tiles remain three-dimensional; gameplay traverses X with fixed Z.",
            shots = reports.ToArray()
        }, true));
        Debug.Log("SIDEVIEW_ENVIRONMENT_CAPTURE_SUCCESS " + reports.Count);
    }

    static void AddScaleActor(CaptureWorld world, GameObject parent, Vector3 position)
    {
        var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Hero + "/Protagonist01_Player.prefab"));
        SceneManager.MoveGameObjectToScene(actor, world.scene);
        actor.transform.SetParent(parent.transform, false);
        foreach (var behavior in actor.GetComponentsInChildren<MonoBehaviour>(true)) behavior.enabled = false;
        foreach (var body in actor.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.useGravity = false; }
        foreach (var collider in actor.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        var animator = actor.GetComponent<Animator>();
        var controller = animator.runtimeAnimatorController as AnimatorController;
        foreach (var nested in actor.GetComponentsInChildren<Animator>(true)) if (nested != animator) nested.enabled = false;
        animator.enabled = true; animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        actor.transform.SetPositionAndRotation(position, Quaternion.LookRotation(Vector3.right));
        animator.Rebind(); animator.Update(0);
        SetBool(animator, "FacingLeft", false);
        animator.Play("Base Layer.Idle", 0, .42f);
        animator.Update(0);
        actor.transform.SetPositionAndRotation(position, Quaternion.LookRotation(Vector3.right));
        var handGrip = actor.GetComponent<ProtagonistHandGrip>();
        if (handGrip) handGrip.ApplyPreview(false, false);
        var grounding = actor.GetComponent<ProtagonistVisualGrounding>();
        if (grounding) grounding.ApplyPreview(true, false);
        foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var snapshot = new SkinnedPoseSnapshot(skin);
            world.RetainSnapshot(snapshot);
            snapshot.Refresh();
        }
    }

    public static void CaptureAll()
    {
        Capture();
        CaptureEnvironment();
    }
}
#endif
