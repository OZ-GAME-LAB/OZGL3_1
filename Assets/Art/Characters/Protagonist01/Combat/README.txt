PROTAGONIST COMBAT — SELECTED FINAL ATTACKS

Character prefab: Prefabs/Protagonist_Combat.prefab
Editable source: ArtSources/Characters/Protagonist01/Final/Combat/Protagonist_Combat.blend
Weapon prefab: Prefabs/Longsword_KayKitSocket.prefab

Exactly three animation clips are retained:
Longsword_Attack_Chop — 0.700 s
Longsword_Attack_Slice — 0.600 s
Longsword_Attack_Stab — 0.700 s

These are the selected final attacks, copied byte-for-byte from the validated delivery. No attack motion, rig, mesh, material, texture or prefab content was rebaked or altered during consolidation. Folder and protagonist prefab filename changed with their .meta GUIDs preserved. The five unselected KayKit locomotion/idle clips were deleted after checking that they had no incoming Unity asset references.

Use this prefab for these Generic transform clips. Its internal hierarchy names (including Protagonist_KayKit_v018 and Hero_KayKit_Rig) are deliberately retained so existing clip bindings continue to work. The folder name does not determine animation binding paths.

The selected previous movement and utility set remains in ../ChibiMotion_v007: 15 clips, comprising 10 core motions and five jump phases. That set uses the previous 24-bone Visual/Armature/Hips hierarchy and its own validated meshes/prefabs. This combat set uses the 23-bone KayKit hierarchy. These are two preserved asset sets; they cannot directly play on one existing skinned rig merely by changing clip filenames or an Animator reference. A separate retarget/bake or rig-switch integration would be required for one runtime character. No such integration or gameplay/controller change was performed in this cleanup.

The combat prefab retains an inactive WeaponRoot child in its saved default pose. Each attack activates and animates that child. To replace it with the separate weapon prefab, preserve the WeaponRoot name and exact parent/initial local transform recorded in Weapon_Attachment.json. Do not add a duplicate weapon under a different name. The FBX contains the static rig; motion is in the three .anim assets.

URP/Lit materials, texture, source attribution and original CC0 license texts are included. The three attacks were previously validated on saved/reloaded Unity prefabs at every 60 Hz sample (123 transform samples total) and 51 actual skinned-mesh poses. Consolidation preserves those tested bytes/GUIDs; the receipt is ArtSources/Characters/Protagonist01/Final/Validation/combat-delivery-receipt.json. This is asset fidelity evidence, not a claim of gameplay integration.

Existing runtime player assets, all environmental props/weapons (including the staff), monsters, gameplay code, controllers, scenes and VFX are outside this package and were not changed by combat consolidation.
