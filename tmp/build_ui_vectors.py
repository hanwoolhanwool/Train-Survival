from pathlib import Path
from io import BytesIO
import json, math, re
import pymupdf as fitz
from PIL import Image, ImageDraw
from export_remaining_icons import ROOT, OUT, ART, font, save_asset

VEC=OUT/'vectors';VEC.mkdir(exist_ok=True)
def path(d,fill='white',stroke=None,width=12):
    return f'<path d="{d}" fill="{fill}"'+(f' stroke="{stroke}" stroke-width="{width}" stroke-linecap="round" stroke-linejoin="round"' if stroke else '')+' fill-rule="evenodd"/>'
def line(d,width=12):return path(d,'none','white',width)
def rect(x,y,w,h,r=0,fill='white',stroke=None,width=12):
    return f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}" fill="{fill}"'+(f' stroke="{stroke}" stroke-width="{width}"' if stroke else '')+'/>'
def circle(x,y,r,fill='white',stroke=None,width=12):
    return f'<circle cx="{x}" cy="{y}" r="{r}" fill="{fill}"'+(f' stroke="{stroke}" stroke-width="{width}"' if stroke else '')+'/>'
def group(body,transform):return f'<g transform="{transform}">{body}</g>'
def heart():return path('M64 108 C55 99 16 75 16 47 C16 19 49 16 64 39 C79 16 112 19 112 47 C112 75 73 99 64 108Z')
def boot():return path('M27 22 H66 V65 Q68 76 88 79 Q108 83 108 99 V108 H22 V78 L27 66Z')
def rays(cx,cy,inner,outer,n,start=0,width=12):
    s=''
    for i in range(n):
        a=start+2*math.pi*i/n
        s+=line(f'M{cx+inner*math.cos(a):.2f} {cy+inner*math.sin(a):.2f} L{cx+outer*math.cos(a):.2f} {cy+outer*math.sin(a):.2f}',width)
    return s

glyphs=[]
def glyph(cat,key,label,body):glyphs.append({'category':cat,'key':key,'label':label,'body':body})
glyph('Status','Health','체력',heart())
glyph('Status','Hunger','허기',path('M16 64 H93 C90 94 78 108 55 108 C32 108 20 93 16 64Z')+line('M91 22 L77 48')+path('M88 19 C99 11 114 20 109 31 C104 41 90 43 85 34 C82 30 84 23 88 19Z'))
glyph('Status','Temperature','체온',rect(58,18,12,73,6)+circle(64,91,22)+line('M98 32 H108 M98 59 H108'))
glyph('Status','Cold','추위',line('M64 18 V110 M24 41 L104 87 M24 87 L104 41')+line('M49 26 L64 39 L79 26 M49 102 L64 89 L79 102 M25 61 L42 52 L41 33 M87 95 L86 76 L103 67 M25 67 L42 76 L41 95 M87 33 L86 52 L103 61',11))
glyph('Status','Heat','더위',circle(45,66,27)+line('M88 22 C73 36 103 43 88 57 C73 71 103 78 88 92 M108 38 C98 48 116 57 108 68 M108 86 V104',11))
glyph('Status','Frostbite','동상',group(boot(),'translate(-3 7) scale(.85)')+path('M86 15 L113 35 L100 62 L77 43Z'))
glyph('Status','Fuel','연료',line('M20 108 V45 Q20 20 45 20 H83 Q108 20 108 45 V108 H20',12)+path('M65 37 C69 55 86 61 86 79 C86 96 75 102 63 102 C48 102 40 91 42 79 C44 67 53 61 52 53 C61 60 62 65 62 69 C72 58 62 50 65 37Z'))
glyph('Status','Ammo','탄약 상태',line('M22 67 V108 H106 V67 M22 67 H36 M92 67 H106')+rect(42,32,16,56,7)+rect(75,32,16,56,7))
glyph('Status','Regen','재생',group(heart(),'translate(0 25) scale(.68)')+line('M98 66 V21 M82 37 L98 21 L114 37'))
glyph('Status','Warmth','보온',path('M64 18 C89 18 109 28 109 44 C109 60 87 69 64 69 C39 69 19 60 19 44 C19 28 39 18 64 18Z M43 34 C29 40 31 49 44 53 H84 C97 49 99 40 85 34Z')+path('M33 60 H53 V111 H29Z M75 60 H95 L102 100 H82Z'))
glyph('Status','DetachedCar','이탈 칸',rect(16,28,68,54,7)+circle(31,101,10)+circle(69,101,10)+path('M30 41 H69 V59 H30Z','black')+line('M84 76 H95 M110 62 L101 48 M111 88 L102 103',11))
glyph('Status','Day','낮',circle(64,64,22,'none','white',12)+rays(64,64,43,48,8,0,11))
glyph('Status','Night','밤',path('M77 19 C47 8 18 31 18 64 C18 96 48 118 78 107 C92 102 103 90 107 76 C79 86 52 67 52 43 C52 31 62 22 77 19Z')+path('M99 17 L104 28 L115 33 L104 38 L99 49 L94 38 L83 33 L94 28Z'))

glyph('Actions','Interact','상호작용',path('M35 60 V36 Q35 28 43 28 Q51 28 51 36 V56 V23 Q51 15 59 15 Q67 15 67 23 V56 V30 Q67 22 75 22 Q83 22 83 30 V59 V42 Q83 34 91 34 Q99 34 99 42 V79 Q99 100 83 111 H52 L23 81 Q15 72 22 65 Q29 59 35 67Z')+line('M18 26 L23 33',11))
glyph('Actions','Grab','집게 채집',path('M43 19 L16 46 L16 77 L42 99 L54 88 L33 69 V54 L53 34Z M85 19 L112 46 V77 L86 99 L74 88 L95 69 V54 L75 34Z')+rect(51,90,26,22,5))
glyph('Actions','Repair','수리',group(rect(32,18,76,30,8)+rect(61,42,18,72,5),'rotate(35 64 64)'))
for key,label,plus in [('Build',' 설치·증축',True),('Demolish','철거',False)]:
    glyph('Actions',key,label,rect(18,22,22,86,4)+rect(50,22,22,86,4)+line('M90 65 H112')+(line('M101 54 V76') if plus else ''))
glyph('Actions','Recouple','재결합',line('M48 36 H37 C13 36 13 92 37 92 H48 M80 36 H91 C115 36 115 92 91 92 H80 M43 64 H85',14))
glyph('Actions','Refuel','연료 투입',line('M69 25 H94 Q110 25 110 43 V106 H62 V91')+line('M18 63 H78 M58 43 L78 63 L58 83',14))
glyph('Actions','Craft','제작',rect(17,61,94,15,3)+rect(26,76,14,33,3)+rect(88,76,14,33,3)+group(rect(43,18,47,16,4)+rect(60,30,13,31,3),'rotate(-20 64 42)'))
glyph('Actions','Cook','요리',path('M29 64 H99 V90 Q99 109 80 109 H48 Q29 109 29 90Z')+line('M20 45 H108 M17 76 H29 M99 76 H111')+path('M30 44 Q38 23 58 23 V16 H70 V23 Q90 23 98 44Z'))
glyph('Actions','Reload','재장전',line('M27 91 A44 44 0 1 1 108 53',12)+path('M112 65 L85 54 L112 36Z')+path('M54 91 V51 L64 34 L74 51 V91Z')+rect(50,94,28,13,3))
glyph('Actions','FishBite','입질·챔질',group(path('M51 46 Q51 33 64 33 Q77 33 77 46 V83 Q77 96 64 96 Q51 96 51 83Z')+line('M64 18 V33 M64 96 V110'),'rotate(20 64 64)')+line('M21 43 L30 51 M97 34 L106 25',12))

glyph('System','Warning','주의·거부',line('M61 22 Q64 16 68 23 L111 99 Q114 107 104 107 H24 Q14 107 19 98Z',11)+line('M64 49 V70',12)+circle(64,91,6))
glyph('System','Confirm','확인·장착',line('M22 65 L50 93 L107 34',16))
glyph('System','Cancel','닫기·불가',line('M30 30 L98 98 M98 30 L30 98',16))
glyph('System','Lock','잠김',line('M39 55 V40 A25 25 0 0 1 89 40 V55')+path('M30 53 H98 Q108 53 108 63 V102 Q108 112 98 112 H30 Q20 112 20 102 V63 Q20 53 30 53Z M55 73 V95 H73 V73Z'))
glyph('System','Chevron','접기·펼치기',line('M27 46 L64 83 L101 46',16))
mouse=rect(31,18,66,92,31,'none','white',12)+line('M31 62 H97 M64 20 V62',12)
glyph('System','MouseLeft','좌클릭',mouse+path('M58 24 Q37 24 37 54 H58Z'))
glyph('System','MouseRight','우클릭',mouse+path('M70 24 Q91 24 91 54 H70Z'))
glyph('System','Drag','끌어놓기',rect(18,60,50,50,5,'none','white',12)+line('M61 67 L106 22 M79 22 H106 V49',12))

glyph('Equipment','Head','머리',path('M40 110 V88 C18 77 18 47 33 29 C49 9 84 15 91 38 L93 52 L110 66 L94 74 V88 Q94 96 83 96 H71 V110Z'))
glyph('Equipment','Body','상체',path('M45 20 Q64 43 83 20 L110 40 L99 66 L85 59 V108 H43 V59 L29 66 L18 40Z'))
glyph('Equipment','Legs','하체',path('M32 18 H96 L106 109 H76 L64 63 L52 109 H22Z'))
glyph('Equipment','Feet','신발',boot())

# A black knockout is encoded as an actual hole, never black pixels in the glyph.
for g in glyphs:
    if g['key']=='DetachedCar':
        g['body']=path('M23 28 H77 Q84 28 84 35 V75 Q84 82 77 82 H23 Q16 82 16 75 V35 Q16 28 23 28Z M30 41 V59 H69 V41Z')+circle(31,101,10)+circle(69,101,10)+line('M84 76 H95 M110 62 L101 48 M111 88 L102 103',11)

components=[]
def comp(key,size,border,body):components.append({'key':key,'size':size,'border':border,'body':body})
comp('PanelBody',(128,128),(16,16,16,16),rect(0,0,128,128,12))
comp('PanelHeader',(256,64),(16,8,16,8),rect(0,0,256,64,6,fill='#ededed')+path('M7 0 H249 Q256 0 256 7 V10 H0 V7 Q0 0 7 0Z',fill='#fafafa')+path('M0 58 H256 V58 Q256 64 250 64 H6 Q0 64 0 58Z',fill='#dcdcdc'))
comp('SlotBase',(128,128),(16,16,16,16),path('M8 0 H120 L128 8 V120 L120 128 H8 L0 120 V8Z'))
comp('SlotOutline',(128,128),(16,16,16,16),path('M9 2 H119 L126 9 V119 L119 126 H9 L2 119 V9Z','none','white',4))
comp('FocusCorners',(128,128),(20,20,20,20),line('M18 3 H3 V18 M110 3 H125 V18 M3 110 V125 H18 M110 125 H125 V110',6))
comp('ControlFace',(192,64),(12,12,12,12),rect(0,0,192,64,9,fill='#ededed')+path('M9 0 H183 Q192 0 192 9 V10 H0 V9 Q0 0 9 0Z',fill='#fafafa')+path('M0 56 H192 Q192 64 183 64 H9 Q0 64 0 56Z',fill='#dcdcdc'))
comp('GaugeTrack',(128,32),(12,8,12,8),rect(0,0,128,32,8))
comp('GaugeFill',(64,16),(6,4,6,4),rect(0,0,64,16,4))
comp('Keycap',(64,64),(12,12,12,12),rect(0,0,64,64,9,fill='#dcdcdc')+rect(3,3,58,53,7,fill='white'))
comp('ProgressRing',(128,128),(0,0,0,0),circle(64,64,49,'none','white',12))
comp('BadgeDisc',(64,64),(0,0,0,0),circle(32,32,31))
comp('Divider',(128,8),(4,0,4,0),rect(0,3,128,2,1))

def render_svg(body,size,target,monochrome=False):
    w,h=size
    svg=f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}pt" height="{h}pt" viewBox="0 0 {w} {h}">{body}</svg>'
    target.write_text(svg,encoding='utf-8')
    with fitz.open(stream=svg.encode(),filetype='svg') as src:
        pdf=src.convert_to_pdf()
    with fitz.open(stream=pdf,filetype='pdf') as doc:
        pix=doc[0].get_pixmap(matrix=fitz.Matrix(4,4),alpha=True)
        im=Image.open(BytesIO(pix.tobytes('png'))).convert('RGBA').resize(size,Image.Resampling.LANCZOS)
    if monochrome:
        alpha=im.getchannel('A');im=Image.new('RGBA',size,'white');im.putalpha(alpha)
    return im

def main():
    inventory=[]
    for g in glyphs:
        prefix={'Status':'Status','Actions':'Action','System':'System','Equipment':'Equip'}[g['category']]
        name=f"G_{prefix}_{g['key']}"
        body=re.sub(r'stroke-width="(\d+)"',lambda m:'stroke-width="'+str(max(14,int(m[1])))+'"',g['body'])
        im=render_svg(body,(128,128),VEC/(name+'.svg'),True)
        vb=im.getchannel('A').point(lambda a:255 if a>=16 else 0).getbbox()
        scale=min(1,102/max(vb[2]-vb[0],vb[3]-vb[1]))
        dx=64-scale*(vb[0]+vb[2])/2;dy=64-scale*(vb[1]+vb[3])/2
        body=group(body,f'translate({dx:.5f} {dy:.5f}) scale({scale:.5f})')
        im=render_svg(body,(128,128),VEC/(name+'.svg'),True)
        vb=im.getchannel('A').point(lambda a:255 if a>=16 else 0).getbbox()
        assert vb and min(vb[0],vb[1],128-vb[2],128-vb[3])>=12,(name,vb)
        target=ART/'Glyphs'/g['category']/(name+'.png')
        # Until finalized, previews are kept outside Assets to allow visual refinement.
        dest=OUT/'vector-png'/'Glyphs'/g['category'];dest.mkdir(parents=True,exist_ok=True)
        im.save(dest/(name+'.png'))
        inventory.append({'name':name,'key':g['key'],'label':g['label'],'category':g['category'],'size':[128,128],'border':[0,0,0,0],'vector':(VEC/(name+'.svg')).relative_to(ROOT).as_posix(),'preview':(dest/(name+'.png')).relative_to(ROOT).as_posix(),'saved_asset':target.relative_to(ROOT).as_posix(),'bounds':vb})
    for c in components:
        name='U_'+c['key'];im=render_svg(c['body'],c['size'],VEC/(name+'.svg'))
        dest=OUT/'vector-png'/'Components';dest.mkdir(parents=True,exist_ok=True);im.save(dest/(name+'.png'))
        inventory.append({'name':name,'key':c['key'],'label':c['key'],'category':'Components','size':c['size'],'border':c['border'],'vector':(VEC/(name+'.svg')).relative_to(ROOT).as_posix(),'preview':(dest/(name+'.png')).relative_to(ROOT).as_posix(),'saved_asset':(ART/'Components'/(name+'.png')).relative_to(ROOT).as_posix()})
    assert len(glyphs)==36 and len(components)==12
    (OUT/'vector-manifest.json').write_text(json.dumps({'mode':'authored SVG geometry rendered to RGBA PNG; no generated raster substitutes','assets':inventory},ensure_ascii=False,indent=2),encoding='utf-8')
    canvas=Image.new('RGB',(1320,1220),'#141616');d=ImageDraw.Draw(canvas)
    d.text((24,20),'상태·행동·시스템·장비 기호 36종',font=font(28,True),fill='#F2EAE0')
    d.text((24,62),'흰색 원본 · 표시색 적용 가능 · 큰 그림 64px / 실제 표시 24px / 32px',font=font(17),fill='#C89B4A')
    for i,g in enumerate(inventory[:36]):
        x,y=24+(i%6)*216,108+(i//6)*180
        d.rounded_rectangle((x,y,x+200,y+164),8,fill='#211D1B',outline='#4A423C')
        pic=Image.open(ROOT/g['preview'])
        for size,xx,yy in [(64,x+12,y+13),(24,x+106,y+28),(32,x+148,y+24)]:
            p=pic.resize((size,size),Image.Resampling.LANCZOS);canvas.paste(p,(xx,yy),p)
        d.text((x+12,y+88),g['label'],font=font(17,True),fill='#F2EAE0')
        d.text((x+12,y+123),g['name'].replace('G_',''),font=font(12),fill='#9A9089')
    canvas.save(OUT/'Glyphs-36.png')
    print('Created 36 glyph SVG+PNG previews and 12 component SVG+PNG previews')

if __name__=='__main__':main()
