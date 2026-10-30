"""Read-only delivery and protected-asset checks, run after the final Unity bake."""
from pathlib import Path
import hashlib, json, re, subprocess

HERE = Path(__file__).resolve().parent
ROOT = next(p for p in HERE.parents if (p / 'ProjectSettings').is_dir())
FINAL = HERE.parents[1]
NATIVE = 'Assets/Art/Characters/Protagonist01/ChibiMotion_v007'
NAMES = ['Longsword_Attack_Chop', 'Longsword_Attack_Slice', 'Longsword_Attack_Stab',
         'Death_Fall', 'Ladder_Climb_Loop']

def sha(data):
    return hashlib.sha256(data).hexdigest()

def saved_head(path):
    return subprocess.run(['git', 'show', 'HEAD:' + path], cwd=ROOT, check=True, capture_output=True).stdout

legacy = json.loads((FINAL / 'LegacyMotions/manifest.json').read_text(encoding='utf8'))
protected = [c['unityClip'] for c in legacy['clips']]
protected += ['Assets/2.Prefab/Player/Player_Chibi.prefab', 'Assets/Anim/Player_Chibi.controller',
              'Assets/Anim/Clips/Climb.anim', 'Assets/Anim/Clips/Climbing Ladder.fbx']
protected += ['Assets/Art/Characters/Protagonist01/Combat/Clips/' + n + '.anim' for n in NAMES[:3]]
checks = []
for path in protected:
    before, now = saved_head(path), (ROOT / path).read_bytes()
    # Git working trees can use CRLF. Compare normalized text where applicable.
    normalize = path.endswith(('.anim', '.prefab', '.controller'))
    unchanged = (before.replace(b'\r\n', b'\n') == now.replace(b'\r\n', b'\n')) if normalize else before == now
    checks.append(dict(path=path, headSHA256=sha(before), workingSHA256=sha(now), unchanged=unchanged))
assert all(c['unchanged'] for c in checks), 'A protected source or gameplay file changed'

audit = json.loads((HERE / 'QA/saved-clip-audit.json').read_text(encoding='utf8'))
live = json.loads((HERE / 'Additional/animator-compatibility.json').read_text(encoding='utf8'))
assert live['passed'] and live['savedPlayerUnchanged'] and live['savedControllerUnchanged']
assert len(audit['clips']) == len(NAMES) == len(live['clips'])
rows = []
for name in NAMES:
    path = NATIVE + '/Clips/' + name + '.anim'
    meta = (ROOT / (path + '.meta')).read_text(encoding='utf8')
    guid = re.search(r'^guid: (\w+)', meta, re.M).group(1)
    row = next(c for c in audit['clips'] if c['name'] == name)
    assert row['missingBindings'] == 0 and row['nonFiniteVertices'] == 0
    rows.append(dict(name=name, path=path, guid=guid, sha256=sha((ROOT / path).read_bytes()),
                     seconds=row['duration'], nativeRig='Chibi24', loop=name=='Ladder_Climb_Loop',
                     samples120Hz=row['samples'], liveAnimatorPassed=True))
assert len({r['guid'] for r in rows}) == len(NAMES)
runtime_diff = subprocess.run(['git', 'diff', '--name-only', 'HEAD', '--', 'Assets/1.Script', 'Assets/2.Prefab',
                              'Assets/Anim', 'ProjectSettings'], cwd=ROOT, check=True, capture_output=True, text=True).stdout.splitlines()
assert not runtime_diff, runtime_diff
result = dict(baseCommit=subprocess.run(['git', 'rev-parse', 'HEAD'], cwd=ROOT, check=True, capture_output=True, text=True).stdout.strip(),
              clips=rows, prop=NATIVE+'/Prefabs/Longsword_Chibi_SocketProp.prefab',
              oldSelectedClipsUnchanged=True, runtimeControllerAndPlayerUnchanged=True,
              protectedFiles=checks, runtimeDiff=runtime_diff, gifStorage='~/Desktop/OZGL3_ArtReview',
              scope='Native clip assets and art-only Editor authoring/review tools; gameplay state/controller timing not integrated or changed.')
(HERE / 'delivery-manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf8')
print('DELIVERY_VERIFIED', len(rows), 'new clips;', len(checks), 'protected files unchanged')
