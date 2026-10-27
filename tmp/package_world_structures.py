from pathlib import Path
import json, hashlib, shutil, zipfile

root=Path(r'C:\Users\iam12\Train Survival')
out=root/'output/imagegen/world-structures-v1'
manifest=json.loads((out/'manifest.json').read_text(encoding='utf-8'))
baseline=json.loads((out/'previous-ui-sha256.json').read_text(encoding='utf-8'))
assert len(manifest['items'])==11 and len(baseline)==88
assert all(hashlib.sha256((root/p).read_bytes()).hexdigest()==sha for p,sha in baseline.items())
for name in ['construction-Heater','construction-Tent','construction-target']:
    shutil.copy2(root/'output/ui-integration'/f'{name}.png',out/'reviews'/f'{name}.png')
report={'delivered_images':11,'ui_registered':97,'overall_image_list_completed':99,
    'previous_88_unchanged':True,'editmode':{'passed':56,'failed':0},'playmode':{'passed':5,'failed':0},
    'construction_capture_recheck':{'passed':1,'failed':0},'materials_connected':3,'terrain_prefabs_connected':20,
    'feedback_images_created':0,'feedback_brief_candidates':4,
    'limits':['Static prefab surface renders omit weather and underwater effects.','HUD captures use deterministic test service data at 1920x1080.','MountedGun and Turret icons follow their existing primitive models.']}
(out/'delivery-validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
package=out/'Train-Survival-World-Structures-v1.zip'
files=[p for p in out.rglob('*') if p.is_file() and p.suffix!='.zip']
files+=list((root/'docs/design/references/world-structures').glob('*'))
files+=[root/'docs/design/Train-Survival-월드텍스처-건축물-이미지-브리프.md']
files+=[root/'output/ui-integration'/f'{name}.png' for name in ['construction-Heater','construction-Tent','construction-target']]
for item in manifest['items']:
    asset=root/item['saved_asset'];files.extend([asset,asset.with_suffix('.png.meta')])
with zipfile.ZipFile(package,'w',zipfile.ZIP_DEFLATED) as z:
    for p in sorted(set(files)):z.write(p,p.relative_to(root))
with zipfile.ZipFile(package) as z:
    assert z.testzip() is None
    assert len([n for n in z.namelist() if n.startswith('Assets/') and n.endswith('.png')])==11
print(json.dumps({'package':str(package),'bytes':package.stat().st_size,**report},ensure_ascii=False))
