import json, math
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
root=Path(r'C:\Users\iam12\Train Survival\docs\design\references\ingame-ui')
records=json.loads((root/'references.json').read_text(encoding='utf-8'))
sheet=Image.new('RGB',(1536,math.ceil(len(records)/4)*420),(48,47,46))
draw=ImageDraw.Draw(sheet)
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',22)
for i,row in enumerate(records):
    tile=Image.open(root/row['preview']).convert('RGBA')
    x,y=(i%4)*384,(i//4)*420
    sheet.paste(tile,(x,y),tile)
    label='Harpoon (assembled)' if row['key']=='Harpoon' else row['key']
    draw.text((x+16,y+386),label,fill='white',font=font)
sheet.save(root/'ReferenceSheet.png')
assert len(records)==17
assert len([r for r in records if r['key']=='Harpoon'])==1
assert not any(r['key']=='HarpoonClaw' for r in records)
assert len(next(r for r in records if r['key']=='Harpoon')['component_models'])==2
print('17 reference objects; Harpoon contains both source models.')
