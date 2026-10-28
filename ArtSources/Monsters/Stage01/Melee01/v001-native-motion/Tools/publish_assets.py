"""Copy only this new, saved-and-reviewed art family into its new project folder."""
import json, shutil, hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];PROJECT=ROOT.parents[4]
QA=ROOT/'QA/UnityProject'
source=QA/'Assets/Art/Monsters'
dest=PROJECT/'Assets/Art/Monsters'
report=json.loads((ROOT/'QA/UnityRendered/report.json').read_text())
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
fbx=ROOT/'Exports/Stage01_Melee01.fbx'
assert report['success'] and report['fbxSHA256']==sha(fbx)
expected={'Idle':4.266666667,'Move':.8,'Attack':1.066666667,'Hit':.666666667}
assert all(abs(c['seconds']-expected[c['name']])<1e-5 for c in report['clips'])
if dest.exists():raise RuntimeError(f'Refusing to overwrite pre-existing art family {dest}')
shutil.copytree(source,dest)
shutil.copy2(source.with_suffix('.meta'),dest.with_suffix('.meta'))
family=dest/'Stage01/Melee01/v001'
shutil.copytree(QA/'Assets/Review',family/'Review')
shutil.copy2(QA/'Assets/Review.meta',family/'Review.meta')
(family/'Licenses').mkdir()
shutil.copy2(ROOT/'SourceAssets/LICENSE_KayKit.txt',family/'Licenses/KayKit_CC0.txt')
(family/'ASSET_README.txt').write_text('''Project M Stage01 Melee01 art sample

One proposed melee skeleton. Original model, skinning, native rig and animation:
KayKit Skeletons 1.0 by Kay Lousberg, CC0. See Licenses/KayKit_CC0.txt.
Source provenance and asset comparison are in ArtSources/Monsters/Stage01/Melee01/v001-native-motion/Research.

Prefab: Prefabs/Stage01_Melee01.prefab
Native Generic Avatar. Animator is enabled; no runtime controller is assigned.
Root motion is disabled. Model's forward axis is +Z; use an OUTER orientation
parent rotated Y90 degrees to face +X in the game's side-view plane.
Do not rename the rig hierarchy without rebinding clips.

Clips: Idle4.266667s(loop), Move0.8s(loop/in-place), Attack1.066667s, Hit0.666667s.
The original joint poses/timing are retained. Move and Attack have constant
vertical floor offsets of0.032263592m and0.005913736m respectively. The native
blade's local shaft axis is converted to the handslot with a fixed X-90 rotation.
The imported editable .blend contains baked keys, not the creator's paid IK control rig.

Review/Stage01_Melee01_Review.unity is an isolated static inspection scene with
camera and floor. Actual animated four-view GIFs and preview connections are at
ArtSources/Monsters/Stage01/Melee01/v001-native-motion/QA/UnityRendered/index.html.
The review scene deliberately lowers its opaque floor5cm; amber markers showY0.
Live enemy AI, damage events, colliders, death behavior, gameplay travel speed,
Animator transitions and VFX are not wired by this art delivery.
''',encoding='utf-8')
receipt={'destination':str(family),'source_fbx_sha256':sha(fbx),'published_fbx_sha256':sha(family/'FBX/Stage01_Melee01.fbx'),'published_prefab_sha256':sha(family/'Prefabs/Stage01_Melee01.prefab'),'file_count':len([p for p in dest.rglob('*') if p.is_file()]),'existing_gameplay_or_art_files_overwritten':False,'source_unity_version':report['unity'],'validation':'Byte-identical saved/reloaded Unity prefab, clips, model, materials, shader and textures copied from isolated Unity QA project.'}
assert receipt['published_fbx_sha256']==report['fbxSHA256']
assert receipt['published_prefab_sha256']==report['prefabSHA256']
(ROOT/'QA/publication-receipt.json').write_text(json.dumps(receipt,indent=2))
print(json.dumps(receipt,indent=2))
