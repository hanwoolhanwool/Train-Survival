using Unity.Profiling;

namespace Game.Core.Diagnostics
{
    /// <summary>
    /// 이 게임이 직접 심은 프로파일러 마커. <b>이름의 단일 출처</b>다 —
    /// 마커를 심는 쪽(게임플레이)과 읽는 쪽(<see cref="PerfProbe"/>)이 문자열을 따로 적으면
    /// 한쪽 오타가 <b>예외 없이 0</b>으로 나온다. Unity 6에 존재하지 않는 카운터 이름을 읽다가
    /// 60초 주행 세 번을 날린 전례가 있다(계획 §1.8).
    ///
    /// <para>마커는 <c>ProfilerRecorder</c>로 <b>빌드 Player에서도</b> 읽힌다. 스파이크가 났을 때
    /// "어느 시스템이었나"를 프레임 번호와 함께 특정하는 것이 목적이다(§4.2).</para>
    /// </summary>
    public static class GameProfilerMarkers
    {
        /// <summary>열차 주행 거리 적분과 지역 경계 관리.</summary>
        public const string WorldScrollUpdateName = "Game.WorldScroll.Update";

        /// <summary>지형 타일 스트리밍 — 6.67초마다 한 장이 교체되는 지점이다.</summary>
        public const string TileStreamUpdateName = "Game.TileStream.Update";

        /// <summary>타일 한 장의 실제 생성(풀 스폰) — 교체 스파이크의 범인 후보.</summary>
        public const string TileSpawnName = "Game.TileStream.Spawn";

        /// <summary>밤 웨이브 몬스터 스폰 판정.</summary>
        public const string WaveSpawnName = "Game.Wave.Spawn";

        public static readonly ProfilerMarker WorldScrollUpdate = new ProfilerMarker(WorldScrollUpdateName);

        public static readonly ProfilerMarker TileStreamUpdate = new ProfilerMarker(TileStreamUpdateName);

        public static readonly ProfilerMarker TileSpawn = new ProfilerMarker(TileSpawnName);

        public static readonly ProfilerMarker WaveSpawn = new ProfilerMarker(WaveSpawnName);
    }
}
