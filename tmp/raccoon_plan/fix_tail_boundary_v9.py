import bpy,json,heapq,math,os
from mathutils import Matrix
OUT='C:/Users/iam12/Train Survival/output/raccoon-tail-v9';os.makedirs(OUT,exist_ok=True)
scene=bpy.context.scene;src=bpy.data.objects['COMPARE_v8_new_Raccoon_측면'];rig=bpy.data.objects['COMPARE_v8_new_Rig_측면']
mesh=src.copy();mesh.data=src.data.copy();mesh.name='Raccoon_TailBoundary_v9';scene.collection.objects.link(mesh)
sel=mesh.vertex_groups['SEL_Tail'].index;ids={v.index for v in mesh.data.vertices if any(w.group==sel and w.weight>.5 for w in v.groups)}
adj={i:[] for i in ids};boundary=set();cross=[]
for edge in mesh.data.edges:
 a,b=edge.vertices;length=(mesh.data.vertices[a].co-mesh.data.vertices[b].co).length
 if a in ids and b in ids:adj[a].append((b,length));adj[b].append((a,length))
 elif (a in ids)!=(b in ids):boundary.add(a if a in ids else b);cross.append((a,b,length))
distance={i:0 if i in boundary else float('inf') for i in ids};queue=[(0,i) for i in boundary];heapq.heapify(queue)
while queue:
 d,i=heapq.heappop(queue)
 if d>distance[i]:continue
 for j,length in adj[i]:
  if d+length<distance[j]:distance[j]=d+length;heapq.heappush(queue,(d+length,j))
deform={g.index:g.name for g in mesh.vertex_groups if g.name in rig.data.bones and rig.data.bones[g.name].use_deform}
old={i:{deform[w.group]:w.weight for w in mesh.data.vertices[i].groups if w.group in deform and w.weight>1e-7} for i in ids}
anchors={}
for i in boundary:
 neighbors=[(b if a==i else a,length) for a,b,length in cross if a==i or b==i];acc={};total=0
 for j,length in neighbors:
  factor=1/max(length,.002);total+=factor
  for w in mesh.data.vertices[j].groups:
   if w.group in deform:acc[deform[w.group]]=acc.get(deform[w.group],0)+w.weight*factor
 anchors[i]={n:w/total for n,w in acc.items()}
base={}
for i in ids:
 if i in anchors:base[i]=anchors[i];continue
 near=sorted(boundary,key=lambda j:(mesh.data.vertices[i].co-mesh.data.vertices[j].co).length_squared)[:3];acc={};total=0
 for j in near:
  factor=1/max((mesh.data.vertices[i].co-mesh.data.vertices[j].co).length_squared,.0001);total+=factor
  for n,w in anchors[j].items():acc[n]=acc.get(n,0)+w*factor
 base[i]={n:w/total for n,w in acc.items()}
def smooth(t):t=max(0,min(1,t));return t*t*(3-2*t)
def apply(width):
 for i in ids:
  blend=smooth(distance[i]/width);weights={n:w*blend for n,w in old[i].items()}
  for n,w in base[i].items():weights[n]=weights.get(n,0)+w*(1-blend)
  weights=dict(sorted(weights.items(),key=lambda v:-v[1])[:4]);total=sum(weights.values());weights={n:w/total for n,w in weights.items()}
  for n in deform.values():mesh.vertex_groups[n].remove([i])
  for n,w in weights.items():
   if w>1e-7:mesh.vertex_groups[n].add([i],w,'REPLACE')
def strain(ob):
 values=[]
 for frame in [1,10,20,30,40,50,60,70]:
  scene.frame_set(frame);bpy.context.view_layer.update();ev=ob.evaluated_get(bpy.context.evaluated_depsgraph_get());geo=ev.to_mesh()
  values.extend((geo.vertices[a].co-geo.vertices[b].co).length/length for a,b,length in cross if length>.002);ev.to_mesh_clear()
 return min(values),max(values)
before=strain(src);results=[]
for width in [.16,.22,.28]:
 apply(width);results.append({'blend_width':width,'boundary_edge_ratio':strain(mesh)})
apply(.22)
# Imported split normals emphasize stretched triangles. Recompute only the attachment's corner normals.
root={i for i in ids if distance[i]<.22};normals=[tuple(n.vector) for n in mesh.data.corner_normals]
for loop in mesh.data.loops:
 if loop.vertex_index in root:normals[loop.index]=(0,0,0)
mesh.data.normals_split_custom_set(normals)
vertices=[{'position':dict(zip(['x','y','z'],mesh.data.vertices[i].co)),'influences':[{'name':deform[w.group],'weight':w.weight} for w in mesh.data.vertices[i].groups if w.group in deform and w.weight>1e-7]} for i in ids]
json.dump({'vertices':vertices},open(OUT+'/unity_tail_weights.json','w'))
report={'boundary_vertices':len(boundary),'tail_vertices':len(ids),'before_boundary_edge_ratio':before,'after_boundary_edge_ratio':strain(mesh),'blend_width':.22,'candidates':results}
json.dump(report,open(OUT+'/tail_attachment_validation.json','w'),indent=2)
scene.frame_set(20);src.hide_set(True);mesh.hide_set(False);print(report)
