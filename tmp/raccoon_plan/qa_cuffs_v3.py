import json
QA_RENDER=globals().get('CUFF_RENDER',False);QA_SAVE=globals().get('CUFF_SAVE',False);QA_ANGLES=globals().get('CUFF_ANGLES',[0,45,60,75,90])
s=open('C:/Users/iam12/Train Survival/tmp/raccoon_plan/qa_elbow_poses.py',encoding='utf-8').read()
s=s.replace('elbow_pose_report.json','cuff_v3_pose_report.json').replace("f'elbow_pose_","f'cuff_v3_pose_").replace('Raccoon_Elbow_PoseQA.blend','Raccoon_Cuff_v3_PoseQA.blend')
extra="""
   alltris=[t for t in posedmesh.loop_triangles if posedmesh.attributes['edit_region'].data[t.polygon_index].value in [2,4]]
   tv=[tuple(t.vertices) for t in alltris]
   kinds=[posedmesh.attributes['edit_region'].data[t.polygon_index].value for t in alltris]
   crossbvh=BVHTree.FromPolygons([v.co for v in posedmesh.vertices],tv,all_triangles=True,epsilon=0)
   cuff_cross=[(i,j) for i,j in crossbvh.overlap(crossbvh) if i<j and kinds[i]!=kinds[j] and not set(tv[i]).intersection(tv[j])]
"""
s=s.replace('   locations=',extra+'   locations=')
s=s.replace("'intersection_positions':locations}","'intersection_positions':locations,'arm_cuff_intersections':len(cuff_cross)}")
s=s.replace('print(json.dumps(results))',"print([(r['angle_degrees'],r['arm_nonadjacent_triangle_intersections'],r['arm_cuff_intersections']) for r in results])")
exec(compile(s,'qa_cuffs_v3_runtime.py','exec'))
