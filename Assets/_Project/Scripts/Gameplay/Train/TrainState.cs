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
        ITrainState, ITrainDamageSink, ITrainGrabResistance, ITrainRepairSink, ITrainExpansion, IFuelLoadProvider
    {
        [SerializeField] private TrainLayoutSettings _layoutSettings;
        [SerializeField] private TrainDurabilitySettings _durabilitySettings;
        [SerializeField] private TrainExpansionSettings _expansionSettings;

        private readonly NetworkList<CarState> _cars = new NetworkList<CarState>();
        private readonly NetworkList<CouplingState> _couplings = new NetworkList<CouplingState>();

        // 칸 위 붙박이 건축물 — 인덱스 = 칸 인덱스 1:1 (기획서 §9 — 건축물 개별 파괴).
        private readonly NetworkList<StructureState> _structures = new NetworkList<StructureState>();

        // 이탈 칸이 슬롯 기준 뒤로 밀려난 거리(m) — 호스트가 시뮬레이션해 복제한다(손잡이-이탈저항 스펙 §6).
        private readonly NetworkList<float> _ejectOffsets = new NetworkList<float>();

        // 표현이 완전히 사라지도록 소실 거리에서 더 물러난 뒤 시뮬을 멈추는 여유 거리(m).
        private const float EjectFreezeExtraMeters = 40f;

        // 호스트 전용 — 칸별 손잡이 잡은 인원 수, 시뮬 정지 여부(복제 불필요, 결과 offset만 복제).
        private int[] _grabberCounts;
        private bool[] _ejectSettled;

        // 호스트 전용 — 칸별 현재 밀림 속도(m/s). 분리 순간 0(관성으로 열차를 따라감)에서 감속도만큼 서서히 오른다.
        private float[] _ejectPushSpeeds;

        // 클라 전용 — 이탈 오프셋 표시 보간 상태. 복제(네트워크 틱) 계단으로 움직이는 오프셋을 추정 속도로
        // 매 프레임 연속 전진시켜, 탑승 시점에서 월드가 떨려 보이지 않게 한다(§M3 피드백 4). 권위 값은 불변.
        private float[] _displayOffsets;
        private float[] _displayVelocities;
        private float[] _displayTargets;
        private float[] _displayTargetTimes;

        // 표시-복제 오차가 이 이상이면(후발 접속 등) 보간 없이 즉시 복제 값으로 붙는다.
        private const float EjectDisplaySnapMeters = 10f;

        // 복제 갱신이 이만큼 멎으면 추정 속도를 버리고 목표 수렴만 한다 — 멈춘 칸을 오래된 속도로 계속 외삽하지 않게.
        private const float EjectDisplayStaleSeconds = 0.3f;

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
            }
        }

        void ITrainGrabResistance.RemoveGrabber(int carIndex)
        {
            if (IsServer && _grabberCounts != null && carIndex >= 0 && carIndex < _grabberCounts.Length)
            {
                _grabberCounts[carIndex] = Mathf.Max(0, _grabberCounts[carIndex] - 1);
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
                BroadcastStructureDestroyedRpc(index);
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

        public int StructureBuildCost => _expansionSettings != null ? _expansionSettings.StructureBuildCost : 0;

        public bool CanBuildCar()
        {
            return FindBuildSlot() >= 0;
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

                // 이탈 시뮬 흔적을 지워 제자리에서 다시 시작하게 한다.
                if (slot < _ejectOffsets.Count && _ejectOffsets[slot] != 0f)
                {
                    _ejectOffsets[slot] = 0f;
                }

                if (_grabberCounts != null && slot < _grabberCounts.Length)
                {
                    _grabberCounts[slot] = 0;
                    _ejectSettled[slot] = false;
                    _ejectPushSpeeds[slot] = 0f;
                }
            }

            BroadcastCarBuiltRpc(slot, rebuilt);
            return true;
        }

        public bool CanBuildStructure(int carIndex)
        {
            return TrainStateLogic.CanBuildStructureAt(SnapshotStructures(), SnapshotCars(), carIndex);
        }

        /// <summary>칸 위에 건축물 1개를 설치한다 — 최대 체력으로 시작, 파괴된 건축물 자리에는 새로 지을 수 있다.</summary>
        public bool ServerTryBuildStructure(int carIndex)
        {
            if (!IsServer)
            {
                return false;
            }

            CarState[] cars = SnapshotCars();
            StructureState[] structures = SnapshotStructures();
            float structureMax = _durabilitySettings != null ? _durabilitySettings.StructureMaxHealth : 1f;
            if (!TrainStateLogic.BuildStructure(structures, cars, carIndex, structureMax))
            {
                return false;
            }

            WriteBackStructures(structures);
            BroadcastStructureBuiltRpc(carIndex);
            return true;
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
            for (int i = 0; i < count; i++)
            {
                _ejectOffsets.Add(0f);
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
        /// 클라 표시 보간 — 네트워크 틱 계단으로 갱신되는 복제 오프셋을, 수신 간격으로 추정한 속도로 매 프레임
        /// 연속 전진시키고 오차는 지수 감쇠로 수렴시킨다(<see cref="WorldScrollController"/>의 표시 거리와 같은 방식).
        /// <see cref="GetEjectOffset"/>이 이 값을 돌려주므로 칸·손잡이·건축물 표현이 함께 부드러워진다.
        /// </summary>
        private void ClientSmoothEjectionDisplay()
        {
            int count = _ejectOffsets.Count;
            if (count == 0)
            {
                return;
            }

            EnsureDisplayCapacity(count);

            float now = Time.time;
            float correctionRate = _durabilitySettings != null ? _durabilitySettings.EjectDisplayCorrectionRate : 8f;

            for (int i = 0; i < count; i++)
            {
                float target = _ejectOffsets[i];

                // 이탈 중이 아닌 칸(정상·파괴·재건 직후)은 보간 없이 원값을 그대로 따른다 — 재건 시 잔상 없이 즉시 복귀.
                bool ejecting = i < _cars.Count && !_cars[i].Attached && _cars[i].Health > 0f;
                if (!ejecting)
                {
                    ResetDisplaySlot(i, target, now);
                    continue;
                }

                if (!Mathf.Approximately(target, _displayTargets[i]))
                {
                    _displayVelocities[i] = EjectMotionMath.EstimateReplicatedVelocity(
                        _displayTargets[i], target, now - _displayTargetTimes[i]);
                    _displayTargets[i] = target;
                    _displayTargetTimes[i] = now;
                }
                else if (now - _displayTargetTimes[i] > EjectDisplayStaleSeconds)
                {
                    _displayVelocities[i] = 0f;
                }

                _displayOffsets[i] = EjectMotionMath.StepDisplayOffset(
                    _displayOffsets[i], target, _displayVelocities[i], Time.deltaTime, correctionRate,
                    EjectDisplaySnapMeters);
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
            Array.Resize(ref _displayVelocities, count);
            Array.Resize(ref _displayTargets, count);
            Array.Resize(ref _displayTargetTimes, count);

            // 새 슬롯(후발 접속·후미 증설)은 현재 복제 값에서 시작한다 — 첫 프레임 워프 방지.
            float now = Time.time;
            for (int i = previous; i < count; i++)
            {
                ResetDisplaySlot(i, _ejectOffsets[i], now);
            }
        }

        private void ResetDisplaySlot(int index, float target, float now)
        {
            _displayOffsets[index] = target;
            _displayVelocities[index] = 0f;
            _displayTargets[index] = target;
            _displayTargetTimes[index] = now;
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
        private void BroadcastStructureBuiltRpc(int index)
        {
            EventBus<StructureBuiltEvent>.Publish(new StructureBuiltEvent(index));
        }

        [Rpc(SendTo.Everyone)]
        private void BroadcastCarBuiltRpc(int index, bool rebuilt)
        {
            EventBus<CarBuiltEvent>.Publish(new CarBuiltEvent(index, rebuilt));
        }
    }
}
