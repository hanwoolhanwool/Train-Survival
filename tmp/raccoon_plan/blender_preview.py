import bpy
from mathutils import Vector
def preview(obj, out, views, size=900, wire=False, hand_only=False):
    scene=bpy.data.scenes.new('_RaccoonReview_Temporary')
    meshobj=obj.copy(); scene.collection.objects.link(meshobj);meshobj.location=(0,0,0);meshobj.hide_render=False
    tempmesh=None
    if hand_only:
        import bmesh
        tempmesh=obj.data.copy();meshobj.data=tempmesh
        bm=bmesh.new();bm.from_mesh(tempmesh);bmesh.ops.delete(bm,geom=[v for v in bm.verts if v.co.x<.43 or v.co.z<-.48 or v.co.z>.10],context='VERTS');bm.to_mesh(tempmesh);bm.free()
    cd=bpy.data.cameras.new('_RaccoonReviewCamera');ca=bpy.data.objects.new('_RaccoonReviewCamera',cd);scene.collection.objects.link(ca);scene.camera=ca
    cd.type='ORTHO'
    scene.render.engine='CYCLES';scene.cycles.samples=24
    scene.render.resolution_x=size;scene.render.resolution_y=size;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG'
    scene.view_settings.view_transform='AgX'
    w=bpy.data.worlds.new('_RaccoonReviewWorld');scene.world=w;w.use_nodes=True
    next(n for n in w.node_tree.nodes if n.type=='BACKGROUND').inputs['Color'].default_value=(0.30,0.30,0.30,1)
    lamps=[]
    for loc,power,area in [((2,-4,4),400,4),((-3,-2,2),230,3),((1,3,3),450,3)]:
        ld=bpy.data.lights.new('_RaccoonReviewLight','AREA');ld.energy=power;ld.shape='DISK';ld.size=area
        lo=bpy.data.objects.new('_RaccoonReviewLight',ld);scene.collection.objects.link(lo);lo.location=loc;lo.rotation_euler=(-lo.location).to_track_quat('-Z','Y').to_euler();lamps.append((lo,ld))
    try:
        for label,direction,aim,ortho in views:
            ca.location=Vector(aim)+Vector(direction).normalized()*4;ca.rotation_euler=(Vector(aim)-ca.location).to_track_quat('-Z','Y').to_euler();cd.ortho_scale=ortho
            scene.render.filepath=str(out)+'/'+label+'.png';bpy.ops.render.render(write_still=True,scene=scene.name)
    finally:
        bpy.data.objects.remove(meshobj,do_unlink=True);bpy.data.objects.remove(ca,do_unlink=True);bpy.data.cameras.remove(cd)
        if tempmesh:bpy.data.meshes.remove(tempmesh)
        for lo,ld in lamps:bpy.data.objects.remove(lo,do_unlink=True);bpy.data.lights.remove(ld)
        bpy.data.scenes.remove(scene);bpy.data.worlds.remove(w)
