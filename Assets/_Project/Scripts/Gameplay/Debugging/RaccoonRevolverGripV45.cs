using UnityEngine;
namespace Game.Gameplay.Debugging
{
    /// <summary>Review-only revolver fit. Keeps the original weapon mesh and other equipment poses.</summary>
    public static class RaccoonRevolverGripV45
    {
        static readonly Vector3 socket=new Vector3(-0.017833866f,0.146799800f,-0.137620200f);
        // FittedSocket-space equivalent of 12 mm toward the player's left in the neutral grip.
        // Keeping it in socket space makes the offset follow the hand through every action.
        static readonly Vector3 leftOffset=new Vector3(.011427980f,0,-.003660802f);
        static readonly Quaternion rotation=new Quaternion(-0.000000464f,-0.154384345f,-0.000000390f,-0.988010900f);
        static readonly Quaternion[] grip={
            new Quaternion(-0.826017200f,-0.527600000f,0.161342600f,-0.115335740f),
            new Quaternion(0.097532840f,0.241717830f,0.543209300f,-0.798112500f),
            new Quaternion(0.229123160f,0.095667110f,0.209077835f,-0.945852458f),
            new Quaternion(0.282586575f,-0.633375300f,-0.505531900f,-0.513242800f),
            new Quaternion(-0.438180476f,-0.061121210f,0.002168685f,-0.896804000f),
            new Quaternion(-0.248459533f,-0.045405097f,0.019876285f,-0.967373431f),
            new Quaternion(0.204398647f,-0.822739065f,0.201183930f,-0.490761369f),
            new Quaternion(-0.544498800f,0.076908770f,-0.196709290f,-0.811733663f),
            new Quaternion(-0.512971640f,-0.118376777f,0.084480640f,-0.845996500f),
            new Quaternion(-0.210893974f,-0.713232160f,0.196419626f,-0.638938900f),
            new Quaternion(-0.463061631f,-0.104773678f,0.051657036f,-0.878594300f),
            new Quaternion(-0.520058600f,-0.006167518f,-0.086222750f,-0.849745100f),
            new Quaternion(-0.421297669f,-0.531950000f,0.216194689f,-0.701995300f),
            new Quaternion(-0.350660700f,-0.262278825f,0.211731100f,-0.873737300f),
            new Quaternion(-0.498449177f,0.092249010f,-0.211497300f,-0.835648000f)
        };
        static readonly Quaternion[] trigger={
            new Quaternion(0.462127537f,-0.514041662f,-0.372689800f,-0.619113500f),
            new Quaternion(-0.653041300f,-0.159072474f,0.230654716f,-0.703584731f),
            new Quaternion(-0.299511522f,-0.066093475f,0.095320630f,-0.947015600f)
        };
        // C-only refinement: keep the index along the frame and lower the last two fingers slightly.
        static readonly Quaternion[] cIndex={new Quaternion(0.113413595f,-0.756513700f,-0.220767483f,-0.605050400f),new Quaternion(-0.225549608f,-0.072936560f,0.014893865f,-0.971383452f),new Quaternion(-0.046703970f,-0.048956167f,0.028255835f,-0.997308200f)};
        static readonly Quaternion[] cTrigger={new Quaternion(0.429242969f,-0.639360666f,-0.195152432f,-0.607358300f),new Quaternion(-0.585132062f,-0.080913150f,0.129575900f,-0.796419263f),new Quaternion(-0.363758057f,-0.047830578f,0.061632797f,-0.928220749f)};
        static readonly Quaternion cRing=new Quaternion(-0.167372137f,-0.705832064f,0.221497416f,-0.651710500f),cLittle=new Quaternion(-0.362316800f,-0.525257500f,0.236067384f,-0.732873300f);
        public static Vector3 ReloadLeft(float phase)
        {
            float reach=phase<.3f?Mathf.SmoothStep(0,1,phase/.3f):1-Mathf.SmoothStep(0,1,(phase-.3f)/.7f);
            return Vector3.Lerp(new Vector3(-.12f,1.08f,.25f),new Vector3(-.06f,1.02f,.43f),reach);
        }
        public static void ApplyOriginalSocket(Animator animator,GameObject[] props)
        {
            var equipment=animator.GetComponent<Game.Gameplay.Player.RaccoonEquipmentPose>();
            Game.Gameplay.Player.RaccoonEquipmentProfile.Entry entry;
            if(equipment==null||equipment.Profile==null||props==null||
                !equipment.Profile.TryGetEntry(Game.Gameplay.Inventory.HotbarItemType.Revolver,out entry))return;
            // The unmodified animation supplies the original fingers. Restore the original attachment too.
            foreach(var prop in props)if(prop!=null&&prop.name=="Revolver"&&prop.activeSelf)
                prop.transform.SetLocalPositionAndRotation(entry.Socket.Position,entry.Socket.Rotation);
        }
        public static void Apply(Animator animator,GameObject[] props,string id,float phase,float weight,bool shiftLeft=false)
        {
            ApplyFingers(animator,id,phase,weight,shiftLeft);
            if(props==null)return;
            foreach(var prop in props)if(prop!=null&&prop.name=="Revolver"&&prop.activeSelf)
                prop.transform.SetLocalPositionAndRotation(socket+(shiftLeft?leftOffset:Vector3.zero),rotation);
        }
        public static void ApplyFingers(Animator animator,string id,float phase,float weight,bool useC)
        {
            // Index stays along the frame at rest/reload; only the firing action curls it.
            float press=id=="RevolverFire"
                ?Mathf.SmoothStep(0,1,phase/.09f)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,.75f,phase)))*weight:0;
            for(int i=0;i<grip.Length;i++)
            {
                var bone=animator.GetBoneTransform((HumanBodyBones)((int)HumanBodyBones.RightThumbProximal+i));
                var resting=useC?(i>=3&&i<6?cIndex[i-3]:i==9?cRing:i==12?cLittle:grip[i]):grip[i];
                if(bone!=null)bone.localRotation=i>=3&&i<6?Quaternion.Slerp(resting,(useC?cTrigger:trigger)[i-3],press):resting;
            }
        }
    }
}

