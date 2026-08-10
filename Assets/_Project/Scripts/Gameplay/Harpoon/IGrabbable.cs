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
    /// 회수 완료(도착)의 결과 — <b>도착 이후 상태의 소유권</b>을 대상이 결정한다 (M5 6차).
    /// 집게는 이 값에 따라 로프를 끊거나(해제·수납) 계속 붙잡는다(파지) — 종류 분기가 없다 (OCP).
    /// </summary>
    public enum GrabCompletionResult
    {
        /// <summary>확정 불성립 — 그 자리 낙하. 집게는 기존 "해제 + 강제 해제 통지" 경로로 되돌아간다.</summary>
        Rejected,

        /// <summary>수납·소멸 (자원) — 대상이 사라졌으므로 집게는 로프를 회수하고 쿨다운으로 넘어간다.</summary>
        Consumed,

        /// <summary>파지 유지 (몬스터) — 집게가 계속 붙잡는다. 놓을 때까지 들고 다닐 수 있다.</summary>
        Held,
    }

    /// <summary>
    /// 파지 부착 표시 계약 (M5 6차 2차) — <see cref="GrabCompletionResult.Held"/>를 돌려준 대상 중
    /// "전 피어 로컬 부착 표시"를 쓰는 것이 구현한다. 집게는 앵커 추종이 끊기는 구간(투척 비행)의
    /// 시작을 이 최소 표면으로만 알린다 — 대상 종류는 여전히 모른다 (ISP).
    /// </summary>
    public interface IHoldAttachable
    {
        /// <summary>서버 전용 — false = 앵커 추종이 끊겼다 (투척 비행). 부착 표시를 풀고 동기화 표시로 복귀한다.</summary>
        void ServerSetHoldAttached(bool attached);

        /// <summary>
        /// 소유자 클라이언트 로컬 — 투척 선반영. 서버 확정(RPC 왕복 + 스냅샷 보간)을 기다리지 않고
        /// 좌클릭 즉시 같은 수식으로 비행을 재생해 게스트 홀더의 반응 지연을 없앤다
        /// (그랩의 <see cref="IGrabbable.BeginPredictedTow"/>와 같은 로컬 선반영 규약).
        /// 서버 확정(기절 복제·디스폰)이 도착하면 스스로 동기화 표시로 복귀한다. 서버에서 호출하면 무시한다.
        /// </summary>
        void BeginPredictedThrow(Vector3 direction, float speed, float range, float radius);
    }

    /// <summary>
    /// 회수 완료(도착) 시점에 대상에게 넘기는 그래버 정보 (M5 5차 — 획득 확정 폴리모피즘 이관).
    /// "도착하면 무슨 일이 일어나는가"를 대상이 결정하므로, 대상이 필요한 것만 여기서 읽는다
    /// (자원 = 인벤토리 수납, 몬스터 = 무력화). 집게는 종류 분기를 하지 않는다 (OCP).
    /// </summary>
    public readonly struct GrabCompletion
    {
        /// <summary>회수한 플레이어의 클라이언트 ID.</summary>
        public readonly ulong GrabberClientId;

        /// <summary>회수한 플레이어의 오브젝트 — 인벤토리 등 부착 컴포넌트 조회용.</summary>
        public readonly GameObject Grabber;

        /// <summary>회수한 집게의 등급 (1~3). 대상이 등급별 결과를 달리하고 싶을 때 쓴다.</summary>
        public readonly int GrabberTier;

        public GrabCompletion(ulong grabberClientId, GameObject grabber, int grabberTier)
        {
            GrabberClientId = grabberClientId;
            Grabber = grabber;
            GrabberTier = grabberTier;
        }
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

        /// <summary>
        /// 무게 등급 (1~3, M5 5차) — 그래버의 집게 등급이 이 값 이상이어야 낚아챌 수 있다
        /// (<see cref="GrabValidation.CanLift"/>). 기존 자원·손잡이는 전부 1이라 회귀가 없다.
        /// </summary>
        int GrabWeight { get; }

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

        /// <summary>
        /// 서버 전용 — 회수 완료(도착) 확정. 도착 시 벌어질 일은 <b>대상이 결정한다</b>:
        /// 자원은 수납 후 소멸(<see cref="GrabCompletionResult.Consumed"/>), 몬스터는 소멸하지 않고
        /// 집게에 매달린 채 유지된다(<see cref="GrabCompletionResult.Held"/> — M5 6차 파지).
        /// <see cref="GrabCompletionResult.Rejected"/>면 확정이 성립하지 않은 것 —
        /// 집게는 기존 "해제 + 강제 해제 통지" 경로로 되돌아간다.
        /// </summary>
        GrabCompletionResult TryCompleteGrab(in GrabCompletion completion);

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
