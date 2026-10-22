"""Package ordered, actual Unity v007 renders at their unchanged 30 Hz clock.

Does not render, synthesize, interpolate, mirror, warp, or modify source PNGs.
Run only after the Unity capture and its review-report.json have completed.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import html
from html.parser import HTMLParser
import json
import math
import os
from pathlib import Path
import re
import shutil
import sys
import tempfile
from urllib.parse import quote, unquote, urlsplit

sys.dont_write_bytecode = True
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[3]
FPS = 30
SOURCE_SIZE = (720, 720)
FINITE_MAIN_HOLD_FRAMES = 8
GROUPS = [
    ("01_Locomotion", "기본 동작", ["Idle_Fists", "Walk_Fists", "Run_Fists", "Jump_Full", "Dash_Forward"]),
    ("02_Reaction", "피격", ["Hit_Reaction"]),
    ("03_Utility", "상호작용", ["Potion_Drink", "Item_Throw_Overhand", "Interact_Reach", "Pickup_Low"]),
]
MAIN_NAMES = [name for _, _, names in GROUPS for name in names]
AUXILIARY_NAMES = {"Jump_Takeoff", "Jump_Rise_Loop", "Jump_Apex_Loop", "Jump_Fall_Loop", "Jump_Land"}
TRANSITION_NAMES = {"Move_Dash_Recover"}
DETAIL_NAMES = set()
# Retired preview artifacts only. Source frames, Unity clips and history are
# managed by the root task; these exact paths are cleaned only with an opt-in.
RETIRED_OUTPUT_FILES = [
    "GIF/Punch_Right_Jab.gif", "GIF/Sword_Horizontal_Slash.gif", "GIF/Staff_Cast_Forward.gif",
    "Contacts/Punch_Right_Jab.png", "Contacts/Sword_Horizontal_Slash.png", "Contacts/Staff_Cast_Forward.png",
    "Transitions/Jump_Attack_Dash.gif", "Transitions/Contacts/Jump_Attack_Dash.png",
    "Details/Staff_Grip_Side_25D.gif", "Details/Sword_Grip_Side_25D.gif",
    "Details/Contacts/Staff_Grip_Side_25D.png", "Details/Contacts/Sword_Grip_Side_25D.png",
    "Groups/02_Combat.gif", "Groups/02_Combat.png",
    "GIF/Sword_Aerial_Plunge.gif", "Auxiliary/Sword_Plunge_Fall_Loop.gif", "Auxiliary/Sword_Plunge_Land.gif",
    "Transitions/Jump_Sword_Plunge.gif", "Contacts/Sword_Aerial_Plunge.png",
    "Contacts/Sword_Plunge_Fall_Loop.png", "Contacts/Sword_Plunge_Land.png",
    "Transitions/Contacts/Jump_Sword_Plunge.png",
    "IndependentCombatQA/Jump_Sword_Plunge_0000_0017.png",
    "IndependentCombatQA/Jump_Sword_Plunge_0018_0035.png",
    "IndependentCombatQA/Jump_Sword_Plunge_0036_0052.png",
    "IndependentCombatQA/Sword_Aerial_Plunge_0000_0017.png",
    "IndependentCombatQA/Sword_Aerial_Plunge_0018_0026.png",
    "IndependentCombatQA/Pass4/Jump_Sword_Plunge_0011_0022.png",
]
RETIRED_OUTPUT_DIRECTORIES = [
    "QA/Punch_Right_Jab", "QA/Sword_Horizontal_Slash", "QA/Staff_Cast_Forward",
    "Transitions/QA/Jump_Attack_Dash", "Details/QA/Staff_Grip_Side_25D", "Details/QA/Sword_Grip_Side_25D",
    "QA/Sword_Aerial_Plunge", "QA/Sword_Plunge_Fall_Loop", "QA/Sword_Plunge_Land",
    "Transitions/QA/Jump_Sword_Plunge",
]


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def dump(path: Path, value) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def clean_retired_outputs(output: Path) -> None:
    """Remove an explicit reviewed list of generated previews, without recursion."""
    base = output.resolve()
    planned_files, planned_directories = [], []
    for relative in RETIRED_OUTPUT_FILES:
        named = base / relative
        target = named.resolve()
        if named.is_symlink() or target != named or not target.is_relative_to(base):
            raise ValueError(f"Retired output escapes the review directory: {target}")
        if target.exists():
            if not target.is_file():
                raise ValueError(f"Expected one generated preview file: {target}")
            planned_files.append(target)
    for relative in RETIRED_OUTPUT_DIRECTORIES:
        named = base / relative
        target = named.resolve()
        if named.is_symlink() or target != named or not target.is_relative_to(base):
            raise ValueError(f"Retired QA directory escapes the review directory: {target}")
        if target.exists():
            if not target.is_dir():
                raise ValueError(f"Expected a generated QA directory: {target}")
            for child in target.iterdir():
                resolved = child.resolve()
                if child.is_symlink() or resolved != child or not resolved.is_relative_to(base) or not child.is_file() or not re.fullmatch(r"\d{4,6}\.png", child.name):
                    raise ValueError(f"Unexpected content in the explicit retired QA directory: {child}")
                planned_files.append(child)
            planned_directories.append(target)
    # All targets have been checked before any mutation. Historical snapshots
    # are outside this explicit list and are never enumerated or removed.
    for path in planned_files:
        path.unlink()
    for path in planned_directories:
        path.rmdir()
    print(f"Removed {len(planned_files)} explicit retired preview files and {len(planned_directories)} empty QA directories.")


def font(size: int):
    for path in ["C:/Windows/Fonts/arial.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"]:
        if Path(path).is_file():
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()


def delays(count: int) -> list[int]:
    # A GIF delay is an integer number of centiseconds. Repeated 33 ms values
    # would be truncated by encoders and accelerate the source animation.
    boundaries = [round(index * 100 / FPS) * 10 for index in range(count + 1)]
    return [boundaries[index + 1] - boundaries[index] for index in range(count)]


def resolve_source(value: str, report_dir: Path) -> Path:
    path = Path(value)
    if path.is_absolute():
        return path.resolve()
    candidates = [PROJECT / path, report_dir / path]
    return next((candidate.resolve() for candidate in candidates if candidate.exists()), candidates[0].resolve())


def sequence(row: dict, report_dir: Path) -> list[Path]:
    if "captureRate" in row and int(row["captureRate"]) != FPS:
        raise ValueError(f"Expected an actual {FPS}Hz capture: {row['name']}")
    if "width" in row and "height" in row and (int(row["width"]), int(row["height"])) != SOURCE_SIZE:
        raise ValueError(f"Unexpected reported source resolution: {row['name']}")
    directory = resolve_source(row["framesDirectory"], report_dir)
    paths = sorted(path for path in directory.glob("*.png") if re.fullmatch(r"\d{4,6}", path.stem))
    if not paths:
        raise ValueError(f"No rendered source frames: {directory}")
    if [int(path.stem) for path in paths] != list(range(len(paths))):
        raise ValueError(f"Source sequence is not contiguous from zero: {directory}")
    if len(paths) != int(row["frameCount"]):
        raise ValueError(f"Report/source count mismatch: {row['name']}: {row['frameCount']} != {len(paths)}")
    return paths


def source_summary(paths: list[Path]) -> dict:
    stamps = []
    combined = hashlib.sha256()
    for path in paths:
        with Image.open(path) as image:
            if image.size != SOURCE_SIZE:
                raise ValueError(f"Unexpected source resolution: {path}: {image.size}")
        digest = sha256(path)
        stamps.append({"name": path.name, "sha256": digest, "bytes": path.stat().st_size})
        combined.update(f"{path.name}:{digest}\n".encode("ascii"))
    return {"directory": str(paths[0].parent), "frames": len(paths), "resolution": list(SOURCE_SIZE),
            "ordered_sha256": combined.hexdigest(), "files": stamps}


def global_palette(frames: list[Image.Image]) -> Image.Image:
    sample_count = min(48, len(frames))
    indexes = sorted(set(round(i * (len(frames) - 1) / max(1, sample_count - 1)) for i in range(sample_count)))
    atlas = Image.new("RGB", (128 * 8, 128 * math.ceil(len(indexes) / 8)), "#181f27")
    for index, source_index in enumerate(indexes):
        tile = frames[source_index].copy()
        tile.thumbnail((128, 128), Image.Resampling.LANCZOS)
        atlas.paste(tile, ((index % 8) * 128, (index // 8) * 128))
    return atlas.quantize(colors=256, method=Image.Quantize.MEDIANCUT)


def inspect_gif(path: Path) -> dict:
    with Image.open(path) as image:
        count, elapsed, duration_values = image.n_frames, 0, set()
        size = image.size
        for index in range(count):
            image.seek(index)
            duration = int(image.info.get("duration", 0))
            if duration <= 0:
                raise ValueError(f"Zero or negative GIF frame delay: {path}#{index}")
            elapsed += duration
            duration_values.add(duration)
    return {"encoded_frames": count, "duration_ms": elapsed, "resolution": list(size),
            "delay_values_ms": sorted(duration_values), "sha256": sha256(path), "bytes": path.stat().st_size}


def encode_images(frames: list[Image.Image], output: Path) -> dict:
    if not frames or len({image.size for image in frames}) != 1:
        raise ValueError(f"Empty or inconsistent GIF source: {output}")
    palette = global_palette(frames)
    indexed = [image.quantize(palette=palette, dither=Image.Dither.NONE) for image in frames]
    duration = delays(len(indexed))
    output.parent.mkdir(parents=True, exist_ok=True)
    indexed[0].save(output, save_all=True, append_images=indexed[1:], duration=duration,
                    loop=0, disposal=2, optimize=False)
    actual = inspect_gif(output)
    if actual["duration_ms"] != sum(duration):
        raise ValueError(f"GIF clock mismatch: {output}: {actual['duration_ms']} != {sum(duration)}")
    return {"file": str(output.resolve()), "source_frames": len(frames), "source_fps": FPS,
            "source_speed": 1, "expected_duration_ms": sum(duration),
            "quantization_clock_error_ms": sum(duration) - len(frames) * 1000 / FPS,
            "duplicate_frame_note": "The encoder may merge identical images while preserving their combined delay.",
            "method": "Ordered actual Unity PNGs, one stable palette and cumulative 30/40ms clock; no pose or time interpolation.",
            **actual}


def export_one(paths: list[Path], output: Path) -> dict:
    frames = []
    for path in paths:
        with Image.open(path) as image:
            frames.append(image.convert("RGB"))
    return encode_images(frames, output)


class ReuseMedia:
    """Copy verified unchanged preview bytes from a separate previous export."""
    def __init__(self, root: Path | None):
        self.root = root.resolve() if root else None
        self.manifest = json.loads((self.root / "export-manifest.json").read_text(encoding="utf-8-sig")) if self.root else {}
        rows = self.manifest.get("motions", []) + self.manifest.get("transitions", []) + self.manifest.get("details", [])
        self.rows = {(r.get("category", "Motion"), r["name"]): r for r in rows}
        first = next(iter(self.manifest.get("motions", [])), None)
        self.original_root = Path(first["file"]).parent.parent if first else None

    def path(self, value: str) -> Path:
        old = Path(value)
        if self.root and self.original_root:
            try:
                relocated = self.root / old.relative_to(self.original_root)
                if relocated.is_file():
                    return relocated
            except ValueError:
                pass
        return old

    def row(self, category: str, name: str, source: dict) -> dict | None:
        old = self.rows.get((category, name))
        if not old or old.get("source", {}).get("ordered_sha256") != source["ordered_sha256"]:
            return None
        if old.get("source_frames") != source["frames"] or old.get("source_fps") != FPS or old.get("source_speed") != 1:
            return None
        return old

    @staticmethod
    def copy(source: Path, destination: Path) -> None:
        destination.parent.mkdir(parents=True, exist_ok=True)
        if source.resolve() != destination.resolve():
            shutil.copy2(source, destination)

    def gif(self, old: dict | None, destination: Path) -> dict | None:
        if not old:
            return None
        source = self.path(old["file"])
        if not source.is_file() or sha256(source) != old.get("sha256"):
            return None
        self.copy(source, destination)
        actual = inspect_gif(destination)
        fields = ("source_frames", "source_fps", "source_speed", "expected_duration_ms",
                  "quantization_clock_error_ms", "duplicate_frame_note", "method")
        return {**{key: old[key] for key in fields if key in old}, **actual,
                "file": str(destination.resolve()), "reused_unchanged_gif": True,
                "reused_from": str(source.resolve())}

    def contacts(self, old: dict | None, output: Path, name: str, qa: bool, frame_count: int):
        if not old or not old.get("contact"):
            return None
        source = self.path(old["contact"])
        old_pages = [self.path(p) for p in old.get("qa", [])]
        if not source.is_file() or (qa and (len(old_pages) != math.ceil(frame_count / 36) or not all(p.is_file() for p in old_pages))):
            return None
        overview = output / "Contacts" / f"{name}.png"
        self.copy(source, overview)
        pages = []
        if qa:
            for source in old_pages:
                destination = output / "QA" / name / source.name
                self.copy(source, destination)
                pages.append(destination)
        return overview, pages

    def group(self, name: str, names: list[str], sources: dict[str, dict], destination: Path) -> dict | None:
        old = next((g for g in self.manifest.get("groups", []) if g["name"] == name and g.get("motions") == names), None)
        if not old or not all(self.row("Motion", n, sources[n]) for n in names):
            return None
        poster = self.path(old.get("poster", ""))
        if not poster.is_file():
            return None
        result = self.gif(old, destination)
        if result is None:
            return None
        target = destination.with_suffix(".png")
        self.copy(poster, target)
        return {**result, "poster": str(target.resolve()), "motions": names}


def fit_tile(path: Path, size: tuple[int, int]) -> Image.Image:
    tile = Image.new("RGB", size, "#181f27")
    with Image.open(path) as image:
        thumb = image.convert("RGB")
        thumb.thumbnail(size, Image.Resampling.LANCZOS)
    tile.paste(thumb, ((size[0] - thumb.width) // 2, (size[1] - thumb.height) // 2))
    return tile


def contact(paths: list[Path], output: Path, indexes: list[int], columns: int, title: str) -> None:
    tile, label, margin, heading = 180, 25, 6, 35
    rows = math.ceil(len(indexes) / columns)
    canvas = Image.new("RGB", (columns * (tile + margin) + margin, rows * (tile + label + margin) + margin + heading), "#181f27")
    draw = ImageDraw.Draw(canvas)
    draw.text((margin, 8), title, font=font(17), fill="#e2eaf5")
    for cell, index in enumerate(indexes):
        x, y = margin + cell % columns * (tile + margin), heading + margin + cell // columns * (tile + label + margin)
        canvas.paste(fit_tile(paths[index], (tile, tile)), (x, y))
        draw.text((x + 3, y + tile + 3), f"{index:04d} | {index / FPS:.3f}s", font=font(13), fill="#c9d6e5")
    output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output)


def export_contacts(paths: list[Path], output: Path, name: str, qa: bool) -> tuple[Path, list[Path]]:
    indexes = [round(index * (len(paths) - 1) / 11) for index in range(12)]
    overview = output / "Contacts" / f"{name}.png"
    contact(paths, overview, indexes, 4, f"{name} | 12 sampled poses")
    pages = []
    if qa:
        for start in range(0, len(paths), 36):
            page = output / "QA" / name / f"{start:04d}.png"
            contact(paths, page, list(range(start, min(start + 36, len(paths)))), 6,
                    f"{name} | consecutive 30fps | {start:04d}-{min(start + 35, len(paths) - 1):04d}")
            pages.append(page)
    return overview, pages


def montage(items: list[dict], paths_by_name: dict[str, list[Path]], output: Path) -> dict:
    columns = 1 if len(items) == 1 else 3 if len(items) > 4 else 2
    tile, label, gap = 320, 34, 10
    rows = math.ceil(len(items) / columns)
    size = (columns * (tile + gap) + gap, rows * (tile + label + gap) + gap)
    sequences = [[fit_tile(path, (tile, tile)) for path in paths_by_name[item["name"]]] for item in items]
    count = max(map(len, sequences))
    frames = []
    for tick in range(count):
        canvas = Image.new("RGB", size, "#181f27")
        draw = ImageDraw.Draw(canvas)
        for cell, (item, images) in enumerate(zip(items, sequences)):
            x, y = gap + cell % columns * (tile + gap), gap + cell // columns * (tile + label + gap)
            # At the same 30Hz sample rate each cell advances exactly one source
            # frame per tick; only its complete original capture repeats.
            canvas.paste(images[tick % len(images)], (x, y))
            draw.text((x + 2, y + tile + 7), item["name"], font=font(15), fill="#e2eaf5")
        frames.append(canvas)
    result = encode_images(frames, output)
    poster = output.with_suffix(".png")
    frames[min(len(frames) - 1, round(len(frames) * .35))].save(poster)
    return {**result, "poster": str(poster.resolve()), "motions": [item["name"] for item in items],
            "method": "Original source PNGs uniformly downscaled and tiled. Every cell advances at its unchanged 30Hz clock and repeats independently. This is a review montage, not a gameplay sequence."}


def href(path: Path, output: Path) -> str:
    return quote(os.path.relpath(path.resolve(), output.resolve()).replace("\\", "/"), safe="/._-")


def gallery(output: Path, report_path: Path, motion_rows: list[dict], transition_rows: list[dict], detail_rows: list[dict],
            artifacts: dict[str, dict], groups: list[dict], partial: bool) -> Path:
    esc = html.escape
    group_html = []
    for group in groups:
        group_html.append(f'<figure><h3>{esc(group["label"])}</h3><img src="{href(Path(group["file"]), output)}" alt="{esc(group["label"])}"><figcaption>각 클립의 원속도 · 독립 반복</figcaption></figure>')

    def card(row: dict, category: str = "Motion") -> str:
        name = row["name"]
        key = category + "/" + name
        item = artifacts[key]
        links = [f'<a href="{href(Path(item["contact"]), output)}">12포즈</a>']
        links.extend(f'<a href="{href(Path(page), output)}">연속 QA {i + 1}</a>' for i, page in enumerate(item["qa"]))
        asset = PROJECT / "Assets/Art/Characters/Protagonist01/ChibiMotion_v007/Clips" / f"{name}.anim"
        if category == "Motion" and asset.is_file():
            links.append(f'<a href="{href(asset, output)}">Unity 클립</a>')
        authored = row.get("durationSeconds", row.get("duration", 0))
        text = f'clip {float(authored):.3f}s · capture {item["capture_seconds"]:.3f}s · {"loop" if row.get("loop") else "finite"} · {item["source_frames"]} source frames'
        if item["end_pose_hold_frames"]:
            text += f' · character endpoint hold {item["end_pose_hold_seconds"]:.3f}s'
        pipeline_note = '<p class="note">저장 클립의 포즈 혼합 뒤 실제 스킨 발바닥의 지면 접촉을 오프라인으로 보정한 프리뷰입니다. 런타임에 연결한 결과가 아닙니다.</p>' if category == "Transition" and name == "Move_Dash_Recover" else ""
        if category == "Detail":
            pipeline_note = '<p class="note">실제 저장 클립의 스킨과 무기를 같은 2.5D 측면 각도에서 손잡이 중심으로 확대 렌더한 접촉 검수입니다. 합성 이미지나 별도 게임 모션이 아닙니다.</p>'
        return f'<article id="{esc(name)}"><h3>{esc(name)}</h3><p>{esc(str(row.get("intent", "")))}</p>{pipeline_note}<p class="meta">{text}</p><img loading="lazy" src="{href(Path(item["file"]), output)}" alt="{esc(name)}"><p>{" · ".join(links)}</p></article>'

    main = "\n".join(card(row) for row in motion_rows if not row.get("auxiliary", False))
    auxiliary = "\n".join(card(row) for row in motion_rows if row.get("auxiliary", False))
    transitions = "\n".join(card(row, "Transition") for row in transition_rows)
    details = "\n".join(card(row, "Detail") for row in detail_rows)
    detail_nav = '<a href="#details">손·손잡이 실제 접촉 확대</a>' if detail_rows else ""
    detail_section = ('<h2 id="details">손·손잡이 실제 접촉 확대</h2>'
                      '<p class="note">손목 점의 일치와 별개로 손바닥·엄지·굽은 손가락과 손잡이의 실제 관계를 확인하는 확대입니다. '
                      '원본 720×720·30fps·1배속이며 기본 검수와 같은 측면 카메라 각도에서 더 좁게 잡았습니다. '
                      '기존 기본 클립 개수에 추가되는 행동이 아니며, 접촉 수치만으로 자연스러움을 판정하지 않습니다.</p>'
                      f'<div class="grid">{details}</div>') if detail_rows else ""
    label = "부분 진단 캡처" if partial else "전체 모션 검수"
    asset_readme = PROJECT / "Assets/Art/Characters/Protagonist01/ChibiMotion_v007/README_ko.txt"
    asset_readme_link = f'<a href="{href(asset_readme, output)}">클립·리그 인수 안내</a>' if asset_readme.is_file() else ""
    camera_row = next((row for row in motion_rows if not row.get("auxiliary")), {})
    position, target = camera_row.get("cameraPosition", {}), camera_row.get("cameraTarget", {})
    offset = [float(position.get(axis, 0)) - float(target.get(axis, 0)) for axis in "xyz"]
    camera_text = (f'orthographic {float(camera_row.get("orthographicSize", 0)):.2f}, '
                   f'target Y {float(target.get("y", 0)):.2f}, '
                   f'offset ({offset[0]:.2f}, {offset[1]:.2f}, {offset[2]:.2f})')
    audit_links = "".join(f'<a href="{href(path, output)}">{label}</a>' for label, path in [
        ("이번 달리기 수정 검수", HERE / "run-revision-qa.txt"),
        ("이번 달리기 접촉·보존 검증", HERE / "run-revision-qa.json"),
    ] if path.is_file())
    document = f'''<!doctype html>
<html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Chibi v007 · Compact Action Motion Review</title>
<style>body{{margin:0;background:#121922;color:#e0e8f4;font:16px/1.65 system-ui,sans-serif}}main{{max-width:1120px;margin:auto;padding:26px 22px 64px}}h1{{font-size:27px}}h2{{margin-top:42px}}h3{{font-size:18px;overflow-wrap:anywhere}}a{{color:#91d7da}}p{{margin:.65em 0}}.meta,.note{{color:#aabdd1}}figure,article{{margin:0;background:#1a232e;padding:14px;border:1px solid #2b394b}}figure{{margin:20px 0}}figure img{{display:block;width:100%;height:auto}}.grid{{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:16px}}article img{{display:block;width:100%;height:auto}}nav{{display:flex;gap:14px;flex-wrap:wrap}}@media(max-width:720px){{.grid{{grid-template-columns:1fr}}}}</style>
<main><h1>Chibi v007 · {label}</h1>
<p>작고 명료한 동작과 짧은 준비·발력·회복을 중심으로 편집한 새 치비 캐릭터의 실제 Unity 렌더입니다.</p>
<p class="note">고정 2.5D 측면 · 원본 720×720 · 30fps · 1× 재생. 기본 전신 카메라 기준: {esc(camera_text)}. 원본 프레임의 순서와 재생시간을 유지합니다. GIF의 색상은 한 팔레트로 양자화되며 시간은 누적 30/40ms로 표현됩니다.</p>
<p class="note">게임 플레이어·입력·공격 판정·Animator Controller에는 연결하지 않은 개별 클립 검수입니다. VFX는 이번 결과에 포함하지 않습니다. 유한 행동 GIF의 재시작과 묶음 안의 독립 반복은 게임 상태 전환을 의미하지 않습니다.</p>
<p class="note">맨손·검·지팡이의 고정 메시 프리팹 3개와 무기 모델은 보존했습니다. 현재 제공하는 모션은 기본 10개와 점프 단계 5개이며, 공격 클립과 무기 공격 확대 프리뷰는 제외했습니다. 실제 게임의 구매·장착 로직에는 연결하지 않았습니다.</p>
<p class="note">기본 유한 행동은 동작 종료 후 캐릭터의 끝 자세를 8프레임(약 .267초) 유지한 실제 Unity 캡처를 포함합니다. 행동 구간의 재생은 1배속이며, 클립 길이와 정지 포함 캡처 길이를 각 카드에 따로 표시합니다. 보조 클립과 전환에는 이 추가 정지를 적용하지 않습니다.</p>
<nav><a href="#groups">묶음</a><a href="#main">기본 10개</a><a href="#auxiliary">점프 단계 5개</a><a href="#transitions">저장 클립 전환 1개</a>{detail_nav}<a href="{href(report_path, output)}">실제 캡처 기록</a><a href="export-manifest.json">내보내기 기록</a><a href="delivery-validation.json">전달 파일 검증</a><a href="README_ko.txt">사용 범위</a>{asset_readme_link}{audit_links}</nav>
<h2>점프·낙하와 물리 이동의 구분</h2>
<p>Jump_Full은 점프 전체를 보여주는 샘플로 Hips의 수직 궤적을 포함합니다. 분리된 Jump 보조 클립5개는 해당 공중 높이를 제거한 포즈 단계입니다. 게임의 Rigidbody/캐릭터 이동으로 높이를 제어할 때는 이 구분을 지켜 수직 이동을 중복 적용하지 않아야 합니다. 전환 GIF도 저장 클립을 샘플링·혼합한 검수이며 물리·충돌·입력 응답을 시험한 결과는 아닙니다.</p>
<h2 id="groups">원속도 묶음</h2>{"".join(group_html)}
<h2 id="main">기본 모션</h2><div class="grid">{main}</div>
{detail_section}
<h2 id="auxiliary">점프·착지 분리 클립 5개</h2><p class="note">수직 이동을 별도 제어할 담당자를 위한 단계별 자산입니다. 동일 자세를 유지하는 루프에는 의도적으로 중복 프레임이 있을 수 있습니다.</p><div class="grid">{auxiliary}</div>
<h2 id="transitions">저장 클립 사이의 전환 확인</h2><p class="note">별도 검수 장면에서 실제 저장한 클립을 혼합한 결과입니다. 지상 Move_Dash_Recover에는 포즈 혼합 후 실제 스킨 발바닥의 지면 접촉을 오프라인에서 보정하는 단계가 추가됩니다. 이 보정은 프리뷰 전용이며 게임 Controller나 런타임에 연결하지 않았습니다. 모든 인게임 전환 상황의 통과를 의미하지 않습니다.</p><div class="grid">{transitions}</div>
</main></html>'''
    path = output / "index.html"
    with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", dir=output,
                                     prefix=".gallery-", suffix=".tmp", delete=False) as stream:
        stream.write(document)
        temporary = Path(stream.name)
    os.replace(temporary, path)
    return path


class LocalLinks(HTMLParser):
    def __init__(self):
        super().__init__()
        self.links = []

    def handle_starttag(self, tag, attrs):
        for key, value in attrs:
            if key in ("src", "href") and value:
                self.links.append(value)


def validate(output: Path, report_path: Path, manifest: dict, expected_auxiliary: int, partial: bool) -> dict:
    checks = []
    def check(name: str, passed: bool, detail=None):
        checks.append({"check": name, "passed": bool(passed), "detail": detail})

    check("capture_report_hash", sha256(report_path) == manifest["capture_report_sha256"])
    rows = manifest["motions"]
    main_names = {row["name"] for row in rows if not row["auxiliary"]}
    aux_names = {row["name"] for row in rows if row["auxiliary"]}
    detail_rows = manifest.get("details", [])
    detail_names = {row["name"] for row in detail_rows}
    check("unique_motion_names", len({row["name"] for row in rows}) == len(rows))
    check("unique_detail_names", len(detail_names) == len(detail_rows))
    check("supported_optional_detail_names", detail_names.issubset(DETAIL_NAMES), sorted(detail_names))
    if not partial:
        check("main_ten_names", main_names == set(MAIN_NAMES), sorted(main_names))
        check("auxiliary_count", len(aux_names) == expected_auxiliary, len(aux_names))
        check("auxiliary_five_names", aux_names == AUXILIARY_NAMES, sorted(aux_names))
        check("transition_one_name", {row["name"] for row in manifest["transitions"]} == TRANSITION_NAMES)
        check("main_three_groups", len([group for group in manifest["groups"] if not group.get("auxiliary")]) == 3)
        check("retired_active_previews_absent", not any((output / relative).exists() for relative in RETIRED_OUTPUT_FILES + RETIRED_OUTPUT_DIRECTORIES))
    for row in manifest["motions"] + manifest["transitions"] + detail_rows:
        path = Path(row["file"])
        decoded = inspect_gif(path)
        prefix = row["category"] + "/" + row["name"]
        check(prefix + "/hash", decoded["sha256"] == row["sha256"])
        check(prefix + "/size", decoded["resolution"] == list(SOURCE_SIZE))
        check(prefix + "/duration", decoded["duration_ms"] == sum(delays(row["source_frames"])), decoded["duration_ms"])
        check(prefix + "/source_speed", row["source_speed"] == 1)
        check(prefix + "/source_count", row["source_frames"] == row["source"]["frames"])
        if row["category"] == "Motion":
            expected_hold = 0 if row["loop"] or row["auxiliary"] else FINITE_MAIN_HOLD_FRAMES
            if not partial:
                check(prefix + "/endpoint_hold", row["end_pose_hold_frames"] == expected_hold, row["end_pose_hold_frames"])
            cycles = 2 if row["loop"] else 1
            expected_count = round(float(row["duration_seconds"]) * FPS * cycles) + expected_hold
            if not partial:
                check(prefix + "/authored_capture_count", row["source_frames"] == expected_count, expected_count)
        check(prefix + "/clock_error", abs(row["quantization_clock_error_ms"]) <= 5.001)
        check(prefix + "/contact", Path(row["contact"]).is_file())
        check(prefix + "/qa_pages", all(Path(page).is_file() for page in row["qa"]), len(row["qa"]))
        # Verify the PNGs again after export. This is a source immutability check,
        # not a judgement of pose quality or motion naturalness.
        check(prefix + "/sources_unchanged", all(
            sha256(Path(row["source"]["directory"]) / frame["name"]) == frame["sha256"]
            for frame in row["source"]["files"]))
    for row in manifest["groups"]:
        decoded = inspect_gif(Path(row["file"]))
        check(row["name"] + "/hash", decoded["sha256"] == row["sha256"])
        check(row["name"] + "/duration", decoded["duration_ms"] == row["expected_duration_ms"])
        check(row["name"] + "/poster", Path(row["poster"]).is_file())
    parser = LocalLinks()
    parser.feed((output / "index.html").read_text(encoding="utf-8"))
    local_count = 0
    for link in parser.links:
        parsed = urlsplit(link)
        if parsed.scheme or parsed.netloc or not parsed.path:
            continue
        local_count += 1
        target = (output / unquote(parsed.path)).resolve()
        # delivery-validation.json is written immediately after this function.
        exists = target == (output / "delivery-validation.json").resolve() or target.exists()
        check("html_link/" + link, exists)
    failed = [item for item in checks if not item["passed"]]
    result = {"validated_utc": datetime.now(timezone.utc).isoformat(), "success": not failed,
              "scope": "Media delivery integrity only; not an automatic naturalness or gameplay approval.",
              "partial_diagnostic": partial, "checks_count": len(checks), "failed_checks": failed,
              "main_motion_gifs": len(main_names), "auxiliary_motion_gifs": len(aux_names),
              "transition_gifs": len(manifest["transitions"]), "montage_gifs": len(manifest["groups"]),
              "detail_gifs": len(detail_rows), "detail_names": sorted(detail_names),
              "local_html_links": local_count, "capture_report_sha256": sha256(report_path),
              "export_manifest_sha256": sha256(output / "export-manifest.json"), "checks": checks}
    dump(output / "delivery-validation.json", result)
    if failed:
        raise AssertionError(f"Delivery validation failed ({len(failed)}): {failed[:3]}")
    return result


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--report", type=Path, default=HERE / "review-report.json")
    parser.add_argument("--output", type=Path, default=HERE / "ReviewOutput")
    parser.add_argument("--qa", action="store_true", help="Also emit every source frame in consecutive 6x6 QA pages.")
    parser.add_argument("--contacts-only", action="store_true", help="Write contact/QA PNGs only; no GIFs or final gallery.")
    parser.add_argument("--allow-partial", action="store_true", help="Diagnostic report only; allow fewer than ten main/five auxiliary motions.")
    parser.add_argument("--reuse-from", type=Path, help="Separate previous export folder; unchanged verified GIFs/QA are copied byte-for-byte.")
    parser.add_argument("--expected-auxiliary", type=int, default=5)
    parser.add_argument("--clean-retired-output", action="store_true", help="After root has archived the prior preview, remove only the explicit retired generated outputs.")
    args = parser.parse_args()
    report_path, output = args.report.resolve(), args.output.resolve()
    if args.reuse_from and args.reuse_from.resolve() == output:
        raise ValueError("Reuse requires a separate previous export folder; fresh outputs must not overwrite mapped media.")
    reuse = ReuseMedia(args.reuse_from)
    if not output.is_relative_to(HERE.resolve()):
        raise ValueError("Output must remain inside the isolated v007 report directory.")
    report = json.loads(report_path.read_text(encoding="utf-8-sig"))
    motions, transitions, details = report.get("motions", []), report.get("transitions", []), report.get("details", [])
    if not motions:
        raise ValueError("No captured motions in the completed report.")
    if len({row["name"] for row in motions}) != len(motions):
        raise ValueError("Duplicate motion names in report.")
    if not isinstance(details, list):
        raise ValueError("Optional details must be a CaptureInfo array.")
    detail_names = {row["name"] for row in details}
    if len(detail_names) != len(details) or not detail_names.issubset(DETAIL_NAMES):
        raise ValueError("Current delivery must have no retired weapon-attack detail captures.")
    if not args.allow_partial:
        names = {row["name"] for row in motions if not row.get("auxiliary", False)}
        auxiliaries = {row["name"] for row in motions if row.get("auxiliary", False)}
        transition_names = {row["name"] for row in transitions}
        if names != set(MAIN_NAMES) or auxiliaries != AUXILIARY_NAMES or transition_names != TRANSITION_NAMES:
            raise ValueError("Active final report must contain exactly ten main, five jump auxiliary, one transition, and no weapon-detail captures.")
    if args.clean_retired_output:
        if args.allow_partial:
            raise ValueError("Retired preview cleanup requires a complete final capture report.")
        clean_retired_outputs(output)
    output.mkdir(parents=True, exist_ok=True)
    paths_by_name, sources_by_name, artifact_by_key = {}, {}, {}
    manifest = {"created_utc": datetime.now(timezone.utc).isoformat(), "capture_report": str(report_path),
                "capture_report_sha256": sha256(report_path), "source_fps": FPS, "source_speed": 1,
                "source_resolution": list(SOURCE_SIZE), "partial_diagnostic": args.allow_partial,
                "finite_main_endpoint_hold_frames": FINITE_MAIN_HOLD_FRAMES,
                "reuse_from": str(args.reuse_from.resolve()) if args.reuse_from else None,
                "supported_optional_detail_names": sorted(DETAIL_NAMES),
                "motions": [], "transitions": [], "details": [], "groups": []}
    for category, rows in [("Motion", motions), ("Transition", transitions), ("Detail", details)]:
        for row in rows:
            name = row["name"]
            if not re.fullmatch(r"[A-Za-z0-9_-]+", name):
                raise ValueError(f"Unsafe output stem: {name}")
            paths = sequence(row, report_path.parent)
            source = source_summary(paths)
            reported_capture = row.get("captureSeconds")
            if reported_capture is not None and abs(float(reported_capture) - len(paths) / FPS) > 1 / FPS + .001:
                raise ValueError(f"Capture duration/count mismatch: {name}")
            if category == "Motion":
                paths_by_name[name] = paths
                sources_by_name[name] = source
            contact_dir = output if category == "Motion" else output / ("Transitions" if category == "Transition" else "Details")
            reusable = reuse.row(category, name, source)
            overview, qa_pages = reuse.contacts(reusable, contact_dir, name, args.qa, len(paths)) or export_contacts(paths, contact_dir, name, args.qa)
            if args.contacts_only:
                print(f"Contact {category}/{name}: {len(paths)} actual frames")
                continue
            subdirectory = ("Details" if category == "Detail" else "Transitions" if category == "Transition"
                            else "Auxiliary" if row.get("auxiliary", False) else "GIF")
            gif_path = output / subdirectory / f"{name}.gif"
            result = reuse.gif(reusable, gif_path) or export_one(paths, gif_path)
            authored_duration = row.get("durationSeconds", row.get("duration", 0))
            hold_frames = max(0, len(paths) - round(float(authored_duration) * FPS)) if (
                category == "Motion" and not row.get("loop", False) and not row.get("auxiliary", False)) else 0
            item = {"name": name, "category": category, "auxiliary": bool(row.get("auxiliary", False)),
                    "intent": row.get("intent", ""), "prop": row.get("prop", ""), "loop": bool(row.get("loop", False)),
                    "duration_seconds": authored_duration,
                    "capture_seconds": len(paths) / FPS, "end_pose_hold_frames": hold_frames,
                    "end_pose_hold_seconds": hold_frames / FPS, "source": source,
                    "capture_pipeline": ("Saved-clip pose blending plus offline skin-sole contact correction; preview only, no runtime integration." if category == "Transition" and name == "Move_Dash_Recover"
                                         else "Actual saved-clip skin and weapon grip close-up; same fixed 2.5D side-view angle, tighter framing; preview only." if category == "Detail"
                                         else "Actual saved-clip Unity capture; offline pose blending for transitions."),
                    "contact": str(overview.resolve()), "qa": [str(path.resolve()) for path in qa_pages], **result}
            manifest[{"Motion": "motions", "Transition": "transitions", "Detail": "details"}[category]].append(item)
            artifact_by_key[category + "/" + name] = item
            print(f"GIF {category}/{name}: {len(paths)} PNGs, {result['duration_ms']}ms")
    if args.contacts_only:
        print("Diagnostic contact export complete; no final GIF/gallery manifest was written.")
        return
    by_name = {row["name"]: row for row in motions}
    for stem, label, names in GROUPS:
        selected = [by_name[name] for name in names if name in by_name]
        if not selected:
            continue
        group_path = output / "Groups" / f"{stem}.gif"
        result = reuse.group(stem, [row["name"] for row in selected], sources_by_name, group_path) or montage(selected, paths_by_name, group_path)
        manifest["groups"].append({"name": stem, "label": label, "auxiliary": False, **result})
    auxiliary = [row for row in motions if row.get("auxiliary", False)]
    if auxiliary:
        group_path = output / "Groups" / "04_Air_Phases.gif"
        result = reuse.group("04_Air_Phases", [row["name"] for row in auxiliary], sources_by_name, group_path) or montage(auxiliary, paths_by_name, group_path)
        manifest["groups"].append({"name": "04_Air_Phases", "label": "점프·착지 분리 단계", "auxiliary": True, **result})
    dump(output / "export-manifest.json", manifest)
    (output / "README_ko.txt").write_text(
        "v007 실제 Unity 모션 검수 GIF\n\n"
        "원본 PNG 순서를 유지하고 30fps·1배속으로 기록했다. GIF의10ms 단위는 누적30/40ms로 보정한다. "
        "정지 포즈가 같은 연속 프레임은 인코더가 합칠 수 있으나 합산 재생시간은 보존한다.\n"
        "기본 유한 행동은 끝 자세8프레임(약.267초) 유지의 실제Unity캡처를 포함한다. 행동 구간을 늦추지 않고 클립과 정지 포함 캡처 길이를 따로 기록한다. "
        "보조 클립과 전환에는 이 추가 끝 자세 정지를 적용하지 않는다.\n"
        "개별 GIF는 원본720x720이고 묶음만 균일 축소한다. 각 묶음 셀은 자기 캡처 길이대로 독립 반복한다.\n"
        "기본10개·점프단계5개·전환1개를 제공한다. 공격3클립과 이에 의존한 공격전환·무기공격확대는 제외했다. "
        "Jump_Full은 샘플 수직Hips 궤적을 포함한다. 분리된 점프·착지 보조 클립에는 해당 높이를 제거했다. "
        "실제 게임 이동과 중복 적용하지 않아야 한다. 본 결과는 물리나 입력·충돌·상태전환 게임 코드의 완성이 아니다.\n"
        "Transitions는 저장 클립의 실제 샘플링·혼합 검수다. 모든 인게임 전환을 검증한 것이 아니다.\n"
        "Move_Dash_Recover는 저장 클립 포즈 혼합 뒤 실제 스킨 발바닥의 지면 접촉을 오프라인에서 보정한다. "
        "이 보정은 프리뷰에만 쓰이며 런타임에 연결한 것이 아니다.\n"
        "맨손·검·지팡이 모델과 고정 프리팹은 보존했다. 이번 결과에는 무기공격 확대영상을 제공하지 않는다.\n"
        "Contacts는12포즈 표본이며 --qa는 빠짐없는 원본 순서의6x6 연속 페이지를 추가한다. "
        "수치·파일 검증은 자연스러움 점수가 아니다. 기존 모델·VFX·원본 PNG는 변경하지 않는다.\n",
        encoding="utf-8")
    gallery(output, report_path, motions, transitions, details, artifact_by_key, manifest["groups"], args.allow_partial)
    result = validate(output, report_path, manifest, args.expected_auxiliary, args.allow_partial)
    print(f"Verified {result['checks_count']} delivery checks. Gallery: {output / 'index.html'}")


if __name__ == "__main__":
    main()
