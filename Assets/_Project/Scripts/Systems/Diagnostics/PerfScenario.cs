using Game.Core.Diagnostics;
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

        [Header("게임 상태 강제")]
        [SerializeField]
        [Tooltip("시간대. Unchanged 면 게임이 정하는 대로 둔다. Night 로 두면 웨이브가 따라온다.")]
        private PerfTimeOfDay _timeOfDay = PerfTimeOfDay.Unchanged;

        [SerializeField, Min(0)]
        [Tooltip("점프할 Day 번호(1부터). 0이면 유지. 숲은 1~5일이고 5일차 밤이 대형 웨이브다.")]
        private int _dayNumber;

        [SerializeField]
        [Tooltip("웨이브 스폰을 켜 둔다. 밤 시나리오에서 꺼져 있으면 몬스터가 안 나온다.")]
        private bool _forceWaveSpawn = true;

        [SerializeField, Min(0f)]
        [Tooltip("상태를 강제한 뒤 측정 전까지 기다리는 시간(초). 몬스터가 스폰돼 자리를 잡을 시간이다.")]
        private float _settleSeconds;

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

        public PerfTimeOfDay TimeOfDay => _timeOfDay;

        public int DayNumber => _dayNumber;

        public bool ForceWaveSpawn => _forceWaveSpawn;

        /// <summary>강제 직후 대기 시간 — 몬스터가 스폰 간격을 따라 실제로 모일 시간이다.</summary>
        public float SettleSeconds => _settleSeconds;

        public int RandomSeed => _randomSeed;

        public int ScreenWidth => _screenWidth;

        public int ScreenHeight => _screenHeight;

        /// <summary>
        /// 이 시나리오가 강제한 조건 — 결과 JSON에 그대로 실어 <b>무엇을 바꾸고 잰 값인지</b>를
        /// 파일 스스로 밝히게 한다(§7 "벤치 모드가 게임 상태를 바꿈"의 대응).
        /// </summary>
        public string DescribeForcedConditions()
        {
            string state = _timeOfDay == PerfTimeOfDay.Unchanged
                ? "state=unchanged"
                : $"time={_timeOfDay} day={(_dayNumber > 0 ? _dayNumber.ToString() : "current")} " +
                  $"waves={(_forceWaveSpawn ? "on" : "off")} settle={_settleSeconds}s";

            return $"seed={_randomSeed} scene={_sceneName} warmup={_warmupFrames}f " +
                   $"duration={_durationSeconds}s {state}";
        }
    }
}
