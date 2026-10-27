from PIL import Image, ImageDraw, ImageFont
from pathlib import Path
R=Path(r'C:\Users\iam12\Train Survival\docs\design\references\ingame-ui')
im=Image.new('RGB',(1600,1310),'#131515')
d=ImageDraw.Draw(im)
C={'panel':'#1F1B1A','line':'#4A423C','text':'#F2EAE0','muted':'#9A9089','gold':'#C89B4A','safe':'#9FC46E','danger':'#F0705F'}
def font(n,b=False): return ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf' if b else 'C:/Windows/Fonts/malgun.ttf',n)
def txt(x,y,s,n=18,c='text',b=False): d.text((x,y),s,font=font(n,b),fill=C.get(c,c))
def box(x,y,w,h,fill='panel',outline='line',radius=10,width=1):
    d.rounded_rectangle((x,y,x+w,y+h),radius=radius,fill=C.get(fill,fill),outline=C.get(outline,outline),width=width)
def line(x1,y1,x2,y2,c='line',w=1): d.line((x1,y1,x2,y2),fill=C.get(c,c),width=w)
def ref(key,x,y,w,h=None):
    p=Image.open(R/f'Ref_{key}.png').convert('RGBA')
    bb=p.getbbox()
    if bb:p=p.crop(bb)
    p.thumbnail((w,h or w),Image.Resampling.LANCZOS)
    im.paste(p,(int(x+(w-p.width)/2),int(y+((h or w)-p.height)/2)),p)
def focus(x,y,s):
    for a,b,dx,dy in [(x,y,1,1),(x+s,y,-1,1),(x,y+s,1,-1),(x+s,y+s,-1,-1)]:
        line(a,b,a+dx*13,b,'gold',3);line(a,b,a,b+dy*13,'gold',3)
def slot(x,y,key=None,s=64,state='normal',count=None):
    edge={'hover':'#F2EAE0','valid':'#9FC46E','invalid':'#F0705F'}.get(state,'#4A423C')
    box(x,y,s,s,fill='#24201E',outline=edge,radius=6,width=2 if state in ('hover','valid','invalid') else 1)
    if key:ref(key,x+8,y+8,s-16)
    if state=='selected':focus(x,y,s)
    if count is not None:
        t=str(count);tw=d.textlength(t,font=font(14,True))
        box(x+s-tw-10,y+s-22,tw+8,21,fill='#1F1B1A',outline='#1F1B1A',radius=3)
        txt(x+s-tw-6,y+s-22,t,14,b=True)
    if state=='invalid':txt(x+s-21,y+1,'×',20,'danger',True)
    if state=='valid':
        line(x+s-22,y+12,x+s-16,y+18,'safe',3)
        line(x+s-16,y+18,x+s-7,y+6,'safe',3)
def tag(x,y,t,w=42):
    box(x,y,w,28,fill='#38322C',outline='#6B6660',radius=5)
    tw=d.textlength(t,font=font(15,True));txt(x+(w-tw)/2,y+2,t,15,b=True)
def gauge(x,y,w,value,col):
    box(x,y,w,16,fill='#111111',outline='line',radius=5)
    if value>0:d.rounded_rectangle((x+2,y+2,x+2+(w-4)*value,y+14),radius=3,fill=col)
def button(x,y,w,t,selected=False):
    box(x,y,w,40,fill='#38322C' if not selected else '#C89B4A',radius=6)
    tw=d.textlength(t,font=font(18,True));txt(x+(w-tw)/2,y+6,t,18,'#1F1B1A' if selected else 'text',True)

txt(48,32,'TRAIN SURVIVAL',17,'gold',True)
txt(48,64,'인게임 UI 구성 확정 시트',38,b=True)
txt(48,118,'공통 원본 12종 → 조합 컴포넌트 12종  ·  아이템 40종  ·  기호 36종',19,'muted')
txt(1210,47,'2026.10.27  /  v1.2',18,'muted')

box(48,170,1504,240)
txt(72,190,'01  같은 슬롯에 상태를 조합한다',22,b=True)
states=[('empty','빈 슬롯',None),('normal','기본','Wood'),('hover','가리킴','Wood'),('selected','선택','Wood'),('valid','놓기 가능','Wood'),('invalid','놓기 불가','Wood')]
for i,(state,label,key) in enumerate(states):
    x=76+i*124;slot(x,244,key,80,state,12 if key else None);txt(x,338,label,16,'muted')
txt(880,240,'선택',18,'gold',True);txt(970,240,'황동 모서리 · 크기 고정',18)
txt(880,277,'거부',18,'danger',True);txt(970,277,'위험 테두리 + X + 사유',18)
txt(880,314,'분리',18,'muted',True);txt(970,314,'그림 / 수량 / 입력 키 / 상태',18)
txt(880,360,'상태마다 아이콘을 다시 그리지 않는다.',17,'muted')

# Inventory at the specified 392px panel width.
box(48,440,392,616)
txt(68,460,'02  가방과 핫바',22,b=True)
txt(68,502,'가방  ·  64px 슬롯 / 8px 간격',16,'muted')
keys=['Wood','Stone','Scrap','Niter','RawFood','Timber','OreVein','Rice','Salt','Ice']
for i,k in enumerate(keys):slot(68+(i%5)*72,544+(i//5)*72,k,64,'selected' if i==0 else 'normal',12-i)
line(68,702,420,702)
txt(68,722,'장비  ·  머리 / 상체 / 하체 / 신발',16,'muted')
for i,label in enumerate(['머리','상체','하체','신발']):
    slot(68+i*88,762,None);txt(78+i*88,835,label,15,'muted')
txt(68,885,'아이템 원화에는 숫자와 키를 넣지 않는다.',16,'muted')
txt(68,914,'이 화면의 물건은 실제 모델 참고 렌더다.',16,'muted')
txt(68,958,'최종 아이콘',18,'gold',True)
txt(68,987,'투명 PNG 512² · 표시 32 / 48 / 56px',16)

box(472,440,592,616)
txt(496,460,'03  제작과 진행',22,b=True)
box(496,511,544,130,fill='#292420',radius=6)
ref('Shotgun',512,528,66,66)
txt(596,522,'샷건 제작',22,b=True)
txt(596,560,'고철 4 · 목재 2',18,'muted')
button(920,548,96,'제작',True)
txt(596,600,'결과 아이콘 + 재료 + 행동 버튼',15,'muted')
txt(496,664,'재료 부족은 숨기지 않고 이유를 표시',17,'muted')
box(496,702,544,72,fill='#292420',radius=6)
txt(516,721,'△',23,'danger',True)
txt(554,714,'재료 부족',18,'danger',True)
txt(554,744,'부족한 수량은 현재 인벤토리에서 계산',15,'muted')
line(496,800,1040,800)
txt(496,822,'게이지는 공통 트랙과 채움을 재사용',18,b=True)
for i,(v,col,label) in enumerate([(0,'#7FA653','0%'),(.5,'#E3B23C','50%'),(1,'#B23A2E','100%')]):
    y=869+i*49;gauge(496,y,240,v,col);txt(758,y-5,label,17,'muted')
txt(496,1010,'채움 비율 예시 · 상태색은 별도로 판정',17,'muted')

box(1096,440,456,616)
txt(1120,460,'04  대상과 사건 안내',22,b=True)
txt(1120,507,'상호작용은 대상 가까이에',17,'muted')
box(1120,547,408,100,fill='#292420',radius=7)
txt(1140,562,'기관차 화구',20,b=True)
tag(1140,601,'E');txt(1196,603,'연료 투입',18)
txt(1120,676,'획득 토스트',17,'muted')
box(1120,710,408,58,fill='#292420',radius=7)
ref('Wood',1134,718,40);txt(1190,722,'목재 +3',21,b=True)
txt(1120,799,'사건 배너  ·  동시에 최대 2개',17,'muted')
box(1120,835,408,64,fill='#292420',outline='#F0705F',radius=7)
txt(1140,853,'△  이탈한 칸을 회수하세요',19,'danger',True)
txt(1120,933,'대상명 / 키 / 결과는 별도 텍스트',17,'muted')
txt(1120,967,'기호 원본은 흰색 마스크',17,'muted')
txt(1120,1001,'패널 바탕의 최종 불투명도 88%',17,'muted')

box(48,1086,1504,166)
txt(72,1105,'05  플레이 화면 배치',21,b=True)
txt(72,1154,'좌하단',16,'muted');txt(72,1184,'체력 · 임계 상태',19)
txt(510,1154,'하단 중앙',16,'muted');txt(510,1184,'손과 무기 공간 확보',19)
for i,k in enumerate(['Harpoon','Revolver','Hammer','Wood','Bundle']):
    x=1120+i*80;slot(x,1150,k,72,'selected' if i==0 else 'normal',12 if k=='Wood' else None)
    txt(x+5,1152,str(i+1),14,b=True)
txt(1120,1108,'우하단 핫바 5칸 / 392px',17,'gold',True)
txt(48,1274,'구성 검토용 · 실제 모델을 임시 배치한 도식이며 최종 아이콘과 게임 적용 화면은 아님 · 수량과 상태는 예시',15,'muted')
im.save(R/'UIComponentBoard.png')
print(R/'UIComponentBoard.png')
