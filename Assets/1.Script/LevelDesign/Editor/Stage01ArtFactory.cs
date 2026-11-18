#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Editor-only source for the original Stage 01 palette, ink hulls and rift sculpture.</summary>
public static class Stage01ArtFactory
{
    public const string Root = "Assets/Art/Stage01Polish";
    private const string Materials = Root + "/Materials";
    private const string Meshes = Root + "/Meshes";
    private const string Prefabs = Root + "/Prefabs";
    private const string Animations = Root + "/Animations";
    private static bool ensuring;
    private static readonly Dictionary<string, Material> materialCache = new Dictionary<string, Material>();
    private static readonly HashSet<string> repaintedMeshes = new HashSet<string>();

    [MenuItem("Project M/Level Design/Ensure Stage 01 Art")]
    public static void EnsureAssets()
    {
        if (ensuring) return;
        ensuring = true;
        try
        {
            EnsureMaterials();
            EnsureAnimation();
            EnsurePortalPrefab(false);
            EnsurePortalPrefab(true);
            AssetDatabase.SaveAssets();
        }
        finally { ensuring = false; }
    }

    public static Material GetMaterial(string name)
    {
        EnsureMaterials();
        Material result;
        if (materialCache.TryGetValue(name, out result)) return result;
        throw new ArgumentException("Unknown Stage 01 material: " + name);
    }

    /// <summary>Repaints private mesh copies. The source kit, source mesh and source material remain intact.</summary>
    public static void ApplyLegacyPalette(GameObject root, string theme = "Clay", bool outlines = true)
    {
        EnsureMaterials();
        var outlineByRenderer = new Dictionary<Renderer, Renderer>();
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.name.EndsWith("_Ink", StringComparison.Ordinal)) continue;
            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
            if (renderer == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable) continue;
            Mesh source = filter.sharedMesh;
            if (!AssetDatabase.GetAssetPath(source).StartsWith(Meshes, StringComparison.Ordinal))
                filter.sharedMesh = RepaintMesh(source, theme);
            renderer.sharedMaterials = Enumerable.Repeat(GetMaterial(theme), filter.sharedMesh.subMeshCount).ToArray();
            if (!outlines) continue;
            Transform existing = filter.transform.Find(filter.name + "_Ink");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            outlineByRenderer[renderer] = AddInk(filter.gameObject, source, theme == "Far");
        }
        foreach (LODGroup group in root.GetComponentsInChildren<LODGroup>(true))
        {
            LOD[] lods = group.GetLODs();
            for (int i = 0; i < lods.Length; i++)
            {
                var renderers = lods[i].renderers.Where(r => r != null).ToList();
                foreach (Renderer r in lods[i].renderers)
                {
                    Renderer ink;
                    if (r != null && outlineByRenderer.TryGetValue(r, out ink)) renderers.Add(ink);
                }
                lods[i].renderers = renderers.Distinct().ToArray();
            }
            group.SetLODs(lods);
            group.RecalculateBounds();
        }
    }

    /// <summary>Creates a collider-free visual only. StageGate should reference the child StatusGem renderer.</summary>
    public static GameObject CreatePortalVisual(Transform parent, bool boss = false)
    {
        EnsureAssets();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PortalPath(boss));
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.name = boss ? "Rift_Boss_Visual" : "Rift_Street_Visual";
        return instance;
    }

    /// <summary>Adds a separate inverted hull, with welded corner normals to avoid faceted cracks.</summary>
    public static MeshRenderer AddOutline(GameObject target, bool distant = false)
    {
        EnsureMaterials();
        MeshFilter filter = target.GetComponent<MeshFilter>();
        return filter != null && filter.sharedMesh != null ? AddInk(target, filter.sharedMesh, distant) : null;
    }

    private static void EnsureFolders()
    {
        foreach (string path in new[] { Root, Materials, Meshes, Prefabs, Animations })
        {
            string[] parts = path.Split('/');
            string parent = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = parent + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, parts[i]);
                parent = next;
            }
        }
    }

    private static void EnsureMaterials()
    {
        if (materialCache.Count > 0 && materialCache.Values.All(m => m != null)) return;
        EnsureFolders();
        Shader cel = Shader.Find("ProjectM/Stage01/City Cel");
        Shader ink = Shader.Find("ProjectM/Stage01/City Ink Hull");
        Shader field = Shader.Find("ProjectM/Stage01/Rift Field");
        Shader spark = Shader.Find("ProjectM/Stage01/Rift Spark");
        if (cel == null || ink == null || field == null || spark == null)
            throw new InvalidOperationException("Import the four Stage01Polish shaders before creating Stage 01 art.");
        foreach (string theme in new[] { "Clay", "Teal", "Ochre", "Foliage", "Far" })
        {
            Material m = MakeMaterial(theme, cel, Color.white);
            m.SetFloat("_UseVertexColor", 1);
            m.SetFloat("_DistanceFade", theme == "Far" ? .52f : 0);
        }
        MakeMaterial("TrimCream", cel, Hex("F0D7A5"));
        MakeMaterial("StoneWarm", cel, Hex("BC795E"));
        MakeMaterial("Foundation", cel, Hex("485665"));
        MakeMaterial("Asphalt", cel, Hex("384653"));
        MakeMaterial("Roof", cel, Hex("335D68"));
        MakeMaterial("CanopyTeal", cel, Hex("429C9C"));
        MakeMaterial("Gold", cel, Hex("E5B363"));
        MakeMaterial("DarkMetal", cel, Hex("253E50"));
        MakeMaterial("FoliageSolid", cel, Hex("52766C"));
        MakeMaterial("Skyline", cel, Hex("5F7A87")).SetFloat("_DistanceFade", .35f);
        MakeMaterial("Ink", ink, Hex("172D3E")).SetFloat("_OutlineWidth", .025f);
        MakeMaterial("InkFar", ink, Hex("4A6878")).SetFloat("_OutlineWidth", .012f);
        MakeMaterial("PortalTeal", cel, Hex("37E0DB")).SetFloat("_Emission", .65f);
        MakeMaterial("PortalViolet", cel, Hex("B58BE5")).SetFloat("_Emission", .6f);
        MakeMaterial("PortalCoreTeal", field, new Color(.018f, .043f, .066f));
        Material boss = MakeMaterial("PortalCoreViolet", field, new Color(.038f, .023f, .075f));
        boss.SetColor("_EdgeColor", new Color(.68f, .32f, 1.4f));
        boss.SetColor("_AccentColor", new Color(1.2f, .76f, .34f));
        MakeMaterial("SparkTeal", spark, new Color(.25f, 1.25f, 1.15f));
        MakeMaterial("SparkViolet", spark, new Color(.95f, .5f, 1.3f));
        foreach (Material material in materialCache.Values) EditorUtility.SetDirty(material);
    }

    private static Material MakeMaterial(string name, Shader shader, Color color)
    {
        string path = Materials + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = "Stage01_" + name, enableInstancing = true };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetColor("_BaseColor", color);
        materialCache[name] = material;
        return material;
    }

    private static Mesh RepaintMesh(Mesh source, string theme)
    {
        string path = Meshes + "/" + MeshKey(source) + "_" + theme + ".asset";
        Mesh result = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (result != null && repaintedMeshes.Contains(path)) return result;
        bool isNew = result == null;
        if (isNew)
        {
            result = UnityEngine.Object.Instantiate(source);
            result.name = source.name + "_Paint_" + theme;
        }
        Color[] colors = source.colors;
        if (colors == null || colors.Length != source.vertexCount)
            colors = Enumerable.Repeat(Color.white, source.vertexCount).ToArray();
        // Vertex attributes bypass Unity's material-color sRGB conversion in a linear project.
        // Paint swatches are authored in sRGB, so explicitly linearize them once here.
        for (int i = 0; i < colors.Length; i++)
        {
            Color paint = Repaint(colors[i], theme);
            colors[i] = PlayerSettings.colorSpace == ColorSpace.Linear ? paint.linear : paint;
        }
        result.colors = colors;
        if (isNew) AssetDatabase.CreateAsset(result, path);
        else EditorUtility.SetDirty(result);
        repaintedMeshes.Add(path);
        return result;
    }

    private static Color Repaint(Color c, string theme)
    {
        float value = c.r * .2126f + c.g * .7152f + c.b * .0722f;
        if (theme == "Foliage")
        {
            if (value > .58f) return Hex("A3BB73");
            if (value > .4f) return Hex("648C53");
            if (value > .28f) return Hex("3F714F");
            return Hex("354A3D");
        }
        if (theme == "Far")
            return Color.Lerp(Hex("4C697A"), Hex("8BA2A2"), Mathf.InverseLerp(.1f, .8f, value));
        if (c.a < .95f) return Hex("E4BB75");
        if (value > .6f) return Hex("EBD2A7");
        if (value > .42f)
            return theme == "Teal" ? Hex("5D9395") : theme == "Ochre" ? Hex("BE965F") : Hex("BB7965");
        if (value > .345f)
            return theme == "Teal" ? Hex("3E737F") : theme == "Ochre" ? Hex("926E50") : Hex("955E59");
        if (c.r > c.g * 1.15f) return Hex("B17A5B");
        if (value > .26f) return Hex("366372");
        if (value > .20f) return Hex("36525F");
        return Hex("203847");
    }

    private static MeshRenderer AddInk(GameObject target, Mesh source, bool far)
    {
        GameObject outline = new GameObject(target.name + "_Ink");
        outline.transform.SetParent(target.transform, false);
        outline.layer = target.layer;
        outline.AddComponent<MeshFilter>().sharedMesh = SmoothHullMesh(source);
        MeshRenderer renderer = outline.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = Enumerable.Repeat(GetMaterial(far ? "InkFar" : "Ink"), source.subMeshCount).ToArray();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private static Mesh SmoothHullMesh(Mesh source)
    {
        string path = Meshes + "/" + MeshKey(source) + "_Hull.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;
        mesh = UnityEngine.Object.Instantiate(source);
        mesh.name = source.name + "_WeldedInkHull";
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        if (normals.Length != vertices.Length) { mesh.RecalculateNormals(); normals = mesh.normals; }
        var sums = new Dictionary<Vector3Int, Vector3>();
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3Int key = Vector3Int.RoundToInt(vertices[i] * 10000f);
            Vector3 sum;
            sums.TryGetValue(key, out sum);
            sums[key] = sum + normals[i];
        }
        for (int i = 0; i < vertices.Length; i++)
            normals[i] = sums[Vector3Int.RoundToInt(vertices[i] * 10000f)].normalized;
        mesh.normals = normals;
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static string MeshKey(Mesh source)
    {
        string guid;
        long localId;
        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out guid, out localId) && !string.IsNullOrEmpty(guid))
            return Sanitize(source.name) + "_" + guid.Substring(0, 12) + "_" + localId;
        return Sanitize(source.name);
    }

    private static string Sanitize(string text)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars()) text = text.Replace(invalid, '_');
        return text;
    }

    private static string PortalPath(bool boss) { return Prefabs + (boss ? "/RiftBoss.prefab" : "/RiftStreet.prefab"); }

    private static void EnsurePortalPrefab(bool boss)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PortalPath(boss)) != null) return;
        GameObject root = new GameObject(boss ? "RiftBoss" : "RiftStreet");
        try
        {
            // This separate renderer is the sole state-color target for the existing StageGate.
            Cube(root.transform, "StatusGem", new Vector3(0, 4.18f, -.32f), new Vector3(.18f, .26f, .14f),
                boss ? "PortalViolet" : "PortalTeal", new Vector3(0, 0, 45));
            Cube(root.transform, "GroundedPlinth", new Vector3(0, .105f, .02f), new Vector3(3.12f, .21f, .84f), "Foundation");
            Cube(root.transform, "PlinthLip", new Vector3(0, .23f, -.04f), new Vector3(2.82f, .12f, .76f), "TrimCream");
            const int segments = 16;
            for (int i = 0; i < segments; i++)
            {
                Mesh mesh = RingSegment(i, segments);
                var block = MeshObject(root.transform, "ArchStone_" + i.ToString("00"), mesh,
                    GetMaterial(i % 4 == 0 ? "TrimCream" : "StoneWarm"));
                AddInk(block, mesh, false);
            }
            Cube(root.transform, "LeftButtress", new Vector3(-1.22f, .61f, -.03f), new Vector3(.43f, 1.0f, .69f), "DarkMetal", new Vector3(0, 0, -8));
            Cube(root.transform, "RightButtress", new Vector3(1.22f, .61f, -.03f), new Vector3(.43f, 1.0f, .69f), "DarkMetal", new Vector3(0, 0, 8));
            for (int side = -1; side <= 1; side += 2)
            {
                Cube(root.transform, "SideRune_" + side, new Vector3(side * 1.28f, 1.87f, -.285f),
                    new Vector3(.095f, .55f, .065f), boss ? "PortalViolet" : "PortalTeal", new Vector3(0, 0, side * -5));
                Cube(root.transform, "UpperFin_" + side, new Vector3(side * .56f, 3.98f, -.01f),
                    new Vector3(.17f, .63f, .35f), "DarkMetal", new Vector3(0, 0, side * -32));
            }
            Cube(root.transform, "CrownKey", new Vector3(0, 3.97f, -.12f), new Vector3(.4f, .38f, .42f), "Gold", new Vector3(0, 0, 45));
            Cube(root.transform, "CrownRune", new Vector3(0, 3.98f, -.36f), new Vector3(.12f, .15f, .035f), boss ? "PortalViolet" : "PortalTeal", new Vector3(0, 0, 45));

            Mesh disc = DiscMesh();
            GameObject backing = MeshObject(root.transform, "DormantAperture", disc, GetMaterial("DarkMetal"));
            backing.transform.localPosition = new Vector3(0, 2.12f, .09f);
            backing.transform.localScale = new Vector3(1.02f, 1.47f, 1);
            GameObject energy = MeshObject(root.transform, "EnergyField", QuadMesh(), GetMaterial(boss ? "PortalCoreViolet" : "PortalCoreTeal"));
            energy.transform.localPosition = new Vector3(0, 2.12f, -.03f);
            energy.transform.localScale = new Vector3(2.08f, 2.97f, 1);
            MakeSparks(energy.transform, boss);
            energy.SetActive(false);

            Animator animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(Animations + "/RiftVisual.controller");
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            GateAnimController gateAnimation = root.AddComponent<GateAnimController>();
            SerializedObject serialized = new SerializedObject(gateAnimation);
            serialized.FindProperty("animator").objectReferenceValue = animator;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (boss) root.transform.localScale = Vector3.one * 1.12f;
            PrefabUtility.SaveAsPrefabAsset(root, PortalPath(boss));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void EnsureAnimation()
    {
        string controllerPath = Animations + "/RiftVisual.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null) return;
        var dormant = new AnimationClip { name = "Rift_Dormant", frameRate = 30 };
        var active = new AnimationClip { name = "Rift_Active", frameRate = 30 };
        var binding = EditorCurveBinding.FloatCurve("EnergyField", typeof(GameObject), "m_IsActive");
        AnimationUtility.SetEditorCurve(dormant, binding, AnimationCurve.Constant(0, 1, 0));
        AnimationUtility.SetEditorCurve(active, binding, AnimationCurve.Constant(0, 1, 1));
        AssetDatabase.CreateAsset(dormant, Animations + "/RiftDormant.anim");
        AssetDatabase.CreateAsset(active, Animations + "/RiftActive.anim");
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.AddParameter("IsActive", AnimatorControllerParameterType.Bool);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState offState = machine.AddState("Dormant");
        offState.motion = dormant;
        AnimatorState onState = machine.AddState("Active");
        onState.motion = active;
        machine.defaultState = offState;
        AnimatorStateTransition activate = offState.AddTransition(onState);
        activate.hasExitTime = false; activate.duration = 0;
        activate.AddCondition(AnimatorConditionMode.If, 0, "IsActive");
        AnimatorStateTransition close = onState.AddTransition(offState);
        close.hasExitTime = false; close.duration = 0;
        close.AddCondition(AnimatorConditionMode.IfNot, 0, "IsActive");
        EditorUtility.SetDirty(controller);
    }

    private static void MakeSparks(Transform parent, bool boss)
    {
        var go = new GameObject("SparseRiftSparks");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0, 0, -.06f);
        // Counter-scale the aperture quad so particle dimensions stay in world meters.
        go.transform.localScale = new Vector3(1 / 2.08f, 1 / 2.97f, 1);
        ParticleSystem particles = go.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.duration = 3; main.loop = true; main.startLifetime = 1.25f;
        main.startSpeed = .15f; main.startSize = .055f; main.maxParticles = 10;
        main.startColor = Color.white; main.simulationSpace = ParticleSystemSimulationSpace.Local;
        var emission = particles.emission; emission.rateOverTime = 3;
        var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 1.0f; shape.radiusThickness = .02f; shape.scale = new Vector3(1, 1.4f, 1);
        var velocity = particles.velocityOverLifetime; velocity.enabled = true;
        velocity.y = .12f; velocity.space = ParticleSystemSimulationSpace.Local;
        var size = particles.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 0), new Keyframe(.2f, 1), new Keyframe(1, 0)));
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = GetMaterial(boss ? "SparkViolet" : "SparkTeal");
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
    }

    private static GameObject Cube(Transform parent, string name, Vector3 position, Vector3 scale, string material, Vector3 rotation = default(Vector3))
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name; cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position; cube.transform.localScale = scale;
        cube.transform.localEulerAngles = rotation;
        UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
        cube.GetComponent<MeshRenderer>().sharedMaterial = GetMaterial(material);
        if (!material.StartsWith("Portal", StringComparison.Ordinal)) AddInk(cube, cube.GetComponent<MeshFilter>().sharedMesh, false);
        return cube;
    }

    private static GameObject MeshObject(Transform parent, string name, Mesh mesh, Material material)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    private static Mesh RingSegment(int index, int count)
    {
        string path = Meshes + "/RiftArchStone_" + index.ToString("00") + ".asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;
        float a = (index * 2 * Mathf.PI / count) + .014f;
        float b = ((index + 1) * 2 * Mathf.PI / count) - .014f;
        Vector3 outerA = new Vector3(Mathf.Cos(a) * 1.42f, Mathf.Sin(a) * 1.92f + 2.12f, 0);
        Vector3 outerB = new Vector3(Mathf.Cos(b) * 1.42f, Mathf.Sin(b) * 1.92f + 2.12f, 0);
        Vector3 innerA = new Vector3(Mathf.Cos(a) * 1.065f, Mathf.Sin(a) * 1.50f + 2.12f, 0);
        Vector3 innerB = new Vector3(Mathf.Cos(b) * 1.065f, Mathf.Sin(b) * 1.50f + 2.12f, 0);
        Vector3 front = Vector3.back * .25f, back = Vector3.forward * .25f;
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        AddQuad(vertices, triangles, outerA + front, innerA + front, innerB + front, outerB + front);
        AddQuad(vertices, triangles, outerB + back, innerB + back, innerA + back, outerA + back);
        AddQuad(vertices, triangles, outerB + front, outerB + back, outerA + back, outerA + front);
        AddQuad(vertices, triangles, innerA + front, innerA + back, innerB + back, innerB + front);
        AddQuad(vertices, triangles, outerA + front, outerA + back, innerA + back, innerA + front);
        AddQuad(vertices, triangles, innerB + front, innerB + back, outerB + back, outerB + front);
        Mesh mesh = new Mesh { name = "RiftArchStone_" + index.ToString("00") };
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static void AddQuad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int i = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
        triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
        triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 3);
    }

    private static Mesh QuadMesh()
    {
        string path = Meshes + "/RiftFieldQuad.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;
        mesh = new Mesh { name = "RiftFieldQuad" };
        mesh.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(-.5f, .5f, 0), new Vector3(.5f, .5f, 0), new Vector3(.5f, -.5f, 0) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static Mesh DiscMesh()
    {
        string path = Meshes + "/RiftDormantDisc.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;
        mesh = new Mesh { name = "RiftDormantDisc" };
        var vertices = new List<Vector3> { Vector3.zero }; var triangles = new List<int>();
        const int count = 48;
        for (int i = 0; i < count; i++)
            vertices.Add(new Vector3(Mathf.Cos(i * 2 * Mathf.PI / count), Mathf.Sin(i * 2 * Mathf.PI / count), 0));
        for (int i = 0; i < count; i++) { triangles.Add(0); triangles.Add((i + 1) % count + 1); triangles.Add(i + 1); }
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path); return mesh;
    }

    private static Color Hex(string hex)
    {
        Color color; ColorUtility.TryParseHtmlString("#" + hex, out color); return color;
    }
}
#endif
