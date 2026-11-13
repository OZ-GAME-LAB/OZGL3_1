Sword basic attack VFX

Ready to use
- Assets/2.Prefab/Player/Player_Chibi.prefab contains SwordAttackVfx with explicit references to its real sword and BladeBase/BladeTip markers.
- Equipping Sword and entering Attack_0, Attack_1 or Attack_2 plays the matching effect automatically. Existing SwordComboAttack damage and animation timings are unchanged.
- The three steps use the current Animator normalized time, so ComboSpeed changes also change effect playback speed.

Assets
- Prefabs/SwordSlash_Sweep.prefab: game-sized variant of MaykerStudio's real Slash VFX. Used for chop and horizontal slash with individual pose/timing settings.
- Prefabs/SwordSlash_Thrust.prefab: short Mayker Impact variant, attached to the actual blade tip.
- Materials/Mayker_* retain the vendor shaders and textures with a cool white/blue palette.
- BladeRibbon material/shader and SwordAttackVfx add a smooth, short trail sampled from the actual blade, including low-frame-rate arc interpolation.
- The source prefabs remain under Assets/3.ThirdParty/MaykerStudio/FreeSlashVFX. Their source and license information are in that folder's README.txt.

Integration details
- The vendor sweep's separately staged hit flash and smoke are disabled in the sweep variant. This keeps air swings attached to the blade instead of displaying an unrelated impact point.
- Game variants use Hierarchy particle scaling, shorter lifetimes, fewer flying particles, and pooled playback. Vendor source files are kept separate.
- Visuals clear on attack cancellation, weapon changes, death, hit/dash interruption, disable, teleport and abrupt facing changes. A Skill state reusing a sword clip does not trigger basic-attack VFX.
- Keep Manual Sampling disabled for normal gameplay. It is only for Timeline/Playable or deterministic review tooling that explicitly calls Sample after posing the character.
- Tune the three Steps on the player component for timing, color, burst pose/scale and tail length. Follow Blade tracks the active stroke and freezes its final pose for the short fade.
- The existing URP camera and Bloom settings are used; no global render settings are replaced.
