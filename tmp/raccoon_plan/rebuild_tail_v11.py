import bpy,pathlib,json,heapq
from mathutils import Vector
project=pathlib.Path('C:/Users/iam12/Train Survival')
review=bpy.data.scenes.new('05 v11 꼬리 제작 확인');review.world=bpy.context.scene.world;review.render.fps=60;review.frame_start=1;review.frame_end=150
for name in ['COMPARE_v9_new_Raccoon_측면','COMPARE_v9_new_Rig_측면']:
 ob=bpy.data.objects[name]
 if name not in review.objects:review.collection.objects.link(ob)
 if ob.parent and ob.parent.name not in review.objects:review.collection.objects.link(ob.parent)
oldcam=bpy.context.scene.camera
cam=oldcam.copy();cam.data=oldcam.data.copy();review.collection.objects.link(cam);review.camera=cam
bpy.context.window.scene=review;review.frame_set(20)
code=(project/'tmp/raccoon_plan/rebuild_tail_v10.py').read_text(encoding='utf-8').replace('v10','v11')
code=code.replace("(0,-.10,-.195,.15,.16),(0,.005,-.195,.15,.16),(0,.13,-.245,.175,.18),(0,.28,-.39,.225,.235)","(0,-.10,-.195,.085,.09),(0,.015,-.195,.075,.08),(0,.13,-.245,.13,.14),(0,.28,-.39,.205,.215)")
exec(compile(code,'rebuild_tail_v11.generated.py','exec'))
exec(compile((project/'tmp/raccoon_plan/close_tail_opening_v11.py').read_text(encoding='utf-8'),'close_tail_opening_v11.py','exec'))

