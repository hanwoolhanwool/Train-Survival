import bpy, pathlib, traceback
root=pathlib.Path('C:/Users/iam12/Train Survival')
status=root/'output/raccoon-support-v17/import-status.txt'
try:
    bpy.context.window.scene=bpy.data.scenes['19 v16 받침손 · 정지']
    for scene in list(bpy.data.scenes):
        if scene.name.startswith(('24 v17','25 v17','26 v17','27 v17','28 v17')):
            for obj in list(scene.objects):
                bpy.data.objects.remove(obj,do_unlink=True)
            bpy.data.scenes.remove(scene)
    status.write_text('Importing final runtime capture')
    for script in ['support_review_v17.py','compare_support_v17.py']:
        path=root/'tmp/raccoon_plan'/script
        exec(compile(path.read_text(encoding='utf-8-sig'),str(path),'exec'),{})
    status.write_text('complete: final runtime capture and comparison')
except Exception:
    status.write_text(traceback.format_exc())
