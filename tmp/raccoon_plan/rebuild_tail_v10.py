import bpy,bmesh,math,json,os
from mathutils import Vector,Matrix
ROOT='C:/Users/iam12/Train Survival/output/raccoon-tail-v10';os.makedirs(ROOT,exist_ok=True)
scene=bpy.context.scene
src=bpy.data.objects['COMPARE_v9_new_Raccoon_측면'];rig=bpy.data.objects['COMPARE_v9_new_Rig_측면']
body=src.copy();body.data=src.data.copy();body.name='Raccoon_RoundTail_Body_v10';scene.collection.objects.link(body)
sel=body.vertex_groups['SEL_Tail'].index;ids={v.index for v in body.data.vertices if any(w.group==sel and w.weight>.5 for w in v.groups)}
removed_positions=[dict(zip(['x','y','z'],body.data.vertices[i].co)) for i in ids]
bm=bmesh.new();bm.from_mesh(body.data);bm.verts.ensure_lookup_table()
faces=[f for f in bm.faces if any(v.index in ids for v in f.verts)]
removed_face_count=len(faces)
bmesh.ops.delete(bm,geom=faces,context='FACES_ONLY');bm.to_mesh(body.data);bm.free();body.data.update()
# New regular sections replace the collapsed, irregular tail surface, including its tapered attachment.
controls=[(0,-.10,-.195,.15,.16),(0,.005,-.195,.15,.16),(0,.13,-.245,.175,.18),(0,.28,-.39,.225,.235),(0,.40,-.58,.235,.245),(0,.48,-.76,.18,.19),(0,.49,-.875,.075,.08),(0,.48,-.925,.008,.009)]
def sample(t):
 x=t*(len(controls)-1);i=min(len(controls)-2,int(x));u=x-i
 a=controls[max(0,i-1)];b=controls[i];c=controls[i+1];d=controls[min(len(controls)-1,i+2)]
 return [.5*((2*b[j])+(-a[j]+c[j])*u+(2*a[j]-5*b[j]+4*c[j]-d[j])*u*u+(-a[j]+3*b[j]-3*c[j]+d[j])*u*u*u) for j in range(5)]
N=33;S=24;verts=[];uvs=[];weights=[];faces=[]
names=['Hips','Tail01','Tail02','Tail03','Tail04','Tail05'];knots=[0,.20,.40,.61,.81,1]
for ring in range(N):
 t=ring/(N-1);s=sample(t);center=Vector(s[:3]);before=Vector(sample(max(0,t-.001))[:3]);after=Vector(sample(min(1,t+.001))[:3]);tangent=(after-before).normalized();x=Vector((1,0,0));up=x.cross(tangent).normalized()
 j=min(4,next((j for j in range(5) if knots[j]<=t<=knots[j+1]),4));f=(t-knots[j])/(knots[j+1]-knots[j]);f=f*f*(3-2*f)
 w={names[j]:1-f,names[j+1]:f}
 for k in range(S):
  angle=2*math.pi*k/S;p=center+x*(math.cos(angle)*s[3])+up*(math.sin(angle)*s[4]);verts.append(tuple(p));uvs.append((k/S,t));weights.append(w)
for ring in range(N-1):
 for k in range(S):faces.append((ring*S+k,ring*S+(k+1)%S,(ring+1)*S+(k+1)%S,(ring+1)*S+k))
verts.append(tuple(Vector(sample(0)[:3])));uvs.append((.5,0));weights.append({'Hips':1});start=len(verts)-1
verts.append(tuple(Vector(sample(1)[:3])));uvs.append((.5,1));weights.append({'Tail05':1});end=len(verts)-1
for k in range(S):faces.append((start,(k+1)%S,k));faces.append((end,(N-1)*S+k,(N-1)*S+(k+1)%S))
geo=bpy.data.meshes.new('Raccoon_RoundTail_Geometry_v10');geo.from_pydata(verts,[],faces);geo.update()
bm=bmesh.new();bm.from_mesh(geo);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(geo);bm.free();geo.update()
uv=geo.uv_layers.new(name='UVMap')
for p in geo.polygons:
 coords=[uvs[geo.loops[i].vertex_index] for i in p.loop_indices]
 seam=max(u for u,v in coords)-min(u for u,v in coords)>.5
 for i,(u,v) in zip(p.loop_indices,coords):uv.data[i].uv=(1 if seam and u==0 else u,v)
 p.use_smooth=True
tail=bpy.data.objects.new('Raccoon_RoundTail_v10',geo);scene.collection.objects.link(tail);tail.parent=rig;tail.matrix_parent_inverse=Matrix.Identity(4);tail.matrix_basis=Matrix.Identity(4)
mod=tail.modifiers.new('Tail Skin','ARMATURE');mod.object=rig
for n in names:tail.vertex_groups.new(name=n)
for i,w in enumerate(weights):
 for n,value in w.items():
  if value>1e-7:tail.vertex_groups[n].add([i],value,'REPLACE')
# A seamless longitudinal stripe texture for the rebuilt UV layout.
width=64;height=1024;pixels=[];light=(.62,.60,.55);dark=(.29,.28,.25)
for y in range(height):
 t=y/(height-1);phase=(t*4.7+.08)%1;edge=.018
 blend=min(1,max(0,(phase-.48)/edge));blend=blend*blend*(3-2*blend)
 if phase>.97:blend=1-min(1,(phase-.97)/.03)
 color=[light[i]*(1-blend)+dark[i]*blend for i in range(3)]
 for x in range(width):pixels.extend((*color,1))
image=bpy.data.images.new('T_Raccoon_Tail_v10',width=width,height=height,alpha=True);image.pixels.foreach_set(pixels);image.filepath_raw=ROOT+'/T_Raccoon_Tail_v10.png';image.file_format='PNG';image.save();image.pack()
mat=bpy.data.materials.new('M_Raccoon_RoundTail_v10');mat.use_nodes=True;bs=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED');tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image;mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color']);bs.inputs['Roughness'].default_value=.85;geo.materials.append(mat)
# Export exact per-corner vertices to preserve the UV seam in Unity.
geo.calc_loop_triangles();out=[];triangles=[]
for tri in geo.loop_triangles:
 for li in tri.loops:
  loop=geo.loops[li];i=loop.vertex_index
  out.append({'position':dict(zip(['x','y','z'],geo.vertices[i].co)),'normal':dict(zip(['x','y','z'],geo.corner_normals[li].vector)),'uv':dict(zip(['x','y'],uv.data[li].uv)),'influences':[{'name':n,'weight':v} for n,v in weights[i].items() if v>1e-7]});triangles.append(len(out)-1)
json.dump({'removePositions':removed_positions,'vertices':out,'triangles':triangles},open(ROOT+'/tail_geometry.json','w'))
src.hide_set(True);body.hide_set(False);tail.hide_set(False)
report={'rings':N,'sides':S,'tail_vertices':len(geo.vertices),'tail_triangles':len(geo.loop_triangles),'removed_original_faces':removed_face_count,'max_influences':2}
json.dump(report,open(ROOT+'/authoring_report.json','w'),indent=2);print(report)
