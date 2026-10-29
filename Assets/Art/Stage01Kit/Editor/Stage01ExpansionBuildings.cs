// Original Stage 1 architecture. X is travel, Y is up; storefronts face -Z.
// Rendered geometry is authored in meters and shares the retained urban vertex palette.
#if UNITY_EDITOR
using UnityEngine;

public static class Stage01ExpansionBuildings
{
    static readonly Color Concrete = Hex("5B626B"), Light = Hex("747D84"), Dark = Hex("434E5A");
    static readonly Color Navy = Hex("22313E"), Metal = Hex("465563"), Teal = Hex("486F73");
    static readonly Color DeepTeal = Hex("334F58"), Glass = Hex("485F69"), DarkGlass = Hex("2E424F");
    static readonly Color Ivory = Hex("A5AEAE"), Ochre = Hex("9B895F"), Red = Hex("815D55"), Green = Hex("42564D");
    static readonly Color Warm = new Color(.7255f, .6431f, .4784f, .65f);

    public static void Build()
    {
        Solid("Building_ConvenienceStore", 8, 4, 5, Convenience(true), Convenience(false));
        Solid("Building_CornerCafe", 8, 8, 5, Cafe(true), Cafe(false));
        Solid("Building_MixedUseNarrow", 6, 12, 5, MixedUse(true), MixedUse(false));
        Solid("Building_Rowhouse", 8, 8, 5, Rowhouse(true), Rowhouse(false));
        Solid("Building_ParkingDeck", 12, 8, 6, Parking(true), Parking(false));
        Interior();
    }

    static void Solid(string name, float width, float height, float depth, S1Geo high, S1Geo low)
    {
        var root = S1Assets.Save(name, high, low);
        S1Assets.Collider(root, "SolidBuilding", new Vector3(0, height * .5f, 0), new Vector3(width, height, depth));
        S1Assets.Anchor(root, "RoofSurface", new Vector3(0, height, 0));
        S1Assets.Anchor(root, "RoofLeft", new Vector3(-width * .5f, height, 0));
        S1Assets.Anchor(root, "RoofRight", new Vector3(width * .5f, height, 0));
        S1Assets.Anchor(root, "GroundFront", new Vector3(0, 0, -depth * .5f - .8f));
        S1Assets.Finish(root);
    }

    // A broad low corner shop. Display and refrigerator details are shallow facade relief,
    // while the separate cutaway shop provides a genuinely open walkable interior.
    static S1Geo Convenience(bool detail)
    {
        var g = Shell(8, 4, 5, Light, detail);
        g.Box(V(0, .36f, -2.54f), V(7.7f, .58f, .12f), Dark);
        for (int i = 0; i < 4; i++) Window(g, -2.85f + i * 1.9f, 1.53f, -2.5f, 1.7f, 2.05f, i == 1 ? Warm : Glass, detail);
        g.Box(V(0, 2.95f, -2.65f), V(8.2f, .76f, .3f), DeepTeal);
        g.Box(V(0, 3.28f, -2.83f), V(8.25f, .09f, .1f), Ivory);
        g.Box(V(0, 2.63f, -2.84f), V(8.25f, .09f, .1f), Ochre);
        Sign(g, V(-.3f, 2.95f, -2.84f), 3.5f, .45f, Ivory, detail);
        g.BoxRotated(V(0, 2.52f, -2.98f), V(7.85f, .1f, .83f), Quaternion.Euler(-9, 0, 0), Teal);
        g.Beam(V(-3.65f, 2.15f, -2.55f), V(-3.65f, 2.45f, -3.33f), .055f, .055f, Metal);
        g.Beam(V(3.65f, 2.15f, -2.55f), V(3.65f, 2.45f, -3.33f), .055f, .055f, Metal);
        if (detail)
        {
            // Visible merchandise silhouettes behind the left opaque display panel.
            for (int row = 0; row < 3; row++)
            {
                float y = .75f + row * .47f;
                g.Box(V(-2.85f, y - .09f, -2.64f), V(1.52f, .055f, .1f), Ivory);
                for (int col = 0; col < 5; col++)
                    g.Box(V(-3.4f + col * .28f, y + .04f, -2.665f), V(.16f, .24f, .035f), (row + col) % 3 == 0 ? Ochre : Teal);
            }
            g.Box(V(.7f, 1.35f, -2.67f), V(.065f, .38f, .07f), Ivory);
            g.Box(V(1.06f, 1.35f, -2.67f), V(.065f, .38f, .07f), Ivory);
            g.Box(V(2.83f, 1.95f, -2.665f), V(1.4f, .16f, .04f), Red);
            g.Box(V(2.83f, 1.71f, -2.665f), V(1.1f, .1f, .04f), Ivory);
            WallGrille(g, V(4.035f, 1, .7f), true);
            RoofDrain(g, 3.65f, 4, 2.5f);
            g.Box(V(-2.6f, 4.22f, 1.77f), V(1.45f, .44f, .68f), Metal);
            for (int i = 0; i < 5; i++) g.Box(V(-3.14f + .27f * i, 4.45f, 1.77f), V(.075f, .035f, .47f), Navy);
        }
        return g;
    }

    static S1Geo Cafe(bool detail)
    {
        var g = Shell(8, 8, 5, Concrete, detail);
        // Deep-set entrance at the right breaks the rhythm of the display windows.
        for (int i = 0; i < 3; i++) Window(g, -2.5f + i * 2.1f, 1.6f, -2.5f, 1.72f, 2.28f, i == 0 ? Warm : DarkGlass, detail);
        Window(g, 3.1f, 1.38f, -2.5f, .82f, 2.3f, Glass, detail);
        g.BoxRotated(V(-.45f, 3.2f, -3.07f), V(7.15f, .16f, 1.16f), Quaternion.Euler(-11, 0, 0), Red);
        g.Box(V(-.45f, 3.02f, -3.61f), V(7.17f, .3f, .1f), Red);
        g.Box(V(-.45f, 3.04f, -3.68f), V(6.9f, .08f, .025f), Ivory);
        g.Box(V(-.5f, 3.8f, -2.68f), V(5.3f, .65f, .32f), Navy);
        Sign(g, V(-.5f, 3.8f, -2.86f), 2.85f, .42f, Ochre, detail);
        for (int i = 0; i < 3; i++) Window(g, -2.48f + i * 2.48f, 5.58f, -2.5f, 1.7f, 2.15f, i == 2 ? Warm : Glass, detail);
        g.Box(V(-.25f, 4.45f, -2.92f), V(7.1f, .17f, 1.04f), Light);
        Rail(g, -3.7f, 3.2f, 4.53f, -3.43f, detail);
        // Return balcony rails leave the roof itself and the Z0 travel plane open.
        g.Beam(V(-3.7f, 4.98f, -3.43f), V(-3.7f, 4.98f, -2.52f), .055f, .055f, Metal);
        g.Beam(V(3.2f, 4.98f, -3.43f), V(3.2f, 4.98f, -2.52f), .055f, .055f, Metal);
        if (detail)
        {
            g.Box(V(-3.32f, 1.04f, -2.66f), V(.055f, .55f, .06f), Ivory);
            g.Box(V(3.31f, 1.15f, -2.66f), V(.055f, .33f, .06f), Ivory);
            for (int i = 0; i < 4; i++) g.Box(V(-3.75f + i * 2.45f, 6.85f, -2.56f), V(.23f, 1.7f, .12f), Light);
            // Two octagonal planter pots and restrained blocky shrubs on the balcony.
            foreach (float x in new[] { -2.95f, 2.45f })
            {
                g.Cone(V(x, 4.7f, -2.97f), .22f, .28f, .34f, 6, Dark);
                g.Cone(V(x, 5.05f, -2.97f), .3f, .18f, .5f, 6, Green);
            }
            RoofDrain(g, 3.68f, 8, 2.5f);
            g.Box(V(-2.5f, 8.25f, 1.7f), V(1.55f, .5f, .9f), Metal);
            g.Cylinder(V(-2.5f, 8.52f, 1.7f), .29f, .055f, 10, Navy);
        }
        return g;
    }

    static S1Geo MixedUse(bool detail)
    {
        var g = Shell(6, 12, 5, Dark, detail);
        Window(g, -1.25f, 1.6f, -2.5f, 1.72f, 2.3f, Glass, detail);
        Window(g, 1.3f, 1.6f, -2.5f, 1.65f, 2.3f, DarkGlass, detail);
        g.Box(V(0, 3.03f, -2.66f), V(5.55f, .65f, .3f), Ochre);
        Sign(g, V(0, 3.03f, -2.83f), 2.9f, .4f, Navy, detail);
        g.Box(V(-2.74f, 7.45f, -2.73f), V(.53f, 5.4f, .48f), DeepTeal);
        g.Box(V(-2.74f, 10.2f, -2.73f), V(.64f, .12f, .55f), Ivory);
        for (int row = 0; row < 3; row++)
        {
            float y = 4.65f + row * 2.55f;
            for (int col = 0; col < 2; col++) Window(g, -.95f + col * 2.1f, y, -2.5f, 1.5f, 1.75f, row == 1 && col == 1 ? Warm : Glass, detail);
            g.Box(V(.25f, y - 1.05f, -2.56f), V(4.85f, .13f, .12f), Light);
            if (detail) Sign(g, V(-2.74f, 5.55f + row * 1.65f, -2.985f), .27f, .62f, Ivory, true);
        }
        // Stairhead is behind the roof lane, so the complete X route at Z0 stays clear.
        g.Box(V(1.48f, 12.77f, 1.55f), V(1.7f, 1.54f, 1.55f), Concrete);
        g.Box(V(1.48f, 13.57f, 1.55f), V(1.92f, .14f, 1.77f), Metal);
        g.Box(V(1.48f, 12.65f, .755f), V(.82f, 1.3f, .045f), DeepTeal);
        if (detail)
        {
            g.Box(V(1.75f, 12.65f, .715f), V(.045f, .18f, .045f), Ivory);
            g.Box(V(2.78f, 6.05f, -2.62f), V(.18f, 11.3f, .16f), Teal);
            g.Box(V(-.7f, 12.28f, 1.55f), V(1.05f, .56f, .7f), Metal);
            for (int i = 0; i < 5; i++) g.Box(V(-1.06f + i * .18f, 12.58f, 1.55f), V(.055f, .045f, .53f), Navy);
            for (int row = 0; row < 4; row++) SideWindow(g, 3.025f, 1.6f + row * 2.55f, .55f, 1.25f, 1.58f, detail);
            RoofDrain(g, -2.7f, 12, 2.5f);
        }
        return g;
    }

    static S1Geo Rowhouse(bool detail)
    {
        var g = Shell(8, 8, 5, Red, detail);
        g.Box(V(0, .55f, -2.54f), V(7.9f, 1.04f, .1f), Dark);
        // Central entrance and paired windows give this facade a residential rhythm.
        Window(g, 0, 1.45f, -2.5f, 1.08f, 2.52f, DeepTeal, detail);
        foreach (float x in new[] { -2.55f, 2.55f }) Window(g, x, 1.72f, -2.5f, 1.6f, 1.6f, Glass, detail);
        for (int row = 0; row < 2; row++)
        {
            float y = 4.4f + row * 2.2f;
            foreach (float x in new[] { -2.45f, 0f, 2.45f }) Window(g, x, y, -2.5f, 1.5f, 1.56f, row == 1 && x < 0 ? Warm : DarkGlass, detail);
            foreach (float x in new[] { -2.45f, 2.45f })
            {
                g.Box(V(x, y - .87f, -2.88f), V(1.82f, .16f, .85f), Light);
                Rail(g, x - .85f, x + .85f, y - .78f, -3.29f, detail);
            }
        }
        g.Box(V(0, 2.96f, -2.83f), V(2.18f, .14f, .7f), Metal);
        if (detail)
        {
            // Low facade brick courses, broad enough to read without fine textures.
            for (int row = 0; row < 8; row++)
                foreach (float x in new[] { -3.65f, 3.65f }) g.Box(V(x, 1.2f + row * .78f, -2.563f), V(.5f, .045f, .028f), Light);
            g.Box(V(.37f, 1.35f, -2.68f), V(.055f, .3f, .055f), Ivory);
            g.Box(V(-.94f, 1.72f, -2.64f), V(.18f, .32f, .08f), Metal);
            g.Box(V(-.94f, 1.75f, -2.69f), V(.1f, .07f, .018f), Warm);
            // Offset rooftop laundry supports have no banner across the walking lane.
            g.Cylinder(V(-2.85f, 8.63f, 1.75f), .035f, 1.26f, 6, Metal);
            g.Cylinder(V(-.55f, 8.63f, 1.75f), .035f, 1.26f, 6, Metal);
            g.Beam(V(-2.85f, 9.15f, 1.75f), V(-.55f, 9.15f, 1.75f), .025f, .025f, Metal);
            for (int i = 0; i < 3; i++) g.Box(V(-2.5f + i * .78f, 8.87f, 1.75f), V(.48f, .54f, .035f), i == 1 ? Ivory : Teal);
            RoofDrain(g, 3.6f, 8, 2.5f);
            for (int row = 0; row < 3; row++) SideWindow(g, 4.025f, 1.65f + row * 2.35f, .25f, 1.26f, 1.4f, detail);
        }
        return g;
    }

    static S1Geo Parking(bool detail)
    {
        var g = new S1Geo();
        // Actual separated slabs and columns create open bays rather than painted windows.
        g.Box(V(0, .17f, 0), V(12, .34f, 6), Dark);
        foreach (float y in new[] { 3.93f, 7.93f })
        {
            g.Box(V(0, y - .1f, 0), V(12.15f, .34f, 6.15f), Light);
            g.Box(V(0, y + .078f, 0), V(11.95f, .016f, 5.95f), Dark);
        }
        // The thin roof finish sits above the structural slab to avoid coplanar faces.
        for (int i = 0; i < 5; i++)
        {
            float x = -5.55f + i * 2.775f;
            foreach (float z in new[] { -2.62f, 2.62f })
            {
                g.Box(V(x, 4, z), V(.48f, 8, .5f), Concrete);
                g.Box(V(x, .62f, z), V(.5f, .56f, .52f), Ochre);
            }
        }
        for (int level = 0; level < 2; level++)
        {
            float y = .6f + level * 4;
            g.Box(V(0, y, 2.9f), V(11.9f, .56f, .2f), Concrete);
            for (int side = -1; side <= 1; side += 2)
                g.Box(V(side * 5.95f, y, 0), V(.16f, .56f, 5.8f), Concrete);
            // Partial front guard has openings under the bays and a drive-in entrance.
            g.Box(V(-3.5f, y, -2.94f), V(4.75f, .52f, .16f), Dark);
            g.Box(V(3.5f, y, -2.94f), V(4.75f, .52f, .16f), Dark);
            if (detail)
            {
                for (int k = 0; k < 3; k++)
                {
                    g.Box(V(-3.5f, y + .55f + k * .2f, -2.96f), V(4.75f, .065f, .1f), Metal);
                    g.Box(V(3.5f, y + .55f + k * .2f, -2.96f), V(4.75f, .065f, .1f), Metal);
                }
                // Diagonal rear ramp silhouette stays behind Z1.2.
                g.Beam(V(-5.1f, .45f + level * 4, 1.8f), V(4.9f, 3.6f + level * 4, 1.8f), .25f, 1.6f, Concrete);
                for (int k = 0; k < 6; k++)
                    g.Box(V(-4.9f + k * 1.95f, .35f + level * 4, -.1f), V(.06f, .025f, 2.4f), Ivory);
            }
        }
        g.Box(V(0, 3.1f, -3.14f), V(2.55f, .92f, .24f), DeepTeal);
        // Original geometric parking symbol, a vertical stem with square top loop.
        g.Box(V(-.5f, 3.1f, -3.28f), V(.12f, .66f, .04f), Ivory);
        g.Box(V(-.27f, 3.36f, -3.28f), V(.46f, .12f, .04f), Ivory);
        g.Box(V(-.27f, 3.08f, -3.28f), V(.46f, .12f, .04f), Ivory);
        g.Box(V(-.03f, 3.22f, -3.28f), V(.12f, .4f, .04f), Ivory);
        g.Box(V(.7f, 3.1f, -3.28f), V(.68f, .1f, .04f), Ochre);
        g.Beam(V(.9f, 3.25f, -3.28f), V(1.08f, 3.1f, -3.28f), .1f, .04f, Ochre);
        g.Beam(V(.9f, 2.95f, -3.28f), V(1.08f, 3.1f, -3.28f), .1f, .04f, Ochre);
        g.Box(V(0, 8.2f, 2.9f), V(12, .4f, .2f), Concrete);
        if (detail)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                g.Box(V(side * 1.08f, .52f, -3.2f), V(.14f, 1.04f, .14f), Metal);
                g.Box(V(side * 1.08f, .96f, -3.2f), V(.16f, .16f, .16f), Ochre);
            }
            for (int i = 0; i < 4; i++) g.Box(V(-4.3f + i * 2.85f, 8.012f, 1.8f), V(1.25f, .024f, .06f), Ivory);
        }
        return g;
    }

    static void Interior()
    {
        var root = S1Assets.Save("Shop_InteriorSlice_8m", InteriorMesh(true), InteriorMesh(false));
        S1Assets.Collider(root, "Floor", V(0, -.12f, 0), V(8, .24f, 5));
        S1Assets.Collider(root, "RearWall", V(0, 2, 2.42f), V(8, 4, .16f));
        // Front openings in the side walls admit the continuous Z0 horizontal route.
        S1Assets.Collider(root, "LeftRearWall", V(-3.92f, 2, 1.85f), V(.16f, 4, 1.3f));
        S1Assets.Collider(root, "RightRearWall", V(3.92f, 2, 1.85f), V(.16f, 4, 1.3f));
        S1Assets.Collider(root, "RearCeiling", V(0, 3.91f, 1.75f), V(8, .18f, 1.5f));
        S1Assets.Anchor(root, "WalkableSurface", V(0, 0, 0));
        S1Assets.Anchor(root, "SnapLeft", V(-4, 0, 0));
        S1Assets.Anchor(root, "SnapRight", V(4, 0, 0));
        S1Assets.Anchor(root, "Entry", V(-4, 0, 0));
        S1Assets.Anchor(root, "Exit", V(4, 0, 0));
        S1Assets.Anchor(root, "RoofSurface", V(0, 4, 1.75f));
        S1Assets.Anchor(root, "RoofLeft", V(-4, 4, 1.75f));
        S1Assets.Anchor(root, "RoofRight", V(4, 4, 1.75f));
        S1Assets.Anchor(root, "GroundFront", V(0, 0, -3.25f));
        S1Assets.Anchor(root, "ItemAlcove_Health", V(-2.9f, 1.6f, 1.82f));
        S1Assets.Anchor(root, "ItemAlcove_Buff", V(2.8f, 1.05f, 1.65f));
        S1Assets.Finish(root);
    }

    static S1Geo InteriorMesh(bool detail)
    {
        var g = new S1Geo();
        g.Box(V(0, -.12f, 0), V(8, .24f, 5), Light);
        g.Box(V(0, 1.96f, 2.42f), V(8, 3.92f, .16f), Concrete);
        g.Box(V(-3.92f, 1.96f, 1.85f), V(.16f, 3.92f, 1.3f), Dark);
        g.Box(V(3.92f, 1.96f, 1.85f), V(.16f, 3.92f, 1.3f), Dark);
        g.Box(V(0, 3.91f, 1.75f), V(8, .18f, 1.5f), Metal);
        g.Box(V(0, .22f, 2.32f), V(7.7f, .44f, .08f), DeepTeal);
        g.Box(V(0, 3.24f, 2.31f), V(7.7f, .46f, .08f), DeepTeal);
        Sign(g, V(-.15f, 3.25f, 2.25f), 3.5f, .32f, Ivory, detail);
        // Short low shelves live behind the travel lane, so they do not form invisible walls.
        foreach (float x in new[] { -2.9f, -.6f })
        {
            g.Box(V(x, .12f, 1.82f), V(1.82f, .24f, .82f), Dark);
            g.Box(V(x, 1.05f, 2.1f), V(1.82f, 1.85f, .12f), Metal);
            foreach (float y in new[] { .48f, 1.02f, 1.56f })
            {
                g.Box(V(x, y, 1.82f), V(1.85f, .075f, .78f), Ivory);
                if (detail)
                    for (int col = 0; col < 6; col++)
                    {
                        // Reserve the left top-shelf center for the health-item anchor.
                        if (x < -2 && y > 1.5f && (col == 2 || col == 3)) continue;
                        float cx = x - .69f + .275f * col;
                        g.Box(V(cx, y + .18f, 1.82f), V(.17f, .28f + .04f * (col % 2), .23f), col % 3 == 0 ? Ochre : col % 3 == 1 ? Teal : Red);
                    }
            }
        }
        // Counter, register and display fridge use big readable geometry.
        g.Box(V(2.55f, .46f, 1.77f), V(2, .92f, .88f), DeepTeal);
        g.Box(V(2.55f, .97f, 1.77f), V(2.15f, .1f, 1.02f), Ivory);
        g.Box(V(2.2f, 1.1f, 1.8f), V(.5f, .16f, .4f), Metal);
        g.BoxRotated(V(2.2f, 1.3f, 1.84f), V(.46f, .28f, .06f), Quaternion.Euler(13, 0, 0), Navy);
        g.Box(V(1.3f, 1.32f, 2.05f), V(.92f, 2.64f, .68f), Light);
        g.Box(V(1.3f, 1.28f, 1.69f), V(.76f, 2.25f, .035f), Glass);
        g.Box(V(1.3f, 2.54f, 1.67f), V(.74f, .16f, .04f), Warm);
        if (detail)
        {
            for (int row = 0; row < 3; row++)
            {
                g.Box(V(1.3f, .55f + row * .65f, 1.65f), V(.72f, .04f, .08f), Ivory);
                for (int col = 0; col < 3; col++) g.Cylinder(V(1.07f + col * .23f, .76f + row * .65f, 1.62f), .065f, .29f, 6, col == 1 ? Ochre : Teal);
            }
            g.Box(V(1.59f, 1.33f, 1.62f), V(.04f, .37f, .04f), Ivory);
            for (int i = 0; i < 7; i++) g.Box(V(-3.4f + i * 1.12f, .009f, -.3f), V(.025f, .018f, 4.05f), Dark);
            for (int i = 0; i < 4; i++) g.Box(V(0, .01f, -1.85f + i * 1.13f), V(7.8f, .02f, .025f), Dark);
            g.Box(V(-2.25f, 3.78f, 1.65f), V(1.6f, .08f, .24f), Warm);
            g.Box(V(2.1f, 3.78f, 1.65f), V(1.6f, .08f, .24f), Warm);
            g.Box(V(3.24f, 2.52f, 2.3f), V(.62f, .5f, .045f), Navy);
            g.Box(V(3.24f, 2.52f, 2.265f), V(.44f, .29f, .025f), Ochre);
        }
        return g;
    }

    static S1Geo Shell(float w, float h, float d, Color body, bool detail)
    {
        var g = new S1Geo();
        g.Box(V(0, (h - .26f) * .5f, 0), V(w, h - .26f, d), body);
        g.Box(V(0, .13f, 0), V(w + .12f, .26f, d + .12f), Dark);
        g.Box(V(0, h - .13f, 0), V(w + .18f, .26f, d + .18f), Ivory);
        g.Box(V(0, h + .004f, 0), V(w - .12f, .008f, d - .12f), Dark);
        g.Box(V(0, h + .15f, d * .5f - .09f), V(w, .3f, .18f), Light);
        if (detail)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                g.Box(V(side * (w * .5f - .1f), h * .5f, -d * .5f - .03f), V(.2f, h - .3f, .06f), Dark);
                g.Box(V(side * (w * .5f - .09f), h + .15f, d * .3f), V(.18f, .3f, d * .4f), Light);
            }
        }
        return g;
    }

    static void Window(S1Geo g, float x, float y, float z, float w, float h, Color pane, bool detail)
    {
        g.Box(V(x, y, z - .035f), V(w + .14f, h + .14f, .07f), Navy);
        g.Box(V(x, y, z - .079f), V(w, h, .018f), pane);
        if (!detail) return;
        g.Box(V(x, y, z - .11f), V(.045f, h, .055f), Light);
        g.Box(V(x, y - h * .5f - .065f, z - .1f), V(w + .24f, .13f, .2f), Light);
        g.Box(V(x - w * .31f, y + h * .32f, z - .093f), V(w * .1f, h * .17f, .008f), Color.Lerp(pane, Ivory, .16f));
    }

    static void SideWindow(S1Geo g, float x, float y, float z, float w, float h, bool detail)
    {
        g.Box(V(x, y, z), V(.07f, h + .14f, w + .14f), Navy);
        g.Box(V(x + .045f, y, z), V(.018f, h, w), Glass);
        if (detail) g.Box(V(x + .065f, y - h * .5f - .06f, z), V(.18f, .12f, w + .2f), Light);
    }

    static void Sign(S1Geo g, Vector3 center, float width, float height, Color ink, bool detail)
    {
        // Abstract glyph groups are original geometric marks, not a copied brand or tiny text.
        int count = detail ? 5 : 3;
        float cell = width / count;
        for (int i = 0; i < count; i++)
        {
            float x = center.x - width * .5f + cell * (i + .5f);
            g.Box(V(x - cell * .23f, center.y, center.z), V(cell * .11f, height, .025f), ink);
            g.Box(V(x + cell * .04f, center.y + height * .35f, center.z), V(cell * .45f, height * .17f, .025f), ink);
            g.Box(V(x + cell * .15f, center.y - height * .02f, center.z), V(cell * .15f, height * .5f, .025f), ink);
            if (detail) g.Box(V(x + cell * .04f, center.y - height * .37f, center.z), V(cell * .45f, height * .15f, .025f), ink);
        }
    }

    static void Rail(S1Geo g, float left, float right, float floorY, float z, bool detail)
    {
        g.Beam(V(left, floorY + .7f, z), V(right, floorY + .7f, z), .065f, .065f, Metal);
        g.Beam(V(left, floorY + .31f, z), V(right, floorY + .31f, z), .045f, .045f, Metal);
        int n = detail ? Mathf.Max(2, Mathf.CeilToInt((right - left) / .55f)) : 2;
        for (int i = 0; i <= n; i++) g.Box(V(Mathf.Lerp(left, right, i / (float)n), floorY + .35f, z), V(.05f, .7f, .05f), Metal);
    }

    static void WallGrille(S1Geo g, Vector3 center, bool side)
    {
        g.Box(center, side ? V(.08f, .72f, 1.05f) : V(1.05f, .72f, .08f), Navy);
        for (int i = 0; i < 5; i++)
            g.Box(center + V(side ? .045f : 0, -.25f + i * .125f, side ? 0 : -.045f), side ? V(.06f, .045f, .94f) : V(.94f, .045f, .06f), Metal);
    }

    static void RoofDrain(S1Geo g, float x, float height, float rearZ)
    {
        g.Cylinder(V(x, height * .49f, rearZ + .07f), .06f, height * .98f, 6, Metal);
        g.Beam(V(x, height - .1f, rearZ + .07f), V(x, height + .09f, rearZ - .23f), .11f, .11f, Metal);
        for (int i = 0; i < Mathf.CeilToInt(height / 2); i++) g.Box(V(x, .6f + i * 2, rearZ + .08f), V(.18f, .065f, .14f), Light);
    }

    static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
    static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var c); return c; }
}
#endif
