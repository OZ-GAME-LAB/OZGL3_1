// Original modular art for Project M. All dimensions are meters; no external art dependency.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class UrbanModuleBuilder
{
    public const string Root = "Assets/Art/Stage01Kit";
    public const string PrefabFolder = Root + "/Prefabs/Modules";
    const string MeshFolder = Root + "/Meshes";
    const string MaterialFolder = Root + "/Materials";
    static Material material;

    // Overcast city: cool muted masses, lighter walkable edges and sparse warm windows.
    // Color remains in vertices, so the entire kit still shares one opaque material.
    static readonly Color Concrete = Hex("5B626B");
    static readonly Color ConcreteLight = Hex("747D84");
    static readonly Color ConcreteDark = Hex("434E5A");
    static readonly Color Asphalt = Hex("29333F");
    static readonly Color Navy = Hex("22313E");
    static readonly Color Metal = Hex("465563");
    static readonly Color Teal = Hex("486F73");
    static readonly Color TealDark = Hex("334F58");
    static readonly Color Glass = Hex("485F69");
    static readonly Color GlassDark = Hex("2E424F");
    static readonly Color WarmWindow = new Color(.7255f,.6431f,.4784f,.65f);
    static readonly Color Ivory = Hex("A5AEAE");
    static readonly Color Ochre = Hex("9B895F");
    static readonly Color Red = Hex("815D55");
    static readonly Color Green = Hex("42564D");
    static readonly Color LeafLight = Hex("5B6A5B");
    static readonly Color Dirt = Hex("4C4943");

    [MenuItem("Project M/Art/Build Urban Platform Modules")]
    public static void Build()
    {
        Directory.CreateDirectory(PrefabFolder);
        Directory.CreateDirectory(MeshFolder);
        Directory.CreateDirectory(MaterialFolder);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        MakeMaterial();
        Building("Building_ShopLow", 8, 4, 4, 0);
        Building("Building_OfficeMid", 8, 8, 5, 1);
        Building("Building_ApartmentTall", 10, 12, 6, 2);
        Building("Building_RooftopArena", 18, 4, 6, 3);
        RooftopHut();
        Road();
        Sidewalk();
        RoofBridge();
        ExteriorStairs();
        Ladder();
        Guardrail();
        UtilityBox();
        Planter();
        RoadBarrier();
        WaterTank();
        Billboard();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("UrbanModuleBuilder: 16 original low-poly module prefabs generated in " + PrefabFolder);
    }

    static void MakeMaterial()
    {
        var shaderPath = MaterialFolder + "/Urban_VertexColor.shader";
        if (!File.Exists(shaderPath) || File.ReadAllText(shaderPath) != ShaderSource)
            File.WriteAllText(shaderPath, ShaderSource);
        AssetDatabase.ImportAsset(shaderPath, ImportAssetOptions.ForceSynchronousImport);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        if (!shader) throw new InvalidOperationException("Urban vertex-color shader did not import.");
        var path = MaterialFolder + "/Urban_SharedPalette.mat";
        material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material)
        {
            material = new Material(shader) { name = "Urban_SharedPalette" };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetColor("_BaseColor", Color.white);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
    }

    static void Building(string name, float w, float h, float d, int type)
    {
        var root = NewRoot(name);
        var high = BuildingMesh(w, h, d, type, true);
        var low = BuildingMesh(w, h, d, type, false);
        var a = AttachMesh(root, "Geometry_LOD0", high, name + "_LOD0");
        var b = AttachMesh(root, "Geometry_LOD1", low, name + "_LOD1");
        var lod = root.AddComponent<LODGroup>();
        lod.SetLODs(new[] { new LOD(.10f, new Renderer[] { a }), new LOD(.025f, new Renderer[] { b }) });
        lod.fadeMode = LODFadeMode.None;
        lod.RecalculateBounds();
        BoxCollision(root, "SolidBuilding", new Vector3(0, h * .5f, 0), new Vector3(w, h, d));
        Anchor(root, "RoofSurface", new Vector3(0, h, 0));
        Anchor(root, "GroundFront", new Vector3(0, 0, -d * .5f - .75f));
        Anchor(root, "RoofLeft", new Vector3(-w * .5f, h, 0));
        Anchor(root, "RoofRight", new Vector3(w * .5f, h, 0));
        Save(root);
    }

    static Geo BuildingMesh(float w, float h, float d, int type, bool detailed)
    {
        var g = new Geo();
        var body = type == 0 ? ConcreteLight : type == 1 ? ConcreteDark : type == 2 ? Concrete : Navy;
        g.Box(new Vector3(0, (h - .26f) * .5f, 0), new Vector3(w, h - .26f, d), body);
        // Beveled broad roof caps and footings make the outline read at gameplay distance.
        g.Box(new Vector3(0, .13f, 0), new Vector3(w + .12f, .26f, d + .12f), ConcreteDark);
        g.Box(new Vector3(0, h - .13f, 0), new Vector3(w + .18f, .26f, d + .18f), Ivory);
        g.Box(new Vector3(0, h + .004f, 0), new Vector3(w - .12f, .008f, d - .12f), ConcreteDark);
        // Front and back are deliberately open at roof level; low rear side coping is decorative.
        g.Box(new Vector3(0, h + .15f, d * .5f - .09f), new Vector3(w, .3f, .18f), ConcreteLight);
        if (detailed)
        {
            g.Box(new Vector3(-w * .5f + .08f, h + .15f, d * .26f), new Vector3(.16f, .3f, d * .48f), ConcreteLight);
            g.Box(new Vector3(w * .5f - .08f, h + .15f, d * .26f), new Vector3(.16f, .3f, d * .48f), ConcreteLight);
            for (int side = -1; side <= 1; side += 2)
                g.Box(new Vector3(side * (w * .5f - .14f), h * .5f, -d * .5f - .035f), new Vector3(.28f, h, .07f), ConcreteDark);
        }
        if (type == 0)
        {
            // A small Seoul street shop with recessed glazing, framed doors, solid striped awning.
            g.Box(new Vector3(0, 2.8f, -d * .5f - .08f), new Vector3(w - .6f, .72f, .2f), TealDark);
            g.Box(new Vector3(0, 3.14f, -d * .5f - .17f), new Vector3(w - .42f, .1f, .34f), Ivory);
            g.Box(new Vector3(0, 2.41f, -d * .5f - .36f), new Vector3(w - .45f, .16f, .7f), Teal);
            for (int x = 0; x < 4; x++)
            {
                float cx = -w * .5f + 1.25f + x * (w - 2.5f) / 3;
                Window(g, cx, 1.32f, -d * .5f, 1.38f, 1.85f, x == 2 ? WarmWindow : GlassDark, detailed, false);
            }
            if (detailed)
            {
                g.Box(new Vector3(.79f, 1.18f, -d * .5f - .18f), new Vector3(.05f, .35f, .05f), Ivory);
                g.Box(new Vector3(2.18f, 1.18f, -d * .5f - .18f), new Vector3(.05f, .35f, .05f), Ivory);
                g.Box(new Vector3(0, 2.8f, -d * .5f - .2f), new Vector3(3.6f, .43f, .035f), Ivory);
                SignStripes(g, new Vector3(0, 2.8f, -d * .5f - .23f), 2.8f, .22f, TealDark);
                g.Box(new Vector3(-w * .5f + .55f, 1.05f, -d * .5f - .14f), new Vector3(.3f, .68f, .14f), Red);
            }
        }
        else
        {
            int floors = type == 1 ? 3 : type == 2 ? 5 : 1;
            int columns = type == 3 ? 8 : type == 2 ? 5 : 4;
            float minY = type == 3 ? 1.7f : 1.32f;
            float maxY = h - 1.3f;
            for (int row = 0; row < floors; row++)
            {
                float y = floors == 1 ? minY : Mathf.Lerp(minY, maxY, row / (float)(floors - 1));
                for (int col = 0; col < columns; col++)
                {
                    float x = Mathf.Lerp(-w * .5f + 1.1f, w * .5f - 1.1f, col / (float)(columns - 1));
                    var c = ((col + row * 3) % 11 == 1) ? WarmWindow : ((col + row) % 2 == 0 ? Glass : GlassDark);
                    Window(g, x, y, -d * .5f, type == 2 ? 1.17f : 1.15f, type == 3 ? 1.35f : 1.42f, c, detailed, false);
                    if (type != 3) Window(g, x, y, d * .5f, 1.15f, 1.4f, GlassDark, detailed, true);
                }
                if (detailed && type == 1)
                    g.Box(new Vector3(0, y - .85f, -d * .5f - .055f), new Vector3(w - .4f, .12f, .11f), ConcreteLight);
                if (detailed && type == 2)
                    for (int side = -1; side <= 1; side += 2)
                        SideWindow(g, side * (w * .5f), y, 0, .95f, 1.35f, side, row % 2 == 0 ? GlassDark : Glass);
            }
            if (detailed && type == 2)
            {
                // Two balcony stacks break up the otherwise large apartment mass.
                for (int row = 1; row < floors; row++)
                {
                    float y = Mathf.Lerp(minY, maxY, row / (float)(floors - 1)) - .8f;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float x = side * w * .29f;
                        g.Box(new Vector3(x, y, -d * .5f - .32f), new Vector3(1.5f, .12f, .64f), ConcreteLight);
                        g.Box(new Vector3(x, y + .35f, -d * .5f - .62f), new Vector3(1.5f, .56f, .07f), Metal);
                    }
                }
            }
            if (detailed && type == 1)
            {
                // Vertical color panel doubles as a useful landmark from upper routes.
                g.Box(new Vector3(-w * .5f + .65f, h * .55f, -d * .5f - .12f), new Vector3(.24f, h * .66f, .16f), Teal);
                g.Box(new Vector3(w * .5f - .65f, h * .55f, -d * .5f - .12f), new Vector3(.24f, h * .66f, .16f), Teal);
            }
        }
        if (detailed)
        {
            // Broad rear utilities avoid monotonous back views without expensive texture sets.
            g.Box(new Vector3(-w * .34f, h * .49f, d * .5f + .06f), new Vector3(.11f, h * .96f, .12f), Metal);
            g.Box(new Vector3(w * .34f, h * .49f, d * .5f + .06f), new Vector3(.11f, h * .96f, .12f), Metal);
            for (int i = 0; i < 3; i++)
                g.Box(new Vector3(w * .5f + .04f, .7f + i * .16f, d * .15f), new Vector3(.08f, .06f, .65f), Metal);
        }
        return g;
    }

    static void Window(Geo g, float x, float y, float z, float w, float h, Color c, bool detailed, bool back)
    {
        float sign = back ? 1 : -1;
        g.Box(new Vector3(x, y, z + sign * .035f), new Vector3(w + .16f, h + .16f, .07f), Navy);
        g.Box(new Vector3(x, y, z + sign * .078f), new Vector3(w, h, .018f), c);
        if (!detailed) return;
        g.Box(new Vector3(x, y, z + sign * .1f), new Vector3(.055f, h, .04f), ConcreteLight);
        g.Box(new Vector3(x, y - h * .5f - .065f, z + sign * .12f), new Vector3(w + .25f, .13f, .26f), ConcreteLight);
        g.Box(new Vector3(x - w * .32f, y + h * .32f, z + sign * .093f), new Vector3(w * .14f, h * .16f, .008f), Lighter(c, .10f));
    }

    static void SideWindow(Geo g, float x, float y, float z, float w, float h, int side, Color c)
    {
        g.Box(new Vector3(x + side * .035f, y, z), new Vector3(.07f, h + .16f, w + .16f), Navy);
        g.Box(new Vector3(x + side * .078f, y, z), new Vector3(.018f, h, w), c);
        g.Box(new Vector3(x + side * .1f, y - h * .5f - .055f, z), new Vector3(.2f, .11f, w + .24f), ConcreteLight);
    }

    static void RooftopHut()
    {
        var g = new Geo();
        g.Box(new Vector3(0, 1.1f, 0), new Vector3(2.5f, 2.2f, 2), ConcreteLight);
        g.Box(new Vector3(0, 2.23f, 0), new Vector3(2.7f, .14f, 2.2f), Metal);
        g.Box(new Vector3(0, .07f, 0), new Vector3(2.64f, .14f, 2.12f), ConcreteDark);
        g.Box(new Vector3(-.35f, .95f, -1.035f), new Vector3(.92f, 1.9f, .07f), Navy);
        g.Box(new Vector3(-.35f, .94f, -1.08f), new Vector3(.78f, 1.78f, .025f), TealDark);
        g.Box(new Vector3(-.07f, .96f, -1.12f), new Vector3(.045f, .21f, .06f), Ivory);
        for (int i = 0; i < 5; i++) g.Box(new Vector3(.78f, 1.35f + i * .12f, -1.055f), new Vector3(.45f, .055f, .08f), Metal);
        g.Box(new Vector3(.9f, 2.43f, .55f), new Vector3(.32f, .32f, .32f), Metal);
        g.Box(new Vector3(.9f, 2.6f, .55f), new Vector3(.45f, .08f, .45f), Navy);
        var root = Single("Rooftop_Hut", g);
        BoxCollision(root, "HutBody", new Vector3(0, 1.15f, 0), new Vector3(2.5f, 2.3f, 2));
        Anchor(root, "RoofSurface", new Vector3(0, 2.3f, 0));
        Save(root);
    }

    static void Road()
    {
        var g = new Geo();
        g.Box(new Vector3(0, -.11f, 0), new Vector3(12, .22f, 6), Asphalt);
        for (int i = 0; i < 4; i++)
        {
            float x = -4.5f + i * 3;
            g.Box(new Vector3(x, .006f, 0), new Vector3(1.65f, .012f, .1f), Ochre);
        }
        for (int side = -1; side <= 1; side += 2)
        {
            g.Box(new Vector3(0, .006f, side * 2.6f), new Vector3(12, .012f, .07f), Ivory);
            for (int i = 0; i < 3; i++)
                g.Box(new Vector3(-4 + i * 4, .008f, side * 2.85f), new Vector3(.56f, .016f, .23f), Navy);
        }
        // A deliberately faceted drain cover, no displacement or unique texture.
        g.Cylinder(new Vector3(3.2f, .008f, 1.35f), .38f, .016f, 8, Metal);
        for (int i = -2; i <= 2; i++) g.Box(new Vector3(3.2f + i * .1f, .019f, 1.35f), new Vector3(.035f, .008f, .43f), Navy);
        var root = Single("Road_Straight_12m", g);
        BoxCollision(root, "RoadSurface", new Vector3(0, -.11f, 0), new Vector3(12, .22f, 6));
        EndAnchors(root, 12, 0);
        Save(root);
    }

    static void Sidewalk()
    {
        var g = new Geo();
        g.Box(new Vector3(0, -.14f, 0), new Vector3(8, .28f, 2.4f), Concrete);
        for (int i = 0; i < 8; i++)
        {
            float x = -3.5f + i;
            g.Box(new Vector3(x, .008f, -1.08f), new Vector3(.965f, .016f, .24f), i % 2 == 0 ? ConcreteLight : ConcreteDark);
            for (int row = 0; row < 2; row++)
                g.Box(new Vector3(x, .005f, -.52f + row * .92f), new Vector3(.975f, .01f, .89f), (i + row) % 3 == 0 ? ConcreteLight : Concrete);
            g.Box(new Vector3(x, .016f, .88f), new Vector3(.98f, .024f, .18f), Ochre);
            for (int dot = 0; dot < 4; dot++)
                g.Box(new Vector3(x - .3f + dot * .2f, .032f, .88f), new Vector3(.09f, .008f, .12f), Ochre);
        }
        var root = Single("Sidewalk_Straight_8m", g);
        BoxCollision(root, "SidewalkSurface", new Vector3(0, -.14f, 0), new Vector3(8, .28f, 2.4f));
        EndAnchors(root, 8, 0);
        Save(root);
    }

    static void RoofBridge()
    {
        var g = new Geo();
        g.Box(new Vector3(0, -.14f, 0), new Vector3(6, .28f, 1.8f), Metal);
        g.Box(new Vector3(0, -.13f, -.82f), new Vector3(6, .4f, .16f), Navy);
        g.Box(new Vector3(0, -.13f, .82f), new Vector3(6, .4f, .16f), Navy);
        for (int i = 0; i < 15; i++)
            g.Box(new Vector3(-2.8f + i * .4f, .009f, 0), new Vector3(.34f, .018f, 1.48f), i % 2 == 0 ? ConcreteDark : Metal);
        RailMesh(g, 6, 1.1f, .8f, true);
        var root = Single("Rooftop_Bridge_6m", g);
        BoxCollision(root, "WalkableDeck", new Vector3(0, -.14f, 0), new Vector3(6, .28f, 1.8f));
        EndAnchors(root, 6, 0);
        Save(root);
    }

    static void ExteriorStairs()
    {
        var g = new Geo();
        const int count = 12;
        for (int i = 0; i < count; i++)
        {
            float step = (i + 1) * .25f;
            float x = -2 + (i + .5f) / 3;
            g.Box(new Vector3(x, step - .09f, 0), new Vector3(1f / 3, .18f, 1.8f), i % 2 == 0 ? ConcreteLight : Concrete);
            g.Box(new Vector3(x - .145f, step - .02f, 0), new Vector3(.035f, .04f, 1.8f), Ochre);
        }
        for (int side = -1; side <= 1; side += 2)
        {
            g.Beam(new Vector3(-2.08f, -.1f, side * .83f), new Vector3(2.07f, 2.99f, side * .83f), .15f, .17f, Metal);
            g.Beam(new Vector3(-2, 1.05f, side * .83f), new Vector3(2, 4.05f, side * .83f), .09f, .09f, Metal);
            for (int i = 0; i < 5; i++)
            {
                float x = -1.85f + i * .93f;
                float y = (x + 2) * .75f;
                g.Box(new Vector3(x, y + .5f, side * .83f), new Vector3(.07f, 1.05f, .07f), Metal);
            }
        }
        var root = Single("Exterior_Stairs_Rise3m", g);
        for (int i = 0; i < count; i++)
        {
            float top = (i + 1) * .25f;
            BoxCollision(root, "Step_" + (i + 1).ToString("00"), new Vector3(-2 + (i + .5f) / 3, top - .09f, 0), new Vector3(1f / 3, .18f, 1.8f));
        }
        Anchor(root, "Entry", new Vector3(-2, 0, 0));
        Anchor(root, "Exit", new Vector3(2, 3, 0));
        Save(root);
    }

    static void Ladder()
    {
        var g = new Geo();
        for (int side = -1; side <= 1; side += 2)
        {
            g.Box(new Vector3(side * .35f, 2, 0), new Vector3(.085f, 4, .085f), TealDark);
            g.Box(new Vector3(side * .35f, 4.14f, .12f), new Vector3(.085f, .28f, .085f), TealDark);
            for (int i = 0; i < 3; i++)
                g.Box(new Vector3(side * .35f, .6f + i * 1.4f, .13f), new Vector3(.12f, .11f, .3f), Metal);
        }
        for (int i = 0; i < 13; i++)
            g.Box(new Vector3(0, .22f + i * .3f, -.02f), new Vector3(.67f, .06f, .12f), i == 0 || i == 12 ? Ochre : Metal);
        var root = Single("Ladder_4m", g);
        // These anchors describe the path only. A ladder controller/trigger belongs to gameplay.
        Anchor(root, "ClimbBottom", new Vector3(0, 0, -.3f));
        Anchor(root, "ClimbTop", new Vector3(0, 4, -.3f));
        Save(root);
    }

    static void Guardrail()
    {
        var g = new Geo();
        RailMesh(g, 4, 1.1f, 0, false);
        var root = Single("Guardrail_4m", g);
        EndAnchors(root, 4, 0);
        Save(root);
    }

    static void RailMesh(Geo g, float length, float height, float z, bool accent)
    {
        g.Box(new Vector3(0, height, z), new Vector3(length, .09f, .09f), Metal);
        g.Box(new Vector3(0, height * .5f, z), new Vector3(length, .055f, .055f), Metal);
        int posts = Mathf.RoundToInt(length) + 1;
        for (int i = 0; i < posts; i++)
        {
            float x = Mathf.Lerp(-length * .5f + .06f, length * .5f - .06f, i / (float)(posts - 1));
            g.Box(new Vector3(x, height * .5f, z), new Vector3(.085f, height, .085f), Metal);
            g.Box(new Vector3(x, .035f, z), new Vector3(.22f, .07f, .23f), Navy);
            if (accent && i % 2 == 0) g.Box(new Vector3(x, height + .002f, z), new Vector3(.16f, .105f, .105f), Ochre);
        }
    }

    static void UtilityBox()
    {
        var g = new Geo();
        g.Box(new Vector3(0, .08f, 0), new Vector3(1.04f, .16f, .6f), ConcreteDark);
        g.Box(new Vector3(0, .66f, 0), new Vector3(.9f, 1.12f, .45f), TealDark);
        g.Box(new Vector3(0, 1.24f, 0), new Vector3(1.02f, .12f, .55f), Metal);
        g.Box(new Vector3(0, .68f, -.238f), new Vector3(.76f, .91f, .025f), Teal);
        g.Box(new Vector3(.27f, .7f, -.27f), new Vector3(.04f, .14f, .04f), Ivory);
        for (int i = 0; i < 5; i++) g.Box(new Vector3(-.04f, .35f + i * .085f, -.26f), new Vector3(.45f, .024f, .025f), Navy);
        g.Box(new Vector3(-.14f, .98f, -.268f), new Vector3(.18f, .14f, .025f), Ochre);
        var root = Single("Utility_Box", g);
        // Decorative beside the player lane; optional collider can be added by the level designer.
        Save(root);
    }

    static void Planter()
    {
        var g = new Geo();
        g.TaperedBox(new Vector3(0, .31f, 0), new Vector3(1.55f, .62f, .62f), new Vector2(1.8f, .75f), ConcreteLight);
        g.Box(new Vector3(0, .62f, 0), new Vector3(1.88f, .12f, .82f), ConcreteDark);
        g.Box(new Vector3(0, .69f, 0), new Vector3(1.64f, .025f, .59f), Dirt);
        for (int i = 0; i < 3; i++)
        {
            var center = new Vector3(-.55f + i * .55f, .88f, 0);
            g.FacetedBush(center, new Vector3(.65f, .52f, .6f), i % 2 == 0 ? Green : LeafLight);
        }
        var root = Single("Planter_Shrubs", g);
        Save(root);
    }

    static void RoadBarrier()
    {
        var g = new Geo();
        g.TaperedBox(new Vector3(0, .4f, 0), new Vector3(1.8f, .8f, .52f), new Vector2(1.52f, .24f), ConcreteLight);
        for (int side = -1; side <= 1; side += 2)
        {
            g.Box(new Vector3(side * .52f, .035f, 0), new Vector3(.32f, .07f, .62f), ConcreteDark);
            g.Box(new Vector3(side * .52f, .86f, 0), new Vector3(.1f, .12f, .1f), Metal);
        }
        g.Box(new Vector3(0, .51f, -.22f), new Vector3(1.43f, .25f, .025f), Ochre);
        for (int i = 0; i < 4; i++)
            g.BoxRotated(new Vector3(-.52f + i * .35f, .51f, -.238f), new Vector3(.11f, .25f, .012f), Quaternion.Euler(0, 0, -18), Navy);
        var root = Single("Road_Barrier", g);
        Save(root);
    }

    static void WaterTank()
    {
        var g = new Geo();
        for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
                g.Box(new Vector3(x * .54f, .28f, z * .54f), new Vector3(.14f, .56f, .14f), Navy);
        g.Box(new Vector3(0, .51f, 0), new Vector3(1.6f, .16f, 1.6f), Metal);
        g.Cylinder(new Vector3(0, 1.35f, 0), .81f, 1.55f, 10, Glass);
        g.Cylinder(new Vector3(0, .64f, 0), .86f, .12f, 10, TealDark);
        g.Cylinder(new Vector3(0, 1.98f, 0), .86f, .14f, 10, TealDark);
        g.Cone(new Vector3(0, 2.17f, 0), .84f, .19f, .28f, 10, Metal);
        g.Cylinder(new Vector3(0, 2.39f, 0), .2f, .15f, 8, Navy);
        g.Box(new Vector3(.95f, .75f, .18f), new Vector3(.12f, 1.4f, .12f), TealDark);
        g.Box(new Vector3(.82f, 1.4f, .18f), new Vector3(.32f, .12f, .12f), TealDark);
        var root = Single("Rooftop_WaterTank", g);
        Save(root);
    }

    static void Billboard()
    {
        var g = new Geo();
        for (int side = -1; side <= 1; side += 2)
        {
            g.Box(new Vector3(side * 1.08f, .5f, 0), new Vector3(.16f, 1, .16f), Metal);
            g.Box(new Vector3(side * 1.08f, .07f, 0), new Vector3(.5f, .14f, .7f), Navy);
            g.Beam(new Vector3(side * 1.08f, .08f, .55f), new Vector3(side * 1.08f, 1.6f, .08f), .07f, .07f, Metal);
        }
        g.Box(new Vector3(0, 1.9f, 0), new Vector3(3.4f, 1.8f, .18f), Navy);
        g.Box(new Vector3(0, 1.9f, -.102f), new Vector3(3.17f, 1.57f, .025f), TealDark);
        g.Box(new Vector3(-.9f, 1.96f, -.128f), new Vector3(.8f, .8f, .025f), Ochre);
        g.BoxRotated(new Vector3(-.9f, 1.96f, -.145f), new Vector3(.4f, .4f, .025f), Quaternion.Euler(0, 0, 45), Ivory);
        SignStripes(g, new Vector3(.55f, 2.14f, -.128f), 1.5f, .18f, Ivory);
        g.Box(new Vector3(.55f, 1.73f, -.13f), new Vector3(1.45f, .1f, .025f), Glass);
        g.Box(new Vector3(.4f, 1.51f, -.13f), new Vector3(1.15f, .1f, .025f), Glass);
        var root = Single("Rooftop_Billboard", g);
        Save(root);
    }

    static void SignStripes(Geo g, Vector3 center, float width, float height, Color color)
    {
        // Deliberate graphic bars rather than machine-generated lettering.
        for (int i = 0; i < 5; i++)
        {
            float w = width / 6;
            g.Box(center + new Vector3(-width * .5f + width * (i + .5f) / 5, 0, 0), new Vector3(w, height, .018f), color);
            if (i % 2 == 0) g.Box(center + new Vector3(-width * .5f + width * (i + .5f) / 5, height * .25f, -.004f), new Vector3(w * .35f, height * .5f, .028f), TealDark);
        }
    }

    static GameObject NewRoot(string name)
    {
        var root = new GameObject(name);
        GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);
        return root;
    }

    static GameObject Single(string name, Geo g)
    {
        var root = NewRoot(name);
        AttachMesh(root, "Geometry", g, name);
        return root;
    }

    static MeshRenderer AttachMesh(GameObject root, string childName, Geo g, string meshName)
    {
        var obj = new GameObject(childName);
        obj.transform.SetParent(root.transform, false);
        GameObjectUtility.SetStaticEditorFlags(obj, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);
        var mesh = g.ToMesh(meshName);
        string path = MeshFolder + "/" + meshName + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing)
        {
            EditorUtility.CopySerialized(mesh, existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            mesh = existing;
            EditorUtility.SetDirty(mesh);
        }
        else AssetDatabase.CreateAsset(mesh, path);
        obj.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = obj.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
        return renderer;
    }

    static void BoxCollision(GameObject root, string name, Vector3 center, Vector3 size)
    {
        var obj = new GameObject("Collision_" + name);
        obj.transform.SetParent(root.transform, false);
        obj.layer = LayerMask.NameToLayer("Ground");
        var collider = obj.AddComponent<BoxCollider>();
        collider.center = center;
        collider.size = size;
        GameObjectUtility.SetStaticEditorFlags(obj, StaticEditorFlags.OccludeeStatic);
    }

    static void Anchor(GameObject root, string name, Vector3 position)
    {
        var anchor = new GameObject(name);
        anchor.transform.SetParent(root.transform, false);
        anchor.transform.localPosition = position;
    }

    static void EndAnchors(GameObject root, float length, float y)
    {
        Anchor(root, "SnapLeft", new Vector3(-length * .5f, y, 0));
        Anchor(root, "SnapRight", new Vector3(length * .5f, y, 0));
        Anchor(root, "WalkableSurface", new Vector3(0, y, 0));
    }

    static void Save(GameObject root)
    {
        PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/" + root.name + ".prefab");
        UnityEngine.Object.DestroyImmediate(root);
    }

    static Color Hex(string value)
    {
        ColorUtility.TryParseHtmlString("#" + value, out var color);
        return color;
    }

    static Color Lighter(Color c, float amount) => Color.Lerp(c, Ivory, amount);

    sealed class Geo
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> triangles = new List<int>();

        public void Box(Vector3 center, Vector3 size, Color c) => BoxRotated(center, size, Quaternion.identity, c);

        public void BoxRotated(Vector3 center, Vector3 size, Quaternion rotation, Color c)
        {
            Vector3 h = size * .5f;
            var p = new[]
            {
                new Vector3(-h.x,-h.y,-h.z), new Vector3(h.x,-h.y,-h.z),
                new Vector3(h.x,h.y,-h.z), new Vector3(-h.x,h.y,-h.z),
                new Vector3(-h.x,-h.y,h.z), new Vector3(h.x,-h.y,h.z),
                new Vector3(h.x,h.y,h.z), new Vector3(-h.x,h.y,h.z)
            };
            for (int i = 0; i < p.Length; i++) p[i] = center + rotation * p[i];
            Quad(p[0], p[3], p[2], p[1], c);
            Quad(p[4], p[5], p[6], p[7], c);
            Quad(p[0], p[4], p[7], p[3], c);
            Quad(p[1], p[2], p[6], p[5], c);
            Quad(p[3], p[7], p[6], p[2], c);
            Quad(p[0], p[1], p[5], p[4], c);
        }

        public void TaperedBox(Vector3 center, Vector3 bottomSize, Vector2 topSize, Color c)
        {
            float y0 = center.y - bottomSize.y * .5f, y1 = center.y + bottomSize.y * .5f;
            var a = new Vector3(center.x - bottomSize.x * .5f, y0, center.z - bottomSize.z * .5f);
            var b = new Vector3(center.x + bottomSize.x * .5f, y0, center.z - bottomSize.z * .5f);
            var d = new Vector3(center.x - bottomSize.x * .5f, y0, center.z + bottomSize.z * .5f);
            var e = new Vector3(center.x + bottomSize.x * .5f, y0, center.z + bottomSize.z * .5f);
            var aa = new Vector3(center.x - topSize.x * .5f, y1, center.z - topSize.y * .5f);
            var bb = new Vector3(center.x + topSize.x * .5f, y1, center.z - topSize.y * .5f);
            var dd = new Vector3(center.x - topSize.x * .5f, y1, center.z + topSize.y * .5f);
            var ee = new Vector3(center.x + topSize.x * .5f, y1, center.z + topSize.y * .5f);
            Quad(a, aa, bb, b, c); Quad(d, e, ee, dd, c);
            Quad(a, d, dd, aa, c); Quad(b, bb, ee, e, c);
            Quad(aa, dd, ee, bb, c); Quad(a, b, e, d, c);
        }

        public void Beam(Vector3 from, Vector3 to, float width, float depth, Color c)
        {
            Vector3 direction = to - from;
            BoxRotated((from + to) * .5f, new Vector3(width, direction.magnitude, depth), Quaternion.FromToRotation(Vector3.up, direction.normalized), c);
        }

        public void Cylinder(Vector3 center, float radius, float height, int sides, Color c) => Cone(center, radius, radius, height, sides, c);

        public void Cone(Vector3 center, float radiusBottom, float radiusTop, float height, int sides, Color c)
        {
            var bottom = center + Vector3.down * height * .5f;
            var top = center + Vector3.up * height * .5f;
            for (int i = 0; i < sides; i++)
            {
                float a = Mathf.PI * 2 * i / sides, b = Mathf.PI * 2 * (i + 1) / sides;
                var da = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var db = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b));
                Quad(bottom + da * radiusBottom, top + da * radiusTop, top + db * radiusTop, bottom + db * radiusBottom, c);
                Tri(top, top + db * radiusTop, top + da * radiusTop, c);
                Tri(bottom, bottom + da * radiusBottom, bottom + db * radiusBottom, c);
            }
        }

        public void FacetedBush(Vector3 center, Vector3 size, Color c)
        {
            var top = center + Vector3.up * size.y * .5f;
            var bottom = center - Vector3.up * size.y * .5f;
            for (int i = 0; i < 6; i++)
            {
                float a = Mathf.PI * 2 * i / 6, b = Mathf.PI * 2 * (i + 1) / 6;
                Vector3 p = center + new Vector3(Mathf.Cos(a) * size.x * .5f, size.y * .02f, Mathf.Sin(a) * size.z * .5f);
                Vector3 q = center + new Vector3(Mathf.Cos(b) * size.x * .5f, size.y * .02f, Mathf.Sin(b) * size.z * .5f);
                Tri(top, q, p, i % 2 == 0 ? c : Color.Lerp(c, LeafLight, .25f));
                Tri(bottom, p, q, c);
            }
        }

        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            int index = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            for (int i = 0; i < 4; i++) colors.Add(color);
            triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
            triangles.Add(index); triangles.Add(index + 2); triangles.Add(index + 3);
        }

        void Tri(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            int index = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            colors.Add(color); colors.Add(color); colors.Add(color);
            triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            // Keep vertices per face for crisp polygonal normals; avoid texture UVs by design.
            mesh.UploadMeshData(false);
            return mesh;
        }
    }

    const string ShaderSource = @"Shader ""ProjectM/Urban Vertex Color""
{
    Properties { _BaseColor (""Tint"", Color) = (1,1,1,1) _BaseMap (""Base"", 2D) = ""white"" {} _Cutoff (""Cutoff"", Float) = 0.5 }
    SubShader
    {
        Tags { ""RenderType""=""Opaque"" ""RenderPipeline""=""UniversalPipeline"" ""Queue""=""Geometry"" }
        HLSLINCLUDE
        #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""
        #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl""
        #include ""Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl""
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float4 _BaseMap_ST;
            half _Cutoff;
        CBUFFER_END
        ENDHLSL
        Pass
        {
            Name ""ForwardLit""
            Tags { ""LightMode""=""UniversalForward"" }
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; half4 color : COLOR; half fog : TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color * _BaseColor;
                output.fog = ComputeFogFactor(pos.positionCS.z);
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 normalWS = normalize(input.normalWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half ndl = saturate(dot(normalWS, light.direction));
                // Scene ambient/probes and the sun own exposure; no baked bright ambient floor.
                half3 ambient = max(SampleSH(normalWS), half3(0, 0, 0));
                half3 direct = light.color * ndl * light.distanceAttenuation * light.shadowAttenuation;
                half3 emission = input.color.rgb * (1.0h - saturate(input.color.a)) * 0.60h;
                half3 color = input.color.rgb * (ambient + direct * 0.80) + emission;
                return half4(MixFog(color, input.fog), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name ""ShadowCaster""
            Tags { ""LightMode""=""ShadowCaster"" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection;
            float3 _LightPosition;
            struct ShadowAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct ShadowVaryings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            ShadowVaryings shadowVert(ShadowAttributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 direction = normalize(_LightPosition - posWS);
                #else
                    float3 direction = _LightDirection;
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normWS, direction));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                return output;
            }
            half4 shadowFrag(ShadowVaryings input) : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name ""DepthOnly""
            Tags { ""LightMode""=""DepthOnly"" }
            ZWrite On ColorMask R Cull Back
            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            #pragma multi_compile_instancing
            struct DepthAttributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DepthVaryings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            DepthVaryings depthVert(DepthAttributes input)
            {
                DepthVaryings output = (DepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            half4 depthFrag(DepthVaryings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }
        Pass
        {
            Name ""DepthNormals""
            Tags { ""LightMode""=""DepthNormals"" }
            ZWrite On Cull Back
            HLSLPROGRAM
            #pragma vertex normalVert
            #pragma fragment normalFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            struct NormalAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct NormalVaryings { float4 positionCS : SV_POSITION; half3 normalWS : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            NormalVaryings normalVert(NormalAttributes input)
            {
                NormalVaryings output = (NormalVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 normalFrag(NormalVaryings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(normal);
                    half3 packed = PackFloat2To888(saturate(oct * 0.5 + 0.5));
                    return half4(packed, 0);
                #else
                    return half4(normal, 0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack ""Universal Render Pipeline/Lit""
}";
}
#endif
