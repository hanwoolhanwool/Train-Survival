from pathlib import Path
import json,re,shutil
import numpy as np
from PIL import Image
root=Path(__file__).resolve().parents[1]
out=root/'output/imagegen/world-structures-v1'
(out/'originals').mkdir(exist_ok=True)
(out/'reviews').mkdir(exist_ok=True)
results=[]
for key in ['Ground_Arctic','Seabed','Ground_Arctic-seam','Seabed-seam']:
 rec=json.loads((out/'records'/f'{key}.json').read_text(encoding='utf-8'))
 source=Path(re.search(r' as (C:\\[^\n]+?\.png) by default',rec['resultHint'])[1])
 shutil.copy2(source,out/'originals'/f'{key}.png')
 im=Image.open(source).convert('RGB').resize((1024,1024),Image.Resampling.LANCZOS)
 a=np.asarray(im).astype(float)
 dx=np.abs(a[:,1:]-a[:,:-1]).mean(axis=(0,2));dy=np.abs(a[1:]-a[:-1]).mean(axis=(1,2))
 edge_x=float(np.abs(a[:,0]-a[:,-1]).mean());edge_y=float(np.abs(a[0]-a[-1]).mean())
 stats={'key':key,'source':str(source),'source_size':Image.open(source).size,'edge_x_mean':edge_x,'edge_y_mean':edge_y,'interior_dx_p95':float(np.percentile(dx,95)),'interior_dy_p95':float(np.percentile(dy,95)),'mean_rgb':a.mean(axis=(0,1)).tolist()}
 results.append(stats)
 # QA layouts only: no creative pixel editing of the generated source.
 tiled=Image.new('RGB',(2048,2048))
 for x in [0,1024]:
  for y in [0,1024]:tiled.paste(im,(x,y))
 tiled.save(out/'reviews'/f'{key}-2x2.png')
 offset=Image.fromarray(np.roll(np.roll(np.asarray(im),512,0),512,1))
 offset.save(out/'reviews'/f'{key}-offset.png')
(out/'texture-seam-check.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
print(json.dumps(results,indent=2))
