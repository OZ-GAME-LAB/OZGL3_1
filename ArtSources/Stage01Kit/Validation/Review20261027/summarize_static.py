import json, hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parent
report=json.loads((ROOT/'unity-static-audit.json').read_text(encoding='utf-8-sig'))
inv=json.loads((ROOT/'nonhero-source-inventory.json').read_text(encoding='utf-8-sig'))
unique={}
for p in report['prefabs']:
 for m in p['meshes']:
  unique[(m['path'],m['name'])]=m
summary={'prefabCount':report['prefabCount'],'uniqueMeshesInspected':len(unique),'totalUniqueVertices':sum(m['vertices'] for m in unique.values()),'totalUniqueTriangles':sum(m['triangles'] for m in unique.values()),'nonfiniteVertices':sum(m['nonfiniteVertices'] for m in unique.values()),'invalidIndices':sum(m['invalidIndices'] for m in unique.values()),'degenerateTriangles':sum(m['degenerateTriangles'] for m in unique.values()),'unreadableMeshCount':sum(not m['readable'] for m in unique.values()),'missingComponents':sum(p['missingComponents'] for p in report['prefabs']),'materialIssues':[{'prefab':p['name'],'issues':p['materialIssues']} for p in report['prefabs'] if p['materialIssues']],'issues':report['issues']}
print(json.dumps(summary,indent=2))
print('Degenerates',[(m['name'],m['degenerateTriangles']) for m in unique.values() if m['degenerateTriangles']])
(ROOT/'numerical-summary.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
