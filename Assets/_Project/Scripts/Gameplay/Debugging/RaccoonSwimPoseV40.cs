using UnityEngine;
namespace Game.Gameplay.Debugging
{
    // Direction is relative to the character's facing, not a rotation of the whole model.
    // There is no sprint/dash branch in the production swimming motor.
    public static class RaccoonSwimPoseV40
    {
        public static RaccoonActionTargetsV36.Goals Evaluate(string id,float phase,Vector3[] feet)
        {
            bool idle=id=="SwimIdle",back=id=="SwimBack";
            int direction=id=="SwimLeft"?-1:id=="SwimRight"?1:0;
            float q=phase*Mathf.PI*2;
            var g=new RaccoonActionTargetsV36.Goals{Drop=idle?.025f:.045f,
                Torso=new Vector3(idle?3:back?-5:direction!=0?5:19,direction*7,-direction*5)};
            for(int side=0;side<2;side++)
            {
                bool left=side==0;float sign=left?-1:1;
                float p=q+(left?0:Mathf.PI),s=Mathf.Sin(p),c=Mathf.Cos(p);
                Vector3 hand;
                if(idle)hand=new Vector3(sign*(.39f+.045f*c),.96f+.016f*s,.27f+.035f*s);
                else if(direction!=0)
                {
                    bool leading=sign==direction;
                    hand=new Vector3(sign*(leading?.46f:.34f)-direction*.075f*s,
                        .99f+.035f*c,(leading?.24f:.33f)+.065f*c);
                }
                else if(back)hand=new Vector3(sign*(.35f+.05f*c),.98f+.025f*c,.30f+.115f*s);
                else hand=new Vector3(sign*(.32f+.055f*s),.98f+.035f*s,.33f+.135f*c);
                float lift=.24f+(idle?.025f:.045f)*Mathf.Sin(p-.35f);
                Vector3 kick=feet[side]+new Vector3(direction*.055f*s,lift,(back?.08f:idle?-.06f:-.18f)+(idle?.025f:.06f)*c);
                if(left){g.Left=hand;g.LeftFoot=kick;}else{g.Right=hand;g.RightFoot=kick;}
            }
            return g;
        }
        public static Vector3 PalmNormal(string id,float phase,bool left)
        {
            float p=phase*Mathf.PI*2+(left?0:Mathf.PI);
            if(id=="SwimIdle")return new Vector3((left?-1:1)*.28f*Mathf.Sin(p),-.92f,-.25f).normalized;
            Vector3 pressure=id=="SwimBack"?Vector3.forward:id=="SwimLeft"?Vector3.right:id=="SwimRight"?Vector3.left:Vector3.back;
            float power=id=="SwimMove"?.5f+.5f*Mathf.Sin(p):.5f+.5f*Mathf.Cos(p);
            return Vector3.Slerp(Vector3.down,pressure,.25f+.65f*power);
        }
        public static Quaternion FootOffset(string id,float phase,bool left)
        {
            float p=phase*Mathf.PI*2+(left?0:Mathf.PI);
            return Quaternion.Euler((id=="SwimIdle"?12:22)+5*Mathf.Sin(p-.35f),0,0);
        }
        public static Vector3 ElbowPole(bool left)=>new Vector3(left?-.45f:.45f,-.06f,-.18f);
    }
}
