using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Debugging
{
    /// <summary>Collision-driven jump inspection; the production network motor is unchanged.</summary>
    public sealed class RaccoonJumpMotorV32 : MonoBehaviour
    {
        public enum JumpState { Grounded, Takeoff, Airborne, Landing }
        public const float Height=1.2f,Gravity=20,Anticipation=.09f,Recovery=.28f;
        private CharacterController controller;
        private RaccoonJumpRigV32 rig;
        private Vector2 velocity,actual;
        private Vector3 spawn;
        private float vertical,timer,buffer,clock,jumpWeight,jumpPose;
        private bool initialized,sprint;
        public bool Automatic=true;
        public JumpState State {get;private set;}
        public RaccoonJumpRigV32 Rig=>rig;
        public Vector2 ActualVelocity=>actual;
        public float VerticalSpeed=>vertical;
        public float JumpWeight=>jumpWeight;
        public float JumpPose=>jumpPose;
        public int LaunchCount {get;private set;}
        public int LandingCount {get;private set;}
        public bool Grounded=>controller!=null&&controller.isGrounded;
        public void Initialize()
        {
            if(initialized)return;
            controller=GetComponent<CharacterController>();controller.minMoveDistance=0;
            rig=GetComponentInChildren<RaccoonJumpRigV32>();spawn=transform.position;rig.Initialize(transform);SettleSpawn();initialized=true;
        }
        public void ResetAt(Vector3 position)
        {
            Initialize();controller.enabled=false;transform.SetPositionAndRotation(position,Quaternion.identity);controller.enabled=true;
            velocity=actual=Vector2.zero;vertical=timer=buffer=clock=jumpWeight=jumpPose=0;
            State=JumpState.Grounded;LaunchCount=LandingCount=0;SettleSpawn();rig.Initialize(transform);
        }
        private void SettleSpawn()
        {
            // Resolve the small editor placement gap before tracking landings. Otherwise
            // the first few millimetres of settling are misreported as an airborne fall.
            Physics.SyncTransforms();controller.Move(Vector3.down*.06f);
            vertical=controller.isGrounded?-2:0;
        }
        public void Advance(float dt,Vector2 input,bool run,bool jumpPressed=false)
        {
            Initialize();dt=Mathf.Clamp(dt,0,.05f);if(dt<=0)return;
            input=Vector2.ClampMagnitude(input,1);sprint=run&&input.y>.98f&&Mathf.Abs(input.x)<.02f;
            buffer=jumpPressed?.12f:Mathf.Max(0,buffer-dt);
            if(controller.isGrounded&&State==JumpState.Grounded&&buffer>0)
            {State=JumpState.Takeoff;timer=0;buffer=0;}
            timer+=dt;
            if(State==JumpState.Takeoff&&timer>=Anticipation)
            {State=JumpState.Airborne;timer=0;vertical=Mathf.Sqrt(2*Gravity*Height);LaunchCount++;}
            Vector2 desired=input*(sprint?2.5f:RaccoonDirectionalMotorV29.WalkSpeed(input));
            float rate=State==JumpState.Airborne?2:desired.magnitude>velocity.magnitude?5:desired.sqrMagnitude>.001f?3:7;
            velocity=Vector2.MoveTowards(velocity,desired,rate*dt);
            float dy;
            if(State==JumpState.Airborne||!controller.isGrounded)
            {dy=vertical*dt-.5f*Gravity*dt*dt;vertical-=Gravity*dt;}
            else{vertical=-2;dy=vertical*dt;}
            Vector3 before=transform.position;
            var flags=controller.Move(transform.TransformDirection(new Vector3(velocity.x*dt,dy,velocity.y*dt)));
            if((flags&CollisionFlags.Above)!=0&&vertical>0)vertical=0;
            if(controller.isGrounded&&State==JumpState.Airborne&&vertical<=0)
            {State=JumpState.Landing;timer=0;vertical=-2;LandingCount++;}
            else if(!controller.isGrounded&&State!=JumpState.Airborne)
            {State=JumpState.Airborne;timer=0;}
            if(State==JumpState.Landing&&timer>=Recovery){State=JumpState.Grounded;timer=0;}
            float targetPose=0;
            switch(State)
            {
                case JumpState.Takeoff:jumpWeight=Mathf.SmoothStep(0,1,timer/Anticipation);break;
                case JumpState.Airborne:
                    jumpWeight=Mathf.MoveTowards(jumpWeight,1,dt/.08f);
                    targetPose=vertical>=0?Mathf.Lerp(1,2,1-vertical/Mathf.Sqrt(2*Gravity*Height)):Mathf.Lerp(2,3,Mathf.Clamp01(-vertical/4));break;
                case JumpState.Landing:jumpWeight=1-Mathf.SmoothStep(0,1,timer/Recovery);targetPose=4;break;
                default:jumpWeight=0;break;
            }
            jumpPose=Mathf.MoveTowards(jumpPose,targetPose,dt*16);
            Vector3 local=transform.InverseTransformDirection(transform.position-before);actual=new Vector2(local.x,local.z)/dt;
        }
        public void Evaluate(float dt){Initialize();rig.Tick(dt,actual,sprint,controller.isGrounded,jumpWeight,jumpPose);}
        private void Start(){Initialize();var c=FindFirstObjectByType<ViewLabOrbitCamera>();if(c!=null)c.Frame(transform,4,1);}
        private void Update()
        {
            Initialize();var k=Keyboard.current;Vector2 input=Vector2.zero;bool run=false,jump=false;
            if(k!=null)
            {
                if(k.tabKey.wasPressedThisFrame)Automatic=!Automatic;
                if(k.rKey.wasPressedThisFrame)ResetAt(spawn);
                if(k.digit1Key.wasPressedThisFrame)ResetAt(new Vector3(-10,.03f,0));
                if(k.digit2Key.wasPressedThisFrame){ResetAt(new Vector3(0,1.03f,3));Automatic=false;}
                if(k.digit3Key.wasPressedThisFrame){ResetAt(new Vector3(10,.03f,0));Automatic=false;}
                input=new Vector2((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0));
                run=k.leftShiftKey.isPressed;jump=k.spaceKey.wasPressedThisFrame;
                if(input.sqrMagnitude>0||jump)Automatic=false;
                if(!Automatic)transform.Rotate(0,((k.eKey.isPressed?1:0)-(k.qKey.isPressed?1:0))*65*Time.deltaTime,0);
            }
            if(Automatic)
            {
                float previous=clock;clock+=Time.deltaTime;
                jump=Crossed(previous,clock,1)||Crossed(previous,clock,5)||Crossed(previous,clock,9)||Crossed(previous,clock,14);
                if(clock>=4&&clock<8)input=Vector2.up;
                else if(clock>=8&&clock<12)input=Vector2.left;
                else if(clock>=12&&clock<17){input=Vector2.up;run=true;}
                else if(clock>=17&&clock<19)input=Vector2.up;
                if(clock>22)ResetAt(spawn);
            }
            Advance(Time.deltaTime,input,run,jump);
        }
        private static bool Crossed(float before,float after,float threshold)=>before<threshold&&after>=threshold;
        private void LateUpdate()=>Evaluate(Time.deltaTime);
        private GUIStyle style;
        private void OnGUI()
        {
            if(style==null)style=new GUIStyle(GUI.skin.label){font=Font.CreateDynamicFontFromOSFont("Malgun Gothic",18),fontSize=18};
            GUI.Box(new Rect(14,14,700,182),GUIContent.none);
            GUI.Label(new Rect(28,24,670,28),"라쿤 v32 · 점프·착지 검토본 / 기존 이동 동작 보존",style);
            GUI.Label(new Rect(28,56,670,28),"Space 점프 · WASD 이동 · Shift 전진 달리기 · Q/E 몸 회전",style);
            GUI.Label(new Rect(28,88,670,28),"1 평지 · 2 단차 내려오기 · 3 낮은 천장 · R 초기화",style);
            GUI.Label(new Rect(28,120,670,28),"Tab 자동/직접 · 우클릭 시점 회전 / 휠 확대",style);
            string state=State==JumpState.Takeoff?"도약 준비":State==JumpState.Airborne?"공중":State==JumpState.Landing?"착지 회복":"지상";
            GUI.Label(new Rect(28,152,670,28),(Automatic?"자동 시연":"직접 조작")+" · "+state+" · 본게임 연결 전 검토본",style);
        }
    }
}
