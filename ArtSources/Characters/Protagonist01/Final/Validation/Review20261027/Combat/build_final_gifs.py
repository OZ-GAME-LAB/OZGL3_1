"""Package actual corrected Unity prefab captures at 30 fps, without added holds."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,hashlib

out=Path(__file__).parent;final=out.parents[2];project=out.parents[6]
source=project/'Logs/AssetAudit20261027/Hero/Frames/CombatRetouched/Combat'
target=final/'Review/Combat'
fontpath=Path('C:/Windows/Fonts/segoeui.ttf')
font=ImageFont.truetype(str(fontpath),12)
entries=[]
for name,expected in [('Longsword_Attack_Chop',21),('Longsword_Attack_Slice',18),('Longsword_Attack_Stab',21)]:
    files=sorted((source/name/'Side').glob('*.png'))[::2]
    assert len(files)==expected
    originals=[Image.open(p).convert('RGB')for p in files]
    for multiplier,suffix in [(1,''),(2,'_Slow')]:
        images=[]
        for i,im in enumerate(originals):
            canvas=Image.new('RGB',(480,524),(21,30,39));canvas.paste(im,(0,44));draw=ImageDraw.Draw(canvas)
            draw.text((12,4),f'{name} | {"1x" if multiplier==1 else "0.5x"}',font=font,fill=(242,244,248))
            draw.text((12,23),f'Unity prefab | elbow skin retouch | {i/30:.3f}s',font=font,fill=(182,196,209));images.append(canvas)
        atlas=Image.new('RGB',(120*len(images),131))
        for i,im in enumerate(images):atlas.paste(im.resize((120,131)),(i*120,0))
        palette=atlas.quantize(colors=256,method=Image.Quantize.MEDIANCUT)
        frames=[im.quantize(palette=palette,dither=Image.Dither.NONE)for im in images]
        durations=[(round((i+1)*100/30)-round(i*100/30))*10*multiplier for i in range(len(frames))]
        p=target/(name+'_Side'+suffix+'.gif')
        frames[0].save(p,save_all=True,append_images=frames[1:],duration=durations,loop=0,disposal=2,optimize=False)
        test=Image.open(p);actual=0
        for i in range(test.n_frames):test.seek(i);actual+=test.info['duration']
        assert test.n_frames==expected and actual==round(expected/30*1000)*multiplier
        entries.append({'file':p.relative_to(final).as_posix(),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'bytes':p.stat().st_size,'clip':name,'speed':'normal' if multiplier==1 else 'slow','frames':expected,'durationMs':actual,'sourceRevision':'2026-10-27 actual Unity corrected Combat prefab'})
(target/'media-manifest.json').write_text(json.dumps({'method':'Actual Unity corrected Combat prefab captures sampled at 60Hz, every second image encoded at 30fps; no artificial hold. A fixed palette per GIF prevents palette flicker. Slow versions double the same frame durations. Legacy15 GIFs are unchanged.','files':entries},indent=2),encoding='utf8')
print('SIX_UNITY_GIFS_VERIFIED')
