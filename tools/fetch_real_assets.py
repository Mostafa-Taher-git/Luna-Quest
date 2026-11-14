#!/usr/bin/env python3
"""Fetch the CC0 / public-domain real-world assets used by the Godot build.
- Poly Haven "Moon Rock 01-07" photoscanned models (CC0), glTF at 1k.
- Poly Haven "moon_dusted_03" regolith PBR texture set (CC0), 2k jpg.
- NASA Blue Marble Next Generation (public domain) for the Earth in the sky.
Run from anywhere; writes into engines/godot/LunaQuest.Godot/assets/real/."""
import json, os, pathlib, urllib.request
ROOT = pathlib.Path(__file__).resolve().parents[1]
OUT = ROOT / 'engines/godot/LunaQuest.Godot/assets/real'
UA = {'User-Agent': 'LunaQuest-asset-fetch/1.0'}

def get(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=120) as r:
        return r.read()

def save(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)

def main():
    for i in range(1, 8):
        aid = f'moon_rock_{i:02d}'
        files = json.loads(get(f'https://api.polyhaven.com/files/{aid}'))
        g = files['gltf']['1k']['gltf']
        d = OUT / 'rocks' / aid
        save(d / f'{aid}.gltf', get(g['url']))
        for rel, inc in g['include'].items():
            save(d / rel, get(inc['url']))
        print('rock', aid)
    tex = json.loads(get('https://api.polyhaven.com/files/moon_dusted_03'))
    for key, name in (('Diffuse', 'albedo'), ('nor_gl', 'normal'), ('Rough', 'rough'), ('AO', 'ao'), ('Displacement', 'height')):
        if key in tex and '2k' in tex[key]:
            fmt = 'jpg' if 'jpg' in tex[key]['2k'] else 'png'
            save(OUT / 'regolith' / f'moon_dusted_03_{name}.{fmt}', get(tex[key]['2k'][fmt]['url']))
            print('regolith', name)
    earth = 'https://eoimages.gsfc.nasa.gov/images/imagerecords/73000/73909/world.topo.bathy.200412.3x5400x2700.jpg'
    save(OUT / 'earth' / 'blue_marble_5400.jpg', get(earth))
    print('earth ok')

if __name__ == '__main__':
    main()
