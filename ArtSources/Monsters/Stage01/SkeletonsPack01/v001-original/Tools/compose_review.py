"""Package actual Unity PNG captures into offline GIF/contact-sheet review media.

Input PNGs and unity-review-report.json are read only. All generated media is
written under --media-root, outside the Unity project. No model/rig/key changes.

Bundled runtime example:
  python compose_review.py --check
  python compose_review.py
  python compose_review.py --self-test

GIF stores time in 10 ms units. Cumulative rounding distributes 30 fps as
30/40/30 ms, and 0.5x as 70/60/70 ms, without slowing every frame to 40 ms.
All source PNGs remain available, including the terminal sample. The terminal
sample has no extra animation interval: loops omit it; one-shots show it in an
explicit 500 ms recovery hold between repeats. Contact sheets include EVERY PNG.
"""
from __future__ import annotations

import argparse
import hashlib
import html
import io
import json
import math
import re
import sys
from datetime import datetime, timezone
from functools import lru_cache
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageOps

DEFAULT_MEDIA = Path("C:/Users/me/Desktop/OZGL3_ArtReview/Monsters/Stage01/SkeletonsPack01/v001-original")
EXPECTED = {
    "Minion": ["Idle", "Move", "Attack", "Hit"],
    "Rogue": ["Idle_Combat", "Running_A", "2H_Ranged_Aiming", "2H_Ranged_Shoot", "2H_Ranged_Reload", "Hit_A"],
    "Warrior": ["Idle_Combat", "Walking_B", "1H_Melee_Attack_Chop", "1H_Melee_Attack_Slice_Diagonal", "Hit_A"],
    "Mage": ["Idle_Combat", "Walking_D_Skeletons", "Spellcast_Shoot", "Spellcast_Raise", "Hit_A"],
}
ROLES_KO = {"Minion": "기존 근접형 하수인", "Rogue": "도적 스켈레톤", "Warrior": "전사 스켈레톤", "Mage": "마법사 스켈레톤"}
MINION_SOURCE = {"Idle": "Idle_Combat", "Move": "Running_A", "Attack": "1H_Melee_Attack_Chop", "Hit": "Hit_A"}
VIEWS_KO = {"Side": "2.5D 측면 비교", "Front": "정면", "Opposite": "반대쪽 측면", "Back": "후면", "Blend": "검수용 연결"}
MEANINGS = {
    "Idle": "대기", "Move": "이동", "Attack": "공격", "Hit": "피격", "Hit_A": "피격",
    "Idle_Combat": "전투 대기", "Running_A": "달리기", "Walking_B": "걷기", "Walking_D_Skeletons": "스켈레톤 걷기",
    "2H_Ranged_Aiming": "석궁 조준", "2H_Ranged_Shoot": "석궁 발사", "2H_Ranged_Reload": "석궁 재장전",
    "1H_Melee_Attack_Chop": "한손 도끼 내려치기", "1H_Melee_Attack_Slice_Diagonal": "한손 도끼 대각선 베기",
    "Spellcast_Shoot": "발사 시전", "Spellcast_Raise": "들어올리기 시전",
}
HEADER = 48


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


@lru_cache(maxsize=12)
def font(size: int):
    # Installed fonts are used for labels only; no font binaries are distributed.
    for name in ("C:/Windows/Fonts/malgunbd.ttf", "C:/Windows/Fonts/arial.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"):
        if Path(name).exists():
            return ImageFont.truetype(name, size)
    return ImageFont.load_default(size=size)


def pngs(folder: Path) -> list[Path]:
    return sorted((p for p in folder.glob("*.png") if p.stem.isdigit()), key=lambda p: int(p.stem))


def quantize_delays(milliseconds: list[float]) -> list[int]:
    """Round cumulative time, so a complete cycle stays at its real duration."""
    total = 0.0
    previous_tick = 0
    result = []
    for value in milliseconds:
        if value <= 0:
            raise ValueError("Non-positive interval")
        total += value
        tick = int(math.floor(total / 10.0 + 0.5))
        result.append((tick - previous_tick) * 10)
        previous_tick = tick
    if any(d <= 0 for d in result):
        raise ValueError("A source interval is shorter than GIF's 10 ms time unit")
    return result


def animation_intervals(paths: list[Path], seconds: float, fps: int):
    """Represent half-open time intervals; endpoint PNG is kept in source only."""
    frames = []
    omitted = []
    epsilon = 1e-5  # Ceil(float clip.length * fps) can add a nearly identical endpoint.
    for p in paths:
        index = int(p.stem)
        start = min(seconds, index / fps)
        end = min(seconds, (index + 1) / fps)
        if start >= seconds - epsilon or end - start < epsilon:
            omitted.append(p.name)
        else:
            frames.append((p, (end - start) * 1000, start))
    if not frames:
        raise ValueError("No positive-duration frame intervals")
    if abs(sum(f[1] for f in frames) - seconds * 1000) > 0.02:
        raise ValueError("PNG intervals do not cover one complete clip cycle")
    return frames, omitted


def annotated(path: Path, role: str, clip: str, speed: float, source_time: float, hold=False, connection=False) -> Image.Image:
    with Image.open(path) as src:
        image = src.convert("RGB")
    width, height = image.size
    # Add a label strip instead of covering an attack silhouette or contact point.
    canvas = Image.new("RGB", (width, height + HEADER), "#10202b")
    canvas.paste(image, (0, HEADER))
    draw = ImageDraw.Draw(canvas)
    draw.text((12, 5), "LEFT  Current protagonist / reference snapshot", font=font(14), fill="#d5e5ea")
    subject = "Minion v001 (adjusted sample)" if role == "Minion" else f"{role} native Skeletons 1.0"
    draw.text((width // 2 + 8, 5), "RIGHT  " + subject, font=font(14), fill="#f6e3b5")
    suffix = " | recovery hold 0.50s" if hold else ""
    if connection:
        suffix += " | review blend only"
    line = f"{clip} | {speed:g}x | common world scale | source t={source_time:.3f}s | PNG {path.stem}{suffix}"
    draw.text((12, 27), line, font=font(12), fill="#9eb8c4")
    return canvas


def make_gif(paths: list[Path], row: dict, role: str, dest: Path, speed: float, fps: int, connection=False) -> dict:
    intervals, omitted = animation_intervals(paths, float(row["seconds"]), fps)
    source_times = [v[2] for v in intervals]
    samples = [(p, ms / speed, time, False) for p, ms, time in intervals]
    if not row.get("loop", False):
        samples.append((paths[-1], 500.0, float(row["seconds"]), True))
    delays = quantize_delays([v[1] for v in samples])
    # One palette across sampled motion poses avoids palette flicker between GIF frames.
    picks = sorted(set(round(i * (len(samples) - 1) / min(15, len(samples) - 1)) for i in range(min(16, len(samples))))) if len(samples) > 1 else [0]
    palette_canvas = Image.new("RGB", (320 * 4, 196 * 4), "#10202b")
    for n, index in enumerate(picks):
        path, _, time, hold = samples[index]
        labeled = annotated(path, role, row["name"], speed, time, hold, connection)
        thumb = ImageOps.contain(labeled, (320, 196), Image.Resampling.LANCZOS)
        palette_canvas.paste(thumb, ((n % 4) * 320, (n // 4) * 196))
    palette = palette_canvas.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
    frames = []
    for path, _, time, hold in samples:
        labeled = annotated(path, role, row["name"], speed, time, hold, connection)
        frames.append(labeled.quantize(palette=palette, dither=Image.Dither.NONE))
    dest.parent.mkdir(parents=True, exist_ok=True)
    frames[0].save(dest, save_all=True, append_images=frames[1:], duration=delays,
                   loop=0, disposal=2, optimize=False)
    with Image.open(dest) as gif:
        encoded = []
        for index in range(gif.n_frames):
            gif.seek(index)
            encoded.append(int(gif.info.get("duration", 0)))
        encoded_size = list(gif.size)
    expected = sum(delays)
    if sum(encoded) != expected:
        raise RuntimeError(f"Encoded GIF timing changed: {dest}")
    return {"path": dest, "sha256": sha256(dest), "bytes": dest.stat().st_size,
            "speed": speed, "source_frames": len(paths), "animation_frames": len(intervals),
            "omitted_endpoint_pngs": omitted, "source_cycle_seconds": float(row["seconds"]),
            "playback_seconds": sum(encoded) / 1000, "recovery_hold_seconds": 0 if row.get("loop") else .5,
            "encoded_frames": len(encoded), "encoded_delays_ms": encoded, "size": encoded_size,
            "source_interval_start_seconds": source_times}


def contacts(paths: list[Path], row: dict, role: str, view: str, media: Path, fps: int, rows_per_page=32) -> list[dict]:
    """240x135 tiles, eight columns; all PNGs, including endpoints and sparse auxiliaries."""
    output = []
    page_size = 8 * rows_per_page
    for start in range(0, len(paths), page_size):
        subset = paths[start:start + page_size]
        rows = math.ceil(len(subset) / 8)
        sheet = Image.new("RGB", (8 * 240, 42 + rows * 135), "#10202b")
        draw = ImageDraw.Draw(sheet)
        draw.text((12, 7), f"{role} | {row['name']} | {view} | every supplied PNG | {start + 1}-{start + len(subset)}/{len(paths)}", font=font(18), fill="white")
        for i, path in enumerate(subset):
            x, y = (i % 8) * 240, 42 + (i // 8) * 135
            with Image.open(path) as src:
                image = ImageOps.contain(src.convert("RGB"), (240, 119), Image.Resampling.LANCZOS)
            sheet.paste(image, (x + (240 - image.width) // 2, y + 16 + (119 - image.height) // 2))
            time = min(float(row["seconds"]), int(path.stem) / fps)
            draw.text((x + 5, y + 1), f"PNG {path.stem}   t={time:.3f}s", font=font(11), fill="#c8dbe3")
        part = start // page_size + 1
        dest = media / "ContactSheets" / role / f"{row['name']}_{view}_allframes_p{part:02d}.png"
        dest.parent.mkdir(parents=True, exist_ok=True)
        sheet.save(dest)
        output.append({"path": dest, "view": view, "page": part, "png_count": len(subset), "tile": [240, 135], "columns": 8, "source_pngs": [str(p.relative_to(media)).replace('\\', '/') for p in subset], "sha256": sha256(dest)})
    return output


def check_inputs(media: Path, allow_partial=False, skip_connections=False):
    report_file = media / "unity-review-report.json"
    if not report_file.exists():
        raise FileNotFoundError(f"Wait for full Unity capture: {report_file}")
    report = json.loads(report_file.read_text(encoding="utf-8-sig"))
    if not report.get("success"):
        raise ValueError("Unity report does not confirm successful capture")
    fps = int(report.get("fps", 30))
    if fps != 30:
        raise ValueError(f"Expected actual capture at 30 fps, found {fps}")
    found = {a["role"]: [c["name"] for c in a.get("clips", [])] for a in report.get("actors", [])}
    if not allow_partial and found != EXPECTED:
        raise ValueError(f"Expected all 20 selected clips; found {found}")
    for actor in report.get("actors", []):
        for row in actor.get("clips", []):
            if actor['role'] == 'Rogue' and row['name'] == '2H_Ranged_Aiming' and row.get('loop'):
                raise ValueError('Rogue 2H_Ranged_Aiming must be a one-shot (loop=false); recapture the updated metadata')
            folder = media / "Frames" / actor["role"] / row["name"] / "Side"
            files = pngs(folder)
            expected = int(row.get("frames", math.ceil(row["seconds"] * fps) + 1))
            indices = [int(p.stem) for p in files]
            if indices != list(range(expected)):
                raise ValueError(f"Incomplete side PNG sequence: {folder}; expected 0..{expected - 1}, got {len(files)} PNGs")
            animation_intervals(files, float(row["seconds"]), fps)
        if not skip_connections:
            by_name = {c['name']: c for c in actor.get('clips', [])}
            for transition in actor.get('connections', []):
                first, second = transition.split(' -> ', 1)
                folder = media/'Connections'/actor['role']/(first+'_to_'+second)
                files = pngs(folder/'Blend')
                seconds = min(.30,float(by_name[first]['seconds']))+.12+min(.4,float(by_name[second]['seconds']))
                expected = math.ceil(seconds*fps)+1
                if not allow_partial and ([int(p.stem) for p in files] != list(range(expected)) or not (folder/'raw_end.png').exists() or not (folder/'raw_start.png').exists()):
                    raise ValueError(f'Incomplete connection capture: {folder}; expected {expected} Blend PNGs and raw end/start PNGs')
                if files:animation_intervals(files,seconds,fps)
    return report, fps


def link(path, media: Path) -> str:
    return html.escape(str(Path(path).relative_to(media)).replace("\\", "/"), quote=True)


def card(item: dict, media: Path, connection=False) -> str:
    role, name, row = item["role"], item["name"], item["row"]
    meaning = MEANINGS.get(name, "검수용 모션 연결" if connection else "원본 동작")
    source_action = MINION_SOURCE.get(name,name) if role == 'Minion' else name
    gif = item["gifs"][0]
    slow = item["gifs"][1]
    sheets = " ".join(f'<a href="{link(s["path"],media)}">{html.escape(VIEWS_KO.get(s["view"],s["view"]))} 전체 {s["png_count"]}컷{(" · p"+str(s["page"])) if len(item["contacts"])>4 else ""}</a>' for s in item["contacts"])
    diagnostics = ""
    if not connection:
        diagnostics = f'<p class="small">무기 포함 전체 모델 최저 Y {row.get("minimumY",0):.5f}m · 끝 자세 차이 {row.get("endpointDifference",0):.5f}m. 발 접지 높이만을 뜻하지 않으며 수치만으로 품질을 판정하지 않습니다.</p>'
        if role == 'Rogue' and name in ('Idle_Combat','Hit_A'):
            diagnostics += '<p class="small">남은 조합 제약: 원본 대기·피격 동작에 석궁을 부착하면 바닥 관통이 남습니다. 원래 뼈·키·무기 비율을 보존한 검수본이며 게임 최종 승인 상태가 아닙니다.</p>'
        if role == 'Mage':
            diagnostics += '<p class="small">남은 조합 제약: 원본 동작과 왼손 스태프 조합의 일부 프레임에 스태프·모자 간섭 및 끝부분의 바닥 접촉이 보고되었습니다. 이 카드의 전체 프레임을 확인해야 하며 게임 최종 승인 상태가 아닙니다.</p>'
        if role == 'Warrior' and name == '1H_Melee_Attack_Slice_Diagonal':
            diagnostics += '<p class="small">남은 조합 제약: 대각선 베기의 회수 구간에서 도끼 끝이 기준선 아래로 내려가는 일부 프레임이 남습니다. 원본 동작과 원래 한손 도끼 비율을 유지한 검수본입니다.</p>'
    raw = ""
    if item.get("raw_end"):
        raw = f'<p class="small"><a href="{link(item["raw_end"],media)}">원래 끝 자세</a> · <a href="{link(item["raw_start"],media)}">다음 원래 시작 자세</a></p>'
    source_name = f'<p class="small">원본 동작: <code>{html.escape(source_action)}</code></p><p class="small path">{html.escape(row.get("asset", "검수용 Playables 연결 · 인게임 로직 미수정"))}</p>' if not connection else f'<p class="small path">{html.escape(row.get("asset", "검수용 연결"))}</p>'
    return f'''<article class="card"><div class="card-head"><span class="badge">{html.escape(ROLES_KO.get(role,role))}</span><h3>{html.escape(name)}</h3><p>{meaning} · 원본 {float(row['seconds']):.3f}초 · {'반복 클립' if row.get('loop') else '단발 동작 + 반복 사이 0.5초 정지'}</p></div>
<a href="{link(gif['path'],media)}"><img loading="lazy" width="960" height="588" src="{link(gif['path'],media)}" alt="{html.escape(role+' '+name)} 원속도 실제 Unity 모델"></a>
<div class="body"><p><a href="{link(gif['path'],media)}">1배속 GIF</a> · <a href="{link(slow['path'],media)}">0.5배속 GIF</a></p>
<details><summary>느린 재생 펼치기</summary><img loading="lazy" width="960" height="588" src="{link(slow['path'],media)}" alt="0.5배속"></details>
<p class="sheets">{sheets}</p>{raw}{diagnostics}{source_name}</div></article>'''


def write_html(items: list[dict], connections: list[dict], report: dict, media: Path):
    sections = []
    nav = []
    for role in EXPECTED:
        subset = [i for i in items if i["role"] == role]
        if not subset:
            continue
        nav.append(f'<a href="#{role}">{html.escape(ROLES_KO[role])} {len(subset)}</a>')
        extra = "기존 색상·크기·바닥 높이 보정 샘플" if role == "Minion" else "KayKit Skeletons 1.0 원본 모델·키 · 같은 wrapper 배율"
        sections.append(f'<section id="{role}"><h2>{html.escape(ROLES_KO[role])} <small>{role}</small></h2><p>{extra}</p><div class="grid">'+"".join(card(i,media) for i in subset)+"</div></section>")
    connection_html = "".join(f'<h3>{html.escape(ROLES_KO[role])}</h3><div class="grid">'+"".join(card(i,media,True) for i in connections if i["role"]==role)+"</div>" for role in EXPECTED if any(i["role"]==role for i in connections))
    records = []
    for folder in (media,media/'QA',media/'Independent'):
        if not folder.exists():continue
        for path in sorted(folder.iterdir()):
            name=path.name.lower()
            if path.is_file() and path.suffix.lower() in ('.json','.txt','.md') and ('main-project-import-validation' in name or ('visual' in name and 'review' in name) or name.startswith('final-')):
                records.append(f'<a href="{link(path,media)}">{html.escape(path.name)}</a>')
    record_links = ('<p class="small">추가 검수 기록: '+ ' · '.join(records)+'</p>') if records else ''
    method = html.escape(report.get("method", ""))
    page = f'''<!doctype html><html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Project M 스켈레톤 4종 동작 검수</title><style>
*{{box-sizing:border-box}}body{{margin:0;background:#101c25;color:#d9e6eb;font:15px/1.65 'Malgun Gothic',sans-serif}}header,main{{max-width:1680px;margin:auto;padding:25px}}header{{padding-bottom:14px}}h1{{font-size:27px;margin:0 0 10px}}h2{{font-size:24px;margin:34px 0 2px}}h3{{font:600 18px/1.4 Consolas,'Malgun Gothic',monospace;margin:10px 0}}p{{margin:8px 0}}a{{color:#a3d7dc;text-underline-offset:3px}}nav{{display:flex;gap:12px;flex-wrap:wrap;margin:20px 0}}nav a,.badge{{background:#273c48;padding:5px 10px;border-radius:5px}}nav a{{text-decoration:none}}.badge{{font-size:12px;color:#e4c998}}.grid{{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:20px}}.card{{background:#172a35;border:1px solid #344c5a;border-radius:9px;overflow:hidden}}.card-head,.body{{padding:14px 18px}}.card-head p{{font-size:12px;color:#aec1cd}}img{{display:block;max-width:100%;height:auto}}.small{{font-size:12px;color:#a2bac8}}.path{{overflow-wrap:anywhere;font-family:Consolas,monospace}}.sheets{{display:flex;flex-wrap:wrap;gap:8px 14px;font-size:12px}}details{{border:1px solid #365362;border-radius:7px;padding:9px 12px;margin:12px 0}}summary{{cursor:pointer;color:#d8c593}}details img{{margin-top:12px}}.notice{{max-width:1150px;color:#aec6d1}}code{{color:#d8c593}}small{{color:#9eb5c1;font-weight:400}}@media(max-width:1000px){{.grid{{grid-template-columns:1fr}}header,main{{padding:16px}}h1{{font-size:23px}}}}
</style></head><body><header><h1>스켈레톤 4종 게임용 동작 검수</h1><p>{len(items)}개 선택 클립 · 신규 3종은 원본 이름 유지 · 실제 Unity 스킨 모델 · 30 fps 캡처</p><div class="notice"><p>측면 화면 왼쪽은 현재 주인공의 정지 참조, 오른쪽은 해당 몬스터입니다. 같은 월드 단위·카메라 배율을 사용합니다. 신규 3종의 공통 wrapper 배율은 0.585289224이며 원본 메시·뼈·키를 체형별로 바꾸지 않습니다. 정면·반대쪽·후면은 같은 배율의 몬스터 단독 화면입니다.</p><p>Warrior는 원래 짧은 한손 도끼에 맞는 원본 한손 공격을 선택했습니다. Rogue의 2H_Ranged_Aiming은 팔을 내려둔 자세에서 조준으로 끝나는 단발 동작이며 반복 클립으로 표시하지 않습니다. 원본 뼈·키는 바꾸지 않습니다. 최종 정적 장착은 Rogue 오른손 석궁 local Y −90°, Warrior 오른손 도끼 identity, Mage 왼손 스태프 local Z +90°입니다. Mage의 오른손은 시전용으로 비웁니다. 원본 몸체·리그·동작 키와 무기 비율은 유지합니다.</p><p>주인공은 격리된 Built-in 검수 장면의 복제본입니다. 복제본의 검수용 재질에 원래 URP의 albedo 텍스처·기본색을 사용하며, 원본 주인공 에셋은 수정하지 않습니다.</p><p>각 GIF는 원래 클립 전체를 한 번 이상 재생합니다. 단발 동작 반복 사이의 0.5초 정지는 재생 안내에 표시됩니다. GIF의 10ms 시간 단위에 맞춰 프레임 지연을 분산했고, 원본 PNG와 끝 프레임은 모두 보존했습니다. 전체 프레임 시트에서 준비·타격·회수를 끊김 없이 확인할 수 있습니다. 반복 동작의 보조 시점은 Unity가 제공한 주요 자세만 있을 수 있습니다.</p><p><strong>잔여 제약이 있으며 게임 최종 승인 상태가 아닙니다.</strong> Rogue 대기·피격의 석궁 바닥 관통, Mage 스태프·모자 간섭과 일부 스태프 끝 바닥 접촉은 원본 동작과 부착 장비의 조합 제약입니다. 다른 원본 후보를 비교해도 남은 문제를 임의의 뼈·키·무기 비율 변경으로 숨기지 않았습니다. 원본 맨손 모델이나 보존된 95개 액션 전체가 사용 불가라는 뜻은 아닙니다. 원본 수치·키 보존 검사 통과는 시각적 품질 승인이 아니며, 실제 프로젝트의 최종 Unity 검증은 별도입니다.</p></div><nav>{''.join(nav)}<a href="#connections">모션 연결</a></nav><p class="small"><a href="review-media-manifest.json">미디어 manifest</a> · <a href="unity-review-report.json">Unity 원본 검수 보고서</a></p></header><main>{''.join(sections)}
<section id="connections"><details><summary>검수용 모션 연결 {len(connections)}개 펼치기</summary><p>0.12초 Playables 혼합을 사용하는 별도 미리보기입니다. 각 원본 클립과 게임 Animator 전환 로직은 수정하지 않습니다. 원래 끝·시작 자세 링크도 함께 제공합니다.</p>{connection_html if connection_html else '<p>Connections 캡처 대기 중.</p>'}</details></section><details><summary>캡처 정보 및 확인 범위</summary><p class="small">Unity {html.escape(str(report.get('unity','unknown')))} · {report.get('width',960)}×{report.get('height',540)} · {report.get('fps',30)}fps · orthographicSize {report.get('orthographicSize','unknown')}</p><p class="small">{method}</p><p class="small">접지·파지·관절·스킨·실루엣은 실제 이미지로 확인해야 합니다. 바닥 최저점·끝 자세 차이 등 수치가 자연스러운 동작을 보증하지 않습니다. 모든 GIF는 Unity 캡처로 제작했으며 생성 영상으로 대체하지 않았습니다.</p></details></main></body></html>'''
    main_validation=None
    for candidate in (media/'main-project-import-validation.json',media/'QA/main-project-import-validation.json',Path(__file__).resolve().parents[1]/'QA/main-project-import-validation.json'):
        if candidate.exists():
            main_validation=json.loads(candidate.read_text(encoding='utf-8-sig'));break
    main_pass=bool(main_validation and main_validation.get('success') and not main_validation.get('errors'))
    if main_pass:
        counts=', '.join(f"{html.escape(r['role'])} {r.get('embeddedTakes','?')}액션/{r.get('originalBonesChecked','?')}본" for r in main_validation.get('roles',[]))
        note=f"<p>메인 Unity 검증 기록: {counts}, 선택 {main_validation.get('selectedClips','?')}클립 곡선·원본 메시·bind-pose·rest TRS 및 보호 {main_validation.get('protectedFilesChecked','?')}파일 해시를 확인했습니다. 구조 검증 통과는 시각적 품질 승인이나 인게임 통합을 뜻하지 않습니다.</p>"
        page=page.replace('<p><strong>잔여 제약이 있으며 게임 최종 승인 상태가 아닙니다.</strong>',note+'<p><strong>잔여 제약이 있으며 게임 최종 승인 상태가 아닙니다.</strong>')
    page=page.replace('Mage 스태프·모자 간섭과 일부 스태프 끝 바닥 접촉은 원본 동작과 부착 장비의 조합 제약입니다.', 'Mage 스태프·모자 간섭과 일부 스태프 끝 바닥 접촉, Warrior 대각선 베기 회수 때 도끼 끝이 기준선 아래로 내려가는 현상은 원본 동작과 부착 장비의 조합 제약입니다.')
    if main_pass:page=page.replace('실제 프로젝트의 최종 Unity 검증은 별도입니다.', '메인 Unity의 원본 보존·구조 검증은 완료했고, 실제 게임 이동 속도·AI·Animator 연결은 검증 범위에서 제외합니다.')
    page=page.replace('</header><main>',record_links+'</header><main>')
    (media / "index.html").write_text(page, encoding="utf-8")


def serializable(value, media: Path):
    if isinstance(value, Path):
        return str(value.relative_to(media)).replace("\\", "/")
    if isinstance(value, dict):
        return {k: serializable(v,media) for k,v in value.items()}
    if isinstance(value, list):
        return [serializable(v,media) for v in value]
    return value


def self_test():
    assert sum(quantize_delays([1000/30]*30)) == 1000
    assert sum(quantize_delays([2000/30]*30)) == 2000
    assert set(quantize_delays([1000/30]*30)) == {30,40}
    for speed, expected in ((1,1500),(.5,2500)):
        delays = quantize_delays([1000/30/speed]*30+[500])
        frames = [Image.new("P",(4,4),i) for i in range(31)]
        buffer = io.BytesIO()
        frames[0].save(buffer,format="GIF",save_all=True,append_images=frames[1:],duration=delays,loop=0,disposal=2,optimize=False)
        buffer.seek(0)
        with Image.open(buffer) as gif:
            actual=[]
            for i in range(gif.n_frames):
                gif.seek(i);actual.append(gif.info['duration'])
        assert sum(actual)==expected,(speed,actual)
    assert sum(map(len,EXPECTED.values()))==20
    print("SELF_TEST_OK: 30 fps timing, 0.5x timing, fixed500ms hold, actual GIF roundtrip, selected20")


def main():
    parser=argparse.ArgumentParser(description=__doc__,formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--media-root",type=Path,default=DEFAULT_MEDIA)
    parser.add_argument("--check",action="store_true",help="Validate inputs only; write nothing")
    parser.add_argument("--html-only",action="store_true",help="Refresh gallery notes and optional QA links from the existing media manifest; do not re-encode media")
    parser.add_argument("--self-test",action="store_true",help="In-memory timing/GIF test; write nothing")
    parser.add_argument("--allow-partial",action="store_true",help="Explicitly permit a subset of selected clips")
    parser.add_argument("--skip-connections",action="store_true")
    args=parser.parse_args()
    if args.self_test:
        self_test();return
    media=args.media_root.resolve()
    if args.html_only:
        report=json.loads((media/'unity-review-report.json').read_text(encoding='utf-8-sig'))
        manifest=json.loads((media/'review-media-manifest.json').read_text(encoding='utf-8-sig'))
        for item in manifest['clips']+manifest['connections']:
            for part in item.get('gifs',[])+item.get('contacts',[]):part['path']=media/part['path']
            for key in ('raw_end','raw_start'):
                if item.get(key):item[key]=media/item[key]
        write_html(manifest['clips'],manifest['connections'],report,media)
        print('HTML_REFRESHED '+str(media/'index.html'));return
    report,fps=check_inputs(media,args.allow_partial,args.skip_connections)
    if args.check:
        print(json.dumps({"ready":True,"selected_clips":sum(len(a['clips']) for a in report['actors']),"fps":fps,"media_root":str(media)},ensure_ascii=False));return
    items=[];connections=[];missing_connections=[]
    for actor in report['actors']:
        role=actor['role']
        for row in actor['clips']:
            folder=media/'Frames'/role/row['name'];paths=pngs(folder/'Side')
            item={'role':role,'name':row['name'],'source_action':MINION_SOURCE.get(row['name'],row['name']) if role=='Minion' else row['name'],'row':row,'gifs':[],'contacts':[]}
            for speed,tag in ((1.0,'1x'),(.5,'0p5x')):
                item['gifs'].append(make_gif(paths,row,role,media/'GIF'/role/(row['name']+'_'+tag+'.gif'),speed,fps))
            for view in ['Side','Front','Opposite','Back']:
                view_files=pngs(folder/view)
                if view_files:item['contacts'].extend(contacts(view_files,row,role,view,media,fps))
            items.append(item)
            print(f"COMPOSED {role}/{row['name']} {len(paths)} PNGs",flush=True)
        if args.skip_connections:continue
        by_name={c['name']:c for c in actor['clips']}
        for transition in actor.get('connections',[]):
            first,second=transition.split(' -> ',1)
            name=first+'_to_'+second;folder=media/'Connections'/role/name
            paths=pngs(folder/'Blend')
            if not paths:
                missing_connections.append(role+'/'+name);continue
            seconds=min(.30,float(by_name[first]['seconds']))+.12+min(.4,float(by_name[second]['seconds']))
            row={'name':name,'seconds':seconds,'loop':False,'asset':'Review only: '+transition+'; blend0.12s'}
            item={'role':role,'name':name,'row':row,'gifs':[],'contacts':contacts(paths,row,role,'Blend',media,fps)}
            for speed,tag in ((1.0,'1x'),(.5,'0p5x')):
                item['gifs'].append(make_gif(paths,row,role,media/'GIF'/'Connections'/role/(name+'_'+tag+'.gif'),speed,fps,True))
            if (folder/'raw_end.png').exists() and (folder/'raw_start.png').exists():
                item.update(raw_end=folder/'raw_end.png',raw_start=folder/'raw_start.png')
            connections.append(item)
    manifest={'created_utc':datetime.now(timezone.utc).isoformat(),'source_report':'unity-review-report.json','source_report_sha256':sha256(media/'unity-review-report.json'),
              'fps':fps,'selected_clip_count':len(items),'gif_count':sum(len(i['gifs']) for i in items),'connection_count':len(connections),'connection_gif_count':sum(len(i['gifs']) for i in connections),'total_gif_count':sum(len(i['gifs']) for i in items+connections),'missing_connections':missing_connections,
              'source_pngs_modified_or_deleted':False,'endpoint_policy':'All PNGs retained. Zero-duration endpoint excluded from animation intervals; one-shot endpoint displayed as explicit500ms recovery hold.',
              'gif_time_quantum_ms':10,'timing_method':'Cumulative10ms rounding distributes30fps as30/40/30ms;0.5x as70/60/70ms; one-shot hold stays500ms.',
              'labels':'Header added above original capture pixels, left current protagonist/right subject; installed system font used for labels only, no font binary distributed.',
              'visual_approval':False,'gameplay_logic_changed':False,'clips':items,'connections':connections}
    (media/'review-media-manifest.json').write_text(json.dumps(serializable(manifest,media),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    write_html(items,connections,report,media)
    print(json.dumps({'complete':True,'selected_clips':len(items),'connections':len(connections),'index':str(media/'index.html')},ensure_ascii=False))


if __name__ == '__main__':
    try:main()
    except (FileNotFoundError,ValueError,RuntimeError) as exc:
        print('COMPOSE_REVIEW_ERROR: '+str(exc),file=sys.stderr)
        raise SystemExit(2)
