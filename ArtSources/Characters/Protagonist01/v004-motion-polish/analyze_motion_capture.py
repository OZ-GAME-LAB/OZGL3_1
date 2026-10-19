"""Read-only motion-capture diagnostics; writes a separate metrics report, never animation assets.

Usage: python analyze_motion_capture.py motion-review-diagnostic.json --output motion-metrics.json
Accepts MotionPolishReviewCapture's motions[].samples schema, plus older samples/poses captures.
Requires numpy. All tolerances below describe diagnostic selection, not automatic art approval.
"""

import argparse
import json
import math
from pathlib import Path

import numpy as np


POSITION_KEYS = ("leftFoot", "rightFoot", "leftHand", "rightHand", "head", "hips", "rootPosition")
IMPACT_WORDS = ("punch", "slash", "hit", "plunge", "throw", "cast", "land")


def vector(value, dimensions=3):
    if isinstance(value, dict):
        return [value.get(k, float("nan")) for k in "xyzw"[:dimensions]]
    if isinstance(value, (tuple, list)) and len(value) >= dimensions:
        return value[:dimensions]
    return [float("nan")] * dimensions


def stats(values):
    values = np.asarray(values, dtype=float)
    values = values[np.isfinite(values)]
    if not len(values):
        return {"count": 0}
    return {"count": int(len(values)), "median": float(np.median(values)),
            "p95": float(np.percentile(values, 95)), "p99": float(np.percentile(values, 99)),
            "maximum": float(values.max())}


def derivative(values, time):
    return np.gradient(values, time, axis=0, edge_order=2 if len(time) > 2 else 1)


def top_samples(values, time, source, indices, count=5):
    order = np.argsort(values[indices])[-count:][::-1]
    return [{"index": int(indices[i]), "time": float(time[indices[i]]),
             "clipTime": float(source[indices[i]]), "value": float(values[indices[i]])} for i in order]


def quat_velocity(q, time):
    """Shortest-path quaternion deltas, invariant to the q/-q representation."""
    q = q.copy()
    q /= np.maximum(np.linalg.norm(q, axis=1, keepdims=True), 1e-12)
    for i in range(1, len(q)):
        if np.dot(q[i - 1], q[i]) < 0:
            q[i] *= -1
    a, b = q[:-1], q[1:]
    av, bv = -a[:, :3], b[:, :3]
    xyz = b[:, 3:4] * av + a[:, 3:4] * bv + np.cross(bv, av)
    w = b[:, 3] * a[:, 3] - np.sum(bv * av, axis=1)
    negative = w < 0
    xyz[negative] *= -1
    w[negative] *= -1
    length = np.linalg.norm(xyz, axis=1)
    angle = 2 * np.arctan2(length, np.clip(w, -1, 1))
    rotation_vector = xyz / np.maximum(length[:, None], 1e-12) * angle[:, None]
    velocity = rotation_vector / np.diff(time)[:, None]
    midpoint = (time[:-1] + time[1:]) * .5
    return velocity, midpoint


def possible_cycle(time, tracks, active, name):
    """Search observable relative-pose recurrence, then rank possible C0/C1 trim windows."""
    required = ("leftFoot", "rightFoot", "leftHand", "rightHand", "head")
    if any(key not in tracks for key in required) or len(active) < 30:
        return {"available": False, "reason": "Insufficient tracked samples"}
    t = time[active]
    dt = float(np.median(np.diff(t)))
    feet_center = (tracks["leftFoot"] + tracks["rightFoot"]) * .5
    feature = np.column_stack((tracks["leftFoot"] - tracks["rightFoot"],
                               tracks["rightHand"] - tracks["leftHand"], tracks["head"] - feet_center))[active]
    # Relative tracks eliminate rigid root travel. Detrend residual generated drift before recurrence search.
    trend = np.column_stack((np.ones(len(t)), t - t[0]))
    feature = feature - trend @ np.linalg.lstsq(trend, feature, rcond=None)[0]
    scale = float(np.sqrt(np.mean(feature ** 2)))
    if scale < .003:
        return {"available": False, "reason": "Relative pose has under 3mm RMS variation; no confident cadence estimate",
                "featureRmsMetres": scale}
    minimum = max(3, int(round(.25 / dt)))
    maximum = min(len(active) // 2, int(round(1.8 / dt)))
    if maximum <= minimum:
        return {"available": False, "reason": "Not enough data for two observable periods"}
    lags = np.arange(minimum, maximum + 1)
    error = np.array([np.sqrt(np.mean((feature[lag:] - feature[:-lag]) ** 2)) / scale for lag in lags])
    minima = [i for i in range(1, len(lags) - 1) if error[i] <= error[i - 1] and error[i] < error[i + 1]]
    if not minima:
        best = int(np.argmin(error))
        return {"available": False, "reason": "No interior recurrence minimum; a monotonic/boundary match is not evidence of a cycle",
                "featureRmsMetres": scale, "bestTestedPeriodSeconds": float(lags[best] * dt),
                "bestTestedRelativePoseNormalizedRms": float(error[best])}
    ranked = sorted(minima, key=lambda i: error[i] + .025 * lags[i] / maximum)[:6]
    candidates = [{"periodSeconds": float(lags[i] * dt), "lagSamples": int(lags[i]),
                   "relativePoseNormalizedRms": float(error[i])} for i in ranked]
    best = ranked[0]
    lag = int(lags[best])
    velocities = {key: derivative(tracks[key], time) for key in required}
    windows = []
    for local_start in range(len(active) - lag):
        start, end = int(active[local_start]), int(active[local_start + lag])
        if end - start != lag:
            continue
        gaps = {key: float(np.linalg.norm(tracks[key][end] - tracks[key][start])) for key in required}
        v_gaps = {key: float(np.linalg.norm(velocities[key][end] - velocities[key][start])) for key in required}
        pos_rms = math.sqrt(sum(v * v for v in gaps.values()) / len(gaps))
        vel_rms = math.sqrt(sum(v * v for v in v_gaps.values()) / len(v_gaps))
        windows.append({"startTime": float(time[start]), "endTime": float(time[end]),
                        "periodSeconds": float(time[end] - time[start]), "positionGapMetres": gaps,
                        "velocityGapMetresPerSecond": v_gaps, "positionRms": pos_rms,
                        "velocityRms": vel_rms, "rankingScoreMetres": pos_rms + .05 * vel_rms})
    windows.sort(key=lambda w: w["rankingScoreMetres"])
    return {"available": True, "method": "Full left/right-labelled relative pose recurrence; minimum .25s, maximum 1.8s and at least two periods. "
            "No half-step leg swap. Windows rank position RMS + .05 seconds * velocity RMS; this does not approve a trim.",
            "featureRmsMetres": scale, "periodCandidates": candidates,
            "recurrenceStrengthHeuristic": "strong" if error[best] < .15 else "moderate" if error[best] < .35 else "weak",
            "cadenceStepsPerMinuteIfTwoStepsPerCycle": float(120 / (lag * dt)) if any(s in name.lower() for s in ("walk", "run")) else None,
            "proposedWindows": windows[:10]}


def analyze_motion(motion):
    samples = motion.get("samples", motion.get("poses", []))
    if not isinstance(samples, list) or len(samples) < 4:
        return {"name": motion.get("name"), "error": "Fewer than four sampled poses"}
    time = np.array([s.get("time", s.get("seconds", i / motion.get("sampleRate", 60))) for i, s in enumerate(samples)], float)
    keep = np.r_[True, np.diff(time) > 1e-8] & np.isfinite(time)
    samples = [s for s, valid in zip(samples, keep) if valid]
    time = time[keep]
    source = np.array([s.get("clipTime", s.get("seconds", t)) for s, t in zip(samples, time)], float)
    core = np.array([s.get("phase", "AuthoredClip") == "AuthoredClip" and s.get("blendWeight", 1) >= .99 for s in samples])
    # Derivatives near an artificial lead/tail/clip boundary do not characterize the authored core.
    core[:2] = False
    core[-2:] = False
    active = np.flatnonzero(core)
    if len(active) < 4:
        active = np.arange(len(time))
    tracks = {}
    for key in POSITION_KEYS:
        p = np.array([vector(s.get(key)) for s in samples], float)
        if p.shape == (len(time), 3) and np.isfinite(p).all():
            tracks[key] = p
    name = motion.get("name", motion.get("clip", "unknown"))
    impact = any(word in name.lower() for word in IMPACT_WORDS)
    result = {"name": name, "source": motion.get("source", motion.get("assetPath")),
              "loop": bool(motion.get("loop", name.lower().split("_")[0] in ("idle", "walk", "run"))),
              "samples": len(time), "durationSeconds": float(time[-1] - time[0]),
              "samplingHzMedian": float(1 / np.median(np.diff(time))), "authoredCoreSamples": len(active),
              "rootTrackAvailable": "rootPosition" in tracks,
              "impactInterpretation": "Dynamic extrema may be intentional action impact; inspect listed clip times against poses."
              if impact else "Sharp extrema in locomotion/idle are continuity candidates; review loop and support phase.",
              "tracks": {}, "footSupport": {}}
    for key, p in tracks.items():
        velocity = derivative(p, time)
        acceleration = derivative(velocity, time)
        jerk = derivative(acceleration, time)
        speed, accel, jerkn = [np.linalg.norm(v, axis=1) for v in (velocity, acceleration, jerk)]
        result["tracks"][key] = {
            "positionRangeMetres": (p.max(axis=0) - p.min(axis=0)).tolist(),
            "netDisplacementMetres": (p[-1] - p[0]).tolist(),
            "speedMetresPerSecond": stats(speed[active]), "accelerationMetresPerSecond2": stats(accel[active]),
            "jerkMetresPerSecond3": stats(jerkn[active]),
            "largestAccelerationSamples": top_samples(accel, time, source, active),
            "maximumToP95AccelerationRatio": float(accel[active].max() / max(np.percentile(accel[active], 95), 1e-9))}
        rotation_key = key + "Rotation"
        q = np.array([vector(s.get(rotation_key), 4) for s in samples], float)
        if np.isfinite(q).all():
            angular, midpoint = quat_velocity(q, time)
            angular_acceleration = derivative(angular, midpoint)
            angular_core = core[:-1] & core[1:]
            result["tracks"][key]["angularSpeedDegreesPerSecond"] = stats(np.linalg.norm(angular, axis=1)[angular_core] * 180 / math.pi)
            result["tracks"][key]["angularAccelerationDegreesPerSecond2"] = stats(np.linalg.norm(angular_acceleration, axis=1)[angular_core] * 180 / math.pi)
        if key not in ("leftFoot", "rightFoot"):
            continue
        sole_key = "leftSoleY" if key == "leftFoot" else "rightSoleY"
        sole = np.array([s.get(sole_key, float("nan")) for s in samples], float)
        if not np.isfinite(sole).all():
            sole = p[:, 1]
            height_basis = "Foot bone Y; sole mesh heights unavailable, so absolute ground contact is not reliable"
        else:
            height_basis = "CPU-skinned foot-weighted sole vertex minimum"
        low = sole <= np.percentile(sole[active], 25)
        contact = (sole >= -.02) & (sole <= .027) & (np.abs(derivative(sole, time)) < .30) & core
        planar = np.linalg.norm(velocity[:, (0, 2)], axis=1)
        facing = motion.get("facing", 1)
        backward = -velocity[:, 0] * facing
        natural = backward[low & core & (backward > .01)]
        result["footSupport"][key] = {
            "heightBasis": height_basis, "soleYRangeMetres": [float(sole.min()), float(sole.max())],
            "absoluteFloorContactSamples": int(contact.sum()),
            "planarSpeedDuringPossibleFloorContact": stats(planar[contact]),
            "lowQuartileBackwardsFootSpeed": stats(natural),
            "note": "Possible support uses sole -2cm..+2.7cm and |vertical speed|<.30m/s. It is a heuristic, not authored contact metadata. "
            "World speed includes any baked source root travel; an in-place nominal-speed preview needs that root velocity added before judging slip."}
    result["cycleSearch"] = possible_cycle(time, tracks, active, name) if result["loop"] else {"available": False, "reason": "Finite action; no automatic loop proposal"}
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    capture = json.loads(args.input.read_text(encoding="utf-8-sig"))
    motions = capture.get("motions", capture.get("clips", []))
    output = args.output or args.input.with_name(args.input.stem + "-metrics.json")
    report = {"input": str(args.input), "method": "Read-only numpy analysis of sampled positions/quaternions. Central finite differences, "
              "quaternion sign continuity and shortest-path angular velocity. Report median/p95/p99/max separately. "
              "Authored-core derivatives exclude two samples at capture edges and blendWeight<.99. "
              "No curve smoothing, interpolation, source changes or gameplay edits.",
              "limitations": "A single cycle cannot establish recurring cadence as confidently as two or more. "
              "Derivative noise scales with sample spacing; compare reports at the same sampling rate. "
              "No universal acceleration limit distinguishes intentional impact from an error. "
              "Loop windows are candidates for review and do not approve automated trimming.",
              "motions": [analyze_motion(m) for m in motions]}
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2, allow_nan=False), encoding="utf-8")
    print(output)
    for motion in report["motions"]:
        cycle = motion.get("cycleSearch", {}).get("periodCandidates", [])
        print(motion["name"], "samples=", motion.get("samples"), "periodCandidates=", [round(c["periodSeconds"], 4) for c in cycle[:3]])


if __name__ == "__main__":
    main()
