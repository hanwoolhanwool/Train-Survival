from pathlib import Path
from PIL import Image
import json, re, shutil, uuid, hashlib
import export_remaining_icons as helper

ROOT=helper.ROOT
OUT=ROOT/'output/imagegen/world-structures-v1'
ART=helper.ART
helper.OUT=OUT
keys=['Heater','Storage','Workbench','Campfire','Purifier','Furnace','MountedGun','Turret','Tent']
labels=['난방기','공유 창고','제작대','화덕','정수기','강화 난방로','거치 기관총','자동 터렛','천막']
baseline=OUT/'previous-ui-sha256.json'
if not baseline.exists():
    baseline.write_text(json.dumps({str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in ART.rglob('*.png')},indent=2),encoding='utf-8')
(OUT/'originals').mkdir(exist_ok=True)
(OUT/'final').mkdir(exist_ok=True)
items=[]
for key,label in zip(keys,labels):
    rec=json.loads((OUT/'records'/f'{key}.json').read_text(encoding='utf-8'))
    source=Path(re.search(r' as (C:\\[^\n]+?\.png) by default',rec['resultHint']).group(1))
    original=OUT/'originals'/f'{key}.png';shutil.copy2(source,original)
    src=Image.open(original)
    assert src.mode=='RGBA' and src.getchannel('A').getextrema()==(0,255),key
    bb=src.getchannel('A').point(lambda a:255 if a>=16 else 0).getbbox()
    cx,cy=(bb[0]+bb[2])/2,(bb[1]+bb[3])/2
    scale=min(420/max(bb[2]-bb[0],bb[3]-bb[1]),254/max(cx,cy,src.width-cx,src.height-cy))
    size=tuple(round(v*scale) for v in src.size);pos=(round(256-cx*scale),round(256-cy*scale))
    im=Image.new('RGBA',(512,512));im.alpha_composite(src.resize(size,Image.Resampling.LANCZOS),pos)
    vb=im.getchannel('A').point(lambda a:255 if a>=16 else 0).getbbox()
    margin=min(vb[0],vb[1],512-vb[2],512-vb[3]);assert margin>=40,key
    target=ART/'Structures'/f'I_Structure_{key}.png';helper.save_asset(im,target)
    shutil.copy2(target,OUT/'final'/target.name)
    rec.update(label=label,source=str(source),saved_asset=target.relative_to(ROOT).as_posix(),dimensions=[512,512],mode='RGBA',visible_bounds=vb,minimum_margin=margin)
    items.append(rec)

template=(ROOT/'Assets/_Project/Art/Textures/T_Env_Ground_Forest_BaseColor.png.meta').read_text(encoding='utf-8')
for key,file in [('Ground_Arctic-seam','T_Env_Ground_Arctic_BaseColor'),('Seabed','T_Env_Seabed_BaseColor')]:
    rec=json.loads((OUT/'records'/f'{key}.json').read_text(encoding='utf-8'))
    im=Image.open(OUT/'originals'/f'{key}.png').convert('RGB').resize((1024,1024),Image.Resampling.LANCZOS)
    target=ROOT/'Assets/_Project/Art/Textures'/f'{file}.png'
    if target.exists():assert Image.open(target).tobytes()==im.tobytes()
    else:im.save(target)
    mp=target.with_suffix('.png.meta')
    if not mp.exists():
        meta=re.sub(r'^guid: .*$', 'guid: '+uuid.uuid4().hex,template,flags=re.M)
        meta=re.sub(r'maxTextureSize: \d+','maxTextureSize: 1024',meta)
        meta=meta.replace('aniso: 1','aniso: 4')
        mp.write_text(meta,encoding='utf-8')
    shutil.copy2(target,OUT/'final'/target.name)
    rec.update(saved_asset=target.relative_to(ROOT).as_posix(),dimensions=[1024,1024],mode='RGB')
    items.append(rec)

catalog=ROOT/'Assets/_Project/Resources/InGameUiArt.asset'
data=catalog.read_text(encoding='utf-8')
for key in keys:
    name=f'I_Structure_{key}'
    if f'Key: {name}\n' in data:continue
    guid=re.search(r'^guid: (\w+)',(ART/'Structures'/f'{name}.png.meta').read_text(),re.M).group(1)
    data+=f'  - Key: {name}\n    Sprite: {{fileID: 21300000, guid: {guid}, type: 3}}\n'
catalog.write_text(data,encoding='utf-8')
previous=json.loads(baseline.read_text())
assert len(previous)==88
assert all(hashlib.sha256((ROOT/p).read_bytes()).hexdigest()==sha for p,sha in previous.items())
helper.board(items[:9],'Structures-Review.png','설치 가능한 건축물 9종',cols=3)
(OUT/'manifest.json').write_text(json.dumps({'mode':'builtin image_gen; separate image per asset','export':'Uniform full-canvas resize preserving original alpha. No mirroring/recoloring. World images resized to 1024 RGB.','previous_88_unchanged':True,'items':items},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'exported':len(items),'registered_ui_count':len(re.findall(r'  - Key:',data)),'previous_88_unchanged':True}))
