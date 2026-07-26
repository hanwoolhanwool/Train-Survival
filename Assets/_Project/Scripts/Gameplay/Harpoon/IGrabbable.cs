using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Harpoon
{
    /// <summary>집게 그랩의 종류 — 대상을 다루는 방식이 달라진다.</summary>
    public enum GrabKind
    {
        /// <summary>플레이어 쪽으로 릴 감아 회수(자원·몬스터). 기본.</summary>
        Reel,

        /// <summary>무거운 대상에 앵커를 걸어 붙잡기만 함(손잡이) — 릴 없이 로프 유지, 저항력 발생.</summary>
        Anchor,
    }

    /// <summary>
    /// 집게로 낚아챌 수 있는 대상 계약 — 자원/몬스터 공용으로 설계한다 (슬라이스 스펙 §1.2).
    /// 상태 변경 메서드는 전부 서버(호스트)에서만 호출한다 — 그랩 확정·끌기는 호스트 소유 (§2.4).
    /// 단 BeginPredictedTow/CancelPredictedTow는 예외로, 쏜 클라이언트의 로컬 선반영 계층이 호출한다.
    /// </summary>
    public interface IGrabbable
    {
        NetworkObject NetworkObject { get; }

        /// <summary>그랩 방식 — <see cref="GrabKind.Reel"/>은 릴 감기, <see cref="GrabKind.Anchor"/>는 붙잡기(릴 없음).</summary>
        GrabKind Kind { get; }

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

        /// <summary>
        /// 클라이언트 로컬 — 쏜 클라이언트가 로컬 명중 시점에 호출하는 예측 고정.
        /// 서버 확정 스냅샷이 도착할 때까지 이동 유도를 멈춰 그랩 전환 순간의 스냅을 없앤다.
        /// 서버 확정 수신 시 자동 해제된다. 서버에서 호출하면 무시한다.
        /// </summary>
        void BeginPredictedTow();

        /// <summary>클라이언트 로컬 — 예측 고정 해제 (거부·타임아웃·강제 해제). 원래 이동 유도로 복귀한다.</summary>
        void CancelPredictedTow();
    }
}
