from pathlib import Path
from PIL import Image,ImageOps,ImageDraw
from collections import defaultdict
import json,hashlib,sys
HERE=Path(__file__).resolve().parent;W=next(p for p in HERE.parents if (p/'Assets').is_dir());source=Path(sys.argv[1]).resolve() if len(sys.argv)>1 else HERE/'hero-audit.json';data=json.loads(source.read_text());label='Retouched' if 'retouched' in source.name else 'Baseline';output=HERE/('Contacts'+label);output.mkdir(exist_ok=True);gifdir=HERE/('GIF'+label);gifdir.mkdir(exist_ok=True)
groups=defaultdict(list)
for r in data['renders']:
 view=r['view'] if r['variant'] in ('MotionReview','Combat') else 'Views'
 groups[(r['set'],r['variant'],r['clip'],view)].append(r)
records=[]
for key,rows in groups.items():
 name='_'.join(key);paths=[]
 for start in range(0,len(rows),12):
  sheet=Image.new('RGB',(960,792),(20,28,36));draw=ImageDraw.Draw(sheet)
  for j,r in enumerate(rows[start:start+12]):
   p=W/r['path'];im=Image.open(p).convert('RGB');thumb=ImageOps.contain(im,(240,240));x=(j%4)*240;y=(j//4)*264;sheet.paste(thumb,(x,y+24));draw.text((x+5,y+4),f'{r["clip"]} | {r["view"]} {r["time"]:.3f}s',fill='white')
  target=output/(name+f'_{start//12+1:02d}.jpg');sheet.save(target,quality=92);paths.append(str(target.relative_to(W)).replace('\\','/'))
 record={'set':key[0],'variant':key[1],'clip':key[2],'view':key[3],'renderedFrames':len(rows),'contactSheets':paths}
 if key[3]=='Side' and key[1] in ('MotionReview','Combat'):
  frames=[]
  for r in rows:
   im=Image.open(W/r['path']).convert('RGB').resize((360,360),Image.Resampling.LANCZOS);canvas=Image.new('RGB',(360,388),(20,28,36));canvas.paste(im,(0,28));ImageDraw.Draw(canvas).text((8,7),f'{r["clip"]} | {r["time"]:.3f}s',fill='white');frames.append(canvas)
  clip=next(c for c in data['clips'] if c['set']==key[0] and c['variant']==key[1] and c['clip']==key[2]);duration=clip['duration'];n=len(frames);delays=[round((i+1)*duration*100/n)*10-round(i*duration*100/n)*10 for i in range(n)];target=gifdir/(name+'.gif');frames[0].save(target,save_all=True,append_images=frames[1:],duration=delays,loop=0,optimize=False,disposal=2);record['gif']=str(target.relative_to(W)).replace('\\','/')
 records.append(record)
(HERE/('presentation-'+label.lower()+'.json')).write_text(json.dumps({'source':str(source.relative_to(W)).replace('\\','/'),'sourceSHA256':hashlib.sha256(source.read_bytes()).hexdigest(),'records':records},indent=2));print('AUDIT_PRESENTATION_READY',label,len(records),'view groups')
