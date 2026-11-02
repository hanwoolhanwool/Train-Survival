using UnityEngine;
namespace Game.Gameplay.Debugging
{
    /// <summary>Local review presentation only. Never consumes ammo or deals damage.</summary>
    public sealed class RaccoonWeaponMechanicsV36:MonoBehaviour
    {
        public Transform Gun,Pump,Casing,Bolt;
        public AudioClip FireSound,OpenSound,CloseSound,InsertSound,ImpactSound;
        public AudioSource Source;
        public int Ejections{get;private set;}
        public int Insertions{get;private set;}
        public int SoundEvents{get;private set;}
        private float casingAge=10;
        private Vector3 casingStart,casingVelocity;
        private Quaternion casingRotation;
        private bool impacted;
        private void Sound(AudioClip clip,float gain){if(Source!=null&&clip!=null){Source.PlayOneShot(clip,gain);SoundEvents++;}}
        public void ResetVisuals(){Ejections=Insertions=SoundEvents=0;casingAge=10;if(Casing!=null)Casing.gameObject.SetActive(false);Apply(0);}
        public void Shot()=>Sound(FireSound,.55f);
        public void Step(float dt,float previousShot,float shot,float previousReload,float reload,int shells)
        {
            if(previousShot<.20f&&shot>=.20f){Sound(OpenSound,.4f);Eject();}
            if(previousShot<.47f&&shot>=.47f)Sound(CloseSound,.45f);
            if(previousReload<.92f&&reload>=.92f)Sound(OpenSound,.35f);
            if(previousReload<.995f&&reload>=.995f)Sound(CloseSound,.4f);
            for(int i=0;i<shells;i++)
            {
                float threshold=.18f+.64f*(i+.94f)/shells;
                if(previousReload<threshold&&reload>=threshold){Insertions++;Sound(InsertSound,.4f);}
            }
            casingAge+=dt;
            if(Casing!=null&&casingAge<1.2f)
            {
                float t=casingAge;
                Vector3 p=casingStart+casingVelocity*t+Vector3.down*4.9f*t*t;
                float floor=transform.position.y+.025f;
                if(p.y<floor){p.y=floor;if(!impacted){impacted=true;Sound(ImpactSound,.18f);}}
                Casing.position=p;if(!impacted)Casing.rotation=casingRotation*Quaternion.Euler(t*500,t*280,t*140);
            }
            else if(Casing!=null)Casing.gameObject.SetActive(false);
        }
        private void Eject()
        {
            if(Gun==null||Casing==null)return;
            Ejections++;impacted=false;casingAge=0;
            casingStart=Gun.TransformPoint(new Vector3(-.00062f,.0009f,.00215f));
            casingVelocity=-Gun.right*.75f+Vector3.up*.7f+Gun.up*.12f;
            casingRotation=Gun.rotation;Casing.SetPositionAndRotation(casingStart,casingRotation);Casing.gameObject.SetActive(true);
        }
        public void Apply(float travel)
        {
            if(Pump!=null)Pump.localPosition=new Vector3(0,-travel/85f,0);
            if(Bolt!=null)Bolt.localPosition=new Vector3(0,-travel*.6f/85f,0);
        }
    }
}
