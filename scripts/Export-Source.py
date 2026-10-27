"""Export reviewable source without build caches, games, accounts, or credentials."""
from pathlib import Path
import hashlib
import json
import subprocess
import zipfile

root = Path(__file__).resolve().parent.parent
reference = root / 'work/upstream/pcl-ce'
source = root / 'src/PCL-ThirdParty'
excluded = {'bin', 'obj', '.git', '.vs', 'publish', 'ThirdPartyData', '.minecraft', 'PCL'}
files = [file for file in source.rglob('*') if file.is_file() and not excluded.intersection(file.relative_to(source).parts[:-1])]
tracked = subprocess.check_output(['git', '-C', str(reference), 'ls-files', '-z']).decode('utf-8').split('\0')
changes = []
def digest(file):
    return hashlib.sha256(file.read_bytes()).hexdigest()
for name in filter(None, tracked):
    old, new = reference / name, source / name
    if not new.exists():
        changes.append({'path': name, 'status': 'removed'})
    elif digest(old) != digest(new):
        changes.append({'path': name, 'status': 'modified', 'originalSha256': digest(old), 'sha256': digest(new)})
for file in files:
    name = file.relative_to(source).as_posix()
    if name not in tracked:
        changes.append({'path': name, 'status': 'added', 'sha256': digest(file)})
(root / 'reports/source-changes.json').write_text(json.dumps({'referenceCommit': '1a7653127d578efdb2a9388999a3fdc67d9e49d5', 'changes': changes}, ensure_ascii=False, indent=2), encoding='utf-8')
archive = root / 'artifacts/PCL老蒋定制版-P1-源码.zip'
with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as output:
    for file in files:
        output.write(file, file.relative_to(root).as_posix())
    for directory in ('scripts', 'docs', 'src/Packaging'):
        for file in (root / directory).rglob('*'):
            if file.is_file() and '__pycache__' not in file.parts:
                output.write(file, file.relative_to(root).as_posix())
    for name in ('README.md', 'AGENTS.md', 'upstream-lock.json', 'global.json', '.gitignore', 'Start-Launcher.cmd',
                 'config/credentials.example.json', 'config/download-sources.example.json'):
        output.write(root / name, name)
print(json.dumps({'sourceArchive': str(archive), 'sourceFiles': len(files), 'changedFiles': len(changes), 'sha256': digest(archive)}, ensure_ascii=False))
