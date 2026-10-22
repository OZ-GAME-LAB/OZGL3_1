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

/// <summary>Builds and renders an isolated static v006 model. Never connects gameplay or modifies v005.</summary>
public static class ChibiCapModelReview
{
    const string Root = "Assets/Art/Characters/Protagonist01/ChibiCap_v006";
    const string ModelPath = Root + "/Model/Protagonist_ChibiCap.fbx";
    const string TexturePath = Root + "/Textures/Protagonist_ChibiCap_BaseColor.png";
    const string MaterialPath = Root + "/Materials/Protagonist_ChibiCap_Unlit.mat";
    const string PrefabPath = Root + "/Prefabs/Protagonist_ChibiCap_Model.prefab";
    const string Output = "ArtSources/Characters/Protagonist01/v006-chibi-cap/ReviewOutput";
    const int Width = 720, Height = 720, TurntableFrames = 120, FrameRate = 30;
    const float TargetHeight = 1.2f;

    [Serializable] sealed class MeshInfo
    {
        public string renderer, mesh, rendererType;
        public int vertices, submeshes, materialSlots;
        public long triangles;
    }
    [Serializable] sealed class ViewInfo
    {
        public string name, image;
        public float modelYawDegrees, orthographicSize;
        public Vector3 cameraPosition, cameraTarget;
    }
    [Serializable] sealed class ReviewReport
    {
        public string unity, model, texture, material, prefab, outputDirectory, method, sourceForward;
        public bool sourceForwardVisuallyVerified, rigged, gameplayConnected;
        public int width, height, turntableFrames, turntableFrameRate, rendererCount, uniqueMeshes, totalVertices;
        public long totalTriangles;
        public float targetHeightMetres, sourceHeightMetres, normalizationScale, renderedHeightMetres, turntableDurationSeconds;
        public Vector3 sourceBoundsCenter, sourceBoundsSize, normalizedBoundsCenter, normalizedBoundsSize;
        public string[] notes;
        public MeshInfo[] meshes;
        public ViewInfo[] views;
    }

    [MenuItem("Project M/Chibi Cap v006/Build Static Model Review")]
    public static void Build()
    {
        try
        {
            if (!File.Exists(ModelPath) || !File.Exists(TexturePath))
                throw new FileNotFoundException("Download the v006 FBX and external base-color PNG before running this review. Expected: " + ModelPath + " ; " + TexturePath);
            Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory(Root + "/Prefabs");
            Directory.CreateDirectory(Output + "/Views");
            Directory.CreateDirectory(Output + "/TurntableFrames");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureImports();
            Material material = BuildMaterial();
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (!model) throw new InvalidOperationException("Cannot import " + ModelPath);

            using (var stage = new ReviewStage())
            {
                GameObject root = stage.New("Protagonist_ChibiCap_Model");
                GameObject visual = Object.Instantiate(model);
                SceneManager.MoveGameObjectToScene(visual, stage.scene);
                visual.name = "Protagonist_ChibiCap_Visual";
                visual.transform.SetParent(root.transform, false);
                foreach (var behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                foreach (var animator in visual.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                foreach (var body in visual.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.useGravity = false; }

                Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true)
                    .Where(r => r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
                if (renderers.Length == 0) throw new InvalidOperationException("The new FBX contains no mesh renderers.");
                foreach (var renderer in renderers)
                {
                    renderer.enabled = true;
                    renderer.sharedMaterials = Enumerable.Repeat(material, Mathf.Max(1, renderer.sharedMaterials.Length)).ToArray();
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                Bounds original = CombinedBounds(renderers);
                if (original.size.y <= .00001f || float.IsNaN(original.size.y))
                    throw new InvalidOperationException("The model has invalid height bounds.");
                float scale = TargetHeight / original.size.y;
                visual.transform.localScale *= scale;
                Bounds scaled = CombinedBounds(renderers);
                visual.transform.localPosition += new Vector3(-scaled.center.x, -scaled.min.y, -scaled.center.z);
                Bounds normalized = CombinedBounds(renderers);

                bool prefabSaved;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out prefabSaved);
                if (!prefabSaved) throw new InvalidOperationException("Could not save the isolated v006 prefab.");

                var meshInfos = renderers.Select(MeshStatistics).ToArray();
                var report = new ReviewReport
                {
                    unity = Application.unityVersion, model = ModelPath, texture = TexturePath,
                    material = MaterialPath, prefab = PrefabPath, outputDirectory = Path.GetFullPath(Output),
                    method = "Actual imported FBX rendered in an isolated Unity preview scene. Fixed orthographic scale, 720x720 PNGs; static model turntable, not action animation.",
                    sourceForward = "Assumed source +Z. Camera is on -Z: front is actor yaw180, screen-right side is yaw90, back is yaw0. Verify the actual face in the first renders.",
                    sourceForwardVisuallyVerified = false, gameplayConnected = false,
                    rigged = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(s => s.bones != null && s.bones.Length > 0),
                    width = Width, height = Height, turntableFrames = TurntableFrames, turntableFrameRate = FrameRate,
                    turntableDurationSeconds = TurntableFrames / (float)FrameRate,
                    rendererCount = renderers.Length, uniqueMeshes = renderers.Select(MeshOf).Where(m => m).Distinct().Count(),
                    totalVertices = meshInfos.Sum(m => m.vertices), totalTriangles = meshInfos.Sum(m => m.triangles),
                    targetHeightMetres = TargetHeight, sourceHeightMetres = original.size.y,
                    normalizationScale = scale, renderedHeightMetres = normalized.size.y,
                    sourceBoundsCenter = original.center, sourceBoundsSize = original.size,
                    normalizedBoundsCenter = normalized.center, normalizedBoundsSize = normalized.size,
                    meshes = meshInfos,
                    notes = new[] {
                        "The prefab preserves the imported source hierarchy and applies uniform normalization only to its visual child. Its root position/rotation/scale are identity.",
                        "The floor and soft ellipse are disposable review guides. The ellipse is not a baked body shadow or VFX asset.",
                        "The same orthographic scale is used for every view and every turntable frame. No per-frame automatic size adjustment is applied.",
                        "This tool does not attach animation, a RuntimeAnimatorController, player scripts, colliders, or gameplay logic.",
                        "Triangle and vertex totals count renderer instances; uniqueMeshes separately counts shared Mesh assets.",
                        "The source forward direction remains an explicit assumption until actual PNG inspection."
                    }
                };

                Bounds turnBounds = normalized;
                for (int i = 0; i < 72; i++)
                {
                    root.transform.localRotation = Quaternion.Euler(0, i * 5, 0);
                    turnBounds.Encapsulate(CombinedBounds(renderers));
                }
                root.transform.localRotation = Quaternion.identity;
                Vector3 center = new Vector3(0, normalized.center.y, 0);
                var standardOffset = new Vector3(0, 1, -10);
                var sideOffset = new Vector3(0, 0, -10);
                var gameOffset = new Vector3(1.05f, 1.05f, -10);
                float commonSize = Mathf.Max(.76f, stage.RequiredSize(turnBounds, center, standardOffset),
                    stage.RequiredSize(turnBounds, center, sideOffset), stage.RequiredSize(turnBounds, center, gameOffset));
                var views = new List<ViewInfo>();
                void SaveView(string name, float yaw, Vector3 offset)
                {
                    root.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                    stage.SetView(center, offset, commonSize);
                    string image = Output + "/Views/" + name + ".png";
                    stage.Save(image);
                    views.Add(new ViewInfo { name = name, image = Path.GetFullPath(image), modelYawDegrees = yaw,
                        orthographicSize = commonSize, cameraPosition = stage.camera.transform.position, cameraTarget = center });
                }
                SaveView("Front", 180, standardOffset);
                SaveView("ThreeQuarter", 145, standardOffset);
                SaveView("Side_Orthographic", 90, sideOffset);
                SaveView("Side_2p5D", 90, gameOffset);
                SaveView("Back", 0, standardOffset);
                stage.SetView(center, standardOffset, commonSize);
                for (int i = 0; i < TurntableFrames; i++)
                {
                    root.transform.localRotation = Quaternion.Euler(0, 180 + 360f * i / TurntableFrames, 0);
                    stage.Save(Output + "/TurntableFrames/" + i.ToString("D4") + ".png");
                }
                report.views = views.ToArray();
                File.WriteAllText(Output + "/model-review.json", JsonUtility.ToJson(report, true));
                root.transform.localRotation = Quaternion.identity;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("CHIBI_CAP_MODEL_REVIEW_SUCCESS " + Path.GetFullPath(Output));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else throw;
        }
    }

    static void ConfigureImports()
    {
        AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        var model = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (model == null) throw new InvalidOperationException("Missing ModelImporter for " + ModelPath);
        model.materialImportMode = ModelImporterMaterialImportMode.None;
        model.importAnimation = false;
        model.animationType = ModelImporterAnimationType.None;
        model.importCameras = false;
        model.importLights = false;
        model.meshCompression = ModelImporterMeshCompression.Off;
        model.importNormals = ModelImporterNormals.Import;
        model.importTangents = ModelImporterTangents.None;
        model.SaveAndReimport();
        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        var texture = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        if (texture == null) throw new InvalidOperationException("Missing TextureImporter for " + TexturePath);
        texture.textureType = TextureImporterType.Default;
        texture.sRGBTexture = true;
        texture.alphaSource = TextureImporterAlphaSource.None;
        texture.mipmapEnabled = true;
        texture.npotScale = TextureImporterNPOTScale.None;
        texture.maxTextureSize = 2048;
        texture.filterMode = FilterMode.Bilinear;
        texture.anisoLevel = 2;
        texture.textureCompression = TextureImporterCompression.CompressedHQ;
        texture.SaveAndReimport();
    }

    static Material BuildMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (!shader) throw new InvalidOperationException("The project requires the URP Unlit shader.");
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (!texture) throw new InvalidOperationException("Cannot load the new base-color PNG.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, MaterialPath); }
        material.name = "Protagonist_ChibiCap_Unlit"; material.shader = shader;
        material.SetTexture("_BaseMap", texture); material.SetColor("_BaseColor", Color.white);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    static Mesh MeshOf(Renderer renderer)
    {
        if (renderer is SkinnedMeshRenderer skin) return skin.sharedMesh;
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        return filter ? filter.sharedMesh : null;
    }
    static MeshInfo MeshStatistics(Renderer renderer)
    {
        Mesh mesh = MeshOf(renderer); long triangles = 0;
        if (mesh) for (int i = 0; i < mesh.subMeshCount; i++)
            if (mesh.GetTopology(i) == MeshTopology.Triangles) triangles += (long)mesh.GetIndexCount(i) / 3;
        return new MeshInfo { renderer = renderer.name, rendererType = renderer.GetType().Name,
            mesh = mesh ? mesh.name : "", vertices = mesh ? mesh.vertexCount : 0,
            submeshes = mesh ? mesh.subMeshCount : 0, triangles = triangles, materialSlots = renderer.sharedMaterials.Length };
    }
    static Bounds CombinedBounds(Renderer[] renderers)
    {
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    sealed class ReviewStage : IDisposable
    {
        public readonly Scene scene;
        public readonly Camera camera;
        readonly Scene previousScene;
        readonly RenderTexture target, previousTarget;
        readonly Texture2D pixels, guideTexture;
        readonly Material floorMaterial, guideMaterial;
        readonly bool asyncCompilation, fog;
        readonly Color ambient;
        readonly AmbientMode ambientMode;
        readonly SphericalHarmonicsL2 ambientProbe;

        public ReviewStage()
        {
            previousScene = SceneManager.GetActiveScene(); previousTarget = RenderTexture.active;
            asyncCompilation = ShaderUtil.allowAsyncCompilation;
            fog = RenderSettings.fog; ambient = RenderSettings.ambientLight;
            ambientMode = RenderSettings.ambientMode; ambientProbe = RenderSettings.ambientProbe;
            ShaderUtil.allowAsyncCompilation = false;
            scene = EditorSceneManager.NewPreviewScene();
            RenderSettings.fog = false; RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.4f, .43f, .48f);
            var probe = new SphericalHarmonicsL2(); probe.AddAmbientLight(RenderSettings.ambientLight); RenderSettings.ambientProbe = probe;
            camera = New("Chibi Cap Static Review Camera").AddComponent<Camera>();
            camera.scene = scene; camera.orthographic = true;
            camera.aspect = Width / (float)Height;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.22f, .25f, .29f);
            camera.nearClipPlane = .02f; camera.farClipPlane = 50;
            camera.allowHDR = false; camera.allowMSAA = true;
            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Disposable v006 review target" };
            target.Create(); camera.targetTexture = target;
            pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            floorMaterial = new Material(unlit); floorMaterial.SetColor("_BaseColor", new Color(.16f, .185f, .215f));
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            SceneManager.MoveGameObjectToScene(floor, scene); floor.name = "Disposable Review Floor";
            floor.transform.localScale = new Vector3(.8f, 1, .6f);
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.GetComponent<MeshRenderer>().sharedMaterial = floorMaterial;
            guideTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = "Disposable foot-contact guide" };
            var colors = new Color[64 * 64];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float distance = new Vector2((x + .5f) / 32 - 1, (y + .5f) / 32 - 1).magnitude;
                colors[y * 64 + x] = new Color(0, 0, 0, Mathf.Pow(Mathf.Clamp01(1 - distance), 1.5f) * .38f);
            }
            guideTexture.SetPixels(colors); guideTexture.Apply(false, false);
            guideMaterial = new Material(unlit) { renderQueue = 3000 };
            guideMaterial.SetTexture("_BaseMap", guideTexture); guideMaterial.SetColor("_BaseColor", Color.white);
            guideMaterial.SetFloat("_Surface", 1); guideMaterial.SetFloat("_Blend", 0);
            guideMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            guideMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            guideMaterial.SetFloat("_ZWrite", 0); guideMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            GameObject guide = GameObject.CreatePrimitive(PrimitiveType.Quad);
            SceneManager.MoveGameObjectToScene(guide, scene); guide.name = "Disposable Contact Ellipse Guide";
            Object.DestroyImmediate(guide.GetComponent<Collider>());
            guide.transform.SetPositionAndRotation(new Vector3(0, .002f, 0), Quaternion.Euler(90, 0, 0));
            guide.transform.localScale = new Vector3(.55f, .30f, 1);
            guide.GetComponent<MeshRenderer>().sharedMaterial = guideMaterial;
        }
        public GameObject New(string name)
        {
            var obj = new GameObject(name); SceneManager.MoveGameObjectToScene(obj, scene); return obj;
        }
        public float RequiredSize(Bounds bounds, Vector3 center, Vector3 offset)
        {
            camera.transform.position = center + offset; camera.transform.LookAt(center);
            float maxY = 0, maxX = 0;
            foreach (int x in new[] { -1, 1 }) foreach (int y in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
            {
                Vector3 delta = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z)) - center;
                maxY = Mathf.Max(maxY, Mathf.Abs(Vector3.Dot(delta, camera.transform.up)));
                maxX = Mathf.Max(maxX, Mathf.Abs(Vector3.Dot(delta, camera.transform.right)));
            }
            return Mathf.Max(maxY, maxX / camera.aspect) * 1.16f;
        }
        public void SetView(Vector3 center, Vector3 offset, float orthographicSize)
        {
            camera.transform.position = center + offset; camera.transform.LookAt(center);
            camera.orthographicSize = orthographicSize;
        }
        public void Save(string path)
        {
            camera.Render(); camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); pixels.Apply(false, false);
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        public void Dispose()
        {
            RenderTexture.active = previousTarget;
            if (camera) camera.targetTexture = null;
            Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(floorMaterial); Object.DestroyImmediate(guideMaterial); Object.DestroyImmediate(guideTexture);
            EditorSceneManager.ClosePreviewScene(scene);
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            ShaderUtil.allowAsyncCompilation = asyncCompilation; RenderSettings.fog = fog;
            RenderSettings.ambientLight = ambient; RenderSettings.ambientMode = ambientMode; RenderSettings.ambientProbe = ambientProbe;
        }
    }
}
#endif
