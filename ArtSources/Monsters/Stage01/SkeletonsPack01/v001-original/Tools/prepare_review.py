"""Stage a disposable Unity project without modifying existing project art.
Run from any directory with the bundled Python runtime. Original sources are copied,
not converted. GUID-preserving dependency closure copies the two comparison actors.
"""
from pathlib import Path
import re, shutil, json, hashlib

ROOT = Path(__file__).resolve().parents[6]
SOURCE = Path(__file__).resolve().parents[1]
QA = ROOT / '.local-validation/SkeletonsPack01'
MEDIA = ROOT.parent.parent / 'OZGL3_ArtReview/Monsters/Stage01/SkeletonsPack01/v001-original'
DEST = QA / 'Assets/Art/Monsters/Stage01/SkeletonsPack01/v001'

def copy(src, dst):
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src, dst)

def main():
    print('ROOT', ROOT)
    assert (ROOT/'Assets').is_dir()
    for sub in ['FBX','Accessories','Textures','Shaders']:
        (DEST/sub).mkdir(parents=True, exist_ok=True)
    for p in (SOURCE/'SourceAssets/Characters/fbx').glob('*.fbx'):
        copy(p,DEST/'FBX'/p.name)
    for p in (SOURCE/'SourceAssets/Accessories/fbx').glob('*.fbx'):
        copy(p,DEST/'Accessories'/p.name)
    copy(SOURCE/'SourceAssets/Textures/skeleton_texture.png',DEST/'Textures/skeleton_texture.png')
    copy(SOURCE/'SourceAssets/LICENSE.txt',DEST/'LICENSE_KayKit.txt')
    for p in (SOURCE/'Tools').glob('*.cs'):
        copy(p,QA/'Assets/Editor'/p.name)
    copy(SOURCE/'Tools/SkeletonNativeAtlas.shader',DEST/'Shaders/SkeletonNativeAtlas.shader')
    # Current hero + previous Minion must stay byte-identical, including import metadata.
    seeds = [
      'Assets/Art/Characters/Protagonist01/Combat/Prefabs/Protagonist_Combat.prefab',
      'Assets/Art/Characters/Protagonist01/Combat/Clips/Longsword_Attack_Chop.anim',
      'Assets/Art/Monsters/Stage01/Melee01/v001/Prefabs/Stage01_Melee01.prefab',
      *['Assets/Art/Monsters/Stage01/Melee01/v001/Clips/'+n+'.anim' for n in ['Idle','Move','Attack','Hit']]]
    guidmap={}
    for meta in (ROOT/'Assets').rglob('*.meta'):
        m=re.search(r'^guid: ([0-9a-f]{32})',meta.read_text(encoding='utf-8',errors='ignore'),re.M)
        if m: guidmap[m[1]]=meta.with_suffix('')
    queue=[ROOT/p for p in seeds]; seen=set(); records=[]
    while queue:
        p=queue.pop()
        if p in seen or not p.is_file(): continue
        seen.add(p); rel=p.relative_to(ROOT); copy(p,QA/rel)
        meta=Path(str(p)+'.meta')
        if meta.exists(): copy(meta,Path(str(QA/rel)+'.meta'))
        data=p.read_bytes()
        records.append({'path':rel.as_posix(),'sha256':hashlib.sha256(data).hexdigest()})
        if data.startswith(b'%YAML') or p.suffix in ['.shader','.mat','.prefab','.asset','.anim']:
            for guid in re.findall(rb'guid: ([0-9a-f]{32})',data):
                q=guidmap.get(guid.decode())
                if q and q.is_file(): queue.append(q)
    (QA/'Packages').mkdir(exist_ok=True)
    (QA/'Packages/manifest.json').write_text('{"dependencies":{"com.unity.modules.animation":"1.0.0","com.unity.modules.imageconversion":"1.0.0","com.unity.modules.physics":"1.0.0"}}',encoding='utf-8')
    (QA/'ProjectSettings').mkdir(exist_ok=True)
    copy(ROOT/'ProjectSettings/ProjectVersion.txt',QA/'ProjectSettings/ProjectVersion.txt')
    MEDIA.mkdir(parents=True,exist_ok=True)
    (SOURCE/'QA/protected-reference-hashes.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
    (QA/'review-paths.json').write_text(json.dumps({'source':str(SOURCE),'output':str(MEDIA)},indent=2),encoding='utf-8')
    print(json.dumps({'qa_project':str(QA),'output':str(MEDIA),'reference_files':len(records)},indent=2))

if __name__=='__main__': main()
