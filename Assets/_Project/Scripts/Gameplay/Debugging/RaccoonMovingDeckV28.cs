using UnityEngine;

namespace Game.Gameplay.Debugging
{
    public sealed class RaccoonMovingDeckV28:MonoBehaviour
    {
        public Vector3 Origin;
        public float TravelSpeed {get;private set;}
        public void Sample(float seconds,bool moving)
        {
            if(!moving){transform.SetPositionAndRotation(Origin,Quaternion.identity);TravelSpeed=0;return;}
            // Bounded periodic route with rest, acceleration, yaw and gentle roll; no teleport at wrap.
            float t=Mathf.Repeat(seconds,32),u=t<16?t/16:(32-t)/16;
            float s=u*u*u*(10+u*(-15+6*u));
            float envelope=Mathf.Sin(Mathf.PI*u);envelope*=envelope;
            transform.SetPositionAndRotation(Origin+new Vector3(4*envelope,0,40*s),Quaternion.Euler(0,18*Mathf.Sin(2*Mathf.PI*u)*envelope,2*Mathf.Sin(2*Mathf.PI*u)*envelope));
            TravelSpeed=40*30*u*u*(1-u)*(1-u)/16;
        }
    }
}
