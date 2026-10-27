from pathlib import Path
from PIL import Image, ImageDraw, ImageChops
import json, hashlib, re, zipfile, shutil
from export_remaining_icons import ROOT, OUT, ART, font, save_asset

def nine(im,size,border):
    l,b,r,t=border;w,h=im.size;tw,th=size
    if not any(border):return im.resize(size,Image.Resampling.LANCZOS)
    assert tw>=l+r and th>=b+t
    sx=[0,l,w-r,w];sy=[0,t,h-b,h];dx=[0,l,tw-r,tw];dy=[0,t,th-b,th]
    dst=Image.new('RGBA',size)
    for yy in range(3):
        for xx in range(3):
            sw,sh=sx[xx+1]-sx[xx],sy[yy+1]-sy[yy]
            dw,dh=dx[xx+1]-dx[xx],dy[yy+1]-dy[yy]
            if min(sw,sh,dw,dh)>0:
                tile=im.crop((sx[xx],sy[yy],sx[xx+1],sy[yy+1])).resize((dw,dh),Image.Resampling.LANCZOS)
                dst.paste(tile,(dx[xx],dy[yy]))
    return dst

def tint(im,color,alpha=1):
    rgb=Image.new('RGB',im.size,color)
    rgb=ImageChops.multiply(rgb,im.convert('RGB')).convert('RGBA')
    rgb.putalpha(im.getchannel('A').point(lambda a:round(a*alpha)))
    return rgb

def main():
    vm=json.loads((OUT/'vector-manifest.json').read_text(encoding='utf-8'))['assets']
    for v in vm:
        im=Image.open(ROOT/v['preview']).convert('RGBA')
        save_asset(im,ROOT/v['saved_asset'],v['border'])
    meta={v['key']:v for v in vm if v['category']=='Components'}
    def part(key,size,color='#F2EAE0',alpha=1):
        v=meta[key];im=Image.open(ROOT/v['saved_asset']).convert('RGBA')
        return tint(nine(im,size,v['border']),color,alpha)
    def glyph(cat,key,size,color='#F2EAE0'):
        im=Image.open(ART/'Glyphs'/cat/(f"G_{'Action' if cat=='Actions' else 'Equip' if cat=='Equipment' else cat}_{key}.png")).convert('RGBA')
        return tint(im.resize((size,size),Image.Resampling.LANCZOS),color)
    def item(key,size):
        matches=list((ART/'Items').rglob('*_'+key+'.png'));assert len(matches)==1
        return Image.open(matches[0]).convert('RGBA').resize((size,size),Image.Resampling.LANCZOS)
    def paste(dst,p,pos):dst.paste(p,pos,p)
    def text(d,pos,s,size=18,color='#F2EAE0',bold=False):d.text(pos,s,font=font(size,bold),fill=color)

    # Show the actual reusable masks under neutral and theme colors.
    sheet=Image.new('RGB',(1320,1000),'#141616');d=ImageDraw.Draw(sheet)
    text(d,(24,18),'공통 UI 원본 부품 12종',28,bold=True)
    text(d,(24,62),'색상은 미리보기에만 적용 · 원본은 흰색/중립 회색 · 글자 없는 재사용 부품',17,'#C89B4A')
    target_sizes={'PanelBody':(340,102),'PanelHeader':(340,48),'SlotBase':(72,72),'SlotOutline':(72,72),'FocusCorners':(72,72),'ControlFace':(240,40),'GaugeTrack':(300,24),'GaugeFill':(280,16),'Keycap':(80,40),'ProgressRing':(64,64),'BadgeDisc':(48,48),'Divider':(340,8)}
    for i,c in enumerate(meta.values()):
        x,y=24+i%3*432,108+i//3*218
        d.rounded_rectangle((x,y,x+408,y+200),8,fill='#292522',outline='#4A423C')
        name=c['key'];size=target_sizes[name]
        color='#C89B4A' if name in ['FocusCorners','GaugeFill'] else '#F2EAE0' if name in ['SlotOutline','ProgressRing','Divider'] else '#69605A'
        paste(sheet,part(name,size,color),(x+20,y+15))
        text(d,(x+20,y+125),name,20,bold=True)
        text(d,(x+20,y+156),f"{c['size'][0]}×{c['size'][1]}  |  L/B/R/T: {' / '.join(map(str,c['border']))}",13,'#B8ADA5')
    sheet.save(OUT/'Components-12.png')

    # Composition sheet uses final assets at their specified display sizes.
    demo=Image.new('RGB',(1120,870),'#192321');d=ImageDraw.Draw(demo)
    text(d,(32,24),'완성 에셋 조합 검토',30,bold=True)
    text(d,(32,69),'실제 PNG를 사용한 배치 예시 · 게임 실행 화면 아님 · 64/72px 슬롯 · 상태 기호 24px',16,'#C89B4A')
    for x,w in [(32,392),(448,392),(864,224)]:
        paste(demo,part('PanelBody',(w,338),'#1F1B1A',.88),(x,120))
        paste(demo,part('PanelHeader',(w,48),'#36302B'),(x,120))
    text(d,(52,130),'가방',22,bold=True);text(d,(468,130),'제작',22,bold=True);text(d,(884,130),'장비',22,bold=True)
    inventory=['Wood','Timber','Stone','Scrap','Niter','RawFood','OreVein','Rice','Salt','Ice','RareMetal','RelicPart','BossCore','Fish','PreservedMeal']
    for i,k in enumerate(inventory):
        x,y=52+i%5*72,184+i//5*80
        paste(demo,part('SlotBase',(64,64),'#302A25'),(x,y));paste(demo,item(k,48),(x+8,y+8))
        paste(demo,part('SlotOutline',(64,64),'#4A423C'),(x,y))
        if i==0:paste(demo,part('FocusCorners',(64,64),'#C89B4A'),(x,y))
        paste(demo,part('BadgeDisc',(22,22),'#1F1B1A'),(x+40,y+40));text(d,(x+46,y+42),str((i+1)*3),12)
    for i,(k,name,mat) in enumerate([('CookedMeal','구운 식사','RawFood'),('Hammer','수리 망치','Scrap'),('WinterPants','방한 바지','Bundle')]):
        y=178+i*88
        paste(demo,part('ControlFace',(352,80),'#38312C'),(468,y));paste(demo,item(k,48),(476,y+12))
        text(d,(534,y+9),name,18,bold=True);paste(demo,item(mat,32),(532,y+38));text(d,(568,y+43),'× 3',14)
        paste(demo,part('ControlFace',(96,40),'#C89B4A'),(712,y+20));text(d,(740,y+28),'제작',16,'#1F1B1A',True)
    for i,(cat,k,label) in enumerate([('Equipment','Head','머리'),('Equipment','Body','상체'),('Equipment','Legs','하체'),('Equipment','Feet','신발')]):
        x,y=884+(i%2)*96,184+(i//2)*120
        paste(demo,part('SlotBase',(64,64),'#302A25'),(x,y));paste(demo,glyph(cat,k,32,'#9A9089'),(x+16,y+16));text(d,(x+15,y+72),label,15)
    text(d,(32,490),'상태와 작업 진행',20,bold=True)
    for i,(k,label,col,ratio) in enumerate([('Health','체력','#9FC46E',.8),('Hunger','허기','#E3B23C',.35),('Temperature','체온','#67E0D2',.62)]):
        y=536+i*54;paste(demo,glyph('Status',k,24,col),(32,y));text(d,(66,y-1),label,16)
        paste(demo,part('GaugeTrack',(240,16),'#101313'),(116,y+4))
        fill=part('GaugeFill',(240,16),col);fill=fill.crop((0,0,round(240*ratio),16));paste(demo,fill,(116,y+4))
    text(d,(448,490),'상호작용 · 선택 · 경고',20,bold=True)
    paste(demo,part('PanelBody',(392,56),'#1F1B1A',.88),(448,528));paste(demo,glyph('Actions','Repair',24),(464,544))
    text(d,(502,543),'열차 수리',18);paste(demo,part('Keycap',(48,32),'#4A423C'),(714,540));text(d,(732,546),'E',14)
    text(d,(773,546),'유지',14)
    paste(demo,part('PanelBody',(392,56),'#1F1B1A',.88),(448,600));paste(demo,glyph('System','Warning',24,'#F0705F'),(464,616));text(d,(502,616),'고철이 2개 부족합니다',18)
    for i,k in enumerate(['Harpoon','Revolver','Shotgun','Hammer','FishingRod']):
        x,y=448+i*80,714
        paste(demo,part('SlotBase',(72,72),'#302A25'),(x,y));paste(demo,item(k,56),(x+8,y+8))
        paste(demo,part('SlotOutline',(72,72),'#4A423C'),(x,y))
        if i==0:paste(demo,part('FocusCorners',(72,72),'#C89B4A'),(x,y))
        paste(demo,part('Keycap',(24,24),'#1F1B1A'),(x+2,y+2));text(d,(x+10,y+4),str(i+1),12)
    paste(demo,part('ProgressRing',(32,32),'#C89B4A'),(32,724));text(d,(78,725),'작업 진행 링 원본',18)
    text(d,(32,820),'선택은 황동 모서리 · 위험은 기호와 문구 · 숫자·키·문구는 그림과 분리',17,'#B8ADA5')
    demo.save(OUT/'UI-Composition.png')

    # Every final PNG must match the exact file set implied by the brief.
    brief=(ROOT/'docs/design/Train-Survival-인게임-UI-이미지-브리프.md').read_text(encoding='utf-8')
    rows=re.findall(r'^\| ([A-Za-z]+) \| ([^|]+) \| (?:실물[^|]*|신규[^|]*|조립 실물[^|]*)\|',brief.split('## 5 상태')[0],re.M)
    assert len(rows)==40
    expected=set()
    for i,(key,label) in enumerate(rows):expected.add(f"Items/{'Resources/I_Res_' if i<23 else 'Equipment/I_Item_'}{key}.png")
    expected|={Path(v['saved_asset']).relative_to(ART.relative_to(ROOT)).as_posix() for v in vm}
    found={p.relative_to(ART).as_posix() for p in ART.rglob('*.png')}
    assert found==expected,(found-expected,expected-found)
    records=[];guids=set()
    for rel in sorted(expected):
        p=ART/rel;im=Image.open(p);assert im.mode=='RGBA'
        if rel.startswith('Items/'):assert im.size==(512,512)
        elif rel.startswith('Glyphs/'):
            assert im.size==(128,128)
            assert all(r==g==b==255 for r,g,b,a in im.getdata() if a>0),rel
        else:
            assert all(r==g==b for r,g,b,a in im.getdata() if a>0),rel
        mt=p.with_suffix('.png.meta').read_text(encoding='utf-8');guid=re.search(r'^guid: (\w+)',mt,re.M)[1]
        assert guid not in guids;guids.add(guid)
        assert 'enableMipMap: 0' in mt and 'spriteMode: 1' in mt and 'alphaIsTransparency: 1' in mt
        amin,amax=im.getchannel('A').getextrema()
        assert amin==0 and amax>=(200 if rel.startswith('Components/') else 255),(rel,amin,amax)
        records.append({'file':rel,'size':im.size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'guid':guid})
    for key in ['SlotOutline','FocusCorners','ProgressRing']:
        im=Image.open(ROOT/meta[key]['saved_asset']);assert im.getpixel((im.width//2,im.height//2))[3]==0,key
    for key in ['PanelBody','SlotBase','GaugeTrack']:
        im=Image.open(ROOT/meta[key]['saved_asset']);assert im.getpixel((im.width//2,im.height//2))[3]==255,key
    slice_tests=[]
    for key,sizes in [('PanelBody',[(280,160),(392,220),(800,300)]),('SlotBase',[(64,64),(72,72)]),('ControlFace',[(96,40),(240,40)]),('GaugeTrack',[(240,16),(520,16)])]:
        c=meta[key];im=Image.open(ROOT/c['saved_asset']);l,b,r,t=c['border']
        for size in sizes:
            scaled=nine(im,size,c['border']);assert scaled.size==size
            for src_box,dst_box in [((0,0,l,t),(0,0,l,t)),((im.width-r,im.height-b,im.width,im.height),(size[0]-r,size[1]-b,*size))]:
                assert im.crop(src_box).tobytes()==scaled.crop(dst_box).tobytes(),(key,size)
            slice_tests.append({'key':key,'size':size,'corners_unchanged':True})
    baseline=json.loads((ROOT/'output/imagegen/ingame-ui-style-v1/prompts-and-sources.json').read_text(encoding='utf-8'))['items']
    for i in baseline:
        saved=ROOT/i['saved_asset'];assert saved.read_bytes()==(ROOT/'output/imagegen/ingame-ui-style-v1/png-512'/i['file']).read_bytes()
    # Small items on all five required environment colors.
    small=Image.new('RGB',(1320,1580),'#141616');sd=ImageDraw.Draw(small)
    text(sd,(24,16),'40종 아이템 · 48px 배경 대비 검토',26,bold=True)
    swatches=[('숲','#33523A'),('사막','#DCA85C'),('바다','#2D7387'),('북극','#E6EEF2'),('밤','#141C2B')]
    for rr,(label,color) in enumerate(swatches):
        y=72+rr*300;sd.rectangle((16,y,1304,y+284),fill=color)
        fg='#141616' if label in ['사막','북극'] else '#F2EAE0';text(sd,(32,y+8),label,18,fg,True)
        for i,(key,label2) in enumerate(rows):
            x,yy=40+(i%10)*126,y+44+(i//10)*59;paste(small,item(key,48),(x,yy));text(sd,(x+47,yy+18),str(i+1),11,fg)
    small.save(OUT/'Items-40-Readability.png')
    validation={'final_png_count':88,'new_png_count':80,'items':40,'glyphs':36,'components':12,'baseline_8_unchanged':True,'nine_slice_checks':slice_tests,'unity_runtime_verified':False,'files':records}
    (OUT/'validation.json').write_text(json.dumps(validation,ensure_ascii=False,indent=2),encoding='utf-8')
    with zipfile.ZipFile(OUT/'TrainSurvival-InGameUI-88.zip','w',zipfile.ZIP_DEFLATED) as z:
        for p in ART.rglob('*'):
            if p.is_file():z.write(p,Path('Assets/_Project/Art/Textures/UI/InGame')/p.relative_to(ART))
        for p in (OUT/'vectors').glob('*.svg'):z.write(p,'Sources/Vectors/'+p.name)
        for p in OUT.glob('*.png'):z.write(p,'Review/'+p.name)
        for name in ['prompts-and-sources.json','vector-manifest.json','validation.json','job-plan.json']:
            z.write(OUT/name,'Production/'+name)
        z.write(ROOT/'output/imagegen/ingame-ui-style-v1/prompts-and-sources.json','Production/baseline-8-prompts.json')
        z.writestr('README.txt','Train Survival in-game UI art: 40 item icons (512 RGBA), 36 glyphs (128 white RGBA), 12 reusable UI parts (neutral masks). Includes Unity Sprite .meta settings and 48 SVG sources. Existing 8 style-baseline icons are unchanged. Item artwork generated using built-in image_gen; glyphs and UI geometry authored as SVG per the brief. UI wiring and Unity runtime validation have not been performed. Source references and prompts are under Production/. For glyphs use runtime tint; PanelBody/SlotBase/GaugeTrack source centers are opaque, apply panel alpha 0.88 once at display time. Do not bake labels, quantities or key bindings into the art. Use sliced rendering for components with nonzero borders; crop GaugeFill for progress and use radial fill on ProgressRing. Review images are not runtime atlases.')
    print(json.dumps({'final_pngs':len(found),'new_pngs':80,'svg_sources':len(list((OUT/'vectors').glob('*.svg'))),'archive':str(OUT/'TrainSurvival-InGameUI-88.zip')},ensure_ascii=False))

if __name__=='__main__':main()
