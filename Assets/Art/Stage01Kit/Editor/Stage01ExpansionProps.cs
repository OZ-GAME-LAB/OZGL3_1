// Original Stage 1 street/rooftop dressing. Generated geometry is authored in meters.
#if UNITY_EDITOR
using UnityEngine;

public static class Stage01ExpansionProps
{
    static readonly Color Concrete = Hex("5B626B"), Light = Hex("747D84"), Dark = Hex("434E5A");
    static readonly Color Navy = Hex("22313E"), Metal = Hex("465563"), Teal = Hex("486F73");
    static readonly Color DeepTeal = Hex("334F58"), Glass = Hex("485F69"), DarkGlass = Hex("2E424F");
    static readonly Color Ivory = Hex("A5AEAE"), Ochre = Hex("9B895F"), Red = Hex("815D55");
    static readonly Color Green = Hex("42564D"), Wood = Hex("766350"), Bag = Hex("303C43");

    public static void Build()
    {
        Stairs(); Landing(); HVAC(); Duct(); BusShelter(); TrafficSignal();
        UtilityPole(); Bollard(); CargoPallet(); WasteBags(); Barricade(); Rubble(); CrackedRoad();
    }

    static void Stairs()
    {
        var g = new S1Geo();
        for (int i = 0; i < 16; i++)
        {
            float x = -4 + (i + .5f) * .5f, top = (i + 1) * .25f;
            g.Box(new Vector3(x, top - .075f, 0), new Vector3(.5f, .15f, 2), i % 2 == 0 ? Light : Concrete);
            g.Box(new Vector3(x - .22f, top - .012f, 0), new Vector3(.045f, .024f, 2), Ochre);
            // Folded steel lip under each exposed riser.
            g.Box(new Vector3(x - .22f, top - .10f, 0), new Vector3(.05f, .2f, 1.96f), Metal);
        }
        for (int side = -1; side <= 1; side += 2)
            g.Beam(new Vector3(-4, -.12f, side * .88f), new Vector3(4, 3.88f, side * .88f), .17f, .16f, DeepTeal);
        // Rear rail leaves the player-facing silhouette and Z=0 lane unobstructed.
        g.Beam(new Vector3(-4, .98f, .91f), new Vector3(4, 4.98f, .91f), .095f, .095f, Metal);
        g.Beam(new Vector3(-4, .53f, .91f), new Vector3(4, 4.53f, .91f), .055f, .055f, Metal);
        for (int i = 0; i <= 8; i++)
        {
            float x = -4 + i;
            g.Box(new Vector3(x, (x + 4) * .5f + .48f, .91f), new Vector3(.075f, 1.02f, .075f), Metal);
        }
        var root = S1Assets.Save("Exterior_Stairs_Rise4m", g);
        for (int i = 0; i < 16; i++)
            S1Assets.Collider(root, "Step_" + (i + 1).ToString("00"), new Vector3(-4 + (i + .5f) * .5f, (i + 1) * .25f - .075f, 0), new Vector3(.5f, .15f, 2));
        S1Assets.Anchor(root, "Entry", new Vector3(-4, 0, 0));
        S1Assets.Anchor(root, "Exit", new Vector3(4, 4, 0));
        S1Assets.Finish(root);
    }

    static void Landing()
    {
        var g = new S1Geo();
        g.Box(new Vector3(0, -.12f, 0), new Vector3(4, .24f, 2.4f), Concrete);
        g.Box(new Vector3(0, -.035f, -1.15f), new Vector3(4, .07f, .1f), Ivory);
        for (int side = -1; side <= 1; side += 2)
        {
            g.Box(new Vector3(0, -.21f, side * 1.05f), new Vector3(4, .18f, .14f), DeepTeal);
            g.Beam(new Vector3(side * 1.8f, -.27f, .95f), new Vector3(side * .8f, -.9f, .95f), .1f, .12f, Metal);
        }
        for (int i = 0; i < 5; i++)
        {
            float x = -1.94f + i * .97f;
            g.Box(new Vector3(x, .52f, 1.1f), new Vector3(.07f, 1.04f, .07f), Metal);
            g.Box(new Vector3(x, .025f, 1.1f), new Vector3(.18f, .05f, .2f), Navy);
        }
        g.Box(new Vector3(0, 1.06f, 1.1f), new Vector3(4, .08f, .08f), Metal);
        g.Box(new Vector3(0, .55f, 1.1f), new Vector3(4, .045f, .045f), Metal);
        var root = S1Assets.Save("Rooftop_Landing_4m", g);
        S1Assets.Collider(root, "Deck", new Vector3(0, -.12f, 0), new Vector3(4, .24f, 2.4f));
        S1Assets.Anchor(root, "SnapLeft", new Vector3(-2, 0, 0));
        S1Assets.Anchor(root, "SnapRight", new Vector3(2, 0, 0));
        S1Assets.Anchor(root, "WalkableSurface", Vector3.zero);
        S1Assets.Finish(root);
    }

    static void HVAC()
    {
        var g = new S1Geo();
        for (int side = -1; side <= 1; side += 2)
            g.Box(new Vector3(0, .1f, side * .72f), new Vector3(3.05f, .2f, .2f), Navy);
        g.Box(new Vector3(0, .76f, 0), new Vector3(2.9f, 1.22f, 1.8f), Light);
        g.Box(new Vector3(0, 1.41f, 0), new Vector3(3.03f, .12f, 1.93f), Metal);
        for (int i = 0; i < 2; i++)
        {
            float x = -.73f + i * 1.46f;
            g.Cylinder(new Vector3(x, 1.49f, 0), .62f, .1f, 12, Navy);
            g.Cylinder(new Vector3(x, 1.55f, 0), .52f, .035f, 12, DarkGlass);
            for (int blade = 0; blade < 5; blade++)
            {
                float a = blade * 72;
                Vector3 c = new Vector3(x, 1.58f, 0) + Quaternion.Euler(0, a, 0) * new Vector3(.23f, 0, 0);
                g.BoxRotated(c, new Vector3(.42f, .025f, .14f), Quaternion.Euler(0, a + 24, 0), Metal);
            }
            g.Cylinder(new Vector3(x, 1.61f, 0), .10f, .065f, 8, Ivory);
            // Two perpendicular raised guards remain readable from an elevated game camera.
            g.Box(new Vector3(x, 1.64f, 0), new Vector3(1.13f, .025f, .025f), Light);
            g.Box(new Vector3(x, 1.64f, 0), new Vector3(.025f, .025f, 1.13f), Light);
        }
        for (int i = 0; i < 7; i++)
            g.Box(new Vector3(-.5f, .4f + i * .11f, -.916f), new Vector3(1.5f, .045f, .04f), Navy);
        g.Box(new Vector3(1.02f, .8f, -.925f), new Vector3(.52f, .85f, .055f), Concrete);
        g.Box(new Vector3(1.15f, .8f, -.966f), new Vector3(.045f, .18f, .04f), Ivory);
        g.Box(new Vector3(.94f, 1.03f, -.963f), new Vector3(.25f, .11f, .025f), Ochre);
        SaveDressing("Rooftop_HVAC", g);
    }

    static void Duct()
    {
        var g = new S1Geo();
        // One closed eight-point extrusion gives the elbow a quiet, coherent silhouette.
        // No intersecting body boxes, repeated ribs, grille slots or overlapping bend pieces.
        var profile = new[] {
            new Vector2(-1.1f,.08f), new Vector2(1.2f,.08f), new Vector2(1.2f,.95f),
            new Vector2(-.12f,.95f), new Vector2(-.3f,1.13f), new Vector2(-.3f,1.75f),
            new Vector2(-1.2f,1.75f), new Vector2(-1.2f,.26f)
        };
        var front = new Vector3[profile.Length];
        var back = new Vector3[profile.Length];
        for (int i = 0; i < profile.Length; i++)
        {
            front[i] = new Vector3(profile[i].x, profile[i].y, -.54f);
            back[i] = new Vector3(profile[i].x, profile[i].y, .54f);
        }
        int[] faces = { 0,1,2, 0,2,3, 0,3,4, 0,4,7, 4,5,6, 4,6,7 };
        for (int i = 0; i < faces.Length; i += 3)
        {
            int a = faces[i], b = faces[i + 1], c = faces[i + 2];
            g.Tri(front[a], front[c], front[b], Light);
            g.Tri(back[a], back[b], back[c], Light);
        }
        for (int i = 0; i < profile.Length; i++)
        {
            int next = (i + 1) % profile.Length;
            g.Quad(front[i], front[next], back[next], back[i], Light);
        }
        g.Box(new Vector3(0, .04f, 0), new Vector3(2.55f, .08f, 1.22f), Dark);
        // Exactly two broad connector seams, comparable in detail density to Utility_Box.
        g.Box(new Vector3(1.08f, .97f, 0), new Vector3(.12f, .04f, 1.16f), Metal);
        for (int side = -1; side <= 1; side += 2)
        {
            g.Box(new Vector3(1.08f, .515f, side * .56f), new Vector3(.12f, .87f, .04f), Metal);
            g.Box(new Vector3(-.75f, 1.59f, side * .56f), new Vector3(.9f, .12f, .04f), Metal);
        }
        g.Box(new Vector3(-1.22f, 1.59f, 0), new Vector3(.04f, .12f, 1.08f), Metal);
        g.Box(new Vector3(-.28f, 1.59f, 0), new Vector3(.04f, .12f, 1.08f), Metal);
        SaveDressing("Rooftop_DuctElbow", g);
    }

    static void BusShelter()
    {
        var g = new S1Geo();
        g.Box(new Vector3(0, .035f, 0), new Vector3(4, .07f, 1.6f), Dark);
        g.Box(new Vector3(0, 2.65f, 0), new Vector3(4.08f, .18f, 1.7f), DeepTeal);
        g.Box(new Vector3(0, 2.78f, .05f), new Vector3(3.9f, .08f, 1.55f), Metal);
        g.Box(new Vector3(0, 2.6f, -.86f), new Vector3(4.08f, .18f, .07f), Ivory);
        for (int i = 0; i < 4; i++)
        {
            float x = -1.86f + i * 1.24f;
            g.Box(new Vector3(x, 1.35f, .7f), new Vector3(.09f, 2.58f, .09f), Metal);
        }
        for (int i = 0; i < 3; i++)
        {
            float x = -1.24f + i * 1.24f;
            g.Box(new Vector3(x, 1.4f, .72f), new Vector3(1.14f, 1.9f, .035f), i == 2 ? DarkGlass : Glass);
            g.Box(new Vector3(x, 1.2f, .688f), new Vector3(1.14f, .055f, .025f), Ivory);
        }
        g.Box(new Vector3(-1.86f, 1.35f, -.1f), new Vector3(.09f, 2.58f, 1.5f), Metal);
        g.Box(new Vector3(-1.85f, 1.4f, -.03f), new Vector3(.035f, 1.9f, 1.2f), DarkGlass);
        // Slatted waiting bench and two open legs.
        for (int i = 0; i < 3; i++)
            g.Box(new Vector3(-.35f, .5f, .18f + i * .14f), new Vector3(2.35f, .08f, .1f), Wood);
        for (int side = -1; side <= 1; side += 2)
            g.Box(new Vector3(-.35f + side * .85f, .26f, .32f), new Vector3(.08f, .48f, .43f), Metal);
        g.Box(new Vector3(1.3f, 1.5f, .65f), new Vector3(.66f, 1.56f, .12f), DeepTeal);
        g.Box(new Vector3(1.3f, 1.6f, .572f), new Vector3(.51f, 1.22f, .025f), Ivory);
        g.Box(new Vector3(1.3f, 2.02f, .55f), new Vector3(.42f, .17f, .02f), Teal);
        for (int i = 0; i < 5; i++)
        {
            g.Box(new Vector3(1.19f, 1.21f + i * .14f, .55f), new Vector3(.24f, .035f, .02f), Navy);
            g.Box(new Vector3(1.49f, 1.21f + i * .14f, .55f), new Vector3(.06f, .035f, .02f), Red);
        }
        g.Box(new Vector3(-.35f, 2.6f, -.91f), new Vector3(1.3f, .18f, .035f), Teal);
        var root = S1Assets.Save("Street_BusShelter", g);
        S1Assets.Anchor(root, "PlacementBack", new Vector3(0, 0, .8f));
        S1Assets.Anchor(root, "WaitingArea", new Vector3(0, 0, -.6f));
        S1Assets.Finish(root);
    }

    static void TrafficSignal()
    {
        var g = new S1Geo();
        g.TaperedBox(new Vector3(-1.14f, .11f, 0), new Vector3(.44f, .22f, .44f), new Vector2(.28f, .28f), Dark);
        g.Cylinder(new Vector3(-1.14f, 2.21f, 0), .065f, 4.2f, 8, Metal);
        g.Beam(new Vector3(-1.14f, 4.28f, 0), new Vector3(-.8f, 4.46f, 0), .13f, .13f, Metal);
        g.Box(new Vector3(.5f, 4.46f, 0), new Vector3(2.65f, .13f, .13f), Metal);
        g.Box(new Vector3(1.05f, 4.2f, 0), new Vector3(.07f, .48f, .07f), Metal);
        g.Box(new Vector3(1.05f, 3.66f, -.07f), new Vector3(.43f, 1.12f, .3f), Navy);
        var colors = new[] { Red, Ochre, Green };
        for (int i = 0; i < 3; i++)
        {
            float y = 3.98f - i * .32f;
            FrontDisc(g, new Vector3(1.05f, y, -.238f), .25f, .04f, colors[i]);
            g.Box(new Vector3(1.05f, y + .13f, -.29f), new Vector3(.34f, .055f, .24f), Metal);
        }
        g.Box(new Vector3(-1.14f, 1.15f, -.11f), new Vector3(.18f, .44f, .12f), DeepTeal);
        g.Box(new Vector3(-1.14f, 1.19f, -.185f), new Vector3(.09f, .09f, .04f), Ivory);
        SaveDressing("Street_TrafficSignal", g);
    }

    static void UtilityPole()
    {
        var g = new S1Geo();
        g.Cylinder(new Vector3(0, .18f, 0), .21f, .36f, 8, Dark);
        g.Cone(new Vector3(0, 2.73f, 0), .13f, .085f, 5.3f, 10, Concrete);
        g.Box(new Vector3(0, 4.7f, 0), new Vector3(2.18f, .13f, .16f), Metal);
        for (int i = 0; i < 5; i++)
        {
            float x = -.92f + i * .46f;
            g.Cylinder(new Vector3(x, 4.94f, 0), .065f, .42f, 8, Ivory);
            for (int j = 0; j < 3; j++)
                g.Cylinder(new Vector3(x, 4.84f + j * .09f, 0), .1f, .045f, 8, DarkGlass);
        }
        g.Cylinder(new Vector3(0, 5.34f, 0), .11f, .06f, 8, Metal);
        g.Box(new Vector3(.35f, 3.6f, 0), new Vector3(.58f, .88f, .65f), Metal);
        g.Box(new Vector3(.35f, 4.06f, 0), new Vector3(.66f, .09f, .73f), Light);
        for (int i = 0; i < 5; i++)
            g.Box(new Vector3(.35f, 3.35f + i * .12f, -.344f), new Vector3(.43f, .045f, .035f), Navy);
        g.Box(new Vector3(-.17f, 1.86f, -.08f), new Vector3(.32f, .55f, .3f), DeepTeal);
        g.Box(new Vector3(-.17f, 1.92f, -.244f), new Vector3(.18f, .18f, .025f), Ochre);
        g.Beam(new Vector3(.42f, 3.16f, .23f), new Vector3(.18f, 2.6f, .23f), .045f, .045f, Navy);
        g.Beam(new Vector3(.18f, 2.6f, .23f), new Vector3(.18f, .2f, .23f), .045f, .045f, Navy);
        var root = S1Assets.Save("Street_UtilityPole", g);
        S1Assets.Anchor(root, "CableAttachment_Left", new Vector3(-.92f, 5.15f, 0));
        S1Assets.Anchor(root, "CableAttachment_Right", new Vector3(.92f, 5.15f, 0));
        S1Assets.Finish(root);
    }

    static void Bollard()
    {
        var g = new S1Geo();
        g.Cylinder(new Vector3(0, .04f, 0), .2f, .08f, 8, Dark);
        g.Cone(new Vector3(0, .43f, 0), .145f, .12f, .78f, 8, Metal);
        g.Cylinder(new Vector3(0, .61f, 0), .151f, .11f, 8, Ivory);
        g.Cylinder(new Vector3(0, .77f, 0), .14f, .07f, 8, Ochre);
        g.Cone(new Vector3(0, .85f, 0), .135f, .07f, .1f, 8, DeepTeal);
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.PI * .5f;
            g.Cylinder(new Vector3(Mathf.Cos(a) * .14f, .084f, Mathf.Sin(a) * .14f), .018f, .018f, 6, Ivory);
        }
        SaveDressing("Street_Bollard", g);
    }

    static void CargoPallet()
    {
        var g = new S1Geo();
        for (int i = 0; i < 3; i++)
            g.Box(new Vector3(-.55f + i * .55f, .08f, 0), new Vector3(.18f, .16f, 1.2f), Wood);
        for (int i = 0; i < 6; i++)
            g.Box(new Vector3(0, .2f, -.5f + i * .2f), new Vector3(1.6f, .09f, .15f), i % 2 == 0 ? Wood : Ochre);
        Crate(g, new Vector3(-.38f, .53f, .03f), new Vector3(.69f, .57f, 1.0f));
        Crate(g, new Vector3(.38f, .49f, .03f), new Vector3(.65f, .49f, .97f));
        Crate(g, new Vector3(-.25f, 1.05f, .08f), new Vector3(.88f, .44f, .8f));
        var root = S1Assets.Save("Prop_CargoPallet", g);
        S1Assets.Anchor(root, "ItemDisplay", new Vector3(-.25f, 1.31f, .08f));
        S1Assets.Finish(root);
    }

    static void Crate(S1Geo g, Vector3 c, Vector3 size)
    {
        g.Box(c, size, Wood);
        for (int side = -1; side <= 1; side += 2)
        {
            g.Box(c + new Vector3(side * (size.x * .5f - .045f), 0, -size.z * .5f - .018f), new Vector3(.09f, size.y, .04f), Ochre);
            g.Box(c + new Vector3(0, side * (size.y * .5f - .04f), -size.z * .5f - .02f), new Vector3(size.x, .08f, .04f), Ochre);
        }
        g.Beam(c + new Vector3(-size.x * .35f, -size.y * .3f, -size.z * .5f - .045f), c + new Vector3(size.x * .35f, size.y * .3f, -size.z * .5f - .045f), .075f, .04f, Light);
        // Fit each strap to its own crate. The upper crate is offset and narrower than the
        // bottom pair, so a single world-space strap would project into empty space.
        for (int side = -1; side <= 1; side += 2)
        {
            float sx = side * size.x * .23f;
            g.Box(c + new Vector3(sx, 0, -size.z * .5f - .076f), new Vector3(.035f, size.y, .022f), DeepTeal);
            g.Box(c + new Vector3(sx, size.y * .5f + .012f, 0), new Vector3(.035f, .024f, size.z), DeepTeal);
            g.Box(c + new Vector3(sx, 0, size.z * .5f + .012f), new Vector3(.035f, size.y, .024f), DeepTeal);
        }
    }

    static void WasteBags()
    {
        var g = new S1Geo();
        BagMesh(g, new Vector3(-.47f, 0, -.13f), .36f, .65f, Bag);
        BagMesh(g, new Vector3(.18f, 0, .12f), .4f, .8f, DeepTeal);
        BagMesh(g, new Vector3(.62f, 0, -.23f), .25f, .49f, DarkGlass);
        g.BoxRotated(new Vector3(-.2f, .06f, -.37f), new Vector3(.62f, .1f, .32f), Quaternion.Euler(0, -18, 0), Wood);
        SaveDressing("Prop_WasteBags", g);
    }

    static void BagMesh(S1Geo g, Vector3 c, float r, float h, Color color)
    {
        // One continuous irregular sack: a flattened contact patch, sagging belly,
        // uneven shoulders and a pinched neck. Shared rings avoid stacked-cone seams.
        const int n = 9;
        float[] heights = { 0, .12f, .38f, .63f, .77f, .84f, .89f, .98f };
        float[] radii = { .62f, .94f, 1f, .84f, .46f, .11f, .13f, .28f };
        var rings = new Vector3[heights.Length, n];
        for (int row = 0; row < heights.Length; row++)
            for (int i = 0; i < n; i++)
            {
                float a = (i + .13f) * Mathf.PI * 2 / n;
                float irregular = 1 + .11f * Mathf.Sin(i * 2.31f + row * .55f);
                float lean = heights[row] * heights[row];
                float y = h * heights[row];
                if (row > 0 && row < 5) y += h * .025f * Mathf.Sin(i * 1.7f + row);
                rings[row, i] = c + new Vector3(
                    Mathf.Cos(a) * r * radii[row] * irregular - r * .2f * lean,
                    y,
                    Mathf.Sin(a) * r * radii[row] * (1 - .08f * Mathf.Cos(i * 1.8f)) + r * .13f * lean);
            }
        for (int row = 0; row < heights.Length - 1; row++)
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                var face = Color.Lerp(color, Navy, i % 3 == 0 ? .17f : .035f);
                g.Quad(rings[row, i], rings[row + 1, i], rings[row + 1, next], rings[row, next], face);
            }
        Vector3 bottom = c;
        Vector3 opening = c + new Vector3(-r * .2f, h * .97f, r * .13f);
        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            g.Tri(bottom, rings[0, i], rings[0, next], color);
            g.Tri(opening, rings[heights.Length - 1, next], rings[heights.Length - 1, i], Navy);
        }
        Vector3 neck = c + new Vector3(-r * .2f * .84f * .84f, h * .855f, r * .13f * .84f * .84f);
        g.Cylinder(neck, r * .145f, h * .035f, 8, Ivory);
        g.BoxRotated(neck + new Vector3(r * .14f, -.005f, -.025f), new Vector3(r * .28f, .018f, .035f), Quaternion.Euler(0, 15, -17), Ivory);
    }

    static void Barricade()
    {
        var g = new S1Geo();
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * .91f;
            g.Box(new Vector3(x, .055f, 0), new Vector3(.32f, .11f, .8f), Dark);
            g.Beam(new Vector3(x, .11f, -.31f), new Vector3(x, .92f, 0), .09f, .09f, Metal);
            g.Beam(new Vector3(x, .11f, .31f), new Vector3(x, .92f, 0), .09f, .09f, Metal);
            g.Cylinder(new Vector3(x, 1.035f, 0), .07f, .18f, 8, Ochre);
            g.Cone(new Vector3(x, 1.15f, 0), .07f, .04f, .06f, 8, Red);
        }
        g.Box(new Vector3(0, .75f, 0), new Vector3(2.4f, .33f, .12f), Ochre);
        g.Box(new Vector3(0, .36f, .02f), new Vector3(2.12f, .14f, .1f), DeepTeal);
        for (int i = 0; i < 8; i++)
            g.BoxRotated(new Vector3(-1.02f + i * .29f, .75f, -.075f), new Vector3(.13f, .32f, .025f), Quaternion.Euler(0, 0, -27), Navy);
        SaveDressing("Prop_EmergencyBarricade", g);
    }

    static void Rubble()
    {
        var g = new S1Geo();
        var centers = new[] { new Vector3(-.99f,.2f,.14f),new Vector3(-.29f,.21f,-.34f),new Vector3(.43f,.26f,.12f),new Vector3(1.09f,.14f,-.1f),new Vector3(-.08f,.47f,.17f) };
        var sizes = new[] { new Vector3(.88f,.34f,.75f),new Vector3(.73f,.3f,.81f),new Vector3(.95f,.42f,.81f),new Vector3(.62f,.23f,.58f),new Vector3(.72f,.23f,.55f) };
        for (int i = 0; i < centers.Length; i++)
        {
            g.BoxRotated(centers[i], sizes[i], Quaternion.Euler(i % 2 == 0 ? 8 : -7, -28 + i * 23, -13 + i * 7), i % 2 == 0 ? Light : Concrete);
            g.BoxRotated(centers[i] + new Vector3(0,.07f,-sizes[i].z*.43f), new Vector3(sizes[i].x*.56f,.06f,.035f), Quaternion.Euler(0,0,-15+i*11), Dark);
        }
        g.Beam(new Vector3(.32f,.4f,.31f), new Vector3(.74f,.66f,.44f), .027f, .027f, Red);
        g.Beam(new Vector3(.74f,.66f,.44f), new Vector3(.91f,.62f,.47f), .027f, .027f, Red);
        g.Beam(new Vector3(-.65f,.31f,-.13f), new Vector3(-.82f,.54f,-.24f), .025f, .025f, Metal);
        SaveDressing("Damage_RubbleCluster", g);
    }

    static void CrackedRoad()
    {
        var g = new S1Geo();
        g.Box(new Vector3(0, -.14f, 0), new Vector3(6, .22f, 4), Navy);
        // Separate asphalt fragments expose real dark seams. Highest fragment is only 4cm above the collision plane.
        float[] widths = { 1.92f, 2.03f, 1.93f };
        float[] cx = { -2.02f, 0, 2.02f };
        for (int row = 0; row < 2; row++)
            for (int col = 0; col < 3; col++)
            {
                float top = (col + row) % 3 == 1 ? .04f : .012f;
                g.Box(new Vector3(cx[col], top - .035f, -.99f + row * 1.98f), new Vector3(widths[col], .07f, 1.91f), (row + col) % 2 == 0 ? Dark : Concrete);
            }
        for (int i = 0; i < 3; i++)
            g.Box(new Vector3(-2.17f + i * 2.03f, .043f, .05f), new Vector3(.98f, .006f, .1f), Ochre);
        // Displaced pieces at the edge tell a damaged street story without creating a gameplay pit.
        g.BoxRotated(new Vector3(-.75f,.015f,-1.52f),new Vector3(.55f,.04f,.38f),Quaternion.Euler(0,-17,0),Light);
        g.BoxRotated(new Vector3(1.15f,.015f,1.55f),new Vector3(.45f,.04f,.35f),Quaternion.Euler(0,28,0),Light);
        var root = S1Assets.Save("Damage_CrackedRoad_6m", g);
        S1Assets.Collider(root, "ContinuousRoad", new Vector3(0, -.12f, 0), new Vector3(6, .24f, 4));
        S1Assets.Anchor(root, "SnapLeft", new Vector3(-3, 0, 0));
        S1Assets.Anchor(root, "SnapRight", new Vector3(3, 0, 0));
        S1Assets.Anchor(root, "WalkableSurface", Vector3.zero);
        S1Assets.Finish(root);
    }

    // An eight-sided planar disc made from crossed rectangles, for opaque faceted lenses/dishes.
    static void FrontDisc(S1Geo g, Vector3 center, float diameter, float depth, Color color)
    {
        g.Box(center, new Vector3(diameter * .64f, diameter, depth), color);
        g.Box(center, new Vector3(diameter, diameter * .64f, depth), color);
        g.BoxRotated(center, new Vector3(diameter * .64f, diameter * .86f, depth), Quaternion.Euler(0, 0, 45), color);
    }

    static void SaveDressing(string name, S1Geo g)
    {
        var root = S1Assets.Save(name, g);
        S1Assets.Anchor(root, "Placement", Vector3.zero);
        S1Assets.Finish(root);
    }

    static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var color); return color; }
}
#endif
