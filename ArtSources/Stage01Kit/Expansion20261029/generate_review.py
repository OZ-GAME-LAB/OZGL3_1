#!/usr/bin/env python3
"""Build the offline Stage 1 modeling review from catalog.json and audit.json.

Run with Python 3. Contact sheets additionally use Pillow, when installed.
The catalog is authoritative for stable IDs; this tool never renumbers entries.
"""

from __future__ import annotations

import argparse
import json
import math
import re
from datetime import datetime
from pathlib import Path


KIND_LABELS = {"environment": "환경", "item": "아이템", "assembly": "조립 예시"}
STATUS_LABELS = {"existing": "기존", "new": "신규"}


def read_json(path: Path, default=None):
    if not path.exists():
        return default
    return json.loads(path.read_text(encoding="utf-8-sig"))


def merge_entries(catalog: dict, audit: dict | None) -> list[dict]:
    entries = catalog.get("entries", [])
    if not entries:
        raise ValueError("catalog.json에 entries 목록이 없습니다.")
    ids = [str(entry.get("id", "")) for entry in entries]
    if any(not value for value in ids) or len(ids) != len(set(ids)):
        raise ValueError("catalog.json의 id는 비어 있지 않아야 하며 서로 달라야 합니다.")
    retired_ids = {entry["id"] for entry in catalog.get("retired_entries", [])}
    if retired_ids.intersection(ids):
        raise ValueError("삭제한 번호를 현재 목록에 재사용할 수 없습니다.")
    audited = {entry["id"]: entry for entry in (audit or {}).get("entries", [])}
    result = []
    for entry in entries:
        item = dict(entry)
        item["audit"] = audited.get(entry["id"], {})
        result.append(item)
    return result


def dimension_text(audit: dict) -> str:
    dimensions = audit.get("dimensions") or {}
    if all(dimensions.get(axis) is not None for axis in "xyz"):
        return " × ".join(f"{float(dimensions[axis]):.2f}" for axis in "xyz") + " m"
    return "검수 대기"


def write_inventory(root: Path, catalog: dict, entries: list[dict]):
    new_count = sum(entry.get("status") == "new" for entry in entries)
    assembly_count = sum(entry.get("kind") == "assembly" for entry in entries)
    new_environment_count = sum(entry.get("status") == "new" and entry.get("kind") == "environment" for entry in entries)
    kind_counts = {kind: sum(entry.get("kind") == kind for entry in entries) for kind in KIND_LABELS}
    lines = [
        catalog.get("title", "1스테이지 환경 모델링 전체 목록"),
        f"기준일: {catalog.get('date', '2026-10-29')}",
        "",
        f"전체 {len(entries)}항목 · 개별 모델 {len(entries) - assembly_count}종 · 조립 {assembly_count}종 · 신규 환경 {new_environment_count}종",
        f"기존 {len(entries) - new_count}항목 / 신규 {new_count}항목 (조립 예시 포함)",
        " / ".join(f"{KIND_LABELS[kind]} {count}개" for kind, count in kind_counts.items()),
        "",
        "피드백은 고정 번호와 함께 남겨 주세요. 예: S1-032 지붕을 조금 낮춰 주세요.",
        "크기 순서: X 폭 × Y 높이 × Z 깊이. 단위 m. 조립 예시는 개별 모델과 별도입니다.",
        "",
    ]
    if catalog.get("scope"):
        lines.extend([str(catalog["scope"]), ""])
    retired = catalog.get("retired_entries", [])
    if retired:
        lines.extend(["삭제된 번호: " + " · ".join(entry["id"] for entry in retired) + " (재사용하지 않음)", ""])
    for entry in entries:
        item_audit = entry["audit"]
        # Keep printed number gaps aligned with the stable model IDs after removal.
        printed_number = entry["id"].rsplit("-", 1)[-1].lstrip("0") or "0"
        lines.extend([
            f"{printed_number.zfill(2)}. [{entry['id']}] {entry.get('name_ko', entry.get('key', ''))}",
            f"    {STATUS_LABELS.get(entry.get('status'), entry.get('status', ''))} · "
            f"{KIND_LABELS.get(entry.get('kind'), entry.get('kind', ''))} · {entry.get('key', '')}",
            f"    용도: {entry.get('role', '')}",
            f"    배치: {entry.get('placement', '')}",
            f"    프리팹: {entry.get('prefab', '')}",
            f"    크기: {dimension_text(item_audit)}",
        ])
        if item_audit:
            lines.append(f"    삼각형 (LOD0): {item_audit.get('triangles', '?'):,}" if isinstance(item_audit.get("triangles"), int)
                         else f"    삼각형 (LOD0): {item_audit.get('triangles', '?')}")
            lines.append(f"    Collider: {item_audit.get('colliders', '?')} / LODGroup: {item_audit.get('lodGroups', '?')}")
        if item_audit.get("issues"):
            lines.append("    확인 사항: " + " / ".join(map(str, item_audit["issues"])))
        lines.append("")
    if retired:
        lines.extend(["삭제 이력 — 아래 번호는 재사용하지 않습니다.", ""])
        for entry in retired:
            lines.append(f"[{entry['id']}] {entry.get('name_ko', entry.get('key', ''))} / "
                         f"{entry.get('reason', '삭제')} / {entry.get('retired_date', '')}")
        lines.append("")
    (root / "전체모델링목록.txt").write_text("\n".join(lines), encoding="utf-8-sig")


HTML = r'''<!doctype html>
<html lang="ko">
<head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>1스테이지 모델링 검수</title>
<style>
:root{color-scheme:light;--ink:#152d38;--muted:#607580;--line:#dce4e7;--accent:#25726f;--navy:#172735;--paper:#f3f6f5}
*{box-sizing:border-box}body{margin:0;background:var(--paper);color:var(--ink);font-family:"Malgun Gothic","Apple SD Gothic Neo","Noto Sans KR",system-ui,sans-serif;font-size:14px;line-height:1.6}
button,input,select,textarea{font:inherit}button,select{cursor:pointer}button{border:1px solid #c7d5da;border-radius:8px;background:#fff;color:var(--ink);padding:9px 13px;transition:background .15s}button:hover{background:#eaf2f0}button:focus-visible,input:focus-visible,select:focus-visible,textarea:focus-visible{outline:3px solid #70b2a9;outline-offset:2px}
header{position:sticky;top:0;z-index:5;background:rgba(249,251,250,.98);border-bottom:1px solid var(--line);box-shadow:0 3px 18px #193d4910}
.header-inner,main{max-width:1800px;margin:auto;padding:20px 28px}.eyebrow{font-size:11px;font-weight:800;letter-spacing:.18em;color:var(--accent)}.heading{display:flex;justify-content:space-between;gap:18px;align-items:center}h1{font-size:25px;letter-spacing:-.04em;margin:3px 0 1px;line-height:1.4}#summary{color:var(--muted);margin:0;font-size:13px}.header-actions{display:flex;gap:8px;flex-shrink:0;align-items:center}.primary{color:white;background:var(--accent);border-color:var(--accent)}.primary:hover{background:#1b5a58}.text-link{color:var(--accent);font-size:13px;white-space:nowrap;text-underline-offset:3px}
.tools{display:flex;gap:9px;align-items:center;flex-wrap:wrap;margin-top:17px}.search{flex:1;min-width:220px;max-width:620px;border:1px solid #c7d5da;background:white;border-radius:9px;padding:10px 13px}.tools select{border:1px solid #c7d5da;border-radius:8px;background:white;color:var(--ink);padding:10px 30px 10px 10px}.view-switch{display:flex;gap:0;border:1px solid #c7d5da;border-radius:8px;overflow:hidden}.view-switch button{border:0;border-radius:0;padding:10px 12px}.view-switch button[aria-pressed=true]{color:white;background:var(--navy)}.tools label{color:var(--muted);font-size:12px}.info-line{display:flex;justify-content:space-between;align-items:center;gap:15px;font-size:12px;color:var(--muted);margin-top:10px}.save-status{color:var(--accent)}
.intro{display:flex;align-items:start;justify-content:space-between;gap:20px;margin:0 0 20px}.intro p{margin:0;max-width:1120px;color:var(--muted);font-size:13px}.intro strong{color:var(--ink)}#visible-count{font-size:13px;font-weight:700;white-space:nowrap}.gallery{display:grid;grid-template-columns:repeat(auto-fill,minmax(350px,1fr));gap:22px}.card{overflow:hidden;border:1px solid #d6e0e3;border-radius:13px;background:#fff;box-shadow:0 5px 17px #18303b07}.card-top{padding:15px 17px 12px}.labels{display:flex;align-items:center;gap:7px;flex-wrap:wrap}.id{background:var(--navy);color:#fff;border-radius:5px;font-size:13px;font-weight:800;letter-spacing:.06em;padding:4px 9px;line-height:1.4}.tag{padding:3px 7px;font-size:11px;line-height:1.5;border-radius:5px;background:#edf1f3;color:#60737d}.tag.new{background:#e1efdf;color:#3d6a34}.tag.kind{background:#e7efef;color:#466c6b}h2{font-size:18px;line-height:1.45;letter-spacing:-.035em;margin:11px 0 2px}.key{font-family:Consolas,monospace;font-size:11px;color:var(--muted);overflow-wrap:anywhere}.image-wrap{background:var(--navy);position:relative;aspect-ratio:4/3;display:flex;align-items:center;justify-content:center;border-top:1px solid #152330;border-bottom:1px solid #152330}.preview{width:100%;height:100%;object-fit:contain;cursor:zoom-in}.image-note{color:#8198a8;font-size:12px;text-align:center;padding:20px}.image-label{position:absolute;bottom:10px;right:11px;pointer-events:none;background:#0b1721ad;color:#a4bdc9;border:1px solid #61758755;border-radius:4px;padding:2px 6px;font-size:10px}
.card-body{padding:15px 17px 17px}.role{margin:0;color:#344d59;font-size:13px;min-height:42px}.placement{margin:8px 0 0;color:var(--muted);font-size:12px}.metrics{display:grid;grid-template-columns:1fr 1fr;gap:6px;margin:14px 0 11px;padding-top:12px;border-top:1px solid #e4ebed}.metric{font-size:12px;color:var(--muted)}.metric strong{display:block;font-size:13px;color:#354e5a;font-weight:600}.metric:first-child{grid-column:1/-1}.path-row{display:flex;align-items:center;gap:8px;background:#f4f7f7;padding:9px;border:1px solid #e1e8e8;border-radius:6px}.path{flex:1;min-width:0;overflow-wrap:anywhere;font:10px/1.5 Consolas,monospace;color:#617782}.copy{padding:5px 8px;font-size:11px;flex-shrink:0}.issues{color:#9d6132;background:#fff4e8;border-radius:5px;margin-top:9px;padding:7px 9px;font-size:11px}.feedback-label{display:block;margin:13px 0 5px;color:#486570;font-size:12px;font-weight:700}.feedback{display:block;width:100%;resize:vertical;min-height:79px;border:1px solid #ccdadd;border-radius:7px;background:#fbfdfc;padding:9px 10px;color:var(--ink);font-size:12px;line-height:1.65}.feedback::placeholder{color:#91a1a8}.card.has-feedback{border-color:#84b5a5}.card.has-feedback .feedback{background:#f3faf5;border-color:#9dc7b4}.empty{display:none;padding:90px 20px;text-align:center;color:var(--muted)}.footer{font-size:11px;color:#7d8e95;margin:30px 0 10px;text-align:center}dialog{padding:0;border:0;border-radius:10px;background:var(--navy);color:white;max-width:94vw;max-height:94vh;width:min(1400px,94vw)}dialog::backdrop{background:#07121dcc}.dialog-head{display:flex;justify-content:space-between;align-items:center;padding:12px 17px;font-size:14px}.dialog-head button{background:transparent;color:white;border-color:#536c7a;padding:4px 11px}.dialog-image{width:100%;max-height:80vh;object-fit:contain;display:block}.dialog-caption{font-size:12px;color:#adc1cb;padding:9px 17px 15px}
@media(max-width:700px){.header-inner,main{padding:15px}.heading{align-items:start}.header-actions{flex-direction:column;align-items:end}.header-actions button{padding:8px 10px;font-size:12px}h1{font-size:21px}.eyebrow{font-size:9px}.search{max-width:none;flex-basis:100%}.tools{gap:6px;margin-top:13px}.tools select,.view-switch button{padding-top:8px;padding-bottom:8px;font-size:12px}.info-line{font-size:10px}.gallery{grid-template-columns:1fr;gap:17px}.intro{gap:9px}.intro p{font-size:11px}.text-link{font-size:11px}}
</style></head>
<body><header><div class="header-inner">
<div class="heading"><div><div class="eyebrow">PROJECT M / STAGE 01 / MODEL REVIEW</div><h1 id="title">1스테이지 환경 모델링</h1><p id="summary"></p></div><div class="header-actions"><button class="primary" id="export">피드백 내보내기</button><a class="text-link" href="전체모델링목록.txt" download>전체 목록 TXT</a></div></div>
<div class="tools"><input id="search" class="search" type="search" placeholder="번호 · 한글 이름 · 파일명 검색" aria-label="모델 검색"><select id="status" aria-label="제작 구분"><option value="all">기존 + 신규</option><option value="new">신규만</option><option value="existing">기존만</option></select><select id="kind" aria-label="에셋 종류"><option value="all">모든 종류</option><option value="environment">환경</option><option value="item">아이템</option><option value="assembly">조립 예시</option></select><select id="notes" aria-label="피드백 유무"><option value="all">전체 피드백</option><option value="written">피드백 작성됨</option><option value="empty">피드백 없음</option></select><div class="view-switch" aria-label="미리보기 시점"><button id="perspective" aria-pressed="true">입체 보기</button><button id="side" aria-pressed="false">게임 측면</button></div></div>
<div class="info-line"><span>번호는 고정입니다. 이미지를 누르면 크게 볼 수 있습니다.</span><span id="save-status" class="save-status" role="status">피드백 자동 저장</span></div>
</div></header><main><div class="intro"><p><strong>수정할 모델은 S1-번호로 알려 주세요.</strong> 카드 아래에 메모를 남기고 TXT로 내보낼 수도 있습니다. 크기는 X 폭 × Y 높이 × Z 깊이이며, 조립 예시는 여러 모델을 배치한 예시입니다. <span id="scope"></span></p><span id="visible-count"></span></div><section id="gallery" class="gallery" aria-label="전체 모델 목록"></section><div id="empty" class="empty">검색 조건에 맞는 모델이 없습니다.</div><div class="footer">로컬 검수 갤러리 · 피드백은 이 브라우저에 저장됩니다. 다른 브라우저로 옮기기 전 TXT로 내보내 주세요.<span id="retired-notice" style="display:block;margin-top:8px"></span></div></main>
<dialog id="lightbox"><div class="dialog-head"><strong id="dialog-title"></strong><button id="close-dialog" aria-label="확대 보기 닫기">닫기 ×</button></div><img id="dialog-image" class="dialog-image" alt=""><div id="dialog-caption" class="dialog-caption"></div></dialog>
<script type="application/json" id="review-data">__REVIEW_DATA__</script>
<script>
"use strict";
const data=JSON.parse(document.getElementById("review-data").textContent), entries=data.entries;
const retiredEntries=Array.isArray(data.retired_entries)?data.retired_entries:[];
const $=id=>document.getElementById(id), labels={environment:"환경",item:"아이템",assembly:"조립 예시"};
const storageKey="project-m-stage01-expansion20261029-feedback-v1";
let feedback={},canStore=true,view="perspective",cards=[];
try{feedback=JSON.parse(localStorage.getItem(storageKey)||"{}");if(!feedback||typeof feedback!=="object"||Array.isArray(feedback))feedback={}}catch(error){canStore=false;feedback={}}
const clean=v=>String(v==null?"":v), fmt=n=>typeof n==="number"?n.toLocaleString("ko-KR"):"—";
function el(tag,cls,text){const node=document.createElement(tag);if(cls)node.className=cls;if(text!==undefined)node.textContent=clean(text);return node}
function dimensions(a){const d=a.dimensions||{};return ["x","y","z"].every(k=>Number.isFinite(d[k]))?[d.x,d.y,d.z].map(n=>n.toFixed(2)).join(" × ")+" m":"검수 대기"}
function save(){try{localStorage.setItem(storageKey,JSON.stringify(feedback));canStore=true;$("save-status").textContent="자동 저장됨 · "+Object.values(feedback).filter(n=>clean(n).trim()).length+"개 메모"}catch(error){canStore=false;$("save-status").textContent="이 브라우저는 자동 저장을 지원하지 않습니다. TXT로 내보내 주세요."}}
async function copyPath(path,button){try{await navigator.clipboard.writeText(path)}catch(error){const field=el("textarea");field.value=path;field.style.position="fixed";field.style.opacity="0";document.body.append(field);field.select();const copied=document.execCommand("copy");field.remove();if(!copied){button.textContent="복사 실패";return}}button.textContent="복사됨";setTimeout(()=>button.textContent="복사",1400)}
function imagePath(entry){const a=entry.audit||{};return view==="side"?(a.side_image||"Renders/"+entry.id+"_side.png"):(a.three_quarter_image||"Renders/"+entry.id+"_perspective.png")}
function setImage(card){const path=imagePath(card.entry);card.image.hidden=false;card.placeholder.hidden=true;card.image.src=encodeURI(path);card.viewLabel.textContent=view==="side"?"GAME SIDE":"3/4 PERSPECTIVE"}
function openImage(card){if(card.image.hidden)return;$("dialog-title").textContent=card.entry.id+" · "+card.entry.name_ko;$("dialog-image").src=card.image.src;$("dialog-image").alt=card.image.alt;$("dialog-caption").textContent=(view==="side"?"게임 측면":"입체 보기")+" · "+card.entry.key;$("lightbox").showModal()}
function metric(parent,label,value){const m=el("div","metric",label);m.append(el("strong","",value));parent.append(m)}
function makeCard(entry){const a=entry.audit||{},article=el("article","card"),top=el("div","card-top"),tags=el("div","labels");article.dataset.id=entry.id;
 tags.append(el("span","id",entry.id),el("span","tag "+(entry.status==="new"?"new":""),entry.status==="new"?"신규":"기존"),el("span","tag kind",labels[entry.kind]||entry.kind));top.append(tags,el("h2","",entry.name_ko||entry.key),el("div","key",entry.key));
 const wrap=el("div","image-wrap"),img=el("img","preview"),placeholder=el("div","image-note","미리보기 준비 중\n렌더 파일이 생성되면 다시 열어 주세요."),viewLabel=el("span","image-label");img.alt=entry.id+" "+entry.name_ko;img.loading="lazy";img.decoding="async";img.onerror=()=>{img.hidden=true;placeholder.hidden=false};wrap.append(img,placeholder,viewLabel);
 const body=el("div","card-body"),role=el("p","role",entry.role),placement=el("p","placement","배치 · "+clean(entry.placement)),metrics=el("div","metrics");metric(metrics,"크기 · X × Y × Z",dimensions(a));metric(metrics,"삼각형 (LOD0)",fmt(a.triangles));metric(metrics,"Collider / LODGroup",a.colliders!==undefined?clean(a.colliders)+" / "+clean(a.lodGroups??0):"검수 대기");
 const pathRow=el("div","path-row"),path=el("code","path",entry.prefab),copy=el("button","copy","복사");copy.type="button";copy.title="프리팹 경로 복사";copy.onclick=()=>copyPath(clean(entry.prefab),copy);pathRow.append(path,copy);body.append(role,placement,metrics,pathRow);
 if(a.issues&&a.issues.length)body.append(el("div","issues","확인 사항 · "+a.issues.join(" / ")));
 const label=el("label","feedback-label",entry.id+" 수정 메모"),textarea=el("textarea","feedback");textarea.id="feedback-"+entry.id;label.htmlFor=textarea.id;textarea.value=clean(feedback[entry.id]||"");textarea.placeholder="예: 간판을 조금 작게 / 이 모델 승인";article.classList.toggle("has-feedback",!!textarea.value.trim());textarea.oninput=()=>{feedback[entry.id]=textarea.value;article.classList.toggle("has-feedback",!!textarea.value.trim());save();if($("notes").value!=="all")applyFilters()};body.append(label,textarea);article.append(top,wrap,body);
 const card={entry,article,image:img,placeholder,viewLabel,search:[entry.id,entry.name_ko,entry.key,entry.role,entry.placement,entry.prefab].map(clean).join(" ").toLocaleLowerCase("ko-KR")};img.onclick=()=>openImage(card);setImage(card);return card}
function applyFilters(){const query=$("search").value.trim().toLocaleLowerCase("ko-KR"),tokens=query.split(/\s+/).filter(Boolean),status=$("status").value,kind=$("kind").value,notes=$("notes").value;let visible=0;for(const card of cards){const e=card.entry,has=!!clean(feedback[e.id]).trim(),match=tokens.every(token=>card.search.includes(token))&&(status==="all"||e.status===status)&&(kind==="all"||e.kind===kind)&&(notes==="all"||(notes==="written"?has:!has));card.article.hidden=!match;if(match)visible++}$("visible-count").textContent=visible+" / "+entries.length+"개";$("empty").style.display=visible?"none":"block"}
function changeView(next){view=next;$("side").setAttribute("aria-pressed",String(view==="side"));$("perspective").setAttribute("aria-pressed",String(view==="perspective"));cards.forEach(setImage)}
$("title").textContent=data.title||"1스테이지 환경 모델링";document.title=$("title").textContent;const assemblyCount=entries.filter(e=>e.kind==="assembly").length,newEnvironmentCount=entries.filter(e=>e.status==="new"&&e.kind==="environment").length;$("summary").textContent=(data.date||"2026-10-29")+" · 전체 "+entries.length+"항목 · 개별 모델 "+(entries.length-assemblyCount)+"종 · 조립 "+assemblyCount+"종 · 신규 환경 "+newEnvironmentCount+"종";$("scope").textContent=data.scope||"";
$("retired-notice").textContent=retiredEntries.length?"삭제된 번호 "+retiredEntries.map(e=>e.id).join(" · ")+" (재사용하지 않음) · 이전 메모는 보존되며 TXT 내보내기에 포함됩니다.":"";
const fragment=document.createDocumentFragment();for(const entry of entries){const card=makeCard(entry);cards.push(card);fragment.append(card.article)}$("gallery").append(fragment);$("search").oninput=applyFilters;for(const id of ["status","kind","notes"])$(id).onchange=applyFilters;$("side").onclick=()=>changeView("side");$("perspective").onclick=()=>changeView("perspective");$("close-dialog").onclick=()=>$("lightbox").close();$("lightbox").onclick=e=>{if(e.target===$("lightbox"))$("lightbox").close()};
$("export").onclick=()=>{const known=[...entries,...retiredEntries.map(e=>({...e,retired:true}))],knownIds=new Set(known.map(e=>e.id));for(const id of Object.keys(feedback)){if(!knownIds.has(id))known.push({id,name_ko:"이전 검수 번호",key:"",prefab:"",retired:true})}const written=known.filter(e=>clean(feedback[e.id]).trim()),now=new Date(),lines=["Project M · 1스테이지 모델링 피드백","내보낸 시각: "+now.toLocaleString("ko-KR"),"메모 "+written.length+"개 / 현재 모델 "+entries.length+"항목","번호는 모델 고정 ID입니다. 삭제된 번호의 이전 메모도 보존합니다.",""];for(const e of written)lines.push("["+e.id+"] "+clean(e.name_ko),e.retired?"상태: 삭제 또는 이전 검수 번호 · 번호 재사용 안 함":"상태: 현재 모델","모델: "+clean(e.key),"프리팹: "+clean(e.prefab),clean(feedback[e.id]).trim(),"");if(!written.length)lines.push("작성한 피드백이 없습니다.");const blob=new Blob(["\ufeff"+lines.join("\r\n")],{type:"text/plain;charset=utf-8"}),url=URL.createObjectURL(blob),link=el("a");link.href=url;link.download="Stage01_모델링피드백_"+now.getFullYear()+String(now.getMonth()+1).padStart(2,"0")+String(now.getDate()).padStart(2,"0")+".txt";document.body.append(link);link.click();link.remove();setTimeout(()=>URL.revokeObjectURL(url),1000)};
applyFilters();if(canStore)$("save-status").textContent="피드백 자동 저장 · "+Object.values(feedback).filter(n=>clean(n).trim()).length+"개 메모";else $("save-status").textContent="자동 저장 불가 · 작성 후 TXT로 내보내 주세요.";
</script></body></html>'''


def write_html(root: Path, catalog: dict, entries: list[dict]):
    payload = {key: catalog.get(key) for key in ("title", "date", "scope", "retired_entries")}
    payload["entries"] = entries
    serialized = json.dumps(payload, ensure_ascii=False).replace("</", "<\\/")
    (root / "index.html").write_text(HTML.replace("__REVIEW_DATA__", serialized), encoding="utf-8")


def build_contact_sheets(root: Path, entries: list[dict]) -> list[str]:
    try:
        from PIL import Image, ImageDraw, ImageFont, ImageOps
    except ImportError:
        return ["Pillow 미설치: ContactSheets 생성을 생략했습니다."]

    font_candidates = [Path("C:/Windows/Fonts/malgun.ttf"), Path("C:/Windows/Fonts/malgunsl.ttf")]
    font_path = next((path for path in font_candidates if path.exists()), None)
    if font_path is None:
        return ["한글 글꼴 malgun.ttf를 찾지 못해 ContactSheets 생성을 생략했습니다."]
    font = lambda size: ImageFont.truetype(str(font_path), size)
    title_font, id_font, name_font, small_font = font(32), font(24), font(21), font(17)
    output = root / "ContactSheets"
    output.mkdir(exist_ok=True)
    warnings = []
    generated_paths = set()
    cols, rows, cell_w, cell_h, gutter, margin, header = 4, 4, 430, 340, 18, 28, 104
    width = 2 * margin + cols * cell_w + (cols - 1) * gutter
    height = header + margin + rows * cell_h + (rows - 1) * gutter

    def wrap_text(draw, text, text_font, max_width):
        lines, current = [], ""
        for character in text:
            if current and draw.textlength(current + character, font=text_font) > max_width:
                lines.append(current)
                current = character
            else:
                current += character
        if current:
            lines.append(current)
        return lines

    for status, prefix, group_title in [("new", "NEW", "신규 모델"), ("existing", "EXISTING", "기존 모델")]:
        group = [entry for entry in entries if entry.get("status") == status]
        for page in range(math.ceil(len(group) / (cols * rows))):
            sheet = Image.new("RGB", (width, height), "#eef3f1")
            draw = ImageDraw.Draw(sheet)
            draw.text((margin, 19), f"STAGE 01  /  {group_title}", font=title_font, fill="#203944")
            draw.text((margin, 65), f"{page + 1:02d} / {math.ceil(len(group) / 16):02d}    ·    번호로 수정 의견을 남겨 주세요    ·    X 폭 / Y 높이 / Z 깊이", font=small_font, fill="#607781")
            for slot, entry in enumerate(group[page * 16:(page + 1) * 16]):
                x = margin + (slot % cols) * (cell_w + gutter)
                y = header + (slot // cols) * (cell_h + gutter)
                draw.rounded_rectangle((x, y, x + cell_w, y + cell_h), radius=9, fill="#ffffff", outline="#cfdbdf")
                draw.rounded_rectangle((x + 10, y + 10, x + 129, y + 43), radius=5, fill="#203644")
                draw.text((x + 18, y + 10), entry["id"], font=id_font, fill="#ffffff")
                kind_text = KIND_LABELS.get(entry.get("kind"), entry.get("kind", ""))
                draw.text((x + 144, y + 14), kind_text, font=small_font, fill="#57737a")
                bx, by, bw, bh = x + 10, y + 53, cell_w - 20, 205
                draw.rectangle((bx, by, bx + bw, by + bh), fill="#172735")
                item_audit = entry.get("audit", {})
                image_relative = item_audit.get("three_quarter_image") or f"Renders/{entry['id']}_perspective.png"
                image_path = root / image_relative
                if not image_path.exists():
                    fallback = item_audit.get("side_image") or f"Renders/{entry['id']}_side.png"
                    image_path = root / fallback
                if image_path.exists():
                    try:
                        with Image.open(image_path) as source:
                            thumb = ImageOps.contain(source.convert("RGBA"), (bw, bh), Image.Resampling.LANCZOS)
                            sheet.paste(thumb, (bx + (bw - thumb.width) // 2, by + (bh - thumb.height) // 2), thumb)
                    except OSError as error:
                        warnings.append(f"이미지 읽기 실패 {entry['id']}: {error}")
                else:
                    draw.text((bx + 115, by + 87), "미리보기 준비 중", font=small_font, fill="#9db1bd")
                name_lines = wrap_text(draw, str(entry.get("name_ko", entry.get("key", ""))), name_font, cell_w - 26)
                for line_index, line in enumerate(name_lines[:2]):
                    draw.text((x + 13, y + 264 + line_index * 27), line, font=name_font, fill="#203b46")
                if len(name_lines) > 2:
                    warnings.append(f"{entry['id']} 이름이 두 줄을 넘습니다. 갤러리에서 전체 이름을 확인하세요.")
            sheet_path = output / f"{prefix}_{page + 1:02d}.png"
            sheet.save(sheet_path, optimize=True)
            generated_paths.add(sheet_path)
    # Remove only obsolete pages made by this generator when a group shrinks.
    # User images and unrelated files in ContactSheets are never selected.
    for stale in output.iterdir():
        if stale.is_file() and re.fullmatch(r"(?:NEW|EXISTING)_\d{2,}\.png", stale.name) and stale not in generated_paths:
            stale.unlink()
    return warnings


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parent)
    parser.add_argument("--skip-contact-sheets", action="store_true")
    args = parser.parse_args()
    root = args.root.resolve()
    catalog = read_json(root / "catalog.json")
    if catalog is None:
        parser.error(f"카탈로그가 없습니다: {root / 'catalog.json'}")
    entries = merge_entries(catalog, read_json(root / "audit.json", {}))
    write_inventory(root, catalog, entries)
    write_html(root, catalog, entries)
    warnings = [] if args.skip_contact_sheets else build_contact_sheets(root, entries)
    print(f"Generated index.html + 전체모델링목록.txt ({len(entries)} entries)")
    for warning in warnings:
        print(warning)


if __name__ == "__main__":
    main()
