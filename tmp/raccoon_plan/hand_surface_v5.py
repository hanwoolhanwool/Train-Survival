import bpy,bmesh
from mathutils import Vector
for name in ['raccoon','raccoon_GripReview_v4']:
    ob=bpy.data.objects[name]
    if ob.get('hand_surface_v5'):continue
    me=ob.data;bm=bmesh.new();bm.from_mesh(me)
    tag=bm.verts.layers.int['anatomy_part'];regions={100,101,102,103,104,105,200,201,202,203,204,205}
    edges=[e for e in bm.edges if all(v[tag] in regions for v in e.verts)]
    bmesh.ops.subdivide_edges(bm,edges=edges,cuts=1,use_grid_fill=True)
    shapes=list(bm.verts.layers.shape.values())
    for iteration in range(3):
        result={}
        for v in bm.verts:
            if v[tag] not in regions:continue
            side=1 if v.co.x>0 else -1
            w=Vector((side*.640,-.302,-.148));c=Vector((side*.509,-.256,.035));d=(w-c).normalized()
            fade=min(1,max(0,(v.co-w).dot(d)/.025))
            neighbors=[e.other_vert(v) for e in v.link_edges]
            average=sum((n.co for n in neighbors),Vector())/len(neighbors)
            result[v]=(average-v.co)*(.48*fade)
        for v,delta in result.items():
            v.co+=delta
            for layer in shapes:v[layer]+=delta
    deform=bm.verts.layers.deform.active
    rig=bpy.data.objects['RIG_Raccoon']
    groups={g.index for g in ob.vertex_groups if g.name in rig.data.bones and rig.data.bones[g.name].use_deform}
    for v in bm.verts:
        weights=sorted([(i,w) for i,w in v[deform].items() if i in groups],key=lambda x:-x[1])
        for i,w in weights:v[deform][i]=0
        total=sum(w for i,w in weights[:4])
        for i,w in weights[:4]:v[deform][i]=w/total
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(me);bm.free();me.update()
    tags=me.attributes['anatomy_part']
    normals=[tuple(n.vector) for n in me.corner_normals]
    for loop in me.loops:
        if tags.data[loop.vertex_index].value in regions:normals[loop.index]=(0,0,0)
    me.normals_split_custom_set(normals)
    ob['hand_surface_v5']=True
    print(name,len(me.vertices),len(me.polygons),list(me.shape_keys.key_blocks.keys()))
bpy.context.view_layer.update()
