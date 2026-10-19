"""Encode actual Unity render frames as GIFs and reproducible side-view review sheets."""
from pathlib import Path
import json
import math
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
FONT = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 17)
SMALL = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 14)
REPORT = json.loads((ROOT / 'motion-review.json').read_text(encoding='utf-8'))
BY_NAME = {m['name']: m for m in REPORT['motions']}


def frames(name):
    return [Image.open(p).convert('RGB') for p in sorted((ROOT / 'Frames' / name).glob('*.png'))]


def palette_for(images):
    picks = images[::max(1, len(images) // 12)]
    sheet = Image.new('RGB', (192 * len(picks), 108))
    for index, frame in enumerate(picks):
        sheet.paste(frame.resize((192, 108)), (192 * index, 0))
    return sheet.quantize(colors=192)


def gif(path, images, fps=16):
    palette = palette_for(images)
    output = [im.quantize(palette=palette, dither=Image.Dither.NONE) for im in images]
    timings = [60 if i % 4 != 3 else 70 for i in range(len(images))]
    output[0].save(path, save_all=True, append_images=output[1:], duration=timings,
                   loop=0, disposal=2, optimize=True)


def title(image, label, subtitle=''):
    image = image.copy()
    draw = ImageDraw.Draw(image)
    draw.rectangle((0, 0, image.width, 29), fill=(19, 26, 33))
    draw.text((10, 5), label, font=FONT, fill=(224, 232, 235))
    if subtitle:
        draw.text((image.width - 220, 7), subtitle, font=SMALL, fill=(143, 163, 174))
    return image


def individual():
    destination = ROOT / 'GIF'
    destination.mkdir(exist_ok=True)
    for name, motion in BY_NAME.items():
        images = [title(im, name.replace('_', ' '), 'UNITY / SIDE VIEW / 16 FPS') for im in frames(name)]
        gif(destination / (name + '.gif'), images)


def overview(name, names, columns=2):
    sequences = {key: frames(key) for key in names}
    rows = math.ceil(len(names) / columns)
    images = []
    for frame in range(80):
        sheet = Image.new('RGB', (384 * columns, 216 * rows), (19, 26, 33))
        for index, key in enumerate(names):
            sequence = sequences[key]
            tile = sequence[frame % len(sequence)].resize((384, 216), Image.Resampling.LANCZOS)
            tile = title(tile, key.replace('_', ' '))
            sheet.paste(tile, ((index % columns) * 384, (index // columns) * 216))
        images.append(sheet)
    gif(ROOT / (name + '.gif'), images)


def audit(name, names):
    w, h = 320, 180
    sheet = Image.new('RGB', (w * 6, (h + 28) * len(names)), (19, 26, 33))
    draw = ImageDraw.Draw(sheet)
    for row, key in enumerate(names):
        motion = BY_NAME[key]
        sequence = frames(key)
        draw.text((8, row * (h + 28) + 4), key + ' / ' + str(round(motion['playbackSeconds'], 3)) + ' sec', font=FONT, fill=(220, 230, 235))
        for col, fraction in enumerate((0, .2, .4, .6, .8, .999)):
            hold = 0 if key in ('Idle', 'Walk', 'Run') else .25
            sample = min(len(sequence) - 1, round((hold + motion['playbackSeconds'] * fraction) * 16))
            tile = sequence[sample].resize((w, h), Image.Resampling.LANCZOS)
            sheet.paste(tile, (col * w, row * (h + 28) + 28))
    sheet.save(ROOT / (name + '.png'))


def gallery():
    cards = '\n'.join('<figure><img loading="lazy" src="GIF/' + name + '.gif"><figcaption>' +
                      name.replace('_', ' ') + ' · ' + str(round(motion['playbackSeconds'], 3)) + '초</figcaption></figure>'
                      for name, motion in BY_NAME.items())
    environment = json.loads((ROOT / 'environment-review.json').read_text(encoding='utf-8'))
    shots = '\n'.join('<figure class="environment"><img loading="lazy" src="Environment/' +
                      Path(shot['image']).name + '"><figcaption>' + shot['name'] + '</figcaption></figure>'
                      for shot in environment['shots'])
    page = '''<!doctype html><html lang="ko"><meta charset="utf-8"><title>주인공 · 도시 사이드뷰 검수</title>
<style>body{margin:28px;background:#141c24;color:#dce5eb;font:16px/1.6 system-ui}h1{font-size:25px}p{max-width:1000px;color:#b4c3ce}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(360px,1fr));gap:20px}figure{margin:0;background:#202c37;border-radius:8px;overflow:hidden}img{display:block;width:100%}figcaption{padding:8px 14px}.environment{grid-column:1/-1;max-width:1600px}</style>
<h1>주인공 동작 28개 · 우중충한 도시</h1><p>Unity의 실제 저장된 Humanoid/Animator/장착 프리팹으로 렌더링했습니다. 기본 동작 15종과 좌우 방향·장비 조건을 함께 확인할 수 있습니다. X/Y 축으로 플레이하며 카메라는 측면에서 약 7도만 기울여 두께가 보이게 했습니다. 동작 GIF는 16fps입니다. 투척은 아트 미리보기이며 실제 아이템 효과는 게임 콜백에 연결해야 합니다. 이번 개정의 추가 Meshy 크레딧은 0입니다.</p><main>'''
    (ROOT / 'index.html').write_text(page + cards + shots + '</main></html>', encoding='utf-8')


if __name__ == '__main__':
    individual()
    overview('01_movement', ['Idle', 'Walk', 'Run', 'Jump', 'Land', 'Dash'], 3)
    overview('02_combat', ['Punch', 'SwordSlash', 'MagicCast', 'Throw'], 2)
    overview('03_actions', ['Drink', 'Interact', 'Pickup', 'Hit', 'Dead'], 3)
    overview('04_left_facing', ['Punch_Left', 'SwordSlash_Left', 'MagicCast_Left', 'Throw_Left'], 2)
    audit('audit-actions', ['Punch', 'SwordSlash', 'MagicCast', 'Throw', 'Drink', 'Pickup'])
    audit('audit-movement', ['Walk', 'Run', 'Jump', 'Dash', 'Hit', 'Dead'])
    gallery()
    print('Encoded', len(BY_NAME), 'individual GIFs and four overview GIFs.')
