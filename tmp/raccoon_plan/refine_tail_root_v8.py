import bpy,json,math
from mathutils import Vector
scene=bpy.data.scenes['Scene'];bpy.context.window.scene=scene;m=bpy.data.objects['raccoon_HybridAuthor_v8'];r=bpy.data.objects['RIG_Raccoon_HybridAuthor_v8']
ids=[v.index for v in m.data.vertices if any(m.vertex_groups[g.group].name=='SEL_Tail' and g.weight>.5 for g in v.groups)];idset=set(ids)
m.data.calc_loop_triangles();tri=[tuple(t.vertices) for t in m.data.loop_triangles if all(i in idset for i in t.vertices)];edges={}
for t in tri:
 for a,b in zip(t,(t[1],t[2],t[0])):edges.setdefault(tuple(sorted((a,b))),[]).append((a,b))
boundary=[v[0] for v in edges.values() if len(v)==1]
def volume(co):
 c=sum((co[i] for e in boundary for i in e),Vector())/(2*len(boundary));return abs((sum(co[a].dot(co[b].cross(co[d])) for a,b,d in tri)+sum(co[b].dot(co[a].cross(c)) for a,b in boundary))/6)
rest=volume([v.co for v in m.data.vertices]);knots=[-.15,-.32,-.51,-.70,-.84]
deform=[g for g in m.vertex_groups if g.name in r.data.bones and r.data.bones[g.name].use_deform]
def smooth(t):t=max(0,min(1,t));return t*t*(3-2*t)
def apply(length,k):
 for i in ids:
  v=m.data.vertices[i].co;hip=1-smooth((-v.z-.08+k*(v.y-.05))/length)
  for g in deform:g.remove([i])
  if v.z>=knots[0]:weights={'Tail01':1}
  elif v.z<=knots[-1]:weights={'Tail05':1}
  else:
   j=next(j for j in range(4) if knots[j]>=v.z>=knots[j+1]);t=smooth((knots[j]-v.z)/(knots[j]-knots[j+1]));weights={'Tail%02d'%(j+1):1-t,'Tail%02d'%(j+2):t}
  weights={n:w*(1-hip) for n,w in weights.items()};weights['Hips']=hip
  for n,w in weights.items():
   if w>1e-7:m.vertex_groups[n].add([i],w,'REPLACE')
results=[]
for length in [.20,.30,.40,.50]:
 for k in [0,.2,.4,.6]:
  apply(length,k);mn=1;vmin=2;vmax=0
  for f in [1,20,39,57]:
   scene.frame_set(f);bpy.context.view_layer.update();ev=m.evaluated_get(bpy.context.evaluated_depsgraph_get());geo=ev.to_mesh();co=[v.co.copy() for v in geo.vertices]
   mn=min(mn,min((co[a]-co[b]).length/(m.data.vertices[a].co-m.data.vertices[b].co).length for a,b in edges if (m.data.vertices[a].co-m.data.vertices[b].co).length>.005))
   ratio=volume(co)/rest;vmin=min(vmin,ratio);vmax=max(vmax,ratio);ev.to_mesh_clear()
  results.append({'length':length,'k':k,'minimum_edge':mn,'min_volume':vmin,'max_volume':vmax,'score':mn-2*max(abs(1-vmin),abs(1-vmax))})
best=max(results,key=lambda v:v['score']);apply(best['length'],best['k']);scene.frame_set(1)
p='C:/Users/iam12/Train Survival/output/raccoon-run-v8/'
vertices=[{'position':dict(zip(['x','y','z'],m.data.vertices[i].co)),'influences':[{'name':m.vertex_groups[w.group].name,'weight':w.weight} for w in m.data.vertices[i].groups if m.vertex_groups[w.group].name in r.data.bones and r.data.bones[m.vertex_groups[w.group].name].use_deform and w.weight>1e-7]} for i in ids]
json.dump({'vertices':vertices},open(p+'unity_tail_weights.json','w'));json.dump({'selected':best,'candidates':results},open(p+'tail_root_fit.json','w'),indent=2)
print('ROOT FIT',best)
