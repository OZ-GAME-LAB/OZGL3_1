"""Read-only comparison of the final saved-clip capture against retained evidence.

Use --report with the completed final report. This script does not assign a
visual-quality pass from a tolerance; it records exactness and observed deltas.
"""
from __future__ import annotations

import argparse
from collections import Counter
import datetime as dt
import hashlib
import io
import json
from pathlib import Path
import subprocess

from PIL import Image, ImageChops


ROOT = Path(__file__).resolve().parents[5]
RESEARCH = Path(__file__).resolve().parent
CLIPS = ROOT / "Assets/Art/Characters/Protagonist01/ChibiMotion_v007/Clips"


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def leaves(value, path=""):
    if isinstance(value, dict):
        for k, v in value.items():
            yield from leaves(v, path + "/" + str(k))
    elif isinstance(value, list):
        for i, v in enumerate(value):
            yield from leaves(v, path + "/" + str(i))
    else:
        yield path, value


def compare_frames(old_frames, new_frames):
    old, new = dict(leaves(old_frames)), dict(leaves(new_frames))
    missing = sorted(set(old) - set(new))
    added = sorted(set(new) - set(old))
    numeric, other = [], []
    per_field = {}
    for path in sorted(set(old) & set(new)):
        a, b = old[path], new[path]
        if a == b:
            continue
        if isinstance(a, (int, float)) and isinstance(b, (int, float)):
            item = {"path": path, "before": a, "after": b, "absoluteDelta": abs(a-b)}
            numeric.append(item)
            field = path.split("/")[2]
            prior = per_field.get(field)
            if prior is None or prior["absoluteDelta"] < item["absoluteDelta"]:
                per_field[field] = item
        else:
            other.append({"path": path, "before": a, "after": b})
    numeric.sort(key=lambda x: x["absoluteDelta"], reverse=True)
    timing_grip = all("/time" not in x["path"] and "/gripL" not in x["path"] and "/gripR" not in x["path"] for x in numeric)
    return {
        "frameCountExact": len(old_frames) == len(new_frames),
        "allOldFrameFieldsPresent": not missing,
        "missingFields": missing[:20], "addedFields": added[:20],
        "allOriginalFrameValuesExact": not missing and not numeric and not other,
        "timeAndGripValuesExact": timing_grip and not other,
        "numericLeafDifferences": len(numeric),
        "maximumAbsoluteScalarDelta": numeric[0] if numeric else None,
        "maximumAbsoluteDeltaByField": per_field,
        "largest30Differences": numeric[:30],
        "nonnumericDifferences": other[:20],
        "countAbsoluteDeltaAbove1eMinus5": sum(x["absoluteDelta"] > 1e-5 for x in numeric),
        "countAbsoluteDeltaAbove1eMinus6": sum(x["absoluteDelta"] > 1e-6 for x in numeric),
        "note": "Scalar deltas retain their source units (positions in metres, directions unitless). Histogram thresholds are descriptive and do not replace visual inspection or establish an acceptance rule.",
    }


def historical_png(entry):
    """Accept only an original file/blob matching the independently saved SHA."""
    candidates = [Path(entry[k]) for k in ("baseline", "source") if entry.get(k)]
    for path in candidates:
        if path.is_file():
            data = path.read_bytes()
            if sha(data) == entry["sha256"]:
                return data, "verified-original-file:" + str(path)
    for path in candidates:
        try:
            relative = path.relative_to(ROOT).as_posix()
        except ValueError:
            continue
        result = subprocess.run(["git", "show", "HEAD:" + relative], cwd=ROOT, capture_output=True)
        if result.returncode == 0 and sha(result.stdout) == entry["sha256"]:
            return result.stdout, "verified-git-HEAD-blob:" + relative
    return None, None


def image_diff(before: bytes, after: bytes):
    a, b = Image.open(io.BytesIO(before)).convert("RGBA"), Image.open(io.BytesIO(after)).convert("RGBA")
    if a.size != b.size:
        return {"sameResolution": False, "beforeSize": a.size, "afterSize": b.size}
    delta = ImageChops.difference(a, b)
    rgb = delta.convert("RGB")
    hist = rgb.histogram()
    total_abs = sum(i * hist[channel*256+i] for channel in range(3) for i in range(256))
    # Pixel count is RGB exact, independently of alpha (all captures are opaque).
    pixels = rgb.get_flattened_data() if hasattr(rgb, "get_flattened_data") else rgb.getdata()
    changed = sum(1 for p in pixels if p != (0, 0, 0))
    return {
        "sameResolution": True,
        "changedRGBPixels": changed,
        "totalPixels": a.width*a.height,
        "meanAbsoluteRGBChannelDelta255": total_abs/(a.width*a.height*3),
        "maximumRGBChannelDelta255": max(v[1] for v in rgb.getextrema()),
        "changedRGBBoundingBox": rgb.getbbox(),
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--output", type=Path, default=RESEARCH / "cleanup-final-capture-regression.json")
    args = parser.parse_args()
    current_bytes = args.report.read_bytes()
    report = json.loads(current_bytes.decode("utf-8-sig"))
    baseline = json.loads((RESEARCH / "cleanup-nonweapon-baseline.json").read_text(encoding="utf-8-sig"))
    images = json.loads((RESEARCH / "cleanup-frame-hashes-baseline.json").read_text(encoding="utf-8-sig"))
    motions = {m["name"]: m for m in report["motions"]}
    rows = []
    for old in baseline["motions"]:
        name = old["name"]
        if name not in motions:
            rows.append({"name": name, "missingMotion": True})
            continue
        new = motions[name]
        clip_path = CLIPS / (name + ".anim")
        source_sha = new.get("sourceClipSHA256")
        row = {
            "name": name,
            "sourceClipSHA256": source_sha,
            "actualSavedClipSHA256": sha(clip_path.read_bytes()),
            "sourceClipMatchesCurrentSavedAsset": bool(source_sha) and source_sha.lower() == sha(clip_path.read_bytes()),
            "sourceMeshSHA256": new.get("sourceMeshSHA256"),
            "captureSettingsExact": {k: old.get(k) == new.get(k) for k in (
                "loop", "durationSeconds", "captureSeconds", "frameCount",
                "captureRate", "width", "height", "cameraPosition", "cameraTarget", "orthographicSize"
            )},
            **compare_frames(old["frames"], new["frames"]),
        }
        if old.get("prop") != "Sword" and new.get("prop") != "Sword":
            old_body = [{k: v for k, v in f.items() if k != "swordTip"} for f in old["frames"]]
            new_body = [{k: v for k, v in f.items() if k != "swordTip"} for f in new["frames"]]
            row["bodyAndOtherRecordedFieldsExcludingInactiveSwordTip"] = {
                "scope": "Only swordTip is separated because this motion has no active Sword prop. The dormant sword marker follows the intentionally remounted weapon, even while its renderer is inactive. All raw original-field differences remain above; this is a second, explicitly scoped character/other-fields comparison.",
                **compare_frames(old_body, new_body),
            }
        rows.append(row)
    image_rows = []
    for old in images["frames"]:
        motion = motions.get(old["name"])
        if not motion:
            image_rows.append({"name": old["name"], "tick": old["tick"], "missingMotion": True})
            continue
        path = Path(motion["framesDirectory"]) / f"{old['tick']:04d}.png"
        if not path.exists():
            image_rows.append({"name": old["name"], "tick": old["tick"], "missingFile": str(path)})
            continue
        data = path.read_bytes()
        same = sha(data) == old["sha256"]
        row = {"name": old["name"], "tick": old["tick"], "path": str(path),
               "baselineSHA256": old["sha256"], "currentSHA256": sha(data), "exactPNGBytes": same,
               "baselineResolution": old["resolution"], "currentResolution": Image.open(io.BytesIO(data)).size}
        if not same:
            historical, historical_source = historical_png(old)
            row["verifiedHistoricalOriginalPNGAvailable"] = historical is not None
            row["historicalSource"] = historical_source
            if historical is not None:
                row["pixelDifference"] = image_diff(historical, data)
        image_rows.append(row)
    output = {
        "auditedUTC": dt.datetime.now(dt.timezone.utc).isoformat(),
        "method": "Read-only comparison against 16-motion/647-frame metadata and 62 original PNG SHA baselines retained before deletion. Final fixed-prefab capture uses the original saved-float-clip sampling path; retired native mesh-switch attempts are not the final architecture. Exactness and any observed changes are reported independently. No baselines, clips, or source PNGs are modified.",
        "report": str(args.report.resolve()), "reportSHA256": sha(current_bytes),
        "expectedBaselineMotionCount": 16,
        "expectedBaselineMetadataFrameCount": 647,
        "baselineMetadataFrameCount": sum(len(m["frames"]) for m in baseline["motions"]),
        "originalFrameValuesExactMotionCount": sum(r.get("allOriginalFrameValuesExact", False) for r in rows),
        "bodyAndOtherFieldsExactExcludingInactiveSwordTipMotionCount": sum(
            r.get("bodyAndOtherRecordedFieldsExcludingInactiveSwordTip", {}).get("allOriginalFrameValuesExact", False)
            for r in rows),
        "samplePNGCount": len(image_rows),
        "samplePNGBytesExactCount": sum(r.get("exactPNGBytes", False) for r in image_rows),
        "samplePNGBytesExactCountByMotion": dict(Counter(r["name"] for r in image_rows if r.get("exactPNGBytes"))),
        "captureLayoutAndSourceIntegrityPassed": all(
            r.get("frameCountExact") and r.get("allOldFrameFieldsPresent") and r.get("timeAndGripValuesExact")
            and r.get("sourceClipMatchesCurrentSavedAsset") and all(r.get("captureSettingsExact", {}).values())
            for r in rows
        ) and len(image_rows) == 62 and all("missingFile" not in r and "missingMotion" not in r for r in image_rows),
        "motions": rows, "samplePNGs": image_rows,
        "acceptanceScope": "No visual-quality pass is inferred from scalar/pixel tolerances. Every nonexact evaluation is explicitly retained for root visual/numeric assessment.",
    }
    args.output.write_text(json.dumps(output, indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps({k: output[k] for k in (
        "originalFrameValuesExactMotionCount", "samplePNGBytesExactCount",
        "captureLayoutAndSourceIntegrityPassed", "reportSHA256"
    )}))
    return 0 if output["captureLayoutAndSourceIntegrityPassed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
