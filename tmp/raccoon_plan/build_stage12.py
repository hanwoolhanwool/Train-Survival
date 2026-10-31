import bpy,bmesh,math,json,os
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform
from collections import Counter
OUT='C:/Users/iam12/Desktop/Train_Survival_Blender/Raccoon_Production'
obj=bpy.data.objects['raccoon']
backup=bpy.data.objects.get('raccoon_SOURCE_before_stage12')
if backup is None:
    coll=bpy.data.collections.new('Raccoon_Source_Archive');bpy.context.scene.collection.children.link(coll)
    backup=obj.copy();backup.data=obj.data.copy();backup.name='raccoon_SOURCE_before_stage12';coll.objects.link(backup)
    backup.hide_render=True;backup.hide_set(True)
source=backup.data
source.calc_loop_triangles()
coords=[v.co.copy() for v in source.vertices]
polys=[tuple(p.vertices) for p in source.polygons]
bvh=BVHTree.FromPolygons(coords,polys,all_triangles=True)
old_uv=source.uv_layers.active
source_normals=[tuple(n.vector) for n in source.corner_normals]
def sample_uv(co):
    p,no,fi,dist=bvh.find_nearest(co)
    if fi is None:return (0,0)
    f=source.polygons[fi]; vv=[coords[j] for j in f.vertices];uvs=[Vector((*old_uv.data[i].uv,0)) for i in f.loop_indices]
    return barycentric_transform(p,*vv,*uvs).xy
bm=bmesh.new();bm.from_mesh(source)
orig=bm.faces.layers.int.new('source_face')
region=bm.faces.layers.int.new('edit_region')
tag=bm.verts.layers.int.new('anatomy_part')
for i,f in enumerate(bm.faces):f[orig]=i+1
bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-5)
uv=bm.loops.layers.uv.active
def vnew(co,part=0):
    v=bm.verts.new(co);v[tag]=part;return v
def face(vs,mat=1,part=1,uvs=None):
    if part==2:
        mat=1
        side=1 if sum(v.co.x for v in vs)>0 else -1
        C=Vector((side*.509,-.256,.035));W=Vector((side*.640,-.302,-.148));D=(W-C).normalized()
        U=Vector((0,1,0));U=(U-D*U.dot(D)).normalized();N=U.cross(D).normalized()*side
        vals=[]
        for v in vs:
            d=v.co-C;q=max(0,min(1,d.dot(D)/(W-C).length));rad=d-D*d.dot(D)
            a=(math.atan2(rad.dot(N),rad.dot(U))+math.pi)/(2*math.pi);vals.append((a,q))
        if max(a for a,q in vals)-min(a for a,q in vals)>.5:vals=[(a+1 if a<.5 else a,q) for a,q in vals]
        uvs=[(.74+.20*a,.03+.16*q) for a,q in vals]
    elif part==1 and uvs and len(set(uvs))==1:
        side=1 if sum(v.co.x for v in vs)>0 else -1
        C=Vector((side*.509,-.256,.035));W=Vector((side*.640,-.302,-.148));D=(W-C).normalized()
        U=Vector((0,1,0));U=(U-D*U.dot(D)).normalized();N=U.cross(D).normalized()*side
        along=[(v.co-W).dot(D) for v in vs]
        if max(along)-min(along)<1e-6:
            uvs=[(.02+.52*max(0,min(1,.5+(v.co-W).dot(U)/.18)),.976+.015*max(0,min(1,.5+(v.co-W).dot(N)/.074))) for v in vs]
        else:
            uvs=[(.02+.52*max(0,min(1,(v.co-W).dot(D)/.103)),.976+.015*max(0,min(1,.5+(v.co-W).dot(N)/.074))) for v in vs]
    f=bm.faces.new(vs);f.material_index=mat;f[orig]=0;f[region]=part;f.smooth=True
    if uvs:
        for l,t in zip(f.loops,uvs):l[uv].uv=t
    elif mat==0:
        for l in f.loops:l[uv].uv=sample_uv(l.vert.co)
    return f
def bridge(a,b,mat=1,part=1):
    assert len(a)==len(b)
    return [face([a[j],a[(j+1)%len(a)],b[(j+1)%len(b)],b[j]],mat,part) for j in range(len(a))]
def order_boundary(vs,C,U,N):
    return sorted(vs,key=lambda v:math.atan2((v.co-C).dot(N),(v.co-C).dot(U)))
def zipper(a,b,C,U,N):
    # Order circular loops at the same seam, then bridge differing counts.
    a=order_boundary(a,C,U,N);b=order_boundary(b,C,U,N)
    aa=[(math.atan2((v.co-C).dot(N),(v.co-C).dot(U))+math.pi)/(2*math.pi) for v in a]
    bb=[(math.atan2((v.co-C).dot(N),(v.co-C).dot(U))+math.pi)/(2*math.pi) for v in b]
    i=j=0
    while i<len(a) or j<len(b):
        na=aa[(i+1)%len(a)]+(1 if i+1>=len(a) else 0) if i<len(a) else 10
        nb=bb[(j+1)%len(b)]+(1 if j+1>=len(b) else 0) if j<len(b) else 10
        if abs(na-nb)<1e-5:
            face([a[i%len(a)],a[(i+1)%len(a)],b[(j+1)%len(b)],b[j%len(b)]],0,2);i+=1;j+=1
        elif na<nb:
            face([a[i%len(a)],a[(i+1)%len(a)],b[j%len(b)]],0,2);i+=1
        else:
            face([a[i%len(a)],b[(j+1)%len(b)],b[j%len(b)]],0,2);j+=1

finger_records=[]
for side in [1,-1]:
    C=Vector((side*.509,-.256,.035));W=Vector((side*.640,-.302,-.148))
    D=(W-C).normalized();U=Vector((0,1,0));U=(U-D*U.dot(D)).normalized();N=U.cross(D).normalized()*side
    geom=[f for f in bm.faces if any(side*v.co.x>.35 and -.5<v.co.z<.16 for v in f.verts)]
    es=set(e for f in geom for e in f.edges);vs=set(v for f in geom for v in f.verts)
    bmesh.ops.bisect_plane(bm,geom=list(vs)+list(es)+geom,dist=1e-7,plane_co=C,plane_no=D,clear_inner=False,clear_outer=False)
    candidates=set(v for v in bm.verts if side*v.co.x>.35 and -.5<v.co.z<.16 and (v.co-C).dot(D)>1e-6)
    seed=max(candidates,key=lambda v:side*v.co.x)
    doomed={seed};queue=[seed]
    while queue:
        v=queue.pop()
        for e in v.link_edges:
            n=e.other_vert(v)
            if n in candidates and n not in doomed:doomed.add(n);queue.append(n)
    doomed=list(doomed)
    bmesh.ops.delete(bm,geom=doomed,context='VERTS')
    boundary=set(v for e in bm.edges if e.is_boundary for v in e.verts if side*v.co.x>.35 and abs((v.co-C).dot(D))<1e-5)
    assert len(boundary)>5,('Bad wrist boundary',side,len(boundary))
    # Palm: two rounded grids and side walls, four distal openings and one opposing thumb opening.
    xs=[-1,-.785,-.57,-.477,-.262,-.047,.046,.261,.476,.569,.784,1]
    lengths=[0,.018,.039,.060,.082,.103]
    widths=[.054,.059,.070,.080,.085,.082]
    thicks=[.037,.036,.034,.031,.027,.023]
    grids={};mids={}
    handpart=100 if side==1 else 200
    for r,(s,hw,ht) in enumerate(zip(lengths,widths,thicks)):
        for k,x in enumerate(xs):
            thickness=ht*(1-.18*x*x)
            for sign in [-1,1]:
                grids[r,k,sign]=vnew(W+D*s+U*(hw*x)+N*(thickness*sign),handpart)
        for k in [0,11]:mids[r,k]=vnew(W+D*s+U*(hw*xs[k]),handpart)
    for r in range(5):
        for k in range(11):
            for sign in [-1,1]:
                vv=[grids[r,k,sign],grids[r,k+1,sign],grids[r+1,k+1,sign],grids[r+1,k,sign]]
                xmin=.02 if sign==-1 else .29
                us=[(xmin+.25*(xs[kk]+1)/2,.71+.26*lengths[rr]/lengths[-1]) for rr,kk in [(r,k),(r,k+1),(r+1,k+1),(r+1,k)]]
                face(vv,uvs=us)
        for k in [0,11]:
            if k==0 and r in [1,2]:continue
            for sign in [-1,1]:
                face([grids[r,k,sign],grids[r+1,k,sign],mids[r+1,k],mids[r,k]],uvs=[(.03,.78)]*4)
    endmid={k:(mids[5,k] if k in [0,11] else vnew(W+D*lengths[5]+U*(widths[5]*xs[k]),handpart)) for k in range(12)}
    for k in [2,5,8]:
        for sign in [-1,1]:face([grids[5,k,sign],grids[5,k+1,sign],endmid[k+1],endmid[k]],uvs=[(.03,.90)]*4)
    root=[grids[0,k,1] for k in range(12)]+[mids[0,11]]+[grids[0,k,-1] for k in reversed(range(12))]+[mids[0,0]]
    # Rebuild exposed forearm using quad rings fitted to the original surface.
    prev=None
    for q in [.06,.32,.39,.46,.60,.76,.90]:
        c=C.lerp(W,q);ring=[]
        for rv in root:
            offset=rv.co-W;rad=(U*offset.dot(U)+N*offset.dot(N)).normalized()
            hit,normal,idx,dist=bvh.ray_cast(c,rad,.15)
            radius=dist if hit is not None else offset.length
            radius=max(.025,min(.092,radius))
            radius=radius*(1-q**4)+offset.length*q**4
            ring.append(vnew(c+rad*radius,handpart+10))
        if prev is None:zipper(list(boundary),ring,C,U,N)
        else:bridge(prev,ring,0,2)
        prev=ring
    bridge(prev,root,0,2)
    def digit(base,axis,length,spread,slot,name):
        center=sum((v.co for v in base),Vector())/8
        ax=axis.normalized()
        # Preserve the opening shape at the palm and soften to an octagonal section.
        aa=U-ax*U.dot(ax)
        if aa.length<.1:aa=N-ax*N.dot(ax)
        aa.normalize();bb=ax.cross(aa).normalized()
        offsets=[v.co-center for v in base]
        previous=base
        ts=[0,.19,.30,.39,.59,.68,.86,.96]
        for q in ts[1:]:
            c=center+ax*(length*q)+U*(spread*q*q)+N*(.025*q*q)
            scale=1-.32*q
            if .27<q<.4 or .57<q<.7:scale+=.035
            if q>.9:scale*=.68
            ring=[]
            for j,off in enumerate(offsets):
                radial=off-ax*off.dot(ax)
                if slot==4:
                    first=offsets[0]-ax*offsets[0].dot(ax)
                    phase=math.atan2(first.dot(bb),first.dot(aa))
                    orient=1 if sum(offsets[k].cross(offsets[(k+1)%8]).dot(ax) for k in range(8))>0 else -1
                    ang=phase+orient*j*2*math.pi/8
                    circular=aa*(math.cos(ang)*.026)+bb*(math.sin(ang)*.026)
                    radial=radial.lerp(circular,min(1,q/.30))
                co=c+radial*scale+ax*off.dot(ax)*(1-q)**2
                ring.append(vnew(co,handpart+slot+1))
            for j in range(8):
                ff=face([previous[j],previous[(j+1)%8],ring[(j+1)%8],ring[j]])
                u0=.56+slot*.082
                for loop,(uu,tt) in zip(ff.loops,[(j/8,ts[ts.index(q)-1]),((j+1)/8,ts[ts.index(q)-1]),((j+1)/8,q),(j/8,q)]):loop[uv].uv=(u0+.072*uu,.71+.26*tt)
            previous=ring
        cap=vnew(center+ax*length+U*spread+N*.025,handpart+slot+1)
        for j in range(8):
            u0=.56+slot*.082
            face([previous[j],previous[(j+1)%8],cap],uvs=[(u0+.072*j/8,.9596),(u0+.072*(j+1)/8,.9596),(u0+.036,.978)])
        finger_records.append({'side':side,'name':name,'tip':list(cap.co),'base':list(center),'part':handpart+slot+1})
    for slot,(k,length,spread,name) in enumerate(zip([0,3,6,9],[.106,.121,.112,.091],[-.007,-.002,.004,.010],['index','middle','ring','little'])):
        base=[grids[5,k,-1],grids[5,k+1,-1],grids[5,k+2,-1],endmid[k+2],grids[5,k+2,1],grids[5,k+1,1],grids[5,k,1],endmid[k]]
        digit(base,D,length,spread,slot,name)
    thumb=[grids[1,0,-1],grids[2,0,-1],grids[3,0,-1],mids[3,0],grids[3,0,1],grids[2,0,1],grids[1,0,1],mids[1,0]]
    digit(thumb,-U*.55+D*.60+N*.60,.105,-.006,4,'thumb')

# Clean construction-only middle vertices and add modest density to the tail while preserving its outline.
bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_edges],context='VERTS')
tailfaces=[f for f in bm.faces if f.calc_center_median().y>.012 and f.calc_center_median().z<-.065 and abs(f.calc_center_median().x)<.29]
for f in tailfaces:f[region]=3;f.material_index=1
tedges=set(e for f in tailfaces for e in f.edges);tverts=set(v for f in tailfaces for v in f.verts)
bmesh.ops.bisect_plane(bm,geom=list(tverts)+list(tedges)+tailfaces,dist=1e-7,plane_co=(0,0,0),plane_no=(1,0,0),clear_inner=False,clear_outer=False)
path=[Vector(p) for p in [(0,-.045,-.175),(0,.15,-.255),(0,.31,-.425),(0,.43,-.625),(0,.49,-.79),(0,.48,-.895)]]
lens=[(b-a).length for a,b in zip(path,path[1:])];total=sum(lens)
tail_a=Vector((0,-.03,-.16));tail_b=Vector((0,.48,-.895));tail_axis=(tail_b-tail_a).normalized()
projections=[v.co.dot(tail_axis) for f in bm.faces if f[region]==3 for v in f.verts]
tail_min=min(projections);tail_max=max(projections)
def tail_uv(co):
    a=tail_a;b=tail_b;tangent=tail_axis
    t=(co.dot(tangent)-tail_min)/(tail_max-tail_min);c=a+tangent*(co-a).dot(tangent)
    bottom=Vector((0,tangent.z,-tangent.y));r=co-c
    angle=math.atan2(r.x,r.dot(bottom))/(2*math.pi)
    return angle%1,t
for f in bm.faces:
    if f[region]!=3:continue
    vals=[tail_uv(l.vert.co) for l in f.loops]
    if max(u for u,t in vals)-min(u for u,t in vals)>.5:vals=[(1 if u<.001 else u,t) for u,t in vals]
    for loop,(u,t) in zip(f.loops,vals):loop[uv].uv=(.02+.66*u,.02+.63*t)
    f.smooth=True
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
bm.normal_update()
mesh=bpy.data.meshes.new('Raccoon_Stage12_QuadHands_UVTail');bm.to_mesh(mesh);bm.free()
mesh.materials.append(source.materials[0])
mat=bpy.data.materials.get('M_Raccoon_HandsTail_Stage12') or bpy.data.materials.new('M_Raccoon_HandsTail_Stage12')
mat.use_nodes=True;nodes=mat.node_tree.nodes;nodes.clear();links=mat.node_tree.links
out=nodes.new('ShaderNodeOutputMaterial');out.location=(800,0)
bs=nodes.new('ShaderNodeBsdfPrincipled');bs.location=(540,0);bs.inputs['Roughness'].default_value=.76;links.new(bs.outputs['BSDF'],out.inputs['Surface'])
tex=nodes.new('ShaderNodeTexCoord');tex.location=(-900,100)
sep=nodes.new('ShaderNodeSeparateXYZ');sep.location=(-730,100);links.new(tex.outputs['UV'],sep.inputs['Vector'])
def mathnode(op,a=None,b=None):
    n=nodes.new('ShaderNodeMath');n.operation=op
    for idx,val in enumerate([a,b]):
        if val is None:continue
        if isinstance(val,(float,int)):n.inputs[idx].default_value=val
        else:links.new(val,n.inputs[idx])
    return n.outputs[0]
t=mathnode('DIVIDE',mathnode('SUBTRACT',sep.outputs['Y'],.02),.63)
# Four complete dark rings plus a dark tip; wrap is continuous around U.
wave=mathnode('COSINE',mathnode('MULTIPLY',t,math.pi*9))
ramp=nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].position=.02;ramp.color_ramp.elements[0].color=(.072,.064,.057,1)
ramp.color_ramp.elements[1].position=.20;ramp.color_ramp.elements[1].color=(.43,.39,.335,1)
links.new(wave,ramp.inputs[0])
is_tail=mathnode('LESS_THAN',sep.outputs['Y'],.68)
mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='MIX';mix.inputs[1].default_value=(.060,.050,.043,1);links.new(is_tail,mix.inputs[0]);links.new(ramp.outputs['Color'],mix.inputs[2])
armmask=mathnode('MULTIPLY',mathnode('GREATER_THAN',sep.outputs['X'],.72),is_tail)
final=nodes.new('ShaderNodeMixRGB');final.blend_type='MIX';links.new(armmask,final.inputs[0]);links.new(mix.outputs[0],final.inputs[1]);final.inputs[2].default_value=(.168,.122,.100,1);links.new(final.outputs[0],bs.inputs['Base Color'])
mesh.materials.append(mat)
# Restore the source split normals on unchanged faces. New hands and tail use their new smooth topology.
orig_attr=mesh.attributes.get('source_face');region_attr=mesh.attributes.get('edit_region')
normals=[(0,0,0)]*len(mesh.loops)
for f in mesh.polygons:
    sid=orig_attr.data[f.index].value-1
    reg=region_attr.data[f.index].value
    if sid<0 or reg==3:continue
    sf=source.polygons[sid];vv=[coords[k] for k in sf.vertices];nn=[Vector(source_normals[j]) for j in sf.loop_indices]
    for li in f.loop_indices:
        co=mesh.vertices[mesh.loops[li].vertex_index].co
        no=barycentric_transform(co,*vv,*nn).normalized()
        normals[li]=tuple(no)
mesh.normals_split_custom_set(normals)
oldwork=obj.data;obj.data=mesh
if oldwork!=source and oldwork.users==0 and oldwork.name.startswith('Raccoon_Stage12'):bpy.data.meshes.remove(oldwork)
obj['stage12']='Five digits per hand; exposed forearm quad loops; tail UV and material rebuilt. Rigging pending.'
obj['digit_count_per_hand']=5
obj['source_backup']=backup.name
obj['finger_layout_json']=json.dumps(finger_records)
obj['production_folder']=OUT
mesh.update()
check=bmesh.new();check.from_mesh(mesh)
report={'verts':len(mesh.vertices),'faces':len(mesh.polygons),'face_sides':dict(Counter(len(p.vertices) for p in mesh.polygons)),'boundary_edges':sum(e.is_boundary for e in check.edges),'nonmanifold_edges':sum(not e.is_manifold for e in check.edges),'loose_verts':sum(not v.link_edges for v in check.verts),'fingers':finger_records}
check.free();mesh.calc_loop_triangles();report['triangles']=len(mesh.loop_triangles)
with open(OUT+'/stage12_geometry_report.json','w') as f:json.dump(report,f,indent=2)
print(json.dumps(report,indent=2))
