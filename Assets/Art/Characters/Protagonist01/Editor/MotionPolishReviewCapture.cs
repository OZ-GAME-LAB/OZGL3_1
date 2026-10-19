#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Isolated v004 review output. Never saves or modifies gameplay, controller or source assets.</summary>
public static class MotionPolishReviewCapture
{
    public const string ReviewDirectory = "ArtSources/Characters/Protagonist01/v004-motion-polish";
    public const string ConfigurationPath = ReviewDirectory + "/review-config.json";
    const int SampleRate = 60, GifRate = 30;
    static bool diagnosticPass;
    static readonly Vector3 ViewOffset = new Vector3(1.05f, 1.05f, -10f);

    [Serializable] public sealed class Configuration
    {
        public string prefabPath, idleClipPath, idleClipName;
        public bool keep60fpsFrames, captureSourceOnly, sourceOnly;
        public int width = 768, height = 432;
        public MotionDefinition[] motions = Array.Empty<MotionDefinition>();
    }
    [Serializable] public sealed class MotionDefinition
    {
        public string name, clipPath, clipName, weaponPrefabPath, propPrefabPath;
        public string weaponSocket = "WeaponSocket", propSocket = "WeaponSocket";
        public bool loop;
        public int cycles = 2, facing = 1;
        public float leadSeconds = .25f, tailSeconds = .30f, blendSeconds = .10f;
        public Vector3 weaponPosition, weaponEuler, propPosition, propEuler;
        public bool useSavedWeaponMount = true, useSavedPropMount = true;
        public float propReleaseNormalizedTime = -1;
        public float propFlightSeconds = .65f;
        public Vector3 propReleaseVelocity = new Vector3(5, 2.6f, 0);
        public float minimumOrthographicSize = 1.65f;
    }
    [Serializable] sealed class Frame
    {
        public int index;
        public float time, clipTime, blendWeight, minimumMeshY;
        public string phase;
        public Vector3 leftFoot, rightFoot, leftHand, rightHand, head, hips;
        public Quaternion leftHandRotation, rightHandRotation, leftFootRotation, rightFootRotation, hipsRotation;
        public float leftFootSpeed, rightFootSpeed, leftHandSpeed, rightHandSpeed;
        public float leftHandAcceleration, rightHandAcceleration, leftHandAngularSpeed, rightHandAngularSpeed;
        public float leftFootAcceleration, rightFootAcceleration, leftFootAngularSpeed, rightFootAngularSpeed;
        public float leftGripPercent, rightGripPercent;
        public float leftSoleY, rightSoleY;
        public bool propReleased;
        public Vector3 propPosition, propMouth, mouthReference;
        public float propMouthGap;
    }
    [Serializable] sealed class MotionReport
    {
        public string name, source, sourceClip, frames30Directory, frames60Directory, evaluation, notes;
        public bool loop, sourceLoopFlag, humanoid, sourceOnly;
        public int facing, sourceFrameRate, sampleRate, gifRate, frames30, frames60;
        public float authoredDuration, reviewDuration, sourceSpeed = 1;
        public float maxHandSpeed, maxHandAcceleration, maxHandAngularSpeed, maxFootSpeed, minimumMeshY;
        public float maxFootAcceleration, maxFootAngularSpeed, rootDepthRange, maxFrameJointDisplacement, maxFrameJointAngle;
        public float leftLoopPositionGap, rightLoopPositionGap, leftLoopVelocityGap, rightLoopVelocityGap;
        public float groundedFootSlipPeak;
        public float propReleaseNormalizedTime, propFlightSeconds;
        public Vector3 propReleaseVelocity;
        public Vector3 cameraPosition, cameraTarget;
        public float orthographicSize;
        public Frame[] samples;
    }
    [Serializable] sealed class CaptureReport
    {
        public string unity, method, sourcePrefab, neutralStage;
        public int sampleRate, gifRate, width, height;
        public MotionReport[] motions;
    }
    [Serializable] sealed class VfxReport
    {
        public string name, prefab, frames30Directory, frames60Directory, method;
        public float duration, travelSpeed, orthographicSize;
        public int sampleRate, gifRate, frames30, frames60, width, height;
        public Vector3 cameraPosition, cameraTarget;
    }
    [Serializable] sealed class HandShot
    {
        public string hand, view, image, mesh;
        public float gripWeight, orthographicSize;
        public Vector3 cameraPosition, target;
    }
    [Serializable] sealed class HandReport
    {
        public string prefab, clip, method;
        public HandShot[] shots;
    }

    sealed class SkinSnapshot : IDisposable
    {
        public readonly SkinnedMeshRenderer source;
        public readonly Mesh mesh;
        readonly GameObject obj;
        readonly MeshRenderer renderer;
        readonly bool enabled;
        readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        readonly int[] leftSole, rightSole;
        public SkinSnapshot(SkinnedMeshRenderer source, Transform leftFoot, Transform rightFoot)
        {
            this.source = source; enabled = source.enabled;
            mesh = new Mesh { name = "v004 disposable CPU pose" }; mesh.MarkDynamic();
            obj = new GameObject("CPU Pose Snapshot", typeof(MeshFilter), typeof(MeshRenderer));
            SceneManager.MoveGameObjectToScene(obj, source.gameObject.scene);
            obj.transform.SetParent(source.transform, false); obj.layer = source.gameObject.layer;
            obj.GetComponent<MeshFilter>().sharedMesh = mesh;
            renderer = obj.GetComponent<MeshRenderer>(); renderer.sharedMaterials = source.sharedMaterials;
            renderer.shadowCastingMode = source.shadowCastingMode; renderer.receiveShadows = source.receiveShadows;
            renderer.lightProbeUsage = source.lightProbeUsage; renderer.reflectionProbeUsage = source.reflectionProbeUsage;
            renderer.renderingLayerMask = source.renderingLayerMask; renderer.enabled = enabled;
            leftSole = SoleVertices(source, leftFoot); rightSole = SoleVertices(source, rightFoot);
            source.enabled = false;
        }
        static int[] SoleVertices(SkinnedMeshRenderer skin, Transform foot)
        {
            if (!foot || !skin.sharedMesh) return Array.Empty<int>();
            var weights = skin.sharedMesh.boneWeights;
            var bones = skin.bones;
            bool FootBone(int index) => index >= 0 && index < bones.Length && bones[index] &&
                (bones[index] == foot || bones[index].IsChildOf(foot));
            var result = new List<int>();
            for (int i = 0; i < weights.Length; i++)
            {
                BoneWeight w = weights[i];
                float sum = (FootBone(w.boneIndex0) ? w.weight0 : 0) + (FootBone(w.boneIndex1) ? w.weight1 : 0) +
                    (FootBone(w.boneIndex2) ? w.weight2 : 0) + (FootBone(w.boneIndex3) ? w.weight3 : 0);
                if (sum > .35f) result.Add(i);
            }
            return result.ToArray();
        }
        public void Bake()
        {
            source.BakeMesh(mesh); mesh.RecalculateBounds(); mesh.UploadMeshData(false);
            source.GetPropertyBlock(properties); renderer.SetPropertyBlock(properties);
        }
        public Bounds WorldBounds()
        {
            Vector3[] v = mesh.vertices;
            if (v.Length == 0) return new Bounds(source.transform.position, Vector3.zero);
            var result = new Bounds(source.transform.TransformPoint(v[0]), Vector3.zero);
            foreach (var point in v) result.Encapsulate(source.transform.TransformPoint(point));
            return result;
        }
        public float MinimumY()
        {
            float value = float.PositiveInfinity;
            foreach (var v in mesh.vertices) value = Mathf.Min(value, source.transform.TransformPoint(v).y);
            return value;
        }
        public float SoleY(bool left)
        {
            int[] indices = left ? leftSole : rightSole;
            if (indices.Length == 0) return float.NaN;
            Vector3[] vertices = mesh.vertices; float min = float.PositiveInfinity;
            foreach (int i in indices) if (i < vertices.Length) min = Mathf.Min(min, source.transform.TransformPoint(vertices[i]).y);
            return min;
        }
        public float GripPercent(string name)
        {
            int index = source.sharedMesh ? source.sharedMesh.GetBlendShapeIndex(name) : -1;
            return index >= 0 ? source.GetBlendShapeWeight(index) : -1;
        }
        public void Dispose()
        {
            if (source) source.enabled = enabled;
            if (obj) Object.DestroyImmediate(obj);
            if (mesh) Object.DestroyImmediate(mesh);
        }
    }

    sealed class Stage : IDisposable
    {
        public readonly Scene scene;
        public readonly Camera camera;
        readonly Scene previous;
        readonly RenderTexture target, previousTarget;
        readonly Texture2D pixels;
        readonly Material floorMaterial, gridMaterial, contactMaterial;
        readonly Mesh gridMesh, contactMesh;
        readonly Texture2D contactTexture;
        readonly Transform contactShadow;
        readonly bool asyncCompilation, fog;
        readonly Color ambient;
        readonly AmbientMode ambientMode;
        readonly SphericalHarmonicsL2 probe;
        public Stage(int width, int height)
        {
            previous = SceneManager.GetActiveScene(); previousTarget = RenderTexture.active;
            asyncCompilation = ShaderUtil.allowAsyncCompilation; fog = RenderSettings.fog;
            ambient = RenderSettings.ambientLight; ambientMode = RenderSettings.ambientMode; probe = RenderSettings.ambientProbe;
            ShaderUtil.allowAsyncCompilation = false; RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.38f, .41f, .46f);
            var neutralProbe = new SphericalHarmonicsL2(); neutralProbe.AddAmbientLight(RenderSettings.ambientLight);
            RenderSettings.ambientProbe = neutralProbe;
            scene = EditorSceneManager.NewPreviewScene();
            camera = New("V004 Neutral Review Camera").AddComponent<Camera>(); camera.scene = scene;
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.105f, .12f, .145f); camera.nearClipPlane = .02f; camera.farClipPlane = 150;
            camera.allowHDR = false; camera.allowMSAA = true;
            var key = New("Neutral Soft Key").AddComponent<Light>(); key.type = LightType.Directional;
            key.intensity = 1.0f; key.color = new Color(.88f, .92f, 1); key.shadows = LightShadows.Soft;
            key.shadowStrength = .7f; key.transform.rotation = Quaternion.Euler(48, -38, 0);
            var fill = New("Neutral Fill").AddComponent<Light>(); fill.type = LightType.Directional;
            fill.intensity = .17f; fill.color = new Color(.75f, .80f, .88f); fill.transform.rotation = Quaternion.Euler(25, 150, 0);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); SceneManager.MoveGameObjectToScene(floor, scene);
            // A finite rear edge reveals the ground plane. An 80m plane filled the whole
            // shallow-angle frustum and made the first diagnostic images look background-only.
            floor.name = "Neutral Ground Reference"; floor.transform.localScale = new Vector3(4, 1, 1.5f);
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP Lit required for the neutral review floor.");
            floorMaterial = new Material(shader) { name = "Disposable neutral floor" };
            floorMaterial.SetColor("_BaseColor", new Color(.145f, .165f, .19f)); floorMaterial.SetFloat("_Smoothness", 0);
            floor.GetComponent<MeshRenderer>().sharedMaterial = floorMaterial;
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            gridMaterial = new Material(unlit) { name = "Disposable 1m reference grid" };
            gridMaterial.SetColor("_BaseColor", new Color(.055f, .07f, .09f));
            var grid = New("Neutral Meter Grid"); grid.AddComponent<MeshFilter>(); grid.AddComponent<MeshRenderer>();
            gridMesh = BuildGrid(); grid.GetComponent<MeshFilter>().sharedMesh = gridMesh;
            grid.GetComponent<MeshRenderer>().sharedMaterial = gridMaterial;
            grid.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            // The character's preserved unlit source material has no body ShadowCaster.
            // This explicitly labelled review guide locates its contact; it changes no pose.
            contactTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = "Disposable soft contact guide" };
            var contactPixels = new Color[64 * 64];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float radius = new Vector2((x + .5f) / 32 - 1, (y + .5f) / 32 - 1).magnitude;
                contactPixels[y * 64 + x] = new Color(0, 0, 0, Mathf.Pow(Mathf.Clamp01(1 - radius), 1.5f) * .55f);
            }
            contactTexture.SetPixels(contactPixels); contactTexture.Apply(false, false);
            contactMaterial = new Material(unlit) { name = "Disposable contact-guide overlay", renderQueue = 3000 };
            contactMaterial.SetTexture("_BaseMap", contactTexture); contactMaterial.SetColor("_BaseColor", Color.white);
            contactMaterial.SetFloat("_Surface", 1); contactMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            contactMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha); contactMaterial.SetFloat("_ZWrite", 0);
            contactMaterial.SetFloat("_Cull", (float)CullMode.Off); contactMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            contactMaterial.SetOverrideTag("RenderType", "Transparent");
            var contact = New("Contact Shadow Guide (review only)"); contactShadow = contact.transform;
            contactMesh = QuadMesh(); contact.AddComponent<MeshFilter>().sharedMesh = contactMesh;
            var contactRenderer = contact.AddComponent<MeshRenderer>(); contactRenderer.sharedMaterial = contactMaterial;
            contactRenderer.shadowCastingMode = ShadowCastingMode.Off;
            contactShadow.localScale = new Vector3(.8f, 1, .45f);
            contactShadow.position = new Vector3(0, .006f, 0);
            target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            target.Create(); camera.targetTexture = target;
            pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        }
        public GameObject New(string name)
        {
            var obj = new GameObject(name); SceneManager.MoveGameObjectToScene(obj, scene); return obj;
        }
        static Mesh QuadMesh()
        {
            var mesh = new Mesh { name = "Disposable contact quad" };
            mesh.vertices = new[] { new Vector3(-.5f,0,-.5f),new Vector3(.5f,0,-.5f),new Vector3(.5f,0,.5f),new Vector3(-.5f,0,.5f) };
            mesh.uv = new[] { Vector2.zero,Vector2.right,Vector2.one,Vector2.up };
            mesh.triangles = new[] { 0,2,1,0,3,2 }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        static Mesh BuildGrid()
        {
            var vertices = new List<Vector3>(); var indices = new List<int>();
            void Strip(float x0, float x1, float z0, float z1)
            {
                int i = vertices.Count;
                vertices.AddRange(new[] { new Vector3(x0,.004f,z0),new Vector3(x1,.004f,z0),new Vector3(x1,.004f,z1),new Vector3(x0,.004f,z1) });
                indices.AddRange(new[] { i,i+2,i+1,i,i+3,i+2 });
            }
            for (int x = -20; x <= 20; x++) Strip(x - .006f, x + .006f, -7.5f, 7.5f);
            for (int z = -7; z <= 7; z++) Strip(-20, 20, z - .006f, z + .006f);
            var mesh = new Mesh { name = "Disposable 1m floor grid" }; mesh.SetVertices(vertices); mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        public void SetContactGuide(Vector3 center, float height, bool visible = true)
        {
            contactShadow.gameObject.SetActive(visible);
            contactShadow.position = new Vector3(center.x, .006f, center.z);
            float spread = Mathf.Clamp(height, 0, 2) * .25f;
            contactShadow.localScale = new Vector3(.8f + spread, 1, .45f + spread * .5f);
            contactMaterial.SetColor("_BaseColor", new Color(1, 1, 1, 1 / (1 + Mathf.Max(0, height))));
        }
        public void Frame(Bounds bounds, float minimumSize)
        {
            Vector3 center = bounds.center;
            center.y = Mathf.Max(.85f, center.y);
            camera.transform.position = center + ViewOffset; camera.transform.LookAt(center);
            Vector3 up = camera.transform.up, right = camera.transform.right;
            float maxY = 0, maxX = 0;
            foreach (int x in new[] { -1, 1 }) foreach (int y in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                maxY = Mathf.Max(maxY, Mathf.Abs(Vector3.Dot(corner - center, up)));
                maxX = Mathf.Max(maxX, Mathf.Abs(Vector3.Dot(corner - center, right)));
            }
            camera.orthographicSize = Mathf.Max(minimumSize, maxY * 1.16f, maxX / camera.aspect * 1.16f);
        }
        public void Save(string path)
        {
            camera.Render(); camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply(false, false);
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        public void Dispose()
        {
            RenderTexture.active = previousTarget; camera.targetTexture = null;
            ShaderUtil.allowAsyncCompilation = asyncCompilation; RenderSettings.fog = fog;
            RenderSettings.ambientMode = ambientMode; RenderSettings.ambientLight = ambient; RenderSettings.ambientProbe = probe;
            Object.DestroyImmediate(target); Object.DestroyImmediate(pixels); Object.DestroyImmediate(floorMaterial);
            Object.DestroyImmediate(gridMaterial); Object.DestroyImmediate(gridMesh); Object.DestroyImmediate(contactMaterial);
            Object.DestroyImmediate(contactTexture); Object.DestroyImmediate(contactMesh);
            EditorSceneManager.ClosePreviewScene(scene);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    [MenuItem("Tools/Project M/V004/Render Natural Motion Review")]
    public static void Capture()
    {
        if (!File.Exists(ConfigurationPath)) throw new FileNotFoundException("Create the v004 review-config.json first", ConfigurationPath);
        var config = JsonUtility.FromJson<Configuration>(File.ReadAllText(ConfigurationPath));
        if (config == null || string.IsNullOrWhiteSpace(config.prefabPath)) throw new InvalidOperationException("Review configuration requires prefabPath.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(config.prefabPath);
        if (!prefab) throw new InvalidOperationException("Missing v004 review prefab: " + config.prefabPath);
        AnimationClip idle = LoadClip(config.idleClipPath, config.idleClipName, false);
        var reports = new List<MotionReport>();
        using (var stage = new Stage(config.width, config.height))
            foreach (MotionDefinition motion in config.motions)
            {
                if (IsDeath(motion.name) || IsDeath(motion.clipName)) continue;
                ValidateName(motion.name);
                reports.Add(CaptureMotion(stage, config, prefab, idle, motion, config.sourceOnly));
                if (config.captureSourceOnly && !config.sourceOnly && !motion.loop)
                    reports.Add(CaptureMotion(stage, config, prefab, idle, motion, true));
                Debug.Log("V004_NATURAL_MOTION_RENDERED " + motion.name);
            }
        Directory.CreateDirectory(ReviewDirectory);
        File.WriteAllText(ReviewDirectory + (diagnosticPass ? "/motion-review-diagnostic.json" : "/motion-review.json"), JsonUtility.ToJson(new CaptureReport
        {
            unity = Application.unityVersion, sourcePrefab = config.prefabPath, sampleRate = SampleRate, gifRate = diagnosticPass ? 10 : GifRate,
            width = config.width, height = config.height, motions = reports.ToArray(),
            neutralStage = "Neutral slate background, finite plain floor with 1m reference grid and actual directional shadows where source shaders support them. The preserved unlit character material has an explicitly review-only soft contact ellipse; this is a visual ground reference, not measured lighting or pose correction. No environment kit or gameplay scene.",
            method = "Disposable prefab and original authored clip at exactly 1x speed. Manual Humanoid/Generic clip Playable evaluates at 60Hz; CPU BakeMesh snapshots render the exact evaluated skin. 30fps frames are every second 60Hz sample; optional 60fps PNG originals share the same clock. Loop clips run continuously for two cycles. Finite clips preserve the entire authored duration with short continuous mixer transitions to Idle. No gameplay assets, controllers or runtime scripts are modified. GIF delay quantization is performed separately by the exporter."
        }, true));
        Debug.Log("V004_MOTION_REVIEW_CAPTURE_SUCCESS " + ReviewDirectory);
    }

    [MenuItem("Tools/Project M/V004/Configure Fourteen Prime Candidate Reviews")]
    public static void ConfigurePrimeCandidates()
    {
        const string folder = "Assets/Art/Characters/Protagonist01/MotionPolish_v004";
        string[] names = { "Idle_Fists", "Walk_Fists", "Run_Fists", "Jump_Full", "Dash_Forward", "Punch_Right_Jab", "Potion_Drink",
            "Item_Throw_Overhand", "Staff_Cast_Forward", "Hit_Reaction", "Interact_Reach", "Pickup_Low", "Sword_Horizontal_Slash", "Sword_Aerial_Plunge" };
        var config = new Configuration
        {
            prefabPath = folder + "/Prefabs/Protagonist01_MotionReview.prefab",
            idleClipPath = folder + "/Clips/Idle_Fists.anim", idleClipName = "Idle_Fists",
            width = 640, height = 480, sourceOnly = true, keep60fpsFrames = false,
            motions = names.Select(name => new MotionDefinition { name = name, clipPath = folder + "/Clips/" + name + ".anim", clipName = name,
                loop = name == "Idle_Fists" || name == "Walk_Fists" || name == "Run_Fists", cycles = 1 }).ToArray()
        };
        Directory.CreateDirectory(ReviewDirectory);
        File.WriteAllText(ConfigurationPath, JsonUtility.ToJson(config, true));
        Debug.Log("V004_PRIME_REVIEW_CONFIG_READY " + ConfigurationPath);
    }

    [MenuItem("Tools/Project M/V004/Render Quick Candidate Diagnostics")]
    public static void CaptureDiagnosticFrames()
    {
        diagnosticPass = true;
        try { Capture(); }
        finally { diagnosticPass = false; }
    }
    public static void CapturePrimeDiagnostics()
    {
        ConfigurePrimeCandidates();
        CaptureDiagnosticFrames();
    }

    public static void ConfigureFinalReview()
    {
        var config = JsonUtility.FromJson<Configuration>(File.ReadAllText(ConfigurationPath));
        config.width = 768; config.height = 576; config.sourceOnly = true; config.captureSourceOnly = false;
        foreach (var motion in config.motions)
        {
            motion.minimumOrthographicSize = 1.35f;
            if (motion.loop) motion.cycles = 2;
            if (motion.name != "Potion_Drink" && motion.name != "Item_Throw_Overhand") continue;
            motion.propPrefabPath = "Assets/Art/Stage01Kit/Prefabs/Props/Potion_Health.prefab";
            motion.useSavedPropMount = false;
            // Existing bottle mesh is base-pivoted (Y 0..237mm); its grip is 83mm above base.
            motion.propPosition = new Vector3(.083f, .045f, 0);
            motion.propEuler = new Vector3(0, 0, 90);
            motion.propReleaseNormalizedTime = motion.name == "Item_Throw_Overhand" ? .46f : -1;
            motion.propFlightSeconds = .65f; motion.propReleaseVelocity = new Vector3(5, 2.6f, 0);
        }
        File.WriteAllText(ConfigurationPath, JsonUtility.ToJson(config, true));
        Debug.Log("V004_FINAL_REVIEW_CONFIG_READY");
    }

    static bool IsDeath(string name) => !string.IsNullOrEmpty(name) &&
        (name.IndexOf("death", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("dead", StringComparison.OrdinalIgnoreCase) >= 0);
    static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Any(c => !(c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '_' || c == '-')))
            throw new InvalidOperationException("Use a unique English alphanumeric motion name: " + name);
    }
    static AnimationClip LoadClip(string path, string name, bool required = true)
    {
        if (string.IsNullOrEmpty(path)) { if (required) throw new InvalidOperationException("Animation path missing"); return null; }
        AnimationClip result = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal) && (string.IsNullOrEmpty(name) || c.name == name));
        if (!result && required) throw new InvalidOperationException("Missing review animation " + path + " / " + name);
        return result;
    }
    static Transform Bone(Animator animator, HumanBodyBones human, params string[] fragments)
    {
        if (animator.isHuman) return animator.GetBoneTransform(human);
        return animator.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => fragments.Any(f => t.name.EndsWith(f, StringComparison.OrdinalIgnoreCase)));
    }
    static Vector3 Position(Transform t) => t ? t.position : Vector3.zero;
    static Quaternion Rotation(Transform t) => t ? t.rotation : Quaternion.identity;

    static MotionReport CaptureMotion(Stage stage, Configuration config, GameObject prefab, AnimationClip idle, MotionDefinition def, bool sourceOnly)
    {
        AnimationClip clip = LoadClip(def.clipPath, def.clipName);
        if (clip.length <= 0) throw new InvalidOperationException("Empty source clip " + def.name);
        string name = def.name + (sourceOnly ? "_SourceOnly" : "");
        var placement = stage.New("V004 Static Review Placement");
        placement.transform.SetPositionAndRotation(Vector3.up * .002f, Quaternion.LookRotation(def.facing < 0 ? Vector3.left : Vector3.right));
        var actor = Object.Instantiate(prefab); SceneManager.MoveGameObjectToScene(actor, stage.scene);
        actor.transform.SetParent(placement.transform, false);
        actor.transform.localPosition = Vector3.zero; actor.transform.localRotation = Quaternion.identity;
        var snapshots = new List<SkinSnapshot>(); var graph = default(PlayableGraph);
        try
        {
            actor.name = "V004_" + name;
            foreach (var behavior in actor.GetComponentsInChildren<MonoBehaviour>(true)) behavior.enabled = false;
            foreach (var body in actor.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.useGravity = false; }
            foreach (var collider in actor.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            Animator animator = actor.GetComponent<Animator>() ?? actor.GetComponentInChildren<Animator>(true);
            if (!animator) throw new InvalidOperationException("Review prefab requires an Animator.");
            foreach (var nested in actor.GetComponentsInChildren<Animator>(true)) if (nested != animator) nested.enabled = false;
            animator.enabled = true; animator.runtimeAnimatorController = null; animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.Rebind(); animator.Update(0);
            Transform lf = Bone(animator, HumanBodyBones.LeftFoot, "LeftFoot", "foot_l"), rf = Bone(animator, HumanBodyBones.RightFoot, "RightFoot", "foot_r");
            Transform lh = Bone(animator, HumanBodyBones.LeftHand, "LeftHand", "hand_l"), rh = Bone(animator, HumanBodyBones.RightHand, "RightHand", "hand_r");
            Transform head = Bone(animator, HumanBodyBones.Head, "Head");
            Transform hips = Bone(animator, HumanBodyBones.Hips, "Hips", "Pelvis");
            Attach(actor, stage, def.weaponPrefabPath, def.weaponSocket, def.facing < 0 ? lh : rh, def.weaponPosition, def.weaponEuler, def.useSavedWeaponMount, def.facing < 0);
            GameObject prop = Attach(actor, stage, def.propPrefabPath, def.propSocket, def.facing < 0 ? lh : rh, def.propPosition, def.propEuler, def.useSavedPropMount, def.facing < 0);
            Transform propParent = prop ? prop.transform.parent : null;
            Vector3 propLocalPosition = prop ? prop.transform.localPosition : Vector3.zero;
            Quaternion propLocalRotation = prop ? prop.transform.localRotation : Quaternion.identity;
            Vector3 propLocalScale = prop ? prop.transform.localScale : Vector3.one;
            foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)) snapshots.Add(new SkinSnapshot(skin, lf, rf));
            if (snapshots.Count == 0) throw new InvalidOperationException("No skinned review mesh in " + config.prefabPath);
            graph = PlayableGraph.Create("V004 natural " + name); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var action = AnimationClipPlayable.Create(graph, clip); action.SetApplyFootIK(false); action.SetApplyPlayableIK(false);
            AnimationClip idleSource = idle ? idle : clip;
            var idlePlayable = AnimationClipPlayable.Create(graph, idleSource); idlePlayable.SetApplyFootIK(false); idlePlayable.SetApplyPlayableIK(false);
            var mixer = AnimationMixerPlayable.Create(graph, 2); graph.Connect(action, 0, mixer, 0); graph.Connect(idlePlayable, 0, mixer, 1);
            AnimationPlayableOutput.Create(graph, "Actual source pose", animator).SetSourcePlayable(mixer); graph.Play();
            float lead = sourceOnly || def.loop || !idle ? 0 : Mathf.Max(0, def.leadSeconds);
            float tail = sourceOnly || def.loop || !idle ? 0 : Mathf.Max(0, def.tailSeconds);
            float actionDuration = clip.length * (def.loop ? Mathf.Max(1, def.cycles) : 1);
            float total = lead + actionDuration + tail;
            int count = Mathf.Max(2, Mathf.CeilToInt(total * SampleRate));
            float blend = sourceOnly || def.loop || !idle ? 0 : Mathf.Min(def.blendSeconds, clip.length * .2f);
            bool previewRelease = prop && def.propReleaseNormalizedTime > 0 && def.propReleaseNormalizedTime < 1;
            float releaseTime = clip.length * def.propReleaseNormalizedTime;
            Vector3 releasePosition = Vector3.zero; Quaternion releaseRotation = Quaternion.identity;
            if (previewRelease)
            {
                action.SetTime(releaseTime); mixer.SetInputWeight(0, 1); mixer.SetInputWeight(1, 0); graph.Evaluate(0);
                releasePosition = prop.transform.position; releaseRotation = prop.transform.rotation;
            }
            void Evaluate(float t, out float actionTime, out float weight, out string phase)
            {
                actionTime = Mathf.Clamp(t - lead, 0, actionDuration);
                float clipTime = def.loop ? Mathf.Repeat(actionTime, clip.length) : Mathf.Min(clip.length - .00001f, actionTime);
                action.SetTime(clipTime);
                // The invisible middle of a finite action can switch the idle clock. Align the
                // tail to the idle clip's loop endpoint so the final GIF frame joins its first.
                float idleClock = t < lead + actionDuration * .5f ? t : idleSource.length - tail + t - lead - actionDuration;
                idlePlayable.SetTime(Mathf.Repeat(idleClock, Mathf.Max(.001f, idleSource.length)));
                weight = sourceOnly || def.loop || !idle ? 1 : t < lead || t >= lead + actionDuration ? 0 : 1;
                if (weight > 0 && blend > 0)
                    weight = Mathf.SmoothStep(0, 1, actionTime / blend) * Mathf.SmoothStep(0, 1, (actionDuration - actionTime) / blend);
                mixer.SetInputWeight(0, weight); mixer.SetInputWeight(1, 1 - weight);
                graph.Evaluate(0);
                if (previewRelease)
                {
                    float elapsed = t - lead - releaseTime;
                    if (elapsed < 0)
                    {
                        prop.transform.SetParent(propParent, false); prop.transform.localPosition = propLocalPosition;
                        prop.transform.localRotation = propLocalRotation; prop.transform.localScale = propLocalScale; prop.SetActive(true);
                    }
                    else
                    {
                        prop.transform.SetParent(placement.transform, true);
                        Vector3 velocity = def.propReleaseVelocity; velocity.x *= def.facing < 0 ? -1 : 1;
                        prop.transform.position = releasePosition + velocity * elapsed + Physics.gravity * (.5f * elapsed * elapsed);
                        prop.transform.rotation = Quaternion.AngleAxis(300 * elapsed, Vector3.forward) * releaseRotation;
                        prop.SetActive(elapsed < Mathf.Max(.1f, def.propFlightSeconds));
                    }
                }
                foreach (var snapshot in snapshots) snapshot.Bake();
                Vector3 footCenter = lf && rf ? (lf.position + rf.position) * .5f : snapshots[0].WorldBounds().center;
                stage.SetContactGuide(footCenter, snapshots.Min(s => s.MinimumY()));
                phase = t < lead ? "IdleLead" : t >= lead + actionDuration ? "IdleTail" : "AuthoredClip";
            }
            Bounds union = default; bool first = true;
            // A fixed camera for the whole shot prevents framing motion from hiding pose discontinuities.
            for (int i = 0; i <= 32; i++)
            {
                Evaluate(total * i / 32, out _, out _, out _);
                foreach (var snapshot in snapshots)
                {
                    if (first) { union = snapshot.WorldBounds(); first = false; } else union.Encapsulate(snapshot.WorldBounds());
                }
                foreach (var renderer in actor.GetComponentsInChildren<MeshRenderer>(true))
                    if (renderer.enabled) union.Encapsulate(renderer.bounds);
            }
            stage.Frame(union, Mathf.Max(1.4f, def.minimumOrthographicSize));
            string frames30 = ReviewDirectory + (diagnosticPass ? "/Diagnostics/" : "/Frames30/") + name, frames60 = ReviewDirectory + "/Frames60/" + name;
            ClearPngs(frames30); if (config.keep60fpsFrames) ClearPngs(frames60);
            var frames = new List<Frame>(); int output30 = 0;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                Evaluate(t, out float actionTime, out float weight, out string phase);
                if (config.keep60fpsFrames && !diagnosticPass) stage.Save(frames60 + "/" + i.ToString("0000") + ".png");
                if (i % (diagnosticPass ? 6 : 2) == 0) { stage.Save(frames30 + "/" + output30.ToString("0000") + ".png"); output30++; }
                frames.Add(new Frame { index = i, time = t, clipTime = def.loop ? Mathf.Repeat(actionTime, clip.length) : actionTime,
                    blendWeight = weight, phase = phase, minimumMeshY = snapshots.Min(s => s.MinimumY()),
                    leftFoot = Position(lf), rightFoot = Position(rf), leftHand = Position(lh), rightHand = Position(rh), head = Position(head),
                    hips = Position(hips), hipsRotation = Rotation(hips), leftFootRotation = Rotation(lf), rightFootRotation = Rotation(rf),
                    leftHandRotation = Rotation(lh), rightHandRotation = Rotation(rh),
                    leftGripPercent = snapshots.Max(s => s.GripPercent("Grip_L")), rightGripPercent = snapshots.Max(s => s.GripPercent("Grip_R")),
                    propReleased = previewRelease && t >= lead + releaseTime, propPosition = prop ? prop.transform.position : Vector3.zero,
                    propMouth = prop ? prop.transform.TransformPoint(Vector3.up * .237f) : Vector3.zero,
                    mouthReference = head ? head.position + placement.transform.TransformVector(new Vector3(0,.14f,.15f)) : Vector3.zero,
                    propMouthGap = prop && head ? Vector3.Distance(prop.transform.TransformPoint(Vector3.up*.237f),head.position+placement.transform.TransformVector(new Vector3(0,.14f,.15f))) : 999,
                    leftSoleY = FiniteMinimum(snapshots.Select(s => s.SoleY(true))), rightSoleY = FiniteMinimum(snapshots.Select(s => s.SoleY(false))) });
            }
            ComputeDerivatives(frames);
            var report = new MotionReport { name = name, source = def.clipPath, sourceClip = clip.name, sourceOnly = sourceOnly,
                loop = def.loop, sourceLoopFlag = clip.isLooping, humanoid = animator.isHuman, facing = def.facing,
                authoredDuration = clip.length, reviewDuration = total, sourceFrameRate = Mathf.RoundToInt(clip.frameRate),
                sampleRate = SampleRate, gifRate = diagnosticPass ? 10 : GifRate, frames30 = output30, frames60 = config.keep60fpsFrames && !diagnosticPass ? count : 0,
                frames30Directory = frames30, frames60Directory = config.keep60fpsFrames ? frames60 : "",
                evaluation = "Manual AnimationClipPlayable at source 1x speed; explicit 60Hz samples; CPU BakeMesh snapshot. A fixed parent supplies review yaw/placement; animated root/bone transform curves are never overwritten. Extracted Humanoid root motion is not applied, so authored vertical movement must be baked into the source pose. No per-frame grounding or height correction is fabricated.",
                notes = "Loop and derivative measurements are diagnostic values, not pass/fail standards. Foot slip is reported only for foot-weighted sole vertices within .03m of the floor. No healing, damage, VFX or gameplay timing is invented in this source-motion shot." +
                    (previewRelease ? " A review-only prop detaches at the configured source hand pose and follows the declared velocity plus Physics.gravity with 300deg/s Z spin; it has no collision, inventory or gameplay effects." : ""),
                cameraPosition = stage.camera.transform.position, cameraTarget = stage.camera.transform.position - ViewOffset,
                propReleaseNormalizedTime = previewRelease ? def.propReleaseNormalizedTime : -1,
                propFlightSeconds = previewRelease ? def.propFlightSeconds : 0, propReleaseVelocity = previewRelease ? def.propReleaseVelocity : Vector3.zero,
                orthographicSize = stage.camera.orthographicSize, samples = frames.ToArray() };
            FinishMetrics(report, frames, clip.length, lead);
            return report;
        }
        finally
        {
            if (graph.IsValid()) graph.Destroy(); foreach (var snapshot in snapshots) snapshot.Dispose(); Object.DestroyImmediate(placement);
        }
    }

    static void ClearPngs(string path)
    {
        Directory.CreateDirectory(path);
        foreach (string file in Directory.GetFiles(path, "*.png")) File.Delete(file);
    }
    static float FiniteMinimum(IEnumerable<float> values)
    {
        float min = float.PositiveInfinity;
        foreach (float f in values) if (!float.IsNaN(f) && !float.IsInfinity(f)) min = Mathf.Min(min, f);
        return float.IsPositiveInfinity(min) ? 999 : min;
    }
    static GameObject Attach(GameObject actor, Stage stage, string path, string socketName, Transform hand, Vector3 position, Vector3 euler, bool savedMount, bool mirrored)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!prefab) throw new InvalidOperationException("Review attachment missing: " + path);
        var obj = Object.Instantiate(prefab); SceneManager.MoveGameObjectToScene(obj, stage.scene);
        Transform socket = actor.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == socketName && (!hand || t.IsChildOf(hand))) ?? hand;
        if (!socket) throw new InvalidOperationException("Review attachment has no hand/socket " + socketName);
        foreach (var collider in obj.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var component in obj.GetComponents<MonoBehaviour>())
        {
            if (!savedMount) break;
            MethodInfo mount = component.GetType().GetMethod("AttachTo", new[] { typeof(Transform), typeof(bool) });
            if (mount == null) continue;
            mount.Invoke(component, new object[] { socket, mirrored }); return obj;
        }
        obj.transform.SetParent(socket, false); obj.transform.localPosition = position; obj.transform.localRotation = Quaternion.Euler(euler);
        return obj;
    }
    static void ComputeDerivatives(List<Frame> frames)
    {
        for (int i = 1; i < frames.Count; i++)
        {
            Frame a = frames[i - 1], b = frames[i]; float dt = b.time - a.time;
            b.leftFootSpeed = Vector3.Distance(a.leftFoot, b.leftFoot) / dt; b.rightFootSpeed = Vector3.Distance(a.rightFoot, b.rightFoot) / dt;
            b.leftHandSpeed = Vector3.Distance(a.leftHand, b.leftHand) / dt; b.rightHandSpeed = Vector3.Distance(a.rightHand, b.rightHand) / dt;
            b.leftHandAngularSpeed = Quaternion.Angle(a.leftHandRotation, b.leftHandRotation) / dt;
            b.rightHandAngularSpeed = Quaternion.Angle(a.rightHandRotation, b.rightHandRotation) / dt;
            b.leftFootAngularSpeed = Quaternion.Angle(a.leftFootRotation, b.leftFootRotation) / dt;
            b.rightFootAngularSpeed = Quaternion.Angle(a.rightFootRotation, b.rightFootRotation) / dt;
            if (i > 1)
            {
                Frame p = frames[i - 2];
                b.leftHandAcceleration = ((b.leftHand - a.leftHand) / dt - (a.leftHand - p.leftHand) / dt).magnitude / dt;
                b.rightHandAcceleration = ((b.rightHand - a.rightHand) / dt - (a.rightHand - p.rightHand) / dt).magnitude / dt;
                b.leftFootAcceleration = ((b.leftFoot - a.leftFoot) / dt - (a.leftFoot - p.leftFoot) / dt).magnitude / dt;
                b.rightFootAcceleration = ((b.rightFoot - a.rightFoot) / dt - (a.rightFoot - p.rightFoot) / dt).magnitude / dt;
            }
        }
    }
    static void FinishMetrics(MotionReport report, List<Frame> frames, float cycle, float lead)
    {
        report.maxHandSpeed = frames.Max(f => Mathf.Max(f.leftHandSpeed, f.rightHandSpeed));
        report.maxHandAcceleration = frames.Max(f => Mathf.Max(f.leftHandAcceleration, f.rightHandAcceleration));
        report.maxHandAngularSpeed = frames.Max(f => Mathf.Max(f.leftHandAngularSpeed, f.rightHandAngularSpeed));
        report.maxFootSpeed = frames.Max(f => Mathf.Max(f.leftFootSpeed, f.rightFootSpeed));
        report.maxFootAcceleration = frames.Max(f => Mathf.Max(f.leftFootAcceleration, f.rightFootAcceleration));
        report.maxFootAngularSpeed = frames.Max(f => Mathf.Max(f.leftFootAngularSpeed, f.rightFootAngularSpeed));
        report.rootDepthRange = frames.Max(f => f.hips.z) - frames.Min(f => f.hips.z);
        report.maxFrameJointDisplacement = Mathf.Max(report.maxHandSpeed, report.maxFootSpeed) / SampleRate;
        report.maxFrameJointAngle = Mathf.Max(report.maxHandAngularSpeed, report.maxFootAngularSpeed) / SampleRate;
        report.minimumMeshY = frames.Min(f => f.minimumMeshY);
        report.groundedFootSlipPeak = frames.Max(f => Mathf.Max(f.leftSoleY < .03f && f.leftSoleY > -.03f ? f.leftFootSpeed : 0,
            f.rightSoleY < .03f && f.rightSoleY > -.03f ? f.rightFootSpeed : 0));
        if (report.loop)
        {
            int boundary = Mathf.RoundToInt((cycle + lead) * SampleRate);
            bool hasSecondCycle = boundary + 1 < frames.Count;
            Frame a = hasSecondCycle ? frames[boundary - 1] : frames.Last();
            Frame b = hasSecondCycle ? frames[boundary] : frames.First();
            Frame c = hasSecondCycle ? frames[boundary + 1] : frames[1];
            report.leftLoopPositionGap = Vector3.Distance(a.leftFoot, b.leftFoot);
            report.rightLoopPositionGap = Vector3.Distance(a.rightFoot, b.rightFoot);
            report.leftLoopVelocityGap = ((b.leftFoot - a.leftFoot) * SampleRate - (c.leftFoot - b.leftFoot) * SampleRate).magnitude;
            report.rightLoopVelocityGap = ((b.rightFoot - a.rightFoot) * SampleRate - (c.rightFoot - b.rightFoot) * SampleRate).magnitude;
        }
    }

    [MenuItem("Tools/Project M/V004/Render Four Neutral VFX Samples")]
    public static void CaptureVfx()
    {
        string[] names = { "DashBurst", "SlashWave", "MagicMissile", "Explosion" };
        bool keep60 = File.Exists(ConfigurationPath) && JsonUtility.FromJson<Configuration>(File.ReadAllText(ConfigurationPath)).keep60fpsFrames;
        using (var stage = new Stage(960, 540))
            foreach (string name in names)
            {
                string path = "Assets/Art/CombatVFX_v1/Prefabs/" + name + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab) throw new InvalidOperationException("VFX review prefab missing: " + path);
                var obj = Object.Instantiate(prefab); SceneManager.MoveGameObjectToScene(obj, stage.scene);
                try
                {
                    var sample = obj.GetComponents<MonoBehaviour>().FirstOrDefault(c => c.GetType().FullName == "ProjectM.CombatVfxPreview.CombatVfxSample");
                    if (!sample) throw new InvalidOperationException("VFX sample API missing: " + name);
                    Type type = sample.GetType(); MethodInfo evaluate = type.GetMethod("SetPreviewTime", new[] { typeof(float) });
                    float duration = Convert.ToSingle(type.GetProperty("Duration").GetValue(sample));
                    float speed = Convert.ToSingle(type.GetProperty("TravelSpeed").GetValue(sample));
                    if (evaluate == null || duration <= 0) throw new InvalidOperationException("Invalid VFX review duration/API " + name);
                    obj.transform.SetPositionAndRotation(new Vector3(0, .002f, 0), Quaternion.LookRotation(Vector3.right));
                    stage.SetContactGuide(Vector3.zero, 0, false);
                    string frames30 = ReviewDirectory + "/VFX/Frames30/" + name, frames60 = ReviewDirectory + "/VFX/Frames60/" + name;
                    ClearPngs(frames30); if (keep60) ClearPngs(frames60);
                    Vector3 target = new Vector3(name == "DashBurst" ? 1 : name == "SlashWave" ? 2.6f : name == "MagicMissile" ? 2.5f : 0, name == "DashBurst" ? .9f : 1, 0);
                    stage.Frame(new Bounds(target, new Vector3(name == "Explosion" ? 7.5f : 8, 3.6f, 2)), name == "Explosion" ? 2.5f : 2.8f);
                    int count = Mathf.CeilToInt((duration + .15f) * SampleRate), out30 = 0;
                    for (int i = 0; i < count; i++)
                    {
                        evaluate.Invoke(sample, new object[] { i / (float)SampleRate });
                        if (keep60) stage.Save(frames60 + "/" + i.ToString("0000") + ".png");
                        if (i % 2 == 0) { stage.Save(frames30 + "/" + out30.ToString("0000") + ".png"); out30++; }
                    }
                    Directory.CreateDirectory(ReviewDirectory + "/VFX");
                    File.WriteAllText(ReviewDirectory + "/VFX/" + name + "-review.json", JsonUtility.ToJson(new VfxReport
                    {
                        name = name, prefab = path, frames30Directory = frames30, frames60Directory = keep60 ? frames60 : "", duration = duration, travelSpeed = speed,
                        frames30 = out30, frames60 = keep60 ? count : 0, sampleRate = SampleRate, gifRate = GifRate, width = 960, height = 540,
                        cameraPosition = stage.camera.transform.position, cameraTarget = stage.camera.transform.position - ViewOffset,
                        orthographicSize = stage.camera.orthographicSize,
                        method = "Actual isolated CombatVfxSample prefab SetPreviewTime at 60Hz; 30fps PNG samples. Neutral floor/lighting, effect local +Z rotated to gameplay +X. No gameplay or environment scene is loaded or modified."
                    }, true));
                }
                finally { Object.DestroyImmediate(obj); }
            }
        Debug.Log("V004_VFX_REVIEW_CAPTURE_SUCCESS");
    }

    [MenuItem("Tools/Project M/V004/Render Fist and Weapon Grip Close-ups")]
    public static void CaptureHands()
    {
        var config = JsonUtility.FromJson<Configuration>(File.ReadAllText(ConfigurationPath));
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(config.prefabPath);
        AnimationClip clip = LoadClip(config.idleClipPath, config.idleClipName);
        if (!prefab) throw new InvalidOperationException("Build the new v004 review prefab before hand capture.");
        var shots = new List<HandShot>();
        using (var stage = new Stage(640, 640))
        {
            stage.SetContactGuide(Vector3.zero, 0, false);
            var placement = stage.New("V004 Disposable Hand Review Placement");
            placement.transform.SetPositionAndRotation(Vector3.up * .002f, Quaternion.LookRotation(Vector3.right));
            var actor = Object.Instantiate(prefab); SceneManager.MoveGameObjectToScene(actor, stage.scene);
            actor.transform.SetParent(placement.transform, false); actor.transform.localPosition = Vector3.zero; actor.transform.localRotation = Quaternion.identity;
            var graph = default(PlayableGraph); var snapshots = new List<SkinSnapshot>();
            try
            {
                foreach (var behavior in actor.GetComponentsInChildren<MonoBehaviour>(true)) behavior.enabled = false;
                foreach (var collider in actor.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                foreach (var body in actor.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.useGravity = false; }
                Animator animator = actor.GetComponent<Animator>() ?? actor.GetComponentInChildren<Animator>(true);
                if (!animator) throw new InvalidOperationException("Hand review Animator is missing.");
                foreach (var other in actor.GetComponentsInChildren<Animator>(true)) if (other != animator) other.enabled = false;
                animator.enabled = true; animator.runtimeAnimatorController = null; animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.Rebind(); animator.Update(0);
                Transform left = Bone(animator, HumanBodyBones.LeftHand, "LeftHand", "hand_l"), right = Bone(animator, HumanBodyBones.RightHand, "RightHand", "hand_r");
                if (!left || !right) throw new InvalidOperationException("Both hand bones are required for close-up review.");
                foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)) snapshots.Add(new SkinSnapshot(skin, null, null));
                graph = PlayableGraph.Create("V004 Actual Hand Geometry Review"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimationClipPlayable.Create(graph, clip); playable.SetApplyFootIK(false); playable.SetApplyPlayableIK(false);
                AnimationPlayableOutput.Create(graph, "Actual Idle", animator).SetSourcePlayable(playable); graph.Play();
                foreach (bool isLeft in new[] { false, true }) foreach (float weight in new[] { 100f, 75f }) foreach (bool front in new[] { false, true })
                {
                    playable.SetTime(clip.length * .35f); graph.Evaluate(0);
                    foreach (var snapshot in snapshots)
                    {
                        foreach (string shape in new[] { "Grip_R", "Grip_L" })
                        {
                            int index = snapshot.source.sharedMesh.GetBlendShapeIndex(shape);
                            if (index >= 0) snapshot.source.SetBlendShapeWeight(index, weight);
                        }
                        snapshot.Bake();
                    }
                    Transform hand = isLeft ? left : right;
                    Vector3 center = hand.TransformPoint(new Vector3(0, .075f, 0));
                    Vector3 offset = front ? new Vector3(3, .14f, isLeft ? .12f : -.12f) : new Vector3(.12f, .14f, isLeft ? 3 : -3);
                    stage.camera.transform.position = center + offset; stage.camera.transform.LookAt(center); stage.camera.orthographicSize = .18f;
                    string name = (isLeft ? "Left" : "Right") + "_" + Mathf.RoundToInt(weight) + "_" + (front ? "Front" : "Side");
                    string path = ReviewDirectory + "/Hands/" + name + ".png"; stage.Save(path);
                    shots.Add(new HandShot { hand = isLeft ? "Left" : "Right", view = front ? "Front" : "Side", image = path,
                        gripWeight = weight, orthographicSize = stage.camera.orthographicSize,
                        cameraPosition = stage.camera.transform.position, target = center,
                        mesh = string.Join(";", snapshots.Select(s => AssetDatabase.GetAssetPath(s.source.sharedMesh))) });
                }
            }
            finally { if (graph.IsValid()) graph.Destroy(); foreach (var snapshot in snapshots) snapshot.Dispose(); Object.DestroyImmediate(placement); }
        }
        Directory.CreateDirectory(ReviewDirectory + "/Hands");
        File.WriteAllText(ReviewDirectory + "/Hands/hand-review.json", JsonUtility.ToJson(new HandReport
        {
            prefab = config.prefabPath, clip = config.idleClipPath,
            method = "The new saved v004 review mesh only. Exact CPU BakeMesh at authored Grip_R/L=100 fist or the separately authored 75 weapon-grip frame, after actual Idle clip evaluation. Both hands viewed from outer side and front. Disposable renderer/mesh snapshots; no original mesh, prefab, controller or gameplay asset is modified.",
            shots = shots.ToArray()
        }, true));
        Debug.Log("V004_HAND_CLOSEUP_CAPTURE_SUCCESS " + shots.Count);
    }
    public static void CaptureAll() { Capture(); CaptureVfx(); CaptureHands(); }
    public static void CaptureFinalReview() { ConfigureFinalReview(); CaptureAll(); }
}
#endif
