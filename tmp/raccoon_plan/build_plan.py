from pathlib import Path
import re, html, shutil
from reportlab.pdfgen import canvas
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, Image, PageBreak, KeepTogether
from reportlab.lib import colors
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.enums import TA_LEFT, TA_CENTER
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib.pagesizes import A4
from pypdf import PdfReader

ROOT=Path('C:/Users/iam12/Train Survival')
SRC=ROOT/'docs/design/Train-Survival-raccoon-캐릭터-제작-기획서.md'
OUT=ROOT/'output/pdf/Train-Survival-raccoon-캐릭터-제작-기획서.pdf'
ASSETS=ROOT/'docs/design/references/raccoon-analysis'
ASSETS.mkdir(parents=True,exist_ok=True)
OUT.parent.mkdir(parents=True,exist_ok=True)
for name in ('front','back','side','hand_palm','hand_other'):
    shutil.copy2(ROOT/f'tmp/raccoon_plan/{name}.png',ASSETS/f'{name}.png')
pdfmetrics.registerFont(TTFont('Malgun','C:/Windows/Fonts/malgun.ttf'))
pdfmetrics.registerFont(TTFont('MalgunBold','C:/Windows/Fonts/malgunbd.ttf'))
pdfmetrics.registerFontFamily('Malgun',normal='Malgun',bold='MalgunBold',italic='Malgun',boldItalic='MalgunBold')
W,H=A4
WIDTH=W-88
styles={
 'body':ParagraphStyle('body',fontName='Malgun',fontSize=10,leading=15.5,spaceAfter=8,wordWrap='CJK',textColor=colors.HexColor('#202329')),
 'title':ParagraphStyle('title',fontName='MalgunBold',fontSize=23,leading=32,spaceAfter=16,textColor=colors.black,wordWrap='CJK'),
 'h1':ParagraphStyle('h1',fontName='MalgunBold',fontSize=17,leading=24,spaceBefore=2,spaceAfter=13,keepWithNext=True,textColor=colors.black,wordWrap='CJK'),
 'h2':ParagraphStyle('h2',fontName='MalgunBold',fontSize=11.5,leading=18,spaceBefore=9,spaceAfter=7,keepWithNext=True,textColor=colors.black,wordWrap='CJK'),
 'cell':ParagraphStyle('cell',fontName='Malgun',fontSize=9,leading=13.4,wordWrap='CJK',textColor=colors.HexColor('#202329')),
 'th':ParagraphStyle('th',fontName='MalgunBold',fontSize=9,leading=13.4,wordWrap='CJK',textColor=colors.white),
 'caption':ParagraphStyle('caption',fontName='Malgun',fontSize=8.4,leading=12.5,spaceAfter=9,textColor=colors.HexColor('#5A616A'),wordWrap='CJK'),
 'bullet':ParagraphStyle('bullet',fontName='Malgun',fontSize=9.7,leading=15,spaceAfter=6,leftIndent=10,firstLineIndent=-10,wordWrap='CJK'),
}

def inline(s):
    s=html.escape(s)
    s=re.sub(r'`([^`]+)`',r'<font size="9">\1</font>',s)
    s=re.sub(r'\*\*([^*]+)\*\*',r'<b>\1</b>',s)
    def link(m):
        label,url=m.groups()
        if url.startswith('https://'):return f'<link href="{url}" color="#1B4F70">{label}</link>'
        return label
    return re.sub(r'\[([^\]]+)\]\(([^)]+)\)',link,s)

story=[]
lines=SRC.read_text(encoding='utf-8').splitlines()
i=0
section=''
while i<len(lines):
    line=lines[i].strip()
    if not line:i+=1;continue
    if line=='<!-- PAGE -->':story.append(PageBreak());i+=1;continue
    if line.startswith('# '):story.append(Paragraph(inline(line[2:]),styles['title']));i+=1;continue
    if line.startswith('## '):
        section=line[3:];story.append(Paragraph(inline(section),styles['h1']));i+=1;continue
    if line.startswith('### '):story.append(Paragraph(inline(line[4:]),styles['h2']));i+=1;continue
    if line.startswith('!['):
        found=re.findall(r'!\[([^\]]*)\]\(([^)]+)\)',line)
        n=len(found);gap=8;cw=(WIDTH-gap*(n-1))/n
        cells=[]
        for alt,path in found:
            from PIL import Image as PILImage
            p=SRC.parent/path
            with PILImage.open(p) as im:pw,ph=im.size
            iw=cw;ih=iw*ph/pw
            if n==2 and ih>200:ih=200;iw=ih*pw/ph
            cells.append(Image(str(p),width=iw,height=ih))
        rows=[]
        for j,c in enumerate(cells):
            if j:rows.append('')
            rows.append(c)
        widths=[]
        for j in range(n):
            if j:widths.append(gap)
            widths.append(cw)
        t=Table([rows],colWidths=widths)
        t.setStyle(TableStyle([('VALIGN',(0,0),(-1,-1),'TOP'),('ALIGN',(0,0),(-1,-1),'CENTER'),('LEFTPADDING',(0,0),(-1,-1),0),('RIGHTPADDING',(0,0),(-1,-1),0),('TOPPADDING',(0,0),(-1,-1),2),('BOTTOMPADDING',(0,0),(-1,-1),5)]))
        story.append(t);i+=1;continue
    if line.startswith('|'):
        rows=[]
        while i<len(lines) and lines[i].strip().startswith('|'):
            row=[c.strip() for c in lines[i].strip().strip('|').split('|')]
            if not all(re.fullmatch(r'[-: ]+',c) for c in row):rows.append(row)
            i+=1
        n=len(rows[0])
        if n==4:ratios=[.15,.43,.12,.30] if section.startswith('10 ') else [.16,.39,.36,.09]
        elif section.startswith('11 '):ratios=[.08,.37,.55]
        elif section.startswith('2 '):ratios=[.17,.39,.44]
        elif section.startswith('8 '):ratios=[.30,.30,.40]
        elif section.startswith('12 '):ratios=[.22,.37,.41]
        else:ratios=[.20,.38,.42]
        data=[]
        for j,row in enumerate(rows):
            cells=[]
            for k,c in enumerate(row):
                base=styles['th' if j==0 else 'cell']
                centered=(section.startswith('11 ') and k==0) or (section.startswith('6 ') and k==3) or (section.startswith('10 ') and k==2)
                cs=ParagraphStyle('aligned',parent=base,alignment=TA_CENTER) if centered else base
                cells.append(Paragraph(inline(c),cs))
            data.append(cells)
        t=Table(data,colWidths=[WIDTH*x for x in ratios],repeatRows=1,hAlign='LEFT')
        cmds=[('BACKGROUND',(0,0),(-1,0),colors.HexColor('#263A46')),('GRID',(0,0),(-1,-1),.45,colors.HexColor('#D9D9D9')),('VALIGN',(0,0),(-1,-1),'MIDDLE'),('LEFTPADDING',(0,0),(-1,-1),7),('RIGHTPADDING',(0,0),(-1,-1),7),('TOPPADDING',(0,0),(-1,-1),6),('BOTTOMPADDING',(0,0),(-1,-1),6)]
        for j in range(1,len(rows)):
            if j%2==0:cmds.append(('BACKGROUND',(0,j),(-1,j),colors.HexColor('#F2F5F7')))
        t.setStyle(TableStyle(cmds));story.extend([t,Spacer(1,10)]);continue
    if line.startswith('- '):story.append(Paragraph('• '+inline(line[2:]),styles['bullet']));i+=1;continue
    if re.match(r'^\d+\. ',line):story.append(Paragraph(inline(line),styles['bullet']));i+=1;continue
    style='caption' if line.startswith('그림 ') or line.startswith('2026년') else 'body'
    story.append(Paragraph(inline(line),styles[style]));i+=1

def decorate(c,doc):
    c.saveState()
    c.setFont('Malgun',8)
    c.setFillColor(colors.HexColor('#65707A'))
    c.drawString(44,25,'Train Survival   /   raccoon 캐릭터 제작 기획   /   v1.0')
    c.drawRightString(W-44,25,str(doc.page))
    c.restoreState()

doc=SimpleDocTemplate(str(OUT),pagesize=A4,leftMargin=44,rightMargin=44,topMargin=40,bottomMargin=44,title='Train Survival raccoon 플레이어 캐릭터 제작 기획서',author='Train Survival',pageCompression=1)
doc.build(story,onFirstPage=decorate,onLaterPages=decorate)
r=PdfReader(str(OUT))
print('OUTPUT',OUT)
print('PAGES',len(r.pages))
for i,p in enumerate(r.pages,1):
    txt=p.extract_text()
    print(i,len(txt),txt.splitlines()[1:3])
