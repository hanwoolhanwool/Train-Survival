using UnityEngine;

namespace Game.Systems.Diagnostics
{
    /// <summary>
    /// 벤치 시나리오 정의. <b>무엇을 어떤 조건으로 재는지</b>를 에셋 하나에 모아 둔다 —
    /// 시나리오가 늘어날 때 코드가 아니라 파일만 늘어나게 하려는 것이다(§6 3차 완료 기준).
    ///
    /// <para><b>결정론이 이 타입의 존재 이유다.</b> 성능 자동화가 실패하는 가장 흔한 이유는
    /// 매 실행마다 다른 게임이 돌기 때문이고, 여기 모인 값들이 그 변수를 고정한다(§4.4).</para>
    /// </summary>
    [CreateAssetMenu(fileName = "PerfScenario", menuName = "Game/Diagnostics/Perf Scenario")]
    public sealed class PerfScenario : ScriptableObject
    {
        [Header("식별")]
        [SerializeField]
        [Tooltip("실행 인자 -perfrun 에 넘기는 이름. 결과 JSON·기준선 파일명이 된다.")]
        private string _scenarioId = "forest-day-60s";

        [Header("주행")]
        [SerializeField]
        [Tooltip("측정할 씬. GameplaySceneRoute 상수와 분리해 둔다 — 라우트가 검증 씬을 가리켜도 벤치는 흔들리지 않는다(§7).")]
        private string _sceneName = "Game_ArtTest";

        [SerializeField, Min(1f)]
        [Tooltip("측정 길이(초). 60초면 활성 타일 9장이 정확히 한 번 전량 교체된다 (40m ÷ 6m/s × 9 = 60.0 · §1.4).")]
        private float _durationSeconds = 60f;

        [SerializeField, Min(0)]
        [Tooltip("버릴 워밍업 프레임 수. 셰이더 컴파일·풀 프리웜이 첫 수 초를 오염시킨다(§2 결정 ⑤).")]
        private int _warmupFrames = 300;

        [Header("결정론")]
        [SerializeField]
        [Tooltip("난수 시드. 매 실행 같은 타일 순서·같은 추첨 결과를 만든다(§4.4).")]
        private int _randomSeed = 20260902;

        [Header("화면")]
        [SerializeField, Min(320)]
        [Tooltip("측정 해상도 너비. 창 크기가 GPU 시간을 지배하므로 실행마다 고정한다(§4.4).")]
        private int _screenWidth = 1920;

        [SerializeField, Min(240)]
        private int _screenHeight = 1080;

        /// <summary>`-perfrun`이 받는 이름이자 결과·기준선 파일명.</summary>
        public string ScenarioId => _scenarioId;

        public string SceneName => _sceneName;

        public float DurationSeconds => _durationSeconds;

        public int WarmupFrames => _warmupFrames;

        public int RandomSeed => _randomSeed;

        public int ScreenWidth => _screenWidth;

        public int ScreenHeight => _screenHeight;

        /// <summary>
        /// 이 시나리오가 강제한 조건 — 결과 JSON에 그대로 실어 <b>무엇을 바꾸고 잰 값인지</b>를
        /// 파일 스스로 밝히게 한다(§7 "벤치 모드가 게임 상태를 바꿈"의 대응).
        /// </summary>
        public string DescribeForcedConditions()
        {
            return $"seed={_randomSeed} scene={_sceneName} warmup={_warmupFrames}f duration={_durationSeconds}s";
        }
    }
}
