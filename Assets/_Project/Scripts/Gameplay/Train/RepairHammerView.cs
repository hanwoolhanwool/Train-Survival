using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 수리 망치 FP 뷰모델 (M8 1차 — 에셋 적용). 핫바가 망치 슬롯을 드는 동안 소유자 화면에만
    /// 보인다 — 원격 피어의 표현은 손 소켓의 TP 월드모델
    /// (<see cref="Game.Gameplay.Player.HeldWeaponSocket"/>)이 맡는다
    /// (무기 손 파지 계획 §2.2 — FP/TP 분리).
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
            bool visible = _controller != null && _controller.IsOwner && _controller.InputEnabled;
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
