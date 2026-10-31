import bpy,bmesh,math,json
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform
OUT='C:/Users/iam12/Desktop/Train_Survival_Blender/Raccoon_Production'
ob=bpy.data.objects['raccoon'];src=bpy.data.objects['raccoon_SOURCE_before_cuff_v3'].data
src.calc_loop_triangles();source_normals=[n.vector.copy() for n in src.corner_normals]
clothfaces=[p for p in src.polygons if p.material_index==0]
clothbvh=BVHTree.FromPolygons([v.co for v in src.vertices],[list(p.vertices) for p in clothfaces],all_triangles=False)
def sample_uv(co):
 hit,no,idx,dist=clothbvh.find_nearest(co);f=clothfaces[idx]
 # Original garment faces are triangles except for cut seam remnants.
 tris=[t for t in src.loop_triangles if t.polygon_index==f.index]
 best=min(tris,key=lambda t:(sum((src.vertices[v].co for v in t.vertices),Vector())/3-hit).length)
 vv=[src.vertices[v].co for v in best.vertices];uu=[Vector((*src.uv_layers.active.data[l].uv,0)) for l in best.loops]
 return barycentric_transform(hit,*vv,*uu).xy
bm=bmesh.new();bm.from_mesh(src);bm.verts.ensure_lookup_table();bm.faces.ensure_lookup_table()
orig=bm.faces.layers.int.new('cuff_source_face');oldv=bm.verts.layers.int.new('cuff_source_vertex')
reg=bm.faces.layers.int['edit_region'];part=bm.verts.layers.int['anatomy_part'];along=bm.verts.layers.float['elbow_t'];ringid=bm.verts.layers.int['elbow_ring'];uv=bm.loops.layers.uv.active
for f in bm.faces:f[orig]=f.index+1
for v in bm.verts:v[oldv]=v.index+1
deform=bm.verts.layers.deform.active
if deform:
 for v in bm.verts:v[deform].clear()
def traverse(vertices,edges,c,u,n):
 start=min(vertices,key=lambda v:math.atan2((v.co-c).dot(n),(v.co-c).dot(u)))
 result=[start];prev=None;cur=start
 while True:
  neighbors=[e.other_vert(cur) for e in edges if cur in e.verts and e.other_vert(cur)!=prev]
  assert neighbors,'Open cuff boundary'
  nxt=neighbors[0]
  if nxt==start:break
  result.append(nxt);prev,cur=cur,nxt;assert len(result)<=len(vertices)
 assert len(result)==len(vertices),(len(result),len(vertices))
 xy=[Vector(((v.co-c).dot(u),(v.co-c).dot(n))) for v in result]
 if sum(xy[i].cross(xy[(i+1)%len(xy)]) for i in range(len(xy)))<0:result=[result[0]]+list(reversed(result[1:]))
 return result
def params(vs,c,u,n):
 result=[];last=None
 for v in vs:
  a=(math.atan2((v.co-c).dot(n),(v.co-c).dot(u))+math.pi)/(2*math.pi)
  if last is not None:
   while a-last<-.5:a+=1
   while a-last>.5:a-=1
  result.append(a);last=a
 return result
def addface(vs,material,texcoords=None):
 f=bm.faces.new(vs);f.material_index=2 if material==0 else material;f[orig]=0;f[reg]=4 if material==0 else 2;f.smooth=True
 for i,l in enumerate(f.loops):
  l[uv].uv=texcoords[i]
 return f
def zipper(a,b,c,u,n,material,ta=0,tb=.18):
 def rotate(vs):
  k=min(range(len(vs)),key=lambda i:math.atan2((vs[i].co-c).dot(n),(vs[i].co-c).dot(u)))
  return vs[k:]+vs[:k]
 a=rotate(a);b=rotate(b)
 aa=params(a,c,u,n);bb=params(b,c,u,n);i=j=0
 while i<len(a) or j<len(b):
  na=aa[(i+1)%len(a)]+int(i+1>=len(a)) if i<len(a) else 10
  nb=bb[(j+1)%len(b)]+int(j+1>=len(b)) if j<len(b) else 10
  ua=aa[i%len(a)]+int(i>=len(a));ub=bb[j%len(b)]+int(j>=len(b))
  if abs(na-nb)<1e-5:
   vv=[a[i%len(a)],a[(i+1)%len(a)],b[(j+1)%len(b)],b[j%len(b)]];tx=[(ua,ta),(na,ta),(nb,tb),(ub,tb)];i+=1;j+=1
  elif na<nb:
   vv=[a[i%len(a)],a[(i+1)%len(a)],b[j%len(b)]];tx=[(ua,ta),(na,ta),(ub,tb)];i+=1
  else:
   vv=[a[i%len(a)],b[(j+1)%len(b)],b[j%len(b)]];tx=[(ua,ta),(nb,tb),(ub,tb)];j+=1
  addface(vv,material,[(.03+.88*x,.03+.90*y) if material==0 else (.755+.18*x,.035+.145*max(0,y)) for x,y in tx])
stats=[]
for side in [1,-1]:
 P=Vector((side*.422,-.242,.055));W=Vector((side*.640,-.302,-.148));D=(W-P).normalized();U=Vector((0,1,0));U=(U-D*U.dot(D)).normalized();N=U.cross(D).normalized()*side
 cut=-.065
 # Slice only the sleeve's local garment region, leaving the completed hand/arm surfaces intact.
 candidates=[f for f in bm.faces if f[reg]==0 and all(side*v.co.x>.22 and -.48<v.co.y<-.02 and -.06<v.co.z<.32 for v in f.verts)]
 es={e for f in candidates for e in f.edges};vs={v for f in candidates for v in f.verts}
 bmesh.ops.bisect_plane(bm,geom=list(vs)+list(es)+candidates,dist=1e-7,plane_co=P+D*cut,plane_no=D,clear_inner=False,clear_outer=False)
 def eligible(f):
  co=f.calc_center_median()
  if side*co.x<.22 or not -.48<co.y<-.02 or not -.08<co.z<.32:return False
  if f[reg]==2:return any(abs(v[along])<1e-6 for v in f.verts) and max(v[along] for v in f.verts)<.181
  return f[reg]==0 and (co-P).dot(D)>cut+1e-7
 seeds=[f for f in bm.faces if f[reg]==2 and eligible(f)]
 selected=set(seeds);queue=list(seeds)
 while queue:
  f=queue.pop()
  for e in f.edges:
   for g in e.link_faces:
    if g not in selected and eligible(g):selected.add(g);queue.append(g)
 boundary=[e for e in bm.edges if sum(f in selected for f in e.link_faces)==1]
 distal={v for e in boundary if all(abs(v[along]-.18)<1e-5 for v in e.verts) for v in e.verts}
 proximal={v for e in boundary if all(abs((v.co-P).dot(D)-cut)<1e-5 for v in e.verts) for v in e.verts}
 assert len(distal)==16,(side,'distal',len(distal))
 assert len(set(v for e in boundary for v in e.verts))==len(proximal)+len(distal),(side,'unexpected boundary',len(boundary),len(proximal),len(distal))
 pe=[e for e in boundary if all(v in proximal for v in e.verts)];de=[e for e in boundary if all(v in distal for v in e.verts)]
 a=traverse(proximal,pe,P,U,N);z=traverse(distal,de,P,U,N)
 stats.append({'side':side,'replaced_faces':len(selected),'proximal_vertices':len(a),'distal_vertices':len(z)})
 inside={v for f in selected for v in f.verts if all(g in selected for g in v.link_faces)}
 bmesh.ops.delete(bm,geom=list(selected),context='FACES_ONLY');bmesh.ops.delete(bm,geom=list(inside),context='VERTS')
 # Rounded folded hem, visible opening, and a recessed lining. Arm begins inside the lining.
 profile=[(-.052,.112,.103,0),(-.036,.124,.115,0),(.010,.124,.115,0),(.023,.108,.099,0),(.014,.099,.089,0),(-.035,.099,.089,0),(-.043,.083,.067,1),(0,.083,.067,1),(.025,.082,.066,1)]
 previous=None
 for ri,(s,ru,rn,mat) in enumerate(profile):
  ring=[]
  for j in range(16):
   theta=-math.pi+2*math.pi*j/16;v=bm.verts.new(P+D*s+U*(ru*math.cos(theta))+N*(rn*math.sin(theta)));v[oldv]=0;v[part]=0 if mat==0 else (110 if side>0 else 210);v[along]=min(.18,max(0,s/(W-P).length));v[ringid]=-1;ring.append(v)
  if previous is None:zipper(a,ring,P,U,N,0,0,1/7)
  else:
   for j in range(16):
    vv=[previous[j],previous[(j+1)%16],ring[(j+1)%16],ring[j]]
    coords=[(j/16,ri/7),((j+1)/16,ri/7),((j+1)/16,(ri+1)/7),(j/16,(ri+1)/7)]
    tx=[(.03+.88*x,.03+.90*y) if mat==0 else (.755+.18*x,.035+.12*y*7/len(profile)) for x,y in coords]
    addface(vv,mat,tx)
  previous=ring
 zipper(previous,z,P,U,N,1,.08,.18)
for side in [1,-1]:
 P=Vector((side*.422,-.242,.055));D=(Vector((side*.640,-.302,-.148))-P).normalized()
 cutverts=[v for v in bm.verts if side*v.co.x>.22 and -.48<v.co.y<-.02 and -.06<v.co.z<.32 and abs((v.co-P).dot(D)+.065)<1e-5]
 bmesh.ops.remove_doubles(bm,verts=cutverts,dist=.00002)
bmesh.ops.delete(bm,geom=[e for e in bm.edges if not e.link_faces],context='EDGES')
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.normal_update()
me=bpy.data.meshes.new('Raccoon_CleanSleeveOpenings_v3');bm.to_mesh(me);bm.free()
for mat in src.materials:me.materials.append(mat)
cuffmat=bpy.data.materials.get('M_Raccoon_CuffFabric_v3') or bpy.data.materials.new('M_Raccoon_CuffFabric_v3');cuffmat.use_nodes=True
me.materials.append(cuffmat)
ns=[(0,0,0)]*len(me.loops)
for f in me.polygons:
 si=me.attributes['cuff_source_face'].data[f.index].value-1
 if si<0 or me.attributes['edit_region'].data[f.index].value==2:continue
 sf=src.polygons[si];lookup={src.loops[l].vertex_index:source_normals[l] for l in sf.loop_indices}
 for l in f.loop_indices:
  v=me.vertices[me.loops[l].vertex_index];oi=me.attributes['cuff_source_vertex'].data[v.index].value-1
  if oi in lookup and (v.co-src.vertices[oi].co).length<1e-6:ns[l]=tuple(lookup[oi])
me.normals_split_custom_set(ns);me.update();ob.data=me;ob.vertex_groups.clear()
for side,base in [('L',100),('R',200)]:
 for j,name in enumerate(['Index','Middle','Ring','Little','Thumb'],1):
  vg=ob.vertex_groups.new(name='SEL_'+name+'.'+side);vg.add([i for i,a in enumerate(me.attributes['anatomy_part'].data) if a.value==base+j],1,'REPLACE')
 for name,pred in [('Hand',lambda x:base<=x<=base+5),('Forearm',lambda x:x==base+10)]:
  vg=ob.vertex_groups.new(name='SEL_'+name+'.'+side);vg.add([i for i,a in enumerate(me.attributes['anatomy_part'].data) if pred(a.value)],1,'REPLACE')
vg=ob.vertex_groups.new(name='SEL_Tail');vg.add(sorted({v for f in me.polygons if me.attributes['edit_region'].data[f.index].value==3 for v in f.vertices}),1,'REPLACE')
ob.shape_key_add(name='Basis')
for sourcekey in src.shape_keys.key_blocks:
 if sourcekey.name=='Basis':continue
 key=ob.shape_key_add(name=sourcekey.name);key.value=0
 for v in me.vertices:
  oi=me.attributes['cuff_source_vertex'].data[v.index].value-1
  if 0<=oi<len(src.vertices) and (v.co-src.vertices[oi].co).length<1e-6:key.data[v.index].co=v.co+(sourcekey.data[oi].co-src.vertices[oi].co)
me.calc_loop_triangles();check=bmesh.new();check.from_mesh(me)
print(json.dumps({'patches':stats,'vertices':len(me.vertices),'triangles':len(me.loop_triangles),'boundary':sum(e.is_boundary for e in check.edges),'nonmanifold':sum(not e.is_manifold for e in check.edges)}));check.free()
