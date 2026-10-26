from pathlib import Path
import json,numpy as np,itertools,hashlib
from scipy.spatial import cKDTree
W=Path(__file__).resolve();W=next(p for p in W.parents if (p/'Assets').is_dir() and (p/'ProjectSettings').is_dir());Q=W/'Logs/AssetAudit20261027/SkinSolver'
old=json.loads((Q/'original-body-weights.json').read_text());new=json.loads((Q/'body-weights.json').read_text());schema=json.loads((W/'Logs/AssetAudit20261027/Hero/Meshes/Combat_Combat_Longsword_Attack_Chop.bin.gz.schema.json').read_text());mesh=next(m for m in schema['meshes'] if m['name']=='Chibi_Body_v009');native=np.array([v['position'] for v in old['vertices']]);candidate=np.array([v['position'] for v in new['vertices']]);unity=np.array([[v[k] for k in 'xyz'] for v in mesh['restVertices']]);bones=mesh['bones']
if not np.array_equal(native,candidate):raise ValueError('Candidate changes rest vertices')
ow=np.array([[v['weights'].get(b,0) for b in bones] for v in old['vertices']]);nw=np.array([[v['weights'].get(b,0) for b in bones] for v in new['vertices']]);delta=np.max(np.abs(ow-nw),axis=1);changed=delta>1e-8
trials=[]
for signs in itertools.product([-1,1],repeat=3):
 p=native*np.array(signs);tree=cKDTree(p);d,ix=tree.query(unity);trials.append((float(d.max()),signs,ix,tree))
error,signs,mapping,tree=min(trials,key=lambda x:x[0])
if error>2e-6:raise ValueError('Rest-coordinate correspondence inaccurate: '+str(error))
rows=[];ambiguous=[];maximum_duplicate_difference=0
for i,j in enumerate(mapping):
 matches=tree.query_ball_point(unity[i],2e-6)
 conflict=float(np.max(np.abs(nw[matches]-nw[j]))) if matches else 0;maximum_duplicate_difference=max(maximum_duplicate_difference,conflict)
 if conflict>1e-5:ambiguous.append({'unityVertex':i,'sourceCandidates':matches,'maxCandidateWeightDifference':conflict})
 if changed[j] or any(changed[k] for k in matches):
  weights=nw[j];weights=weights/weights.sum();entries=[{'boneIndex':k,'weight':float(weight)} for k,weight in enumerate(weights) if weight>0];entries.sort(key=lambda x:x['weight'],reverse=True);rows.append({'vertex':i,'nativeVertex':int(j),'weights':entries})
if ambiguous:raise ValueError('Ambiguous coincident source vertex weights: '+repr(ambiguous[:8]))
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest();prefab=W/'Assets/Art/Characters/Protagonist01/Combat/Prefabs/Protagonist_Combat.prefab'
patch={'prefab':'Assets/Art/Characters/Protagonist01/Combat/Prefabs/Protagonist_Combat.prefab','expectedPrefabSHA256':sha(prefab),'meshName':'Chibi_Body_v009','vertexCount':len(unity),'bones':bones,'patches':rows,'sourceCandidateSHA256':sha(Q/'body-weights.json'),'sourceOriginalSHA256':sha(Q/'original-body-weights.json')}
(Q/'unity-weight-patch.json').write_text(json.dumps(patch,indent=2));report={'sourceVertices':len(native),'unityVertices':len(unity),'nativeChangedAt1e8':int(changed.sum()),'nativeChangedAt1e6':int((delta>1e-6).sum()),'patchedUnityVertices':len(rows),'mappingAxisSigns':signs,'maximumRestMappingDistanceM':error,'maximumCoincidentCandidateWeightDifference':maximum_duplicate_difference,'ambiguousVertices':ambiguous,'maximumSourceWeightDelta':float(delta.max()),'patchSHA256':sha(Q/'unity-weight-patch.json'),'restGeometryExact':True,'method':'Position correspondence in native/exported rest coordinates, axis-sign convention measured across every vertex. All coincident source candidates checked for equivalent weights. Only source vertices with actually changed candidate weights are patched; all other Unity-imported weights remain untouched.'}
(W/'ArtSources/Characters/Protagonist01/Final/Validation/Review20261027/Combat/unity-weight-mapping.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
