using UnityEngine;
namespace Game.Gameplay.Debugging
{
    // Authored trajectory targets, shared by clip baking and runtime contact correction.
    public static class RaccoonActionTargetsV36
    {
        static float S(float x)=>Mathf.SmoothStep(0,1,Mathf.Clamp01(x));
        static float Pulse(float t,float peak=.3f)=>t<peak?S(t/peak):1-S((t-peak)/(1-peak));
        static float Climb(float phase){if(phase<.65f)return phase;float u=(phase-.65f)/.35f;return (2*u*u*u-3*u*u+1)*.65f+(2*u*u*u-3*u*u+u)*.35f;}
        static float ReturnArc(float phase)=>phase<.65f?0:Mathf.Pow(Mathf.Sin(Mathf.PI*(phase-.65f)/.35f),2);
        public struct Goals {public Vector3 Left,Right,LeftFoot,RightFoot,Torso;public float Drop;}
        public static Goals Evaluate(RaccoonActionLibraryV36.Entry entry,float t,Vector3[] hands,Vector3[] fp)
        {
            if(entry.Id.StartsWith("Swim"))return RaccoonSwimPoseV40.Evaluate(entry.Id,t,fp);
            if(entry.KeepWeapon&&RaccoonShotgunActionsV41.IsAction(entry.Id))return RaccoonShotgunActionsV41.Goals(entry.Id,t,hands,fp);
            if(!entry.KeepWeapon&&!entry.UpperBody)hands=new[]{new Vector3(-.44f,.73f,.04f),new Vector3(.44f,.73f,.04f)};
            float q=2*Mathf.PI*t,sin=Mathf.Sin(q),cos=Mathf.Cos(q),env=entry.Loop?1:Pulse(t);
                    Vector3 left=hands[0],right=hands[1],lf=fp[0],rf=fp[1],torso=Vector3.zero;float drop=0;
                    string id=entry.Id;
                    bool solveHands=!entry.UpperBody;
                    if(entry.UpperBody)
                    {float hit=Pulse(t,.18f);torso=id=="HitFront"?new Vector3(-9,0,0):id=="HitBack"?new Vector3(10,0,0):new Vector3(0,0,id=="HitLeft"?-10:10);torso*=hit;}
                    else
                    {
                        if(id=="Respawn"){drop=.07f*(1-S(t));torso.x=5*(1-S(t));}
                        if(id=="RifleFire"){float recoil=Pulse(t,.07f);left+=new Vector3(0,.012f,-.035f)*recoil;right+=new Vector3(0,.012f,-.035f)*recoil;torso.x=-2*recoil;}
                        if(id=="RifleReload"){left=Vector3.Lerp(hands[0],new Vector3(.04f,1.12f,.36f),env);torso.x=3*env;}
                        if(id=="Equip"||id=="Holster")
                        {float v=id=="Equip"?1-S(t):S(t);var off=new Vector3(.025f,-.12f,-.10f)*v;left+=off;right+=off;torso.y=-4*v;}
                        if(id=="Interact"){right=Vector3.Lerp(hands[1],new Vector3(.22f,.96f,.55f),env);torso.x=4*env;}
                        if(id=="Pickup"){drop=.25f*env;torso.x=16*env;right=Vector3.Lerp(hands[1],new Vector3(.22f,.50f,.40f),env);left=Vector3.Lerp(hands[0],new Vector3(-.30f,.62f,.25f),env);}
                        if(id=="Repair")
                        {float stroke=RaccoonActionHandsV37.RepairLift(t);right=Vector3.Lerp(new Vector3(.40f,1.03f,.45f),new Vector3(.46f,1.13f,.30f),stroke);torso.x=2*(1-stroke);torso.y=-2*stroke;}
                        if(id=="HammerIdle")right=new Vector3(.43f,.82f,.20f);
                        if(id=="Melee")
                        {var wind=new Vector3(.32f,1.4f,.23f);var strike=new Vector3(.15f,1.04f,.59f);right=t<.25f?Vector3.Lerp(hands[1],wind,S(t/.25f)):t<.5f?Vector3.Lerp(wind,strike,S((t-.25f)/.25f)):Vector3.Lerp(strike,hands[1],S((t-.5f)/.5f));torso.y=-16*Pulse(t,.35f);left=Vector3.Lerp(hands[0],new Vector3(-.27f,1.1f,.28f),env);}
                        if(id=="Eat"){right=Vector3.Lerp(hands[1],new Vector3(.10f,1.34f,.28f),env);torso.x=3*env;}
                        if(id=="Carry"){left=new Vector3(-.25f,1.00f,.36f);right=new Vector3(.25f,1.00f,.36f);torso.x=-4+.5f*sin;}
                        if(id.StartsWith("Harpoon")){left=new Vector3(-.09f,1.10f,.54f);right=new Vector3(.14f,1.13f,.27f);float v=id=="HarpoonFire"?Pulse(t,.12f):id=="HarpoonReel"?.5f-.5f*cos:0;left+=Vector3.back*.035f*v;right+=Vector3.back*.035f*v;torso.x=-3*v;}
                        if(id.StartsWith("Revolver"))
                        {right=new Vector3(.17f,1.20f,.49f);left=new Vector3(-.12f,1.08f,.25f);if(id=="RevolverFire")right+=new Vector3(0,.04f,-.035f)*Pulse(t,.10f);else if(id=="RevolverReload"){right=Vector3.Lerp(right,new Vector3(.17f,1.08f,.33f),env);left=Vector3.Lerp(left,new Vector3(.08f,1.07f,.35f),env);}}
                        if(id.StartsWith("Ladder"))
                        {float p=id=="LadderHold"?0:Mathf.Repeat(id=="LadderDown"?1-t:t,1),r=Mathf.Repeat(p+.5f,1);drop=-.06f;left=new Vector3(-.24f,1.46f-.26f*Climb(p),.43f-.07f*ReturnArc(p));right=new Vector3(.24f,1.46f-.26f*Climb(r),.43f-.07f*ReturnArc(r));lf+=new Vector3(0,.29f-.26f*Climb(r),.30f-.08f*ReturnArc(r));rf+=new Vector3(0,.29f-.26f*Climb(p),.30f-.08f*ReturnArc(p));torso.x=5;}
                        if(id=="Mantle"){drop=-.17f*env;left=Vector3.Lerp(hands[0],new Vector3(-.3f,1.23f,.43f),env);right=Vector3.Lerp(hands[1],new Vector3(.3f,1.23f,.43f),env);lf+=new Vector3(0,.30f,.22f)*env;rf+=new Vector3(0,.20f,.07f)*env;torso.x=14*env;}
                        if(id=="Grabbed"){left=new Vector3(-.25f,1.21f+.035f*sin,.43f);right=new Vector3(.25f,1.21f-.035f*sin,.43f);torso.y=7*sin;drop=.025f*(1-cos);}
                        if(id=="Carried"){drop=-.28f;left=new Vector3(-.39f,1.10f+.025f*sin,.22f);right=new Vector3(.39f,1.10f-.025f*sin,.22f);lf+=new Vector3(0,.37f,.1f);rf+=new Vector3(0,.33f,.14f);torso.z=12;}
                        if(id.StartsWith("Fishing"))
                        {right=new Vector3(.20f,1.12f,.38f);left=new Vector3(-.08f,1.10f,.37f);if(id=="FishingCast"){float cast=Pulse(t,.35f);right+=new Vector3(0,.24f,-.17f)*cast;left+=new Vector3(0,.18f,-.12f)*cast;torso.x=-6*cast;}if(id=="FishingReel")left+=new Vector3(.025f*cos,.025f*sin,0);}
                    }
            return new Goals{Left=left,Right=right,LeftFoot=lf,RightFoot=rf,Torso=torso,Drop=drop};
        }
    }
}



