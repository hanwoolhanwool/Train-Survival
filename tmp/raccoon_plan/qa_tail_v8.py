import bpy,json,math
from mathutils import Vector
scene=bpy.data.scenes['Scene'];bpy.context.window.scene=scene
mesh=bpy.data.objects['raccoon_HybridAuthor_v8'];rig=bpy.data.objects['RIG_Raccoon_HybridAuthor_v8'];source=bpy.data.objects['raccoon_RunAuthor_v7']
old=source.copy();old.data=source.data.copy();old.name='_TailBeforeQA';scene.collection.objects.link(old);old.parent=rig;old.matrix_parent_inverse=mesh.matrix_parent_inverse.copy();old.matrix_basis=mesh.matrix_basis.copy()
old.data.shape_keys.animation_data_clear()
for mod in old.modifiers:
 if mod.type=='ARMATURE':mod.object=rig
sel=mesh.vertex_groups['SEL_Tail'].index;ids={v.index for v in mesh.data.vertices if any(g.group==sel and g.weight>.5 for g in v.groups)}
mesh.data.calc_loop_triangles();tri=[tuple(t.vertices) for t in mesh.data.loop_triangles if all(i in ids for i in t.vertices)]
edges={}
for t in tri:
 for a,b in zip(t,(t[1],t[2],t[0])):
  key=tuple(sorted((a,b)));edges.setdefault(key,[]).append((a,b))
boundary=[v[0] for v in edges.values() if len(v)==1]
def volume(co):
 center=sum((co[i] for e in boundary for i in e),Vector())/max(1,len(boundary)*2)
 total=sum(co[a].dot(co[b].cross(co[c])) for a,b,c in tri)
 total+=sum(co[b].dot(co[a].cross(center)) for a,b in boundary)
 return abs(total/6)
rest=volume([v.co.copy() for v in mesh.data.vertices]);results=[]
for f in range(1,76,3):
 scene.frame_set(f);bpy.context.view_layer.update();row={'frame':f}
 for ob,label in [(old,'before'),(mesh,'after')]:
  ev=ob.evaluated_get(bpy.context.evaluated_depsgraph_get());geo=ev.to_mesh();co=[v.co.copy() for v in geo.vertices];row[label+'_volume_ratio']=volume(co)/rest
  ratios=[(co[a]-co[b]).length/(mesh.data.vertices[a].co-mesh.data.vertices[b].co).length for a,b in edges if (mesh.data.vertices[a].co-mesh.data.vertices[b].co).length>.005]
  row[label+'_minimum_edge_ratio']=min(ratios);ev.to_mesh_clear()
 results.append(row)
bpy.data.objects.remove(old,do_unlink=True)
report={'method':'Same hybrid pose and same unchanged mesh; compare original vs revised skin weights. Tail surface volume includes a cap over its root boundary.','tail_vertices':len(ids),'tail_triangles':len(tri),'before_min_volume_ratio':min(r['before_volume_ratio'] for r in results),'after_min_volume_ratio':min(r['after_volume_ratio'] for r in results),'before_min_edge_ratio':min(r['before_minimum_edge_ratio'] for r in results),'after_min_edge_ratio':min(r['after_minimum_edge_ratio'] for r in results),'samples':results}
json.dump(report,open('C:/Users/iam12/Train Survival/output/raccoon-run-v8/tail_shape_validation.json','w'),indent=2);print({k:v for k,v in report.items() if k!='samples'});scene.frame_set(1)
