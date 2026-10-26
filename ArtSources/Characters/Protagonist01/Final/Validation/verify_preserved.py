from pathlib import Path
import json,hashlib
H=Path(__file__).resolve().parent;p=json.loads((H/'cleanup-plan.json').read_text());W=Path(p['workspace'])
differences=[]
for name,digest in p['retainedHashes'].items():
 f=W/name
 if not f.is_file() or hashlib.sha256(f.read_bytes()).hexdigest()!=digest:differences.append(name)
remaining=[r['path'] for r in p['deleteTargets'] if Path(r['path']).exists()]
r={'retainedFilesChecked':len(p['retainedHashes']),'changedOrMissingRetainedFiles':differences,'remainingDeletionTargets':remaining,'passed':not differences and not remaining}
(H/'preservation-result.json').write_text(json.dumps(r,indent=2));print(json.dumps(r));assert r['passed']
