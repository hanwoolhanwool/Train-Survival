using UnityEngine;

namespace Game.Gameplay.Debugging
{
    // Isolated motor: separate platform travel from the player's own locomotion speed.
    public sealed class RaccoonTrainMotorV28:MonoBehaviour
    {
        private CharacterController controller;
        private RaccoonTrainRigV28 rig;
        private Transform support;
        private Matrix4x4 previousSupport;
        private Quaternion previousRotation;
        private float speed;
        public float RelativeSpeed {get;private set;}
        public RaccoonTrainRigV28 Rig=>rig;
        public bool Grounded=>controller!=null&&controller.isGrounded;
        public Transform Support=>support;

        public void Initialize()
        {
            if(controller!=null&&rig!=null)return;
            controller=GetComponent<CharacterController>();rig=GetComponentInChildren<RaccoonTrainRigV28>();rig.Initialize(transform);
        }
        public void BindSupport(Transform value)
        {
            Initialize();support=value;
            if(support!=null){previousSupport=support.localToWorldMatrix;previousRotation=support.rotation;}
        }
        public void ResetAt(Vector3 position,Quaternion rotation,Transform surface)
        {
            Initialize();controller.enabled=false;transform.SetPositionAndRotation(position,rotation);controller.enabled=true;
            speed=RelativeSpeed=0;rig.Initialize(transform);BindSupport(surface);
        }
        public void Advance(float dt,Vector2 input,bool sprint)
        {
            Initialize();if(dt<=0)return;dt=Mathf.Min(dt,.05f);input=Vector2.ClampMagnitude(input,1);
            if(support!=null)
            {
                var current=support.localToWorldMatrix;var delta=current*previousSupport.inverse;
                var rotation=support.rotation*Quaternion.Inverse(previousRotation);
                float yaw=Mathf.DeltaAngle(previousRotation.eulerAngles.y,support.eulerAngles.y);
                var carried=delta.MultiplyPoint3x4(transform.position);
                // Carry first, then measure only intentional displacement. Do not count deck travel as running.
                controller.enabled=false;transform.position=carried;transform.rotation=Quaternion.AngleAxis(yaw,Vector3.up)*transform.rotation;controller.enabled=true;
                rig.TransportSupport(delta,rotation,yaw);previousSupport=current;previousRotation=support.rotation;
            }
            float desired=input.sqrMagnitude>.001f?(sprint?RaccoonSandboxV26.RunSpeed:RaccoonSandboxV26.WalkSpeed)*input.magnitude:0;
            speed=Mathf.MoveTowards(speed,desired,dt*(desired>speed?5:desired>0?3:7));
            if(input.sqrMagnitude>.001f)
            {
                Vector3 wish=new Vector3(input.x,0,input.y);if(support!=null)wish=Quaternion.Euler(0,support.eulerAngles.y,0)*wish;
                transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(wish),dt*120);
            }
            var before=transform.position;controller.Move((transform.forward*speed+Vector3.down*2)*dt);
            RelativeSpeed=Vector3.ProjectOnPlane(transform.position-before,Vector3.up).magnitude/dt;
            if(Physics.Raycast(transform.position+Vector3.up*.25f,Vector3.down,out var hit,.65f,~(1<<2),QueryTriggerInteraction.Ignore))
            {
                var next=hit.collider.GetComponentInParent<RaccoonMovingDeckV28>();var target=next!=null?next.transform:null;
                if(target!=support)BindSupport(target);
            }
            else BindSupport(null);
        }
        public void Evaluate(float dt){Initialize();rig.Tick(dt,RelativeSpeed,Grounded);}
    }
}
