// Revision of retained environment props: broad authored silhouettes, one shared palette.
// The retained mesh, prefab, root components and GroundPivot keep their original GUID/local IDs.
#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class Stage01SimplifiedProps
{
    const string Root = "Assets/Art/Stage01Kit";
    const string MeshFolder = Root + "/Meshes/Props";
    const string PrefabFolder = Root + "/Prefabs/Props";
    static readonly Color Navy = Hex("22313E"), Metal = Hex("465563"), Ivory = Hex("A5AEAE");
    static readonly Color Light = Hex("747D84"), Dark = Hex("434E5A"), Teal = Hex("486F73"), DeepTeal = Hex("334F58");
    static readonly Color Leaf = Hex("42564D"), LeafLight = Hex("5B6A5B"), Wood = Hex("514A42");
    static readonly Color Ochre = Hex("9B895F"), Red = Hex("815D55");
    static readonly Color Lamp = new Color(.7569f, .6784f, .5137f, .25f);

    [MenuItem("Project M/Art/Rebuild Simplified Street Props")]
    public static void Build()
    {
        foreach (string key in new[] { "StreetLamp", "StreetTree", "VendingMachine", "ACUnit", "SatelliteDish" }) BuildOne(key);
        AssetDatabase.SaveAssets();
        Debug.Log("STAGE01_SIMPLIFIED_PROPS_COMPLETE 5 retained models revised");
    }

    public static bool IsRevised(string key)
    {
        return key == "StreetLamp" || key == "StreetTree" || key == "VendingMachine" || key == "ACUnit" || key == "SatelliteDish" || key == "GatePylon";
    }

    public static void BuildOne(string key)
    {
        if (!IsRevised(key)) throw new ArgumentException("Not a revised Stage 1 prop: " + key);
        if (key == "GatePylon") key = "SatelliteDish";
        Directory.CreateDirectory(MeshFolder);
        Directory.CreateDirectory(PrefabFolder);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if (key == "SatelliteDish")
        {
            MoveRetained(MeshFolder + "/GatePylon.asset", MeshFolder + "/SatelliteDish.asset");
            MoveRetained(PrefabFolder + "/GatePylon.prefab", PrefabFolder + "/SatelliteDish.prefab");
        }
        S1Geo geometry;
        switch (key)
        {
            case "StreetLamp": geometry = StreetLamp(); break;
            case "StreetTree": geometry = StreetTree(); break;
            case "VendingMachine": geometry = VendingMachine(); break;
            case "ACUnit": geometry = ACUnit(); break;
            default: geometry = SatelliteDish(); break;
        }
        SaveRetained(key, geometry);
    }

    static void MoveRetained(string oldPath, string newPath)
    {
        if (!File.Exists(oldPath)) return;
        if (File.Exists(newPath)) throw new InvalidOperationException("Both retained asset paths exist; resolve without replacing a GUID: " + oldPath + " / " + newPath);
        string error = AssetDatabase.MoveAsset(oldPath, newPath);
        if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
    }

    static void SaveRetained(string key, S1Geo geometry)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Urban_SharedPalette.mat");
        if (!material) throw new FileNotFoundException("The retained Urban_SharedPalette material is required.");
        var generated = geometry.ToMesh(key + "_FlatPalette");
        string meshPath = MeshFolder + "/" + key + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (mesh)
        {
            EditorUtility.CopySerialized(generated, mesh);
            Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(mesh);
        }
        else { mesh = generated; AssetDatabase.CreateAsset(mesh, meshPath); }
        string prefabPath = PrefabFolder + "/" + key + ".prefab";
        bool retained = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        GameObject root = retained ? PrefabUtility.LoadPrefabContents(prefabPath) : new GameObject(key);
        try
        {
            // Update the loaded root rather than replacing its component hierarchy. Existing
            // prefab-instance transform overrides therefore still address the same local IDs.
            root.name = key;
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            var filter = root.GetComponent<MeshFilter>();
            if (!filter) filter = root.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = root.GetComponent<MeshRenderer>();
            if (!renderer) renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { material };
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            if (key == "VendingMachine")
            {
                var box = root.GetComponent<BoxCollider>();
                if (!box) box = root.AddComponent<BoxCollider>();
                box.center = new Vector3(0, .925f, 0);
                box.size = new Vector3(1.02f, 1.85f, .78f);
            }
            var pivot = root.transform.Find("GroundPivot");
            if (!pivot)
            {
                pivot = new GameObject("GroundPivot").transform;
                pivot.SetParent(root.transform, false);
            }
            pivot.localPosition = Vector3.zero;
            pivot.localRotation = Quaternion.identity;
            pivot.localScale = Vector3.one;
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log("STAGE01_SIMPLIFIED_PROP " + key + " triangles=" + mesh.triangles.Length / 3 + " bounds=" + mesh.bounds.size);
        }
        finally
        {
            if (retained) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
        }
    }

    static S1Geo StreetLamp()
    {
        var g = new S1Geo();
        // Every vertical section shares XZ=(0,0); no crooked generated neck or offset head.
        g.Cone(V(0, .09f, 0), .28f, .23f, .18f, 8, Dark);
        g.Cone(V(0, .36f, 0), .13f, .105f, .36f, 6, Metal);
        g.Cylinder(V(0, 1.94f, 0), .067f, 2.8f, 6, Metal);
        g.TaperedBox(V(0, 3.39f, 0), V(.25f, .1f, .25f), new Vector2(.38f, .38f), Dark);
        g.Box(V(0, 3.67f, 0), V(.4f, .46f, .4f), Lamp);
        foreach (float x in new[] { -.23f, .23f })
            foreach (float z in new[] { -.23f, .23f }) g.Box(V(x, 3.68f, z), V(.045f, .5f, .045f), Metal);
        g.Box(V(0, 3.43f, 0), V(.55f, .06f, .55f), Dark);
        g.Box(V(0, 3.96f, 0), V(.57f, .08f, .57f), Metal);
        // A square pyramid aligns all four eaves with the square lantern frame.
        // A four-sided radial cone would rotate its diamond footprint by 45 degrees.
        var a=V(-.32f,4,-.32f);var b=V(.32f,4,-.32f);
        var c=V(.32f,4,.32f);var d=V(-.32f,4,.32f);var tip=V(0,4.3f,0);
        g.Tri(a,tip,b,Metal);g.Tri(b,tip,c,Metal);g.Tri(c,tip,d,Metal);g.Tri(d,tip,a,Metal);
        g.Tri(a,b,c,Metal);g.Tri(a,c,d,Metal);
        return g;
    }

    static S1Geo StreetTree()
    {
        var g = new S1Geo();
        g.Cone(V(0, 1.2f, 0), .18f, .105f, 2.4f, 8, Wood);
        g.Beam(V(0, 1.9f, 0), V(-.54f, 2.92f, .06f), .115f, .11f, Wood);
        g.Beam(V(0, 1.9f, 0), V(.54f, 2.98f, .1f), .115f, .11f, Wood);
        Canopy(g, V(-.57f, 3.04f, .03f), V(1.64f, 1.7f, 1.68f), .08f, Leaf);
        Canopy(g, V(.57f, 3.2f, .08f), V(1.65f, 1.78f, 1.62f), .34f, LeafLight);
        Canopy(g, V(-.06f, 3.625f, -.04f), V(1.65f, 1.75f, 1.6f), .19f, Leaf);
        return g;
    }

    static void Canopy(S1Geo g, Vector3 c, Vector3 size, float phase, Color color)
    {
        const int n = 8;
        var rings = new Vector3[3, n];
        float[] yy = { -.29f, .015f, .29f }, rr = { .72f, 1f, .7f };
        for (int row = 0; row < 3; row++)
            for (int i = 0; i < n; i++)
            {
                float angle = i * Mathf.PI * 2 / n + phase + row * .035f;
                float irregular = 1 + .075f * Mathf.Sin(i * 2.3f + row * 1.7f + phase);
                rings[row, i] = c + V(Mathf.Cos(angle) * size.x * .5f * rr[row] * irregular,
                    yy[row] * size.y, Mathf.Sin(angle) * size.z * .5f * rr[row] * irregular);
            }
        Vector3 bottom = c - Vector3.up * size.y * .5f, top = c + Vector3.up * size.y * .5f;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            Color facet = i % 3 == 0 ? Color.Lerp(color, LeafLight, .25f) : color;
            g.Tri(bottom, rings[0, i], rings[0, j], facet);
            for (int row = 0; row < 2; row++) g.Quad(rings[row, i], rings[row + 1, i], rings[row + 1, j], rings[row, j], facet);
            g.Tri(top, rings[2, j], rings[2, i], facet);
        }
    }

    static S1Geo VendingMachine()
    {
        var g = new S1Geo();
        BeveledBody(g, V(0, .925f, 0), V(1.02f, 1.85f, .78f), .055f, DeepTeal);
        g.Box(V(-.12f, 1.1f, -.402f), V(.7f, 1.18f, .04f), Ivory);
        g.Box(V(-.12f, 1.1f, -.427f), V(.61f, 1.08f, .018f), Navy);
        for (int row = 0; row < 2; row++)
        {
            float y = .83f + row * .48f;
            for (int col = 0; col < 3; col++)
                g.Cylinder(V(-.325f + col * .205f, y, -.45f), .067f, .29f, 6, col == 0 ? Teal : col == 1 ? Ochre : Red);
            g.Box(V(-.12f, y - .17f, -.458f), V(.59f, .045f, .09f), Light);
        }
        // One payment slot and one broad purchase control, without tiny repeated buttons.
        g.Box(V(.37f, 1.2f, -.414f), V(.14f, .25f, .035f), Navy);
        g.Box(V(.37f, .82f, -.421f), V(.13f, .1f, .045f), Ochre);
        g.Box(V(-.12f, .32f, -.407f), V(.58f, .19f, .035f), Navy);
        g.Box(V(-.12f, .2f, -.435f), V(.64f, .065f, .12f), Metal);
        g.Box(V(-.12f, 1.74f, -.405f), V(.68f, .065f, .025f), Ivory);
        return g;
    }

    static S1Geo ACUnit()
    {
        var g = new S1Geo();
        foreach (float x in new[] { -.46f, .46f }) g.Box(V(x, .04f, 0), V(.2f, .08f, .52f), Metal);
        BeveledBody(g, V(0, .49f, 0), V(1.4f, .82f, .6f), .04f, Light);
        Vector3 fan = V(-.2f, .5f, -.319f);
        CylinderAxis(g, fan, .305f, .025f, 8, Navy, Quaternion.Euler(90, 0, 0));
        // A broad octagonal rim and three visible blades replace dense generated grille wires.
        Ring(g, fan + V(0, 0, -.019f), .33f, .29f, 8, Ivory, Quaternion.identity);
        for (int i = 0; i < 3; i++)
        {
            Quaternion rot = Quaternion.Euler(0, 0, 20 + i * 120);
            g.BoxRotated(fan + rot * V(0, .135f, 0) + V(0, 0, -.035f), V(.1f, .26f, .026f), rot, Metal);
        }
        CylinderAxis(g, fan + V(0, 0, -.058f), .07f, .035f, 8, Ivory, Quaternion.Euler(90, 0, 0));
        g.Box(V(.47f, .5f, -.312f), V(.28f, .59f, .025f), Metal);
        g.Box(V(.47f, .64f, -.332f), V(.17f, .065f, .018f), Ivory);
        return g;
    }

    static S1Geo SatelliteDish()
    {
        var g = new S1Geo();
        var center = V(0, .86f, 0);
        var tilt = Quaternion.Euler(25, 0, 0);
        g.Box(V(0, .06f, .22f), V(.62f, .12f, .66f), Dark);
        g.Cone(V(0, .43f, .22f), .09f, .07f, .62f, 8, Metal);
        g.BoxRotated(center + tilt * V(0, 0, .21f), V(.29f, .29f, .14f), tilt, Metal);
        g.Beam(V(-.22f, .12f, .43f), V(0, .68f, .22f), .055f, .055f, Metal);
        g.Beam(V(.22f, .12f, .43f), V(0, .68f, .22f), .055f, .055f, Metal);
        // Two radial bands form a visibly concave bowl. Both shell surfaces and rim are closed;
        // the front faces -Z, tilted upward by 25 degrees, with no central antenna tower.
        const int sides = 12;
        const float thickness = .045f;
        float[] radii = { .4f, .75f }, depth = { .04f, -.14f };
        for (int i = 0; i < sides; i++)
        {
            float a = Mathf.PI * 2 * i / sides, b = Mathf.PI * 2 * (i + 1) / sides;
            Vector3 innerA = BowlPoint(center, tilt, radii[0], depth[0], a);
            Vector3 innerB = BowlPoint(center, tilt, radii[0], depth[0], b);
            Vector3 outerA = BowlPoint(center, tilt, radii[1], depth[1], a);
            Vector3 outerB = BowlPoint(center, tilt, radii[1], depth[1], b);
            Vector3 back = tilt * V(0, 0, thickness);
            g.Tri(center + tilt * V(0, 0, .13f), innerB, innerA, Ivory);
            g.Quad(innerA, innerB, outerB, outerA, Ivory);
            g.Tri(center + tilt * V(0, 0, .13f + thickness), innerA + back, innerB + back, Light);
            g.Quad(innerA + back, outerA + back, outerB + back, innerB + back, Light);
            g.Quad(outerA, outerB, outerB + back, outerA + back, Metal);
        }
        Vector3 armStart = center + tilt * V(0, -.63f, -.1f);
        Vector3 elbow = center + tilt * V(0, -.58f, -.6f);
        Vector3 feed = center + tilt * V(0, -.025f, -.6f);
        g.Beam(armStart, elbow, .055f, .055f, Metal);
        g.Beam(elbow, feed, .055f, .055f, Metal);
        g.BoxRotated(feed, V(.15f, .15f, .21f), tilt, DeepTeal);
        return g;
    }

    static Vector3 BowlPoint(Vector3 center, Quaternion rotation, float radius, float depth, float angle)
    { return center + rotation * V(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, depth); }

    static void BeveledBody(S1Geo g, Vector3 center, Vector3 size, float bevel, Color color)
    {
        float x = size.x * .5f, y = size.y * .5f, z = size.z * .5f;
        var profile = new[] { V(-x + bevel, -y, 0), V(x - bevel, -y, 0), V(x, -y + bevel, 0), V(x, y - bevel, 0),
            V(x - bevel, y, 0), V(-x + bevel, y, 0), V(-x, y - bevel, 0), V(-x, -y + bevel, 0) };
        for (int i = 0; i < profile.Length; i++)
        {
            int j = (i + 1) % profile.Length;
            Vector3 a = center + profile[i] + V(0, 0, -z), b = center + profile[j] + V(0, 0, -z);
            Vector3 c = center + profile[i] + V(0, 0, z), d = center + profile[j] + V(0, 0, z);
            g.Tri(center + V(0, 0, -z), b, a, color);
            g.Tri(center + V(0, 0, z), c, d, color);
            g.Quad(a, b, d, c, color);
        }
    }

    static void CylinderAxis(S1Geo g, Vector3 c, float r, float h, int sides, Color color, Quaternion q)
    {
        Vector3 bot = c + q * V(0, -h * .5f, 0), top = c + q * V(0, h * .5f, 0);
        for (int i = 0; i < sides; i++)
        {
            float a = Mathf.PI * 2 * i / sides, b = Mathf.PI * 2 * (i + 1) / sides;
            Vector3 u = q * V(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r), v = q * V(Mathf.Cos(b) * r, 0, Mathf.Sin(b) * r);
            g.Quad(bot + u, top + u, top + v, bot + v, color);
            g.Tri(top, top + v, top + u, color);
            g.Tri(bot, bot + u, bot + v, color);
        }
    }

    static void Ring(S1Geo g, Vector3 c, float outer, float inner, int sides, Color color, Quaternion q)
    {
        for (int i = 0; i < sides; i++)
        {
            float a = Mathf.PI * 2 * i / sides, b = Mathf.PI * 2 * (i + 1) / sides;
            Vector3 ua = V(Mathf.Cos(a), Mathf.Sin(a), 0), ub = V(Mathf.Cos(b), Mathf.Sin(b), 0);
            g.Quad(c + q * (ua * inner), c + q * (ub * inner), c + q * (ub * outer), c + q * (ua * outer), color);
        }
    }

    static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
    static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var c); return c; }
}
#endif
