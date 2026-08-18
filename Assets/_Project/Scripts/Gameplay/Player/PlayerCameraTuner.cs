using Game.Core.Events;
using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 시점 모드별 카메라 파라미터 적용 (1인칭 통합 시점 전환 계획 §3.4) — 표현 전용.
    /// 통합 1인칭은 손에 쥔 무기가 카메라 앞 0.5 m 안팎에 오므로 근평면 0.3으로는 그립·개머리판이
    /// 잘린다. 모드마다 값을 <b>설정 에셋에서 다시 읽어</b> 쓰기 때문에 전환을 반복해도 누적되지 않는다
    /// (§4.1 멱등성).
    ///
    /// <para><see cref="Camera"/>와 같은 GameObject(CameraRig/CameraPivot/PlayerCamera)에 붙인다.
    /// 원격 프록시는 카메라 리그 자체가 꺼져 있어(<see cref="NetworkPlayerController"/>) 이 컴포넌트가
    /// 활성화되지 않는다 — 모드 이벤트를 구독조차 하지 않는다.</para>
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class PlayerCameraTuner : MonoBehaviour
    {
        private Camera _camera;
        private PlayerViewModeController _viewMode;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _viewMode = GetComponentInParent<PlayerViewModeController>();
        }

        private void OnEnable()
        {
            EventBus<PlayerViewModeChangedLocalEvent>.Subscribe(OnViewModeChanged);

            // 구독 순서에 의존하지 않도록 현재 값을 직접 읽어 즉시 적용한다.
            Apply(_viewMode != null ? _viewMode.Mode : PlayerViewMode.SplitFpTp);
        }

        private void OnDisable()
        {
            EventBus<PlayerViewModeChangedLocalEvent>.Unsubscribe(OnViewModeChanged);
        }

        private void OnViewModeChanged(PlayerViewModeChangedLocalEvent evt)
        {
            Apply(evt.Mode);
        }

        private void Apply(PlayerViewMode mode)
        {
            PlayerViewSettings settings = _viewMode != null ? _viewMode.Settings : null;
            if (settings == null || _camera == null)
            {
                return;
            }

            _camera.nearClipPlane = settings.GetNearClip(mode);
            _camera.fieldOfView = settings.GetFieldOfView(mode);
            transform.localPosition = settings.GetCameraLocalOffset(mode);
        }
    }
}
