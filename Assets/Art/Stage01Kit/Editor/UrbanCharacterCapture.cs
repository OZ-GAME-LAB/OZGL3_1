#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Disposable Humanoid pose capture; never changes character, clip or equipment assets.</summary>
public static class UrbanCharacterCapture
{
    const string Root = "Assets/Art/Stage01Kit";
    const string Hero = "Assets/Art/Characters/Protagonist01/Runtime";
    const string Output = "ArtSources/Stage01Kit";
    const int TileWidth = 480, TileHeight = 600, Columns = 4, Rows = 3;

    [Serializable] sealed class Shot
    {
        public string title, clip, weapon, prop;
        public bool closeup;
        public float normalized;
        public Vector3 handPosition, weaponGripPosition, weaponTipPosition;
        public Vector3 mouthTarget, bottleMouth;
        public float bottleMouthGap;
        public bool humanoid, socketBound;
        public bool overrideGripRotation;
        public Vector3 gripEuler, rightHandWorldX, rightHandWorldY, rightHandWorldZ;
        public Quaternion rightHandLocalRotation, rightHandWorldRotation;
        public int column, row;
        public Shot(string title, string clip, string weapon, string prop, float normalized, bool closeup = false)
        { this.title = title; this.clip = clip; this.weapon = weapon; this.prop = prop; this.normalized = normalized; this.closeup = closeup; }
    }

    [MenuItem("Tools/Project M/Capture Weapon Grip Rotation Candidates")]
    public static void CaptureGripCandidates()
    {
        Directory.CreateDirectory(Output);
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewPreviewScene();
        var camera = new GameObject("Grip Candidate QA Camera", typeof(Camera)).GetComponent<Camera>();
        SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
        camera.scene = scene;
        camera.orthographic = true;
        camera.nearClipPlane = .01f; camera.farClipPlane = 30;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.86f, .89f, .91f);
        camera.allowHDR = false;
        var sun = new GameObject("Grip Candidate QA Key", typeof(Light)).GetComponent<Light>();
        SceneManager.MoveGameObjectToScene(sun.gameObject, scene);
        sun.type = LightType.Directional; sun.intensity = 1.2f;
        sun.transform.rotation = Quaternion.Euler(36, -32, 0);
        var target = new RenderTexture(TileWidth, TileHeight, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        target.Create(); camera.targetTexture = target;
        var sheet = new Texture2D(TileWidth * 5, TileHeight * 2, TextureFormat.RGB24, false);
        var oldTarget = RenderTexture.active;
        bool oldAsync = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        var rotations = new[] { new Vector3(0, 0, 90), new Vector3(0, 0, -90), new Vector3(90, 0, 0), new Vector3(-90, 0, 0), new Vector3(0, 0, 180) };
        var labels = new[] { "Z +90", "Z -90", "X +90", "X -90", "Z 180" };
        var shots = new List<Shot>();
        try
        {
            var clips = LoadClips();
            for (int row = 0; row < 2; row++)
                for (int col = 0; col < 5; col++)
                {
                    string weapon = row == 0 ? "Sword" : "Staff";
                    var shot = new Shot(weapon.ToUpperInvariant() + " / " + labels[col], "Idle", weapon, "", .42f)
                    {
                        row = row, column = col, overrideGripRotation = true, gripEuler = rotations[col]
                    };
                    CaptureShot(scene, camera, target, sheet, clips, shot, 2);
                    shots.Add(shot);
                }
            sheet.Apply();
            string path = Output + "/character-grip-candidates.png";
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            File.WriteAllText(Output + "/character-grip-candidates.json", JsonUtility.ToJson(new Report
            {
                image = path, tileWidth = TileWidth, tileHeight = TileHeight, columns = 5, rows = 2, shots = shots.ToArray(),
                method = "Root Humanoid Idle at .42. Disposable weapon localRotation override only; original prefabs unchanged. Row 0 Sword, row 1 Staff. Columns Z+90, Z-90, X+90, X-90, Z180. RightHand local/world quaternions and three world-space basis axes recorded per shot."
            }, true));
            Debug.Log("URBAN_GRIP_CANDIDATES_SUCCESS " + path);
        }
        finally
        {
            RenderTexture.active = oldTarget; ShaderUtil.allowAsyncCompilation = oldAsync;
            camera.targetTexture = null; Object.DestroyImmediate(target); Object.DestroyImmediate(sheet);
            EditorSceneManager.ClosePreviewScene(scene);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }
    [Serializable] sealed class Report
    {
        public string image, method;
        public int tileWidth, tileHeight, columns, rows;
        public Shot[] shots;
    }

    [MenuItem("Tools/Project M/Capture Equipped Character and Extra Actions")]
    public static void Capture()
    {
        Directory.CreateDirectory(Output);
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewPreviewScene();
        var camera = new GameObject("Character QA Camera", typeof(Camera)).GetComponent<Camera>();
        SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
        camera.orthographic = true;
        camera.nearClipPlane = .01f;
        camera.farClipPlane = 30;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.86f, .89f, .91f);
        camera.allowHDR = false;
        camera.allowMSAA = true;
        camera.scene = scene;
        var sun = new GameObject("Character QA Key", typeof(Light)).GetComponent<Light>();
        SceneManager.MoveGameObjectToScene(sun.gameObject, scene);
        sun.type = LightType.Directional;
        sun.intensity = 1.2f;
        sun.transform.rotation = Quaternion.Euler(36, -32, 0);
        sun.shadows = LightShadows.None;
        var fill = new GameObject("Character QA Fill", typeof(Light)).GetComponent<Light>();
        SceneManager.MoveGameObjectToScene(fill.gameObject, scene);
        fill.type = LightType.Directional; fill.intensity = .25f;
        fill.transform.rotation = Quaternion.Euler(24, 155, 0);
        var target = new RenderTexture(TileWidth, TileHeight, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        var sheet = new Texture2D(TileWidth * Columns, TileHeight * Rows, TextureFormat.RGB24, false);
        var oldTarget = RenderTexture.active;
        bool oldAsyncCompilation = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        target.Create();
        camera.targetTexture = target;
        var shots = new[]
        {
            new Shot("SWORD / IDLE", "Idle", "Sword", "", .42f),
            new Shot("SWORD / SLASH", "SwordSlash", "Sword", "", .55f),
            new Shot("STAFF / IDLE", "Idle", "Staff", "", .42f),
            new Shot("STAFF / MAGIC CAST", "MagicCast", "Staff", "", .48f),
            new Shot("SWORD / GRIP", "Idle", "Sword", "", .42f, true),
            new Shot("STAFF / GRIP", "Idle", "Staff", "", .42f, true),
            new Shot("DRINK / LEFT HAND", "Drink", "", "Potion_Health", .45f),
            new Shot("THROW / WIND UP", "Throw", "", "Potion_Attack", .33f),
            new Shot("INTERACT", "Interact", "", "", .5f),
            new Shot("PICK UP", "Pickup", "", "", .36f),
            new Shot("DRINK / GRIP", "Drink", "", "Potion_Health", .45f, true),
            new Shot("THROW / RELEASE", "Throw", "", "", .52f)
        };
        try
        {
            var clips = LoadClips();
            for (int i = 0; i < shots.Length; i++)
            {
                Shot shot = shots[i];
                shot.column = i % Columns; shot.row = i / Columns;
                CaptureShot(scene, camera, target, sheet, clips, shot);
            }
            sheet.Apply();
            string path = Output + "/character-actions-contact.png";
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            File.WriteAllText(Output + "/character-actions-capture.json", JsonUtility.ToJson(new Report
            {
                image = path, tileWidth = TileWidth, tileHeight = TileHeight, columns = Columns, rows = Rows, shots = shots,
                method = "Actual root Humanoid Animator driven by manual AnimationClipPlayable; nested disabled Animator removed in disposable scene. Row 0 is top of image. Weapon prefab grip offsets preserved. Drink bottle uses the calibrated ConsumableHandProp on LeftHand/WeaponSocket_Left and the saved contact normalized time."
            }, true));
            Debug.Log("URBAN_CHARACTER_CAPTURE_SUCCESS " + path);
        }
        finally
        {
            RenderTexture.active = oldTarget;
            ShaderUtil.allowAsyncCompilation = oldAsyncCompilation;
            camera.targetTexture = null;
            Object.DestroyImmediate(target); Object.DestroyImmediate(sheet);
            EditorSceneManager.ClosePreviewScene(scene);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    static Dictionary<string, AnimationClip> LoadClips()
    {
        var clips = new Dictionary<string, AnimationClip>();
        foreach (string name in new[] { "Protagonist01_Actions.fbx", "Protagonist01_ExtendedActions.fbx" })
            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(Hero + "/" + name).OfType<AnimationClip>())
                if (!clip.name.StartsWith("__preview__", StringComparison.Ordinal)) clips[clip.name] = clip;
        return clips;
    }

    static void CaptureShot(Scene scene, Camera camera, RenderTexture target, Texture2D sheet,
        Dictionary<string, AnimationClip> clips, Shot shot, int rowCount = Rows)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Hero + "/Protagonist01_Player.prefab");
        if (!asset || !clips.TryGetValue(shot.clip, out var clip)) throw new InvalidOperationException("Missing character capture input: " + shot.clip);
        var actor = Object.Instantiate(asset);
        SceneManager.MoveGameObjectToScene(actor, scene);
        PlayableGraph graph = default;
        try
        {
            actor.name = "Capture_" + shot.title;
            actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0, 155, 0));
            foreach (var behavior in actor.GetComponentsInChildren<MonoBehaviour>(true)) behavior.enabled = false;
            foreach (var body in actor.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.useGravity = false; }
            foreach (var collider in actor.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            var animator = actor.GetComponent<Animator>();
            if (!animator || !animator.avatar || !animator.avatar.isHuman || !animator.avatar.isValid)
                throw new InvalidOperationException("Root Humanoid Animator/avatar missing.");
            // A nested disabled Animator can still claim the skeleton and break root Playables.
            foreach (var nested in actor.GetComponentsInChildren<Animator>(true))
                if (nested != animator) nested.enabled = false;
            animator.enabled = true;
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind(); animator.Update(0);
            foreach (var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)) renderer.updateWhenOffscreen = true;
            Transform right = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            SocketWeapon equipped = null;
            if (!string.IsNullOrEmpty(shot.weapon))
            {
                var weaponAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Weapons/" + shot.weapon + ".prefab");
                if (!weaponAsset) throw new InvalidOperationException("Missing capture weapon " + shot.weapon);
                equipped = Object.Instantiate(weaponAsset).GetComponent<SocketWeapon>();
                SceneManager.MoveGameObjectToScene(equipped.gameObject, scene);
                var socket = right.GetComponentsInChildren<Transform>(true).First(t => t.name == "WeaponSocket");
                equipped.AttachTo(socket);
                if (shot.overrideGripRotation) equipped.transform.localRotation = Quaternion.Euler(shot.gripEuler);
                shot.socketBound = equipped.transform.parent == socket;
            }
            Transform focusHand = shot.clip == "Drink" ? left : right;
            GameObject heldProp = null;
            if (!string.IsNullOrEmpty(shot.prop))
            {
                var propAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Props/" + shot.prop + ".prefab");
                if (!propAsset) throw new InvalidOperationException("Missing capture prop " + shot.prop);
                var bottle = Object.Instantiate(propAsset);
                heldProp = bottle;
                SceneManager.MoveGameObjectToScene(bottle, scene);
                var socket = focusHand.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name == (shot.clip == "Drink" ? "WeaponSocket_Left" : "WeaponSocket")) ?? focusHand;
                var calibratedMount = bottle.GetComponent<ConsumableHandProp>();
                if (shot.clip == "Drink")
                {
                    if (!calibratedMount) throw new InvalidOperationException("Calibrate potion hand prop before character capture.");
                    calibratedMount.AttachTo(socket);
                }
                else
                {
                    bottle.transform.SetParent(socket, false);
                    bottle.transform.localPosition = new Vector3(0, .02f, 0);
                    bottle.transform.localRotation = Quaternion.identity;
                }
                foreach (var collider in bottle.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                shot.socketBound = bottle.transform.parent == socket;
            }
            graph = PlayableGraph.Create("Character QA " + shot.clip);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(false); playable.SetApplyPlayableIK(false);
            AnimationPlayableOutput.Create(graph, "Pose", animator).SetSourcePlayable(playable);
            graph.Play();
            if (shot.clip == "Drink")
                shot.normalized = UrbanConsumableBuilder.ReadContactTime(shot.normalized);
            playable.SetTime(clip.length * shot.normalized); graph.Evaluate(0);
            actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0, 155, 0));
            shot.humanoid = animator.isHuman && clip.humanMotion;
            shot.handPosition = focusHand.position;
            shot.rightHandLocalRotation = right.localRotation;
            shot.rightHandWorldRotation = right.rotation;
            shot.rightHandWorldX = right.right;
            shot.rightHandWorldY = right.up;
            shot.rightHandWorldZ = right.forward;
            if (shot.clip == "Drink" && heldProp)
            {
                shot.mouthTarget = UrbanConsumableBuilder.MouthTarget(head);
                shot.bottleMouth = heldProp.transform.TransformPoint(Vector3.up * .237f);
                shot.bottleMouthGap = Vector3.Distance(shot.mouthTarget, shot.bottleMouth);
            }
            if (equipped)
            {
                shot.weaponGripPosition = equipped.transform.position;
                var tip = equipped.transform.Find("EffectTip");
                shot.weaponTipPosition = tip ? tip.position : equipped.transform.position;
            }
            Bounds bounds = PoseBounds(actor);
            Vector3 center = shot.closeup ? focusHand.position + (equipped ? equipped.transform.up * .09f : Vector3.up * .07f) : bounds.center;
            Vector3 direction = actor.transform.TransformDirection(new Vector3(.32f, .08f, 1)).normalized;
            camera.transform.position = center + direction * 6;
            camera.transform.LookAt(center);
            if (shot.closeup) camera.orthographicSize = .34f;
            else
            {
                float maxY = 0, maxX = 0;
                foreach (var corner in Corners(bounds))
                {
                    Vector3 delta = corner - center;
                    maxY = Mathf.Max(maxY, Mathf.Abs(Vector3.Dot(camera.transform.up, delta)));
                    maxX = Mathf.Max(maxX, Mathf.Abs(Vector3.Dot(camera.transform.right, delta)));
                }
                camera.orthographicSize = Mathf.Max(.98f, maxY * 1.12f, maxX / ((float)TileWidth / TileHeight) * 1.12f);
            }
            camera.Render(); // Warm this exact mesh/material/lighting variant before reading it.
            camera.Render(); RenderTexture.active = target;
            sheet.ReadPixels(new Rect(0, 0, TileWidth, TileHeight), shot.column * TileWidth, (rowCount - shot.row - 1) * TileHeight);
        }
        finally
        {
            if (graph.IsValid()) graph.Destroy();
            Object.DestroyImmediate(actor);
        }
    }

    static Bounds PoseBounds(GameObject actor)
    {
        bool first = true; Bounds bounds = default;
        foreach (var renderer in actor.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            if (renderer is SkinnedMeshRenderer skinned)
            {
                var baked = new Mesh();
                try
                {
                    skinned.BakeMesh(baked);
                    foreach (var vertex in baked.vertices)
                    {
                        Vector3 p = skinned.transform.TransformPoint(vertex);
                        if (first) { bounds = new Bounds(p, Vector3.zero); first = false; } else bounds.Encapsulate(p);
                    }
                }
                finally { Object.DestroyImmediate(baked); }
            }
            else if (first) { bounds = renderer.bounds; first = false; }
            else bounds.Encapsulate(renderer.bounds);
        }
        if (first) throw new InvalidOperationException("No visible capture geometry.");
        return bounds;
    }

    static IEnumerable<Vector3> Corners(Bounds b)
    {
        for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                    yield return b.center + Vector3.Scale(b.extents, new Vector3(x, y, z));
    }
}
#endif
