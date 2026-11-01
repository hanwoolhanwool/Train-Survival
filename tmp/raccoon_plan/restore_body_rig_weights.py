import bpy,math
from mathutils.kdtree import KDTree
ob=bpy.data.objects['raccoon'];me=ob.data;rig=bpy.data.objects['RIG_Raccoon'];src=bpy.data.objects['raccoon_SOURCE_before_stage12'];ref=bpy.data.objects['Mesh_0.001']
kd=KDTree(len(src.data.vertices))
for v in src.data.vertices:kd.insert(v.co,v.index)
kd.balance()
mapping={n:n for n in rig.data.bones.keys()};mapping.update({'Bone_035':'Ear01.L','Bone_034':'Ear02.L','Bone_033':'Ear02.L','Bone_038':'Ear01.R','Bone_037':'Ear02.R','Bone_036':'Ear02.R','Bone_040':'Scarf01','Bone_039':'Scarf02'})
tail={i for p in me.polygons if me.attributes['edit_region'].data[p.index].value==3 for i in p.vertices};cuff={i for p in me.polygons if me.attributes['edit_region'].data[p.index].value==4 for i in p.vertices}
deform={g.index:g.name for g in ob.vertex_groups if g.name in rig.data.bones and rig.data.bones[g.name].use_deform}
def smooth(t):
    t=max(0,min(1,t));return t*t*(3-2*t)
for v in me.vertices:
    if me.attributes['anatomy_part'].data[v.index].value!=0 or v.index in tail:continue
    _,idx,dist=kd.find(v.co);near=kd.find_range(v.co,.00002) if dist<.00002 else [(None,idx,dist)];w={}
    for _,i,_ in near:
        for g in ref.data.vertices[i].groups:
            n=mapping.get(ref.vertex_groups[g.group].name,'Hips');w[n]=w.get(n,0)+g.weight/len(near)
    for side in ['Left','Right']:
        x=w.pop(side+'LowerArm',0)+w.pop(side+'Hand',0)
        for n in list(w):
            if n.startswith(side) and any(s in n for s in ['Proximal','Intermediate','Distal']):x+=w.pop(n)
        w[side+'UpperArm']=w.get(side+'UpperArm',0)+x
    side='Left' if v.co.x>0 else 'Right';t=me.attributes['elbow_t'].data[v.index].value
    if v.index in cuff or (t>=0 and t<.1) or (abs(v.co.x)>.30 and -.035<v.co.z<.26 and w.get(side+'UpperArm',0)>.70):w={side+'UpperArm':1}
    if v.co.z>-.17:
        x=sum(w.pop(n,0) for n in list(w) if 'Leg' in n or 'Foot' in n or 'Toes' in n);w['Hips']=w.get('Hips',0)+x
    w=dict(sorted(((n,x) for n,x in w.items() if x>1e-6),key=lambda p:p[1],reverse=True)[:4]);total=sum(w.values())
    for g in list(v.groups):
        if g.group in deform:ob.vertex_groups[g.group].remove([v.index])
    for n,x in w.items():ob.vertex_groups[n].add([v.index],x/total,'REPLACE')
exec(compile(open('C:/Users/iam12/Train Survival/tmp/raccoon_plan/refine_hip_weights.py',encoding='utf-8').read(),'refine_hip_weights.py','exec'))
bpy.context.view_layer.update()
