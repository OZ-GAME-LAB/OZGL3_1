Combat review 2026-10-27

Scope: the accepted Chop, Slice and Stab clips, their native KayKit23 rig, body, two mitten meshes and rigid longsword. All 123 baked poses were rendered from side, front and opposite views. All four evaluated meshes were sampled at 120 Hz (243 samples including endpoints). The complete cycle contact sheets were visually reviewed, then elbow extremes were inspected in enlarged front, opposite and quarter views.

Finding and correction
Elbow sleeve pinching was visible at close range. A broad weight smoothing candidate was rejected because it increased edge stretching. The final correction optimizes only the upperarm/lowerarm weight share near each elbow over all 123 poses, with a 15 mm surface displacement limit and a 0.15 weight-change limit. Actual maximum surface displacement is 13.915 mm; 275 native vertex weight records change, including UV-seam duplicates. Rest geometry, UVs, skeleton and all six action curves are identical by signature. Sword/mitten evaluated metrics and joint metrics remain exactly equal across all 243 before/after samples.

The correction is deliberately small. It does not claim perfect anatomical volume preservation, zero occlusion at every camera angle, or a redesign of the accepted motion. Low-poly sleeve folds and momentary occlusion during attacks remain stylistic limitations. The two native hero rigs are still separate and were not retargeted or integrated into gameplay.

Evidence
- combat-numeric-audit.json: pre-correction baseline, original blend SHA aa9726296cfcbcc4f804017870879026b6fb82e4f4b938daf85f2a8f69a09087.
- combat-numeric-final.json: final candidate evaluated values; source publication preserves these values.
- source-retouch-receipt.json: current source hash, unchanged rig/mesh/action signature and packed/external texture verification.
- skin-candidate-comparison.json: rest-relative edge metrics over all 123 poses before/after; global extreme statistics are diagnostics, not automatic visual pass criteria.
- Final_*_AllFrames.jpg: final full cycles, all three camera views, visually inspected.
- Detail_* and unprefixed AllFrames sheets: pre-correction visual baseline.
- ../../../Combat/SkinWeights/ElbowWeightPatch.json: per-vertex before/after values; Body_ElbowWeights.json is the complete current named-bone transfer map.
- ../Legacy: independent actual Unity prefab, imported mesh and AnimationClip review, including targeted final Combat validation.

audit_combat.py can regenerate current evaluated geometry measurements and textured workbench captures without saving the blend. Raw PNG captures are written under ignored Logs/AssetAudit20261027, not into the delivery folder. The original cleanup hashes in Final/Validation and Combat/Validation are historical and predate this correction.
