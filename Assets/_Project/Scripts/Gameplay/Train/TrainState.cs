using System.Collections.Generic;
using Game.Core.Events;
using Game.Core.Pooling;
using Game.Core.Services;
using Game.Gameplay.World;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 열차 편성 상태 모델 — 호스트 권위 (개발 가이드 §6.3: 칸 배열·연결부를 호스트가 소유하는 단일 상태 모델).
    /// 규칙 판정은 순수 <see cref="TrainStateLogic"/>이 담당하고, 여기서는 <see cref="NetworkList{T}"/> 복제와
    /// 변이 확정·권위 이벤트 발행만 맡는다. 변화는 원자적으로 확정 후 전파되므로 클라이언트에 부분 적용 상태가 보이지 않는다.
    /// 기관차(인덱스 0)는 파괴 불가 — 불변식은 <see cref="TrainStateLogic"/>이 강제한다.
    /// Train 루트(씬 NetworkObject)에 1개 배치한다.
    /// </summary>
    public sealed class TrainState : NetworkBehaviour, ITrainState, ITrainDamageSink, ITrainGrabResistance
    {
        [SerializeField] private TrainLayoutSettings _layoutSettings;
        [SerializeField] private TrainDurabilitySettings _durabilitySettings;

        [Tooltip("이탈 칸의 손잡이 그랩 앵커 프리팹(NetworkObject). 칸이 이탈할 때 앞·뒤 끝에 하나씩 스폰된다.")]
        [SerializeField] private HandrailAnchor _handrailAnchorPrefab;

        private readonly NetworkList<CarState> _cars = new NetworkList<CarState>();
        private readonly NetworkList<CouplingState> _couplings = new NetworkList<CouplingState>();

        // 이탈 칸이 슬롯 기준 뒤로 밀려난 거리(m) — 호스트가 시뮬레이션해 복제한다(손잡이-이탈저항 스펙 §6).
        private readonly NetworkList<float> _ejectOffsets = new NetworkList<float>();

        // 표현이 완전히 사라지도록 소실 거리에서 더 물러난 뒤 시뮬을 멈추는 여유 거리(m).
        private const float EjectFreezeExtraMeters = 40f;

        // 호스트 전용 — 칸별 손잡이 잡은 인원 수, 소실 앵커 정리 여부, 시뮬 정지 여부(복제 불필요, 결과 offset만 복제).
        private int[] _grabberCounts;
        private bool[] _lostMarked;
        private bool[] _ejectSettled;

        // 호스트 전용 — 이탈 칸별 스폰된 손잡이 앵커.
        private readonly Dictionary<int, List<HandrailAnchor>> _carAnchors = new Dictionary<int, List<HandrailAnchor>>();

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

            // 스폰 시점의 편성으로 표현을 재동기화한다 — 신규 시작과 후발 접속(복제된 목록) 모두 이 경로로 반영된다.
            EventBus<TrainInitializedEvent>.Publish(new TrainInitializedEvent(_cars.Count));
        }

        public override void OnNetworkDespawn()
        {
            _cars.OnListChanged -= OnCarsChanged;
            _couplings.OnListChanged -= OnCouplingsChanged;

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

            if (IsServer)
            {
                ServerDespawnAllAnchors();
            }
        }

        private void Update()
        {
            if (IsServer)
            {
                ServerSimulateEjection();
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

        public float GetEjectOffset(int index)
        {
            return index >= 0 && index < _ejectOffsets.Count ? _ejectOffsets[index] : 0f;
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
                ServerSpawnAnchorsForDetached(detached);
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
                ServerSpawnAnchorsForDetached(detached);
                BroadcastCarsDetachedRpc(detached);
            }
        }

        private void ServerInitialize()
        {
            _cars.Clear();
            _couplings.Clear();

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

            _ejectOffsets.Clear();
            for (int i = 0; i < count; i++)
            {
                _ejectOffsets.Add(0f);
            }

            _grabberCounts = new int[count];
            _ejectSettled = new bool[count];
            _lostMarked = new bool[count];
        }

        /// <summary>이탈-멀쩡한 칸을 손잡이 저항을 반영해 매 프레임 이동시킨다(호스트, 손잡이-이탈저항 스펙 §4·§6).</summary>
        private void ServerSimulateEjection()
        {
            if (_durabilitySettings == null || _ejectOffsets.Count != _cars.Count)
            {
                return;
            }

            float scrollSpeed = ServiceLocator.TryGet(out IWorldScrollService scroll) ? scroll.ScrollSpeed : 0f;
            float dt = Time.deltaTime;

            for (int i = 0; i < _cars.Count; i++)
            {
                CarState car = _cars[i];
                // 연쇄 이탈로 떨어져 나갔지만 파괴는 아닌 칸만 이동한다. 정상·파괴 칸은 스킵.
                bool ejecting = !car.Attached && car.Health > 0f;
                if (!ejecting || _ejectSettled[i])
                {
                    continue;
                }

                int grabbers = _grabberCounts[i];
                float netVelocity = EjectMotionMath.ComputeNetVelocity(
                    scrollSpeed, _durabilitySettings.EjectExtraSpeed, grabbers, _durabilitySettings.PullPerGrabber);
                float next = EjectMotionMath.StepOffset(_ejectOffsets[i], netVelocity, dt);

                if (!Mathf.Approximately(next, _ejectOffsets[i]))
                {
                    _ejectOffsets[i] = next;
                }

                // 소실 확정(회수 불가): 손잡이 앵커를 1회 정리한다(기획서 §9.1).
                if (!_lostMarked[i] && EjectMotionMath.IsCarLost(next, _durabilitySettings.LostDistance, grabbers))
                {
                    _lostMarked[i] = true;
                    ServerDespawnAnchorsFor(i);
                }

                // 표현이 완전히 사라진 뒤에야 시뮬을 멈춘다 — 소실 칸이 화면에 프리즈된 채 남지 않게 한다.
                if (next >= _durabilitySettings.LostDistance + EjectFreezeExtraMeters)
                {
                    _ejectSettled[i] = true;
                }
            }
        }

        // ── 손잡이 앵커 스폰/소멸 (손잡이-이탈저항 스펙 §5) ──────────────────

        private void ServerSpawnAnchorsForDetached(int[] detached)
        {
            if (_handrailAnchorPrefab == null || _layoutSettings == null)
            {
                return;
            }

            foreach (int carIndex in detached)
            {
                if (_carAnchors.ContainsKey(carIndex))
                {
                    continue;
                }

                ComputeCarEndPositions(carIndex, out Vector3 frontEnd, out Vector3 rearEnd);
                var anchors = new List<HandrailAnchor>(2)
                {
                    ServerSpawnAnchor(carIndex, frontEnd),
                    ServerSpawnAnchor(carIndex, rearEnd),
                };
                _carAnchors[carIndex] = anchors;
            }
        }

        private HandrailAnchor ServerSpawnAnchor(int carIndex, Vector3 endLocalPosition)
        {
            GameObject instance = PoolManager.Spawn(
                _handrailAnchorPrefab.gameObject, endLocalPosition, Quaternion.identity);
            var anchor = instance.GetComponent<HandrailAnchor>();
            anchor.ServerSetup(carIndex, endLocalPosition);
            anchor.NetworkObject.Spawn();
            return anchor;
        }

        private void ServerDespawnAnchorsFor(int carIndex)
        {
            if (!_carAnchors.TryGetValue(carIndex, out List<HandrailAnchor> anchors))
            {
                return;
            }

            foreach (HandrailAnchor anchor in anchors)
            {
                if (anchor != null && anchor.NetworkObject != null && anchor.NetworkObject.IsSpawned)
                {
                    anchor.NetworkObject.Despawn(true);
                }
            }

            _carAnchors.Remove(carIndex);
        }

        private void ServerDespawnAllAnchors()
        {
            foreach (List<HandrailAnchor> anchors in _carAnchors.Values)
            {
                foreach (HandrailAnchor anchor in anchors)
                {
                    if (anchor != null && anchor.NetworkObject != null && anchor.NetworkObject.IsSpawned)
                    {
                        anchor.NetworkObject.Despawn(true);
                    }
                }
            }

            _carAnchors.Clear();
        }

        /// <summary>슬롯 기준 칸 앞·뒤 끝의 손잡이 위치(열차 원점 좌표). 앞 끝(+Z)이 엔진 쪽 = 본대에서 잡기 쉬운 쪽.</summary>
        private void ComputeCarEndPositions(int carIndex, out Vector3 frontEnd, out Vector3 rearEnd)
        {
            float carLength = _layoutSettings.CarLength;
            float gap = _layoutSettings.CouplingGap;
            float centerZ = _layoutSettings.FrontZ - carLength * 0.5f - carIndex * (carLength + gap);
            float y = _layoutSettings.DeckHeight;

            frontEnd = new Vector3(0f, y, centerZ + carLength * 0.5f);
            rearEnd = new Vector3(0f, y, centerZ - carLength * 0.5f);
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
    }
}
