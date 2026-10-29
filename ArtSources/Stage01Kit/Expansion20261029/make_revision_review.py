#!/usr/bin/env python3
"""Compose numbered revision review sheets from untouched before/after renders.

Run after the revised audit and renders are complete. This only lays out supplied
images with captions; it does not alter any model or its rendered geometry.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path


REVISION_ID = "20261029-01"
REVISIONS = [
    ("S1-008", "앞쪽 난간 제거", "카메라 앞 난간을 제거하고 뒤쪽 난간은 유지"),
    ("S1-017", "곧은 가로등", "기둥을 곧게 정리하고 조명 형태 단순화"),
    ("S1-018", "가로수 단순화", "큰 수관과 줄기 중심으로 형태 단순화"),
    ("S1-019", "자판기 단순화", "큰 본체와 진열창 중심으로 형태 단순화"),
    ("S1-022", "실외기 단순화", "본체와 팬 중심으로 형태 단순화"),
    ("S1-023", "위성접시로 교체", "게이트 기둥을 일반 옥상 위성접시로 교체"),
    ("S1-041", "덕트 단순화", "굽은 덕트의 큰 형태와 배출구 중심으로 단순화"),
]
RETIRED_IDS = ["S1-042", "S1-052", "S1-053"]
BG = "#edf2f0"
INK = "#203945"
MUTED = "#607985"
NAVY = "#172735"


def read_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def indexed_audit(path: Path) -> dict:
    return {entry["id"]: entry for entry in read_json(path).get("entries", [])}


def triangle_text(entry: dict | None) -> str:
    value = (entry or {}).get("triangles")
    return f"{value:,}" if isinstance(value, (int, float)) else "기록 없음"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parent)
    args = parser.parse_args()
    root = args.root.resolve()
    revision = root / "Revisions" / REVISION_ID
    before_dir = revision / "Before"
    before_audit = indexed_audit(revision / "audit-before.json")
    after_audit = indexed_audit(root / "audit.json")
    catalog = read_json(root / "catalog.json")
    entries = {entry["id"]: entry for entry in catalog["entries"]}
    retired = {entry["id"]: entry for entry in catalog.get("retired_entries", [])}
    for item_id in RETIRED_IDS:
        if item_id in entries:
            parser.error(f"삭제 번호가 현재 카탈로그에 남아 있습니다: {item_id}")
    for item_id, _, _ in REVISIONS:
        if item_id not in entries or item_id not in after_audit:
            parser.error(f"현재 카탈로그 또는 audit에 수정 모델이 없습니다: {item_id}")

    try:
        from PIL import Image, ImageDraw, ImageFont, ImageOps
    except ImportError:
        parser.error("이 이미지 검수 도구에는 Pillow가 필요합니다.")
    font_path = Path("C:/Windows/Fonts/malgun.ttf")
    if not font_path.exists():
        parser.error(f"한글 글꼴을 찾을 수 없습니다: {font_path}")
    font = lambda size: ImageFont.truetype(str(font_path), size)
    title_font, heading_font, id_font, body_font, small_font = font(35), font(26), font(25), font(21), font(17)
    warnings = []

    def label(draw, position, text, text_font, fill=INK):
        draw.text(position, text, font=text_font, fill=fill)

    def lines(draw, text, text_font, width):
        result, current = [], ""
        for char in str(text):
            if current and draw.textlength(current + char, font=text_font) > width:
                result.append(current)
                current = char
            else:
                current += char
        if current:
            result.append(current)
        return result

    def image_panel(canvas, draw, path, box):
        x, y, width, height = box
        draw.rectangle((x, y, x + width, y + height), fill=NAVY)
        if not path.exists():
            warnings.append(f"렌더 없음: {path}")
            label(draw, (x + 24, y + height // 2 - 12), "렌더 파일 없음", body_font, "#a3b8c1")
            return
        with Image.open(path) as source:
            thumb = ImageOps.contain(source.convert("RGBA"), (width, height), Image.Resampling.LANCZOS)
            canvas.paste(thumb, (x + (width - thumb.width) // 2, y + (height - thumb.height) // 2), thumb)

    def new_page(width, height, title, subtitle):
        canvas = Image.new("RGB", (width, height), BG)
        draw = ImageDraw.Draw(canvas)
        label(draw, (30, 19), title, title_font)
        label(draw, (32, 69), subtitle, small_font, MUTED)
        return canvas, draw

    def current_image(item_id):
        return root / "Renders" / f"{item_id}_perspective.png"

    # A single overview with seven revised models and a clear retired-number card.
    width, height, margin, gutter = 1920, 1000, 28, 18
    cell_w, cell_h, top = 452, 419, 112
    sheet, draw = new_page(width, height, "STAGE 01 / 변경 모델 미리보기", "2026-10-29 · 변경 7종 / 삭제 3종 · 고정 번호로 수정 의견을 남겨 주세요")
    for index, (item_id, short_label, _) in enumerate(REVISIONS):
        x = margin + (index % 4) * (cell_w + gutter)
        y = top + (index // 4) * (cell_h + gutter)
        draw.rounded_rectangle((x, y, x + cell_w, y + cell_h), 9, fill="white", outline="#cfdbdf")
        draw.rounded_rectangle((x + 10, y + 10, x + 140, y + 45), 5, fill=INK)
        label(draw, (x + 18, y + 11), item_id, id_font, "white")
        label(draw, (x + 151, y + 16), short_label, small_font, MUTED)
        image_panel(sheet, draw, current_image(item_id), (x + 10, y + 55, cell_w - 20, 263))
        name_lines = lines(draw, entries[item_id].get("name_ko", item_id), body_font, cell_w - 28)
        for line_index, text in enumerate(name_lines[:2]):
            label(draw, (x + 14, y + 326 + 27 * line_index), text, body_font)
        label(draw, (x + 14, y + 387), "삼각형 (LOD0) · " + triangle_text(after_audit[item_id]), small_font, MUTED)
    x = margin + 3 * (cell_w + gutter)
    y = top + cell_h + gutter
    draw.rounded_rectangle((x, y, x + cell_w, y + cell_h), 9, fill="#e3eae7", outline="#cbd8d3")
    label(draw, (x + 23, y + 26), "삭제된 모델", heading_font)
    label(draw, (x + 23, y + 75), "아래 번호는 재사용하지 않습니다.", small_font, MUTED)
    for index, item_id in enumerate(RETIRED_IDS):
        item = retired.get(item_id, before_audit.get(item_id, {}))
        label(draw, (x + 24, y + 129 + index * 76), item_id, id_font)
        label(draw, (x + 24, y + 164 + index * 76), item.get("name_ko", item.get("key", "")), small_font, MUTED)
    revision.mkdir(parents=True, exist_ok=True)
    sheet.save(revision / "변경모델미리보기.png", optimize=True)

    # Two pages keep before/after models large enough to inspect.
    row_h, compare_w, column_w, left = 524, 1840, 876, 28
    for page, group in enumerate((REVISIONS[:4], REVISIONS[4:]), 1):
        compare_h = 148 + len(group) * row_h
        comparison, draw = new_page(compare_w, compare_h, f"STAGE 01 / 수정 전후 비교  {page}/2", "왼쪽 수정 전 · 오른쪽 수정 후 · 번호 유지 · 삼각형 수는 LOD0 기준")
        label(draw, (left + 10, 106), "BEFORE / 수정 전", small_font, MUTED)
        label(draw, (left + column_w + 32, 106), "AFTER / 수정 후", small_font, MUTED)
        for index, (item_id, short_label, detail) in enumerate(group):
            y = 140 + index * row_h
            draw.rounded_rectangle((left, y, compare_w - left, y + row_h - 15), 9, fill="white", outline="#cfdbdf")
            label(draw, (left + 15, y + 9), item_id + " · " + entries[item_id].get("name_ko", ""), heading_font)
            label(draw, (left + 15, y + 49), detail, small_font, MUTED)
            image_panel(comparison, draw, before_dir / f"{item_id}_perspective.png", (left + 12, y + 83, column_w - 12, 368))
            image_panel(comparison, draw, current_image(item_id), (left + column_w + 22, y + 83, column_w - 12, 368))
            label(draw, (left + 15, y + 463), "수정 전 삼각형 (LOD0) · " + triangle_text(before_audit.get(item_id)), body_font, MUTED)
            label(draw, (left + column_w + 24, y + 463), "수정 후 삼각형 (LOD0) · " + triangle_text(after_audit[item_id]), body_font, MUTED)
        name = "수정전후비교.png" if page == 1 else f"수정전후비교_{page:02d}.png"
        comparison.save(revision / name, optimize=True)

    # Optional actual-character renders show the rail change at three stair positions.
    safety_paths = []
    for index in range(1, 4):
        filename = f"S1-008_character_{index}.png"
        candidates = [root / "Renders" / filename, revision / filename, revision / "Safety" / filename]
        safety_paths.append(next((path for path in candidates if path.exists()), None))
    if all(safety_paths):
        safety, draw = new_page(1920, 680, "S1-008 / 계단 난간·캐릭터 검수", "카메라 앞 난간 제거 후 · 실제 캐릭터를 계단 세 위치에 배치한 렌더")
        for index, path in enumerate(safety_paths):
            x = 28 + index * 632
            image_panel(safety, draw, path, (x, 116, 604, 453))
            label(draw, (x + 4, 589), f"계단 위 캐릭터 위치 {index + 1}", body_font)
        safety.save(revision / "난간캐릭터검수.png", optimize=True)
    else:
        warnings.append("캐릭터 검수 렌더 3개가 모두 준비되지 않아 난간캐릭터검수.png를 생성하지 않았습니다.")

    notes = [
        "1스테이지 환경 모델링 수정 검수",
        "기준일: 2026-10-29 / 수정 묶음: " + REVISION_ID,
        "",
        "활성 54항목: 개별 모델 50종 + 조립 예시 4종. 신규 환경은 21종입니다.",
        "기존 번호는 유지하며, 삭제한 번호는 재사용하지 않습니다.",
        "삼각형 수는 audit의 LOD0 메시 집계이며, LOD를 모두 합한 수가 아닙니다.",
        "전후 이미지는 각 시점에 저장된 렌더를 사용했습니다. 모델 픽셀을 수정하지 않았습니다.",
        "",
        "수정 7종",
    ]
    for item_id, _, detail in REVISIONS:
        notes.extend([
            f"[{item_id}] {entries[item_id].get('name_ko', '')}",
            "  " + detail,
            "  삼각형 (LOD0): " + triangle_text(before_audit.get(item_id)) + " → " + triangle_text(after_audit[item_id]),
            "",
        ])
    notes.extend(["삭제 3종 — 아래 번호는 재사용하지 않음", ""])
    for item_id in RETIRED_IDS:
        item = retired.get(item_id, before_audit.get(item_id, {}))
        notes.append(f"[{item_id}] {item.get('name_ko', item.get('key', ''))} / 사용자 요청 삭제")
    notes.extend(["", "검수 파일", "변경모델미리보기.png — 수정 모델 7종과 삭제 번호 한눈에 보기", "수정전후비교.png / 수정전후비교_02.png — 수정 전후 비교 2페이지"])
    if all(safety_paths):
        notes.append("난간캐릭터검수.png — 계단에서 캐릭터가 보이는 정도 확인용 3개 위치")
    if warnings:
        notes.extend(["", "생성 시 확인 사항", *warnings])
    (revision / "notes.txt").write_text("\n".join(notes) + "\n", encoding="utf-8-sig")
    print(f"Generated revision review sheets: {revision}")
    for warning in warnings:
        print(warning)


if __name__ == "__main__":
    main()
