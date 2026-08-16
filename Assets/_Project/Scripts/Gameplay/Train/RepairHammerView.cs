using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 수리 망치 1인칭 뷰모델 (M8 1차 — 에셋 적용). 핫바가 망치 슬롯을 드는 동안만 보인다.
    /// 표시 조건은 <see cref="RepairHammerController.InputEnabled"/>를 그대로 따른다 —
    /// 게이트는 <see cref="Game.Gameplay.Inventory.HotbarController"/>가 소유자 로컬로만 열므로
    /// 원격 피어에서는 항상 꺼져 있다(TP 무기 표현은 1차 범위 밖, 계획서 §2.1).
    /// Player 프리팹에서 망치 모델의 부모 피벗에 부착한다.
    /// </summary>
    public sealed class RepairHammerView : MonoBehaviour
    {
        private RepairHammerController _controller;
        private Renderer[] _renderers;
        private bool _visible;

        private void Awake()
        {
            _controller = GetComponentInParent<RepairHammerController>();
            _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            _visible = true;
            SetVisible(false);
        }

        private void Update()
        {
            bool visible = _controller != null && _controller.InputEnabled;
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
