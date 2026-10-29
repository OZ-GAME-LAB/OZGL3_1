from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import json
import math
import sys

root = Path('C:/Users/me/Desktop/OZGL3_ArtReview/Monsters/Stage01/SkeletonsPack01/v001-original')
report_path = root / 'unity-review-report.json'
if not report_path.exists():
    report_path = root / 'preview-report.json'
report = json.loads(report_path.read_text(encoding='utf-8-sig'))
out = root / 'Independent' / 'FullFrames'
out.mkdir(exist_ok=True)
font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 13)
ledger = []
for actor in report['actors']:
    role = actor['role']
    if len(sys.argv) > 1 and role != sys.argv[1]:
        continue
    for clip in actor['clips']:
        name = clip['name']
        if len(sys.argv) > 2 and name != sys.argv[2]:
            continue
        for view in ['Side', 'Front', 'Opposite', 'Back']:
            paths = sorted((root / 'Frames' / role / name / view).glob('*.png'))
            if not paths:
                continue
            for start in range(0, len(paths), 64):
                subset = paths[start:start+64]
                sheet = Image.new('RGB', (1600, 32 + 228 * math.ceil(len(subset)/8)), '#20252a')
                draw = ImageDraw.Draw(sheet)
                draw.text((5, 5), f'{role} / {name} / {view} / all PNGs {start+1}-{start+len(subset)} of {len(paths)}', font=font, fill='white')
                for i, path in enumerate(subset):
                    with Image.open(path) as source:
                        crop = source.crop((440, 0, 960, 540) if view == 'Side' else (200, 0, 760, 540)).convert('RGB')
                        crop.thumbnail((200, 208), Image.Resampling.LANCZOS)
                        x, y = i % 8 * 200, 32 + i // 8 * 228
                        sheet.paste(crop, (x+(200-crop.width)//2, y+20))
                    draw.text((x+4, y+3), f'f{path.stem} {int(path.stem)/30:.3f}s', font=font, fill='#eeeeee')
                dest = out / f'{role}_{name}_{view}_{start//64+1:02d}.jpg'
                sheet.save(dest, quality=94)
                ledger.append({'sheet': str(dest), 'role':role,'clip':name,'view':view,'first':subset[0].name,'last':subset[-1].name,'frames':len(subset)})
suffix = '_'.join(sys.argv[1:]) if len(sys.argv) > 1 else 'all'
(out / f'manifest_{suffix}.json').write_text(json.dumps(ledger, indent=2), encoding='utf-8')
print(json.dumps({'sheets': len(ledger), 'frames_shown':sum(x['frames'] for x in ledger),'output':str(out)}))
