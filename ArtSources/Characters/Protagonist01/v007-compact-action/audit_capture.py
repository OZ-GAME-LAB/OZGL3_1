"""Measurements of saved-clip Unity skin captures, supplementary to visual review."""
import hashlib
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parent
WORKSPACE = ROOT.parents[3]


def distance(a, b):
    return math.dist([a[k] for k in 'xyz'], [b[k] for k in 'xyz'])


def vector(a, b):
    return [a[k] - b[k] for k in 'xyz']


def angle(a, b):
    length = math.sqrt(sum(x*x for x in a) * sum(x*x for x in b))
    return math.degrees(math.acos(max(-1, min(1, sum(x*y for x, y in zip(a, b)) / length))))


def main():
    source = ROOT / 'review-report.json'
    report = json.loads(source.read_text(encoding='utf-8'))
    checks = []
    def check(name, passed, evidence):
        checks.append({'name': name, 'passed': bool(passed), 'evidence': evidence})
    motions = {m['name']: m for m in report['motions']}
    main_motions = [m for m in motions.values() if not m['auxiliary']]
    check('13 main + 5 auxiliary + 2 offline transition captures',
          len(main_motions) == 13 and len(motions) == 18 and len(report['transitions']) == 2,
          [len(main_motions), len(motions) - len(main_motions), len(report['transitions'])])
    for m in list(motions.values()) + report['transitions']:
        rows = m['frames']
        minimum = min(min(f['leftSoleY'], f['rightSoleY']) for f in rows)
        check(m['name'] + ': no skin sole penetration', minimum >= -.001, {'minimumYMetres': minimum})
        if m['loop']:
            half = len(rows) // 2
            error = max(distance(rows[0][k], rows[half][k]) for k in
                        ['hips', 'head', 'leftHand', 'rightHand', 'leftFoot', 'rightFoot'])
            check(m['name'] + ': repeated cycle pose matches', error < .0002,
                  {'maximumPositionErrorMetres': error})
    for name in ['Idle_Fists', 'Walk_Fists', 'Run_Fists']:
        rows = motions[name]['frames']
        check(name + ': both fists closed throughout',
              all(abs(f['gripL'] - 100) < .001 and abs(f['gripR'] - 100) < .001 for f in rows),
              {'leftMin': min(f['gripL'] for f in rows), 'rightMin': min(f['gripR'] for f in rows)})
        max_pitch = max(abs(math.degrees(math.asin(max(-1, min(1, f['faceForward']['y']))))) for f in rows)
        check(name + ': forward gaze remains level', max_pitch < 3,
              {'maximumAbsoluteFacePitchDegrees': max_pitch})
    neutral = motions['Idle_Fists']['frames'][0]
    for m in main_motions:
        if m['loop']:
            continue
        end = m['frames'][-1]
        weapon = m['prop'] in ['Sword', 'Staff']
        keys = ['hips', 'head', 'leftHand', 'leftFoot', 'rightFoot'] if weapon else ['hips', 'head', 'leftHand', 'rightHand', 'leftFoot', 'rightFoot']
        error = max(distance(end[k], neutral[k]) for k in keys)
        check(m['name'] + ': body recovery returns to common stance', error < .003,
              {'maximumPositionErrorMetres': error})
        if weapon:
            error = max(distance(end[k], m['frames'][0][k]) for k in ['rightHand', 'propHilt', 'propAxis', 'propNormal'])
            check(m['name'] + ': weapon returns to its initial equipped guard', error < .003,
                  {'maximumPositionOrUnitDirectionError': error})
    sip = [f for f in motions['Potion_Drink']['frames'] if .70 <= f['time'] <= 1.16]
    mouth_error = max(distance(f['potionNozzle'], f['mouth']) for f in sip)
    check('Potion: actual mounted nozzle contacts mouth during sip', mouth_error < .005,
          {'maximumErrorMetres': mouth_error})
    pickup = motions['Pickup_Low']['frames'][15]
    y = pickup['rightHandLowest']['y']
    clearance = pickup['rightHandLowest']['x'] - max(pickup['leftToe']['x'], pickup['rightToe']['x'])
    check('Pickup: actual open-hand skin reaches ground ahead of shoes', -.001 <= y <= .015 and clearance > .03,
          {'frame': 15, 'time': pickup['time'], 'fingertipYMetres': y, 'forwardToeClearanceMetres': clearance})
    slash = [f for f in motions['Sword_Horizontal_Slash']['frames'] if .14 <= f['time'] <= .30]
    error = max(abs(f['swordTip']['y'] - f['propHilt']['y']) for f in slash)
    check('Sword slash: cutting section remains horizontal', error < .005,
          {'maximumTipHiltHeightDifferenceMetres': error})
    # The actor faces world +X; its near/right side is world -Z in this fixed side capture.
    sword = motions['Sword_Horizontal_Slash']['frames']
    outward = [f['swordTip']['z'] - f['propHilt']['z'] for f in sword[4:8]]
    check('Sword slash: one inside-to-outside cutting pass',
          outward[0] > .2 and outward[-1] < -.2 and all(b < a for a, b in zip(outward, outward[1:]))
          and min(f['propAxis']['y'] for f in sword) > -.005,
          {'cutFrames': [4, 5, 6, 7], 'tipRelativeWorldZMetres': outward})
    check('Sword recovery: blade never folds downward and returns point-up to guard',
          min(f['propAxis']['y'] for f in sword) > -.005 and sword[-1]['propAxis']['y'] > .15,
          {'minimumBladeAxisY': min(f['propAxis']['y'] for f in sword), 'guardBladeAxisY': sword[-1]['propAxis']['y']})
    cast = [f for f in motions['Staff_Cast_Forward']['frames'] if .13 <= f['time'] <= .46]
    steps = [distance(a['rightHand'], b['rightHand']) for a, b in zip(cast, cast[1:])]
    check('Staff cast: no frozen hand frames across release and recovery handoff', min(steps) > .0005,
          {'minimumAdjacentHandDisplacementMetres': min(steps), 'maximum': max(steps)})
    grip_build = json.loads((WORKSPACE / 'ArtSources/Stage01Kit/research/staff-cylinder-grip-build.json').read_text(encoding='utf-8'))
    mount = math.radians(grip_build['mountRotationEuler']['x'])
    for name in ['Sword_Horizontal_Slash', 'Staff_Cast_Forward']:
        wrist_angles, elbow_bends = [], []
        for f in motions[name]['frames']:
            palm = [f['propAxis'][k] * math.cos(mount) - f['propNormal'][k] * math.sin(mount) for k in 'xyz']
            wrist_angles.append(angle(vector(f['rightHand'], f['rightElbow']), palm))
            elbow_bends.append(180 - angle(vector(f['rightShoulder'], f['rightElbow']), vector(f['rightHand'], f['rightElbow'])))
        check(name + ': no near-right-angle palm kink or locked elbow',
              max(wrist_angles) < 65 and min(elbow_bends) > 20,
              {'maximumPalmForearmAngleDegrees': max(wrist_angles), 'minimumElbowBendDegrees': min(elbow_bends),
               'criterion': 'Project screening thresholds, not an anatomical or visual-quality certification. Bind-pose palm alignment is approximately 8.8 degrees.'})
        grip_frame = 175 if name.startswith('Sword') else 150
        check(name + ': dedicated cylinder grip held throughout',
              all(abs(f['gripR'] - grip_frame) < .001 and abs(f['gripL'] - 100) < .001 for f in motions[name]['frames']),
              {'rightGripFrame': grip_frame, 'leftGripFrame': 100})
    details = report.get('details', [])
    check('Both weapons include actual saved-clip grip close-ups',
          {m['name'] for m in details} == {'Staff_Grip_Side_25D', 'Sword_Grip_Side_25D'}
          and all(m['orthographicSize'] < .25 and m['captureRate'] == 30 for m in details),
          [{'name': m['name'], 'orthographicSize': m['orthographicSize']} for m in details])
    orb = json.loads((WORKSPACE / 'ArtSources/Stage01Kit/staff-orb-refinement.json').read_text(encoding='utf-8'))
    check('Staff orb: smaller floating translucent head; original shaft source and socket metadata preserved',
          orb['orbDiameter'] < orb['originalHeadSize']['x'] and orb['orbTriangles'] == 80
          and abs(orb['floatingGap'] - .045) < .0001 and 0 < orb['alphaAtFaceCenter'] < 1
          and orb['originalSharedMeshUnchanged'] and orb['socketWeaponMetadataUnchanged'],
          {'diameterMetres': orb['orbDiameter'], 'gapMetres': orb['floatingGap'],
           'alphaAtFaceCenter': orb['alphaAtFaceCenter'], 'transparentPasses': orb['transparentPasses']})
    removed = ['Sword_Aerial_Plunge', 'Sword_Plunge_Fall_Loop', 'Sword_Plunge_Land']
    clip_root = WORKSPACE / 'Assets/Art/Characters/Protagonist01/ChibiMotion_v007/Clips'
    check('Removed plunge clips and transition are absent from active set',
          all(n not in motions and not (clip_root / (n + '.anim')).exists() for n in removed)
          and not any(m['name'] == 'Jump_Sword_Plunge' for m in report['transitions']), removed)
    baseline = json.loads((ROOT / 'preserved-assets-baseline.json').read_text(encoding='utf-8-sig'))
    changed = [x['path'] for x in baseline if not Path(x['path']).is_file() or
               hashlib.sha256(Path(x['path']).read_bytes()).hexdigest() != x['sha256']]
    check('Preserved v005/v006 and VFX assets are byte-identical', not changed,
          {'filesCompared': len(baseline), 'changed': changed})
    unchanged = json.loads((ROOT / 'targeted-revision-baseline.json').read_text(encoding='utf-8'))
    changed = [x['path'] for x in unchanged if not Path(x['path']).is_file() or
               hashlib.sha256(Path(x['path']).read_bytes()).hexdigest() != x['sha256']]
    check('Unchanged motion clips and sword prefab are byte-identical', not changed,
          {'filesCompared': len(unchanged), 'changed': changed})
    result = {'sourceSHA256': hashlib.sha256(source.read_bytes()).hexdigest(),
              'scope': 'Numerical skin/pose and preservation measurements; visual review is separately recorded in qa-review.txt. Offline previews do not validate runtime input/physics.',
              'passed': all(x['passed'] for x in checks), 'checks': checks}
    (ROOT / 'capture-audit.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'passed': result['passed'], 'checks': len(checks),
                      'failures': [x for x in checks if not x['passed']]}, ensure_ascii=False))
    raise SystemExit(0 if result['passed'] else 1)


if __name__ == '__main__':
    main()
