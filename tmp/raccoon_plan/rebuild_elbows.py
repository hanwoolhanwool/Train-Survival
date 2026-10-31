import bpy,bmesh,math,json
from mathutils import Vector
from collections import Counter
OUT='C:/Users/iam12/Desktop/Train_Survival_Blender/Raccoon_Production'
ob=bpy.data.objects['raccoon'];src=bpy.data.objects['raccoon_SOURCE_before_elbow'].data
skinids=json.load(open('C:/Users/iam12/Train Survival/tmp/raccoon_plan/elbow_skin_faces.json'))
normals=[v.vector.copy() for v in src.corner_normals]
bm=bmesh.new();bm.from_mesh(src);bm.faces.ensure_lookup_table();bm.verts.ensure_lookup_table()
# Selection-mask indices belong to the source datablock; rebuild them explicitly after retopology.
deform=bm.verts.layers.deform.active
if deform:
 for v in bm.verts:v[deform].clear()
orig=bm.faces.layers.int.get('elbow_source_face') or bm.faces.layers.int.new('elbow_source_face')
oldv=bm.verts.layers.int.get('elbow_source_vertex') or bm.verts.layers.int.new('elbow_source_vertex')
reg=bm.faces.layers.int['edit_region'];part=bm.verts.layers.int['anatomy_part'];uv=bm.loops.layers.uv.active
along=bm.verts.layers.float.get('elbow_t') or bm.verts.layers.float.new('elbow_t')
ringid=bm.verts.layers.int.get('elbow_ring') or bm.verts.layers.int.new('elbow_ring')
for f in bm.faces:f[orig]=f.index+1
for v in bm.verts:v[oldv]=v.index+1;v[along]=-1;v[ringid]=-1
skinsets={side:{bm.faces[i] for i in skinids[str(side)]} for side in [1,-1]}
plans=[]
boundary_cycles={}
def smooth(x):x=max(0,min(1,x));return x*x*(3-2*x)
def ordered(vs,c,U,N):return sorted(vs,key=lambda v:math.atan2((v.co-c).dot(N),(v.co-c).dot(U)))
def cycle_parameter(vs,c,U,N):
 edges=boundary_cycles.get(frozenset(vs))
 if edges is None:
  vv=ordered(vs,c,U,N)
  return vv,[(math.atan2((v.co-c).dot(N),(v.co-c).dot(U))+math.pi)/(2*math.pi) for v in vv]
 start=ordered(vs,c,U,N)[0];vv=[start];previous=None;v=start
 while True:
  candidates=[e.other_vert(v) for e in edges if v in e.verts and e.other_vert(v)!=previous]
  nxt=candidates[0]
  if nxt==start:break
  vv.append(nxt);previous,v=v,nxt
  assert len(vv)<=len(vs)
 assert len(vv)==len(vs)
 xy=[Vector(((v.co-c).dot(U),(v.co-c).dot(N))) for v in vv]
 if sum(xy[i].cross(xy[(i+1)%len(vv)]) for i in range(len(vv)))<0:vv=[vv[0]]+list(reversed(vv[1:]))
 aa=[];last=None
 for v in vv:
  a=(math.atan2((v.co-c).dot(N),(v.co-c).dot(U))+math.pi)/(2*math.pi)
  if last is not None:
   while a-last<-.5:a+=1
   while a-last>.5:a-=1
  aa.append(a);last=a
 return vv,aa
def addface(vs,side,us):
 f=bm.faces.new(vs);f.material_index=1;f.smooth=True;f[reg]=2;f[orig]=0
 for l,(u,t) in zip(f.loops,us):l[uv].uv=(.755+.18*u,.035+.145*t)
 return f
def zipper(a,b,c,U,N,ta,tb,side):
 a,aa=cycle_parameter(a,c,U,N);b,bb=cycle_parameter(b,c,U,N)
 i=j=0
 while i<len(a) or j<len(b):
  na=(aa[(i+1)%len(a)]+(1 if i+1>=len(a) else 0)) if i<len(a) else 10
  nb=(bb[(j+1)%len(b)]+(1 if j+1>=len(b) else 0)) if j<len(b) else 10
  ua=aa[i%len(a)]+(1 if i>=len(a) else 0);ub=bb[j%len(b)]+(1 if j>=len(b) else 0)
  if abs(na-nb)<1e-5:
   vv=[a[i%len(a)],a[(i+1)%len(a)],b[(j+1)%len(b)],b[j%len(b)]];tt=[(ua,ta),(na,ta),(nb,tb),(ub,tb)];i+=1;j+=1
  elif na<nb:
   vv=[a[i%len(a)],a[(i+1)%len(a)],b[j%len(b)]];tt=[(ua,ta),(na,ta),(ub,tb)];i+=1
  else:
   vv=[a[i%len(a)],b[(j+1)%len(b)],b[j%len(b)]];tt=[(ua,ta),(nb,tb),(ub,tb)];j+=1
  # The atlas contains a uniform fur patch; keep the seam within its bounds.
  addface(vv,side,tt)
for side in [1,-1]:
 faces=skinsets[side]
 boundary=[e for e in bm.edges if sum(f in faces for f in e.link_faces)==1]
 distal={v for e in boundary if any(f[reg]==1 for f in e.link_faces) for v in e.verts}
 proximal={v for e in boundary if not any(f[reg]==1 for f in e.link_faces) for v in e.verts}
 boundary_cycles[frozenset(proximal)]=[e for e in boundary if all(v in proximal for v in e.verts)]
 assert len(distal)==26,(side,len(distal))
 # Each retained cuff and wrist boundary must remain a single closed loop.
 for vv in [proximal,distal]:
  assert all(sum(e in boundary and e.other_vert(v) in vv for e in v.link_edges)==2 for v in vv)
 W=Vector((side*.640,-.302,-.148));Dend=(W-Vector((side*.509,-.256,.035))).normalized()
 # Use the existing sleeve opening for the root centre, not the previous cut plane origin.
 P=Vector(tuple((min(v.co[k] for v in proximal)+max(v.co[k] for v in proximal))*.5 for k in range(3)))
 # Match arm silhouette symmetrically; irregular source cuff edges remain untouched.
 P=Vector((side*.422,-.242,.055));D0=(W-P).normalized();length=(W-P).length
 B=P+D0*(length/3);C=W-Dend*(length/3)
 def center(t):return P*(1-t)**3+B*(3*t*(1-t)**2)+C*(3*t*t*(1-t))+W*t**3
 def tangent(t):return ((B-P)*3*(1-t)**2+(C-B)*6*t*(1-t)+(W-C)*3*t*t).normalized()
 def frame(t):
  d=tangent(t);u=Vector((0,1,0));u=(u-d*u.dot(d)).normalized();n=u.cross(d).normalized()*side;return d,u,n
 def radii(t):
  keys=[(0,.083,.067),(.16,.081,.065),(.32,.075,.061),(.40,.074,.060),(.53,.072,.058),(.70,.065,.049),(.88,.056,.037),(1,.054,.033)]
  for (ta,a,b),(tb,c,d) in zip(keys,keys[1:]):
   if t<=tb:
    q=smooth((t-ta)/(tb-ta));return a*(1-q)+c*q,b*(1-q)+d*q
  return keys[-1][1:]
 # Regularize the hidden sleeve opening as one convex section. The old lining contains
 # folded triangular tips, which otherwise create inward-facing transition faces.
 _,u0,n0=frame(0)
 cuff,params=cycle_parameter(list(proximal),P,u0,n0)
 lengths=[(cuff[(i+1)%len(cuff)].co-v.co).length for i,v in enumerate(cuff)];total=sum(lengths);acc=0
 phase=math.atan2((cuff[0].co-P).dot(n0),(cuff[0].co-P).dot(u0))
 for i,v in enumerate(cuff):
  theta=phase+2*math.pi*acc/total
  v.co=P+u0*(.083*math.cos(theta))+n0*(.067*math.sin(theta));acc+=lengths[i]
 # Record stable copies before deleting only the selected skin faces and their unused interior.
 interior={v for f in faces for v in f.verts if all(g in faces for g in v.link_faces)}
 bmesh.ops.delete(bm,geom=list(faces),context='FACES_ONLY')
 bmesh.ops.delete(bm,geom=[v for v in interior if v.is_valid],context='VERTS')
 for v in proximal:v[along]=0
 for v in distal:v[along]=1
 _,ue,ne=frame(1);root2=[Vector(((v.co-W).dot(ue),(v.co-W).dot(ne))) for v in ordered(distal,W,ue,ne)]
 def root_radius(theta):
  direction=Vector((math.cos(theta),math.sin(theta)));hits=[]
  for a,b in zip(root2,root2[1:]+root2[:1]):
   e=b-a;den=direction.cross(e)
   if abs(den)<1e-9:continue
   r=a.cross(e)/den;s=a.cross(direction)/den
   if r>0 and -.00001<=s<=1.00001:hits.append(r)
  return min(hits) if hits else .045
 ts=[.18,.25,.32,.39,.46,.53,.63,.76,.88,.96]
 rings=[]
 for rid,t in enumerate(ts):
  c=center(t);d,u,n=frame(t);ru,rn=radii(t);ring=[]
  for j in range(16):
   theta=-math.pi+2*math.pi*j/16
   r=1/math.sqrt((math.cos(theta)/ru)**2+(math.sin(theta)/rn)**2)
   k=smooth((t-.68)/.32);r=r*(1-k)+root_radius(theta)*k
   # Slight posterior elbow fullness with a shallow front crease, distributed over several rings.
   bump=math.exp(-((t-.39)/.13)**2);r+=.0025*bump*max(0,math.cos(theta))
   co=c+(u*math.cos(theta)+n*math.sin(theta))*r
   v=bm.verts.new(co);v[part]=110 if side>0 else 210;v[oldv]=0;v[along]=t;v[ringid]=rid+1;ring.append(v)
  rings.append(ring)
  if rid:
   for j in range(16):addface([rings[-2][j],rings[-2][(j+1)%16],ring[(j+1)%16],ring[j]],side,[(j/16,ts[rid-1]),((j+1)/16,ts[rid-1]),((j+1)/16,t),(j/16,t)])
 _,u,n=frame(ts[0]);zipper(list(proximal),rings[0],center(ts[0]*.5),u,n,0,ts[0],side)
 _,u,n=frame(1);zipper(rings[-1],list(distal),W,u,n,ts[-1],1,side)
 plans.append({'side':side,'root':list(P),'control1':list(B),'control2':list(C),'wrist':list(W),'elbow':list(center(.39)),'rings':ts})
bmesh.ops.delete(bm,geom=[e for e in bm.edges if not e.link_faces],context='EDGES')
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.normal_update()
me=bpy.data.meshes.new('Raccoon_ContinuousElbows');bm.to_mesh(me);bm.free()
for mat in src.materials:me.materials.append(mat)
ns=[(0,0,0)]*len(me.loops)
for f in me.polygons:
 si=me.attributes['elbow_source_face'].data[f.index].value-1
 if si<0:continue
 old=src.polygons[si];mapping={src.loops[l].vertex_index:normals[l] for l in old.loop_indices}
 for l in f.loop_indices:
  vi=me.attributes['elbow_source_vertex'].data[me.loops[l].vertex_index].value-1
  if me.attributes['elbow_t'].data[me.loops[l].vertex_index].value!=0:ns[l]=tuple(mapping[vi])
me.normals_split_custom_set(ns);me.update()
ob.data=me;ob['elbow_layout_json']=json.dumps(plans)
ob['elbow_revision']='Continuous oval arm sections from sleeve opening to wrist. Joint support loops; pose checks pending.'
ob.vertex_groups.clear()
for side,base in [('L',100),('R',200)]:
 for j,name in enumerate(['Index','Middle','Ring','Little','Thumb'],1):
  vg=ob.vertex_groups.new(name='SEL_'+name+'.'+side);vg.add([i for i,a in enumerate(me.attributes['anatomy_part'].data) if a.value==base+j],1,'REPLACE')
 vg=ob.vertex_groups.new(name='SEL_Hand.'+side);vg.add([i for i,a in enumerate(me.attributes['anatomy_part'].data) if base<=a.value<=base+5],1,'REPLACE')
 vg=ob.vertex_groups.new(name='SEL_Forearm.'+side);vg.add([i for i,a in enumerate(me.attributes['anatomy_part'].data) if a.value==base+10],1,'REPLACE')
vg=ob.vertex_groups.new(name='SEL_Tail');vg.add(sorted({v for f in me.polygons if me.attributes['edit_region'].data[f.index].value==3 for v in f.vertices}),1,'REPLACE')
check=bmesh.new();check.from_mesh(me);me.calc_loop_triangles()
report={'vertices':len(me.vertices),'faces':len(me.polygons),'triangles':len(me.loop_triangles),'boundary':sum(e.is_boundary for e in check.edges),'nonmanifold':sum(not e.is_manifold for e in check.edges),'loose_vertices':sum(not v.link_edges for v in check.verts),'new_arm_quads':sum(len(f.vertices)==4 and me.attributes['edit_region'].data[f.index].value==2 for f in me.polygons)}
check.free();print(json.dumps(report))
with open(OUT+'/elbow_geometry_report.json','w') as f:json.dump(report,f,indent=2)
