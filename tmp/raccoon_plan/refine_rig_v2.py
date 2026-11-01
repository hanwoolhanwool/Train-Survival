import bpy,math,json
from mathutils import Vector
ob=bpy.data.objects['raccoon'];me=ob.data;rig=bpy.data.objects['RIG_Raccoon']
deform={g.index:g.name for g in ob.vertex_groups if g.name in rig.data.bones and rig.data.bones[g.name].use_deform}
regions=[set() for v in me.vertices]
for p in me.polygons:
    for i in p.vertices:regions[i].add(me.attributes['edit_region'].data[p.index].value)
def smooth(t):
    t=max(0,min(1,t));return t*t*(3-2*t)
for v in me.vertices:
    w={deform[g.group]:g.weight for g in v.groups if g.group in deform};before=w.copy()
    if me.attributes['anatomy_part'].data[v.index].value==0:
        for n in list(w):
            if n.startswith('Tail'):
                factor=0 if 3 not in regions[v.index] else smooth((v.co.y+.056)/.11)*smooth((-.065-v.co.z)/.17)
                x=w[n]*(1-factor);w[n]-=x;w['Hips']=w.get('Hips',0)+x
        sign=1 if v.co.x>0 else -1;side='Left' if sign==1 else 'Right'
        d=(v.co-Vector((sign*.303,-.115,-.613))).length
        alpha=math.exp(-((d/.075)**4))
        if d<.16:
            w={n:x*(1-alpha) for n,x in w.items()};w[side+'LowerLeg']=w.get(side+'LowerLeg',0)+alpha*.62;w[side+'UpperLeg']=w.get(side+'UpperLeg',0)+alpha*.38
    if w==before:continue
    w=dict(sorted(((n,x) for n,x in w.items() if x>1e-6),key=lambda p:p[1],reverse=True)[:4]);total=sum(w.values())
    for g in list(v.groups):
        if g.group in deform:ob.vertex_groups[g.group].remove([v.index])
    for n,x in w.items():ob.vertex_groups[n].add([v.index],x/total,'REPLACE')
ob.update_tag();bpy.context.view_layer.update()
