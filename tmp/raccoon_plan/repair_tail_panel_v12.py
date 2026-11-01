import bpy,bmesh,json,pathlib
from mathutils import Vector
project=pathlib.Path('C:/Users/iam12/Train Survival')
exec(compile((project/'tmp/raccoon_plan/repair_tail_surround_v12.py').read_text(encoding='utf-8'),'repair_tail_surround_v12.py','exec'))
base=bpy.data.objects['Raccoon_RoundTail_Body_v11'];me=base.data;me.calc_loop_triangles()
selected={p.index for p in me.polygons if abs(p.center.x)<.22 and -.335<p.center.z<-.065 and p.center.y>-.13 and all(me.vertices[i].co.z<-.045 and abs(me.vertices[i].co.x)<.24 for i in p.vertices)}
path=project/'output/raccoon-tail-v12/tail_geometry.json';data=json.loads(path.read_text());opening=data['opening'];remove=[]
for tri in me.loop_triangles:
 if tri.polygon_index not in selected:continue
 remove.append({'positions':[dict(zip(['x','y','z'],me.vertices[i].co)) for i in tri.vertices]})
 for li in tri.loops:
  loop=me.loops[li];v=me.vertices[loop.vertex_index];p=v.co
  influences=[{'name':base.vertex_groups[g.group].name,'weight':g.weight} for g in v.groups if base.vertex_groups[g.group].name in base.parent.data.bones and base.parent.data.bones[base.vertex_groups[g.group].name].use_deform]
  opening['vertices'].append({'position':dict(zip(['x','y','z'],p)),'normal':dict(zip(['x','y','z'],me.corner_normals[li].vector)),'uv':{'x':(p.x+.24)/.48,'y':(p.z+.35)/.30},'influences':influences});opening['triangles'].append(len(opening['vertices'])-1)
data['removeFaces']=remove;path.write_text(json.dumps(data))
fixed=me.copy();bm=bmesh.new();bm.from_mesh(fixed);bm.faces.ensure_lookup_table();bmesh.ops.delete(bm,geom=[bm.faces[i] for i in selected],context='FACES_ONLY');bm.to_mesh(fixed);bm.free();fixed.update()
records=opening['vertices'];geometry=bpy.data.meshes.new('TailSurroundPanel_v12');geometry.from_pydata([tuple(r['position'][k] for k in ['x','y','z']) for r in records],[],[opening['triangles'][i:i+3] for i in range(0,len(opening['triangles']),3)]);geometry.update();layer=geometry.uv_layers.new(name='UVMap')
for p in geometry.polygons:
 p.use_smooth=True
 for li in p.loop_indices:
  r=records[geometry.loops[li].vertex_index];layer.data[li].uv=(r['uv']['x'],r['uv']['y'])
geometry.normals_split_custom_set_from_vertices([tuple(r['normal'][k] for k in ['x','y','z']) for r in records]);geometry.materials.append(material)
for ob in list(bpy.data.objects):
 if ob.type!='MESH':continue
 if (ob.name=='Raccoon_RoundTail_Body_v11' or ('v11' in ob.name and '_new_Raccoon' in ob.name)):
  ob.data=fixed.copy()
 if 'v11' in ob.name and 'Opening' in ob.name:
  ob.data=geometry.copy()
  for g in list(ob.vertex_groups):ob.vertex_groups.remove(g)
  for name in {w['name'] for r in records for w in r['influences']}:ob.vertex_groups.new(name=name)
  for i,r in enumerate(records):
   for w in r['influences']:ob.vertex_groups[w['name']].add([i],w['weight'],'REPLACE')
print('SURROUND_PANEL',len(selected),'old faces re-UVd;',len(remove),'triangles; original positions retained')
