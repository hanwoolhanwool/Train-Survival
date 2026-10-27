from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, PngImagePlugin
import json, shutil, uuid, re, zipfile

root=Path(r'C:\Users\iam12\Train Survival')
out=root/'output/imagegen/ingame-ui-style-v1'
manifest=json.loads((out/'prompts-and-sources.json').read_text(encoding='utf-8'))
items=manifest['items']
source_dir=out/'originals'
source_dir.mkdir(exist_ok=True)
final_dir=out/'png-512'
final_dir.mkdir(exist_ok=True)
template=(root/'Assets/_Project/Art/Textures/UI/Ready/T_Ready_Icon_Crown.png.meta').read_text(encoding='utf-8')
labels={'Wood':'목재','Timber':'원목','RawFood':'식재료','Harpoon':'집게','Revolver':'리볼버','Bundle':'보따리','CookedMeal':'구운 식사','WinterParka':'방한 파카'}

def ensure_asset_dir(p):
    if not p.exists():
        ensure_asset_dir(p.parent)
        p.mkdir()
        p.with_name(p.name+'.meta').write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n',encoding='utf-8')

qa=[]
for item in items:
    original=source_dir/item['file']
    shutil.copy2(item['source'],original)
    src=Image.open(original)
    assert src.mode=='RGBA'
    alpha=src.getchannel('A')
    assert alpha.getextrema()==(0,255)
    # Visible bounds define layout only. Keep all original alpha values and pixels;
    # resize the entire source uniformly, without recoloring, masking or mirroring.
    bb=alpha.point(lambda a:255 if a>=16 else 0).getbbox()
    cx,cy=(bb[0]+bb[2])/2,(bb[1]+bb[3])/2
    scale=min(420/max(bb[2]-bb[0],bb[3]-bb[1]),254/max(cx,cy,src.width-cx,src.height-cy))
    size=(round(src.width*scale),round(src.height*scale))
    resized=src.resize(size,Image.Resampling.LANCZOS)
    x=round(256-(bb[0]+bb[2])/2*scale)
    y=round(256-(bb[1]+bb[3])/2*scale)
    assert x>=0 and y>=0 and x+size[0]<=512 and y+size[1]<=512,(item['key'],x,y,size)
    image=Image.new('RGBA',(512,512),(0,0,0,0))
    image.alpha_composite(resized,(x,y))
    pnginfo=PngImagePlugin.PngInfo();pnginfo.add(b'sRGB',b'\x00')
    final=final_dir/item['file']
    image.save(final,pnginfo=pnginfo)
    target=root/'Assets/_Project/Art/Textures/UI/InGame/Items'/item['category']/item['file']
    ensure_asset_dir(target.parent)
    if target.exists():
        assert target.read_bytes()==final.read_bytes(),f'Existing different asset must not be overwritten: {target}'
    else:
        shutil.copy2(final,target)
    meta=re.sub(r'^guid: .*$', 'guid: '+uuid.uuid4().hex,template,flags=re.M)
    meta=re.sub(r'(maxTextureSize:) \d+',r'\1 512',meta)
    meta=re.sub(r'(textureCompression:) \d+',r'\1 0',meta)
    meta=re.sub(r'(spriteGenerateFallbackPhysicsShape:) \d+',r'\1 0',meta)
    meta=re.sub(r'(spriteID:) [a-f0-9]+',r'\1 '+uuid.uuid4().hex,meta)
    if not target.with_suffix('.png.meta').exists():
        target.with_suffix('.png.meta').write_text(meta,encoding='utf-8')
    vb=image.getchannel('A').point(lambda a:255 if a>=16 else 0).getbbox()
    assert min(vb[0],vb[1],512-vb[2],512-vb[3])>=40
    assert image.getpixel((0,0))[3]==0 and image.getpixel((511,511))[3]==0
    item['saved_original']=str(original.relative_to(root)).replace('\\','/')
    item['saved_asset']=str(target.relative_to(root)).replace('\\','/')
    qa.append({'key':item['key'],'file':item['saved_asset'],'dimensions':[512,512],'mode':'RGBA','visible_alpha_bounds':vb,'transparent_corners':True,'sprite_metadata':{'mipmaps':False,'filter':'Bilinear','wrap':'Clamp','maxSize':512,'compression':'Uncompressed'}})

manifest['export']='512x512 RGBA sRGB; full original canvas resized uniformly and centered using alpha>=16 visible bounds; visible longest dimension up to 420px, scaled down slightly when needed to retain the entire source canvas; all alpha values preserved; no recoloring, cropping or mirroring.'
(out/'prompts-and-sources.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
(out/'validation.json').write_text(json.dumps({'items':qa,'visual_checks':['Actual reference silhouettes and color blocks reviewed','Harpoon launcher and claw attached; claw upper-right, grip lower-left','No labels, slots, badges or background baked into item artwork'],'unity_runtime_verified':False},ensure_ascii=False,indent=2),encoding='utf-8')

# Review sheet: originals remain separate. This board is not a sprite atlas.
board=Image.new('RGB',(1536,1110),'#141616')
draw=ImageDraw.Draw(board)
def ft(n,b=False):return ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf' if b else 'C:/Windows/Fonts/malgun.ttf',n)
def txt(x,y,t,n=17,col='#F2EAE0',bold=False):draw.text((x,y),t,font=ft(n,bold),fill=col)
txt(40,24,'TRAIN SURVIVAL',16,'#C89B4A',True)
txt(40,57,'스타일 기준 아이콘 8종',32,bold=True)
txt(40,104,'실제 모델 6종 + 신규 디자인 2종  ·  512×512 RGBA  ·  집게 조립 형태와 방향 유지',18,'#9A9089')
for i,item in enumerate(items):
    x,y=40+(i%4)*374,155+(i//4)*408
    draw.rounded_rectangle((x,y,x+342,y+385),10,fill='#211D1B',outline='#4A423C')
    pic=Image.open(final_dir/item['file'])
    big=pic.resize((270,270),Image.Resampling.LANCZOS)
    board.paste(big,(x+36,y+5),big)
    txt(x+20,y+274,labels[item['key']],22,bold=True)
    txt(x+20,y+307,item['file'],13,'#9A9089')
    for j,size in enumerate([32,48,56]):
        px=x+20+j*107
        small=pic.resize((size,size),Image.Resampling.LANCZOS)
        board.paste(small,(px,y+332+(56-size)//2),small)
        txt(px+size+3,y+347,str(size),11,'#9A9089')
txt(40,995,'작은 표시는 실제 32 / 48 / 56px 크기입니다. 위 큰 그림은 스타일 검토용입니다.',18,'#9A9089')
txt(40,1030,'집게: 발사기 + 집게발을 한 도구로 유지  |  그림에 수량·입력 키·선택 테두리를 포함하지 않음',18,'#9A9089')
txt(40,1067,'2026.10.27  ·  제작 1차  ·  UI 연결 전',16,'#C89B4A')
board.save(out/'StyleBaseline-8.png')

# Cross-background small-size comparison.
swatches=[('Forest','#33523A'),('Desert','#DCA85C'),('Sea','#2D7387'),('Arctic','#E6EEF2'),('Night','#141C2B')]
test=Image.new('RGB',(1040,540),'#141616');td=ImageDraw.Draw(test)
td.text((20,12),'48px background checks',font=ft(20,True),fill='#F2EAE0')
for row,(label,color) in enumerate(swatches):
    yy=55+row*92
    td.rectangle((15,yy,1025,yy+82),fill=color)
    td.text((27,yy+27),label,font=ft(16,True),fill='#141616' if label in ('Arctic','Desert') else '#F2EAE0')
    for col,item in enumerate(items):
        pic=Image.open(final_dir/item['file']).resize((48,48),Image.Resampling.LANCZOS)
        test.paste(pic,(170+col*104,yy+16),pic)
test.save(out/'Readability-48px.png')

with zipfile.ZipFile(out/'TrainSurvival-StyleIcons-8-512.zip','w',compression=zipfile.ZIP_DEFLATED) as z:
    for item in items:
        z.write(final_dir/item['file'],item['file'])
    z.write(out/'StyleBaseline-8.png','Preview-StyleBaseline.png')
    z.write(out/'Readability-48px.png','Preview-Readability.png')
    z.write(out/'prompts-and-sources.json','prompts-and-sources.json')
    z.write(out/'validation.json','validation.json')
print(json.dumps({'exported':len(items),'asset_root':str(root/'Assets/_Project/Art/Textures/UI/InGame/Items'),'review_sheet':str(out/'StyleBaseline-8.png'),'archive':str(out/'TrainSurvival-StyleIcons-8-512.zip')},ensure_ascii=False))
