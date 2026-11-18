#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Static geometry and asset validation for the three-height Stage 01 route.
/// Camera renders use MainScene lighting. They are explicitly not gameplay proof.
/// </summary>
public static class Stage01VerticalAssetQA
{
    const string StagePath = "Assets/2.Prefab/CoreSystem/stage.prefab";
    const string PlayerPath = "Assets/2.Prefab/Player/Player_Chibi.prefab";

    [Serializable]
    sealed class Report
    {
        public string utc, project, prefab, scope = "STATIC asset/collision inspection and scene-camera previews; no gameplay claims";
        public int rendererCount, colliderCount, supportSamples, clearanceSamples, spawnAnchors;
        public float jumpImpulse, airSpeed, theoreticalApex;
        public List<string> checks = new List<string>();
        public List<string> errors = new List<string>();
        public List<string> warnings = new List<string>();
        public List<string> captures = new List<string>();
        public List<CaptureReadback> captureReadbacks = new List<CaptureReadback>();
    }

    [Serializable]
    sealed class CaptureReadback
    {
        public string path;
        public int warmupRenders = 2, width, height, sampledPixels;
        public float meanRed, meanGreen, meanBlue, meanLuminance;
    }

    [MenuItem("OZ/Level Design/Validate Vertical Stage 01 Assets")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run static asset QA outside play mode.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string output = Environment.GetEnvironmentVariable("OZ_STAGE_QA_DIR");
        output = Path.GetFullPath(string.IsNullOrWhiteSpace(output) ? Path.Combine(Path.GetTempPath(), "OZStage01VerticalAssets") : output);
        Directory.CreateDirectory(output);
        var report = new Report { utc = DateTime.UtcNow.ToString("O"), project = Application.dataPath, prefab = StagePath };
        GameObject root = null;
        Camera camera = null;
        Vector3 oldPosition = Vector3.zero;
        Quaternion oldRotation = Quaternion.identity;
        float oldSize = 0;
        bool oldOrtho = false;
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity", OpenSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StagePath);
            if (prefab == null) throw new InvalidOperationException("Stage prefab missing: " + StagePath);
            root = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            root.SetActive(true);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Physics.SyncTransforms();
            CheckAssets(root, report);
            CheckRoute(root, report);
            CheckProgression(root, report);
            CheckLadders(root, report);
            CheckJumpMetrics(report);

            camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("MainScene camera missing; preview requires actual scene camera settings.");
            oldPosition = camera.transform.position;
            oldRotation = camera.transform.rotation;
            oldSize = camera.orthographicSize;
            oldOrtho = camera.orthographic;
            var layout = root.GetComponent<StageLayout>();
            if (layout == null) throw new InvalidOperationException("StageLayout missing.");
            var settings = new SerializedObject(layout);
            float size = settings.FindProperty("cameraSize").floatValue;
            float height = settings.FindProperty("cameraHeight").floatValue;
            float pitch = settings.FindProperty("cameraPitch").floatValue;
            var previews = new[]
            {
                new { label = "static_01_market_street", x = 14f, y = 0f },
                new { label = "static_02_climb_rooftop", x = 32f, y = 2.25f },
                new { label = "static_03_rooftop_arena", x = 45f, y = 4.5f },
                new { label = "static_04_high_roof_arena", x = 76f, y = 8.5f },
                new { label = "static_05_boss_descent", x = 96f, y = 0f }
            };
            foreach (var preview in previews)
            {
                camera.orthographic = true;
                camera.orthographicSize = size;
                camera.transform.rotation = Quaternion.Euler(pitch, 0, 0);
                float halfWidth = size * (16f / 9f);
                float x = Mathf.Clamp(preview.x + 2f, layout.HorizontalBounds.x + halfWidth, layout.HorizontalBounds.y - halfWidth);
                float y = height - oldPosition.z * Mathf.Tan(pitch * Mathf.Deg2Rad) + Mathf.Max(0, preview.y - 2.5f) * .6f;
                camera.transform.position = new Vector3(x, y, oldPosition.z);
                string path = Path.Combine(output, preview.label + ".png");
                CheckReadback(Capture(camera, path, 1920, 1080), report);
                report.captures.Add(path);
            }
            camera.orthographic = true;
            camera.orthographicSize = 11f;
            camera.transform.position = new Vector3(51, 23, -19);
            camera.transform.LookAt(new Vector3(51, 4, 3));
            string depthView = Path.Combine(output, "static_06_roof_depth.png");
            CheckReadback(Capture(camera, depthView, 1920, 1080), report);
            report.captures.Add(depthView);
            report.checks.Add("Additional elevated art inspection checks rear roof surfaces, prop supports and facade/deck depth; it is not the gameplay camera.");
            camera.orthographic = true;
            camera.orthographicSize = 15f;
            camera.transform.rotation = Quaternion.Euler(pitch, 0, 0);
            camera.transform.position = new Vector3(51, 9f - oldPosition.z * Mathf.Tan(pitch * Mathf.Deg2Rad), oldPosition.z);
            string panorama = Path.Combine(output, "static_00_entire_vertical_route.png");
            CheckReadback(Capture(camera, panorama, 4096, 900), report);
            report.captures.Add(panorama);
            report.checks.Add("Camera previews use the actual MainScene camera, renderer settings and lighting; filenames identify static previews.");
        }
        catch (Exception exception)
        {
            report.errors.Add(exception.ToString());
            Debug.LogException(exception);
        }
        finally
        {
            if (camera != null)
            {
                camera.transform.SetPositionAndRotation(oldPosition, oldRotation);
                camera.orthographicSize = oldSize;
                camera.orthographic = oldOrtho;
            }
            if (root != null) Object.DestroyImmediate(root);
            File.WriteAllText(Path.Combine(output, "vertical_asset_report.json"), JsonUtility.ToJson(report, true));
        }
        if (report.errors.Count > 0)
            throw new InvalidOperationException("Vertical Stage01 static QA failed (" + report.errors.Count + "). See " + Path.Combine(output, "vertical_asset_report.json"));
        Debug.Log("[Stage01VerticalAssetQA] Passed " + report.supportSamples + " support and " + report.clearanceSamples
            + " body-clearance samples, " + report.spawnAnchors + " spawn anchors. Static previews: " + output);
    }

    static void CheckAssets(GameObject root, Report report)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        report.rendererCount = renderers.Length;
        report.colliderCount = root.GetComponentsInChildren<Collider>(true).Length;
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0,
                "Missing script: " + Hierarchy(transform), report);
            foreach (Component component in transform.GetComponents<Component>())
            {
                if (component == null) continue;
                var serialized = new SerializedObject(component);
                SerializedProperty property = serialized.GetIterator();
                while (property.NextVisible(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference)
                        Require(property.objectReferenceValue != null || property.objectReferenceInstanceIDValue == 0,
                            "Broken object reference: " + Hierarchy(transform) + "." + property.propertyPath, report);
            }
        }
        foreach (Renderer renderer in renderers)
        {
            Require(renderer.sharedMaterials.Length > 0, "No material slots: " + Hierarchy(renderer.transform), report);
            foreach (Material material in renderer.sharedMaterials)
            {
                Require(material != null, "Missing material: " + Hierarchy(renderer.transform), report);
                if (material == null) continue;
                Require(material.shader != null && material.shader.name != "Hidden/InternalErrorShader",
                    "Missing/error shader: " + material.name, report);
                if (material.shader != null && !material.shader.isSupported)
                    report.warnings.Add("Shader reports unsupported in current graphics device: " + material.shader.name);
            }
            if (renderer is MeshRenderer)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                Require(filter != null && filter.sharedMesh != null, "Missing mesh: " + Hierarchy(renderer.transform), report);
            }
            if (renderer is SkinnedMeshRenderer skinned)
                Require(skinned.sharedMesh != null, "Missing skinned mesh: " + Hierarchy(renderer.transform), report);
        }
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
        {
            Require(IsFinite(collider.transform.position) && IsFinite(collider.transform.lossyScale),
                "Non-finite collider transform: " + Hierarchy(collider.transform), report);
            if (collider is MeshCollider mesh)
                Require(mesh.sharedMesh != null, "Mesh collider has no mesh: " + Hierarchy(collider.transform), report);
        }
        report.checks.Add("All authored objects inspected for missing scripts/references, renderer meshes/materials/shaders and physical collider data.");
    }

    static void CheckRoute(GameObject root, Report report)
    {
        // The continuous street is a recovery route below all elevated arenas.
        SampleRoute(root, -11.5f, 113.5f, 0f, "street recovery lane", report);
        SampleRoute(root, 26.4f + .35f, 32.8f - .35f, 2.25f, "first approach step", report);
        SampleRoute(root, 34f + .35f, 58.5f - .35f, 4.5f, "first rooftop arena", report);
        SampleRoute(root, 59.2f + .35f, 64.5f - .35f, 6.5f, "second approach step", report);
        SampleRoute(root, 66f + .35f, 88.5f - .35f, 8.5f, "high rooftop arena", report);
        SampleRoute(root, 88.5f + .35f, 93.5f - .35f, 4.5f, "descent deck", report);
        report.checks.Add("Street and all five authored upper-route surfaces sampled every 0.25m for supported feet and a 1.295m player body at z=0.");
    }

    static void SampleRoute(GameObject root, float from, float to, float y, string label, Report report)
    {
        for (float x = from; x <= to + .001f; x += .25f)
        {
            report.supportSamples++;
            Require(HasSupport(root, new Vector3(x, y, 0)), label + " unsupported at x=" + x.ToString("F2"), report);
            CheckClearance(root, new Vector3(x, y, 0), label, report);
        }
    }

    static bool IsStageSolid(Collider collider, GameObject root)
    {
        return collider != null && collider.enabled && !collider.isTrigger && collider.gameObject.activeInHierarchy
            && collider.transform.IsChildOf(root.transform) && collider.GetComponentInParent<StageEncounterBarrier>() == null;
    }

    static bool HasSupport(GameObject root, Vector3 feet)
    {
        var ray = new Ray(feet + Vector3.up * .3f, Vector3.down);
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            if (IsStageSolid(collider, root) && collider.Raycast(ray, out RaycastHit hit, .6f)
                && Mathf.Abs(hit.point.y - feet.y) <= .075f && hit.normal.y >= .85f) return true;
        return false;
    }

    static void CheckClearance(GameObject root, Vector3 feet, string label, Report report)
    {
        report.clearanceSamples++;
        Collider[] overlaps = Physics.OverlapCapsule(feet + Vector3.up * .32f, feet + Vector3.up * 1.035f,
            .275f, ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider collider in overlaps)
            if (IsStageSolid(collider, root))
                Require(false, label + " capsule blocked at " + feet + " by " + Hierarchy(collider.transform), report);
    }

    static void CheckProgression(GameObject root, Report report)
    {
        Stage stage = root.GetComponent<Stage>();
        StageLayout layout = root.GetComponent<StageLayout>();
        Require(stage != null && layout != null, "Stage or StageLayout missing.", report);
        if (stage == null || layout == null) return;
        Require(layout.PlayerSpawn != null, "Player spawn missing.", report);
        if (layout.PlayerSpawn != null)
        {
            Require(HasSupport(root, layout.PlayerSpawn.position), "Player start unsupported.", report);
            CheckClearance(root, layout.PlayerSpawn.position, "Player start", report);
        }
        Require(stage.Gates.Count == 3, "Expected three normal encounters.", report);
        Vector3[] expected = { new Vector3(14, 0, 0), new Vector3(45, 4.5f, 0), new Vector3(76, 8.5f, 0) };
        for (int index = 0; index < stage.Gates.Count; index++)
        {
            StageGate gate = stage.Gates[index];
            Require(gate != null, "Missing encounter " + index, report);
            if (gate == null) continue;
            if (index < expected.Length)
                Require(Vector3.Distance(gate.transform.position, expected[index]) <= .15f,
                    "Encounter " + (index + 1) + " must be at " + expected[index] + "; found " + gate.transform.position, report);
            Require(HasSupport(root, gate.transform.position), "Gate unsupported: " + gate.name, report);
            if (gate is Gate normal)
            {
                var settings = new SerializedObject(normal);
                Require(settings.FindProperty("activationDistance").floatValue > 0, "Encounter starts before approach: " + gate.name, report);
                Require(settings.FindProperty("spawnInterval").floatValue <= .7f, "Slow encounter burst: " + gate.name, report);
                // Deliberately smaller than the 4m between rooftop levels.
                Require(settings.FindProperty("activationHeight").floatValue < 3.9f,
                    "Gate can trigger from street/below its intended deck: " + gate.name, report);
            }
            CheckSpawns(root, gate.GetComponentInChildren<MonsterSpawner>(true), gate.transform.position.y, report);
        }
        Require(stage.BossGate != null, "Boss gate missing.", report);
        if (stage.BossGate != null)
        {
            GameObject playerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            foreach (MonoBehaviour attack in playerAsset.GetComponents<MonoBehaviour>())
            {
                if (!(attack is PunchAttack) && !(attack is SwordComboAttack)) continue;
                int targets = new SerializedObject(attack).FindProperty("_enemyLayer").intValue;
                Require((targets & (1 << stage.BossGate.gameObject.layer)) != 0,
                    "Boss portal cannot receive " + attack.GetType().Name + " hits because its authored layer is outside the weapon target mask.", report);
            }
            Require(Vector3.Distance(stage.BossGate.transform.position, new Vector3(104, 0, 0)) <= .15f,
                "Boss gate must sit on the final street arena at (104,0,0).", report);
            CheckSpawns(root, stage.BossGate.GetComponentInChildren<MonsterSpawner>(true), 0, report);
        }
        report.checks.Add("Gate order and elevations, vertical activation bands, player start and every normal/boss spawn anchor checked against actual collider tops.");
    }

    static void CheckSpawns(GameObject root, MonsterSpawner spawner, float expectedY, Report report)
    {
        Require(spawner != null, "Gate spawner missing.", report);
        if (spawner == null) return;
        var settings = new SerializedObject(spawner);
        SerializedProperty points = settings.FindProperty("spawnPoints");
        Require(points != null && points.arraySize > 0, "No authored spawn anchors: " + spawner.name, report);
        Require(settings.FindProperty("alignFeetToSpawnPoint").boolValue, "Spawn must align enemy feet to deck: " + spawner.name, report);
        if (points == null) return;
        for (int i = 0; i < points.arraySize; i++)
        {
            var point = points.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
            Require(point != null, "Missing spawn anchor: " + spawner.name + " #" + i, report);
            if (point == null) continue;
            report.spawnAnchors++;
            Require(Mathf.Abs(point.position.y - expectedY) <= .075f && Mathf.Abs(point.position.z) <= .075f,
                "Enemy feet must share intended arena plane: " + point.name + " " + point.position, report);
            Require(HasSupport(root, point.position), "Enemy spawn unsupported: " + point.name, report);
            CheckClearance(root, point.position, "Spawn " + point.name, report);
        }
    }

    static void CheckLadders(GameObject root, Report report)
    {
        Ladder[] ladders = root.GetComponentsInChildren<Ladder>(true);
        Require(ladders.Length >= 2, "Two ladder alternatives required.", report);
        float[] centers = { 33.5f, 65.5f };
        float[] exits = { 34.45f, 66.45f };
        float[] tops = { 4.5f, 8.5f };
        for (int i = 0; i < centers.Length; i++)
        {
            Ladder ladder = ladders.OrderBy(l => Mathf.Abs(l.transform.position.x - centers[i])).FirstOrDefault();
            if (ladder == null) continue;
            var trigger = ladder.GetComponent<BoxCollider>();
            var exit = new SerializedObject(ladder).FindProperty("_topExit").objectReferenceValue as Transform;
            Require(trigger != null && trigger.enabled && trigger.isTrigger, "Ladder requires authored enabled trigger: " + ladder.name, report);
            Require(Mathf.Abs(ladder.transform.position.x - centers[i]) <= .1f, "Missing ladder at x=" + centers[i], report);
            Require(exit != null, "Ladder TopExit missing: " + ladder.name, report);
            if (exit == null) continue;
            Require(Mathf.Abs(exit.position.x - exits[i]) <= .15f && Mathf.Abs(exit.position.y - tops[i]) <= .075f,
                "Ladder exit must land inside its roof: " + ladder.name + " " + exit.position, report);
            Require(HasSupport(root, exit.position), "Ladder exit unsupported: " + ladder.name, report);
            CheckClearance(root, exit.position, "Ladder exit " + ladder.name, report);
            if (trigger != null)
            {
                Require(trigger.bounds.max.y >= tops[i] - .3f, "Ladder trigger does not reach exit: " + ladder.name, report);
                Require(trigger.bounds.size.x >= .65f && trigger.bounds.size.z >= 1,
                    "Ladder grab volume too narrow: " + ladder.name, report);
            }
        }
        report.checks.Add("Both ladder triggers, top exits and standing clearance checked; actual climb/regrab/drop behavior requires live input QA.");
    }

    static void CheckJumpMetrics(Report report)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
        var player = prefab != null ? prefab.GetComponent<PlayerController>() : null;
        Require(player != null, "Player prefab movement data missing.", report);
        if (player == null) return;
        var settings = new SerializedObject(player);
        report.jumpImpulse = settings.FindProperty("_jumpForce").floatValue;
        report.airSpeed = settings.FindProperty("_jumpSpeed").floatValue;
        float gravity = -Physics.gravity.y;
        Require(gravity > 0 && report.jumpImpulse > 0 && report.airSpeed > 0, "Invalid configured jump metrics.", report);
        if (gravity <= 0) return;
        report.theoreticalApex = report.jumpImpulse * report.jumpImpulse / (2 * gravity);
        var jumps = new[]
        {
            new Vector2(2.25f, 2.4f), new Vector2(2.25f, 1.2f),
            new Vector2(2f, .7f), new Vector2(2f, 1.5f)
        };
        foreach (Vector2 jump in jumps)
        {
            float discriminant = report.jumpImpulse * report.jumpImpulse - 2 * gravity * jump.x;
            Require(discriminant > 0, "Authored route rise exceeds jump apex: " + jump.x, report);
            if (discriminant <= 0) continue;
            float reach = report.airSpeed * (report.jumpImpulse + Mathf.Sqrt(discriminant)) / gravity;
            Require(reach > jump.y + 1.5f, "Insufficient horizontal landing margin for gap " + jump.y, report);
        }
        report.checks.Add("Four ascent transitions analytically checked using actual player jump/air-speed data; this is reachability analysis, not a live jump test.");
    }

    static CaptureReadback Capture(Camera camera, string path, int width, int height)
    {
        var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;
        float previousAspect = camera.aspect;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.aspect = (float)width / height;
            camera.targetTexture = target;
            // A pooled render target has undefined contents. Also warm URP's first
            // camera render before the captured render (no simulated gameplay).
            RenderTexture.active = target;
            GL.Clear(true, true, camera.backgroundColor);
            camera.Render();
            camera.Render();
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            // ReadPixels synchronizes this target's GPU readback. Record RGB
            // independently so a cold first-render red-only image cannot pass.
            var readback = new CaptureReadback { path = path, width = width, height = height };
            Color32[] pixels = image.GetPixels32();
            int stride = Math.Max(1, pixels.Length / 12000);
            double red = 0, green = 0, blue = 0;
            for (int index = 0; index < pixels.Length; index += stride)
            {
                Color32 pixel = pixels[index];
                red += pixel.r; green += pixel.g; blue += pixel.b;
                readback.sampledPixels++;
            }
            double divisor = readback.sampledPixels * 255.0;
            readback.meanRed = (float)(red / divisor);
            readback.meanGreen = (float)(green / divisor);
            readback.meanBlue = (float)(blue / divisor);
            readback.meanLuminance = .2126f * readback.meanRed + .7152f * readback.meanGreen + .0722f * readback.meanBlue;
            return readback;
        }
        finally
        {
            camera.targetTexture = previousTarget;
            camera.aspect = previousAspect;
            RenderTexture.active = previousActive;
            Object.DestroyImmediate(image);
            RenderTexture.ReleaseTemporary(target);
        }
    }

    static void CheckReadback(CaptureReadback readback, Report report)
    {
        report.captureReadbacks.Add(readback);
        Require(readback.meanLuminance > .005f,
            "Camera readback is nearly black: " + readback.path, report);
        Require(!(readback.meanRed > .015f && readback.meanGreen < .005f && readback.meanBlue < .005f),
            "Camera readback contains only red channel data: " + readback.path, report);
    }

    static string Hierarchy(Transform transform) => AnimationUtility.CalculateTransformPath(transform, null);
    static bool IsFinite(Vector3 vector) => !float.IsNaN(vector.x) && !float.IsNaN(vector.y) && !float.IsNaN(vector.z)
        && !float.IsInfinity(vector.x) && !float.IsInfinity(vector.y) && !float.IsInfinity(vector.z);
    static void Require(bool valid, string error, Report report)
    {
        if (!valid && !report.errors.Contains(error)) report.errors.Add(error);
    }
}
#endif
