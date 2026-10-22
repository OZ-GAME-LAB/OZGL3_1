#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using ProjectM.CombatVfxPreview;
using Object = UnityEngine.Object;

/// <summary>Creates four isolated, reusable art samples without touching any game scene, prefab or combat code.</summary>
public static class CombatVfxBuilder
{
    public const string Root = "Assets/Art/CombatVFX_v1";
    public const string Prefabs = Root + "/Prefabs";
    private static Material glow, spark, smoke;
    private static Mesh shard, core, crescent, crescentGlow, crescentCore, ribbon, ivoryRibbon, ring, tealDome, star, helix;
    private static readonly Color Teal = new Color(.25f, .83f, .88f), Ivory = new Color(1f, .96f, .77f);

    [MenuItem("Project M/Samples/Build Four Combat VFX Samples")]
    public static void Build()
    {
        foreach (string folder in new[] { Root, Prefabs, Root + "/Materials", Root + "/Meshes", Root + "/Shaders", Root + "/Samples" })
            Directory.CreateDirectory(folder);
        string shaderPath = Root + "/Shaders/CombatVfxPreview.shader";
        File.WriteAllText(shaderPath, ShaderSource);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("VFX shader failed to import.");
        glow = Material("VFX_Glow", shader, BlendMode.SrcAlpha, BlendMode.One, 0);
        spark = Material("VFX_Spark", shader, BlendMode.SrcAlpha, BlendMode.One, 2);
        smoke = Material("VFX_Smoke", shader, BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, 3);
        shard = SaveMesh("Shard", Octahedron(Color.white));
        core = SaveMesh("Core", Octahedron(Ivory));
        crescent = SaveMesh("Crescent", Arc(1.03f, .84f, 78, 18, Ivory));
        crescentGlow = SaveMesh("Crescent_Glow", Arc(1.12f, .73f, 82, 18, WithAlpha(Teal, .34f)));
        crescentCore = SaveMesh("Crescent_Core", Arc(1.00f, .965f, 76, 18, WithAlpha(Ivory, .95f)));
        ribbon = SaveMesh("Ribbon", Ribbon(1.7f, .24f, Teal));
        ivoryRibbon = SaveMesh("Ribbon_Ivory", Ribbon(1.7f, .24f, Ivory));
        ring = SaveMesh("Ring", Arc(1.0f, .955f, 180, 40, Color.white));
        tealDome = SaveMesh("Shockwave_Dome", Arc(1.0f, .955f, 90, 24, Teal));
        star = SaveMesh("Burst_Star", Star(Ivory));
        helix = SaveMesh("Missile_Helix", Helix(Teal));
        BuildDash(); BuildSlash(); BuildMissile(); BuildExplosion();
        ValidateAndReport();
        File.WriteAllText(Root + "/Samples/README_ko.txt", Usage);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("COMBAT_VFX_SAMPLES_BUILD_SUCCESS: four isolated ParticleSystem/mesh samples, no gameplay integration.");
    }

    private static void BuildDash()
    {
        var build = new Effect("DashBurst", CombatVfxKind.DashBurst, .60f, 14, .22f, new Vector3(0, .95f, 0));
        for (int i = 0; i < 4; i++)
            build.Layer("Wind_Streak_" + i, ribbon, glow, CombatVfxLayerRole.DashStreak,
                new Vector3(i % 2 == 0 ? -.06f : .06f, -.45f + i * .29f, -.20f - i * .22f),
                new Vector3(1, .35f + i * .10f, 1.0f + i * .20f), Quaternion.Euler(0, 0, i % 2 == 0 ? 0 : 180), i * .018f, .50f);
        build.Layer("Air_Arc", crescentGlow, glow, CombatVfxLayerRole.DashAir, new Vector3(0, 0, .12f),
            new Vector3(.75f, .85f, .38f), Quaternion.identity, 0, .55f);
        var streaks = build.Particles("Wind_Glints", .22f, .18f, .08f, 28, 8, Teal, Ivory, 0, 1.4f, 0);
        var shape = streaks.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(.16f, 1.2f, .2f);
        var velocity = streaks.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.z = -2.7f;
        build.Save();
    }

    private static void BuildSlash()
    {
        var build = new Effect("SlashWave", CombatVfxKind.SlashWave, .95f, 7.5f, .58f, new Vector3(0, 1.05f, 0));
        build.Layer("Teal_Edge", crescentGlow, glow, CombatVfxLayerRole.SlashGlow, Vector3.zero, Vector3.one, Quaternion.identity, 0, .8f);
        build.Layer("Ivory_Blade", crescent, glow, CombatVfxLayerRole.SlashCore, new Vector3(-.012f, 0, .015f), Vector3.one, Quaternion.identity, 0, .85f);
        build.Layer("White_Leading_Edge", crescentCore, glow, CombatVfxLayerRole.SlashCore, new Vector3(-.025f, 0, .025f), Vector3.one, Quaternion.identity, 0, 1f);
        for (int i = 0; i < 3; i++)
            build.Layer("Wake_" + i, ribbon, glow, CombatVfxLayerRole.SlashTail,
                new Vector3(.025f, (i - 1) * .44f, .10f), new Vector3(1, .26f, .62f + i * .08f), Quaternion.identity, .025f * i, .55f);
        var dust = build.Particles("Edge_Splinters", .58f, .20f, .055f, 20, 5, Ivory, Teal, 0, .8f, 0);
        var shape = dust.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(.12f, 1.15f, .25f);
        var velocity = dust.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local; velocity.z = -1.7f;
        build.Save();
    }

    private static void BuildMissile()
    {
        var build = new Effect("MagicMissile", CombatVfxKind.MagicMissile, 1.05f, 8.5f, .68f, new Vector3(0, 1.1f, 0));
        build.Layer("Ivory_Core", core, glow, CombatVfxLayerRole.MissileCore, Vector3.zero,
            new Vector3(.24f, .24f, .38f), Quaternion.identity, 0, 1f);
        build.Layer("Teal_Shell", shard, glow, CombatVfxLayerRole.MissileShell, Vector3.zero,
            new Vector3(.40f, .40f, .54f), Quaternion.identity, 0, .22f);
        build.Layer("Orbiting_Helix", helix, glow, CombatVfxLayerRole.MissileOrbit, Vector3.zero,
            Vector3.one, Quaternion.identity, 0, .75f);
        build.Layer("Long_Teal_Tail", ribbon, glow, CombatVfxLayerRole.MissileTail, new Vector3(0, 0, -.12f),
            new Vector3(1, .68f, 1.05f), Quaternion.identity, 0, .75f);
        build.Layer("Thin_Ivory_Tail", ivoryRibbon, glow, CombatVfxLayerRole.MissileTail, new Vector3(-.012f, 0, -.05f),
            new Vector3(1, .20f, .82f), Quaternion.Euler(0, 0, 20), .02f, .85f);
        var dust = build.Particles("Magic_Wake", .68f, .27f, .065f, 26, 5, Teal, Ivory, 0, .50f, 0);
        var shape = dust.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .10f;
        var velocity = dust.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local; velocity.z = -1.25f;
        build.Save();
    }

    private static void BuildExplosion()
    {
        var build = new Effect("Explosion", CombatVfxKind.Explosion, 1.55f, 0, 0, new Vector3(0, .82f, 0));
        build.Layer("White_Flash", core, glow, CombatVfxLayerRole.BlastFlash, Vector3.zero,
            new Vector3(.6f, .6f, .65f), Quaternion.identity, 0, 1f);
        build.Layer("Angular_Flare", star, glow, CombatVfxLayerRole.BlastStar, Vector3.zero,
            new Vector3(1, .75f, .85f), Quaternion.identity, .015f, .75f);
        build.Layer("Air_Shockwave", tealDome, glow, CombatVfxLayerRole.BlastRing, new Vector3(0, -.76f, 0),
            Vector3.one, Quaternion.Euler(-90, 0, 0), .02f, .45f);
        build.Layer("Ground_Shockwave", ring, glow, CombatVfxLayerRole.GroundRing, new Vector3(0, -.76f, 0),
            Vector3.one, Quaternion.Euler(0, 0, 90), .025f, .50f);
        var flame = build.Particles("Angular_Flames", .12f, .42f, .42f, 0, 18, Ivory, new Color(1, .48f, .17f), .015f, 3.7f, .38f);
        var flameShape = flame.shape; flameShape.shapeType = ParticleSystemShapeType.Hemisphere;
        flameShape.rotation = new Vector3(-90, 0, 0); flameShape.radius = .15f;
        var flameSize = flame.sizeOverLifetime; flameSize.enabled = true;
        flameSize.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .2f), new Keyframe(.18f, 1), new Keyframe(1, .08f)));
        var flameColor = flame.colorOverLifetime;
        var cooling = new Gradient();
        cooling.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(1, .60f, .28f), .45f),
            new GradientColorKey(new Color(.35f, .15f, .06f), 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .07f), new GradientAlphaKey(.8f, .5f), new GradientAlphaKey(0, 1) });
        flameColor.color = cooling;
        var embers = build.Particles("Hot_Embers", .10f, .70f, .07f, 0, 24, Ivory, new Color(1, .62f, .22f), .015f, 4.6f, .30f);
        var emberShape = embers.shape; emberShape.shapeType = ParticleSystemShapeType.Hemisphere;
        emberShape.rotation = new Vector3(-90, 0, 0); emberShape.radius = .20f;
        var emberRenderer = embers.GetComponent<ParticleSystemRenderer>();
        emberRenderer.renderMode = ParticleSystemRenderMode.Stretch; emberRenderer.sharedMaterial = spark;
        emberRenderer.velocityScale = .10f; emberRenderer.lengthScale = 1.4f; emberRenderer.cameraVelocityScale = 0;
        var puffs = build.Particles("Faceted_Smoke", .15f, 1.12f, .55f, 0, 7,
            new Color(.20f, .25f, .30f, .50f), new Color(.30f, .34f, .37f, .42f), .075f, .7f, -.025f, smoke, true);
        var smokeShape = puffs.shape; smokeShape.shapeType = ParticleSystemShapeType.Sphere; smokeShape.radius = .38f;
        var smokeVelocity = puffs.velocityOverLifetime; smokeVelocity.enabled = true;
        smokeVelocity.space = ParticleSystemSimulationSpace.Local; smokeVelocity.y = .8f;
        var smokeSize = puffs.sizeOverLifetime; smokeSize.enabled = true;
        smokeSize.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .55f), new Keyframe(.3f, 1.4f), new Keyframe(1, 2.1f)));
        build.Save();
    }

    private sealed class Effect
    {
        readonly GameObject root;
        readonly Transform flight;
        readonly CombatVfxKind kind;
        readonly float duration, speed, travel;
        readonly List<CombatVfxSample.MeshLayer> layers = new List<CombatVfxSample.MeshLayer>();
        readonly List<ParticleSystem> particles = new List<ParticleSystem>();
        public Effect(string name, CombatVfxKind type, float seconds, float travelSpeed, float travelSeconds, Vector3 origin)
        {
            root = new GameObject(name); kind = type; duration = seconds; speed = travelSpeed; travel = travelSeconds;
            flight = new GameObject("Visual_Travel").transform; flight.SetParent(root.transform, false); flight.localPosition = origin;
        }
        public void Layer(string name, Mesh mesh, Material material, CombatVfxLayerRole role, Vector3 position, Vector3 scale,
            Quaternion rotation, float delay, float opacity)
        {
            var child = new GameObject(name); child.transform.SetParent(flight, false);
            child.transform.localPosition = position; child.transform.localScale = scale; child.transform.localRotation = rotation;
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            layers.Add(new CombatVfxSample.MeshLayer { renderer = renderer, role = role, position = position, scale = scale,
                rotation = rotation, delay = delay, opacity = opacity });
        }
        public ParticleSystem Particles(string name, float emitSeconds, float lifetime, float size, float rate, int burst,
            Color a, Color b, float delay, float speed, float gravity, Material material = null, bool billboard = false)
        {
            var child = new GameObject(name); child.transform.SetParent(flight, false);
            var system = child.AddComponent<ParticleSystem>();
            var main = system.main; main.playOnAwake = false; main.loop = false; main.duration = emitSeconds;
            main.startDelay = delay; main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * .72f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .55f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * .65f, size * 1.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(a, b); main.gravityModifier = gravity;
            main.startRotation3D = true; main.startRotationX = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2); main.startRotationZ = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.simulationSpeed = 1;
            main.maxParticles = Mathf.Max(burst + 4, Mathf.CeilToInt(rate * lifetime) + 8);
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.stopAction = ParticleSystemStopAction.None;
            var emission = system.emission; emission.enabled = true; emission.rateOverTime = rate;
            if (burst > 0) emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)burst) });
            var shape = system.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .08f;
            var color = system.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .06f), new GradientAlphaKey(.85f, .45f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            var sizeOverLife = system.sizeOverLifetime; sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, 0));
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = billboard ? ParticleSystemRenderMode.Billboard : ParticleSystemRenderMode.Mesh;
            if (!billboard) renderer.mesh = shard;
            renderer.sharedMaterial = material != null ? material : glow;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.enableGPUInstancing = false;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            system.useAutoRandomSeed = false; system.randomSeed = (uint)(1249 + particles.Count * 83 + (int)kind * 241);
            system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Add(system); return system;
        }
        public void Save()
        {
            try
            {
                var driver = root.AddComponent<CombatVfxSample>();
                driver.Configure(kind, duration, speed, travel, flight, layers.ToArray(), particles.ToArray());
                PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/" + root.name + ".prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }

    private static Material Material(string name, Shader shader, BlendMode source, BlendMode destination, int mask)
    {
        string path = Root + "/Materials/" + name + ".mat";
        var result = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (result == null) { result = new Material(shader); AssetDatabase.CreateAsset(result, path); }
        result.shader = shader; result.SetColor("_Tint", Color.white); result.SetFloat("_SrcBlend", (float)source);
        result.SetFloat("_DstBlend", (float)destination); result.SetFloat("_Mask", mask);
        result.renderQueue = mask == 3 ? 3000 : 3010; result.enableInstancing = true;
        EditorUtility.SetDirty(result); return result;
    }
    private static Mesh SaveMesh(string name, Mesh generated)
    {
        generated.name = name; generated.RecalculateNormals(); generated.RecalculateBounds();
        string path = Root + "/Meshes/" + name + ".asset";
        var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (saved == null) { AssetDatabase.CreateAsset(generated, path); return generated; }
        EditorUtility.CopySerialized(generated, saved); Object.DestroyImmediate(generated); EditorUtility.SetDirty(saved); return saved;
    }
    private static Color WithAlpha(Color color, float alpha) { color.a = alpha; return color; }

    private sealed class Geometry
    {
        readonly List<Vector3> vertices = new List<Vector3>(); readonly List<Color> colors = new List<Color>();
        readonly List<Vector2> uv = new List<Vector2>(); readonly List<int> triangles = new List<int>();
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc)
        {
            int n = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c);
            colors.Add(ca); colors.Add(cb); colors.Add(cc); uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(Vector2.up);
            triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 2);
        }
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color ca, Color cb, Color cc, Color cd)
        { Tri(a, b, c, ca, cb, cc); Tri(a, c, d, ca, cc, cd); }
        public Mesh Mesh() { var m = new Mesh(); m.SetVertices(vertices); m.SetColors(colors); m.SetUVs(0, uv); m.SetTriangles(triangles, 0); return m; }
    }
    private static Mesh Octahedron(Color color)
    {
        var g = new Geometry(); var belt = new[] { Vector3.right * .5f, Vector3.up * .5f, Vector3.left * .5f, Vector3.down * .5f };
        for (int i = 0; i < 4; i++)
        {
            Color shade = color * (i % 2 == 0 ? 1 : .80f); shade.a = color.a;
            g.Tri(Vector3.forward * .5f, belt[i], belt[(i + 1) % 4], color, shade, shade);
            g.Tri(Vector3.back * .5f, belt[(i + 1) % 4], belt[i], shade, shade, shade);
        }
        return g.Mesh();
    }
    private static Mesh Arc(float outer, float inner, float degrees, int segments, Color color)
    {
        var g = new Geometry();
        for (int i = 0; i < segments; i++)
        {
            float a = Mathf.Lerp(-degrees, degrees, i / (float)segments) * Mathf.Deg2Rad;
            float b = Mathf.Lerp(-degrees, degrees, (i + 1) / (float)segments) * Mathf.Deg2Rad;
            Vector3 pa = new Vector3(0, Mathf.Sin(a), Mathf.Cos(a)), pb = new Vector3(0, Mathf.Sin(b), Mathf.Cos(b));
            float aa = degrees > 100 ? 1 : Mathf.Sin(Mathf.PI * i / segments);
            float ab = degrees > 100 ? 1 : Mathf.Sin(Mathf.PI * (i + 1) / segments);
            g.Quad(pa * outer, pb * outer, pb * inner, pa * inner,
                WithAlpha(color, color.a * aa), WithAlpha(color, color.a * ab), WithAlpha(color, color.a * ab), WithAlpha(color, color.a * aa));
        }
        return g.Mesh();
    }
    private static Mesh Ribbon(float length, float width, Color color)
    {
        var g = new Geometry(); const int segments = 12;
        for (int i = 0; i < segments; i++)
        {
            float a = i / (float)segments, b = (i + 1) / (float)segments;
            Vector3 ca = new Vector3(0, Mathf.Sin(a * Mathf.PI * 2) * width * .35f * a, -length * a);
            Vector3 cb = new Vector3(0, Mathf.Sin(b * Mathf.PI * 2) * width * .35f * b, -length * b);
            float wa = width * (1 - a), wb = width * (1 - b);
            g.Quad(ca + Vector3.up * wa, cb + Vector3.up * wb, cb - Vector3.up * wb, ca - Vector3.up * wa,
                WithAlpha(color, 1 - a), WithAlpha(color, 1 - b), WithAlpha(color, 1 - b), WithAlpha(color, 1 - a));
        }
        return g.Mesh();
    }
    private static Mesh Star(Color color)
    {
        var g = new Geometry(); const int points = 16;
        for (int i = 0; i < points; i++)
        {
            float a = i * Mathf.PI * 2 / points, b = (i + 1) * Mathf.PI * 2 / points;
            float ra = i % 2 == 0 ? 1.15f : .28f, rb = (i + 1) % 2 == 0 ? 1.15f : .28f;
            g.Tri(Vector3.zero, new Vector3(0, Mathf.Cos(a) * ra, Mathf.Sin(a) * ra),
                new Vector3(0, Mathf.Cos(b) * rb, Mathf.Sin(b) * rb), color, WithAlpha(color, .3f), WithAlpha(color, .3f));
        }
        return g.Mesh();
    }
    private static Mesh Helix(Color color)
    {
        var g = new Geometry(); const int segments = 36;
        for (int i = 0; i < segments; i++)
        {
            float a = i / (float)segments, b = (i + 1) / (float)segments;
            Vector3 pa = new Vector3(Mathf.Cos(a * Mathf.PI * 4) * .20f, Mathf.Sin(a * Mathf.PI * 4) * .20f, -.65f * a);
            Vector3 pb = new Vector3(Mathf.Cos(b * Mathf.PI * 4) * .20f, Mathf.Sin(b * Mathf.PI * 4) * .20f, -.65f * b);
            Vector3 normal = new Vector3(Mathf.Cos(a * Mathf.PI * 4), Mathf.Sin(a * Mathf.PI * 4), 0) * .018f;
            g.Quad(pa + normal, pb + normal, pb - normal, pa - normal,
                WithAlpha(color, 1 - a * .8f), WithAlpha(color, 1 - b * .8f), WithAlpha(color, 1 - b * .8f), WithAlpha(color, 1 - a * .8f));
        }
        return g.Mesh();
    }

    private static void ValidateAndReport()
    {
        var entries = new List<EffectReport>();
        foreach (string name in new[] { "DashBurst", "SlashWave", "MagicMissile", "Explosion" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/" + name + ".prefab");
            var sample = prefab.GetComponent<CombatVfxSample>();
            var systems = prefab.GetComponentsInChildren<ParticleSystem>(true);
            if (sample == null || systems.Any(p => p.main.playOnAwake || p.main.loop)) throw new InvalidOperationException("Invalid VFX sample setup: " + name);
            if (prefab.GetComponentInChildren<Collider>(true) != null || prefab.GetComponentInChildren<Light>(true) != null)
                throw new InvalidOperationException("Preview VFX unexpectedly contains physics or a dynamic light.");
            entries.Add(new EffectReport { name = name, prefab = Prefabs + "/" + name + ".prefab", duration = sample.Duration,
                travelSpeed = sample.TravelSpeed, travelDuration = sample.TravelDuration, particleSystems = systems.Length,
                maximumParticles = systems.Sum(p => p.main.maxParticles), meshRenderers = prefab.GetComponentsInChildren<MeshRenderer>(true).Length,
                meshTriangles = prefab.GetComponentsInChildren<MeshFilter>(true).Sum(f => f.sharedMesh.triangles.Length / 3) });
        }
        File.WriteAllText(Root + "/BuildReport.json", JsonUtility.ToJson(new BuildReport { effects = entries.ToArray() }, true));
    }
    [Serializable] private sealed class EffectReport
    {
        public string name, prefab; public float duration, travelSpeed, travelDuration;
        public int particleSystems, maximumParticles, meshRenderers, meshTriangles;
    }
    [Serializable] private sealed class BuildReport
    {
        public bool previewOnly = true, gameplayIntegration = false, meshGenerationCredits = false;
        public string unity = "6000.3.21f1", renderPipeline = "URP 17.3", simulation = "Native ParticleSystem, fixed 60 Hz; deterministic seeds; no VFX Graph, depth texture, dynamic lights, collision or texture downloads.";
        public EffectReport[] effects;
    }

    private const string Usage = @"Project M Combat VFX v1 — isolated visual samples

이 폴더는 샘플 프리팹과 GIF 캡처용입니다. 기존 게임 씬·플레이어·전투·환경 자산에 자동 연결하지 않습니다.
프리팹: Prefabs/DashBurst, SlashWave, MagicMissile, Explosion
구성 기준값(초 / m/s / 입자 상한 합계 / Renderer 오브젝트 수):
DashBurst: 0.60 / 14.0(0.22초 이동) / 14 / 6
SlashWave: 0.95 / 7.5(0.58초 이동) / 12 / 7
MagicMissile: 1.05 / 8.5(0.68초 이동) / 16 / 6
Explosion: 1.55 / 이동 없음 / 61 / 7
위 입자 수는 ParticleSystem.maxParticles 합계이며 항상 그 수만큼 방출하지 않습니다. Renderer 수 역시 구성 수이며 실제 드로콜 실측값이 아닙니다. 최신 생성 결과는 BuildReport.json을 확인하세요.
방향: 효과의 로컬 +Z가 전방입니다. 횡스크롤 +X는 Quaternion.LookRotation(Vector3.right), 반대는 Vector3.left로 배치하세요.
API namespace: ProjectM.CombatVfxPreview
GetComponent<CombatVfxSample>().Play()로 샘플을 명시적으로 시작합니다. SetPreviewTime(seconds)는 60Hz 입자/메시 시뮬레이션을 고정 난수로 재현합니다. ResetPreview()로 처음으로 되돌립니다.
배치/활성화만으로 재생하지 않습니다. 샘플 재사용 시 ResetPreview()로 입자와 재생 시간을 지우며 끝나면 Completed 이벤트로 알립니다. 오브젝트를 자동 파괴하거나 기존 PoolManager에 등록하지 않습니다.
GIF 캡처는 원점에 프리팹을 두고 60Hz 순서로 SetPreviewTime(t)를 호출하세요. 진행 위치는 내부 Visual_Travel이 계산하므로 루트를 중복 이동시키지 않습니다. 루프 끝에 약간의 빈 프레임을 남기면 효과 시작점이 구별됩니다.
SlashWave는 검기 메시가 전진하고 MagicMissile은 코어·나선·리본 꼬리와 입자를 남깁니다. DashBurst는 짧은 바람 잔상, Explosion은 섬광·각진 불꽃·불티·연기·공중/지면 충격파입니다.
투명 효과는 깊이 검사를 유지합니다. 꼬리는 일정 면적의 메시 리본이며 화면 전체 왜곡·충돌·동적 광원·VFX Graph·외부 텍스처는 사용하지 않습니다.
입자 수 상한과 메시 수는 BuildReport.json의 구성값이며 CPU/GPU 성능 실측이나 목표 기기 프레임 보장 수치가 아닙니다.
생성 메뉴: Project M > Samples > Build Four Combat VFX Samples / CombatVfxBuilder.Build(). 추가 Meshy 크레딧은 사용하지 않습니다.
";

    private const string ShaderSource = @"Shader ""ProjectM/Preview VFX""
{
    Properties
    {
        [HDR] _Tint(""Tint"", Color) = (1,1,1,1)
        _Mask(""Mask 0 Mesh / 2 Spark / 3 Smoke"", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend(""Source Blend"", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend(""Destination Blend"", Float) = 1
    }
    SubShader
    {
        Tags { ""RenderType""=""Transparent"" ""RenderPipeline""=""UniversalPipeline"" ""Queue""=""Transparent"" }
        Pass
        {
            Name ""VFXPreview""
            Tags { ""LightMode""=""UniversalForward"" }
            Blend [_SrcBlend] [_DstBlend]
            Cull Off ZWrite Off ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Mask, _SrcBlend, _DstBlend;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _Tint;
                output.uv = input.uv;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                half4 color = input.color;
                float2 p = input.uv * 2 - 1;
                if (_Mask > 2.5)
                {
                    float hex = max(abs(p.y), abs(p.x) * .866 + abs(p.y) * .5);
                    float facet = floor((p.x + p.y * .35) * 4) * .08;
                    color.rgb *= saturate(.72 - p.y * .18 + facet);
                    color.a *= 1 - smoothstep(.65, .98, hex);
                }
                else if (_Mask > 1.5)
                    color.a *= saturate(1 - abs(p.x)) * saturate(1 - abs(p.y));
                return color;
            }
            ENDHLSL
        }
    }
}
";
}
#endif
