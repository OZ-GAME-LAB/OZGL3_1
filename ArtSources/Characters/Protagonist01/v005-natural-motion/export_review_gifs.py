"""Encode actual Unity review PNGs without retiming their source animation.

GIF stores 10 ms delay units. A cumulative 30/40 ms schedule reproduces a 30 Hz
timeline; passing 33/34 ms to Pillow would truncate the delay and speed it up.
"""
from __future__ import annotations

import argparse
import html
import json
import math
import os
from pathlib import Path
from urllib.parse import quote

from PIL import Image, ImageDraw, ImageFont


def clock_delays(count: int, fps: float) -> list[int]:
    boundaries = [round(i * 100.0 / fps) for i in range(count + 1)]
    return [(boundaries[i + 1] - boundaries[i]) * 10 for i in range(count)]


def load_font(size: int):
    for path in ("C:/Windows/Fonts/arial.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"):
        if Path(path).exists():
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()


def palette_for(frames: list[Image.Image]) -> Image.Image:
    stride = max(1, math.ceil(len(frames) / 48))
    selected = frames[::stride]
    columns = min(8, len(selected))
    atlas = Image.new("RGB", (160 * columns, 90 * math.ceil(len(selected) / columns)))
    for i, frame in enumerate(selected):
        thumb = frame.copy()
        thumb.thumbnail((160, 90), Image.Resampling.LANCZOS)
        atlas.paste(thumb, ((i % columns) * 160, (i // columns) * 90))
    return atlas.quantize(colors=256, method=Image.Quantize.MEDIANCUT)


def encode(folder: Path, output: Path, fps: float) -> dict:
    paths = sorted(folder.glob("*.png"))
    if not paths:
        raise FileNotFoundError(f"No review frames: {folder}")
    frames = []
    for path in paths:
        with Image.open(path) as source:
            frames.append(source.convert("RGB"))
    if len({image.size for image in frames}) != 1:
        raise ValueError(f"Inconsistent frame sizes: {folder}")
    palette = palette_for(frames)
    indexed = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
    durations = clock_delays(len(indexed), fps)
    output.parent.mkdir(parents=True, exist_ok=True)
    indexed[0].save(output, save_all=True, append_images=indexed[1:], duration=durations,
                    loop=0, disposal=2, optimize=False)
    with Image.open(output) as check:
        encoded_count = check.n_frames
        actual_durations = []
        for index in range(check.n_frames):
            check.seek(index)
            actual_durations.append(check.info.get("duration", 0))
    if sum(actual_durations) != sum(durations):
        raise ValueError(f"GIF clock mismatch: {output}: {sum(actual_durations)} != {sum(durations)}")
    return {
        "gif": str(output), "source_frames": len(paths), "encoded_frames": encoded_count,
        "fps_timeline": fps, "source_duration_ms": len(paths) * 1000.0 / fps,
        "gif_duration_ms": sum(actual_durations), "gif_delay_units_ms": sorted(set(actual_durations)),
        "clock_error_ms": sum(actual_durations) - len(paths) * 1000.0 / fps,
        "frame_delay_ms": durations, "width": frames[0].width, "height": frames[0].height,
        "palette": "One shared 256-color palette per clip; no temporal palette switching; no dithering.",
        "duplicate_frame_note": "Pillow may merge identical PNGs while preserving their combined duration.",
    }


GROUPS = [
    ("01_Locomotion", "기본 동작", ["Idle_Fists", "Walk_Fists", "Run_Fists", "Jump_Full", "Dash_Forward"]),
    ("02_Combat", "전투", ["Punch_Right_Jab", "Sword_Horizontal_Slash", "Sword_Aerial_Plunge", "Staff_Cast_Forward", "Hit_Reaction"]),
    ("03_Utility", "상호작용", ["Potion_Drink", "Item_Throw_Overhand", "Interact_Reach", "Pickup_Low"]),
]
LABELS = {
    "Idle_Fists": "Idle", "Walk_Fists": "Walk", "Run_Fists": "Run", "Jump_Full": "Jump", "Dash_Forward": "Dash",
    "Punch_Right_Jab": "Punch", "Sword_Horizontal_Slash": "Sword Slash", "Sword_Aerial_Plunge": "Sword Plunge",
    "Staff_Cast_Forward": "Staff Cast", "Hit_Reaction": "Hit Reaction", "Potion_Drink": "Drink Potion",
    "Item_Throw_Overhand": "Throw", "Interact_Reach": "Interact", "Pickup_Low": "Pick Up",
}


def canonical(name: str) -> str:
    return name.removesuffix("_SourceOnly")


def montage(entries: list[tuple[str, Path, float, str]], output: Path, group_name: str) -> dict:
    columns = 3 if len(entries) > 4 else 2
    tile_w, tile_h, header, gap = 320, 240, 28, 10
    rows = math.ceil(len(entries) / columns)
    size = (columns * (tile_w + gap) + gap, rows * (tile_h + header + gap) + gap)
    sequences = []
    for name, folder, fps, category in entries:
        frames = []
        for path in sorted(folder.glob("*.png")):
            with Image.open(path) as source:
                thumb = source.convert("RGB")
                thumb.thumbnail((tile_w, tile_h), Image.Resampling.LANCZOS)
                tile = Image.new("RGB", (tile_w, tile_h), (24, 29, 36))
                tile.paste(thumb, ((tile_w - thumb.width) // 2, (tile_h - thumb.height) // 2))
                frames.append(tile)
        if not frames:
            raise FileNotFoundError(f"No montage frames: {folder}")
        sequences.append((name, frames, fps))
    count = max(90, math.ceil(max(len(frames) / fps for _, frames, fps in sequences) * 30))
    frames = []
    font = load_font(15)
    for tick in range(count):
        canvas = Image.new("RGB", size, (24, 29, 36))
        draw = ImageDraw.Draw(canvas)
        for cell, (name, images, fps) in enumerate(sequences):
            x = gap + (cell % columns) * (tile_w + gap)
            y = gap + (cell // columns) * (tile_h + header + gap)
            # Every tile keeps its own authored clock; no clip is compressed to match a neighbour.
            frame_index = int(tick / 30 * fps + 1e-7) % len(images)
            canvas.paste(images[frame_index], (x, y))
            draw.text((x, y + tile_h + 4), LABELS.get(canonical(name), name), font=font, fill=(222, 231, 242))
        frames.append(canvas)
    palette = palette_for(frames)
    indexed = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
    durations = clock_delays(count, 30)
    output.parent.mkdir(parents=True, exist_ok=True)
    indexed[0].save(output, save_all=True, append_images=indexed[1:], loop=0, duration=durations, disposal=2, optimize=False)
    frames[min(count - 1, round(count * .42))].save(output.with_suffix(".png"))
    with Image.open(output) as check:
        actual = 0
        for index in range(check.n_frames):
            check.seek(index)
            actual += check.info.get("duration", 0)
        if actual != sum(durations):
            raise ValueError(f"Montage timing mismatch: {output}")
    return {"name": group_name, "gif": str(output), "poster": str(output.with_suffix(".png")), "motions": [canonical(e[0]) for e in entries],
            "width": size[0], "height": size[1], "duration_ms": sum(durations), "fps_timeline": 30,
            "method": "Original PNGs tiled at their own unchanged sample rate. Each tile repeats independently; the group loop is a review montage, not a synchronized gameplay sequence."}


def hand_contact(directory: Path, output: Path) -> Path | None:
    paths = sorted((directory / "Hands").glob("*.png"))
    if not paths:
        return None
    columns, tile, gap, label = 4, 280, 10, 26
    sheet = Image.new("RGB", (columns * (tile + gap) + gap, math.ceil(len(paths) / columns) * (tile + gap + label) + gap), (24, 29, 36))
    draw = ImageDraw.Draw(sheet)
    for i, path in enumerate(paths):
        x, y = gap + (i % columns) * (tile + gap), gap + (i // columns) * (tile + gap + label)
        with Image.open(path) as source:
            image = source.convert("RGB")
            image.thumbnail((tile, tile), Image.Resampling.LANCZOS)
            sheet.paste(image, (x + (tile - image.width) // 2, y + (tile - image.height) // 2))
        draw.text((x, y + tile + 3), path.stem, font=load_font(13), fill=(222, 231, 242))
    sheet.save(output)
    return output


def relative_link(target: str | Path, directory: Path) -> str:
    return quote(Path(os.path.relpath(Path(target), directory)).as_posix())


def previous_comparisons(manifest: dict, previous_directory: Path) -> list[dict]:
    previous_manifest = previous_directory / "ReviewOutput" / "export-manifest.json"
    if not previous_manifest.exists():
        return []
    previous = json.loads(previous_manifest.read_text(encoding="utf-8-sig"))
    earlier = {canonical(item["name"]): item for item in previous.get("outputs", [])
               if item.get("category") == "Motion" and "gif" in item}
    result = []
    for current in manifest["outputs"]:
        key = canonical(current["name"])
        old = earlier.get(key)
        if "gif" not in current or not old:
            continue
        previous_path = Path(old["gif"])
        if not previous_path.is_absolute():
            previous_path = previous_directory / "ReviewOutput" / "GIF" / previous_path.name
        if not previous_path.exists():
            continue
        result.append({"name": key, "current_gif": current["gif"], "previous_gif": str(previous_path),
                       "current_duration_ms": current["gif_duration_ms"], "previous_duration_ms": old["gif_duration_ms"]})
    return result


def gallery(output: Path, manifest: dict, hands: Path | None):
    group_items = []
    for group in manifest.get("groups", []):
        relative = Path(group["gif"]).relative_to(output.parent).as_posix()
        group_items.append(f'<figure><a href="{quote(relative)}"><img loading="lazy" src="{quote(relative)}" alt="{html.escape(group["name"])}"></a><figcaption>{html.escape(group["name"])}</figcaption></figure>')
    cards = []
    lookup = {motion: title for _, title, names in GROUPS for motion in names}
    for item in manifest["outputs"]:
        if "gif" not in item:
            continue
        key = canonical(item["name"])
        category = lookup.get(key, "기타")
        relative = Path(item["gif"]).relative_to(output.parent).as_posix()
        contact_relative = Path(item["contact"]).relative_to(output.parent).as_posix()
        cards.append(f'<article data-category="{html.escape(category)}"><a href="{quote(relative)}"><img loading="lazy" src="{quote(relative)}" alt="{html.escape(key)}"></a><h3>{html.escape(LABELS.get(key,key))}</h3><p>{html.escape(key)} · {item["gif_duration_ms"]/1000:.2f}s</p><details><summary>프레임 확인</summary><a href="{quote(contact_relative)}"><img loading="lazy" src="{quote(contact_relative)}" alt="{html.escape(key)} frames"></a></details></article>')
    hands_html = f'<section><h2>손 모델 검수</h2><a href="{quote(hands.name)}"><img class="hands" loading="lazy" src="{quote(hands.name)}" alt="Fist and weapon grip close-ups"></a><p>100: 주먹 · 75: 무기 그립. 정면과 측면의 실제 메시를 확인합니다.</p></section>' if hands else ""
    comparisons = []
    for item in manifest.get("comparisons", []):
        previous_url = relative_link(item["previous_gif"], output.parent)
        current_url = relative_link(item["current_gif"], output.parent)
        label = html.escape(LABELS.get(item["name"], item["name"]))
        comparisons.append(f'<article><h3>{label}</h3><div class="compare-pair"><figure><figcaption>이전 v004 · {item["previous_duration_ms"]/1000:.2f}s</figcaption><a href="{previous_url}"><img loading="lazy" src="{previous_url}" alt="{label} v004"></a></figure><figure><figcaption>현재 v005 · {item["current_duration_ms"]/1000:.2f}s</figcaption><a href="{current_url}"><img loading="lazy" src="{current_url}" alt="{label} v005"></a></figure></div></article>')
    comparison_html = '<section><h2>이전 결과와 비교</h2><p>각 GIF는 자체 원래 속도로 반복됩니다. 소스 길이와 루프 횟수가 다를 수 있으며 두 재생기는 프레임 동기화되지 않습니다. 이전 파일은 수정하지 않고 참조합니다.</p><div class="comparison-cards">' + ''.join(comparisons) + '</div></section>' if comparisons else ""
    output.write_text('''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Natural Character Motion Review v005</title>
<style>:root{color-scheme:dark}*{box-sizing:border-box}body{margin:0;background:#121820;color:#e3ebf5;font:15px/1.6 system-ui,sans-serif}main{max-width:1400px;margin:auto;padding:32px 20px}h1{font-size:28px;margin:0}p{color:#a7bacf}section{margin:30px 0}.groups,.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(310px,1fr));gap:18px}figure,article{margin:0;background:#1c2530;border:1px solid #303d4c;border-radius:12px;overflow:hidden}figure img,article img{width:100%;height:auto;display:block}figcaption,h3,article p,details{margin:10px 14px}h3{margin-bottom:0}article p{margin-top:0;font-size:12px}button{background:#253447;color:#e3ebf5;border:1px solid #475c73;border-radius:8px;padding:8px 14px;margin:0 7px 8px 0;cursor:pointer}button[aria-pressed=true]{background:#345f70;border-color:#72b3c2}article[hidden]{display:none}.hands{width:100%;max-width:1160px;border-radius:12px}summary{cursor:pointer}a{color:inherit}footer{font-size:12px;color:#8da3bb}</style>
<style>.compare-pair{display:grid;grid-template-columns:1fr 1fr;gap:8px}.compare-pair figure{border:0}.comparison-cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(520px,1fr));gap:18px}@media(max-width:600px){.comparison-cards{grid-template-columns:1fr}.compare-pair{grid-template-columns:1fr}}</style>
<main><h1>캐릭터 자연스러운 모션 검수 · v005</h1><p>중립 배경에서 본 실제 모델의 2.5D 측면 샘플입니다. 각 동작의 원래 재생 속도를 유지했습니다. 인게임 적용 전 검수 자료입니다.</p>
<section><h2>모아 보기</h2><div class="groups">''' + ''.join(group_items) + '''</div></section>
<section><h2>개별 동작</h2><nav>''' + ''.join(f'<button aria-pressed="{str(title=="전체").lower()}" data-filter="{title}">{title}</button>' for title in ["전체", "기본 동작", "전투", "상호작용"]) + '''</nav><div class="cards">''' + ''.join(cards) + '''</div></section>''' + hands_html + comparison_html + '''<footer>원본 Unity 프레임으로 구성한 오프라인 갤러리입니다. 그룹 타일은 각각의 주기로 반복되며, 실제 플레이를 녹화한 영상은 아닙니다.</footer></main>
<script>document.querySelectorAll('[data-filter]').forEach(button=>button.addEventListener('click',()=>{document.querySelectorAll('[data-filter]').forEach(b=>b.setAttribute('aria-pressed',String(b===button)));document.querySelectorAll('[data-category]').forEach(card=>card.hidden=button.dataset.filter!=='전체'&&card.dataset.category!==button.dataset.filter)}))</script></html>''', encoding="utf-8")


def resolve(project: Path, value: str) -> Path:
    path = Path(value)
    return path if path.is_absolute() else project / path


def contact(folder: Path, output: Path, title: str, fps: float, columns=4, cells=12):
    paths = sorted(folder.glob("*.png"))
    if not paths:
        return
    indices = sorted(set(round(i * (len(paths) - 1) / max(1, cells - 1)) for i in range(cells)))
    tile_width, tile_height, gap, heading = 320, 240, 10, 30
    rows = math.ceil(len(indices) / columns)
    sheet = Image.new("RGB", (columns * (tile_width + gap) + gap, heading + rows * (tile_height + 28 + gap)), (24, 29, 36))
    draw = ImageDraw.Draw(sheet)
    draw.text((gap, 7), title, font=load_font(16), fill=(222, 231, 242))
    for cell, index in enumerate(indices):
        x = gap + (cell % columns) * (tile_width + gap)
        y = heading + (cell // columns) * (tile_height + 28 + gap)
        with Image.open(paths[index]) as source:
            frame = source.convert("RGB")
            frame.thumbnail((tile_width, tile_height), Image.Resampling.LANCZOS)
            sheet.paste(frame, (x + (tile_width - frame.width) // 2, y + (tile_height - frame.height) // 2))
        draw.text((x, y + tile_height + 4), f"{index / fps:.3f} s", font=load_font(13), fill=(167, 184, 205))
    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output)


def overview(entries: list[tuple[str, Path, float]], output: Path):
    if not entries:
        return
    columns, width, height, gap = 4, 320, 240, 12
    sheet = Image.new("RGB", (columns * (width + gap) + gap, math.ceil(len(entries) / columns) * (height + 32 + gap) + gap), (24, 29, 36))
    draw = ImageDraw.Draw(sheet)
    for i, (name, folder, fps) in enumerate(entries):
        paths = sorted(folder.glob("*.png"))
        if not paths:
            continue
        x = gap + (i % columns) * (width + gap)
        y = gap + (i // columns) * (height + 32 + gap)
        index = min(len(paths) - 1, max(0, round(len(paths) * .42)))
        with Image.open(paths[index]) as source:
            frame = source.convert("RGB")
            frame.thumbnail((width, height), Image.Resampling.LANCZOS)
            sheet.paste(frame, (x + (width - frame.width) // 2, y + (height - frame.height) // 2))
        draw.text((x, y + height + 4), name, font=load_font(14), fill=(222, 231, 242))
    sheet.save(output)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--report-directory", type=Path, default=Path(__file__).resolve().parent)
    parser.add_argument("--project-root", type=Path)
    parser.add_argument("--diagnostic", action="store_true", help="Use the separate 10fps diagnostic image report.")
    parser.add_argument("--contacts-only", action="store_true")
    parser.add_argument("--final-groups", action="store_true", help="Create three motion montage GIFs and an offline gallery with read-only v004 comparisons.")
    parser.add_argument("--comparison-directory", type=Path, help="Previous motion report directory (defaults to sibling v004-motion-polish).")
    arguments = parser.parse_args()
    if arguments.final_groups and (arguments.diagnostic or arguments.contacts_only):
        parser.error("--final-groups requires full 30fps final GIF export.")
    directory = arguments.report_directory.resolve()
    project = arguments.project_root.resolve() if arguments.project_root else directory.parents[3]
    report_path = directory / ("motion-review-diagnostic.json" if arguments.diagnostic else "motion-review.json")
    all_entries = []
    if report_path.exists():
        report = json.loads(report_path.read_text(encoding="utf-8-sig"))
        for motion in report.get("motions", []):
            if "dead" in motion["name"].lower() or "death" in motion["name"].lower():
                continue
            all_entries.append((motion["name"], resolve(project, motion["frames30Directory"]), float(motion.get("gifRate", 30)), "Motion"))
    if not all_entries:
        raise FileNotFoundError("No rendered Unity review reports found.")
    output_root = directory / ("DiagnosticOutput" if arguments.diagnostic else "ReviewOutput")
    output_root.mkdir(parents=True, exist_ok=True)
    manifest = {"method": "Actual rendered Unity PNGs only. No generated/interpolated frames, speed changes or optical flow.",
                "gif_clock": "Cumulative centisecond rounding preserves the exact average 30Hz timeline with 30/40ms delays; GIF cannot store 33/34ms directly.", "outputs": []}
    for name, folder, fps, category in all_entries:
        contact_path = output_root / "Contacts" / f"{name}.png"
        contact(folder, contact_path, f"{name} / authored 1x", fps)
        entry = {"name": name, "category": category, "contact": str(contact_path)}
        if not arguments.contacts_only:
            entry.update(encode(folder, output_root / "GIF" / f"{name}.gif", fps))
        manifest["outputs"].append(entry)
    overview([(name, folder, fps) for name, folder, fps, category in all_entries if category == "Motion"], output_root / "motion-overview.png")
    if arguments.final_groups:
        manifest["groups"] = []
        by_name = {canonical(entry[0]): entry for entry in all_entries}
        for filename, title, names in GROUPS:
            selected = [by_name[name] for name in names if name in by_name]
            if selected:
                manifest["groups"].append(montage(selected, output_root / "Groups" / f"{filename}.gif", title))
        hands = hand_contact(directory, output_root / "hand-grips-contact.png")
        previous = arguments.comparison_directory.resolve() if arguments.comparison_directory else directory.with_name("v004-motion-polish")
        manifest["comparisons"] = previous_comparisons(manifest, previous)
        manifest["comparison_policy"] = "Read-only links to existing v004 motion GIFs. No previous assets or effects are regenerated, copied, or modified. Independent GIF clocks are explicitly disclosed."
        gallery_path = output_root / "index.html"
        gallery(gallery_path, manifest, hands)
        manifest["gallery"] = str(gallery_path)
    (output_root / "export-manifest.json").write_text(json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps({"outputs": len(manifest["outputs"]), "directory": str(output_root)}, ensure_ascii=False))


if __name__ == "__main__":
    main()
