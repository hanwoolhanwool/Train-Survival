using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Debugging
{
    /// <summary>Local input and physical movement for the separate character review scene.</summary>
    public sealed class RaccoonSandboxV26 : MonoBehaviour
    {
        // Review speeds follow the clips' stride; production movement speeds are untouched.
        public const float WalkSpeed=.8f,RunSpeed=2.5f;
        private CharacterController controller;
        private RaccoonCandidateRigV26 rig;
        private Vector3 spawn;
        private float speed,actualSpeed,demoTime;
        private bool initialized,automatic=true;
        private GUIStyle titleStyle,bodyStyle;
        public RaccoonCandidateRigV26 Rig=>rig;
        public float ActualSpeed=>actualSpeed;
        public bool Automatic {get=>automatic;set{automatic=value;demoTime=0;}}

        public void Initialize()
        {
            if(initialized)return;
            controller=GetComponent<CharacterController>();rig=GetComponentInChildren<RaccoonCandidateRigV26>();
            spawn=transform.position;rig.Initialize(transform);initialized=true;
        }
        private void Start(){Initialize();Application.runInBackground=true;var orbit=FindFirstObjectByType<ViewLabOrbitCamera>();if(orbit!=null)orbit.Frame(transform,4.3f,1);}
        public void ResetAt(Vector3 position)
        {
            Initialize();controller.enabled=false;transform.SetPositionAndRotation(position,Quaternion.identity);controller.enabled=true;
            speed=actualSpeed=demoTime=0;rig.Initialize(transform);
        }
        public void Advance(float dt,Vector2 input,bool sprint)
        {
            Initialize();dt=Mathf.Clamp(dt,0,.05f);input=Vector2.ClampMagnitude(input,1);
            float desired=input.sqrMagnitude>.001f?(sprint?RunSpeed:WalkSpeed)*input.magnitude:0;
            // Continue stepping while easing from run to walk; stopping still uses a prompt brake.
            speed=Mathf.MoveTowards(speed,desired,dt*(desired>speed?5:desired>0?3:7));
            if(input.sqrMagnitude>.001f)
            {
                var target=Quaternion.LookRotation(new Vector3(input.x,0,input.y));
                transform.rotation=Quaternion.RotateTowards(transform.rotation,target,dt*120);
            }
            Vector3 before=transform.position;
            controller.Move((transform.forward*speed+Vector3.down*2)*dt);
            actualSpeed=dt>0?Vector3.ProjectOnPlane(transform.position-before,Vector3.up).magnitude/dt:0;
        }
        public void Evaluate(float dt){Initialize();rig.Tick(dt,actualSpeed,controller.isGrounded);}
        private void Update()
        {
            Initialize();var keys=Keyboard.current;
            if(keys!=null&&keys.tabKey.wasPressedThisFrame)Automatic=!Automatic;
            if(keys!=null&&keys.rKey.wasPressedThisFrame)ResetAt(spawn);
            if(keys!=null&&keys.digit1Key.wasPressedThisFrame)ResetAt(new Vector3(-10,.1f,0));
            if(keys!=null&&keys.digit2Key.wasPressedThisFrame)ResetAt(new Vector3(0,.1f,13));
            if(keys!=null&&keys.digit3Key.wasPressedThisFrame)ResetAt(new Vector3(12,1.1f,13));
            Vector2 input=Vector2.zero;bool sprint=false;
            if(keys!=null)
            {
                input=new Vector2((keys.dKey.isPressed?1:0)-(keys.aKey.isPressed?1:0),(keys.wKey.isPressed?1:0)-(keys.sKey.isPressed?1:0));
                sprint=keys.leftShiftKey.isPressed||keys.rightShiftKey.isPressed;
                if(input.sqrMagnitude>0)automatic=false;
            }
            if(automatic)
            {
                demoTime+=Time.deltaTime;
                input=demoTime<1||demoTime>24?Vector2.zero:Vector2.up;
                // A long run exposes repeated seams; the second deceleration starts on another foot phase.
                sprint=demoTime>3&&demoTime<13||demoTime>18&&demoTime<21;
                if(demoTime>27)ResetAt(spawn);
            }
            Advance(Time.deltaTime,input,sprint);
        }
        private void LateUpdate()=>Evaluate(Time.deltaTime);
        private void OnGUI()
        {
            if(titleStyle==null){var font=Font.CreateDynamicFontFromOSFont("Malgun Gothic",18);titleStyle=new GUIStyle(GUI.skin.label){font=font,fontSize=22,fontStyle=FontStyle.Bold};bodyStyle=new GUIStyle(GUI.skin.label){font=font,fontSize=16};}
            GUI.Box(new Rect(14,14,590,191),GUIContent.none);
            GUI.Label(new Rect(28,24,560,32),"라쿤 수정본 v26 · 독립 조작 테스트",titleStyle);
            GUI.Label(new Rect(28,62,560,26),"WASD 이동·방향 전환   |   Shift 달리기   |   R 초기화",bodyStyle);
            GUI.Label(new Rect(28,88,560,26),"1 평지   2 오르막·내리막   3 옆경사   |   Tab 자동/직접",bodyStyle);
            GUI.Label(new Rect(28,114,560,26),"우클릭 드래그 시점 회전 · 휠 확대/축소",bodyStyle);
            GUI.Label(new Rect(28,140,560,26),(automatic?"자동 시연 · 달리기 반복 → 걷기 전환":"직접 조작")+" · 원본 보존",bodyStyle);
            GUI.Label(new Rect(28,166,560,26),"반복·전환 보정 v27 · 달리기 검토 속도 2.5m/s",bodyStyle);
        }
    }
}

