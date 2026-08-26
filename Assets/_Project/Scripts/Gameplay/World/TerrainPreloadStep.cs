using System.Collections.Generic;
using Game.Core.Logging;
using Game.Core.Pooling;
using Game.Gameplay.Region;
using Game.Systems.Loading;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// A 묶음 — 씬 로드 <b>전</b>에 출발 구간의 지형 타일을 풀에 채운다 —
    /// [인게임 진입 로딩 구현 계획](docs/plans/features/인게임-진입-로딩-구현-계획.md) §5.2 · §5.5.
    ///
    /// <para><b>왜 씬 로드 전인가</b>(§3.1): 인게임 씬이 활성화되는 순간
    /// <see cref="TerrainTileStreamer"/>의 첫 <c>Update</c>가 바로 돌아 타일 9장을 한꺼번에
    /// 인스턴스화한다. 뒤에 하면 이미 늦고, NGO는 <c>allowSceneActivation</c>을 열어 주지 않아
    /// 활성화를 미룰 수도 없다.</para>
    ///
    /// <para><b>씬 전환을 넘어 살아남는다</b>(§0.5): <c>PoolManager</c> 호스트가
    /// <c>DontDestroyOnLoad</c>라 대기실에서 만든 인스턴스가 그대로 인게임에서 쓰인다.
    /// 그리고 풀의 키는 <b>프리팹 참조</b>이므로, 팔레트에서 꺼낸 같은 에셋을 프리웜하면
    /// 스트리머의 <c>Spawn</c>이 그대로 히트한다.</para>
    ///
    /// <para><b>이미 만들어 둔 것은 다시 만들지 않는다.</b> 인게임을 나갔다 다시 시작하면
    /// 풀에는 지난 회차의 인스턴스가 그대로 있다 — 세어 두지 않으면 들어갈 때마다 9장씩 늘어난다.</para>
    ///
    /// <para><b>한 프레임에 다 하지 않는다</b>(§5.5). <c>PoolManager.Prewarm</c>은 한 프레임에
    /// 동기로 도는 <c>Instantiate</c> 루프라 그대로 부르면 진행바가 그 프레임에 얼어붙는다.
    /// <b>로딩을 빨리 끝내는 것보다 진행바가 부드럽게 도는 쪽이 낫다</b> — 어차피 전원 대기가 있어서
    /// 제일 느린 PC가 총 시간을 정한다.</para>
    /// </summary>
    public sealed class TerrainPreloadStep : SessionPreloadStepBehaviour
    {
        /// <summary>계획 한 줄 — 이 프리팹을 몇 개 더 만들어야 하는가.</summary>
        private sealed class Job
        {
            public GameObject Prefab;
            public int Remaining;
        }

        [Header("계획의 출처")]
        [SerializeField]
        [Tooltip("지역 순서. 출발 지역은 언제나 첫 번째다 — 그 지역의 팔레트로 계획을 세운다.")]
        private RegionTimelineSettings _timeline;

        [SerializeField]
        [Tooltip("타일 길이·전방·후방. 출발 구간의 인덱스 범위가 여기서 나온다.")]
        private WorldScrollSettings _scroll;

        [SerializeField]
        [Tooltip("출발 지역에 팔레트가 없을 때 쓰는 타일. 스트리머의 기본 타일과 같은 것을 꽂는다.")]
        private GameObject _fallbackTilePrefab;

        [SerializeField]
        [Tooltip("기차역 시퀀스 — 스트리머에 꽂은 것과 같은 SO를 꽂는다.\n" +
                 "다르면 계획과 실제가 어긋나 프리웜이 헛일이 된다.")]
        private StationSequenceSettings _stationSettings;

        [Header("함께 채우는 것")]
        [SerializeField]
        [Tooltip("지형 말고도 첫 프레임에 필요한 것들 (탄착 효과·예광탄 등).")]
        private PrewarmEntry[] _extra;

        [Header("실행")]
        [SerializeField, Min(1)]
        [Tooltip("한 프레임에 만들 최대 개수. 크게 잡으면 로딩이 짧아지고 진행바가 끊긴다.")]
        private int _perFrame = 2;

        private readonly List<Job> _jobs = new List<Job>();

        /// <summary>지금까지 실제로 만들어 둔 수 — 프리팹별. 회차를 넘어 누적된다.</summary>
        private readonly Dictionary<GameObject, int> _prewarmed = new Dictionary<GameObject, int>();

        private int _total;
        private int _done;

        public override PreloadPhase Phase => PreloadPhase.BeforeSceneLoad;

        /// <summary>
        /// 이 회차에 만들 총량. <b>계획은 여기서 세워진다</b> — 코디네이터가 단계를 열 때
        /// 가장 먼저 읽는 값이기 때문이다(<see cref="ISessionPreloadStep.Total"/>).
        /// 지난 회차가 끝나 계획이 비어 있을 때만 다시 세우므로, 한 회차 안에서 여러 번 읽어도
        /// 총량이 흔들리지 않는다.
        /// </summary>
        public override int Total
        {
            get
            {
                if (_jobs.Count == 0)
                {
                    BuildPlan();
                }

                return _total;
            }
        }

        public override int Done => _done;

        public override void Advance()
        {
            int budget = Mathf.Max(1, _perFrame);

            while (budget > 0 && _jobs.Count > 0)
            {
                Job job = _jobs[0];
                if (job.Prefab == null || job.Remaining <= 0)
                {
                    _jobs.RemoveAt(0);
                    continue;
                }

                PoolManager.Prewarm(job.Prefab, 1);
                _prewarmed.TryGetValue(job.Prefab, out int already);
                _prewarmed[job.Prefab] = already + 1;

                job.Remaining--;
                _done++;
                budget--;

                if (job.Remaining <= 0)
                {
                    _jobs.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// 원하는 총량을 프리팹별로 모은 뒤(합산), <b>이미 만들어 둔 만큼을 빼고</b> 계획을 세운다.
        /// 합산을 먼저 하는 순서가 중요하다 — 같은 프리팹이 팔레트와 추가 목록 양쪽에 나올 때
        /// 빼기를 두 번 하면 모자라게 만든다.
        /// </summary>
        private void BuildPlan()
        {
            _jobs.Clear();
            _total = 0;
            _done = 0;

            var wants = new Dictionary<GameObject, int>();
            AddTerrainWants(wants);

            if (_extra != null)
            {
                for (int i = 0; i < _extra.Length; i++)
                {
                    AddWant(wants, _extra[i].Prefab, _extra[i].Count);
                }
            }

            foreach (KeyValuePair<GameObject, int> want in wants)
            {
                _prewarmed.TryGetValue(want.Key, out int already);
                int need = want.Value - already;
                if (need <= 0)
                {
                    continue;
                }

                _jobs.Add(new Job { Prefab = want.Key, Remaining = need });
                _total += need;
            }

            if (_total > 0)
            {
                GameLog.Info(LogCategory.World, $"지형 프리웜 계획: {_jobs.Count}종 · {_total}개");
            }
        }

        private void AddTerrainWants(Dictionary<GameObject, int> wants)
        {
            if (_scroll == null)
            {
                return;
            }

            GameplayPreloadPlan.StartRange(
                _scroll.TileLength, _scroll.TilesAhead, _scroll.TilesBehind, out int first, out int last);

            // 기차역이 출발 구간에 걸리면 그 장수만큼 팔레트·폴백에서 빠진다 — 스트리머와 같은 순서다.
            bool stationOn = _stationSettings != null && _stationSettings.IsEnabled;
            int blockSize = stationOn ? _stationSettings.BlockSize : 0;
            int stageCount = stationOn ? _stationSettings.StageCount : 0;

            int stationTiles = 0;
            int[] stationCounts = GameplayPreloadPlan.StationStageCounts(first, last, blockSize, stageCount);
            for (int i = 0; i < stationCounts.Length; i++)
            {
                AddWant(wants, _stationSettings.GetStagePrefab(i), stationCounts[i]);
                stationTiles += stationCounts[i];
            }

            RegionDefinition region = _timeline == null ? null : _timeline.GetRegion(0);
            TerrainSegmentPalette palette = region == null ? null : region.SegmentPalette;

            int[] counts = GameplayPreloadPlan.SegmentCounts(
                first,
                last,
                palette == null ? null : palette.GetWeights(),
                palette == null ? null : palette.GetNoRepeatFlags(),
                blockSize,
                stageCount);

            if (counts.Length > 0)
            {
                for (int i = 0; i < counts.Length; i++)
                {
                    AddWant(wants, palette.GetPrefab(i), counts[i]);
                }

                return;
            }

            // 팔레트가 없는 지역 — 스트리머도 같은 순서로 폴백한다(지역 정의 → 씬 기본 타일).
            GameObject fallback = region != null && region.TerrainTilePrefab != null
                ? region.TerrainTilePrefab
                : _fallbackTilePrefab;

            AddWant(wants, fallback, last - first + 1 - stationTiles);
        }

        /// <summary>같은 프리팹이 여러 번 나오면 합친다(§5.2 — 합산하지 않으면 풀을 여러 번 만든다).</summary>
        private static void AddWant(Dictionary<GameObject, int> wants, GameObject prefab, int count)
        {
            if (prefab == null || count <= 0)
            {
                return;
            }

            wants.TryGetValue(prefab, out int already);
            wants[prefab] = already + count;
        }
    }
}
