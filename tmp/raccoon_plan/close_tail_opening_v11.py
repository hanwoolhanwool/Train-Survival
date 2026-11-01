import bpy,bmesh,json,pathlib
from mathutils import Vector,Matrix
project=pathlib.Path('C:/Users/iam12/Train Survival');body=bpy.data.objects['Raccoon_RoundTail_Body_v11'];base=bpy.data.objects['Raccoon_RoundTail_Body_v10']
# Retain the garment silhouette. Add an inset around the smaller opening instead of pulling the garment inward.
body.data=base.data.copy()
bm=bmesh.new();bm.from_mesh(body.data);bm.verts.ensure_lookup_table();uv=bm.loops.layers.uv.active
src=bpy.data.objects['COMPARE_v9_new_Raccoon_측면'];sel=src.vertex_groups['SEL_Tail'].index
ids={v.index for v in src.data.vertices if any(g.group==sel and g.weight>.5 for g in v.groups)}
outer={i for p in src.data.polygons if any(i in ids for i in p.vertices) for i in p.vertices if i not in ids}
edges=[e for e in bm.edges if e.is_boundary and all(v.index in outer for v in e.verts)]
verts=[];faces=[];coords=[];weights=[]
for e in edges:
 f=e.link_faces[0];a,b=e.verts;lookup={l.vert:l[uv].uv.copy() for l in f.loops};mid=(lookup[a]+lookup[b])*.5;inside=sum((l[uv].uv for l in f.loops),Vector((0,0)))/len(f.loops);shift=(inside-mid).normalized()*.003
 corners=[]
 for v,is_inner in [(a,False),(b,False),(b,True),(a,True)]:
  p=v.co.copy()
  if is_inner:p.x*=.50;p.z=-.195+(p.z+.195)*.50;p.y=-.047
  corners.append(len(verts));verts.append(tuple(p));coords.append(tuple(lookup[v]+(shift if is_inner else Vector((0,0)))))
  w={'Hips':1.0} if is_inner else {body.vertex_groups[g.group].name:g.weight for g in body.data.vertices[v.index].groups if body.vertex_groups[g.group].name in body.parent.data.bones and body.parent.data.bones[body.vertex_groups[g.group].name].use_deform}
  total=sum(w.values());weights.append({n:x/total for n,x in w.items()})
 if (Vector(verts[corners[1]])-Vector(verts[corners[0]])).cross(Vector(verts[corners[2]])-Vector(verts[corners[0]])).y<0:corners.reverse()
 faces.append(corners)
bm.free();geo=bpy.data.meshes.new('TailOpeningInset_v11');geo.from_pydata(verts,[],faces);geo.update();layer=geo.uv_layers.new(name='UVMap')
for p in geo.polygons:
 p.use_smooth=True
 for li in p.loop_indices:layer.data[li].uv=coords[geo.loops[li].vertex_index]
ob=bpy.data.objects.new('Raccoon_TailOpening_v11',geo);bpy.context.scene.collection.objects.link(ob);ob.parent=body.parent;ob.matrix_basis=Matrix.Identity(4);ob.matrix_parent_inverse=Matrix.Identity(4)
geo.materials.append(body.data.materials[0]);mod=ob.modifiers.new('Opening Skin','ARMATURE');mod.object=body.parent
for n in set(n for w in weights for n in w):ob.vertex_groups.new(name=n)
for i,w in enumerate(weights):
 for n,value in w.items():ob.vertex_groups[n].add([i],value,'REPLACE')
geo.calc_loop_triangles();records=[];triangles=[]
for tri in geo.loop_triangles:
 for li in tri.loops:
  i=geo.loops[li].vertex_index;records.append({'position':dict(zip(['x','y','z'],geo.vertices[i].co)),'normal':dict(zip(['x','y','z'],geo.corner_normals[li].vector)),'uv':dict(zip(['x','y'],layer.data[li].uv)),'influences':[{'name':n,'weight':value} for n,value in weights[i].items()]});triangles.append(len(records)-1)
path=project/'output/raccoon-tail-v11/tail_geometry.json';data=json.loads(path.read_text());data.pop('bodyPatch',None);data['opening']={'vertices':records,'triangles':triangles};path.write_text(json.dumps(data))
print('Opening inset',len(edges),'segments',len(records),'corners; original garment vertices unchanged')
