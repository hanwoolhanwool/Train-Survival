using UnityEngine;

namespace Game.Gameplay.Harpoon
{
    /// <summary>
    /// 집게(하푼) 발사기 뷰모델 (M8 1차 — 에셋 적용). 집게 슬롯을 드는 동안만 보인다.
    /// <see cref="HarpoonController.InputEnabled"/>의 기본값이 true라(비소유자에게는 핫바 게이트가
    /// 돌지 않는다) 소유자 게이트는 소유자에게만 쓰고, 원격 피어는 복제된 파지 슬롯
    /// (<see cref="Game.Gameplay.Player.PlayerAimView.HeldItem"/>)을 따른다 (M8 검증 개선).
    /// Player 프리팹에서 발사기 모델의 부모 피벗에 부착한다.
    ///
    /// <para><b>통합 1인칭</b>에서는 이 화면 전용 뷰모델을 띄우지 않는다 — 손에 쥔 무기가 그 자리를
    /// 대신한다 (1인칭 통합 시점 전환 계획 §3.2). 원격 프록시의 모드는 항상 분리에 머물러 있어
    /// 이 조건은 <b>자기 화면에만</b> 걸린다.</para>
    /// </summary>
    public sealed class HarpoonView : MonoBehaviour
    {
        private HarpoonController _controller;
        private Game.Gameplay.Player.PlayerAimView _aim;
        private Game.Gameplay.Player.PlayerViewModeController _viewMode;
        private Renderer[] _renderers;
        private bool _visible;

        private void Awake()
        {
            _controller = GetComponentInParent<HarpoonController>();
            _aim = GetComponentInParent<Game.Gameplay.Player.PlayerAimView>();
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
                && (_controller.IsOwner
                    ? _controller.InputEnabled
                    : _aim != null && _aim.HeldItem == Game.Gameplay.Inventory.HotbarItemType.Harpoon);
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
