"""Verify current selected clips and external previews; preserve historical evidence."""
from pathlib import Path
from html.parser import HTMLParser
from urllib.parse import urlsplit,unquote
import hashlib,json,sys,os

out=Path(__file__).resolve().parent;final=out.parents[1]
workspace=next(p for p in out.parents if (p/'Assets').is_dir() and (p/'ProjectSettings').is_dir())
sys.path.insert(0,str(workspace/'ArtSources'))
from review_storage import media_path,storage_metadata
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
receipt=json.loads((final/'Validation/combat-delivery-receipt.json').read_text(encoding='utf-8-sig'))
legacy=json.loads((final/'LegacyMotions/manifest.json').read_text(encoding='utf-8-sig'))
current={r['unityClip']:r for r in legacy['clips']}
changed=[];normalizedOnly=[]
for r in receipt['selectedClips']:
    p=workspace/r['path'];expected=current.get(r['path'])
    if not p.exists():changed.append(str(p))
    elif expected:
        normalized=hashlib.sha256(p.read_bytes().replace(b'\r\n',b'\n')).hexdigest()
        if sha(p)!=expected['unitySHA256'] and normalized!=expected.get('unityLFNormalizedSHA256'):changed.append(str(p))
    elif sha(p)!=r['sha256']:
        normalized=hashlib.sha256(p.read_bytes().replace(b'\r\n',b'\n')).hexdigest()
        if normalized!=r['sha256']:changed.append(str(p))
        else:normalizedOnly.append(r['path'])
    m=p.with_suffix(p.suffix+'.meta')
    if not m.exists() or sha(m)!=r['metaSHA256']:changed.append(str(m))

gifchanges=[]
for r in legacy['clips']:
    p=media_path(final/'LegacyMotions'/r['gif'])
    if not p.exists() or sha(p)!=r['gifSHA256']:gifchanges.append(r['name'])
media=json.loads((final/'Review/Combat/media-manifest.json').read_text())
mediaerrors=[]
for r in media['files']:
    p=media_path(final/r['file'])
    if not p.exists() or sha(p)!=r['sha256']:mediaerrors.append(r['file'])

class Links(HTMLParser):
    def __init__(self):super().__init__();self.values=[]
    def handle_starttag(self,tag,attrs):
        self.values.extend(v for k,v in attrs if k in ('href','src')and v)

badlinks=[];linkcount=0
catalogs=[final/'Review/index.html',media_path(final/'Review/index.html'),
          final/'Review/Airborne/index.html',media_path(final/'Review/Airborne/index.html')]
for catalog in catalogs:
    if not catalog.exists():badlinks.append(str(catalog));continue
    parser=Links();parser.feed(catalog.read_text(encoding='utf8'))
    for value in parser.values:
        parsed=urlsplit(value)
        if value.startswith('#')or parsed.scheme not in ('','file'):continue
        path=unquote(parsed.path)
        if parsed.scheme=='file':
            if os.name=='nt' and len(path)>2 and path[0]=='/' and path[2]==':':path=path[1:]
            target=Path(path)
        else:target=catalog.parent/path
        linkcount+=1
        if not target.exists():badlinks.append(f'{catalog}: {value}')

source=json.loads((out/'Combat/source-retouch-receipt.json').read_text())
sourceok=sha(final/'Combat/Protagonist_Combat.blend')==source['afterBlendSHA256']
result={'previewStorage':storage_metadata(),'selectedUnityClipsChecked':len(receipt['selectedClips']),
        'currentClipManifest':'LegacyMotions/manifest.json','changedSelectedClipsOrMetas':changed,'lineEndingOnlyDifferences':normalizedOnly,
        'legacyGIFsChecked':len(legacy['clips']),'changedLegacyGIFs':gifchanges,
        'combatGIFsChecked':len(media['files']),'combatGIFHashMismatches':mediaerrors,
        'catalogsChecked':len(catalogs),'localCatalogLinksChecked':linkcount,'missingCatalogLinks':badlinks,
        'currentSourceHashMatchesRetouchReceipt':sourceok,'passed':not(changed or gifchanges or mediaerrors or badlinks)and sourceok}
(final/'Validation/delivery-verification-current.json').write_text(json.dumps(result,indent=2),encoding='utf8')
print(json.dumps(result));assert result['passed']
