"""Read-only clip/prefab audit after the final Unity capture is complete.

Require no mesh object-reference curves in all 18 shared clips. Verify the three
fixed unarmed/sword/staff mesh prefabs separately. HEAD is evidence only; this
script never restores assets or changes baselines.
"""
from __future__ import annotations

import argparse
import copy
import datetime as dt
import hashlib
import json
import math
from pathlib import Path
import re
import subprocess
import sys

import yaml


ROOT = Path(__file__).resolve().parents[5]
RESEARCH = Path(__file__).resolve().parent
CLIPS = ROOT / "Assets/Art/Characters/Protagonist01/ChibiMotion_v007/Clips"
GAIT = {"Walk_Fists", "Run_Fists"}
WEAPONS = {"Sword_Horizontal_Slash", "Staff_Cast_Forward"}
CURVE_GROUPS = (
    "m_RotationCurves", "m_CompressedRotationCurves", "m_EulerCurves",
    "m_PositionCurves", "m_ScaleCurves", "m_FloatCurves",
    "m_EditorCurves", "m_EulerEditorCurves",
)
# Recorded before the intentional mesh-curve addition, while both file SHAs
# matched cleanup-gait-curve-comparison.json. This is an independent fixed
# reference, not a baseline generated from the later final clips.
APPROVED_GAIT = {
    "Walk_Fists": {
        "fileSHA256": "ff96b55f7212b1aa614e0d40cf4aac73438545614f1f85de0339548a137d253c",
        "curvePayloadSHA256": "cb2fdc6e05730220f25beedbc9453f22bf3c77a3f5ccd89602fc933526735974",
    },
    "Run_Fists": {
        "fileSHA256": "6e60b2828969595b84e8bbe37b9c07da87422a49d135a6072ca9e070755c0a72",
        "curvePayloadSHA256": "3758313c4d8098e51553449153063988bbb3087f287075e4cc5e0a74db3e6837",
    },
}


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def digest(value) -> str:
    return sha(json.dumps(value, sort_keys=True, separators=(",", ":")).encode())


def clip_load(data: bytes) -> dict:
    text = re.sub(r"^%[^\n]*\n", "", data.decode("utf-8-sig"), flags=re.M)
    text = re.sub(r"^--- !u!\d+ &-?\d+", "---", text, flags=re.M)
    return yaml.load(text, Loader=yaml.CSafeLoader)["AnimationClip"]


def keys(binding: dict) -> list:
    curve = binding.get("curve", {})
    return curve.get("m_Curve", []) if isinstance(curve, dict) else curve


def payload(clip: dict) -> dict:
    # Use the exact schema used for the fixed pre-mesh fingerprints, including
    # this serialized boolean whose name also ends with "Curves".
    return {**{group: clip.get(group, []) for group in CURVE_GROUPS},
            "m_HasMotionFloatCurves": clip.get("m_HasMotionFloatCurves")}


def identity(binding: dict) -> dict:
    return {k: v for k, v in binding.items() if k != "curve"}


def flatten(value, prefix="") -> dict:
    if isinstance(value, dict):
        result = {}
        for key, item in value.items():
            result.update(flatten(item, prefix + "/" + str(key)))
        return result
    if isinstance(value, list):
        result = {}
        for i, item in enumerate(value):
            result.update(flatten(item, prefix + "/" + str(i)))
        return result
    return {prefix: value}


def numeric_difference_summary(before, after) -> dict:
    a, b = flatten(before), flatten(after)
    numeric, other = [], []
    for path in sorted(set(a) | set(b)):
        old, new = a.get(path), b.get(path)
        if old == new:
            continue
        if isinstance(old, (int, float)) and isinstance(new, (int, float)):
            numeric.append((abs(float(old) - float(new)), path, old, new))
        else:
            other.append({"path": path, "before": old, "after": new})
    numeric.sort(reverse=True)
    return {
        "numericLeafDifferences": len(numeric),
        "maxNumericLeafDifference": (
            {"delta": numeric[0][0], "path": numeric[0][1],
             "before": numeric[0][2], "after": numeric[0][3]} if numeric else None
        ),
        "nonnumericDifferences": other[:12],
    }


def without_rotation_values(binding: dict) -> dict:
    """Retain binding, key time, tangent mode/weight metadata, and infinity."""
    result = copy.deepcopy(binding)
    for key in keys(result):
        for field in ("value", "inSlope", "outSlope"):
            key.pop(field, None)
    return result


def quaternion_angle(a: dict, b: dict) -> float:
    av = [float(a[c]) for c in "xyzw"]
    bv = [float(b[c]) for c in "xyzw"]
    an = math.sqrt(sum(x*x for x in av))
    bn = math.sqrt(sum(x*x for x in bv))
    if not an or not bn:
        return math.inf
    av, bv = [x/an for x in av], [x/bn for x in bv]
    # Chord-angle form stays precise at the observed ~0.0002 degree changes.
    if sum(x*y for x, y in zip(av, bv)) < 0:
        bv = [-x for x in bv]
    chord = math.sqrt(sum((x-y)**2 for x, y in zip(av, bv)))
    return math.degrees(4 * math.asin(min(1.0, chord/2)))


def gait_compare(old: dict, new: dict, name: str) -> dict:
    groups = {}
    structure_ok = True
    position_time_exact = True
    for group in CURVE_GROUPS:
        a, b = old.get(group, []), new.get(group, [])
        binding_structure = len(a) == len(b)
        time_structure = binding_structure
        permitted_values_only = binding_structure
        if binding_structure:
            for x, y in zip(a, b):
                binding_structure &= identity(x) == identity(y)
                xk, yk = keys(x), keys(y)
                time_structure &= len(xk) == len(yk)
                if len(xk) == len(yk):
                    time_structure &= all(k.get("time") == l.get("time") for k, l in zip(xk, yk))
                rot = group in ("m_RotationCurves", "m_EulerEditorCurves")
                rot |= group == "m_EditorCurves" and str(x.get("attribute", "")).startswith("m_LocalRotation.")
                permitted_values_only &= (
                    without_rotation_values(x) == without_rotation_values(y) if rot else x == y
                )
        structure_ok &= binding_structure and time_structure and permitted_values_only
        if group in ("m_PositionCurves", "m_ScaleCurves", "m_FloatCurves"):
            position_time_exact &= a == b
        groups[group] = {
            "bindings": len(a), "bindingsExact": binding_structure,
            "keyTimesAndCountsExact": time_structure, "contentExact": a == b,
            "onlyPermittedRotationValueSlopeDifferences": permitted_values_only,
            **numeric_difference_summary(a, b),
        }
    maximum = {"angleDegrees": 0.0}
    for a, b in zip(old.get("m_RotationCurves", []), new.get("m_RotationCurves", [])):
        for i, (ak, bk) in enumerate(zip(keys(a), keys(b))):
            angle = quaternion_angle(ak["value"], bk["value"])
            if angle > maximum["angleDegrees"]:
                maximum = {"angleDegrees": angle, "path": a.get("path"),
                           "keyIndex": i, "time": ak.get("time")}
    approved = APPROVED_GAIT[name]
    exact_approved = digest(payload(new)) == approved["curvePayloadSHA256"]
    return {
        "passed": structure_ok and position_time_exact and maximum["angleDegrees"] < .001 and exact_approved,
        "sameApprovedPreMeshCurvePayload": exact_approved,
        "approvedPreMeshFileSHA256": approved["fileSHA256"],
        "approvedPreMeshCurvePayloadSHA256": approved["curvePayloadSHA256"],
        "positionScaleGripAndKeyTimeExact": position_time_exact,
        "bindingStructureAndNonrotationMetadataExact": structure_ok,
        "maximumQuaternionKeyAngle": maximum,
        "quaternionAngleLimitDegrees": .001,
        "groups": groups,
        "scope": "Serialized quaternion keys and curve metadata, not continuous Unity playback. Euler-editor derivative differences are explicitly recorded; the complete motion payload must equal the approved pre-mesh payload. Actual saved-clip pose/capture regression is a separate audit.",
    }


def guid(path: Path) -> str:
    text = path.with_suffix(path.suffix + ".meta").read_text(encoding="utf-8-sig")
    return re.search(r"^guid:\s*(\w+)", text, re.M).group(1)


def mesh_curve_check(clip: dict) -> dict:
    curves = clip.get("m_PPtrCurves", [])
    bindings = clip.get("m_ClipBindingConstant", {})
    pptr_bindings = [b for b in bindings.get("genericBindings", []) if b.get("isPPtrCurve") == 1]
    mapping = bindings.get("pptrCurveMapping", [])
    return {"passed": not curves and not pptr_bindings and not mapping,
            "curveCount": len(curves), "curves": curves,
            "objectReferenceBindingCount": len(pptr_bindings),
            "objectReferenceMappingCount": len(mapping)}


def static_metadata(clip: dict) -> dict:
    """Retain every metadata field, including object-reference binding tables."""
    result = copy.deepcopy(clip)
    for group in CURVE_GROUPS:
        result.pop(group, None)
    return result


def prefab_mesh_check(path: Path, expected_guid: str) -> dict:
    data = path.read_bytes()
    text = re.sub(r"^%[^\n]*\n", "", data.decode("utf-8-sig"), flags=re.M)
    text = re.sub(r"^--- !u!\d+ &-?\d+", "---", text, flags=re.M)
    docs = list(yaml.load_all(text, Loader=yaml.CSafeLoader))
    meshes = [d["SkinnedMeshRenderer"].get("m_Mesh") for d in docs
              if isinstance(d, dict) and "SkinnedMeshRenderer" in d]
    expected = {"fileID": 4300000, "guid": expected_guid, "type": 2}
    return {"passed": meshes == [expected], "path": str(path),
            "prefabSHA256": sha(data), "skinnedMeshRendererCount": len(meshes),
            "savedMeshReferences": meshes, "expectedMeshReference": expected}


def run(args) -> dict:
    baseline = json.loads((RESEARCH / "cleanup-original-targeted-baseline.json").read_text(encoding="utf-8-sig"))
    expected = {Path(item["path"]).stem: item["sha256"] for item in baseline if item["path"].endswith(".anim")}
    if len(expected) != 16:
        raise ValueError(f"Expected the retained 16-clip baseline, found {len(expected)}")
    mesh_paths = {
        "base": ROOT / args.base_mesh,
        "sword": ROOT / args.sword_mesh,
        "staff": ROOT / args.staff_mesh,
    }
    meshes = {key: {"path": str(p), "guid": guid(p), "sha256": sha(p.read_bytes())} for key, p in mesh_paths.items()}
    prefabs = {
        "base": ROOT / args.base_prefab,
        "sword": ROOT / args.sword_prefab,
        "staff": ROOT / args.staff_prefab,
    }
    prefab_checks = {key: prefab_mesh_check(path, meshes[key]["guid"]) for key, path in prefabs.items()}
    names = sorted(set(expected) | WEAPONS)
    actual_names = {p.stem for p in CLIPS.glob("*.anim")}
    rows = []
    for name in names:
        relative = (CLIPS / (name + ".anim")).relative_to(ROOT).as_posix()
        before = subprocess.run(["git", "show", "HEAD:" + relative], cwd=ROOT, capture_output=True, check=True).stdout
        after = (ROOT / relative).read_bytes()
        old, new = clip_load(before), clip_load(after)
        old_sha = sha(before)
        baseline_ok = old_sha == expected[name] if name in expected else True
        mesh = mesh_curve_check(new)
        old_static = static_metadata(old)
        new_static = static_metadata(new)
        static_exact = old_static == new_static
        row = {
            "name": name, "gitHEADSHA256": old_sha,
            "recordedCleanupBaselineSHA256": expected.get(name),
            "gitHEADMatchesRecordedBaseline": baseline_ok,
            "baselineScope": "cleanup baseline SHA verified" if name in expected else "weapon motion compared to immutable git HEAD blob; no separate historical SHA was recorded",
            "currentSHA256": sha(after), "sameFileBytes": before == after,
            "objectReferenceCurvesAbsent": mesh,
            "oldMeshCurveCount": len(old.get("m_PPtrCurves", [])),
            "staticMetadataAndBindingTablesExact": static_exact,
            "staticDifferences": numeric_difference_summary(old_static, new_static),
            "motionCurvePayloadExact": payload(old) == payload(new),
            "oldMotionCurvePayloadSHA256": digest(payload(old)),
            "newMotionCurvePayloadSHA256": digest(payload(new)),
        }
        if name in GAIT:
            row["gaitRegression"] = gait_compare(old, new, name)
            curves_ok = row["gaitRegression"]["passed"]
        else:
            curves_ok = row["motionCurvePayloadExact"]
            row["curveGroupsExact"] = {g: old.get(g, []) == new.get(g, []) for g in CURVE_GROUPS}
        row["passed"] = baseline_ok and static_exact and curves_ok and mesh["passed"]
        rows.append(row)
    return {
        "auditedUTC": dt.datetime.now(dt.timezone.utc).isoformat(),
        "method": "Read-only git HEAD blobs and current saved Unity YAML. No Unity execution, clip rewriting, source restoration, or baseline replacement. All 18 shared clips must contain zero PPtr curves/bindings. Static mesh selection is verified in three fixed art review prefabs, with no game/controller integration claim.",
        "expectedClipCount": 18, "actualClipCount": len(actual_names),
        "exactClipSet": actual_names == set(names), "meshes": meshes,
        "fixedPrefabChecks": prefab_checks,
        "checks": {
            "all16HistoricalBaselineSHAsVerified": all(r["gitHEADMatchesRecordedBaseline"] for r in rows if r["name"] in expected),
            "all18ObjectReferenceCurvesAbsent": all(r["objectReferenceCurvesAbsent"]["passed"] for r in rows),
            "threeFixedPrefabMeshesCorrect": all(p["passed"] for p in prefab_checks.values()),
            "all14NonGaitNonweaponMotionPayloadsExact": all(r["motionCurvePayloadExact"] for r in rows if r["name"] in expected and r["name"] not in GAIT),
            "bothWeaponMotionPayloadsExactToHEAD": all(r["motionCurvePayloadExact"] for r in rows if r["name"] in WEAPONS),
            "bothGaitsMatchApprovedTinyQuaternionDifference": all(r["gaitRegression"]["passed"] for r in rows if r["name"] in GAIT),
        },
        "clips": rows,
        "passed": actual_names == set(names) and all(r["passed"] for r in rows) and all(p["passed"] for p in prefab_checks.values()),
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-mesh", default="Assets/Art/Characters/Protagonist01/ChibiMotion_v007/Meshes/Protagonist_Chibi_CylinderGrip.asset")
    parser.add_argument("--sword-mesh", default="Assets/Art/Characters/Protagonist01/ChibiMotion_v007/Meshes/Protagonist_Chibi_SwordClosedGrip.asset", help="Final sword full-body variant .asset, repository-relative")
    parser.add_argument("--staff-mesh", default="Assets/Art/Characters/Protagonist01/ChibiMotion_v007/Meshes/Protagonist_Chibi_StaffClosedGrip.asset", help="Final staff full-body variant .asset, repository-relative")
    parser.add_argument("--base-prefab", default="Assets/Art/Characters/Protagonist01/ChibiMotion_v007/Prefabs/Protagonist_Chibi_MotionReview.prefab")
    parser.add_argument("--sword-prefab", default="Assets/Art/Characters/Protagonist01/ChibiMotion_v007/Prefabs/Protagonist_Chibi_SwordGrip.prefab")
    parser.add_argument("--staff-prefab", default="Assets/Art/Characters/Protagonist01/ChibiMotion_v007/Prefabs/Protagonist_Chibi_StaffGrip.prefab")
    parser.add_argument("--output", type=Path, default=RESEARCH / "cleanup-final-curve-audit.json")
    args = parser.parse_args()
    report = run(args)
    args.output.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps({"passed": report["passed"], "clips": len(report["clips"]),
                      "checks": report["checks"], "output": str(args.output)}, ensure_ascii=False))
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    sys.exit(main())
