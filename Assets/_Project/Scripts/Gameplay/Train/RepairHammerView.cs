using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 수리 망치 뷰모델 (M8 1차 — 에셋 적용). 핫바가 망치 슬롯을 드는 동안만 보인다.
    /// 소유자는 입력 게이트(<see cref="RepairHammerController.InputEnabled"/>)를, 원격 피어는
    /// 복제된 파지 슬롯(<see cref="Game.Gameplay.Player.PlayerAimView.HeldItem"/>)을 따른다
    /// (M8 검증 개선 — TP 무기 공유).
    /// Player 프리팹에서 망치 모델의 부모 피벗에 부착한다.
    /// </summary>
    public sealed class RepairHammerView : MonoBehaviour
    {
        private RepairHammerController _controller;
        private Game.Gameplay.Player.PlayerAimView _aim;
        private Renderer[] _renderers;
        private bool _visible;

        private void Awake()
        {
            _controller = GetComponentInParent<RepairHammerController>();
            _aim = GetComponentInParent<Game.Gameplay.Player.PlayerAimView>();
            _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            _visible = true;
            SetVisible(false);
        }

        private void Update()
        {
            bool visible = _controller != null
                && (_controller.IsOwner
                    ? _controller.InputEnabled
                    : _aim != null && _aim.HeldItem == Game.Gameplay.Inventory.HotbarItemType.Hammer);
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
