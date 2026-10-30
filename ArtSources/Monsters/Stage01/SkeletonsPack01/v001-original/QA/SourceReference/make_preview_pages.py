from pathlib import Path
import json
from PIL import Image, ImageDraw

root = Path('C:/Users/me/Desktop/OZGL3_ArtReview/Monsters/Stage01/SkeletonsPack01/v001-original')
report = json.loads((root / 'preview-report.json').read_text(encoding='utf-8-sig'))
out = root / 'Independent'
out.mkdir(exist_ok=True)
for actor in report['actors']:
    role = actor['role']
    clips = actor['clips']
    for view in ['Side', 'Front', 'Opposite', 'Back']:
        rows = []
        for clip in clips:
            paths = sorted((root / 'Preview' / role / clip['name'] / view).glob('*.png'))
            if not paths:
                continue
            # Preserve the existing five key samples when that is what the preview contains.
            # For final sequences, pick equally spaced keys; this is not an every-frame sheet.
            selected = [paths[round(i * (len(paths)-1)/4)] for i in range(5)]
            rows.append((clip['name'], selected, len(paths)))
        if not rows:
            continue
        sheet = Image.new('RGB', (1500, 355 * len(rows)), '#20252a')
        draw = ImageDraw.Draw(sheet)
        for row, (name, paths, count) in enumerate(rows):
            for col, path in enumerate(paths):
                with Image.open(path) as source:
                    crop = source.crop((490, 12, 930, 530) if view == 'Side' else (260, 12, 700, 530))
                    crop.thumbnail((300, 325))
                    sheet.paste(crop, (col * 300, row * 355 + 30))
                draw.text((col * 300 + 3, row * 355 + 3), f'{role} {name}', fill='white')
                draw.text((col * 300 + 3, row * 355 + 16), f'{view} f{path.stem} / {count} PNGs', fill='#cccccc')
        sheet.save(out / f'PreviewKeys_{role}_{view}.jpg', quality=93)
print('Saved key-sample sheets in', out)
