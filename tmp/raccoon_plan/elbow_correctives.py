import bpy,json,math
from mathutils import Vector,Quaternion,Matrix
ob=bpy.data.objects['raccoon'];me=ob.data
def smooth(x):x=max(0,min(1,x));return x*x*(3-2*x)
def weight(t):return smooth((t-.04)/.70)
if me.shape_keys is None:ob.shape_key_add(name='Basis')
for rec in json.loads(ob['elbow_layout_json']):
 side=rec['side'];suffix='L' if side>0 else 'R';name='COR_Elbow90.'+suffix
 if name in me.shape_keys.key_blocks:ob.shape_key_remove(me.shape_keys.key_blocks[name])
 key=ob.shape_key_add(name=name);key.value=0
 P,B,C,W=[Vector(rec[n]) for n in ['root','control1','control2','wrist']];E=Vector(rec['elbow'])
 def center(t):return P*(1-t)**3+B*(3*t*(1-t)**2)+C*(3*t*t*(1-t))+W*t**3
 def tangent(t):return ((B-P)*3*(1-t)**2+(C-B)*6*t*(1-t)+(W-C)*3*t*t).normalized()
 axis=(W-E).normalized().cross(Vector((0,-1,0))).normalized();R=Quaternion(axis,math.pi/2).to_matrix();I=Matrix.Identity(3)
 def pose(p,t):w=weight(t);return p*(1-w)+(E+R@(p-E))*w
 for v in me.vertices:
  if me.attributes['anatomy_part'].data[v.index].value!=(110 if side>0 else 210):continue
  t=me.attributes['elbow_t'].data[v.index].value
  if not .18<t<.76:continue
  envelope=math.exp(-((t-.39)/.15)**2)*smooth((t-.18)/.08)*smooth((.76-t)/.10)
  A=I*(1-weight(t))+R*weight(t)
  rest=v.co-center(t);front=Vector((0,-1,0));front=(front-tangent(t)*front.dot(tangent(t))).normalized()
  facing=rest.normalized().dot(front);offset=A@rest;transverse=axis*offset.dot(axis)
  scale=1+envelope*(.20*max(0,-facing)**2-.35*max(0,facing)**2)
  desired=transverse*(1+.25*envelope)+(offset-transverse)*scale
  key.data[v.index].co=v.co+A.inverted()@(desired-offset)
 key.slider_min=0;key.slider_max=1
ob['elbow_corrective_notes']='COR_Elbow90.L/R: default 0; tested at 90 deg with linear skinning. QA lower-arm weight smoothstep((t-.04)/.70), elbow pivot in elbow_layout_json. Connect and retune during final rigging; no automatic drivers yet.'
print('Created bilateral elbow flexion correctives')



