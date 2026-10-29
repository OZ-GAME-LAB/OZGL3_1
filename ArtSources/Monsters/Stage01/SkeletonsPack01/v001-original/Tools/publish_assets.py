"""Copy only this task's validated new Unity family, preserving its GUIDs.
Existing hero, Minion, environment and gameplay files are never destinations.
"""
from pathlib import Path
import hashlib, json, shutil

SOURCE = Path(__file__).resolve().parents[1]
ROOT = Path(__file__).resolve().parents[6]
QA = ROOT / '.local-validation/SkeletonsPack01'
REL = Path('Assets/Art/Monsters/Stage01/SkeletonsPack01/v001')

def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def copy(src,dst):
    dst.parent.mkdir(parents=True,exist_ok=True)
    shutil.copy2(src,dst)

def main():
    import_report=json.loads((SOURCE/'QA/unity-import-report.json').read_text(encoding='utf-8'))
    render_report=json.loads((SOURCE/'QA/unity-review-report.json').read_text(encoding='utf-8'))
    assert import_report['success'] and render_report['success']
    for row in json.loads((SOURCE/'QA/protected-reference-hashes.json').read_text(encoding='utf-8')):
        assert digest(ROOT/row['path'])==row['sha256'], 'Protected file changed: '+row['path']
    for role in import_report['roles']:
        assert role['embeddedClipCount']==95 and role['sourceBytesUnchanged']
        assert all(c['curvesIdentical'] for c in role['clips'])
    destination=(ROOT/REL).resolve()
    assert destination.is_relative_to((ROOT/'Assets/Art/Monsters/Stage01/SkeletonsPack01').resolve())
    records=[]
    for p in (QA/REL).rglob('*'):
        if p.is_file():
            target=destination/p.relative_to(QA/REL);copy(p,target)
            records.append({'path':target.relative_to(ROOT).as_posix(),'sha256':digest(target)})
    for rel in [REL.with_suffix('.meta'), REL.parent.with_suffix('.meta')]:
        if (QA/rel).exists(): copy(QA/rel,ROOT/rel)
    # Review scenes/materials keep GUIDs and only move into this new family.
    for p in (QA/'Assets/Review').iterdir():
        if p.is_file() and (p.suffix in ['.mat','.unity'] or p.name.endswith(('.mat.meta','.unity.meta'))):
            copy(p,destination/'Review'/p.name)
    validation=SOURCE/'Tools/SkeletonPackValidate.cs'
    assert validation.exists()
    copy(validation,destination/'Editor/SkeletonPackValidate.cs')
    copy(SOURCE/'DELIVERY_NOTES.txt',destination/'README.txt')
    result={'success':True,'source_validation_success':True,'visual_approval':False,'protected_reference_count':22,'new_family_path':REL.as_posix(),'files':records,'scope':'Only new SkeletonsPack01 assets and independent review scenes. Main-project import validation must run separately. Residual original-motion/equipment conflicts are documented.'}
    (SOURCE/'QA/published-assets.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps({'new_family':str(destination),'files':len(records),'protected_files_unchanged':True},indent=2))

if __name__=='__main__':main()
