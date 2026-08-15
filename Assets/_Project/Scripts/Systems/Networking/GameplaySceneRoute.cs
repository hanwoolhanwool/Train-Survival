namespace Game.Systems.Networking
{
    /// <summary>
    /// 인게임 씬 이름의 단일 출처 — 기본 편성(Game)과 M8 아트 검증 편성(Game_ArtTest) 중
    /// 이번 세션이 어느 쪽으로 들어갈지를 담는다.
    /// 씬 이름을 소비처마다 상수로 흩어 두면 검증용 씬에서 <b>조용히</b> 어긋난다 —
    /// 메뉴는 아트 씬을 로드했는데 초기 배치 게이트는 "Game"이 아니라며 스폰을 영원히 보류하는 식이다.
    /// 시작·초기 배치 판정·재시작이 모두 이 타입을 보게 해서 그 어긋남을 구조적으로 막는다.
    /// </summary>
    /// <remarks>
    /// <see cref="Current"/>는 호스트가 고르는 값이라 클라이언트에는 복제되지 않는다.
    /// 클라이언트는 호스트가 지시한 씬을 NGO 씬 동기화로 받으므로, "지금 인게임인가"를
    /// 물을 때는 <see cref="Current"/> 비교가 아니라 <see cref="IsGameplayScene"/>를 쓴다.
    /// </remarks>
    public static class GameplaySceneRoute
    {
        /// <summary>프리미티브 편성 씬 — 아트 패스 이전의 기준 씬.</summary>
        public const string Default = "Game";

        /// <summary>M8 아트 패스 검증 씬 — 프리미티브 대신 실제 모델을 얹은 같은 편성.</summary>
        public const string ArtTest = "Game_ArtTest";

        /// <summary>
        /// 이번 실행이 처음 들어갈 인게임 씬. M8 아트 패스 동안은 아트 검증 씬이 작업 씬이라
        /// 여기를 시작점으로 둔다 — <see cref="Default"/> 쪽은 칸 규격이 TrainLayoutSettings와
        /// 어긋난 채 남아 있어(칸 중심 z 3 m·갑판 0.35 m) 그대로 들어가면 판정과 시각이 따로 논다.
        /// 아트 패스가 끝나 두 씬이 다시 맞으면 이 값만 <see cref="Default"/>로 되돌리면 된다.
        /// </summary>
        private const string Startup = ArtTest;

        private static string _current = Startup;

        /// <summary>호스트가 이번에 로드할(또는 로드한) 인게임 씬 이름.</summary>
        public static string Current => _current;

        /// <summary>메뉴에서 시작 대상 씬을 고른다 — 알 수 없는 이름이면 시작 씬으로 되돌린다.</summary>
        public static void Select(string sceneName)
        {
            _current = IsGameplayScene(sceneName) ? sceneName : Startup;
        }

        /// <summary>이 씬 이름이 인게임 씬인가 — 호스트·클라이언트 양쪽에서 유효한 판정.</summary>
        public static bool IsGameplayScene(string sceneName)
        {
            return sceneName == Default || sceneName == ArtTest;
        }

        /// <summary>메뉴 토글용 — 현재 씬의 반대편 이름.</summary>
        public static string Other => _current == Default ? ArtTest : Default;
    }
}
