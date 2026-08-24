using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 거치 무기 축의 서비스 계약 (M7 4차) — 플레이어 쪽 조작 계층이 열차 씬 오브젝트를 직접 물지 않게
    /// 하는 경계다 (DIP, <see cref="ITrainState"/>·<see cref="ITrainStorage"/>와 같은 규약).
    /// 구현은 Train 루트의 <see cref="MountedWeaponHost"/> 하나다.
    /// </summary>
    public interface IMountedWeapons
    {
        /// <summary>그 종류의 거치 무기 설정 — 거치 무기가 아니면 null (§2.1 — 참조 유무가 곧 판정).</summary>
        MountedWeaponSettings GetSettings(StructureKind kind);

        /// <summary>그 건축물을 지금 누가 붙어 쓰고 있는가 — 비점유면 false.</summary>
        bool TryGetOccupant(int structureId, out ulong clientId);

        /// <summary>그 사람이 지금 붙어 있는 거치 무기 — 없으면 false (한 사람은 하나만).</summary>
        bool TryGetMountedStructure(ulong clientId, out int structureId);

        /// <summary>붙기를 요청한다 (소유자 로컬 → 서버 승인). 승인 여부는 점유 리스트 복제로 돌아온다.</summary>
        void RequestMount(int structureId);

        /// <summary>내리기를 요청한다 — 중복 호출은 무해하다.</summary>
        void RequestDismount();

        /// <summary>
        /// 표현용 조준각 (도) — 포신 회전에만 쓴다. <b>판정에 쓰지 않는다</b>: 원격 값은
        /// 10 Hz Unreliable 중계라 유실될 수 있고, 유실돼도 그림만 잠깐 늦는다 (결정 ⑥).
        /// </summary>
        bool TryGetAim(int structureId, out float yawDeg, out float pitchDeg);

        /// <summary>
        /// 점유자 자신의 조준각을 표현 캐시에 밀어 넣는다 — 로컬 화면의 포신은 중계를 기다리지 않는다.
        /// 원격 전파는 구현이 10 Hz로 솎아 보낸다.
        /// </summary>
        void PublishLocalAim(int structureId, float yawDeg, float pitchDeg);

        /// <summary>
        /// 발사를 서버에 보고한다 (§2.4) — 사각 재검증·장탄 차감·연출 중계가 여기서 확정된다.
        /// 연출은 보고와 무관하게 쏜 사람이 이미 로컬 재생했다(지연 0).
        /// </summary>
        void ReportFire(int structureId, uint seed, Vector3 aimOrigin, Vector3 aimForward);

        /// <summary>
        /// 명중을 서버에 보고한다 — 판정은 <b>좌석 기준 거리</b>로 재검증된다. 같은 발사의 시드를
        /// 함께 싣는 이유는 <b>승인된 발사의 명중만</b> 피해가 되게 하기 위함이다: 장탄이 바닥나
        /// 기각된 발사의 명중 보고가 뒤늦게 들어와도 공짜 피해가 되지 않는다.
        /// </summary>
        void ReportHit(
            int structureId, uint seed, NetworkObjectReference target, Vector3 hitPoint, int pelletHits);

        /// <summary>
        /// 재장전을 요청한다 (§2.5) — 점유자의 개인 인벤에서 차감해 무기 탄창을 채운다.
        /// 확정 발수는 <see cref="MountedReloadConfirmedLocalEvent"/>로 돌아온다.
        /// </summary>
        void RequestReload(int structureId, int requestedRounds);
    }
}
