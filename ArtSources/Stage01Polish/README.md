# Stage 01: sun-faded city and fractured rifts

Original project assets are authored deterministically by `Assets/1.Script/LevelDesign/Editor/Stage01ArtFactory.cs`.
There are no new runtime components. Source kit meshes and materials are never overwritten. Generated art is stored in `Assets/Art/Stage01Polish/`.

## Art direction and reading order

The playable foreground uses warm clay, cream cornices, desaturated petrol glass, deep navy contours and clearly grounded stone plinths. Cool, low-contrast distant architecture separates the foreground silhouette. Ink width is 0.025 world meters (approximately two pixels in a 1080-high orthographic view at size 6.8); distant contours are narrower and lighter. The shader uses two broad light bands and a cool shadow floor rather than small noisy surface detail.

Rifts are original sixteen-stone elliptical sculptures, with an actual thick silhouette, structural buttresses and an overlapping plinth. A dark aperture remains when inactive. Active cyan energy and boss violet energy use analytic shader-time flow. A small number of diamond particles accent the edge rather than obscuring the landing surface. All visual pieces deliberately have no colliders.

The top StatusGem renderer is a separate state target for the existing StageGate color property block. Assign that child renderer to StageGate's gateRenderer; assigning the aperture or stone frame would overwrite the art palette. The existing GateAnimController's IsActive boolean drives the authored Animator states. Dormant/closed leave the physical arch present and disable EnergyField; active enables it. No game state or gate code changes are required.

## Factory API

- `EnsureAssets()` creates missing deterministic materials, meshes, rift prefabs, animation clips and controller.
- `GetMaterial(name)` returns a named shared material.
- `ApplyLegacyPalette(root, theme = "Clay", outlines = true)` assigns repainted mesh copies and separately rendered welded-normal ink hulls, including each existing LOD level.
- `CreatePortalVisual(parent, boss = false)` instantiates the appropriate prefab and returns its root.
- `AddOutline(object, distant = false)` preserves the object's existing material and adds a separate inverted hull.

Vertex-painted kit themes: Clay, Teal, Ochre, Foliage, Far. These use `_UseVertexColor = 1`. Uniform solid materials: TrimCream, StoneWarm, Foundation, Asphalt, Roof, CanopyTeal, Gold, DarkMetal, FoliageSolid, Skyline. Additional materials: Ink, InkFar, PortalTeal, PortalViolet, PortalCoreTeal, PortalCoreViolet, SparkTeal, SparkViolet.

The uniform shader is `ProjectM/Stage01/City Cel`; `_BaseColor` controls paint and `_UseVertexColor=0` is the default. Root-level authoring can add further uniformly colored materials without modifying this factory.

Paint swatches are specified in sRGB and converted to linear vertex attributes when the project's color space is Linear. This conversion is deliberately separate from material color properties. Cached repainted meshes update in-place, preserving asset GUIDs, on the next builder run after an editor domain reload. All four City Cel passes share one material constant-buffer definition; the shader does not borrow passes from a material with a different SRP buffer layout.

`ProjectM/Stage01/Sky Atmosphere` is a separate, single-pass URP unlit shader for the distant sky plate. Its world-height gradient transitions from `_HorizonColor` (soft mint, default 0.70/0.84/0.81) to `_ZenithColor` (blue, default 0.34/0.57/0.74), between `_HorizonY=-12` and `_ZenithY=18`. It has no outline, shadow pass or lighting response. A smooth gradient provides broad color variation while leaving the foreground silhouettes quiet. Keep the sky plate within the actual camera far clip; the stage camera at Z=-25 with far clip 100 requires the plate around Z=69 rather than Z=84. Root stage authoring owns the sky material and sparse cloud geometry.

## Professional references studied

- Valve, [Illustrative Rendering in Team Fortress 2](https://cdn.steamstatic.com/apps/valve/2007/NPAR07_IllustrativeRenderingInTeamFortress2.pdf), NPAR 2007. Applied the principles of strong silhouette, controlled luminance hierarchy, and coherent warm/cool response. No Valve textures or meshes are included.
- Arc System Works / Junya Christopher Motomura, [GuiltyGearXrd's Art Style: The X Factor Between 2D and 3D](https://media.gdcvault.com/gdc2015/presentations/Motomura_Junya_GuiltyGearXrd.pdf), GDC 2015. Applied the principle that contour design and intentionally authored geometry need to support the cel shader. No Arc System Works imagery or assets are included.

## QA responsibilities

The factory is editor-only. Shader animation never changes physics or portal trigger placement. New visual prefab assets contain no colliders. Main stage authoring must explicitly check the gate landing footprint, vertical route, off-screen object culling and foreground occlusion in the actual camera. Console/shader compilation and in-game screenshots are required after import; static asset generation alone is not gameplay validation.
