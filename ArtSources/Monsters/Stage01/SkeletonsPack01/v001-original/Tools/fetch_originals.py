"""Acquire byte-identical official source files at a fixed Git commit.

No source models, animation, skin weights, scale, palette or floor offset are edited.
"""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime,timezone
import urllib.request,json,hashlib

ROOT=Path(__file__).resolve().parents[1]
REPO='KayKit-Game-Assets/KayKit-Character-Pack-Skeletons-1.0'
COMMIT='15b62b9bad122f72926c10fb14d622c73819fa54'
PREFIX='addons/kaykit_character_pack_skeletons/'
API=f'https://api.github.com/repos/{REPO}/git/trees/{COMMIT}?recursive=1'
HEADERS={'User-Agent':'Codex-NativeAssetAudit','Accept':'application/vnd.github+json'}

def request(url):
    with urllib.request.urlopen(urllib.request.Request(url,headers=HEADERS),timeout=120) as response:
        return response.read()

tree_path=ROOT/'QA/official-commit-tree.json'
tree_bytes=tree_path.read_bytes() if tree_path.exists() else request(API)
tree=json.loads(tree_bytes)
if tree.get('truncated'):raise RuntimeError('Incomplete upstream tree is not acceptable.')
entries={row['path']:row for row in tree['tree'] if row['type']=='blob'}
targets=[]
for name in ['Skeleton_Rogue','Skeleton_Warrior','Skeleton_Mage']:
    for kind,extension in [('fbx','fbx'),('gltf','glb')]:
        path=f'Characters/{kind}/{name}.{extension}'
        targets.append((PREFIX+path,'SourceAssets/'+path))
for name in ['Skeleton_Crossbow','Skeleton_Axe','Skeleton_Staff','Skeleton_Quiver','Skeleton_Arrow']:
    targets.append((PREFIX+f'Assets/fbx/{name}.fbx',f'SourceAssets/Accessories/fbx/{name}.fbx'))
    for extension in ['gltf','bin']:
        targets.append((PREFIX+f'Assets/gltf/{name}.{extension}',f'SourceAssets/Accessories/gltf/{name}.{extension}'))
targets.extend([(PREFIX+'Textures/skeleton_texture.png','SourceAssets/Textures/skeleton_texture.png'),(PREFIX+'Textures/skeleton_texture.png','SourceAssets/Accessories/gltf/skeleton_texture.png'),('LICENSE.txt','SourceAssets/LICENSE.txt'),('README.md','SourceAssets/README.md')])

def blob_sha(data):return hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()
def acquire(pair):
    upstream,local=pair
    row=entries[upstream]
    url=f'https://raw.githubusercontent.com/{REPO}/{COMMIT}/{upstream}'
    target=ROOT/local
    if target.exists():
        data=target.read_bytes()
        if blob_sha(data)!=row['sha']:raise FileExistsError(f'Refusing to overwrite conflicting source: {target}')
    else:
        data=request(url)
        if len(data)!=row['size'] or blob_sha(data)!=row['sha']:raise RuntimeError(f'Git blob verification failed: {upstream}')
        target.parent.mkdir(parents=True,exist_ok=True)
        target.write_bytes(data)
    result={'local_path':local,'upstream_path':upstream,'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest(),'git_blob_sha1':blob_sha(data),'expected_git_blob_sha1':row['sha'],'matches_commit_tree':True,'pinned_download_url':url}
    print('VERIFIED',local,len(data),flush=True)
    return result

with ThreadPoolExecutor(max_workers=4) as pool:files=list(pool.map(acquire,targets))
(ROOT/'QA/official-commit-tree.json').write_bytes(tree_bytes)
provenance={'repository':f'https://github.com/{REPO}','pinned_commit':COMMIT,'api_tree_url':API,'retrieved_at_utc':datetime.now(timezone.utc).isoformat(),'license':'CC0-1.0','source_files_modified':False,'scope':'Three original characters in FBX and GLB, five original FBX accessories, original atlas and license/readme. No edited source geometry, rig, animation, scale, colors or ground offsets.','files':files}
(ROOT/'QA/source-provenance.json').write_text(json.dumps(provenance,indent=2,ensure_ascii=False),encoding='utf-8')
print('SOURCE_ASSETS_CONFIRMED',len(files),ROOT/'SourceAssets',flush=True)
