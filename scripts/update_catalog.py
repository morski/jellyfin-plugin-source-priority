"""Merge a published release into the catalog, retaining previous versions."""
import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
entry = json.loads((root / 'dist/version.json').read_text())
path = root / 'manifest.json'
catalog = json.loads(path.read_text())
plugin_id = '5abdf826-f64e-45c5-b3d8-9b9cf31a053f'
plugin = next((p for p in catalog if p.get('guid') == plugin_id), None)
if plugin is None:
    plugin = {
        'guid': plugin_id,
        'name': 'Source Priority',
        'description': 'Per-library folder priorities for merged movie and TV episode versions.',
        'overview': 'Prefer versions from selected library folders while keeping every copy available.',
        'owner': 'morski',
        'category': 'General',
        'versions': []
    }
    catalog.append(plugin)
plugin['versions'] = [v for v in plugin['versions'] if v['version'] != entry['version']] + [entry]
plugin['versions'].sort(key=lambda v: tuple(map(int, v['version'].split('.'))), reverse=True)
path.write_text(json.dumps(catalog, indent=2) + '\n')
print('Catalog updated')
