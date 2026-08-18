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
    /// <para>그림자 차단·통합 1인칭 게이트·가시성 토글은
    /// <see cref="Game.Gameplay.Player.FirstPersonViewModel"/>이 공통으로 처리한다.</para>
    /// </summary>
    public sealed class HarpoonView : Game.Gameplay.Player.FirstPersonViewModel
    {
        private HarpoonController _controller;

        protected override bool IsHeldByOwner =>
            _controller != null && _controller.IsSpawned
            && _controller.IsOwner && _controller.InputEnabled;

        protected override void Awake()
        {
            base.Awake();
            _controller = GetComponentInParent<HarpoonController>();
        }
    }
}
