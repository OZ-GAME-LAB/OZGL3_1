"""Read-only source-frame QA, isolated Idle comparison, and delivery validation.

Run with python -B. Writes only v005 ReviewOutput/Comparisons, FinalQA and
delivery-validation.json. Never launches Unity or rebuilds character/effect assets.
"""
from __future__ import annotations

import hashlib
import argparse
import html.parser
import importlib.util
import json
import math
from datetime import datetime, timezone
from pathlib import Path
from urllib.parse import unquote, urlsplit

from PIL import Image, ImageDraw


BASE = Path(__file__).resolve().parent
PROJECT = BASE.parents[3]
OUTPUT = BASE / "ReviewOutput"
EARLIER = BASE.with_name("v004-motion-polish")


def read(path: Path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sha256(path: Path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def actual_path(value: str):
    path = Path(value)
    return path if path.is_absolute() else PROJECT / path


def exporter():
    spec = importlib.util.spec_from_file_location("v005_export", BASE / "export_review_gifs.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def vector(value):
    return tuple(float(value[key]) for key in ("x", "y", "z"))


def subtract(a, b):
    return tuple(x - y for x, y in zip(a, b))


def dot(a, b):
    return sum(x * y for x, y in zip(a, b))


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def normal(a):
    length = math.sqrt(dot(a, a))
    return tuple(value / length for value in a)


def idle_comparison(export, current_report):
    previous_report = read(EARLIER / "motion-review.json")
    previous = next(item for item in previous_report["motions"] if item["name"] == "Idle_Fists_SourceOnly")
    current = next(item for item in current_report["motions"] if item["name"] == "Idle_Fists_SourceOnly")
    previous_forward = normal(subtract(vector(previous["cameraTarget"]), vector(previous["cameraPosition"])))
    current_forward = normal(subtract(vector(current["cameraTarget"]), vector(current["cameraPosition"])))
    if dot(previous_forward, current_forward) < .9999999:
        raise ValueError("Idle source camera directions differ; a planar translation cannot normalize perspective.")
    if abs(previous["orthographicSize"] - current["orthographicSize"]) > .000001:
        raise ValueError("Idle source orthographic scales differ.")
    sequences = []
    shifts = []
    for motion in [previous, current]:
        paths = sorted(actual_path(motion["frames30Directory"]).glob("*.png"))
        if not paths:
            raise FileNotFoundError(motion["frames30Directory"])
        with Image.open(paths[0]) as first:
            source_size = first.size
        right = normal(cross((0, 1, 0), previous_forward))
        up = normal(cross(previous_forward, right))
        delta = subtract(vector(motion["cameraPosition"]), vector(previous["cameraPosition"]))
        pixels_per_meter = source_size[1] / (2 * previous["orthographicSize"])
        shift = (dot(delta, right) * pixels_per_meter, -dot(delta, up) * pixels_per_meter)
        shifts.append(shift)
        frames = []
        for path in paths:
            with Image.open(path) as image:
                image = image.convert("RGB")
                if any(abs(component) > .000001 for component in shift):
                    image = image.transform(image.size, Image.Transform.AFFINE,
                                            (1, 0, -shift[0], 0, 1, -shift[1]),
                                            resample=Image.Resampling.BICUBIC, fillcolor=(27, 30, 36))
                image = image.resize((512, 384), Image.Resampling.LANCZOS)
                frames.append(image)
        sequences.append(frames)
    count = max(len(sequence) for sequence in sequences)
    delays = export.clock_delays(count, 30)
    font = export.load_font(19)
    composed = []
    for index in range(count):
        image = Image.new("RGB", (1048, 434), (24, 29, 36))
        draw = ImageDraw.Draw(image)
        for tile, label in enumerate(["PREVIOUS v004 | original 1x", "REVISED v005 | original 1x"]):
            x = 8 + tile * 520
            draw.text((x + 6, 7), label, font=font, fill=(223, 232, 243))
            image.paste(sequences[tile][index % len(sequences[tile])], (x, 34))
        draw.text((13, 416), "Same orthographic projection | actual Unity PNGs | independent authored loops",
                  font=export.load_font(11), fill=(163, 184, 204))
        composed.append(image)
    target = OUTPUT / "Comparisons" / "Idle_v004_vs_v005.gif"
    target.parent.mkdir(exist_ok=True)
    palette = export.palette_for(composed)
    indexed = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in composed]
    indexed[0].save(target, save_all=True, append_images=indexed[1:], duration=delays, loop=0,
                    disposal=2, optimize=False)
    composed[36].save(target.with_suffix(".png"))
    metadata = {
        "gif": str(target), "poster": str(target.with_suffix(".png")), "source_rate": 30,
        "frame_count": count, "duration_ms": sum(delays), "source_speed": 1,
        "previous_frames": len(sequences[0]), "current_frames": len(sequences[1]),
        "previous_authored_duration": previous["authoredDuration"], "current_authored_duration": current["authoredDuration"],
        "camera_reference": {key: previous[key] for key in ["cameraPosition", "cameraTarget", "orthographicSize"]},
        "camera_translation_pixels": {"previous": shifts[0], "current": shifts[1]},
        "method": "Actual 30fps Unity PNGs, 1x clocks, independent existing loops. Identical orientation and orthographic scale. Only the saved camera center difference is normalized by uniform image translation (about 1.5 source pixels); both tiles then share one uniform spatial downscale. No pose, body alignment, height, timing, temporal interpolation, or original file is modified.",
    }
    target.with_suffix(".json").write_text(json.dumps(metadata, indent=2), encoding="utf-8")
    return metadata


def sequential_qa(export, report):
    motions = {item["name"].removesuffix("_SourceOnly"): item for item in report["motions"]}
    cases = [
        ("Drink_Entry_30fps", "Potion_Drink", 18, 18, (285, 115, 475, 285)),
        ("Drink_Contact_30fps", "Potion_Drink", 30, 18, (285, 115, 475, 285)),
        ("Pickup_Reach_30fps", "Pickup_Low", 18, 18, None),
        ("Pickup_Return_30fps", "Pickup_Low", 42, 18, None),
        ("Throw_Release_30fps", "Item_Throw_Overhand", 34, 18, None),
        ("Interact_Reach_30fps", "Interact_Reach", 12, 18, None),
        ("Idle_Loop_30fps", "Idle_Fists", 111, 18, None),
        ("Walk_Loop_30fps", "Walk_Fists", 22, 18, None),
        ("Run_Loop_30fps", "Run_Fists", 15, 18, None),
    ]
    destination = BASE / "research" / "FinalQA"
    destination.mkdir(exist_ok=True)
    entries = []
    for label, name, start, count, crop in cases:
        paths = sorted(actual_path(motions[name]["frames30Directory"]).glob("*.png"))
        sheet = Image.new("RGB", (1464, 684), (24, 29, 36))
        draw = ImageDraw.Draw(sheet)
        draw.text((10, 5), f"{label}: consecutive original 30fps frames, no skipped samples", font=export.load_font(15), fill=(223, 232, 243))
        for cell, index in enumerate(range(start, min(len(paths), start + count))):
            with Image.open(paths[index]) as frame:
                frame = frame.convert("RGB")
                if crop:
                    frame = frame.crop(crop)
                frame.thumbnail((234, 185), Image.Resampling.LANCZOS)
                x = 6 + (cell % 6) * 243
                y = 30 + (cell // 6) * 216
                sheet.paste(frame, (x + (234 - frame.width) // 2, y + (185 - frame.height) // 2))
            draw.text((x + 5, y + 187), f"{index:03d} | {index / 30:.3f}s", font=export.load_font(13), fill=(181, 200, 220))
        target = destination / f"{label}.png"
        sheet.save(target)
        entries.append({"name": label, "motion": name, "first_frame": start, "frames": count,
                        "sampling": "consecutive 30fps actual PNGs", "image": str(target)})
    (destination / "frame-sequences.json").write_text(json.dumps(entries, indent=2), encoding="utf-8")
    return entries


class LocalLinks(html.parser.HTMLParser):
    def __init__(self):
        super().__init__()
        self.links = []

    def handle_starttag(self, tag, attrs):
        for key, value in attrs:
            if key in ["href", "src"] and value:
                self.links.append(value)


def validate(export, report, comparison, qa):
    manifest_path = OUTPUT / "export-manifest.json"
    manifest = read(manifest_path)
    checks, failures = [], []

    def check(label, passed, detail=None):
        checks.append({"name": label, "passed": bool(passed), "detail": detail})
        if not passed:
            failures.append(label)

    check("14 motion reports", len(report["motions"]) == 14)
    check("14 individual GIFs", len(manifest["outputs"]) == len(list((OUTPUT / "GIF").glob("*.gif"))) == 14)
    check("3 motion montage GIFs", len(manifest["groups"]) == len(list((OUTPUT / "Groups").glob("*.gif"))) == 3)
    check("14 prior motion comparisons", len(manifest.get("comparisons", [])) == 14)
    check("No effect outputs", all(item["category"] == "Motion" for item in manifest["outputs"]))
    check("No death output", all("dead" not in item["name"].lower() and "death" not in item["name"].lower() for item in manifest["outputs"]))
    check("Newest export follows source report", manifest_path.stat().st_mtime >= (BASE / "motion-review.json").stat().st_mtime)
    lookup = {item["name"]: item for item in report["motions"]}
    artifacts = []
    for item in manifest["outputs"]:
        motion = lookup[item["name"]]
        path = actual_path(item["gif"])
        frames = sorted(actual_path(motion["frames30Directory"]).glob("*.png"))
        check(item["name"] + " PNG count", len(frames) == motion["frames30"] == item["source_frames"])
        check(item["name"] + " source 1x clock", motion["sourceSpeed"] == 1 and motion["sampleRate"] == 60 and motion["gifRate"] == 30)
        check(item["name"] + " newest GIF", path.stat().st_mtime >= max(frame.stat().st_mtime for frame in frames))
        check(item["name"] + " contact exists", actual_path(item["contact"]).exists())
        with Image.open(path) as gif:
            durations = []
            for i in range(gif.n_frames):
                gif.seek(i)
                durations.append(gif.info.get("duration", 0))
            clock = sum(durations)
            check(item["name"] + " dimensions", gif.size == (report["width"], report["height"]) == (item["width"], item["height"]))
            check(item["name"] + " encoded frame count", gif.n_frames == item["encoded_frames"])
            check(item["name"] + " duration", clock == item["gif_duration_ms"] == sum(export.clock_delays(len(frames), 30)), clock)
            check(item["name"] + " quantization bound", abs(clock - len(frames) * 1000 / 30) <= 5.000001)
        artifacts.append({"file": str(path), "sha256": sha256(path), "bytes": path.stat().st_size, "duration_ms": clock})
    for group in manifest["groups"]:
        path = actual_path(group["gif"])
        with Image.open(path) as gif:
            clock = 0
            for i in range(gif.n_frames):
                gif.seek(i)
                clock += gif.info.get("duration", 0)
            check(group["name"] + " group duration", clock == group["duration_ms"], clock)
            check(group["name"] + " group dimensions", gif.size == (group["width"], group["height"]))
        check(group["name"] + " poster exists", actual_path(group["poster"]).exists())
        artifacts.append({"file": str(path), "sha256": sha256(path), "bytes": path.stat().st_size, "duration_ms": clock})
    gallery = OUTPUT / "index.html"
    parser = LocalLinks()
    source = gallery.read_text(encoding="utf-8")
    parser.feed(source)
    check("Gallery v005 title", "Natural Character Motion Review v005" in source)
    check("Gallery no effect category", "VFX" not in source)
    for index, value in enumerate(parser.links):
        parsed = urlsplit(value)
        target = gallery.parent / unquote(parsed.path)
        check(f"Gallery local link {index + 1}", not parsed.scheme and not parsed.netloc and target.exists(), value)
    baseline = read(BASE / "vfx-unchanged-baseline.json")
    for item in baseline:
        path = Path(item["Path"])
        check("Effect unchanged: " + path.name, path.exists() and sha256(path).upper() == item["Hash"].upper())
    comparison_path = Path(comparison["gif"])
    with Image.open(comparison_path) as gif:
        clock = 0
        for i in range(gif.n_frames):
            gif.seek(i)
            clock += gif.info.get("duration", 0)
        check("Idle comparison clock", clock == comparison["duration_ms"])
        check("Idle comparison dimensions", gif.size == (1048, 434))
    check("Nine consecutive-frame QA sheets", len(qa) == 9 and all(Path(item["image"]).exists() for item in qa))
    result = {
        "validated_utc": datetime.now(timezone.utc).isoformat(), "success": not failures,
        "check_count": len(checks), "failed_checks": failures,
        "source_report_sha256": sha256(BASE / "motion-review.json"), "export_manifest_sha256": sha256(manifest_path),
        "motion_gifs": 14, "montage_gifs": 3, "local_html_links": len(parser.links),
        "effect_baseline_files": len(baseline), "comparison": comparison, "artifacts": artifacts,
        "visual_qa_scope": "Independent eye review is recorded separately after reading consecutive original 30fps image sequences. File checks are not a substitute for aesthetic approval.",
        "checks": checks,
    }
    (BASE / "delivery-validation.json").write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps({"success": result["success"], "checks": len(checks), "failed": failures,
                      "comparison": comparison["gif"], "qa_sheets": len(qa)}, ensure_ascii=False))
    if failures:
        raise SystemExit(1)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--prepare-only", action="store_true", help="Build comparison and QA sheets; defer final delivery validation.")
    arguments = parser.parse_args()
    export = exporter()
    report = read(BASE / "motion-review.json")
    comparison = idle_comparison(export, report)
    qa = sequential_qa(export, report)
    if arguments.prepare_only:
        print(json.dumps({"comparison": comparison["gif"], "qa_sheets": len(qa), "validation_deferred": True}))
    else:
        validate(export, report, comparison, qa)


if __name__ == "__main__":
    main()
