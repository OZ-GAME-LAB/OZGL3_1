"""Read original FBX metadata without importing or changing file data."""
from pathlib import Path
from io_scene_fbx import parse_fbx
import json
ROOT=Path(__file__).resolve().parents[1]
rows=[]
for path in sorted((ROOT/'SourceAssets').glob('**/*.fbx')):
    root,version=parse_fbx.parse(str(path))
    settings=next(e for e in root.elems if e.id==b'GlobalSettings')
    props=next(e for e in settings.elems if e.id==b'Properties70')
    selected={}
    for p in props.elems:
        name=p.props[0].decode() if isinstance(p.props[0],bytes) else str(p.props[0])
        if name in {'UpAxis','UpAxisSign','FrontAxis','FrontAxisSign','CoordAxis','CoordAxisSign','UnitScaleFactor','OriginalUnitScaleFactor','TimeMode','CustomFrameRate'}:selected[name]=p.props[-1]
    objects=next(e for e in root.elems if e.id==b'Objects')
    models=[e.props[1].decode(errors='replace') for e in objects.elems if e.id==b'Model']
    rows.append({'file':str(path.relative_to(ROOT)),'fbx_version':version,'global_settings':selected,'model_names':models,'icosphere_model_names':[name for name in models if 'icosphere' in name.lower()]})
(ROOT/'QA/original-fbx-global-settings.json').write_text(json.dumps(rows,indent=2),encoding='utf-8')
print(json.dumps(rows,indent=2),flush=True)
