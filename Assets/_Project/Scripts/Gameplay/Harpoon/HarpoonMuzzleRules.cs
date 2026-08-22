namespace Game.Gameplay.Harpoon
{
    /// <summary>
    /// 훅·로프가 나갈 총구 앵커의 종류 (집게 발사위치 통합 계획 §2 결정 ①).
    /// </summary>
    public enum MuzzleAnchor : byte
    {
        /// <summary>앵커가 하나도 없어 레거시 총구(카메라 아래 고정점)로 물러선다.</summary>
        Fallback = 0,

        /// <summary>FP 뷰모델(<c>AimPivot/HarpoonPivot</c>)의 총구 — 소유자가 자기 화면에서 보는 집게.</summary>
        Fp = 1,

        /// <summary>TP 월드모델(손 본 소켓)의 총구 — 원격 피어가 보는, 캐릭터가 손에 쥔 집게.</summary>
        Tp = 2,
    }

    /// <summary>
    /// 총구 앵커 선택 규칙 — <b>보는 사람 축</b>이다 (집게 발사위치 통합 계획 §2 결정 ①).
    /// 소유자는 자기 화면의 FP 뷰모델 총구를, 원격 피어는 그 캐릭터가 손에 쥔 TP 모델의 총구를 쓴다.
    /// 소유자의 시점 모드(분리/통합)와는 직교한다 — 모드 축을 얹는 것은 1인칭 통합 시점 전환 계획 §3.5의 몫이다.
    ///
    /// 판정을 순수 함수가 소유한다 (아키텍처 규칙 — EditMode 테스트가 <c>Game.Editor</c>를 참조하지 않는다).
    /// <see cref="UnityEngine.Transform"/>을 인자로 받지 않고 가용성만 받는 이유도 같다.
    /// </summary>
    public static class HarpoonMuzzleRules
    {
        /// <summary>
        /// 이 피어가 쓸 총구 앵커를 고른다.
        /// 선호 앵커가 없으면 반대쪽으로 물러서고, 둘 다 없을 때만 <see cref="MuzzleAnchor.Fallback"/>이다 —
        /// 무기 전환 중 모델이 아직 붙지 않은 프레임에도 로프가 원점(0,0,0)으로 튀지 않아야 한다.
        /// </summary>
        /// <param name="isOwner">이 집게의 소유자 화면인가 (<c>NetworkBehaviour.IsOwner</c>).</param>
        /// <param name="fpAvailable">FP 총구 앵커가 배선돼 있는가.</param>
        /// <param name="tpAvailable">TP 총구 앵커가 배선돼 있는가.</param>
        public static MuzzleAnchor ResolveAnchor(bool isOwner, bool fpAvailable, bool tpAvailable)
        {
            if (isOwner)
            {
                if (fpAvailable)
                {
                    return MuzzleAnchor.Fp;
                }

                return tpAvailable ? MuzzleAnchor.Tp : MuzzleAnchor.Fallback;
            }

            if (tpAvailable)
            {
                return MuzzleAnchor.Tp;
            }

            return fpAvailable ? MuzzleAnchor.Fp : MuzzleAnchor.Fallback;
        }
    }
}
