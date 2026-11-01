using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Debugging
{
    public sealed class RaccoonTrainReviewV28:MonoBehaviour
    {
        public RaccoonMovingDeckV28 Deck;
        public RaccoonTrainMotorV28 Player;
        public Transform Scenery;
        private float clock,scroll;
        private bool movingDeck,automatic=true;
        private GUIStyle title,body;
        private void Start(){Application.runInBackground=true;ResetReview(false);FindFirstObjectByType<ViewLabOrbitCamera>()?.Frame(Player.transform,5.3f,1);}
        public void ResetReview(bool move)
        {
            movingDeck=move;clock=scroll=0;automatic=true;Deck.Sample(0,move);Scenery.localPosition=Vector3.zero;
            Player.ResetAt(Deck.transform.TransformPoint(new Vector3(0,.03f,-9)),Quaternion.identity,Deck.transform);Physics.SyncTransforms();
        }
        private void Update()
        {
            var keys=Keyboard.current;
            if(keys!=null&&keys.digit1Key.wasPressedThisFrame)ResetReview(false);
            if(keys!=null&&keys.digit2Key.wasPressedThisFrame)ResetReview(true);
            if(keys!=null&&keys.rKey.wasPressedThisFrame)ResetReview(movingDeck);
            if(keys!=null&&keys.tabKey.wasPressedThisFrame)automatic=!automatic;
            float dt=Mathf.Min(Time.deltaTime,.05f);clock+=dt;Deck.Sample(clock,movingDeck);
            if(!movingDeck){scroll+=dt*5;Scenery.localPosition=Vector3.back*scroll;}
            Physics.SyncTransforms();
            Vector2 input=Vector2.zero;bool sprint=false;
            if(keys!=null){input=new Vector2((keys.dKey.isPressed?1:0)-(keys.aKey.isPressed?1:0),(keys.wKey.isPressed?1:0)-(keys.sKey.isPressed?1:0));sprint=keys.leftShiftKey.isPressed||keys.rightShiftKey.isPressed;if(input.sqrMagnitude>0)automatic=false;}
            if(automatic)
            {
                float t=Mathf.Repeat(clock,32);
                // Remain standing during acceleration, then walk/run/slow/stop on the deck.
                input=t>=5&&t<16?Vector2.up:t>=20&&t<30?Vector2.down:Vector2.zero;
                sprint=t>=9&&t<12||t>=24&&t<27;
                if(clock>=32){ResetReview(movingDeck);input=Vector2.zero;sprint=false;}
            }
            Player.Advance(dt,input,sprint);
        }
        private void LateUpdate()=>Player.Evaluate(Mathf.Min(Time.deltaTime,.05f));
        private void OnGUI()
        {
            if(title==null){var font=Font.CreateDynamicFontFromOSFont("Malgun Gothic",18);title=new GUIStyle(GUI.skin.label){font=font,fontSize=22,fontStyle=FontStyle.Bold};body=new GUIStyle(GUI.skin.label){font=font,fontSize=16};}
            GUI.Box(new Rect(14,14,640,191),GUIContent.none);
            GUI.Label(new Rect(28,24,610,32),"라쿤 v28 · 열차 위 동작 검토",title);
            GUI.Label(new Rect(28,62,610,26),movingDeck?"2 · 객차 이동·회전 시험":"1 · 본게임 방식: 열차 고정 / 배경 이동",body);
            GUI.Label(new Rect(28,88,610,26),"1 / 2 시험 선택 · WASD 이동 · Shift 달리기 · Tab 자동/직접",body);
            GUI.Label(new Rect(28,114,610,26),"R 처음부터 · 우클릭 시점 회전 · 휠 확대/축소",body);
            GUI.Label(new Rect(28,140,610,26),$"{(automatic?"자동 시연":"직접 조작")} · 바닥 기준 이동 {Player.RelativeSpeed:F2}m/s",body);
            GUI.Label(new Rect(28,166,610,26),"승인한 v27 동작 유지 · 독립 검토 장면",body);
        }
    }
}
