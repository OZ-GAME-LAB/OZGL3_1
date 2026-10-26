Project M Stage01 Melee01 art sample

One proposed melee skeleton. Original model, skinning, native rig and animation:
KayKit Skeletons 1.0 by Kay Lousberg, CC0. See Licenses/KayKit_CC0.txt.
Source provenance and asset comparison are in ArtSources/Monsters/Stage01/Melee01/v001-native-motion/Research.

Prefab: Prefabs/Stage01_Melee01.prefab
Native Generic Avatar. Animator is enabled; no runtime controller is assigned.
Root motion is disabled. Model's forward axis is +Z; use an OUTER orientation
parent rotated Y90 degrees to face +X in the game's side-view plane.
Do not rename the rig hierarchy without rebinding clips.

Clips: Idle4.266667s(loop), Move0.8s(loop/in-place), Attack1.066667s, Hit0.666667s.
The original joint poses/timing are retained. Move and Attack have constant
vertical floor offsets of0.032263592m and0.005913736m respectively. The native
blade's local shaft axis is converted to the handslot with a fixed X-90 rotation.
The imported editable .blend contains baked keys, not the creator's paid IK control rig.

Review/Stage01_Melee01_Review.unity is an isolated static inspection scene with
camera and floor. Actual animated four-view GIFs and preview connections are at
ArtSources/Monsters/Stage01/Melee01/v001-native-motion/QA/UnityRendered/index.html.
The review scene deliberately lowers its opaque floor5cm; amber markers showY0.
Live enemy AI, damage events, colliders, death behavior, gameplay travel speed,
Animator transitions and VFX are not wired by this art delivery.
