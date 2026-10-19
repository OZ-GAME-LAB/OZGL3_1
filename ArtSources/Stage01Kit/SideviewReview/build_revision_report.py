"""Summarize saved Unity validation and actual sideview render artifacts."""
from datetime import datetime, timezone
from pathlib import Path
import json
from PIL import Image

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parents[2]


def read(relative):
    return json.loads((PROJECT / relative).read_text(encoding='utf-8'))


motion = json.loads((ROOT / 'motion-review.json').read_text(encoding='utf-8'))
environment = json.loads((ROOT / 'environment-review.json').read_text(encoding='utf-8'))
core = read('ArtSources/Characters/Protagonist01/v002-rigged/play-validation.json')
extra = read('ArtSources/Characters/Protagonist01/v003-extended/validation.json')
kit = read('ArtSources/Stage01Kit/unity-kit-validation.json')
staff = read('ArtSources/Stage01Kit/staff-refinement.json')
contact = json.loads((ROOT / 'contact-diagnostics.json').read_text(encoding='utf-8'))
assert core['success'] and extra['success'] and extra['playPassed'] and kit['success']
assert len(motion['motions']) == 28 and all(m['humanoid'] for m in motion['motions'])

gifs = []
for item in motion['motions']:
    path = ROOT / 'GIF' / (item['name'] + '.gif')
    with Image.open(path) as image:
        encoded_frames = image.n_frames
        assert 2 <= encoded_frames <= item['frames'], item['name']
        duration = 0
        for index in range(encoded_frames):
            image.seek(index)
            duration += image.info['duration']
        expected_duration = 60 * item['frames'] + 10 * (item['frames'] // 4)
        assert duration == expected_duration, (item['name'], duration, expected_duration)
        gifs.append({'name': item['name'], 'sourceFrames': item['frames'], 'encodedFrames': encoded_frames,
                     'durationMilliseconds': duration, 'bytes': path.stat().st_size})
assert len(environment['shots']) == 5
for shot in environment['shots']:
    with Image.open(PROJECT / shot['image']) as image:
        assert image.size == (1600, 900)

report = {
    'success': True,
    'generatedUtc': datetime.now(timezone.utc).isoformat(),
    'unity': motion['unity'],
    'additionalMeshyCreditsThisRevision': 0,
    'camera': motion['camera'],
    'renderMethod': motion['method'],
    'canonicalActions': 15,
    'directionAndEquipmentReviewGifs': len(gifs),
    'environmentScreenshots': len(environment['shots']),
    'gifs': gifs,
    'staff': staff,
    'validation': {
        'corePlayPassed': core['success'], 'corePlayChecks': len(core['checks']),
        'extendedEditorPassed': extra['editorPassed'], 'extendedEditorChecks': len(extra['editorChecks']),
        'extendedPlayPassed': extra['playPassed'], 'extendedPlayChecks': len(extra['playChecks']),
        'extendedErrors': extra['errors'],
        'attackContactExpectedSeconds': extra['expectedAttackContactSeconds'],
        'attackContactObservedSeconds': extra['actualAttackContactSeconds'],
        'kitPassed': kit['success'], 'environmentMaterials': kit['sharedEnvironmentMaterials'],
        'shaderErrors': kit['shaderErrors'], 'platformRoofsValidated': kit['platformRoofsValidated'],
    },
    'grounding': {
        'minimumGroundedSkinClearanceMetres': min(m['minimumSoleClearance'] for m in motion['motions']),
        'perMotion': {m['name']: m['minimumSoleClearance'] for m in motion['motions']},
        'runtimeMethod': '40 sole landmarks, 234 whole-body landmarks only when dead; no runtime BakeMesh. Physics root/collider unchanged.',
    },
    'attackContactMeasurements': [
        {key: item[key] for key in ('configuration', 'state', 'sourceSeconds', 'savedAttackContactNormalized',
                                   'maximumRightHandForwardNormalized', 'maximumTipForwardNormalized')}
        for item in contact['motions']
    ],
    'scope': 'Saved Unity art/player prefabs and preview scenes. Motion GIFs are sampled Mecanim poses, not an input-driven video. Input-driven Play Mode is validated separately. Throw trajectory is art preview only; item effects and weapon combat implementations remain game callbacks.',
    'gallery': 'ArtSources/Stage01Kit/SideviewReview/index.html',
    'previewScene': kit['scene'],
}
(ROOT / 'revision-validation.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print('PASS:', len(gifs), 'GIFs,', len(environment['shots']), 'environment shots,',
      len(core['checks']) + len(extra['playChecks']), 'Play Mode checks; additional Meshy credits: 0')
