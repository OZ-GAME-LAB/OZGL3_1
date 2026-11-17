Free Quick Effects Vol.1 (URP) - Gabriel Aguiar Productions

Official package: FreeQuickEffectsVol1_2022_URP_v1.0.unitypackage
Creator download: https://www.patreon.com/GabrielAguiarProd/posts/free-quick-vol-1-117366219
Unity Asset Store: https://assetstore.unity.com/packages/vfx/particles/free-quick-effects-vol-1-304424
License shown on the official Asset Store listing: Standard Unity Asset Store EULA
License reference: https://unity.com/legal/as-terms
Refer to the official listing and creator distribution terms for usage conditions.

Usage
- Drag any of the 30 prefabs from Prefabs into a scene and play its ParticleSystem.
- Projectiles contain visual particles; movement and gameplay collision are supplied by the game.
- Settings/FE__GlobalVolumeProfile.asset is the original optional Bloom profile.
- That original profile also enables Vignette; copy only its Bloom settings if a scene should keep its current framing.
- Use this project's existing URP pipeline; no global render settings were replaced.
- The source prefabs enable Play On Awake and some effects loop. For one-shot skills, stop/clear the particles and manage their lifetime in the owning skill or effect pool.

Integration notes
- Original asset GUIDs are preserved. The root location is Assets/3.ThirdParty/GabrielAguiarProductions.
- The vendor demo environment and old pipeline settings were excluded because their public package contains missing environment dependencies and outdated XR references. Obtain the untouched demo from the official download above.
- vfx_Lightning_02 contains a trail-only renderer (renderMode=None). Its unused, missing particle-material reference was cleared; the real trail material, particle settings, textures, colors and timing are unchanged.
- All other retained vendor files match the official package before Unity's normal importer upgrades.
