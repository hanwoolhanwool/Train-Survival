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
    }
}
