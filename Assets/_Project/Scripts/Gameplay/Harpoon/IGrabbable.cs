using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Harpoon
{
    /// <summary>
    /// 집게로 낚아챌 수 있는 대상 계약 — 자원/몬스터 공용으로 설계한다 (슬라이스 스펙 §1.2).
    /// 상태 변경 메서드는 전부 서버(호스트)에서만 호출한다 — 그랩 확정·끌기는 호스트 소유 (§2.4).
    /// </summary>
    public interface IGrabbable
    {
        NetworkObject NetworkObject { get; }

        /// <summary>그랩 가능한 상태인지 (미소멸·미점유). 서버 기준 진실.</summary>
        bool IsAvailableForGrab { get; }

        /// <summary>다른 플레이어의 그랩에 점유돼 있는지. 서버 기준 진실.</summary>
        bool IsClaimed { get; }

        /// <summary>서버 전용 — 그랩 점유 시도. 점유 성공 시 컨베이어 제외(열차 프레임 소속 전환).</summary>
        bool TryClaimGrab(ulong grabberClientId);

        /// <summary>서버 전용 — 견인 중 위치 갱신. 30 Hz 고주기 동기화 값으로 기록된다 (§2.4).</summary>
        void UpdateTowPosition(Vector3 position);

        /// <summary>서버 전용 — 그랩 해제. 현재 위치에서 월드 소속으로 복귀(낙하).</summary>
        void ReleaseGrab();

        /// <summary>서버 전용 — 회수 완료(획득). 대상을 소멸 처리한다.</summary>
        void CompleteGrab();
    }
}
