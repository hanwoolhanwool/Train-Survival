import bpy, math
from mathutils.kdtree import KDTree
ob=bpy.data.objects['raccoon'];me=ob.data;rig=bpy.data.objects['RIG_Raccoon'];src=bpy.data.objects['raccoon_SOURCE_before_stage12'];ref=bpy.data.objects['Mesh_0.001']
kd=KDTree(len(src.data.vertices))
for v in src.data.vertices:kd.insert(v.co,v.index)
kd.balance()
tail={i for p in me.polygons if me.attributes['edit_region'].data[p.index].value==3 for i in p.vertices}
deform={g.index:g.name for g in ob.vertex_groups if g.name in rig.data.bones and rig.data.bones[g.name].use_deform}
def smooth(t):
    t=max(0,min(1,t));return t*t*(3-2*t)
for v in me.vertices:
    if v.index in tail or me.attributes['anatomy_part'].data[v.index].value!=0 or v.co.z>=-.05:continue
    _,idx,dist=kd.find(v.co);near=kd.find_range(v.co,.00002) if dist<.00002 else [(None,idx,dist)]
    w={}
    for _,i,_ in near:
        for g in ref.data.vertices[i].groups:
            n=ref.vertex_groups[g.group].name
            if n not in rig.data.bones or not rig.data.bones[n].use_deform:n='Hips'
            w[n]=w.get(n,0)+g.weight/len(near)
    factor=smooth((-.08-v.co.z)/.30)
    for n in list(w):
        if 'UpperLeg' in n or 'LowerLeg' in n:
            transfer=w[n]*(1-factor);w[n]-=transfer;w['Hips']=w.get('Hips',0)+transfer
    w=dict(sorted(w.items(),key=lambda x:x[1],reverse=True)[:4]);total=sum(w.values())
    for g in list(v.groups):
        if g.group in deform:ob.vertex_groups[g.group].remove([v.index])
    for n,x in w.items():
        if x>1e-6:ob.vertex_groups[n].add([v.index],x/total,'REPLACE')
bpy.context.view_layer.update();print('Continuous hip-to-thigh transition restored.')
