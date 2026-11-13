Free Slash VFX - MaykerStudio

Source: Unity Asset Store, Free Slash VFX v1.0
https://assetstore.unity.com/packages/vfx/shaders/free-slash-vfx-309498
License: Standard Unity Asset Store EULA
https://unity.com/legal/as-terms

Included examples
- Prefabs/Slash VFX.prefab: original base slash.
- Prefabs/Slash Eletric VFX.prefab: original electric variation (vendor spelling).
- Prefabs/Slash Fire VFX.prefab: original fire variation.
- Prefabs/Slash Water VFX.prefab: original water variation.
- Prefabs/Slash Earth VFX.prefab: original earth variation.
- Prefabs/Impact.prefab: original impact flash/sparks.
- Native Shader Graphs, materials, textures and meshes are retained for reuse.

Usage
These are URP particle effects. Play the root ParticleSystem with children enabled.
The original slash is authored at human scale; make a game-specific prefab variant
and adjust particle Scaling Mode, scale, lifetime and color for the chibi character.
Custom particle vertex streams and Custom1 curves drive the original shader motion;
retain those streams when making variants. Impact is a visual effect only.
Use this project's existing URP pipeline; no project rendering settings are replaced.

Import notes
- Original asset GUIDs and the six original prefab files are preserved.
- Water depends on the vendor demo Sphere.fbx. It is retained as Model/WaterSphere.fbx
  with its original GUID; no demo scene or environment is required.
- Nine Flying Particles materials contained an obsolete _EmissionTex texture slot
  pointing to a texture absent from the official package. Trail.shadergraph does not
  declare or read that property. Only that missing, unused texture reference was
  cleared. Active textures, shaders, colors and particle timing are unchanged.
- Excluded demo scenes, camera/player/example scripts, bundled duplicate TextMesh Pro,
  pipeline configuration, Packages/manifest.json and unrelated katana demo model.
- Excluded projectile/multi-slash demo prefabs and their unused decal material/shader.
  The multi-slash example and decal shader include missing source references; these
  are not required by the six retained effects.
- The original Rock model attribution is retained in Model/Rock.

Gameplay-specific sword variants and their runtime timing belong to
Assets/Art/Effects/SwordSlash and SwordAttackVfx, not this vendor source directory.
