using System;
using System.Collections.Generic;
using Game.Core.Events;
using Game.Core.Services;
using Game.Gameplay.World;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 열차 편성 상태 모델 — 호스트 권위 (개발 가이드 §6.3: 칸 배열·연결부·건축물을 호스트가 소유하는 단일 상태 모델).
    /// 규칙 판정은 순수 <see cref="TrainStateLogic"/>이 담당하고, 여기서는 <see cref="NetworkList{T}"/> 복제와
    /// 변이 확정·권위 이벤트 발행만 맡는다. 변화는 원자적으로 확정 후 전파되므로 클라이언트에 부분 적용 상태가 보이지 않는다.
    /// 기관차(인덱스 0)는 파괴 불가 — 불변식은 <see cref="TrainStateLogic"/>이 강제한다.
    /// Train 루트(씬 NetworkObject)에 1개 배치한다.
    /// </summary>
    // 오프셋 소비자(CarView -100, HandrailAnchor 등)보다 먼저 서버 시뮬·클라 표시 보간을 갱신해,
    // 칸과 손잡이가 같은 프레임의 동일한 오프셋 값을 읽게 한다(어긋나면 손잡이가 칸에서 떠 보인다).
    [DefaultExecutionOrder(-150)]
    public sealed class TrainState : NetworkBehaviour,
        ITrainState, ITrainDamageSink, ITrainGrabResistance, ITrainRepairSink, ITrainExpansion, ITrainRecouple,
        IFuelLoadProvider
    {
        [SerializeField] private TrainLayoutSettings _layoutSettings;
        [SerializeField] private TrainDurabilitySettings _durabilitySettings;
        [SerializeField] private TrainExpansionSettings _expansionSettings;
        [SerializeField] private StructureCatalog _structureCatalog;

        private readonly NetworkList<CarState> _cars = new NetworkList<CarState>();
        private readonly NetworkList<CouplingState> _couplings = new NetworkList<CouplingState>();

        // 칸 위 붙박이 건축물 — 인덱스 = 칸 인덱스 1:1 (기획서 §9 — 건축물 개별 파괴).
        private readonly NetworkList<StructureState> _structures = new NetworkList<StructureState>();

        // 이탈 칸이 슬롯 기준 뒤로 밀려난 거리(m) — 호스트가 시뮬레이션해 복제한다(손잡이-이탈저항 스펙 §6).
        private readonly NetworkList<float> _ejectOffsets = new NetworkList<float>();

        // 칸별 손잡이 잡은 인원 수 복제 — 클라 표시 재시뮬의 저항 입력(§M3 피드백 4). 판정 진실은 호스트 _grabberCounts.
        private readonly NetworkList<int> _grabberCountsSync = new NetworkList<int>();

        // 표현이 완전히 사라지도록 소실 거리에서 더 물러난 뒤 시뮬을 멈추는 여유 거리(m).
        private const float EjectFreezeExtraMeters = 40f;

        // 호스트 전용 — 칸별 손잡이 잡은 인원 수, 시뮬 정지 여부(복제 불필요, 결과 offset만 복제).
        private int[] _grabberCounts;
        private bool[] _ejectSettled;

        // 호스트 전용 — 칸별 현재 밀림 속도(m/s). 분리 순간 0(관성으로 열차를 따라감)에서 감속도만큼 서서히 오른다.
        private float[] _ejectPushSpeeds;

        // 클라 전용 — 이탈 오프셋 표시 재시뮬 상태. 호스트와 같은 수식·입력(스크롤 속도 + 설정 + 복제 손잡이 인원)으로
        // 매 프레임 연속 적분해 네트워크 틱 계단을 없애고, 복제 오프셋은 드리프트 보정 목표로만 쓴다(§M3 피드백 4).
        private float[] _displayOffsets;
        private float[] _displayPushSpeeds;

        // 표시-복제 오차가 이 이상이면(후발 접속 등) 보간 없이 즉시 복제 값으로 붙는다.
        private const float EjectDisplaySnapMeters = 10f;

        public int CarCount => _cars.Count;

        public int CouplingCount => _couplings.Count;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                ServerInitialize();
            }

            _cars.OnListChanged += OnCarsChanged;
            _couplings.OnListChanged += OnCouplingsChanged;
            _structures.OnListChanged += OnStructuresChanged;

            if (!ServiceLocator.IsRegistered<ITrainState>())
            {
                ServiceLocator.Register<ITrainState>(this);
            }

            if (!ServiceLocator.IsRegistered<ITrainDamageSink>())
            {
                ServiceLocator.Register<ITrainDamageSink>(this);
            }

            if (!ServiceLocator.IsRegistered<ITrainGrabResistance>())
            {
                ServiceLocator.Register<ITrainGrabResistance>(this);
            }

            if (!ServiceLocator.IsRegistered<ITrainRepairSink>())
            {
                ServiceLocator.Register<ITrainRepairSink>(this);
            }

            if (!ServiceLocator.IsRegistered<ITrainExpansion>())
            {
                ServiceLocator.Register<ITrainExpansion>(this);
            }

            if (!ServiceLocator.IsRegistered<ITrainRecouple>())
            {
                ServiceLocator.Register<ITrainRecouple>(this);
            }

            if (!ServiceLocator.IsRegistered<IFuelLoadProvider>())
            {
                ServiceLocator.Register<IFuelLoadProvider>(this);
            }

            // 스폰 시점의 편성으로 표현을 재동기화한다 — 신규 시작과 후발 접속(복제된 목록) 모두 이 경로로 반영된다.
            EventBus<TrainInitializedEvent>.Publish(new TrainInitializedEvent(_cars.Count));
        }

        public override void OnNetworkDespawn()
        {
            _cars.OnListChanged -= OnCarsChanged;
            _couplings.OnListChanged -= OnCouplingsChanged;
            _structures.OnListChanged -= OnStructuresChanged;

            if (ServiceLocator.TryGet(out ITrainState service) && ReferenceEquals(service, this))
            {
                ServiceLocator.Unregister<ITrainState>();
            }

            if (ServiceLocator.TryGet(out ITrainDamageSink sink) && ReferenceEquals(sink, this))
            {
                ServiceLocator.Unregister<ITrainDamageSink>();
            }

            if (ServiceLocator.TryGet(out ITrainGrabResistance resist) && ReferenceEquals(resist, this))
            {
                ServiceLocator.Unregister<ITrainGrabResistance>();
            }

            if (ServiceLocator.TryGet(out ITrainRepairSink repair) && ReferenceEquals(repair, this))
            {
                ServiceLocator.Unregister<ITrainRepairSink>();
            }

            if (ServiceLocator.TryGet(out ITrainExpansion expansion) && ReferenceEquals(expansion, this))
            {
                ServiceLocator.Unregister<ITrainExpansion>();
            }

            if (ServiceLocator.TryGet(out ITrainRecouple recouple) && ReferenceEquals(recouple, this))
            {
                ServiceLocator.Unregister<ITrainRecouple>();
            }

            if (ServiceLocator.TryGet(out IFuelLoadProvider load) && ReferenceEquals(load, this))
            {
                ServiceLocator.Unregister<IFuelLoadProvider>();
            }
        }

        private void Update()
        {
            if (IsServer)
            {
                ServerSimulateEjection();
            }
            else
            {
                ClientSmoothEjectionDisplay();
            }
        }

        // ── ITrainDamageSink — 리시버(CarView·CouplingPart)가 호출하는 호스트 전용 변이면 ──────────

        void ITrainDamageSink.ApplyCarDamage(int carIndex, float amount)
        {
            ServerApplyCarDamage(carIndex, amount);
        }

        void ITrainDamageSink.ApplyCouplingDamage(int couplingIndex, float amount)
        {
            ServerApplyCouplingDamage(couplingIndex, amount);
        }

        void ITrainDamageSink.ApplyStructureDamage(int carIndex, float amount)
        {
            ServerApplyStructureDamage(carIndex, amount);
        }

        public bool TryGetCar(int index, out CarState car)
        {
            if (index >= 0 && index < _cars.Count)
            {
                car = _cars[index];
                return true;
            }

            car = default;
            return false;
        }

        public bool TryGetCoupling(int index, out CouplingState coupling)
        {
            if (index >= 0 && index < _couplings.Count)
            {
                coupling = _couplings[index];
                return true;
            }

            coupling = default;
            return false;
        }

        public bool TryGetStructure(int index, out StructureState structure)
        {
            if (index >= 0 && index < _structures.Count)
            {
                structure = _structures[index];
                return true;
            }

            structure = default;
            return false;
        }

        public float GetEjectOffset(int index)
        {
            if (index < 0 || index >= _ejectOffsets.Count)
            {
                return 0f;
            }

            // 클라이언트에는 표시 보간 값을 준다 — 복제 계단이 탑승 시점 월드 떨림으로 보이지 않게(§M3 피드백 4).
            // 서버(권위 판정·시뮬)는 원값 그대로다.
            if (!IsServer && _displayOffsets != null && index < _displayOffsets.Length)
            {
                return _displayOffsets[index];
            }

            return _ejectOffsets[index];
        }

        /// <summary>이탈 중(미부착·미파괴)이고 소실 거리 전인 칸만 손잡이를 잡을 수 있다(손잡이-이탈저항 스펙 §5).</summary>
        public bool IsCarGrabbable(int index)
        {
            return TryGetCar(index, out CarState car)
                && !car.Attached && car.Health > 0f
                && _durabilitySettings != null
                && GetEjectOffset(index) < _durabilitySettings.LostDistance;
        }

        /// <summary>
        /// 살아 있는 연결부 중 가장 후미만 공격 대상이다(후미 순차 파괴 규칙 — <see cref="TrainStateLogic.IsCouplingTargetable"/>과
        /// 동일 규칙을 복제 목록에서 할당 없이 판정한다. 조회 빈도가 높아 스냅샷 배열을 만들지 않는다).
        /// </summary>
        public bool IsCouplingTargetable(int index)
        {
            if (!IsCouplingLive(index))
            {
                return false;
            }

            for (int i = index + 1; i < _couplings.Count; i++)
            {
                if (IsCouplingLive(i))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>갑판 낙하 판정의 폭·높이 여유 (m) — PlayerTemperature의 칸 위 판정과 같은 규약.</summary>
        private const float DeckApertureMargin = 0.5f;

        public bool TryGetDeckSurface(Vector3 position, out float deckHeight, out int carIndex)
        {
            deckHeight = 0f;
            carIndex = -1;
            if (_layoutSettings == null)
            {
                return false;
            }

            if (!TrainLayoutMath.IsWithinDeckAperture(
                position, _layoutSettings.CarWidth * 0.5f, _layoutSettings.DeckHeight, DeckApertureMargin))
            {
                return false;
            }

            for (int i = 0; i < CarCount; i++)
            {
                // 파괴된 칸은 갑판이 없다 — 이탈 칸은 갑판이 남아 있으므로 허용한다 (오프셋 반영. 창고 접근과 같은 규약).
                if (!TryGetCar(i, out CarState car) || car.Health <= 0f)
                {
                    continue;
                }

                if (_layoutSettings.IsZOnCar(position.z, i, GetEjectOffset(i)))
                {
                    deckHeight = _layoutSettings.DeckHeight;
                    carIndex = i;
                    return true;
                }
            }

            return false;
        }

        public bool IsDeckAlive(int carIndex)
        {
            if (!TryGetCar(carIndex, out CarState car) || car.Health <= 0f)
            {
                return false;
            }

            // 붙어 있는 칸은 항상 존재한다. 이탈 칸은 소실 거리(표현이 꺼지는 지점) 전까지만 —
            // 그 뒤로는 칸이 세상에서 사라진 것이므로 갑판 위 물건도 함께 회수돼야 한다.
            return car.Attached
                || (_durabilitySettings != null && GetEjectOffset(carIndex) < _durabilitySettings.LostDistance);
        }

        /// <summary>연결부가 끊기지 않았고 잇는 두 칸(index, index+1)이 모두 편성에 살아 붙어 있는지.</summary>
        private bool IsCouplingLive(int index)
        {
            return TryGetCoupling(index, out CouplingState coupling) && !coupling.Broken
                && TryGetCar(index, out CarState front) && TrainStateLogic.IsCarPresent(front)
                && TryGetCar(index + 1, out CarState rear) && TrainStateLogic.IsCarPresent(rear);
        }

        // ── ITrainGrabResistance — 손잡이 앵커가 호출하는 호스트 전용 저항 카운트 ──────────

        void ITrainGrabResistance.AddGrabber(int carIndex)
        {
            if (IsServer && _grabberCounts != null && carIndex >= 0 && carIndex < _grabberCounts.Length)
            {
                _grabberCounts[carIndex]++;
                SyncGrabberCount(carIndex);
            }
        }

        void ITrainGrabResistance.RemoveGrabber(int carIndex)
        {
            if (IsServer && _grabberCounts != null && carIndex >= 0 && carIndex < _grabberCounts.Length)
            {
                _grabberCounts[carIndex] = Mathf.Max(0, _grabberCounts[carIndex] - 1);
                SyncGrabberCount(carIndex);
            }
        }

        /// <summary>호스트 저항 인원을 복제 목록에 반영한다 — 클라 표시 재시뮬이 같은 저항으로 적분하게.</summary>
        private void SyncGrabberCount(int carIndex)
        {
            if (carIndex < _grabberCountsSync.Count && _grabberCountsSync[carIndex] != _grabberCounts[carIndex])
            {
                _grabberCountsSync[carIndex] = _grabberCounts[carIndex];
            }
        }

        int ITrainGrabResistance.GetGrabberCount(int carIndex)
        {
            return _grabberCounts != null && carIndex >= 0 && carIndex < _grabberCounts.Length
                ? _grabberCounts[carIndex]
                : 0;
        }

        // ── 호스트 권위: 변이 확정 (원자적으로 스냅샷 계산 후 일괄 반영) ──────────

        /// <summary>
        /// 칸에 데미지를 적용하고, 파괴되면 인접 연결부 절단 + 후방 연쇄 이탈까지 원자적으로 확정한다.
        /// 순수 로직으로 스냅샷을 계산한 뒤 두 NetworkList에 되쓴다 — 중간 상태가 복제되지 않는다.
        /// </summary>
        public void ServerApplyCarDamage(int index, float amount)
        {
            if (!IsServer)
            {
                return;
            }

            CarState[] cars = SnapshotCars();
            CarDamageResult result = TrainStateLogic.ApplyDamage(cars, index, amount);
            if (result == CarDamageResult.Ignored)
            {
                return;
            }

            if (result != CarDamageResult.Destroyed)
            {
                WriteBackCars(cars);
                return;
            }

            CouplingState[] couplings = SnapshotCouplings();
            int[] detached = TrainStateLogic.DestroyAndDetach(cars, index);

            // 칸 파괴 = 칸 위 건축물도 함께 소멸 — 창고였다면 내용물이 보따리로 지상에 떨어진다
            // (M5 8차 — 갑판이 사라지므로 지상 낙하. deckAlive는 복제 전 상태라 호출 문맥이 넘긴다).
            ServerDropStorageAsBundleIfPresent(index, deckAlive: false);

            // 파괴된 칸에 닿은 앞뒤 연결부를 끊는다 (뒤 연결부는 칸이 마지막이면 없다).
            var brokenCouplings = new List<int>(2);
            if (TrainStateLogic.BreakCoupling(couplings, index - 1))
            {
                brokenCouplings.Add(index - 1);
            }

            if (TrainStateLogic.BreakCoupling(couplings, index))
            {
                brokenCouplings.Add(index);
            }

            WriteBackCars(cars);
            WriteBackCouplings(couplings);

            BroadcastCarDestroyedRpc(index);
            foreach (int c in brokenCouplings)
            {
                BroadcastCouplingBrokenRpc(c);
            }

            if (detached.Length > 0)
            {
                BroadcastCarsDetachedRpc(detached);
            }
        }

        /// <summary>연결부에 데미지를 적용하고, 끊기면 후방 칸을 연쇄 이탈시킨다(기획서 §9 — 연결부 = 방어 목표).</summary>
        public void ServerApplyCouplingDamage(int index, float amount)
        {
            if (!IsServer)
            {
                return;
            }

            CarState[] cars = SnapshotCars();
            CouplingState[] couplings = SnapshotCouplings();
            CouplingDamageResult result = TrainStateLogic.ApplyCouplingDamage(couplings, cars, index, amount);
            if (result == CouplingDamageResult.Ignored)
            {
                return;
            }

            if (result != CouplingDamageResult.Broken)
            {
                WriteBackCouplings(couplings);
                return;
            }

            int[] detached = TrainStateLogic.DetachFrom(cars, index + 1);

            WriteBackCouplings(couplings);
            WriteBackCars(cars);

            BroadcastCouplingBrokenRpc(index);
            if (detached.Length > 0)
            {
                BroadcastCarsDetachedRpc(detached);
            }
        }

        /// <summary>칸 위 건축물에 데미지를 적용한다 — 칸과 달리 연쇄가 없어 건축물 하나만 원자적으로 갱신된다(기획서 §9).</summary>
        public void ServerApplyStructureDamage(int index, float amount)
        {
            if (!IsServer)
            {
                return;
            }

            CarState[] cars = SnapshotCars();
            StructureState[] structures = SnapshotStructures();
            CarDamageResult result = TrainStateLogic.ApplyStructureDamage(structures, cars, index, amount);
            if (result == CarDamageResult.Ignored)
            {
                return;
            }

            WriteBackStructures(structures);

            if (result == CarDamageResult.Destroyed)
            {
                // 창고 파괴 = 내용물이 보따리로 그 칸 갑판 위에 떨어진다 (M5 8차 — 칸은 살아 있다).
                // Kind는 파괴 후에도 남으므로 스냅샷에서 읽는다.
                if (structures[index].Kind == StructureKind.Storage)
                {
                    ServerDropStorageAsBundleIfPresent(index, deckAlive: true);
                }

                BroadcastStructureDestroyedRpc(index);
            }
        }

        /// <summary>
        /// 창고 내용물 소실 — 슬롯 재건(안전망) 확정 지점에서 명시 호출한다. 파괴 시점에 이미
        /// 보따리가 나왔으므로 여기서 또 내면 이중 생성이다 (M5 8차 착수 전 결정 — 소실 유지).
        /// 이벤트 구독이 아닌 직접 호출이라 누락 지점이 코드 리뷰에서 드러난다. 이탈은 소실이 아니다(재결합 보존).
        /// </summary>
        private void ServerClearStorageIfPresent(int index)
        {
            if (ServiceLocator.TryGet(out ITrainStorage storage))
            {
                storage.ServerClearStorage(index);
            }
        }

        /// <summary>
        /// 창고 내용물을 보따리로 내놓는다 (M5 8차) — 파괴 확정 지점(건축물 파괴 = 갑판 휴지 ·
        /// 칸 파괴 = 지상 낙하)에서 명시 호출한다. deckAlive는 호출 문맥이 넘긴다 —
        /// 칸 파괴 경로는 WriteBackCars 전이라 복제 상태(IsDeckAlive)로 판정할 수 없다.
        /// </summary>
        private void ServerDropStorageAsBundleIfPresent(int index, bool deckAlive)
        {
            if (ServiceLocator.TryGet(out ITrainStorage storage))
            {
                storage.ServerDropStorageAsBundle(index, deckAlive);
            }
        }

        // ── ITrainRepairSink — 수리 망치가 호출하는 호스트 전용 수리면 (기획서 §9) ──────────

        public bool ServerApplyRepair(TrainPartKind kind, int index, float amount)
        {
            if (!IsServer)
            {
                return false;
            }

            switch (kind)
            {
                case TrainPartKind.Car:
                {
                    CarState[] cars = SnapshotCars();
                    if (!TrainStateLogic.RepairCar(cars, index, amount))
                    {
                        return false;
                    }

                    WriteBackCars(cars);
                    return true;
                }

                case TrainPartKind.Coupling:
                {
                    CarState[] cars = SnapshotCars();
                    CouplingState[] couplings = SnapshotCouplings();
                    if (!TrainStateLogic.RepairCoupling(couplings, cars, index, amount))
                    {
                        return false;
                    }

                    WriteBackCouplings(couplings);
                    return true;
                }

                case TrainPartKind.Structure:
                {
                    CarState[] cars = SnapshotCars();
                    StructureState[] structures = SnapshotStructures();
                    if (!TrainStateLogic.RepairStructure(structures, cars, index, amount))
                    {
                        return false;
                    }

                    WriteBackStructures(structures);
                    return true;
                }

                default:
                    return false;
            }
        }

        // ── ITrainExpansion — 칸 건설(재건·후미 증설)·건축물 설치 (§M3) ──────────

        public int MaxCarCount => _expansionSettings != null ? _expansionSettings.MaxCarCount : CarCount;

        public int CarBuildCost => _expansionSettings != null ? _expansionSettings.CarBuildCost : 0;

        public int GetStructureBuildCost(StructureKind kind)
        {
            int fallback = _expansionSettings != null ? _expansionSettings.StructureBuildCost : 0;
            return _structureCatalog != null ? _structureCatalog.GetBuildCost(kind, fallback) : fallback;
        }

        public bool TryGetBuildSlot(out int slotIndex)
        {
            slotIndex = FindBuildSlot();
            return slotIndex >= 0;
        }

        /// <summary>
        /// 칸 1개를 짓는다 — 첫 빈 슬롯(파괴·소실)이면 그 자리 재건(앞 연결부 복구), 없으면 후미 증설.
        /// 관련 목록 변이가 같은 프레임에 커밋되므로 클라이언트에 부분 편성이 보이지 않는다.
        /// </summary>
        public bool ServerTryBuildCar()
        {
            if (!IsServer)
            {
                return false;
            }

            int slot = FindBuildSlot();
            if (slot < 0)
            {
                return false;
            }

            float carMax = MaxHealthFor(CarType.Standard);
            float couplingMax = _durabilitySettings != null ? _durabilitySettings.CouplingMaxHealth : 1f;
            bool rebuilt = slot < _cars.Count;

            if (slot == _cars.Count)
            {
                // 후미 증설 — 칸·연결부·건축물 슬롯·이탈 오프셋을 함께 늘린다.
                _cars.Add(new CarState
                {
                    Type = CarType.Standard,
                    Health = carMax,
                    MaxHealth = carMax,
                    Attached = true,
                });
                _couplings.Add(new CouplingState
                {
                    Health = couplingMax,
                    MaxHealth = couplingMax,
                    Broken = false,
                });
                _structures.Add(default);
                _ejectOffsets.Add(0f);
                _grabberCountsSync.Add(0);

                Array.Resize(ref _grabberCounts, _cars.Count);
                Array.Resize(ref _ejectSettled, _cars.Count);
                Array.Resize(ref _ejectPushSpeeds, _cars.Count);
            }
            else
            {
                // 빈 슬롯 재건 — 순수 로직으로 스냅샷을 고쳐 일괄 되쓴다.
                CarState[] cars = SnapshotCars();
                CouplingState[] couplings = SnapshotCouplings();
                StructureState[] structures = SnapshotStructures();
                TrainStateLogic.RebuildSlot(cars, couplings, structures, slot, carMax, couplingMax);

                WriteBackCars(cars);
                WriteBackCouplings(couplings);
                WriteBackStructures(structures);

                // 재건은 잔해 제거 — 그 자리 창고 내용물도 남아 있을 이유가 없다 (파괴 시점 소실의 안전망).
                ServerClearStorageIfPresent(slot);

                ResetEjectSimulation(slot);
            }

            BroadcastCarBuiltRpc(slot, rebuilt);
            return true;
        }

        public bool CanBuildStructure(int carIndex)
        {
            return TrainStateLogic.CanBuildStructureAt(SnapshotStructures(), SnapshotCars(), carIndex);
        }

        /// <summary>칸 위에 지정 종류의 건축물 1개를 설치한다 — 종류별 최대 체력(카탈로그)으로 시작, 파괴된 자리에는 새로 지을 수 있다.</summary>
        public bool ServerTryBuildStructure(int carIndex, StructureKind kind)
        {
            if (!IsServer)
            {
                return false;
            }

            CarState[] cars = SnapshotCars();
            StructureState[] structures = SnapshotStructures();
            float structureMax = _structureCatalog != null ? _structureCatalog.GetMaxHealth(kind, 1f) : 1f;
            if (!TrainStateLogic.BuildStructure(structures, cars, carIndex, kind, structureMax))
            {
                return false;
            }

            WriteBackStructures(structures);
            BroadcastStructureBuiltRpc(carIndex, kind);
            return true;
        }

        // ── ITrainRecouple — 이탈 칸 재결합 (손잡이-이탈저항 스펙 §4.1) ──────────

        public int RecoupleCost => _expansionSettings != null ? _expansionSettings.RecoupleCost : 0;

        public bool TryGetRecoupleTarget(out int carIndex)
        {
            float lostDistance = _durabilitySettings != null ? _durabilitySettings.LostDistance : float.MaxValue;
            carIndex = CarRecoupleAimLogic.FindRecoupleTarget(SnapshotCars(), SnapshotEjectOffsets(), lostDistance);
            return carIndex >= 0;
        }

        /// <summary>
        /// 슬롯까지 끌어온 이탈 칸을 편성에 다시 붙인다 — 칸 체력·칸 위 건축물은 그대로 두고
        /// 앞 연결부만 절반 체력으로 되살린다(스펙 §4.1). 슬롯 도달은 조준이 쓰는 표시 보간 값이 아니라
        /// 권위 오프셋으로 다시 본다. 두 목록 변이가 같은 프레임에 커밋되므로 부분 편성이 보이지 않는다.
        /// </summary>
        public bool ServerTryRecouple(int carIndex)
        {
            if (!IsServer || carIndex < 0 || carIndex >= _ejectOffsets.Count
                || _ejectOffsets[carIndex] > CarRecoupleAimLogic.SlotArrivalEpsilon)
            {
                return false;
            }

            CarState[] cars = SnapshotCars();
            CouplingState[] couplings = SnapshotCouplings();
            float couplingMax = _durabilitySettings != null ? _durabilitySettings.CouplingMaxHealth : 1f;
            if (!TrainStateLogic.Recouple(cars, couplings, carIndex, couplingMax))
            {
                return false;
            }

            WriteBackCars(cars);
            WriteBackCouplings(couplings);

            // 다시 떨어져 나가더라도 관성(속도 0)부터 새로 시작하게 한다. 잡고 있던 손잡이는 붙는 즉시
            // 잡기 조건(IsCarGrabbable)이 깨져 앵커 쪽에서 스스로 풀린다.
            ResetEjectSimulation(carIndex);

            BroadcastCarRecoupledRpc(carIndex);
            return true;
        }

        /// <summary>이탈 시뮬 흔적(오프셋·저항 인원·밀림 속도·정지 플래그)을 지워 제자리에서 다시 시작하게 한다.</summary>
        private void ResetEjectSimulation(int index)
        {
            if (index < _ejectOffsets.Count && _ejectOffsets[index] != 0f)
            {
                _ejectOffsets[index] = 0f;
            }

            if (_grabberCounts != null && index < _grabberCounts.Length)
            {
                _grabberCounts[index] = 0;
                _ejectSettled[index] = false;
                _ejectPushSpeeds[index] = 0f;
                SyncGrabberCount(index);
            }
        }

        /// <summary>지금 지을 슬롯 — 첫 빈 슬롯(파괴·소실) 우선, 없으면 후미 새 슬롯. 없으면 -1.</summary>
        private int FindBuildSlot()
        {
            float lostDistance = _durabilitySettings != null ? _durabilitySettings.LostDistance : float.MaxValue;
            return TrainStateLogic.FindBuildSlot(SnapshotCars(), SnapshotEjectOffsets(), lostDistance, MaxCarCount);
        }

        private float[] SnapshotEjectOffsets()
        {
            var snapshot = new float[_ejectOffsets.Count];
            for (int i = 0; i < _ejectOffsets.Count; i++)
            {
                snapshot[i] = _ejectOffsets[i];
            }

            return snapshot;
        }

        // ── IFuelLoadProvider — 연료 소모 가중치 입력 (기획서 §7.1 트레이드오프) ──────────

        /// <summary>기관차가 끌고 있는(연결·생존) 화물칸 수 — 칸이 이탈·파괴되면 즉시 줄어 소모도 가벼워진다.</summary>
        public int AttachedCarCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _cars.Count; i++)
                {
                    CarState car = _cars[i];
                    if (car.Type != CarType.Locomotive && TrainStateLogic.IsCarPresent(car))
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        private void ServerInitialize()
        {
            _cars.Clear();
            _couplings.Clear();
            _structures.Clear();

            int count = _layoutSettings != null ? _layoutSettings.CarCount : 0;
            var order = new CarType[count];
            for (int i = 0; i < count; i++)
            {
                // 선두(0) = 기관차, 나머지 = 일반 화물칸. 온실칸 등 증설은 §M3 후속 단계에서 편입한다.
                order[i] = i == TrainStateLogic.LocomotiveIndex ? CarType.Locomotive : CarType.Standard;
            }

            CarState[] cars = TrainStateLogic.BuildInitialCars(order, MaxHealthFor);
            for (int i = 0; i < cars.Length; i++)
            {
                _cars.Add(cars[i]);
            }

            float couplingMax = _durabilitySettings != null ? _durabilitySettings.CouplingMaxHealth : 1f;
            CouplingState[] couplings = TrainStateLogic.BuildInitialCouplings(count, couplingMax);
            for (int i = 0; i < couplings.Length; i++)
            {
                _couplings.Add(couplings[i]);
            }

            // 건축물 슬롯은 전부 빈 상태로 시작한다 — 건축물은 설치(수리 망치 우클릭)로만 생긴다.
            StructureState[] structures = TrainStateLogic.BuildInitialStructures(count);
            for (int i = 0; i < structures.Length; i++)
            {
                _structures.Add(structures[i]);
            }

            _ejectOffsets.Clear();
            _grabberCountsSync.Clear();
            for (int i = 0; i < count; i++)
            {
                _ejectOffsets.Add(0f);
                _grabberCountsSync.Add(0);
            }

            _grabberCounts = new int[count];
            _ejectSettled = new bool[count];
            _ejectPushSpeeds = new float[count];
        }

        /// <summary>이탈-멀쩡한 칸을 손잡이 저항을 반영해 매 프레임 이동시킨다(호스트, 손잡이-이탈저항 스펙 §4·§6).</summary>
        private void ServerSimulateEjection()
        {
            if (_durabilitySettings == null || _ejectOffsets.Count != _cars.Count)
            {
                return;
            }

            float scrollSpeed = ServiceLocator.TryGet(out IWorldScrollService scroll) ? scroll.ScrollSpeed : 0f;
            float targetPushSpeed = EjectMotionMath.ComputeTargetPushSpeed(scrollSpeed, _durabilitySettings.EjectExtraSpeed);
            float dt = Time.deltaTime;

            for (int i = 0; i < _cars.Count; i++)
            {
                CarState car = _cars[i];
                // 연쇄 이탈로 떨어져 나갔지만 파괴는 아닌 칸만 이동한다. 정상·파괴 칸은 스킵.
                bool ejecting = !car.Attached && car.Health > 0f;
                if (!ejecting)
                {
                    // 다음 분리가 다시 관성(속도 0)부터 시작하도록 리셋해 둔다.
                    _ejectPushSpeeds[i] = 0f;
                    continue;
                }

                if (_ejectSettled[i])
                {
                    continue;
                }

                // 분리 직후엔 관성으로 열차를 따라가다 감속도만큼 서서히 뒤처진다(밀림 속도 0 → 목표 램프).
                float pushSpeed = EjectMotionMath.StepPushSpeed(
                    _ejectPushSpeeds[i], targetPushSpeed, _durabilitySettings.EjectDeceleration, dt);
                _ejectPushSpeeds[i] = pushSpeed;

                int grabbers = _grabberCounts[i];
                float netVelocity = EjectMotionMath.ComputeNetVelocity(pushSpeed, grabbers, _durabilitySettings.PullPerGrabber);
                float next = EjectMotionMath.StepOffset(_ejectOffsets[i], netVelocity, dt);

                if (!Mathf.Approximately(next, _ejectOffsets[i]))
                {
                    _ejectOffsets[i] = next;
                }

                // 표현이 완전히 사라진 뒤에야 시뮬을 멈춘다 — 소실 칸이 화면에 프리즈된 채 남지 않게 한다.
                if (next >= _durabilitySettings.LostDistance + EjectFreezeExtraMeters)
                {
                    _ejectSettled[i] = true;
                }
            }
        }

        /// <summary>
        /// 클라 표시 재시뮬 — 호스트 <see cref="ServerSimulateEjection"/>과 같은 수식·입력(스크롤 속도, 설정,
        /// 복제 손잡이 인원)으로 오프셋을 매 프레임 연속 적분해 네트워크 틱 계단을 없애고, 복제 오프셋과의
        /// 드리프트만 저속 지수 감쇠로 보정한다. 수신 간격으로 속도를 추정하는 방식은 틱 도착의 프레임 양자화
        /// 때문에 추정 속도가 진동해 잔여 떨림이 남는다 — 재시뮬은 호스트와 동일한 연속 이동을 만든다.
        /// <see cref="GetEjectOffset"/>이 이 값을 돌려주므로 칸·손잡이·건축물 표현이 함께 부드러워진다.
        /// </summary>
        private void ClientSmoothEjectionDisplay()
        {
            int count = _ejectOffsets.Count;
            if (count == 0 || _durabilitySettings == null)
            {
                return;
            }

            EnsureDisplayCapacity(count);

            float scrollSpeed = ServiceLocator.TryGet(out IWorldScrollService scroll) ? scroll.ScrollSpeed : 0f;
            float targetPushSpeed = EjectMotionMath.ComputeTargetPushSpeed(scrollSpeed, _durabilitySettings.EjectExtraSpeed);
            float dt = Time.deltaTime;

            for (int i = 0; i < count; i++)
            {
                float target = _ejectOffsets[i];

                // 이탈 중이 아닌 칸(정상·파괴·재건 직후)은 재시뮬 없이 원값을 그대로 따른다 — 재건 시 잔상 없이 즉시 복귀.
                bool ejecting = i < _cars.Count && !_cars[i].Attached && _cars[i].Health > 0f;
                if (!ejecting)
                {
                    ResetDisplaySlot(i, target);
                    continue;
                }

                // 분리 관측 시점부터 호스트와 같은 관성 램프(0 → 목표)를 다시 밟는다.
                _displayPushSpeeds[i] = EjectMotionMath.StepPushSpeed(
                    _displayPushSpeeds[i], targetPushSpeed, _durabilitySettings.EjectDeceleration, dt);
                int grabbers = i < _grabberCountsSync.Count ? _grabberCountsSync[i] : 0;
                float netVelocity = EjectMotionMath.ComputeNetVelocity(
                    _displayPushSpeeds[i], grabbers, _durabilitySettings.PullPerGrabber);

                _displayOffsets[i] = EjectMotionMath.StepDisplayOffset(
                    _displayOffsets[i], target, netVelocity, dt,
                    _durabilitySettings.EjectDisplayCorrectionRate, EjectDisplaySnapMeters);
            }
        }

        private void EnsureDisplayCapacity(int count)
        {
            if (_displayOffsets != null && _displayOffsets.Length >= count)
            {
                return;
            }

            int previous = _displayOffsets != null ? _displayOffsets.Length : 0;
            Array.Resize(ref _displayOffsets, count);
            Array.Resize(ref _displayPushSpeeds, count);

            // 새 슬롯(후발 접속·후미 증설)은 현재 복제 값에서 시작한다 — 첫 프레임 워프 방지.
            for (int i = previous; i < count; i++)
            {
                ResetDisplaySlot(i, _ejectOffsets[i]);
            }
        }

        private void ResetDisplaySlot(int index, float target)
        {
            _displayOffsets[index] = target;
            _displayPushSpeeds[index] = 0f;
        }

        private float MaxHealthFor(CarType type)
        {
            return _durabilitySettings != null ? _durabilitySettings.MaxHealthFor(type) : float.PositiveInfinity;
        }

        private CarState[] SnapshotCars()
        {
            var snapshot = new CarState[_cars.Count];
            for (int i = 0; i < _cars.Count; i++)
            {
                snapshot[i] = _cars[i];
            }

            return snapshot;
        }

        private CouplingState[] SnapshotCouplings()
        {
            var snapshot = new CouplingState[_couplings.Count];
            for (int i = 0; i < _couplings.Count; i++)
            {
                snapshot[i] = _couplings[i];
            }

            return snapshot;
        }

        private StructureState[] SnapshotStructures()
        {
            var snapshot = new StructureState[_structures.Count];
            for (int i = 0; i < _structures.Count; i++)
            {
                snapshot[i] = _structures[i];
            }

            return snapshot;
        }

        private void WriteBackCars(CarState[] snapshot)
        {
            for (int i = 0; i < snapshot.Length && i < _cars.Count; i++)
            {
                if (!_cars[i].Equals(snapshot[i]))
                {
                    _cars[i] = snapshot[i];
                }
            }
        }

        private void WriteBackCouplings(CouplingState[] snapshot)
        {
            for (int i = 0; i < snapshot.Length && i < _couplings.Count; i++)
            {
                if (!_couplings[i].Equals(snapshot[i]))
                {
                    _couplings[i] = snapshot[i];
                }
            }
        }

        private void WriteBackStructures(StructureState[] snapshot)
        {
            for (int i = 0; i < snapshot.Length && i < _structures.Count; i++)
            {
                if (!_structures[i].Equals(snapshot[i]))
                {
                    _structures[i] = snapshot[i];
                }
            }
        }

        private void OnCarsChanged(NetworkListEvent<CarState> change)
        {
            if (change.Type == NetworkListEvent<CarState>.EventType.Value
                || change.Type == NetworkListEvent<CarState>.EventType.Add)
            {
                EventBus<CarStateChangedEvent>.Publish(new CarStateChangedEvent(change.Index, change.Value));
            }
        }

        private void OnCouplingsChanged(NetworkListEvent<CouplingState> change)
        {
            if (change.Type == NetworkListEvent<CouplingState>.EventType.Value
                || change.Type == NetworkListEvent<CouplingState>.EventType.Add)
            {
                EventBus<CouplingStateChangedEvent>.Publish(new CouplingStateChangedEvent(change.Index, change.Value));
            }
        }

        private void OnStructuresChanged(NetworkListEvent<StructureState> change)
        {
            if (change.Type == NetworkListEvent<StructureState>.EventType.Value
                || change.Type == NetworkListEvent<StructureState>.EventType.Add)
            {
                EventBus<StructureStateChangedEvent>.Publish(new StructureStateChangedEvent(change.Index, change.Value));
            }
        }

        // ── 권위 이벤트 전파 — 호스트 확정 후 전 피어에서 동일하게 발행(§M3) ──────────

        [Rpc(SendTo.Everyone)]
        private void BroadcastCarDestroyedRpc(int index)
        {
            EventBus<CarDestroyedEvent>.Publish(new CarDestroyedEvent(index));
        }

        [Rpc(SendTo.Everyone)]
        private void BroadcastCouplingBrokenRpc(int index)
        {
            EventBus<CouplingBrokenEvent>.Publish(new CouplingBrokenEvent(index));
        }

        [Rpc(SendTo.Everyone)]
        private void BroadcastCarsDetachedRpc(int[] indices)
        {
            EventBus<CarsDetachedEvent>.Publish(new CarsDetachedEvent(indices));
        }

        [Rpc(SendTo.Everyone)]
        private void BroadcastStructureDestroyedRpc(int index)
        {
            EventBus<StructureDestroyedEvent>.Publish(new StructureDestroyedEvent(index));
        }

        [Rpc(SendTo.Everyone)]
        private void BroadcastStructureBuiltRpc(int index, StructureKind kind)
        {
            EventBus<StructureBuiltEvent>.Publish(new StructureBuiltEvent(index, kind));
        }

        [Rpc(SendTo.Everyone)]
        private void BroadcastCarBuiltRpc(int index, bool rebuilt)
        {
            EventBus<CarBuiltEvent>.Publish(new CarBuiltEvent(index, rebuilt));
        }

        [Rpc(SendTo.Everyone)]
        private void BroadcastCarRecoupledRpc(int index)
        {
            EventBus<CarRecoupledEvent>.Publish(new CarRecoupledEvent(index));
        }
    }
}
