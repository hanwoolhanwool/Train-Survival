using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 외부 힘에 끌려가는 구간의 최소 계약 (집게 단계별 파지 계획 §3.5) —
    /// <see cref="PlayerMovementState.Grabbed"/> 골격의 <b>구동 입력</b>이다.
    /// <para>
    /// <see cref="NetworkPlayerController"/>는 이 표면만 보고 "지금 끌리는 중인가, 어디로 얼마나 빠르게"를
    /// 얻는다 — <b>무엇이 끄는지는 모른다</b>. 집게가 아니라 다른 것(폭풍·기중기 등)이 끌게 되어도
    /// 이동 쪽 코드는 그대로다 (DIP: 이동은 추상에, 견인 사유는 구현에).
    /// </para>
    /// 위치 권위는 여전히 소유자에게 있다 — 서버는 "누가 끄는가"만 복제하고,
    /// 끌려가는 계산은 소유자가 로컬에서 수행한다 (그래서 끌리는 쪽 화면이 지연으로 튀지 않는다).
    /// </summary>
    public interface IExternalTow
    {
        /// <summary>지금 외부 힘에 끌리는 중인가 — 복제 값 기준이라 전 피어 판정이 같다.</summary>
        bool IsTowed { get; }

        /// <summary>
        /// 이번 프레임 끌려갈 목표점과 속도. 끌리는 중이 아니면 false (출력은 의미 없음).
        /// </summary>
        bool TryGetTowStep(out Vector3 anchor, out float speed);
    }
}
