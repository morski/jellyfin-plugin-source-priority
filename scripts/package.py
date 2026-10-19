"""Build a Jellyfin install archive and catalog entry from the compiled assembly."""
import argparse
import hashlib
import json
import re
import xml.etree.ElementTree as ET
import zipfile
from datetime import datetime, timezone
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--version', required=True)
args = parser.parse_args()
if not re.fullmatch(r'\d+\.\d+\.\d+', args.version):
    raise SystemExit('Use a stable version tag such as v1.1.1')
root = Path(__file__).resolve().parents[1]
project = ET.parse(root / 'src/SourcePriority.csproj')
if project.findtext('.//Version') != args.version:
    raise SystemExit('Tag version must match the project Version')
assembly = root / 'src/bin/Release/net10.0/Jellyfin.Plugin.SourcePriority.dll'
if not assembly.is_file():
    raise SystemExit('Build the Release configuration first')
dist = root / 'dist'
dist.mkdir(exist_ok=True)
archive = dist / f'Source-Priority-{args.version}.zip'
with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as package:
    # Fixed metadata keeps checksums stable when packaging the same DLL again.
    info = zipfile.ZipInfo(assembly.name, date_time=(2020, 1, 1, 0, 0, 0))
    info.compress_type = zipfile.ZIP_DEFLATED
    package.writestr(info, assembly.read_bytes())
with zipfile.ZipFile(archive) as package:
    assert package.namelist() == [assembly.name] and package.testzip() is None
entry = {
    'version': args.version + '.0',
    'changelog': 'See the GitHub release notes for this version.',
    'targetAbi': '12.2.0.0',
    'sourceUrl': f'https://github.com/morski/jellyfin-plugin-source-priority/releases/download/v{args.version}/{archive.name}',
    # Jellyfin 12.2 requires MD5 here; SHA256 is supplied separately as well.
    'checksum': hashlib.md5(archive.read_bytes(), usedforsecurity=False).hexdigest(),
    'timestamp': datetime.now(timezone.utc).strftime('%Y-%m-%dT%H:%M:%SZ')
}
(dist / 'version.json').write_text(json.dumps(entry, indent=2) + '\n')
(dist / (archive.name + '.sha256')).write_text(hashlib.sha256(archive.read_bytes()).hexdigest() + '  ' + archive.name + '\n')
print(f'Validated installation package: {archive.name}')
