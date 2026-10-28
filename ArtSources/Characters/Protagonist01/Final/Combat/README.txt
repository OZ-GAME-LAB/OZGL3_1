Approved protagonist combat source

Protagonist_Combat.blend is self-contained. The character texture is packed and an identical JPEG is included at Textures/Image_0.jpg. No previous version folder is needed.

Selected clips (60 fps baked keys):
- Longsword_Attack_Chop: frames 1-43, 0.7 seconds.
- Longsword_Attack_Slice: frames 1-37, 0.6 seconds.
- Longsword_Attack_Stab: frames 1-43, 0.7 seconds.

Each clip has two editable actions: Hero_KayKit_Rig and WeaponRoot. Bind the two named actions together using clips.json or the scene clips_json property. The scene opens at Chop frame 1. No idle or movement action is included.

The source retains the 13 export objects and review cameras/lights/floor. Unused models, original library actions, prototype objects, and stale version metadata have been removed. The six retained actions and rest geometry are unchanged. Validation/source-integrity.json and canonical-readcheck.json are historical cleanup snapshots from before the subsequent skin correction.

On 2026-10-27, bounded upperarm/lowerarm weight redistribution softened elbow sleeve pinching. SkinWeights/ contains the exact correction. No bone, rest vertex, UV, animation key, weapon or mitten motion changed. The latest source hash and invariant checks are in ../Validation/Review20261027/Combat/source-retouch-receipt.json. Unity uses the Combat prefab's corrected body mesh override; the original FBX is retained as its imported rig/mesh dependency.

KayKit license texts and creator URLs are in Licenses/ and Provenance/. The source rig and motion came from the CC0 KayKit Character Animations and Skeletons packages.

Final standalone side GIFs are in ../Review/Combat/ relative to this folder. No game runtime/controller or VFX work is included.
