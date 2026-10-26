"""Portable, read-only post-cleanup Unity reference check. Safe to rerun."""
from pathlib import Path
import argparse,json,re,hashlib
HERE=Path(__file__).resolve().parent
W=next(p for p in HERE.parents if (p/'Assets').is_dir() and (p/'ProjectSettings').is_dir())
def read(p):return p.read_text(encoding='utf8',errors='replace')
def refs(t):return set(re.findall(r'\bguid:\s*([0-9a-f]{32})',t))
def rel(p):return str(p.relative_to(W)).replace('\\','/')
def run():
 parser=argparse.ArgumentParser();parser.add_argument('--baseline',default=str(HERE/'unity-guid-baseline.json'));parser.add_argument('--out',default=str(HERE/'guid-validation-current.json'));args=parser.parse_args()
 baseline=json.loads(Path(args.baseline).read_text());before=baseline['guidToAsset'];now={};duplicates=[];broken=[];references={};scanned=0;external=set()
 for mp in (W/'Assets').rglob('*.meta'):
  m=re.search(r'^guid:\s*([0-9a-f]{32})',read(mp),re.M)
  if not m:continue
  g=m.group(1);p=Path(str(mp)[:-5]);name=rel(p)
  if g in now and now[g]!=name:duplicates.append({'guid':g,'paths':[now[g],name]})
  now[g]=name
 extensions={'.prefab','.unity','.asset','.controller','.overridecontroller','.anim','.mat','.playable','.rendertexture','.mask','.lighting','.terrainlayer','.spriteatlas','.shadervariants','.vfx','.shadergraph','.vfxgraph'}
 for p in (W/'Assets').rglob('*'):
  if not p.is_file() or p.suffix=='.meta':continue
  ids=set();mp=Path(str(p)+'.meta')
  if p.suffix.lower() in extensions:ids|=refs(read(p));scanned+=1
  if mp.exists():ids|=refs(read(mp))
  ids={g for g in ids if set(g)!={'0'}};source=rel(p);targets=[]
  for g in ids:
   if g in now:targets.append(now[g])
   elif g in before:broken.append({'source':source,'missingGUID':g,'previousTarget':before[g]})
   else:external.add(g)
  references[source]=sorted(set(targets)-{source})
 combat='Assets/Art/Characters/Protagonist01/Combat/';selected=[combat+'Clips/Longsword_Attack_'+n+'.anim' for n in ['Chop','Slice','Stab']]+[combat+'Prefabs/Protagonist_Combat.prefab',combat+'Prefabs/Longsword_KayKitSocket.prefab'];closure=set(selected);pending=list(selected)
 while pending:
  for p in references.get(pending.pop(),[]):
   if p not in closure:closure.add(p);pending.append(p)
 absent=[p for p in selected if not (W/p).is_file()]
 old_clips=list((W/'Assets/Art/Characters/Protagonist01/ChibiMotion_v007/Clips').glob('*.anim'));new_clips=list((W/combat/'Clips').glob('*.anim'))
 result={'method':'Read-only scan of all retained Unity serialized assets plus importer metadata. Missing GUIDs that existed in the pre-cleanup project are errors. Package/builtin or already-unresolved GUIDs are reported separately, not falsely attributed to cleanup. C# string-path loading is outside this GUID check.','baselineSHA256':hashlib.sha256(Path(args.baseline).read_bytes()).hexdigest(),'serializedTextAssetsScanned':scanned,'currentAssetGUIDs':len(now),'duplicateGUIDs':duplicates,'newlyBrokenReferences':broken,'externalOrPreviouslyUnresolvedGUIDCount':len(external),'selectedRootFilesMissing':absent,'oldNonAttackClipCount':len(old_clips),'combatClipCount':len(new_clips),'combatDependencyClosure':sorted(closure),'combatDependencyGUIDs':{p:g for g,p in now.items() if p in closure},'passed':not duplicates and not broken and not absent and len(old_clips)==15 and len(new_clips)==3}
 Path(args.out).write_text(json.dumps(result,indent=2));print('RETAINED_GUID_CHECK',json.dumps({k:result[k] for k in ['serializedTextAssetsScanned','currentAssetGUIDs','newlyBrokenReferences','duplicateGUIDs','oldNonAttackClipCount','combatClipCount','passed']}))
 if not result['passed']:raise SystemExit(1)
if __name__=='__main__':run()
