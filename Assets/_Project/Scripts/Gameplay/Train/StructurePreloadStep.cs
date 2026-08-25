using System.Collections.Generic;
using Game.Core.Logging;
using Game.Core.Pooling;
using Game.Systems.Loading;
using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// B 묶음 — 건축물의 첫-회 비용을 로딩 뒤로 옮긴다 —
    /// [인게임 진입 로딩 구현 계획](docs/plans/features/인게임-진입-로딩-구현-계획.md) §5.3.
    ///
    /// <para><b>한 종류마다 두 번 만든다</b>: 조준 시점의 고스트 사본 하나와, 설치 시점의 실물 하나.
    /// 지금은 그 둘이 각각 첫 조준·첫 설치 프레임에서 <c>Instantiate</c> 실경로를 탄다 —
    /// 고스트는 <see cref="StructurePlacementGhostView"/>가, 실물은
    /// <see cref="StructureViewSpawner"/>의 <c>PoolManager.Spawn</c>이.</para>
    ///
    /// <para><b>둘은 같은 메시·텍스처를 참조한다.</b> 그래서 고스트를 먼저 만들면 실물 프리웜은
    /// 이미 로드된 에셋을 복제하는 값싼 일이 된다 — 순서가 값을 한다.</para>
    ///
    /// <para><b>씬 로드 후에만 할 수 있다</b>(§3.1): 고스트 뷰가 인게임 씬의 <c>CarBuildGhost</c>에
    /// 붙어 있기 때문이다. 그래서 이 컴포넌트도 인게임 씬에 산다.</para>
    ///
    /// <para><b>한 프레임에 하나씩</b>(§5.5) — 건축물 텍스처는 종당 약 2.8 MB라
    /// 몰아서 만들면 진행바가 눈에 띄게 끊긴다.</para>
    /// </summary>
    public sealed class StructurePreloadStep : SessionPreloadStepBehaviour
    {
        [SerializeField]
        [Tooltip("설치 가능한 종류의 출처. 고스트 뷰가 쓰는 것과 같은 카탈로그여야 한다.")]
        private StructureCatalog _catalog;

        [SerializeField]
        [Tooltip("고스트 프리뷰를 만드는 쪽. 보통 같은 오브젝트(CarBuildGhost)에 함께 있다.")]
        private StructurePlacementGhostView _ghost;

        [SerializeField, Min(1)]
        [Tooltip("한 프레임에 처리할 단위 수. 텍스처가 무거워 1을 권한다.")]
        private int _perFrame = 1;

        /// <summary>이 회차에 다룰 종류들 — <see cref="Total"/>을 읽는 시점에 확정된다.</summary>
        private readonly List<StructureKind> _kinds = new List<StructureKind>();

        /// <summary>이미 실물 풀을 채워 둔 프리팹. 풀은 씬 전환을 넘어 살아남으므로 회차를 넘어 기억한다.</summary>
        private static readonly HashSet<GameObject> PooledPrefabs = new HashSet<GameObject>();

        private int _total;
        private int _done;

        public override PreloadPhase Phase => PreloadPhase.AfterSceneLoad;

        /// <summary>
        /// 만들 단위 수 — 종류당 둘(고스트 · 실물 풀). 계획은 여기서 세워진다
        /// (코디네이터가 단계를 열 때 가장 먼저 읽는 값이다).
        /// </summary>
        public override int Total
        {
            get
            {
                if (_kinds.Count == 0)
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

            while (budget > 0 && _done < _total)
            {
                // 앞의 절반은 고스트, 뒤의 절반은 실물 — 고스트가 먼저라야 실물이 값싸진다.
                int unit = _done;
                if (unit < _kinds.Count)
                {
                    PrewarmGhost(_kinds[unit]);
                }
                else
                {
                    PrewarmView(_kinds[unit - _kinds.Count]);
                }

                _done++;
                budget--;
            }

            if (_done >= _total && _total > 0)
            {
                GameLog.Info(LogCategory.Train, $"건축물 프리로드 완료: {_kinds.Count}종 · {_total}단위");
            }
        }

        private void BuildPlan()
        {
            _kinds.Clear();
            _total = 0;
            _done = 0;

            if (_catalog == null)
            {
                return;
            }

            for (int i = 0; i < _catalog.EntryCount; i++)
            {
                if (!_catalog.TryGetKindAt(i, out StructureKind kind) || !_catalog.IsPlaceable(kind))
                {
                    continue;
                }

                // 설치 목록에서 빠진 종류(돔)는 조준도 설치도 되지 않으므로 만들 이유가 없다.
                _kinds.Add(kind);
            }

            _total = _kinds.Count * 2;
        }

        private void PrewarmGhost(StructureKind kind)
        {
            if (_ghost == null)
            {
                return;
            }

            _ghost.Prewarm(kind);
        }

        private void PrewarmView(StructureKind kind)
        {
            GameObject prefab = _catalog == null ? null : _catalog.GetViewPrefab(kind);
            if (prefab == null || !PooledPrefabs.Add(prefab))
            {
                return;
            }

            // 한 개면 충분하다 — 첫 한 개만 첫-회 렉이고, 두 번째부터는 이미 로드된 에셋이다.
            PoolManager.Prewarm(prefab, 1);
        }
    }
}
