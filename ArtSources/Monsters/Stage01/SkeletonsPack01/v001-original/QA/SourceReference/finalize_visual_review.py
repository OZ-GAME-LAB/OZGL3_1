"""Read delivered media and bind independent visual review to current files."""
from pathlib import Path
import hashlib
import json
from datetime import datetime, timezone
from PIL import Image

PROJECT = Path('C:/Users/me/Desktop/OZGameLab3/OZGL3_1')
BASE = PROJECT / 'ArtSources/Monsters/Stage01/SkeletonsPack01/v001-original'
QA = BASE / 'QA'
MEDIA = Path('C:/Users/me/Desktop/OZGL3_ArtReview/Monsters/Stage01/SkeletonsPack01/v001-original')

def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

manifest = read(MEDIA / 'review-media-manifest.json')
main = read(QA / 'main-project-import-validation.json')
unity = read(QA / 'unity-review-report.json')
fidelity = read(QA / 'source-motion-fidelity.json')
checks = []
errors = []
for section in ('clips', 'connections'):
    for item in manifest[section]:
        for gif in item['gifs']:
            file = MEDIA / gif['path']
            actual_sha = sha(file)
            durations = []
            with Image.open(file) as im:
                for i in range(im.n_frames):
                    im.seek(i)
                    durations.append(im.info.get('duration', 0))
            actual = sum(durations) / 1000
            expected = gif['source_cycle_seconds'] / gif['speed'] + gif['recovery_hold_seconds']
            error = abs(actual - expected) * 1000
            good = (actual_sha == gif['sha256'] and durations == gif['encoded_delays_ms']
                    and len(durations) == gif['encoded_frames'] and error <= 5.01)
            check = {'path': gif['path'], 'sha256': actual_sha, 'frames': len(durations),
                     'encoded_seconds': actual, 'expected_seconds': expected,
                     'timing_error_ms': error, 'pass': good}
            checks.append(check)
            if not good:
                errors.append(check)

source_hashes = []
for role in main['roles']:
    model = PROJECT / role['sourceFBX']
    prefab = PROJECT / role['prefab']
    actual = sha(model)
    if actual != role['sourceSHA256']:
        errors.append({'role': role['role'], 'error': 'Published FBX changed after main-project validation'})
    source_hashes.append({'role': role['role'], 'source_fbx': role['sourceFBX'],
                          'source_fbx_sha256': actual, 'prefab': role['prefab'],
                          'prefab_sha256': sha(prefab)})

timing = {'created_utc': datetime.now(timezone.utc).isoformat(),
          'pass': not errors, 'gif_count': len(checks),
          'maximum_timing_error_ms': max(c['timing_error_ms'] for c in checks),
          'checks': checks, 'errors': errors,
          'method': 'Read every encoded GIF frame duration with Pillow; compare file SHA256 and frame delays with the delivered manifest. This is timing/persistence verification, not direct real-time playback observation.'}
(QA / 'independent-media-verification.json').write_text(json.dumps(timing, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

assert not errors, errors
assert len(checks) == 94
assert main['success'] and not main.get('errors')
assert unity['success']
assert fidelity['raw_source_hashes_unchanged']
side_count = sum(len(list(p.glob('*.png'))) for p in (MEDIA / 'Frames').glob('*/*/Side'))
assert side_count == 1053, side_count
report = read(QA / 'final-visual-review.json')
report['verification_metadata'] = {
    'created_utc': timing['created_utc'],
    'unity_review_report_sha256': sha(QA / 'unity-review-report.json'),
    'media_manifest_sha256': sha(MEDIA / 'review-media-manifest.json'),
    'main_project_validation_sha256': sha(QA / 'main-project-import-validation.json'),
    'source_motion_fidelity_sha256': sha(QA / 'source-motion-fidelity.json'),
    'media_verification_sha256': sha(QA / 'independent-media-verification.json'),
    'main_project_readonly_validation_success': main['success'],
    'unity_render_success': unity['success'],
    'protected_preexisting_files_checked': main['protectedFilesChecked'],
    'source_hashes_unchanged': fidelity['raw_source_hashes_unchanged'],
    'fidelity_scope': fidelity['scope'],
    'side_pngs_reviewed': side_count,
    'action_auxiliary_pngs_reviewed': 963,
    'connections_reviewed': 27,
    'connection_blend_frames_reviewed': 702,
    'connection_raw_endpoints_reviewed': 54,
    'gif_actual_file_count': len(checks),
    'gif_hashes_and_delays_match_manifest': not errors,
    'maximum_gif_timing_error_ms': timing['maximum_timing_error_ms'],
    'native_model_and_prefab_files': source_hashes,
    'test_environment_note': 'Visual PNGs are actual skinned-model renders in the isolated Unity review environment. Main-project validation reopened/read the delivered assets structurally; it did not exercise game logic or render the running game.'
}
report['evidence_files'].append('QA/independent-media-verification.json')
(QA / 'final-visual-review.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
with (QA / 'final-visual-review.txt').open('a', encoding='utf-8') as f:
    f.write('\n최종 파일 확인: 실제 GIF 94개의 SHA256, 모든 프레임 지연시간이 배포 manifest와 일치합니다. 원속도/0.5배속과 원샷 끝 500ms 검수 정지 시간을 포함한 최대 총시간 오차는 %.4fms입니다. 이는 인코딩 검사이며 실시간 재생 관찰을 뜻하지 않습니다.\n' % timing['maximum_timing_error_ms'])
    f.write('측면 PNG 1053장, 공격 보조시점 PNG 963장, 연결 27개의 702 블렌드 프레임과 54 원본 끝/시작 프레임을 검수했습니다. 메인 프로젝트 검증은 저장된 실제 에셋의 읽기/구조 검증이며 인게임 실행 검증은 아닙니다.\n')
print(json.dumps({'pass': timing['pass'], 'gifs': len(checks), 'max_error_ms': timing['maximum_timing_error_ms'], 'side_pngs': side_count, 'reports': ['QA/final-visual-review.json', 'QA/final-visual-review.txt', 'QA/independent-media-verification.json']}, ensure_ascii=False))
