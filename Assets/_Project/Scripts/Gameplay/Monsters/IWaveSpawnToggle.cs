namespace Game.Gameplay.Monsters
{
    /// <summary>
    /// 몬스터 웨이브 스폰 토글 계약 (M5 4차 — QA 검증성). 끄면 진행 중인 웨이브를 회수하고
    /// 다음 밤 웨이브도 시작하지 않는다 — 밤 노숙 체온 검증(3차 이월 B1 계열)의 선결 수단.
    /// 서버 전용: 호스트 권위 상태이므로 클라이언트 요청은 RPC로 서버에 도달해야 한다.
    /// </summary>
    public interface IWaveSpawnToggle
    {
        bool SpawnEnabled { get; }

        void ServerSetSpawnEnabled(bool enabled);
    }
}
