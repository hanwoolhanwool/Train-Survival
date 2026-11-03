import json,numpy as np
from collections import defaultdict
j=json.load(open('output/raccoon-skin-v59/source.json'));v=np.array(j['vertices']);t=np.array(j['triangles'][1]).reshape(-1,3)
p=list(range(len(v)))
def find(a):
 while p[a]!=a:p[a]=p[p[a]];a=p[a]
 return a
def join(a,b):p[find(a)]=find(b)
seen={}
for i in np.unique(t):
 key=tuple(np.round(v[i],5))
 if key in seen:join(int(i),seen[key])
 else:seen[key]=int(i)
for a,b,c in t:join(a,b);join(a,c)
g=defaultdict(list)
for i in np.unique(t):g[find(i)].append(int(i))
gs=sorted(g.values(),key=len,reverse=True)
json.dump(gs,open('output/raccoon-skin-v59/components.json','w'))
for n,ids in enumerate(gs[:45]):
 pts=v[ids];weight=defaultdict(float)
 for i in ids:
  for b,w in zip(j['weights'][i]['ids'],j['weights'][i]['weights']):weight[j['bones'][b]['name']]+=w
 print(n,len(ids),'min',np.round(pts.min(0),3),'max',np.round(pts.max(0),3),'weights',[(b,round(w/len(ids),2)) for b,w in sorted(weight.items(),key=lambda x:-x[1])[:5]])
print('BONES')
for i,b in enumerate(j['bones']):print(i,b['name'],np.round(b['pos'],3))
