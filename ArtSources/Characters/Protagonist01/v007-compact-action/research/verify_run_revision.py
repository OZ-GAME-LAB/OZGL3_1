"""Verify the narrow Run-only revision against its original immutable inventory."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[5]
HERE = Path(__file__).resolve().parents[1]
def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))
def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()
def distance(a, b):
    if isinstance(a, dict):
        return max(distance(a[k], b[k]) for k in a)
    return abs(a-b)

baseline = read(HERE/'run-revision-before-sha.json')
report = read(HERE/'review-report.json')
original = read(HERE/'CaptureRuns'/baseline['sourceSession']/'review-report.json')
run_info = read(HERE/'run-only-review.json')
removed = {'Punch_Right_Jab', 'Sword_Horizontal_Slash', 'Staff_Cast_Forward'}
names = {m['name'] for m in report['motions']}
assert len(names) == 15 and not names & removed
assert [m['name'] for m in report['transitions']] == ['Move_Dash_Recover']
assert not report['details']
assert sha(HERE/'review-report.json') == run_info['afterReportSHA256']
protected = []
documentation = []
doc_record_path = HERE/'research/run-revision-readme-change.json'
doc_record = read(doc_record_path) if doc_record_path.exists() else None
for item in baseline['files']:
    path = ROOT/item['path']
    if item['retire']:
        assert not path.exists(), path
    elif not item['allowChange']:
        if doc_record and item['path'] == doc_record['path']:
            assert item['path'] == 'Assets/Art/Characters/Protagonist01/ChibiMotion_v007/README_ko.txt'
            assert item['sha256'] == doc_record['beforeSHA256']
            assert sha(path) == doc_record['afterSHA256']
            documentation.append(doc_record)
            continue
        assert path.is_file() and sha(path) == item['sha256'], path
        protected.append(item['path'])
assert len(protected)+len(documentation) == 289
before_run = next(m for m in original['motions'] if m['name'] == 'Run_Fists')
after_run = next(m for m in report['motions'] if m['name'] == 'Run_Fists')
fields = ['time','hips','head','faceForward','leftFoot','rightFoot',
          'leftSoleY','rightSoleY','leftToe','rightToe','rightShoulder',
          'leftShoulder','gripL','gripR']
assert len(before_run['frames']) == len(after_run['frames']) == 38
differences = {k:max(distance(a[k],b[k]) for a,b in zip(before_run['frames'],after_run['frames'])) for k in fields}
assert all(value == 0 for value in differences.values()), differences
other = sorted(names-{'Run_Fists'})
for name in other:
    old = next(m for m in original['motions'] if m['name'] == name)
    new = next(m for m in report['motions'] if m['name'] == name)
    assert old == new, name
result = {
    'reportSHA256':sha(HERE/'review-report.json'),
    'runClipSHA256':after_run['sourceClipSHA256'],
    'sourceSession':Path(run_info['geometryDirectory']).parent.name,
    'savedAssetsChecked':len(protected),'savedAssetsUnchanged':True,'failures':[],
    'separatelyRecordedDocumentationChanges':documentation,
    'runFramesCompared':38,
    'unmodifiedRunBodyFieldsMaximumAbsoluteDifference':differences,
    'approved14ClipNames':other,
    'activeMotionCount':15,'activeTransitionCount':1,'details':0,
    'documentationNote':'289 saved files were unchanged at bake. Final delivery preserves 288 exactly and separately records one README inventory correction.'
}
(HERE/'run-revision-preservation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'preservedAssets':len(protected),'otherClips':len(other),'unmodifiedRunBody':all(v==0 for v in differences.values()),'runClipSHA256':after_run['sourceClipSHA256']}))
