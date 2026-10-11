#!/usr/bin/env python3
"""Export the application images verified by the Compose integration job."""

import json
import os
import subprocess
from pathlib import Path


def main() -> None:
    revision = os.environ['DRIA_REVISION']
    version = Path('version.txt').read_text().strip()
    manifest = {'revision': revision, 'version': version, 'images': {}}
    sources = {'api': 'dria-api:local', 'web': 'dria-web:local'}
    for component, source in sources.items():
        details = json.loads(subprocess.check_output(['docker', 'image', 'inspect', source]))[0]
        labels = details['Config'].get('Labels') or {}
        if labels.get('org.opencontainers.image.version') != version:
            raise ValueError(f'{source} has an incorrect application version.')
        if labels.get('org.opencontainers.image.revision') != revision:
            raise ValueError(f'{source} was not built from this CI revision.')
        manifest['images'][component] = {'source': source, 'id': details['Id']}

    output = Path('artifacts/images')
    output.mkdir(parents=True, exist_ok=True)
    subprocess.run(['docker', 'save', '--output', str(output / 'images.tar'), *sources.values()], check=True)
    (output / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
    print(f'Exported tested DrIA {version} images.')


if __name__ == '__main__':
    main()
