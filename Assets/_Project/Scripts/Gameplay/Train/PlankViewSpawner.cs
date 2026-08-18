using System.Collections.Generic;
using Game.Core.Events;
using Game.Core.Pooling;
using Game.Core.Services;
using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 칸 옆면 판자 증축의 실물을 스폰·회수한다 (건축 개편 3차 — 계획서 §2.9).
    /// 상태를 소유하지 않는 표현 계층: 판자 열 수는 <see cref="CarState.LeftPlanks"/>·
    /// <see cref="CarState.RightPlanks"/>가 진실이고, 여기서는 권위 이벤트
    /// (<see cref="CarStateChangedEvent"/> — 판자 열은 CarState가 나르므로 별도 이벤트가 필요 없다)와 초기 동기화
    /// (<see cref="TrainInitializedEvent"/> — 신규·후발 접속 공통)에 반응해
    /// <see cref="PoolManager"/>로 로컬 스폰만 한다.
    /// 실물은 칸 오브젝트의 스케일 보정 앵커(<see cref="CarViewAnchor"/>) 밑에 붙어 이탈 이동을 따라간다.
    /// 판자 크기는 그리드 셀·칸 길이에서 파생하므로 프리팹은 1 m 단위 상자 하나면 된다.
    /// Train 루트에 1개 배치한다.
    /// </summary>
    public sealed class PlankViewSpawner : MonoBehaviour
    {
        [Tooltip("판자 1열 실물 프리팹 — 1 m 정육면체 기준(런타임에 셀 크기·칸 길이·두께로 스케일된다). " +
            "NetworkObject 없음: 각 피어가 복제 상태를 보고 로컬 스폰한다.")]
        [SerializeField] private GameObject _plankPrefab;

        [SerializeField] private TrainLayoutSettings _layoutSettings;

        [Tooltip("판자 두께(m) — 상면이 갑판 높이와 맞도록 이 두께만큼 아래로 깔린다.")]
        [SerializeField, Min(0.01f)] private float _thickness = 0.15f;

        // 판자 열 키(칸·쪽·서수) → 실물. 키는 정수 하나로 인코딩해 딕셔너리 비용을 낮춘다.
        private readonly Dictionary<int, GameObject> _views = new Dictionary<int, GameObject>();

        // 칸 인덱스 → 칸 트랜스폼 — 씬 정적 배치(Car_Locomotive/Car_N)를 첫 동기화 때 수집한다.
        private readonly Dictionary<int, Transform> _carTransforms = new Dictionary<int, Transform>();

        private void OnEnable()
        {
            EventBus<TrainInitializedEvent>.Subscribe(OnTrainInitialized);
            EventBus<CarStateChangedEvent>.Subscribe(OnCarStateChanged);
            ResyncAll();
        }

        private void OnDisable()
        {
            EventBus<TrainInitializedEvent>.Unsubscribe(OnTrainInitialized);
            EventBus<CarStateChangedEvent>.Unsubscribe(OnCarStateChanged);
            DespawnAll();
        }

        private void OnTrainInitialized(TrainInitializedEvent _)
        {
            // 편성 자체가 바뀐 시점 — 증설 슬롯 포함 칸 트랜스폼을 다시 모은다.
            CarViewAnchor.CollectCars(_carTransforms);
            ResyncAll();
        }

        /// <summary>
        /// 칸 파괴·재건은 판자를 함께 없앤다 — 새 칸은 판자 열 0으로 시작하므로 그 칸만 맞추면 된다.
        /// 체력 변화(피격)로도 오는 이벤트라 편성 전체를 훑지 않는다.
        /// </summary>
        private void OnCarStateChanged(CarStateChangedEvent evt)
        {
            SyncCar(evt.Index);
        }

        /// <summary>
        /// 편성 전체 기준 재구성 — 신규 시작·후발 접속(복제된 상태) 경로.
        /// 상태에 없는 실물은 회수하고, 실물이 없는 판자 열은 스폰한다.
        /// </summary>
        private void ResyncAll()
        {
            if (!ServiceLocator.TryGet(out ITrainState train))
            {
                return;
            }

            for (int carIndex = 0; carIndex < train.CarCount; carIndex++)
            {
                SyncCar(carIndex);
            }
        }

        /// <summary>칸 하나의 판자 실물을 상태에 맞춘다 — 줄어든 열은 회수하고, 늘어난 열은 스폰한다.</summary>
        private void SyncCar(int carIndex)
        {
            if (_plankPrefab == null || _layoutSettings == null || !ServiceLocator.TryGet(out ITrainState train))
            {
                return;
            }

            // 이탈 칸의 판자는 칸을 따라 함께 흘러가야 하므로 '파괴되지 않았는가'만 본다
            // (CarView·StructureView의 이탈 표현 규약과 같다 — 소실 거리 처리는 PlankView가 맡는다).
            bool deckAlive = train.TryGetCar(carIndex, out CarState car) && car.Health > 0f;
            SyncSide(carIndex, PlankSide.Left, deckAlive ? car.LeftPlanks : 0);
            SyncSide(carIndex, PlankSide.Right, deckAlive ? car.RightPlanks : 0);
        }

        private void SyncSide(int carIndex, PlankSide side, int columns)
        {
            int count = StructureGridLogic.ClampPlankColumns(columns);
            for (int ordinal = 0; ordinal < count; ordinal++)
            {
                Spawn(carIndex, side, ordinal);
            }

            for (int ordinal = count; ordinal < StructureGridLogic.MaxPlankColumnsPerSide; ordinal++)
            {
                Despawn(Key(carIndex, side, ordinal));
            }
        }

        private void Spawn(int carIndex, PlankSide side, int ordinal)
        {
            int key = Key(carIndex, side, ordinal);
            if (_views.ContainsKey(key))
            {
                return;
            }

            Transform parent = CarViewAnchor.ResolveForCar(carIndex, _carTransforms);
            if (parent == null)
            {
                return;
            }

            float cellSize = _layoutSettings.StructureCellSize;
            int bodyColumns = StructureGridLogic.BodyColumns(_layoutSettings.CarWidth, cellSize);
            int rows = StructureGridLogic.Rows(_layoutSettings.CarLength, cellSize);
            float worldX = StructureGridLogic.ColumnCenterWorldX(
                StructureGridLogic.PlankColumn(side, ordinal, bodyColumns), bodyColumns, cellSize);

            float ejectOffset = ServiceLocator.TryGet(out ITrainState train) ? train.GetEjectOffset(carIndex) : 0f;
            var position = new Vector3(
                worldX,
                _layoutSettings.DeckHeight - _thickness * 0.5f,
                _layoutSettings.CarCenterZ(carIndex, ejectOffset));

            GameObject view = PoolManager.Spawn(_plankPrefab, position, Quaternion.identity, parent);
            view.transform.localScale = new Vector3(cellSize, _thickness, rows * cellSize);
            PlankView plank = view.GetComponent<PlankView>();
            if (plank != null)
            {
                plank.Bind(carIndex);
            }

            _views.Add(key, view);
        }

        private void Despawn(int key)
        {
            if (_views.TryGetValue(key, out GameObject view))
            {
                _views.Remove(key);
                if (view != null)
                {
                    PoolManager.Despawn(view);
                }
            }
        }

        private void DespawnAll()
        {
            foreach (KeyValuePair<int, GameObject> pair in _views)
            {
                if (pair.Value != null)
                {
                    PoolManager.Despawn(pair.Value);
                }
            }

            _views.Clear();
        }

        /// <summary>판자 열 하나의 딕셔너리 키 — 칸·쪽·서수를 정수 하나로 인코딩한다.</summary>
        private static int Key(int carIndex, PlankSide side, int ordinal)
        {
            return (carIndex * 100) + ((int)side * 10) + ordinal;
        }

    }
}
