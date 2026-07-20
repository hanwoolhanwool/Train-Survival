using Game.Core.Events;
using Game.Gameplay.Player;
using Game.Gameplay.World;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 슬라이스 디버그 HUD — 팀 자원 카운터 + 후미 이탈 경고 표시.
    /// UI는 상태를 소유하지 않는다: 권위/로컬 표현 이벤트 구독으로 갱신만 한다 (시스템 맵 §3).
    /// </summary>
    public sealed class SliceHud : MonoBehaviour
    {
        private const float WarningHoldSeconds = 0.5f;

        private int _resourceTotal;
        private float _warningMeters;
        private float _warningUntilTime;
        private float _fellBehindUntilTime;

        private void OnEnable()
        {
            EventBus<ResourceAcquiredEvent>.Subscribe(OnResourceAcquired);
            EventBus<FallBehindWarningLocalEvent>.Subscribe(OnFallBehindWarning);
            EventBus<PlayerFellBehindEvent>.Subscribe(OnPlayerFellBehind);
        }

        private void OnDisable()
        {
            EventBus<ResourceAcquiredEvent>.Unsubscribe(OnResourceAcquired);
            EventBus<FallBehindWarningLocalEvent>.Unsubscribe(OnFallBehindWarning);
            EventBus<PlayerFellBehindEvent>.Unsubscribe(OnPlayerFellBehind);
        }

        private void OnResourceAcquired(ResourceAcquiredEvent evt)
        {
            _resourceTotal = evt.TeamTotal;
        }

        private void OnFallBehindWarning(FallBehindWarningLocalEvent evt)
        {
            _warningMeters = evt.MetersBehindRear;
            _warningUntilTime = Time.unscaledTime + WarningHoldSeconds;
        }

        private void OnPlayerFellBehind(PlayerFellBehindEvent evt)
        {
            _fellBehindUntilTime = Time.unscaledTime + 3f;
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20f, 20f, 360f, 160f));
            GUILayout.Label($"자원: {_resourceTotal}");

            if (Time.unscaledTime < _warningUntilTime)
            {
                GUILayout.Label($"<color=orange>경고 — 열차에서 {_warningMeters:F0} m 뒤처짐!</color>");
            }

            if (Time.unscaledTime < _fellBehindUntilTime)
            {
                GUILayout.Label("<color=red>플레이어 이탈 — 후미 칸에서 부활</color>");
            }

            GUILayout.EndArea();

            // 조준점.
            GUI.Label(new Rect(Screen.width * 0.5f - 4f, Screen.height * 0.5f - 8f, 8f, 16f), "+");
        }
    }
}
