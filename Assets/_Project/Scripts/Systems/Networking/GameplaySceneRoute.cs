using UnityEngine.SceneManagement;

namespace Game.Systems.Networking
{
    /// <summary>
    /// 인게임 씬 이름의 단일 출처. 씬 이름을 소비처마다 상수로 흩어 두면 <b>조용히</b> 어긋난다 —
    /// 메뉴는 인게임 씬을 로드했는데 초기 배치 게이트는 다른 이름을 기다리며 스폰을 영원히
    /// 보류하는 식이다. 시작·초기 배치 판정·재시작이 모두 이 타입을 보게 해서 그 어긋남을
    /// 구조적으로 막는다.
    /// </summary>
    /// <remarks>
    /// 인게임 씬이 하나뿐이므로 <see cref="Name"/>은 상수다. 클라이언트는 호스트가 지시한 씬을
    /// NGO 씬 동기화로 받으므로, "지금 인게임인가"를 물을 때는 이름 비교가 아니라
    /// <see cref="IsGameplayScene"/>·<see cref="IsActiveSceneGameplay"/>를 쓴다.
    /// </remarks>
    public static class GameplaySceneRoute
    {
        /// <summary>인게임 씬 — 열차 편성과 지역이 서는 유일한 플레이 씬.</summary>
        public const string Name = "Game_ArtTest";

        /// <summary>이 씬 이름이 인게임 씬인가 — 호스트·클라이언트 양쪽에서 유효한 판정.</summary>
        public static bool IsGameplayScene(string sceneName)
        {
            return sceneName == Name;
        }

        /// <summary>
        /// 지금 활성 씬이 인게임인가 — <b>대기실과 인게임을 가르는 판정</b>.
        ///
        /// <para>플레이어는 NGO 접속 시점에 스폰되므로 씬 전환 전(메뉴 씬)부터 월드에 존재한다.
        /// "스폰됐다"와 "조작해도 되는 때다"는 다른 말이므로, 소유자 입력을 여는 쪽은
        /// <c>IsOwner</c>만 보지 말고 이 판정을 함께 봐야 한다.</para>
        /// </summary>
        public static bool IsActiveSceneGameplay()
        {
            return IsGameplayScene(SceneManager.GetActiveScene().name);
        }
    }
}
