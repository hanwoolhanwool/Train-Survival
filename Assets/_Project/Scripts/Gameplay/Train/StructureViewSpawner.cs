using Game.Core.Logging;
using System.Collections.Generic;
using Game.Core.Events;
using Game.Core.Pooling;
using Game.Core.Services;
using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 건축물 그리드 항목을 실물 프리팹으로 스폰·회수한다 (건축 개편 1차 — 계획서 §2.6).
    /// 상태를 소유하지 않는 표현 계층: 항목 추가/제거 권위 이벤트와 초기 동기화
    /// (<see cref="TrainInitializedEvent"/> — 신규·후발 접속 공통)에 반응해 <see cref="PoolManager"/>로
    /// 로컬 스폰만 한다. 프리팹은 종류별로 <see cref="StructureCatalog"/>가 든다 (OCP — 종류 추가는 에셋만).
    /// 실물은 칸 오브젝트(Car_N)의 자식으로 붙어 이탈 이동을 그대로 따라간다.
    /// Train 루트에 1개 배치한다.
    /// </summary>
    public sealed class StructureViewSpawner : MonoBehaviour
    {
        [SerializeField] private StructureCatalog _catalog;
        [SerializeField] private TrainLayoutSettings _layoutSettings;

        private readonly Dictionary<int, StructureView> _views = new Dictionary<int, StructureView>();
        private readonly List<int> _staleIds = new List<int>();

        // 재구성 때 살아 있는 항목 Id — 매번 새로 만들지 않고 비운 뒤 채운다.
        private readonly HashSet<int> _liveIds = new HashSet<int>();

        // 칸 인덱스 → 칸 트랜스폼 — 씬 정적 배치(Car_Locomotive/Car_N)를 첫 동기화 때 수집한다.
        private readonly Dictionary<int, Transform> _carTransforms = new Dictionary<int, Transform>();

        private void OnEnable()
        {
            EventBus<TrainInitializedEvent>.Subscribe(OnTrainInitialized);
            EventBus<StructureEntryChangedEvent>.Subscribe(OnEntryChanged);
            ResyncAll();
        }

        private void OnDisable()
        {
            EventBus<TrainInitializedEvent>.Unsubscribe(OnTrainInitialized);
            EventBus<StructureEntryChangedEvent>.Unsubscribe(OnEntryChanged);
            DespawnAll();
        }

        private void OnTrainInitialized(TrainInitializedEvent _)
        {
            ResyncAll();
        }

        private void OnEntryChanged(StructureEntryChangedEvent evt)
        {
            switch (evt.Change)
            {
                case StructureListChange.Added:
                    Spawn(evt.Entry);
                    break;

                case StructureListChange.Removed:
                    Despawn(evt.Entry.Id);
                    break;

                case StructureListChange.Reset:
                    ResyncAll();
                    break;

                // Updated(체력 등)는 각 StructureView가 스스로 반영한다.
            }
        }

        /// <summary>
        /// 리스트 전체 기준 재구성 — 신규 시작·후발 접속(복제된 목록)·목록 전체 변화 공통 경로.
        /// 목록에 없는 실물은 회수하고, 실물이 없는 항목은 스폰한다.
        /// </summary>
        private void ResyncAll()
        {
            if (!ServiceLocator.TryGet(out ITrainState train))
            {
                return;
            }

            CarViewAnchor.CollectCars(_carTransforms);

            // 살아 있는 Id를 한 번에 모아 두고 비교한다 — 항목마다 목록을 다시 훑으면
            // 후발 접속(실물 × 항목)에서 제곱이 된다.
            _liveIds.Clear();
            for (int i = 0; i < train.StructureCount; i++)
            {
                if (train.TryGetStructureAt(i, out StructureEntry live))
                {
                    _liveIds.Add(live.Id);
                }
            }

            _staleIds.Clear();
            foreach (KeyValuePair<int, StructureView> pair in _views)
            {
                if (!_liveIds.Contains(pair.Key))
                {
                    _staleIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < _staleIds.Count; i++)
            {
                Despawn(_staleIds[i]);
            }

            for (int i = 0; i < train.StructureCount; i++)
            {
                if (train.TryGetStructureAt(i, out StructureEntry entry) && !_views.ContainsKey(entry.Id))
                {
                    Spawn(entry);
                }
            }
        }

        private void Spawn(StructureEntry entry)
        {
            if (_catalog == null || _layoutSettings == null || _views.ContainsKey(entry.Id))
            {
                return;
            }

            GameObject prefab = _catalog.GetViewPrefab(entry.Kind);
            if (prefab == null)
            {
                GameLog.Warn(LogCategory.Train, $"{entry.Kind} 실물 프리팹이 카탈로그에 없다 — 항목 #{entry.Id} 표현 생략");
                return;
            }

            // 스폰 지점은 상태가 계산한다 — 프리뷰·사거리 검증·창고 접근과 같은 한 지점을 쓴다.
            if (!ServiceLocator.TryGet(out ITrainState train)
                || !train.TryGetStructureCenter(entry.Id, out Vector3 position))
            {
                return;
            }

            Quaternion rotation = Quaternion.Euler(0f, entry.Rotation * 90f, 0f);
            Transform parent = CarViewAnchor.ResolveForCar(entry.CarIndex, _carTransforms);

            StructureView view = PoolManager.Spawn(prefab, position, rotation, parent)
                .GetComponent<StructureView>();
            if (view == null)
            {
                GameLog.Warn(LogCategory.Train, $"{entry.Kind} 프리팹 루트에 StructureView가 없다 — 항목 #{entry.Id}");
                return;
            }

            view.Bind(entry);

            // 가변 크기 종류(천막)는 프리팹 하나가 여러 크기로 선다 — 실제 발자국을 뷰에 알린다.
            // 회전은 위에서 Transform에 이미 걸었으므로 회전 전 값을 넘긴다 (천막 계획 §4.7).
            // GetComponent를 쓴다 — TryGetComponent는 <b>인터페이스를 찾지 못한다</b>.
            // 조용히 false를 돌려주므로 천막이 프리팹 원본 크기(기둥 넷이 한 점에 겹친 상태)로 선다.
            var footprintView = view.GetComponent<IStructureFootprintView>();
            if (footprintView != null)
            {
                footprintView.ApplyFootprint(entry.FootprintWidth, entry.FootprintLength,
                    _layoutSettings.StructureCellSize);
            }
            _views.Add(entry.Id, view);
        }

        private void Despawn(int structureId)
        {
            if (_views.TryGetValue(structureId, out StructureView view))
            {
                _views.Remove(structureId);
                if (view != null)
                {
                    PoolManager.Despawn(view.gameObject);
                }
            }
        }

        private void DespawnAll()
        {
            foreach (KeyValuePair<int, StructureView> pair in _views)
            {
                if (pair.Value != null)
                {
                    PoolManager.Despawn(pair.Value.gameObject);
                }
            }

            _views.Clear();
        }
    }
}
