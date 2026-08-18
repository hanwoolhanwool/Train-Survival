using UnityEngine;

namespace Game.Gameplay.Harpoon
{
    /// <summary>
    /// 집게(하푼) 발사기 FP 뷰모델 (M8 1차 — 에셋 적용). 집게 슬롯을 드는 동안 소유자 화면에만
    /// 보인다 — 원격 피어의 표현은 손 소켓의 TP 월드모델
    /// (<see cref="Game.Gameplay.Player.HeldWeaponSocket"/>)이 맡는다
    /// (무기 손 파지 계획 §2.2 — FP/TP 분리).
    /// Player 프리팹에서 발사기 모델의 부모 피벗에 부착한다.
    ///
    /// <para><b>통합 1인칭</b>에서는 이 화면 전용 뷰모델을 띄우지 않는다 — 손에 쥔 무기가 그 자리를
    /// 대신한다 (1인칭 통합 시점 전환 계획 §3.2). 원격 프록시의 모드는 항상 분리에 머물러 있어
    /// 이 조건은 자기 화면에만 걸린다.</para>
    /// </summary>
    public sealed class HarpoonView : MonoBehaviour
    {
        private HarpoonController _controller;
        private Game.Gameplay.Player.PlayerViewModeController _viewMode;
        private Renderer[] _renderers;
        private bool _visible;

        private void Awake()
        {
            _controller = GetComponentInParent<HarpoonController>();
            _viewMode = GetComponentInParent<Game.Gameplay.Player.PlayerViewModeController>();
            _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            _visible = true;
            SetVisible(false);
        }

        private void Update()
        {
            bool unified = _viewMode != null
                && _viewMode.Mode == Game.Gameplay.Player.PlayerViewMode.UnifiedFirstPerson;
            bool visible = !unified && _controller != null && _controller.IsSpawned
                && _controller.IsOwner && _controller.InputEnabled;
            if (visible != _visible)
            {
                SetVisible(visible);
            }
        }

        private void SetVisible(bool visible)
        {
            if (_visible == visible)
            {
                return;
            }

            _visible = visible;
            for (int i = 0; i < _renderers.Length; i++)
            {
                _renderers[i].enabled = visible;
            }
        }
    }
}
