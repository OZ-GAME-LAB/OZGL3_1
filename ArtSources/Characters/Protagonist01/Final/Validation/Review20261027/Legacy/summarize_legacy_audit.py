"""Aggregate numerical evidence and the completed, explicitly bounded visual review."""
from pathlib import Path
import hashlib, json

HERE = Path(__file__).resolve().parent
ROOT = next(p for p in HERE.parents if (p / 'Assets').is_dir())
data = json.loads((HERE / 'hero-audit.json').read_text())
clips = [c for c in data['clips'] if c['set'] == 'Legacy']
frames = [f for c in clips for f in c['frames']]
contacts = HERE / 'ContactsBaseline'
reviewed = sorted(list(contacts.glob('Legacy_MotionReview_*_Side_*.jpg')) +
                  list(contacts.glob('Legacy_MotionReview_*_Opposite_*.jpg')) +
                  list(contacts.glob('Static_*_Views_*.jpg')))
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
clip_hashes = {c['clipPath'].replace('\\', '/'): c['clipSHA256'] for c in clips}
prefab_hashes = {c['prefab'].replace('\\', '/'): c['prefabSHA256'] for c in clips}
preserved = [{'path': p, 'auditSHA256': h, 'currentSHA256': sha(ROOT / p),
              'unchanged': h == sha(ROOT / p)} for p, h in {**clip_hashes, **prefab_hashes}.items()]
assert all(p['unchanged'] for p in preserved)
summary = {
    'date': '2026-10-27', 'reviewer': 'kaykit_delivery',
    'result': 'No new blocking deformation or binding defect found in the retained Legacy clips/meshes within the inspected scope. Approved Legacy motion and model assets were preserved.',
    'actualUnityVersion': data['unityVersion'],
    'samplingMethod': 'Actual main-project Unity prefab instances; saved AnimationClip.SampleAnimation at 60 Hz including endpoint; every skinned world vertex BakeMesh and finite local transform checks. No Animator/controller or runtime integration.',
    'legacyClipCount': len(clip_hashes), 'legacyPrefabVariants': len(prefab_hashes),
    'legacyCombinations': len(clips), 'legacyPoseSamples': sum(c['samples'] for c in clips),
    'missingBindingTargets': sum(c['missingBindingTargets'] for c in clips),
    'nonFiniteValues': sum(f['nonFiniteValues'] for f in frames),
    'zeroScaleAxes': sum(f['zeroScaleAxes'] for f in frames),
    'collapsedTrianglesAtAreaBelow1eMinus10SquareMetres': sum(f['collapsedTriangles'] for f in frames),
    'maximumLoopEndpointSurfaceDifferenceMetres': max(c['loopSeamVertexDistance'] for c in clips if c['loop']),
    'visualScope': {
        'primarySide': 'Every 30 Hz rendered frame of each of the 15 MotionReview clips, in chronological contact sheets.',
        'oppositeSide': 'Nine equally spaced poses including endpoints for every MotionReview clip.',
        'secondaryReviewer': 'independent-secondary-visual-review.json contains the separate review of 30 Front/ThreeQuarter sheets and 14 SwordGrip/StaffGrip utility sheets.',
        'staticModels': 'Four views each of v006 imported rest mesh, v006 Rig prefab, v006 Fists source mesh on rig instance, and existing Runtime reference. The raw imported FBX rest mesh is gray; the retained prefab supplies its intended textured material.',
        'observations': [
            'No detached/reversed wrist or elbow, large mesh tear, or Run fist buried in torso observed in reviewed game-scale frames.',
            'Potion hand-to-face approach, Throw preparation near the cheek, and Pickup hand near the shoes are consistent with the authored intent.',
            'Idle is intentionally restrained. Jump phase clips are small transition/loop components; their static-ground preview is not a gameplay jump simulation.',
            'Compact fast Throw/Hit/Dash frame-to-frame rotation measurements are not by themselves defects; corresponding visible sequences were inspected.',
            'No Legacy changes were justified by the current evidence.'
        ],
        'limits': [
            'These are asset-space sampled poses and review-camera renders, not a playtested combat/controller integration.',
            'No potion, thrown object, or environment interaction prop was attached to Legacy utility clips; actual object contact is not verified.',
            'Front/ThreeQuarter/Opposite and grip-variant renders use selected phases rather than every 60 Hz frame; all frames received numerical mesh validation.',
            'A finite sampling and camera review cannot establish absence of every possible subframe/self-intersection or future transition artifact.',
            'Runtime reference was checked as a static dependency; its legacy controller motions were not regenerated or exhaustively replayed.'
        ]
    },
    'reviewedContactSheets': [{'path': str(p.relative_to(ROOT)).replace('\\', '/'), 'sha256': sha(p)} for p in reviewed],
    'preservedSelectedAssets': preserved,
    'authoringMaintenance': 'authoring-path-repairs.json: four output path replacements across ChibiMotionSet.cs, ChibiCapHandBuilder.cs, ChibiGaitSourceMigration.cs. No builder was run and no motion-generation logic changed.',
    'combatCorrectionSeparateReceipt': '../Combat/unity-skin-regression.json'
}
(HERE / 'final-audit-summary.json').write_text(json.dumps(summary, indent=2))
print(json.dumps({k: summary[k] for k in ['legacyClipCount', 'legacyPrefabVariants', 'legacyCombinations', 'legacyPoseSamples', 'missingBindingTargets', 'nonFiniteValues', 'zeroScaleAxes', 'collapsedTrianglesAtAreaBelow1eMinus10SquareMetres', 'maximumLoopEndpointSurfaceDifferenceMetres']}, indent=2))
print('Reviewed contact sheets:', len(reviewed))
