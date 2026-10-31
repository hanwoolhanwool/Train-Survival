import json, heapq
import numpy as np
from pathlib import Path
p=Path('C:/Users/iam12/Desktop/Train_Survival_Blender/Raccoon_Production')
d=json.loads((p/'source_geometry.json').read_text());v=np.array(d['verts']);f=np.array(d['faces'])
adj=[{} for _ in v]
for face in f:
 for a,b in zip(face,np.roll(face,1)):
  w=float(np.linalg.norm(v[a]-v[b]));adj[a][b]=w;adj[b][a]=w
seeds=np.where((v[:,0]>.55)&(v[:,2]>-.16)&(v[:,2]<-.13))[0]
ds=np.full(len(v),np.inf);q=[]
for s in seeds:ds[s]=0;heapq.heappush(q,(0,s))
while q:
 cost,k=heapq.heappop(q)
 if cost>ds[k]:continue
 for j,w in adj[k].items():
  if cost+w<ds[j]:ds[j]=cost+w;heapq.heappush(q,(ds[j],j))
print('seeds',v[seeds])
for t in [.08,.10,.12,.14,.16,.18,.20,.22,.24]:
 remaining=set(np.where((v[:,0]>.54)&(v[:,2]<-.13)&(ds>t))[0]);groups=[]
 while remaining:
  seed=remaining.pop();g=[seed];queue=[seed]
  while queue:
   k=queue.pop()
   for j in adj[k]:
    if j in remaining:remaining.remove(j);queue.append(j);g.append(j)
  groups.append(g)
 print('threshold',t,[(len(ids),np.round(v[ids].mean(0),4).tolist(),round(ds[ids].max(),3)) for ids in groups if len(ids)>2])

import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
fig,axs=plt.subplots(1,3,figsize=(14,6))
mask=(v[:,0]>.53)&(v[:,2]<-.1)
for ax,(x,y) in zip(axs,[(0,2),(1,2),(0,1)]):
 for tri in f:
  if mask[tri].all():
   pts=v[np.r_[tri,tri[0]]];ax.plot(pts[:,x],pts[:,y],color='#999999',linewidth=.5)
 ax.scatter(v[mask,x],v[mask,y],c=ds[mask],s=4,cmap='turbo');ax.set_aspect('equal');ax.set_xlabel('xyz'[x]);ax.set_ylabel('xyz'[y])
fig.tight_layout();fig.savefig(p/'hand_topology_analysis.png',dpi=140)
