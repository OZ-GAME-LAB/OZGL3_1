"""Build a bounded deletion plan after validating retained hero assets."""
from pathlib import Path
import json,hashlib,re,os
HERE=Path(__file__).resolve().parent
W=next(p for p in HERE.parents if (p/'Assets').is_dir() and (p/'ProjectSettings').is_dir())
S=W/'ArtSources/Characters/Protagonist01';A=W/'Assets/Art/Characters/Protagonist01';F=S/'Final'
VERSIONS=['v006-chibi-cap','v007-compact-action','v008-longsword','v009-longsword-redesign','v010-body-driven','v010-body-driven-longsword','v011-torso-led-longsword','v012-whole-body-longsword','v013-chest-locked-longsword','v014-shoulder-elbow-longsword','v015-upright-overhead-longsword','v016-overhead-arm-correction','v017-kaykit-retarget','v018-kaykit-retouch']
PREVIEW_EDITORS=['ChibiCapModelReview','ChibiCapElasticReview','LongswordV008Delivery','LongswordV009Delivery','LongswordV010Delivery','LongswordV011Delivery']
targets=[S/n for n in VERSIONS]+[S/'CleanupPlan']
for name in ['KayKit_v017']+['Longsword_v%03d'%n for n in range(8,17)]:targets += [A/name,A/(name+'.meta')]
for name in ['Clips','Model']:targets += [A/'ChibiCap_v006'/name,A/'ChibiCap_v006'/(name+'.meta')]
targets += [A/'ChibiCap_v006/Prefabs'/('Protagonist_ChibiCap_Model.prefab'+s) for s in ('','.meta')]
for n in PREVIEW_EDITORS:targets += [A/'Editor'/(n+'.cs'+s) for s in ('','.meta')]
targets=[p.resolve() for p in targets if p.exists()]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def inside(p,t):return p==t or t in p.parents
def willdelete(p):return any(inside(p,t) for t in targets)

# Selected .anim and their .meta bytes must still match the tested receipts.
receipt=json.loads((HERE/'combat-delivery-receipt.json').read_text())
for row in receipt['selectedClips']+receipt['runtimeDependencies']+receipt['combatDependencies']+receipt['usedV006MaterialTexture']:
 p=W/row['path'];assert p.is_file() and sha(p)==row['sha256'],row['path']
 if 'metaSHA256' in row:assert sha(Path(str(p)+'.meta'))==row['metaSHA256'],str(p)
assert len(list((A/'ChibiMotion_v007/Clips').glob('*.anim')))==15
assert len(list((A/'Combat/Clips').glob('*.anim')))==3
assert sha(F/'Combat/Protagonist_Combat.blend')==receipt['editableSourceSHA256']
legacy=json.loads((F/'LegacyMotions/manifest.json').read_text())
for c in legacy['clips']:
 assert sha(F/'LegacyMotions'/c['gif'])==c['gifSHA256']
 assert sha(W/c['unityClip'])==c['unitySHA256']
assert (F/'Model/Protagonist_ChibiCap_Rig.glb').is_file()

# Verify no retained serialized Unity asset points at a GUID being removed.
doomed={};broken=[]
for p in (W/'Assets').rglob('*.meta'):
 if willdelete(p):
  m=re.search(r'^guid:\s*([0-9a-f]{32})',p.read_text(errors='replace'),re.M)
  if m:doomed[m.group(1)]=str(p.relative_to(W))
exts={'.meta','.prefab','.unity','.asset','.controller','.overridecontroller','.anim','.mat','.playable','.mask','.rendertexture','.shadergraph','.vfx','.vfxgraph','.spriteatlas'}
for p in (W/'Assets').rglob('*'):
 if p.is_file() and p.suffix in exts and not willdelete(p):
  for g in set(re.findall(r'\bguid:\s*([0-9a-f]{32})',p.read_text(errors='replace')))&set(doomed):broken.append({'source':str(p.relative_to(W)),'deletedTargetMeta':doomed[g]})
assert not broken,broken

rows=[];links=[]
for t in targets:
 assert inside(t,S) or inside(t,A)
 count=0;size=0
 if t.is_dir():
  for base,dirs,files in os.walk(t,followlinks=False):
   for d in list(dirs):
    p=Path(base)/d
    if p.is_symlink() or p.is_junction():links.append(str(p));dirs.remove(d)
   for name in files:
    p=Path(base)/name
    if p.is_symlink():links.append(str(p))
    else:count+=1;size+=p.stat().st_size
 else:count=1;size=t.stat().st_size
 rows.append({'path':str(t),'directory':t.is_dir(),'files':count,'bytes':size})
assert not links,{'unexpectedReparsePoints':links}

# Hash every retained hero production file, including authoring dependencies.
keep={}
for p in A.rglob('*'):
 if p.is_file() and not willdelete(p):keep[str(p.relative_to(W))]=sha(p)
for p in F.rglob('*'):
 if p.is_file() and 'Validation' not in p.relative_to(F).parts:keep[str(p.relative_to(W))]=sha(p)
plan={'authorizedSelection':'User explicitly confirmed 3 latest attacks including Stab + existing v007 nonattack motions; delete unused animation/model versions.',
      'workspace':str(W),'sourceRoot':str(S),'assetRoot':str(A),'gatePassed':True,'selectedClips':18,
      'deleteTargets':rows,'deleteFileCount':sum(r['files'] for r in rows),'deleteBytes':sum(r['bytes'] for r in rows),
      'brokenIncomingReferences':broken,'reparsePoints':links,'retainedHashes':keep}
(HERE/'cleanup-plan.json').write_text(json.dumps(plan,indent=2),encoding='utf8')
print(json.dumps({'gatePassed':True,'targets':len(rows),'files':plan['deleteFileCount'],'bytes':plan['deleteBytes'],'GiB':round(plan['deleteBytes']/1024**3,3),'protectedFiles':len(keep)}))
