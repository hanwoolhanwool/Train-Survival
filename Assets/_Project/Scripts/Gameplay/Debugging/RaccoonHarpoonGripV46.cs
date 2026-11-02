using UnityEngine;
namespace Game.Gameplay.Debugging
{
    /// <summary>Review-only launcher placement using the accepted revolver C hand and body pose.</summary>
    public static class RaccoonHarpoonGripV46
    {
        static readonly RaccoonActionLibraryV36.Entry baseline=new RaccoonActionLibraryV36.Entry
            {Id="RevolverIdle",Prop="Revolver",Loop=true,Duration=1};
        // Neutral player frame: 8 mm left and 5 mm up from the initial v46 fit.
        static readonly Vector3 socket=new Vector3(-.007696111f,.139999900f,-.125642700f);
        static readonly Quaternion rotation=new Quaternion(-.000000464f,-.154384345f,-.000000390f,-.988010900f);
        public static RaccoonActionTargetsV36.Goals Targets(RaccoonActionLibraryV36.Entry entry,float phase,Vector3[] hands,Vector3[] feet)
        {
            var g=RaccoonActionTargetsV36.Evaluate(baseline,0,hands,feet);
            if(entry.Id=="HarpoonFire")
            {
                float recoil=phase<.10f?Mathf.SmoothStep(0,1,phase/.10f):1-Mathf.SmoothStep(0,1,(phase-.10f)/.90f);
                g.Right+=new Vector3(0,.04f,-.035f)*recoil;
            }
            if(entry.Id=="HarpoonReel")
                g.Right+=new Vector3(0,.005f,-.025f)*(.5f-.5f*Mathf.Cos(phase*Mathf.PI*2));
            return g;
        }
        public static void Apply(Animator animator,GameObject[] props,string id,float phase,float weight,float returnBlend)
        {
            bool reel=id=="HarpoonReel",fire=id=="HarpoonFire";
            RaccoonRevolverGripV45.ApplyFingers(animator,fire||reel?"RevolverFire":"RevolverIdle",
                reel?.2f:phase,reel?weight:returnBlend,true);
            if(props==null)return;
            foreach(var prop in props)if(prop!=null&&prop.name=="Harpoon"&&prop.activeSelf)
            {
                prop.transform.SetLocalPositionAndRotation(socket,rotation);
                prop.transform.localScale=Vector3.one*65;
            }
        }
    }
}
