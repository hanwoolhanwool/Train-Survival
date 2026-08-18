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

        // 칸 인덱스 → 칸 트랜스폼 — 씬 정적 배치(Car_Locomotive/Car_N)를 첫 동기화 때 수집한다.
        private readonly Dictionary<int, Transform> _carTransforms = new Dictionary<int, Transform>();

        // 칸 인덱스 → 스케일 보정 앵커 — 칸 오브젝트는 비균등 스케일(4.6 × 3.4 × 15)의 보정 홀더라
        // 실물을 칸에 직접 붙이면 90° 회전 시 부모 축 스케일이 실물의 다른 축에 걸려 길게 늘어난다.
        // 월드 스케일 (1,1,1)로 되돌린 앵커를 칸마다 만들어 그 밑에 스폰한다 (이탈 이동은 그대로 따라간다).
        private readonly Dictionary<int, Transform> _carAnchors = new Dictionary<int, Transform>();

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

            CollectCarTransforms();

            _staleIds.Clear();
            foreach (KeyValuePair<int, StructureView> pair in _views)
            {
                if (!train.TryGetStructureById(pair.Key, out _))
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
                Debug.LogWarning($"[StructureViewSpawner] {entry.Kind} 실물 프리팹이 카탈로그에 없다 — 항목 #{entry.Id} 표현 생략");
                return;
            }

            float ejectOffset = ServiceLocator.TryGet(out ITrainState train) ? train.GetEjectOffset(entry.CarIndex) : 0f;
            float centerZ = _layoutSettings.CarCenterZ(entry.CarIndex, ejectOffset);
            StructureGridLogic.RotatedFootprint(entry.FootprintWidth, entry.FootprintLength, entry.Rotation,
                out int rotatedWidth, out int rotatedLength);
            StructureGridLogic.CellRegionCenterWorld(entry.CellX, entry.CellZ, rotatedWidth, rotatedLength,
                centerZ, _layoutSettings.CarWidth, _layoutSettings.CarLength, _layoutSettings.StructureCellSize,
                out float worldX, out float worldZ);

            var position = new Vector3(worldX, _layoutSettings.DeckHeight, worldZ);
            Quaternion rotation = Quaternion.Euler(0f, entry.Rotation * 90f, 0f);
            Transform parent = ResolveCarAnchor(entry.CarIndex);

            StructureView view = PoolManager.Spawn(prefab, position, rotation, parent)
                .GetComponent<StructureView>();
            if (view == null)
            {
                Debug.LogWarning($"[StructureViewSpawner] {entry.Kind} 프리팹 루트에 StructureView가 없다 — 항목 #{entry.Id}");
                return;
            }

            view.Bind(entry);
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

        /// <summary>칸 트랜스폼 수집 — 증설 예비 슬롯 포함 씬 정적 배치라 편성 변화 때 다시 모으면 충분하다.</summary>
        private void CollectCarTransforms()
        {
            _carTransforms.Clear();
            _carAnchors.Clear();
            foreach (CarView car in FindObjectsByType<CarView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                _carTransforms[car.CarIndex] = car.transform;
            }
        }

        private Transform ResolveCarTransform(int carIndex)
        {
            if (_carTransforms.Count == 0)
            {
                CollectCarTransforms();
            }

            return _carTransforms.TryGetValue(carIndex, out Transform car) ? car : null;
        }

        /// <summary>칸 밑의 스폰 앵커 — 스케일 보정 규약은 <see cref="CarViewAnchor"/>가 든다(판자 뷰와 공유).</summary>
        private Transform ResolveCarAnchor(int carIndex)
        {
            if (_carAnchors.TryGetValue(carIndex, out Transform cached) && cached != null)
            {
                return cached;
            }

            Transform anchor = CarViewAnchor.Resolve(ResolveCarTransform(carIndex));
            if (anchor != null)
            {
                _carAnchors[carIndex] = anchor;
            }

            return anchor;
        }
    }
}
