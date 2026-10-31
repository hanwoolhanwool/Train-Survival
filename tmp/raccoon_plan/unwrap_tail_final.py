import bpy,bmesh,math
from mathutils import Vector
ob=bpy.data.objects['raccoon'];me=ob.data;oldUV=me.uv_layers.active
faces=[p for p in me.polygons if me.attributes['edit_region'].data[p.index].value==3]
ids=sorted(set(v for p in faces for v in p.vertices));idx={v:i for i,v in enumerate(ids)}
tm=bpy.data.meshes.new('_TailUVReview');tm.from_pydata([me.vertices[i].co[:] for i in ids],[],[[idx[v] for v in p.vertices] for p in faces]);tm.update()
tm.uv_layers.new(name='UVMap');tm.uv_layers.new(name='BandCoordinates')
axis=Vector((0,.51,-.735)).normalized();a=Vector((0,-.03,-.16));bottom=Vector((0,axis.z,-axis.y))
vals=[v.co.dot(axis) for v in tm.vertices];low=min(vals);high=max(vals)
for p,q in zip(faces,tm.polygons):
 coords=[]
 for vi in q.vertices:
  co=tm.vertices[vi].co;r=co-(a+axis*(co-a).dot(axis));u=(math.atan2(r.x,r.dot(bottom))/(2*math.pi))%1;t=(co.dot(axis)-low)/(high-low);coords.append((u,t))
 if max(u for u,t in coords)-min(u for u,t in coords)>.5:coords=[(1 if u<.001 else u,t) for u,t in coords]
 for k,(u,t) in zip(q.loop_indices,coords):
  tm.uv_layers['UVMap'].data[k].uv=(.02+.66*u,.02+.63*t)
  tm.uv_layers['BandCoordinates'].data[k].uv=(.02+.66*u,.02+.63*t)
tm.uv_layers.active_index=0;tm.uv_layers['UVMap'].active_render=True
bm=bmesh.new();bm.from_mesh(tm);uv=bm.loops.layers.uv.get('UVMap')
for e in bm.edges:
 if not e.is_manifold:e.seam=True;continue
 a,b=e.link_loops
 for v in e.verts:
  ua=next(l[uv].uv for l in a.face.loops if l.vert==v);ub=next(l[uv].uv for l in b.face.loops if l.vert==v)
  if (ua-ub).length>1e-4:e.seam=True
bm.to_mesh(tm);bm.free()
tm.uv_layers.active_index=0;tm.uv_layers['UVMap'].active_render=True
temp=bpy.data.objects.new('_TailUVReview',tm);bpy.context.scene.collection.objects.link(temp)
previous=bpy.context.view_layer.objects.active;selected=list(bpy.context.selected_objects)
for o in selected:o.select_set(False)
temp.select_set(True);bpy.context.view_layer.objects.active=temp
try:
 bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.unwrap(method='ANGLE_BASED',margin=.025);bpy.ops.object.mode_set(mode='OBJECT')
 for q in tm.uv_layers['UVMap'].data:q.uv=(.025+q.uv.x*.65,.025+q.uv.y*.62)
 # Give the very narrow seam quad a separate planar island to avoid a folded UV corner.
 for p,q in zip(faces,tm.polygons):
  if p.index!=10650:continue
  co=[tm.vertices[v].co.copy() for v in q.vertices];x=(co[1]-co[0]).normalized();y=q.normal.cross(x).normalized()
  xy=[((v-co[0]).dot(x),(v-co[0]).dot(y)) for v in co];xmin=min(a for a,b in xy);xmax=max(a for a,b in xy);ymin=min(b for a,b in xy);ymax=max(b for a,b in xy)
  for k,(a,b) in zip(q.loop_indices,xy):tm.uv_layers['UVMap'].data[k].uv=(.69+.025*(a-xmin)/(xmax-xmin),.30+.025*(b-ymin)/(ymax-ymin))
 for p,q in zip(faces,tm.polygons):
  for l,k in zip(p.loop_indices,q.loop_indices):oldUV.data[l].uv=tm.uv_layers['UVMap'].data[k].uv
 # Bake the existing procedural bands through the preserved coordinate layer.
 mat=bpy.data.materials['M_Raccoon_HandsTail_ProceduralSource'].copy();mat.name='_TailUVBake';tm.materials.append(mat)
 nodes=mat.node_tree.nodes;links=mat.node_tree.links
 tc=next(n for n in nodes if n.type=='TEX_COORD');uvnode=nodes.new('ShaderNodeUVMap');uvnode.uv_map='BandCoordinates'
 for link in list(tc.outputs['UV'].links):links.new(uvnode.outputs['UV'],link.to_socket)
 bs=next(n for n in nodes if n.type=='BSDF_PRINCIPLED');out=next(n for n in nodes if n.type=='OUTPUT_MATERIAL');em=nodes.new('ShaderNodeEmission')
 links.new(bs.inputs['Base Color'].links[0].from_socket,em.inputs['Color']);links.new(em.outputs[0],out.inputs['Surface'])
 im=bpy.data.images['T_Raccoon_HandsTail_BaseColor_2K'];target=nodes.new('ShaderNodeTexImage');target.image=im
 for n in nodes:n.select=False
 target.select=True;nodes.active=target
 scene=bpy.data.scenes.new('_TailUVBake');scene.collection.objects.link(temp);scene.render.engine='CYCLES';scene.cycles.samples=1
 scene.view_layers[0].objects.active=temp;temp.select_set(True,view_layer=scene.view_layers[0]);tm.uv_layers.active_index=0;tm.uv_layers['UVMap'].active_render=True
 with bpy.context.temp_override(scene=scene,view_layer=scene.view_layers[0],active_object=temp,object=temp,selected_objects=[temp],selected_editable_objects=[temp]):
  bpy.ops.object.bake(type='EMIT',margin=12,use_clear=False)
 im.filepath_raw='C:/Users/iam12/Desktop/Train_Survival_Blender/Raccoon_Production/Textures/T_Raccoon_HandsTail_BaseColor_2K.png';im.file_format='PNG';im.save();im.pack()
 bpy.data.scenes.remove(scene);tm.materials.clear();bpy.data.materials.remove(mat)
 print('Tail unwrapped and baked')
finally:
 if temp.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
 bpy.data.objects.remove(temp,do_unlink=True);bpy.data.meshes.remove(tm)
 for o in selected:o.select_set(True)
 bpy.context.view_layer.objects.active=previous
