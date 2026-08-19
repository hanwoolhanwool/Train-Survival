using Game.Core.Events;
using Game.Gameplay.Harpoon;
using Game.Gameplay.Inventory;
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

        /// <summary>그랩 거부 사유 표시 시간 — 조준점 근처에 잠깐 띄우고 사라진다.</summary>
        private const float GrabRejectHoldSeconds = 2f;

        private int _resourceTotal;
        private float _warningMeters;
        private float _warningUntilTime;
        private float _fellBehindUntilTime;
        private string _grabRejectMessage;
        private float _grabRejectUntilTime;

        private void OnEnable()
        {
            EventBus<ResourceAcquiredEvent>.Subscribe(OnResourceAcquired);
            EventBus<FallBehindWarningLocalEvent>.Subscribe(OnFallBehindWarning);
            EventBus<PlayerFellBehindEvent>.Subscribe(OnPlayerFellBehind);
            EventBus<HarpoonGrabRejectedLocalEvent>.Subscribe(OnGrabRejected);
            EventBus<HotbarSelectionRejectedLocalEvent>.Subscribe(OnSelectionRejected);
        }

        private void OnDisable()
        {
            EventBus<ResourceAcquiredEvent>.Unsubscribe(OnResourceAcquired);
            EventBus<FallBehindWarningLocalEvent>.Unsubscribe(OnFallBehindWarning);
            EventBus<PlayerFellBehindEvent>.Unsubscribe(OnPlayerFellBehind);
            EventBus<HarpoonGrabRejectedLocalEvent>.Unsubscribe(OnGrabRejected);
            EventBus<HotbarSelectionRejectedLocalEvent>.Unsubscribe(OnSelectionRejected);
        }

        /// <summary>
        /// 그랩 거부 안내 (M5 5차) — 자원 노드는 종류 색으로만 구분되므로 "왜 안 잡히는지"를
        /// 알려주지 않으면 상위 자원이 그냥 고장난 것처럼 보인다.
        /// </summary>
        private void OnGrabRejected(HarpoonGrabRejectedLocalEvent evt)
        {
            _grabRejectMessage = GetRejectMessage(evt.Verdict);
            _grabRejectUntilTime = string.IsNullOrEmpty(_grabRejectMessage)
                ? 0f
                : Time.unscaledTime + GrabRejectHoldSeconds;
        }

        /// <summary>
        /// 슬롯 전환 거부 안내 (집게 단계별 파지 계획 §3.6) — 1단계 집게는 잡은 동안 손이 묶인다.
        /// 그랩 거부와 <b>같은 자리·같은 수명</b>으로 띄운다: 둘 다 "왜 안 되는지"를 알리는 안내라
        /// 화면에서 다른 문법으로 보이면 안 된다. 반복 억제는 발행 쪽이 결정한다
        /// (<see cref="HotbarSelectionRejectedLocalEvent.ShowMessage"/>) — 여기서는 시키는 대로만 띄운다.
        /// </summary>
        private void OnSelectionRejected(HotbarSelectionRejectedLocalEvent evt)
        {
            if (!evt.ShowMessage)
            {
                return;
            }

            _grabRejectMessage = GetSelectionRejectMessage(evt.Reason);
            _grabRejectUntilTime = string.IsNullOrEmpty(_grabRejectMessage)
                ? 0f
                : Time.unscaledTime + GrabRejectHoldSeconds;
        }

        private static string GetSelectionRejectMessage(HotbarSwitchRejectReason reason)
        {
            switch (reason)
            {
                case HotbarSwitchRejectReason.HarpoonTier1HandsFull:
                    return "잡은 손이 묶였다 — 강화 집게라야 든 채로 무기를 바꾼다";

                default:
                    return string.Empty;
            }
        }

        private static string GetRejectMessage(GrabVerdict verdict)
        {
            switch (verdict)
            {
                case GrabVerdict.InsufficientTier:
                    return "너무 무겁다 — 강화 집게가 필요하다";

                case GrabVerdict.TargetClaimed:
                    return "다른 사람이 잡고 있다";

                case GrabVerdict.OutOfRange:
                    return "너무 멀다";

                default:
                    // 대상 소멸은 화면에서 이미 사라진 것이 보이므로 굳이 알리지 않는다.
                    return string.Empty;
            }
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
                GUILayout.Label($"<color={UiPalette.HexAlertText}>경고 — 열차에서 {_warningMeters:F0} m 뒤처짐!</color>");
            }

            if (Time.unscaledTime < _fellBehindUntilTime)
            {
                GUILayout.Label($"<color={UiPalette.HexCriticalText}>플레이어 이탈 — 후미 칸에서 부활</color>");
            }

            GUILayout.EndArea();

            if (Time.unscaledTime < _grabRejectUntilTime)
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 180f, Screen.height * 0.5f + 28f, 360f, 24f),
                    $"<color={UiPalette.HexCriticalText}>{_grabRejectMessage}</color>");
            }

            // 조준점.
            GUI.Label(new Rect(Screen.width * 0.5f - 4f, Screen.height * 0.5f - 8f, 8f, 16f), "+");
        }
    }
}
