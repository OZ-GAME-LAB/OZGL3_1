"""Read-only release checks, separate from historical pre-retouch cleanup hashes."""
from pathlib import Path
from html.parser import HTMLParser
from urllib.parse import urlsplit,unquote
import hashlib,json

out=Path(__file__).parent;final=out.parents[1];workspace=next(p for p in out.parents if (p/'Assets').is_dir() and (p/'ProjectSettings').is_dir())
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
receipt=json.loads((final/'Validation/combat-delivery-receipt.json').read_text(encoding='utf-8-sig'))
changed=[]
for r in receipt['selectedClips']:
    p=workspace/r['path']
    if not p.exists() or sha(p)!=r['sha256']:changed.append(str(p))
    m=p.with_suffix(p.suffix+'.meta')
    if not m.exists() or sha(m)!=r['metaSHA256']:changed.append(str(m))
legacy=json.loads((final/'LegacyMotions/manifest.json').read_text(encoding='utf-8-sig'))
gifchanges=[r['name']for r in legacy['clips']if sha(final/'LegacyMotions'/r['gif'])!=r['gifSHA256']]
media=json.loads((final/'Review/Combat/media-manifest.json').read_text())
mediaerrors=[r['file']for r in media['files']if sha(final/r['file'])!=r['sha256']]
class Links(HTMLParser):
    def __init__(self):super().__init__();self.values=[]
    def handle_starttag(self,tag,attrs):
        self.values.extend(v for k,v in attrs if k in ('href','src')and v)
catalog=final/'Review/index.html';parser=Links();parser.feed(catalog.read_text(encoding='utf8'));local=[v for v in parser.values if not urlsplit(v).scheme and not v.startswith('#')]
badlinks=[v for v in local if not (catalog.parent/unquote(urlsplit(v).path)).exists()]
source=json.loads((out/'Combat/source-retouch-receipt.json').read_text())
sourceok=sha(final/'Combat/Protagonist_Combat.blend')==source['afterBlendSHA256']
result={'selectedUnityClipsChecked':len(receipt['selectedClips']),'changedSelectedClipsOrMetas':changed,'legacyGIFsChecked':len(legacy['clips']),'changedLegacyGIFs':gifchanges,'combatGIFsChecked':len(media['files']),'combatGIFHashMismatches':mediaerrors,'localCatalogLinksChecked':len(local),'missingCatalogLinks':badlinks,'currentSourceHashMatchesRetouchReceipt':sourceok,'passed':not(changed or gifchanges or mediaerrors or badlinks)and sourceok}
(out/'delivery-verification.json').write_text(json.dumps(result,indent=2),encoding='utf8')
print(json.dumps(result));assert result['passed']
