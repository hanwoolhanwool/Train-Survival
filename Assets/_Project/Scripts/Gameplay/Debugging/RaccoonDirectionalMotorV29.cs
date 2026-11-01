using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Debugging
{
    /// <summary>Isolated aiming locomotion review. Does not replace the network player.</summary>
    public sealed class RaccoonDirectionalMotorV29 : MonoBehaviour
    {
        public static float WalkSpeed(Vector2 direction)
        {
            direction=direction.normalized;
            float side=Mathf.Abs(direction.x), forward=Mathf.Max(0,direction.y), back=Mathf.Max(0,-direction.y);
            return (side*.45f+forward*.8f+back*.55f)/Mathf.Max(.001f,side+forward+back);
        }
        private CharacterController controller;
        private RaccoonDirectionalRigV29 rig;
        private Vector2 velocity,actual;
        private Vector3 spawn;
        private float clock;
        private bool initialized,sprint;
        public bool Automatic=true;
        public RaccoonDirectionalRigV29 Rig=>rig;
        public Vector2 ActualVelocity=>actual;
        public void Initialize()
        {
            if(initialized)return;
            controller=GetComponent<CharacterController>();rig=GetComponentInChildren<RaccoonDirectionalRigV29>();
            // Slow strafing at high refresh rates produces sub-millimetre movement.
            // Discarding it makes measured speed jump between idle and walking.
            controller.minMoveDistance=0;
            spawn=transform.position;rig.Initialize(transform);initialized=true;
        }
        public void ResetAt(Vector3 position)
        {
            Initialize();controller.enabled=false;transform.SetPositionAndRotation(position,Quaternion.identity);controller.enabled=true;
            velocity=actual=Vector2.zero;clock=0;rig.Initialize(transform);
        }
        public void Advance(float dt,Vector2 input,bool run)
        {
            Initialize();dt=Mathf.Clamp(dt,0,.05f);input=Vector2.ClampMagnitude(input,1);
            // Sideways and backward are deliberate walking steps, not sideways sprinting.
            sprint=run&&input.y>.98f&&Mathf.Abs(input.x)<.02f;
            Vector2 target=input*(sprint?2.5f:WalkSpeed(input));
            float rate=target.magnitude>velocity.magnitude?5:target.sqrMagnitude>.001f?3:7;
            velocity=Vector2.MoveTowards(velocity,target,dt*rate);
            Vector3 before=transform.position;
            controller.Move((transform.TransformDirection(new Vector3(velocity.x,0,velocity.y))+Vector3.down*2)*dt);
            Vector3 local=transform.InverseTransformDirection(transform.position-before);
            actual=dt>0?new Vector2(local.x,local.z)/dt:Vector2.zero;
        }
        public void Evaluate(float dt){Initialize();rig.Tick(dt,actual,sprint,controller.isGrounded);}
        private void Start(){Initialize();var c=FindFirstObjectByType<ViewLabOrbitCamera>();if(c!=null)c.Frame(transform,3.2f,1);}
        private void Update()
        {
            Initialize();var k=Keyboard.current;Vector2 input=Vector2.zero;bool run=false;
            if(k!=null)
            {
                if(k.tabKey.wasPressedThisFrame)Automatic=!Automatic;
                if(k.rKey.wasPressedThisFrame)ResetAt(spawn);
                input=new Vector2((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0));
                run=k.leftShiftKey.isPressed;
                if(input.sqrMagnitude>0)Automatic=false;
                if(!Automatic)transform.Rotate(0,((k.eKey.isPressed?1:0)-(k.qKey.isPressed?1:0))*65*Time.deltaTime,0);
            }
            if(Automatic)
            {
                clock+=Time.deltaTime;
                // Rest, left/right, backward/forward, diagonal reversal, then preserved run.
                if(clock>2&&clock<6)input=Vector2.left;
                else if(clock<10&&clock>=6)input=Vector2.right;
                else if(clock>12&&clock<16)input=Vector2.down;
                else if(clock>=16&&clock<20)input=Vector2.up;
                else if(clock>22&&clock<26)input=new Vector2(-1,-1).normalized;
                else if(clock>=26&&clock<30)input=new Vector2(1,1).normalized;
                else if(clock>32&&clock<37){input=Vector2.up;run=true;}
                else if(clock>=37&&clock<40)input=Vector2.up;
                if(clock>43)ResetAt(spawn);
            }
            Advance(Time.deltaTime,input,run);
        }
        private void LateUpdate()=>Evaluate(Time.deltaTime);
        private GUIStyle style;
        private void OnGUI()
        {
            if(style==null)style=new GUIStyle(GUI.skin.label){font=Font.CreateDynamicFontFromOSFont("Malgun Gothic",18),fontSize=18};
            GUI.Box(new Rect(14,14,660,150),GUIContent.none);
            GUI.Label(new Rect(28,24,630,28),"라쿤 v29 수정본 · 후진·좌우 이동 / 고개 전환 보정",style);
            GUI.Label(new Rect(28,56,630,28),"WASD 이동  ·  Q/E 몸 회전  ·  Shift 전진 달리기",style);
            GUI.Label(new Rect(28,88,630,28),"Tab 자동/직접  ·  R 초기화  ·  우클릭 시점 회전 / 휠 확대",style);
            GUI.Label(new Rect(28,120,630,28),(Automatic?"자동 시연":"직접 조작")+"  ·  기존 v27·v28 보존  ·  본게임 연결 전 검토본",style);
        }
    }
}
