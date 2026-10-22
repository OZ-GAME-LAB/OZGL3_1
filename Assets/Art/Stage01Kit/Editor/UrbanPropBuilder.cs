#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Preserves source FBXs and authors normalized, flat-colored rigid prop prefabs.</summary>
public static class UrbanPropBuilder
{
    const string Root = UrbanModuleBuilder.Root;
    const string MeshFolder = Root + "/Meshes/Props";
    const string PrefabFolder = Root + "/Prefabs/Props";
    // Match the overcast building palette; gate energy and tiny lamps retain distinct accents.
    static readonly Color Navy = Hex("22313E"), Metal = Hex("465563"), Ivory = Hex("A5AEAE");
    static readonly Color Teal = Hex("486F73"), DeepTeal = Hex("334F58"), Leaf = Hex("42564D");
    static readonly Color LeafLight = Hex("5B6A5B"), Brown = Hex("514A42"), Wood = Hex("766350");
    static readonly Color Ochre = Hex("9B895F"), Red = Hex("815D55"), Silver = Hex("7C8A91");
    static readonly Color WarmLamp = Glow("C1AD83", .25f), GateEnergy = Hex("498F8C"), GateCore = Glow("C0D3CB", .65f);
    static Material material;

    [MenuItem("Project M/Art/Build Urban Signature Props")]
    public static void Build()
    {
        Directory.CreateDirectory(MeshFolder);
        Directory.CreateDirectory(PrefabFolder);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Urban_SharedPalette.mat");
        if (!material) throw new InvalidOperationException("Run UrbanModuleBuilder.Build() before UrbanPropBuilder.Build().");
        var reports = new List<PropReport>();
        Create("StreetLamp", 4.3f, reports);
        Create("StreetTree", 4.5f, reports);
        Create("VendingMachine", 1.85f, reports);
        Create("Bench", .85f, reports);
        Create("TrashBin", .9f, reports);
        Create("ACUnit", .9f, reports);
        Create("GatePylon", 2.2f, reports);
        Potion("Potion_Health", Red, reports);
        Potion("Potion_Attack", Ochre, reports);
        Potion("Potion_Defense", Teal, reports);
        Potion("Potion_Speed", LeafLight, reports);
        Directory.CreateDirectory("ArtSources/Stage01Kit");
        File.WriteAllText("ArtSources/Stage01Kit/prop-normalization.json", JsonUtility.ToJson(new PropReports { props = reports.ToArray() }, true));
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("UrbanPropBuilder: seven normalized Meshy props and four potion prefabs generated.");
    }

    static void Create(string key, float targetHeight, List<PropReport> reports)
    {
        string sourcePath = Root + "/Models/" + key + ".fbx";
        var importer = AssetImporter.GetAtPath(sourcePath) as ModelImporter;
        if (importer == null) throw new FileNotFoundException("Missing source prop FBX: " + sourcePath);
        if (!importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        if (!source) throw new InvalidOperationException("Could not load prop " + sourcePath);
        var instance = UnityEngine.Object.Instantiate(source);
        instance.hideFlags = HideFlags.HideAndDontSave;
        var g = new PropGeo();
        int sourceTriangles = 0;
        try
        {
            var faces = new List<Face>();
            var bounds = new Bounds();
            bool first = true;
            var rotation = Quaternion.Euler(0, 180, 0);
            foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (!mesh) continue;
                var v = mesh.vertices;
                var ix = mesh.triangles;
                var matrix = filter.transform.localToWorldMatrix;
                bool mirrored = matrix.determinant < 0;
                for (int i = 0; i + 2 < ix.Length; i += 3)
                {
                    var a = rotation * matrix.MultiplyPoint3x4(v[ix[i]]);
                    var b = rotation * matrix.MultiplyPoint3x4(v[ix[i + 1]]);
                    var c = rotation * matrix.MultiplyPoint3x4(v[ix[i + 2]]);
                    if (mirrored) { var swap = b; b = c; c = swap; }
                    if (Vector3.Cross(b - a, c - a).sqrMagnitude < 1e-16f) continue;
                    faces.Add(new Face(a, b, c));
                    foreach (var p in new[] { a, b, c })
                    {
                        if (first) { bounds = new Bounds(p, Vector3.zero); first = false; }
                        else bounds.Encapsulate(p);
                    }
                }
            }
            if (first || bounds.size.y < .00001f) throw new InvalidOperationException("Empty prop source: " + key);
            sourceTriangles = faces.Count;
            float scale = targetHeight / bounds.size.y;
            Vector3 origin = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            var size = bounds.size * scale;
            foreach (var f in faces)
            {
                Vector3 a = (f.a - origin) * scale, b = (f.b - origin) * scale, c = (f.c - origin) * scale;
                var center = (a + b + c) / 3;
                var normal = Vector3.Cross(b - a, c - a).normalized;
                g.Tri(a, b, c, SurfaceColor(key, center, normal, size));
            }
            AddAuthoredDetails(key, g, size);
            Save(key, g, sourcePath, sourceTriangles, reports, key == "VendingMachine" || key == "TrashBin");
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    static Color SurfaceColor(string key, Vector3 p, Vector3 normal, Vector3 size)
    {
        float y = p.y / size.y;
        float front = -size.z * .20f;
        Color color;
        switch (key)
        {
            case "StreetLamp":
                color = y > .77f && y < .94f && Mathf.Abs(normal.y) < .55f ? WarmLamp : Navy;
                if (y > .11f && y < .14f) color = Teal;
                break;
            case "StreetTree":
                color = y < .49f ? Brown : Leaf;
                if (y > .49f && normal.y > .25f) color = LeafLight;
                break;
            case "VendingMachine":
                color = Teal;
                if (y < .09f) color = Navy;
                if (p.z < front)
                {
                    color = Ivory;
                    if (y > .35f && y < .85f && p.x > -size.x * .37f && p.x < size.x * .25f) color = Navy;
                    if (y > .08f && y < .25f && Mathf.Abs(p.x) < size.x * .3f) color = DeepTeal;
                    if (y > .88f) color = DeepTeal;
                }
                break;
            case "Bench":
                color = y > .4f ? Wood : Metal;
                if (y > .42f && normal.y > .5f) color = Ochre;
                break;
            case "TrashBin":
                color = y > .72f || y < .08f ? Navy : Teal;
                if (y > .69f && y < .75f) color = Silver;
                break;
            case "ACUnit":
                color = Ivory;
                if (y < .11f || p.x > size.x * .43f) color = Metal;
                if (p.z < front && new Vector2(p.x + size.x * .06f, p.y - size.y * .53f).magnitude < size.y * .345f) color = Navy;
                break;
            case "GatePylon":
                color = y < .29f ? Navy : Silver;
                if (y > .11f && y < .17f) color = Teal;
                if (y > .34f && Mathf.Abs(normal.z) < .48f) color = Metal;
                break;
            default: color = Metal; break;
        }
        // Sparse face variation preserves large color blocks without texture noise.
        return color;
    }

    static void AddAuthoredDetails(string key, PropGeo g, Vector3 size)
    {
        float h = size.y, w = size.x, d = size.z;
        float front = -d * .5f;
        if (key == "StreetTree")
        {
            g.Bush(new Vector3(-w * .19f, h * .77f, -d * .08f), new Vector3(w * .58f, h * .26f, d * .68f), Leaf);
            g.Bush(new Vector3(w * .2f, h * .81f, d * .03f), new Vector3(w * .58f, h * .28f, d * .65f), LeafLight);
            g.Bush(new Vector3(-w * .03f, h * .88f, d * .13f), new Vector3(w * .52f, h * .23f, d * .55f), Leaf);
        }
        else if (key == "VendingMachine")
        {
            // Close the generated lower shell and give it a readable recessed delivery slot.
            g.Box(new Vector3(0,h*.18f,front+.006f),new Vector3(w*.78f,h*.29f,.055f),DeepTeal);
            g.Box(new Vector3(-w*.06f,h*.205f,front-.025f),new Vector3(w*.47f,h*.075f,.018f),Navy);
            g.Box(new Vector3(-w*.06f,h*.16f,front-.045f),new Vector3(w*.50f,h*.018f,.09f),Metal);
            // An opaque dark display and miniature eight-sided cans replace empty generated shelves.
            g.Box(new Vector3(-w * .06f, h * .605f, front - .003f), new Vector3(w * .59f, h * .48f, .012f), Navy);
            var colors = new[] { Red, Ochre, Teal, Silver };
            float canR = Mathf.Min(w * .052f, h * .027f);
            for (int row = 0; row < 3; row++)
            {
                float y = h * (.435f + row * .166f);
                for (int col = 0; col < 4; col++)
                {
                    float x = w * (-.275f + col * .145f);
                    var center = new Vector3(x, y, front - canR * .72f);
                    g.Cylinder(center, canR, h * .105f, 8, colors[(col + row) % colors.Length]);
                    g.Cylinder(center + Vector3.up * h * .053f, canR * .92f, h * .008f, 8, Silver);
                    g.Box(new Vector3(x, y, front - canR * 1.75f), new Vector3(canR, h * .028f, .004f), Ivory);
                }
                g.Box(new Vector3(-w * .057f, y - h * .064f, front - .015f), new Vector3(w * .61f, h * .018f, .04f), Metal);
            }
            g.Box(new Vector3(w * .35f, h * .66f, front - .016f), new Vector3(w * .1f, h * .12f, .02f), DeepTeal);
            for (int i = 0; i < 3; i++) g.Box(new Vector3(w * .35f, h * (.48f + i * .046f), front - .02f), new Vector3(w * .055f, h * .022f, .025f), Ochre);
            g.Box(new Vector3(-w * .06f, h * .935f, front - .01f), new Vector3(w * .55f, h * .042f, .02f), WarmLamp);
        }
        else if (key == "ACUnit")
        {
            Vector3 center = new Vector3(-w * .06f, h * .53f, front - .009f);
            var orientation = Quaternion.Euler(90, 0, 0);
            g.Cylinder(center, h * .343f, .014f, 12, Navy, orientation);
            for (int i = 0; i < 5; i++)
            {
                var rotation = Quaternion.Euler(0, 0, i * 72 + 16);
                var offset = rotation * new Vector3(0, h * .145f, 0);
                g.Box(center + offset + new Vector3(0, 0, -.016f), new Vector3(h * .087f, h * .31f, .022f), rotation, Metal);
            }
            g.Cylinder(center + new Vector3(0, 0, -.035f), h * .069f, .035f, 8, Silver, orientation);
            // Few broad grille bars keep this legible without a dense fan grille.
            for (int i = -1; i <= 1; i++)
                g.Box(center + new Vector3(i * h * .18f, 0, -.06f), new Vector3(.012f, h * .58f, .014f), Silver);
        }
        else if (key == "GatePylon")
        {
            g.Crystal(new Vector3(0, h * .68f, -d * .08f), new Vector3(h * .33f, h * .49f, h * .23f), GateEnergy);
            g.Crystal(new Vector3(0, h * .68f, -d * .08f - h * .125f), new Vector3(h * .105f, h * .28f, h * .035f), GateCore);
        }
    }

    static void Potion(string key, Color liquid, List<PropReport> reports)
    {
        var g = new PropGeo();
        // Solid matte color is intentional: no transparency sorting or extra glass pass.
        g.Cylinder(new Vector3(0, .083f, 0), .06f, .125f, 8, liquid);
        g.Cone(new Vector3(0, .16f, 0), .06f, .028f, .03f, 8, Ivory);
        g.Cylinder(new Vector3(0, .187f, 0), .028f, .024f, 8, Ivory);
        g.Cylinder(new Vector3(0, .218f, 0), .035f, .038f, 8, Navy);
        g.Cylinder(new Vector3(0, .016f, 0), .062f, .032f, 8, Ivory);
        g.Box(new Vector3(0, .087f, -.059f), new Vector3(.052f, .047f, .008f), Ivory);
        g.Box(new Vector3(0, .087f, -.065f), new Vector3(.031f, .013f, .006f), liquid);
        if (key == "Potion_Health") g.Box(new Vector3(0, .087f, -.067f), new Vector3(.013f, .031f, .006f), liquid);
        Save(key, g, "Original authored polygon bottle", 0, reports, false);
    }

    static void Save(string key, PropGeo g, string source, int sourceTriangles, List<PropReport> reports, bool box)
    {
        var mesh = g.ToMesh(key + "_FlatPalette");
        string path = MeshFolder + "/" + key + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing)
        {
            EditorUtility.CopySerialized(mesh, existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            mesh = existing;
            EditorUtility.SetDirty(mesh);
        }
        else AssetDatabase.CreateAsset(mesh, path);
        var root = new GameObject(key);
        root.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = root.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        if (box)
        {
            var collider = root.AddComponent<BoxCollider>();
            collider.center = mesh.bounds.center;
            collider.size = mesh.bounds.size;
        }
        var anchor = new GameObject("GroundPivot");
        anchor.transform.SetParent(root.transform, false);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/" + key + ".prefab");
        reports.Add(new PropReport
        {
            key = key, source = source, prefab = PrefabFolder + "/" + key + ".prefab",
            dimensions_m = mesh.bounds.size, center_m = mesh.bounds.center,
            source_triangles = sourceTriangles, final_triangles = mesh.triangles.Length / 3,
            front = "-Z", pivot = "Source base Y=0, footprint centered XZ", materials = 1,
            collision = box ? "single body box; place outside player lane if decorative" : "none; decorative"
        });
        UnityEngine.Object.DestroyImmediate(root);
    }

    static Color Hex(string s) { ColorUtility.TryParseHtmlString("#" + s, out var c); return c; }
    static Color Glow(string s,float alpha){Color c=Hex(s);c.a=alpha;return c;}
    readonly struct Face
    {
        public readonly Vector3 a, b, c;
        public Face(Vector3 a, Vector3 b, Vector3 c) { this.a = a; this.b = b; this.c = c; }
    }
    [Serializable] sealed class PropReports { public PropReport[] props; }
    [Serializable] sealed class PropReport
    {
        public string key, source, prefab, front, pivot, collision;
        public Vector3 dimensions_m, center_m;
        public int source_triangles, final_triangles, materials;
    }

    sealed class PropGeo
    {
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> ix = new List<int>();
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            int start = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            colors.Add(color); colors.Add(color); colors.Add(color);
            ix.Add(start); ix.Add(start + 1); ix.Add(start + 2);
        }
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color) { Tri(a, b, c, color); Tri(a, c, d, color); }
        public void Box(Vector3 p, Vector3 size, Color c) => Box(p, size, Quaternion.identity, c);
        public void Box(Vector3 p, Vector3 size, Quaternion rotation, Color c)
        {
            var h = size * .5f;
            var a = new[] {
                new Vector3(-h.x,-h.y,-h.z), new Vector3(h.x,-h.y,-h.z), new Vector3(h.x,h.y,-h.z), new Vector3(-h.x,h.y,-h.z),
                new Vector3(-h.x,-h.y,h.z), new Vector3(h.x,-h.y,h.z), new Vector3(h.x,h.y,h.z), new Vector3(-h.x,h.y,h.z)
            };
            for (int i = 0; i < a.Length; i++) a[i] = p + rotation * a[i];
            Quad(a[0], a[3], a[2], a[1], c); Quad(a[4], a[5], a[6], a[7], c);
            Quad(a[0], a[4], a[7], a[3], c); Quad(a[1], a[2], a[6], a[5], c);
            Quad(a[3], a[7], a[6], a[2], c); Quad(a[0], a[1], a[5], a[4], c);
        }
        public void Cylinder(Vector3 p, float r, float h, int sides, Color c) => Cone(p, r, r, h, sides, c, Quaternion.identity);
        public void Cylinder(Vector3 p, float r, float h, int sides, Color c, Quaternion rotation) => Cone(p, r, r, h, sides, c, rotation);
        public void Cone(Vector3 p, float bottomRadius, float topRadius, float h, int sides, Color c) => Cone(p, bottomRadius, topRadius, h, sides, c, Quaternion.identity);
        void Cone(Vector3 p, float bottomRadius, float topRadius, float h, int sides, Color c, Quaternion rotation)
        {
            var bottom = new Vector3(0, -h * .5f, 0); var top = new Vector3(0, h * .5f, 0);
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides, b = (i + 1) * Mathf.PI * 2 / sides;
                var aa = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); var bb = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b));
                Vector3 b0 = p + rotation * (bottom + aa * bottomRadius), b1 = p + rotation * (bottom + bb * bottomRadius);
                Vector3 t0 = p + rotation * (top + aa * topRadius), t1 = p + rotation * (top + bb * topRadius);
                Quad(b0, t0, t1, b1, c);
                Tri(p + rotation * top, t1, t0, c);
                Tri(p + rotation * bottom, b0, b1, c);
            }
        }
        public void Bush(Vector3 center, Vector3 size, Color c)
        {
            var upper = center + Vector3.up * size.y * .5f;
            var lower = center - Vector3.up * size.y * .5f;
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2 / 7, b = (i + 1) * Mathf.PI * 2 / 7;
                var p = center + new Vector3(Mathf.Cos(a) * size.x * .5f, 0, Mathf.Sin(a) * size.z * .5f);
                var q = center + new Vector3(Mathf.Cos(b) * size.x * .5f, 0, Mathf.Sin(b) * size.z * .5f);
                Tri(upper, q, p, Color.Lerp(c, LeafLight, i % 2 == 0 ? .2f : 0));
                Tri(lower, p, q, c);
            }
        }
        public void Crystal(Vector3 center, Vector3 size, Color c)
        {
            var top = center + Vector3.up * size.y * .5f;
            var bottom = center - Vector3.up * size.y * .5f;
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI * 2 / 6, b = (i + 1) * Mathf.PI * 2 / 6;
                var p = center + new Vector3(Mathf.Cos(a) * size.x * .5f, -size.y * .05f, Mathf.Sin(a) * size.z * .5f);
                var q = center + new Vector3(Mathf.Cos(b) * size.x * .5f, -size.y * .05f, Mathf.Sin(b) * size.z * .5f);
                Tri(top, q, p, i % 2 == 0 ? c : Color.Lerp(c, Ivory, .24f));
                Tri(bottom, p, q, Color.Lerp(c, Navy, .18f));
            }
        }
        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (v.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(v); mesh.SetColors(colors); mesh.SetTriangles(ix, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
#endif
