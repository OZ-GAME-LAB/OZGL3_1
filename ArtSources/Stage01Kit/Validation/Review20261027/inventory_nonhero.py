import pathlib,json,hashlib,re,collections
P=pathlib.Path(__file__).resolve().parents[4]
O=pathlib.Path(__file__).resolve().parent
def rel(p):return p.relative_to(P).as_posix()
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(p):return {'path':rel(p),'bytes':p.stat().st_size,'sha256':sha(p)}
stage=P/'Assets/Art/Stage01Kit';monster=P/'Assets/Art/Monsters/Stage01/Melee01/v001';ms=P/'ArtSources/Monsters/Stage01/Melee01/v001-native-motion'
guid={}
for p in (P/'Assets/Art').rglob('*.meta'):
    t=p.read_text(errors='replace');m=re.search(r'^guid:\s*([a-f0-9]{32})',t,re.M)
    if m:guid[m.group(1)]=p.with_suffix('')
def prefab(p):
    r=record(p);text=p.read_text();refs=set(re.findall(r'guid:\s*([a-f0-9]{32})',text));r['resolvedArtReferences']=[rel(guid[x]) for x in sorted(refs) if x in guid];r['otherReferences']=[x for x in sorted(refs) if x not in guid and not x.startswith('0000000000000000')];return r
g=json.loads((P/'ArtSources/Stage01Kit/meshy-generation.json').read_text(encoding='utf-8-sig'))
tasks=[{'name':x['key'],'provider':'Meshy','model':'T2.5 Smart Topology','taskId':x['created']['task_id'],'recordedCredits':x['result']['consumed_credits'],'sourceFBX':x['result']['source_fbx'],'activePrefab':x['result']['prefab']} for x in g['tasks']]
sp=[prefab(p) for p in sorted((stage/'Prefabs').rglob('*.prefab'))]
proof=json.loads((ms/'QA/final-delivery-check.json').read_text(encoding='utf-8-sig'))
comparisons=[('source_blend_sha256',ms/'Stage01_Melee01.blend'),('fbx_sha256',monster/'FBX/Stage01_Melee01.fbx'),('prefab_sha256',monster/'Prefabs/Stage01_Melee01.prefab')]
monster_proof=[{'file':rel(p),'expectedSHA256':proof[k],'actualSHA256':sha(p),'matchesPreviousApprovedReview':sha(p)==proof[k]} for k,p in comparisons]
out={'date':'2026-10-27','scope':'Retained Stage01 environments, noncombat props, weapons, potions, assembled blocks, and delivered Melee01 monster. Hero, VFX and unrelated third-party/UI excluded. Read-only inventory; no runtime integration assessment.',
 'stage01':{'prefabCount':len(sp),'prefabCategories':dict(collections.Counter(p.parent.name for p in (stage/'Prefabs').rglob('*.prefab'))),'prefabs':sp,'sourceFBX':[record(p) for p in sorted((stage/'Models').glob('*.fbx'))],'generatedMeshes':[record(p) for p in sorted((stage/'Meshes').rglob('*.asset'))],'meshGenerationReceipts':tasks,'recordedMeshyCredits':sum(t['recordedCredits'] for t in tasks),'localAuthoringTools':[record(p) for p in sorted((stage/'Editor').glob('*Builder.cs'))],'provenanceNotes':['Seven Meshy environment props and two original Meshy weapons use generated geometry, local vertex-color normalization and authored prefabs.','Sixteen modular environment prefabs and four potion variants were authored locally in Unity editor tooling.','The Staff prefab uses the locally revised smaller floating semitransparent faceted orb; original Meshy crystal geometry is not the active head.','Stage01 Sword is the retained original short sword asset; hero Final/Combat contains its separate approved longsword.'], 'providerLicenseNote':'Generation task receipts establish provider/model/task identity and recorded credit use. These are not KayKit CC0 assets; no claim of a third-party asset-pack license is made.'},
 'monster':{'prefabCount':1,'prefab':prefab(monster/'Prefabs/Stage01_Melee01.prefab'),'deliveredClips':[record(p) for p in sorted((monster/'Clips').glob('*.anim'))],'sourceProvenance':record(ms/'Research/source-provenance.json'),'sourceURL':'https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Skeletons-1.0','pinnedCommit':'15b62b9bad122f72926c10fb14d622c73819fa54','license':'CC0-1.0','licenseFile':record(monster/'Licenses/KayKit_CC0.txt'),'approvedReviewIdentity':monster_proof,'technique':'Same-model native embedded motion; selected Idle_Combat, Running_A, 1H_Melee_Attack_Chop and Hit_A. Uniform scale, palette/cloak adjustments, static weapon-axis alignment, constant clip floor offset; no AI pose synthesis or retargeting.'}}
(O/'nonhero-source-inventory.json').write_text(json.dumps(out,indent=2),encoding='utf-8')
print(json.dumps({'stagePrefabs':len(sp),'categories':out['stage01']['prefabCategories'],'generatedMeshCount':len(out['stage01']['generatedMeshes']),'sourceFBX':len(out['stage01']['sourceFBX']),'recordedMeshyCredits':out['stage01']['recordedMeshyCredits'],'monsterApprovedReviewIdentity':monster_proof},indent=2))
