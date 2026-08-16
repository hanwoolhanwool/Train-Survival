using UnityEngine;

namespace Game.Gameplay.Harpoon
{
    /// <summary>
    /// 집게(하푼) 발사기 1인칭 뷰모델 (M8 1차 — 에셋 적용). 집게 슬롯을 드는 동안만 보인다.
    /// <see cref="HarpoonController.InputEnabled"/>의 기본값이 true라(비소유자에게는 핫바 게이트가
    /// 돌지 않는다) 소유자 여부를 함께 확인한다 — 원격 피어에서는 항상 꺼져 있다.
    /// Player 프리팹에서 발사기 모델의 부모 피벗에 부착한다.
    /// </summary>
    public sealed class HarpoonView : MonoBehaviour
    {
        private HarpoonController _controller;
        private Renderer[] _renderers;
        private bool _visible;

        private void Awake()
        {
            _controller = GetComponentInParent<HarpoonController>();
            _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            _visible = true;
            SetVisible(false);
        }

        private void Update()
        {
            bool visible = _controller != null && _controller.IsSpawned && _controller.IsOwner && _controller.InputEnabled;
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
