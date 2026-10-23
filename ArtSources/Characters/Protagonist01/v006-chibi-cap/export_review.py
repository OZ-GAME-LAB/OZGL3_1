"""Export actual Unity v006 PNG renders without synthesizing or retiming poses."""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import html
import json
import os
from pathlib import Path
import re
from urllib.parse import quote, unquote, urlsplit
import sys

sys.dont_write_bytecode = True
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
OUT = HERE / "ReviewOutput"
VIEWS = [
    ("Front", "Front"), ("ThreeQuarter", "Three-quarter"),
    ("Side_Orthographic", "Pure side view"), ("Side_2p5D", "2.5D side view"),
    ("Back", "Back"),
]


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def timings(count: int, fps: float) -> list[int]:
    # GIF delays use centiseconds; cumulative rounding preserves the authored clock.
    points = [round(i * 100 / fps) * 10 for i in range(count + 1)]
    return [points[i + 1] - points[i] for i in range(count)]


def png_sequence(directory: Path, expected: int | None = None) -> list[Path]:
    files = sorted(p for p in directory.glob("*.png") if re.fullmatch(r"\d{4,6}", p.stem))
    if expected is not None and len(files) != expected:
        raise ValueError(f"Expected {expected} source frames in {directory}; found {len(files)}")
    if not files:
        raise ValueError(f"No actual Unity PNG source frames in {directory}")
    indexes = [int(p.stem) for p in files]
    if indexes != list(range(len(files))):
        raise ValueError(f"Noncontiguous source frames: {directory}")
    return files


def export_gif(files: list[Path], destination: Path, fps: float, expected_size=None) -> dict:
    if fps <= 0 or fps > 100:
        raise ValueError("GIF source fps must be >0 and <=100")
    with Image.open(files[0]) as first:
        size = first.size
    if expected_size is not None and size != expected_size:
        raise ValueError(f"Expected {expected_size}, received {size}: {files[0]}")
    # One global palette prevents frame-specific quantization from changing colors.
    sample = Image.new("RGB", (128 * 6, 128 * 4), "#192027")
    selected = sorted(set(round(i * (len(files) - 1) / 23) for i in range(24)))
    for i, index in enumerate(selected):
        with Image.open(files[index]) as source:
            tile = source.convert("RGB")
            tile.thumbnail((128, 128))
            sample.paste(tile, ((i % 6) * 128, (i // 6) * 128))
    palette = sample.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
    frames = []
    for path in files:
        with Image.open(path) as source:
            if source.size != size:
                raise ValueError(f"Mismatched frame resolution: {path}")
            frames.append(source.convert("RGB").quantize(palette=palette, dither=Image.Dither.NONE))
    delays = timings(len(frames), fps)
    destination.parent.mkdir(parents=True, exist_ok=True)
    frames[0].save(destination, save_all=True, append_images=frames[1:], duration=delays,
                   loop=0, disposal=2, optimize=False)
    with Image.open(destination) as check:
        encoded_count = check.n_frames
        actual_delay = 0
        for index in range(encoded_count):
            check.seek(index)
            actual_delay += check.info.get("duration", 0)
    target_delay = sum(delays)
    if actual_delay != target_delay:
        raise AssertionError(f"GIF duration mismatch: {actual_delay} != {target_delay}")
    return {
        "file": str(destination), "sha256": digest(destination),
        "source_directory": str(files[0].parent), "source_frame_count": len(files),
        "encoded_frame_count": encoded_count, "resolution": list(size),
        "source_fps": fps, "source_speed": 1, "duration_ms": actual_delay,
        "method": "Actual ordered Unity PNGs, no pose interpolation or time scaling; one palette, cumulative centisecond timing.",
    }


def contact_sheet() -> Path:
    tile_size = 480
    label_height = 40
    sheet = Image.new("RGB", (tile_size * 2, (tile_size + label_height) * 3), "#191f26")
    font = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 21)
    draw = ImageDraw.Draw(sheet)
    for index, (stem, label) in enumerate(VIEWS):
        path = OUT / "Views" / f"{stem}.png"
        with Image.open(path) as source:
            if source.size != (720, 720):
                raise ValueError(f"Unexpected view size: {path} {source.size}")
            thumb = source.convert("RGB").resize((tile_size, tile_size), Image.Resampling.LANCZOS)
        x = (index % 2) * tile_size
        y = (index // 2) * (tile_size + label_height)
        draw.text((x + 14, y + 10), label, font=font, fill="#e4ebf4")
        sheet.paste(thumb, (x, y + label_height))
    path = OUT / "Model_Views_Contact.png"
    sheet.save(path)
    return path


def local_link(path: Path) -> str:
    return quote(os.path.relpath(path.resolve(), OUT.resolve()).replace("\\", "/"), safe="/._-")


def gallery(turntable: Path, elastic: Path | None) -> Path:
    model_dir = ROOT / "Assets/Art/Characters/Protagonist01/ChibiCap_v006"
    links = [
        ("FBX 모델", model_dir / "Model/Protagonist_ChibiCap.fbx"),
        ("GLB 원본", HERE / "Protagonist_ChibiCap.glb"),
        ("모델 프리팹", model_dir / "Prefabs/Protagonist_ChibiCap_Model.prefab"),
        ("리깅·주먹 프리팹", model_dir / "Prefabs/Protagonist_ChibiCap_Rig.prefab"),
        ("탄성 펀치 테스트 클립", model_dir / "Clips/Punch_Elastic_Preview.anim"),
        ("생성 기준 이미지", HERE / "Protagonist_ChibiCap_Concept.png"),
        ("Unity 모델 계측", OUT / "model-review.json"),
        ("레퍼런스 재분석", HERE / "research/reference-analysis-2026-10-21.txt"),
        ("시각 검수", HERE / "research/visual-audit.txt"),
        ("생성 기록", HERE / "generation.json"),
    ]
    available = [(label, path) for label, path in links if path.is_file()]
    nav = " · ".join(f'<a href="{local_link(path)}">{html.escape(label)}</a>' for label, path in available)
    cards = "\n".join(
        f'<figure><img src="Views/{stem}.png" alt="{label}" loading="lazy"><figcaption>{label}</figcaption></figure>'
        for stem, label in VIEWS)
    elastic_markup = ""
    if elastic:
        neutral = HERE / "ElasticReview/Neutral_Front.png"
        neutral_side = HERE / "ElasticReview/Neutral_Side_2p5D.png"
        neutral_markup = ""
        if neutral.is_file() and neutral_side.is_file():
            neutral_markup = f'<h2>주먹을 쥔 기본 자세</h2><div class="grid"><figure><img src="{local_link(neutral)}" alt="Rigged neutral front"><figcaption>정면 · 별도 리그/주먹 메시 적용</figcaption></figure><figure><img src="{local_link(neutral_side)}" alt="Rigged neutral side"><figcaption>2.5D 측면 · 기본 자세</figcaption></figure></div>'
        elastic_markup = f'<h2>탄성 액션 방향 테스트</h2><p>샘플 전용 개별 클립의 실제 Unity 렌더입니다. 게임에 연결한 결과가 아닙니다.</p><img class="hero" src="{local_link(elastic)}" alt="Elastic punch sample">' + neutral_markup
    document = f'''<!doctype html>
<html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Protagonist Chibi Cap v006 — Model Review</title>
<style>body{{margin:0;background:#151b22;color:#e2e9f2;font:16px/1.7 system-ui,sans-serif}}main{{max-width:1080px;margin:auto;padding:28px 20px 60px}}h1{{font-size:27px}}h2{{margin-top:36px;font-size:22px}}a{{color:#95d5da}}.hero{{display:block;max-width:720px;width:100%;margin:18px auto;background:#1a2027}}.grid{{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:16px}}figure{{margin:0;background:#1b232d}}figure img{{display:block;width:100%}}figcaption{{padding:7px 14px}}.note{{color:#afbecf}}@media(max-width:640px){{.grid{{grid-template-columns:1fr}}}}</style>
<main><h1>새 주인공 · Chibi Cap v006</h1>
<p>큰 머리, 짧고 넓은 몸통과 팔다리, 넓은 모자와 큰 신발을 중심으로 만든 새 로우폴리 모델입니다.</p>
<p class="note">아래는 실제 FBX를 Unity에서 렌더한 화면입니다. 턴테이블은 정적 모델의 360도 회전이며 캐릭터 액션 애니메이션이 아닙니다. 기존 플레이어·씬·모션·VFX에는 연결하지 않았습니다.</p>
<nav>{nav}</nav>
<h2>360도 모델 확인</h2><p>원속도 · 30fps · 4초 반복 · 동일 카메라와 크기</p>
<img class="hero" src="{local_link(turntable)}" alt="Actual Unity model turntable">
{elastic_markup}
<h2>정면·측면·뒤 확인</h2><div class="grid">{cards}</div>
<h2>생성 기준 이미지</h2><p class="note">아래는 이미지 생성 단계의 기준 시안입니다. 최종 메시의 증거는 위의 Unity 렌더입니다.</p>
<img class="hero" src="{local_link(HERE / 'Protagonist_ChibiCap_Concept.png')}" alt="Concept image, not final mesh">
</main></html>'''
    path = OUT / "index.html"
    path.write_text(document, encoding="utf-8")
    return path


def validate_links(index: Path) -> list[dict]:
    records = []
    for raw in sorted(set(re.findall(r'(?:src|href)="([^"]+)"', index.read_text(encoding="utf-8")))):
        url = urlsplit(raw)
        if url.scheme or url.netloc or not url.path:
            continue
        target = (index.parent / unquote(url.path)).resolve()
        records.append({"link": raw, "exists": target.is_file()})
    missing = [item["link"] for item in records if not item["exists"]]
    if missing:
        raise FileNotFoundError("Broken gallery links: " + ", ".join(missing))
    return records


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--elastic-frames", type=Path,
                        help="Optional actual Unity ElasticReview/Frames PNG directory. No generated in-between frames.")
    parser.add_argument("--elastic-fps", type=float, default=30)
    args = parser.parse_args()
    source = png_sequence(OUT / "TurntableFrames", expected=120)
    turntable = OUT / "Protagonist_ChibiCap_Turntable.gif"
    exports = [export_gif(source, turntable, 30, expected_size=(720, 720))]
    contact = contact_sheet()
    elastic_path = None
    elastic_source = args.elastic_frames
    if elastic_source is None:
        elastic_source = next((p for p in (HERE / "ElasticReview/Frames30", HERE / "ElasticReview/Frames")
                               if p.is_dir()), None)
    if elastic_source:
        elastic_path = OUT / "PunchElastic.gif"
        exports.append(export_gif(png_sequence(elastic_source), elastic_path, args.elastic_fps))
    index = gallery(turntable, elastic_path)
    records = validate_links(index)
    evidence = {
        "validated_utc": datetime.now(timezone.utc).isoformat(), "success": True,
        "model_report_sha256": digest(OUT / "model-review.json") if (OUT / "model-review.json").is_file() else None,
        "elastic_report_sha256": digest(HERE / "ElasticReview/elastic-review.json") if elastic_path and (HERE / "ElasticReview/elastic-review.json").is_file() else None,
        "source_turntable_frames": 120, "resolution": [720, 720],
        "turntable_source_fps": 30, "turntable_source_speed": 1,
        "turntable_duration_ms": exports[0]["duration_ms"],
        "gif_exports": exports, "contact": str(contact),
        "contact_sha256": digest(contact), "index": str(index),
        "local_links": records, "local_link_count": len(records),
        "view_files": [{"name": stem, "file": str(OUT / "Views" / f"{stem}.png"),
                        "sha256": digest(OUT / "Views" / f"{stem}.png")} for stem, _ in VIEWS],
        "scope": "Media integrity and delivery checks only. This is not an automatic naturalness or topology score.",
    }
    (OUT / "delivery-validation.json").write_text(json.dumps(evidence, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"success": True, "exports": [x["file"] for x in exports], "gallery": str(index),
                      "source_frames": len(source), "local_links": len(records)}, ensure_ascii=False))


if __name__ == "__main__":
    main()
