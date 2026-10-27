from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, PngImagePlugin
import json, shutil, uuid, re

ROOT=Path(r'C:\Users\iam12\Train Survival')
OUT=ROOT/'output/imagegen/ingame-ui-production-v1'
ART=ROOT/'Assets/_Project/Art/Textures/UI/InGame'
TEMPLATE=(ART/'Items/Resources/I_Res_Wood.png.meta').read_text(encoding='utf-8')

def asset_dir(p):
    if not p.exists():
        asset_dir(p.parent)
        p.mkdir()
        p.with_name(p.name+'.meta').write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n',encoding='utf-8')

def save_asset(im,target,border=(0,0,0,0)):
    asset_dir(target.parent)
    if target.exists():
        old=Image.open(target).convert('RGBA')
        assert old.size==im.size and old.tobytes()==im.tobytes(),f'Refusing to replace existing asset: {target}'
    else:
        pi=PngImagePlugin.PngInfo();pi.add(b'sRGB',b'\x00')
        im.save(target,pnginfo=pi)
    mp=target.with_suffix('.png.meta')
    if not mp.exists():
        meta=re.sub(r'^guid: .*$', 'guid: '+uuid.uuid4().hex,TEMPLATE,flags=re.M)
        meta=re.sub(r'(spriteID:) [a-f0-9]+',r'\1 '+uuid.uuid4().hex,meta)
        meta=re.sub(r'(maxTextureSize:) \d+',lambda m:m[1]+' '+str(max(im.size)),meta)
        meta=re.sub(r'spriteBorder: .*',f'spriteBorder: {{x: {border[0]}, y: {border[1]}, z: {border[2]}, w: {border[3]}}}',meta)
        # Full rectangle supports slicing and predictable masks.
        meta=meta.replace('spriteMeshType: 1','spriteMeshType: 0')
        mp.write_text(meta,encoding='utf-8')

def font(n,bold=False):
    return ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf' if bold else 'C:/Windows/Fonts/malgun.ttf',n)

def board(items,name,title,cols=4):
    rows=(len(items)+cols-1)//cols
    canvas=Image.new('RGB',(cols*320+48,rows*330+140),'#141616');d=ImageDraw.Draw(canvas)
    d.text((24,20),title,font=font(27,True),fill='#F2EAE0')
    d.text((24,65),'TRAIN SURVIVAL  ·  512×512 RGBA  ·  32 / 48 / 56px 비교',font=font(16),fill='#C89B4A')
    for i,item in enumerate(items):
        x,y=24+(i%cols)*320,112+(i//cols)*330
        d.rounded_rectangle((x,y,x+304,y+314),radius=10,fill='#211D1B',outline='#4A423C')
        pic=Image.open(ROOT/item['saved_asset']).convert('RGBA')
        big=pic.resize((220,220),Image.Resampling.LANCZOS);canvas.paste(big,(x+42,y),big)
        d.text((x+16,y+217),item.get('label',item['key']),font=font(20,True),fill='#F2EAE0')
        d.text((x+16,y+246),item['key'],font=font(13),fill='#9A9089')
        for j,size in enumerate((32,48,56)):
            small=pic.resize((size,size),Image.Resampling.LANCZOS)
            canvas.paste(small,(x+16+j*98,y+263+(56-size)//2),small)
    canvas.save(OUT/name)

def main():
    (OUT/'originals').mkdir(exist_ok=True);(OUT/'png-512').mkdir(exist_ok=True)
    items=[];qa=[]
    plan=json.loads((OUT/'job-plan.json').read_text(encoding='utf-8'))['items']
    for job in plan:
        rp=OUT/'records'/f"{job['key']}.json"
        if not rp.exists():continue
        item=json.loads(rp.read_text(encoding='utf-8'))
        source=re.search(r' as (C:\\[^\n]+?\.png) by default',item['output_hint']).group(1)
        original=OUT/'originals'/item['file'];shutil.copy2(source,original)
        src=Image.open(original);assert src.mode=='RGBA',item['key']
        a=src.getchannel('A');assert a.getextrema()==(0,255),item['key']
        bb=a.point(lambda a:255 if a>=16 else 0).getbbox()
        cx,cy=(bb[0]+bb[2])/2,(bb[1]+bb[3])/2
        scale=min(420/max(bb[2]-bb[0],bb[3]-bb[1]),254/max(cx,cy,src.width-cx,src.height-cy))
        size=tuple(round(v*scale) for v in src.size)
        pos=(round(256-cx*scale),round(256-cy*scale))
        assert min(pos)>=0 and pos[0]+size[0]<=512 and pos[1]+size[1]<=512
        im=Image.new('RGBA',(512,512));im.alpha_composite(src.resize(size,Image.Resampling.LANCZOS),pos)
        vb=im.getchannel('A').point(lambda a:255 if a>=16 else 0).getbbox()
        assert min(vb[0],vb[1],512-vb[2],512-vb[3])>=40,item['key']
        target=ART/'Items'/item['category']/item['file'];save_asset(im,target)
        shutil.copy2(target,OUT/'png-512'/item['file'])
        item['source']=source;item['saved_original']=original.relative_to(ROOT).as_posix();item['saved_asset']=target.relative_to(ROOT).as_posix()
        items.append(item);qa.append({'key':item['key'],'dimensions':[512,512],'mode':'RGBA','visible_bounds':vb,'minimum_margin':min(vb[0],vb[1],512-vb[2],512-vb[3])})
    (OUT/'prompts-and-sources.json').write_text(json.dumps({'mode':'builtin image_gen, separate call for each item','export':'Uniform resize of full canvas; original alpha preserved; no mirroring or recoloring','items':items},ensure_ascii=False,indent=2),encoding='utf-8')
    (OUT/'item-validation.json').write_text(json.dumps({'items':qa,'unity_runtime_verified':False},ensure_ascii=False,indent=2),encoding='utf-8')
    for cat,label in [('Resources','자원·탄약·음식'),('Equipment','무기·도구·장비')]:
        group=[i for i in items if i['category']==cat]
        if group:board(group,f'{cat}-Remaining.png',f'{label} 추가 제작 {len(group)}종')
    print(json.dumps({'exported':len(items),'pending':[i['key'] for i in plan if i['key'] not in [r['key'] for r in items]]},ensure_ascii=False))

if __name__=='__main__':main()
