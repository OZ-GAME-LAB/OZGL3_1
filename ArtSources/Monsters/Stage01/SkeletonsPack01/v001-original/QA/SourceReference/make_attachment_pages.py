from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path('C:/Users/me/Desktop/OZGL3_ArtReview/Monsters/Stage01/SkeletonsPack01/v001-original')
out = root / 'Independent'
out.mkdir(exist_ok=True)
font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 18)
rotations = ['0_0_0', '-90_0_0', '90_0_0', '0_-90_0', '0_90_0', '0_0_-90', '0_0_90']
for role in ['Rogue', 'Warrior', 'Mage', 'MageLeft']:
    sheet = Image.new('RGB', (1200, 1400), '#19232f')
    draw = ImageDraw.Draw(sheet)
    for n, (rot, view) in enumerate((r, v) for r in rotations for v in ['Side', 'Front']):
        source = root / 'AttachmentStudy' / role / (rot + '_' + view + '.png')
        crop = (490, 20, 930, 535) if view == 'Side' else (260, 20, 700, 535)
        frame = Image.open(source).convert('RGB').crop(crop).resize((300, 325))
        x, y = (n % 4) * 300, (n // 4) * 350
        sheet.paste(frame, (x, y + 24))
        draw.text((x + 6, y + 2), role + ' ' + rot + ' ' + view, font=font, fill='white')
    sheet.save(out / ('AttachmentStudy_' + role + '.jpg'), quality=94)
print('ATTACHMENT_CONTACTS_COMPLETE')
