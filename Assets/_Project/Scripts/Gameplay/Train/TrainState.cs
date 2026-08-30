using Game.Core.Logging;
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

        // 칸 위 건축물 — 그리드 평탄 리스트 (건축 개편 1차, 결정 ③). 항목 존재 = 설치됨이고,
        // 이탈 칸의 항목도 carIndex로 남아 상태가 보존된다(재결합 시 무변경 복원 — ServerTryRecouple 규약).
        private readonly NetworkList<StructureEntry> _structures = new NetworkList<StructureEntry>();

        // 호스트 전용 — 건축물 Id 발급 일련번호 (1부터, 0 = 무효). 철거·피해 RPC의 안정 참조 키.
        private ushort _nextStructureId = 1;

        // 설치 판정용 점유 모양 버퍼 (천막 계획 결정 ⑥) — 순수 함수에 넘길 값을 매 프레임
        // 새로 할당하지 않으려고 재사용한다. 항목 수보다 길 수 있고, 판정은 인덱스 범위로만 읽는다.
        private StructureOccupancy[] _occupancyBuffer;

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

        // 클라 전용 — 마지막으로 관측한 저항 인원과 보정률 한시 상향 종료 시각 (M5 8차 — 7차 버그 5:
        // 인원 복제 지연 동안 옛 저항으로 적분된 드리프트가 저속 보정으로 고무줄처럼 남던 것을,
        // 인원 변화 관측 직후에만 보정률을 상향해 빠르게 회수한다).
        private int[] _displayGrabberCounts;
        private float[] _displayBoostUntil;

        // 표시-복제 오차가 이 이상이면(후발 접속 등) 보간 없이 즉시 복제 값으로 붙는다.
        private const float EjectDisplaySnapMeters = 10f;

        [Tooltip("QA — 이탈 칸의 복제 오프셋 vs 표시 오프셋 차를 주기 로그로 남긴다 (검증 R9 수치화용. 릴리스에서 끔).")]
        [SerializeField] private bool _qaLogEjectDisplayDrift;

        private const float EjectDriftLogIntervalSeconds = 0.5f;
        private float _nextDriftLogTime;

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

        void ITrainDamageSink.ApplyStructureDamage(int structureId, float amount)
        {
            ServerApplyStructureDamage(structureId, amount);
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

        public int StructureCount => _structures.Count;

        public bool TryGetStructureAt(int listIndex, out StructureEntry entry)
        {
            if (listIndex >= 0 && listIndex < _structures.Count)
            {
                entry = _structures[listIndex];
                return true;
            }

            entry = default;
            return false;
        }

        public bool TryGetStructureById(int structureId, out StructureEntry entry)
        {
            if (structureId > 0)
            {
                for (int i = 0; i < _structures.Count; i++)
                {
                    if (_structures[i].Id == structureId)
                    {
                        entry = _structures[i];
                        return true;
                    }
                }
            }

            entry = default;
            return false;
        }

        public bool TryGetStructureCenter(int structureId, out Vector3 center)
        {
            if (TryGetStructureById(structureId, out StructureEntry entry))
            {
                center = StructureCenter(entry);
                return true;
            }

            center = default;
            return false;
        }

        public bool TryGetNearestStructure(StructureKind kind, Vector3 from,
            out StructureEntry nearest, out Vector3 center)
        {
            nearest = default;
            center = default;
            if (_layoutSettings == null)
            {
                return false;
            }

            bool found = false;
            float bestSqr = float.PositiveInfinity;
            for (int i = 0; i < _structures.Count; i++)
            {
                StructureEntry entry = _structures[i];
                if (entry.Kind != kind || entry.Health <= 0f
                    || !TryGetCar(entry.CarIndex, out CarState car) || car.Health <= 0f)
                {
                    continue;
                }

                Vector3 point = StructureCenter(entry);
                float sqr = (from - point).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    nearest = entry;
                    center = point;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>
        /// 점유 중인 거치 무기인가 (M7 4차 §2.7) — 종류 판정은 카탈로그가, 점유 판정은
        /// <see cref="IMountedWeapons"/>가 소유한다. 거치 무기 축이 없는 세션(서비스 미등록)에서는
        /// 항상 거짓이라 기존 철거 경로가 무수정으로 통과한다.
        /// </summary>
        private bool IsOccupiedMountedWeapon(StructureEntry entry)
        {
            return _structureCatalog != null && _structureCatalog.IsMountedWeapon(entry.Kind)
                && ServiceLocator.TryGet(out IMountedWeapons mounted)
                && mounted.TryGetOccupant(entry.Id, out _);
        }

        /// <summary>
        /// 이 종류가 공유 저장 블록을 갖는지 — 카탈로그 플래그가 진실이다 (2차 §2.8).
        /// 창고 계열 종류를 추가해도 블록 할당·배출·재건 정리 경로에 코드 수정이 필요 없다(OCP).
        /// </summary>
        private bool ProvidesStorageBlock(StructureKind kind)
        {
            return _structureCatalog != null && _structureCatalog.ProvidesStorageBlock(kind);
        }

        /// <summary>
        /// 항목의 점유 영역 중심 월드 지점 (갑판 높이) — 이탈 오프셋을 반영한다. 프리뷰·사거리 검증·
        /// 뷰 스폰·창고 접근·제작 조회·보따리 배출이 <b>전부 이 한 지점</b>을 쓴다.
        /// </summary>
        private Vector3 StructureCenter(StructureEntry entry)
        {
            float centerZ = _layoutSettings.CarCenterZ(entry.CarIndex, GetEjectOffset(entry.CarIndex));
            StructureGridLogic.EntryCenterWorld(entry, centerZ,
                _layoutSettings.CarWidth, _layoutSettings.DeckLength, _layoutSettings.StructureCellSize,
                out float worldX, out float worldZ);
            return new Vector3(worldX, _layoutSettings.DeckHeight, worldZ);
        }

        public int CountStructures(StructureKind kind)
        {
            int count = 0;
            for (int i = 0; i < _structures.Count; i++)
            {
                StructureEntry entry = _structures[i];
                if (entry.Kind == kind && StructureGridLogic.IsAlive(entry))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 월드 지점이 살아 있는 칸 위 건축물의 점유 영역 안인가 — 몬스터 관통 방지(계획서 §2.10).
        /// 그리드 점유 조회(복제 데이터)라 서버 시뮬레이션에서 물리 쿼리 없이 판정된다.
        /// </summary>
        public bool IsStructureBlockingAt(Vector3 position, float padding)
        {
            // 지점이 얹힌 칸을 먼저 좁힌다 — 매 프레임 갑판 위 몬스터마다 도는 경로라, 다른 칸 위
            // 건축물까지 전부 훑으면 설치 수에 비례해 비싸진다 (건축 개편 이후 칸당 다중 설치).
            if (_layoutSettings == null || !TryGetCarAtZ(position.z, out int carIndex, out _, out float centerZ))
            {
                return false;
            }

            for (int i = 0; i < _structures.Count; i++)
            {
                StructureEntry entry = _structures[i];
                if (entry.CarIndex != carIndex || !StructureGridLogic.IsAlive(entry))
                {
                    continue;
                }

                if (StructureGridLogic.IsWorldPointOnEntry(entry, position.x, position.z, padding,
                    centerZ, _layoutSettings.CarWidth, _layoutSettings.DeckLength, _layoutSettings.StructureCellSize))
                {
                    return true;
                }
            }

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

        /// <summary>
        /// 갑판 낙하 판정의 <b>세로</b> 여유 (m) — 갑판면 바로 아래까지는 갑판 위로 친다.
        /// 가로 여유는 없다 (건축 개편 §7): 판자 유무가 갑판 폭을 바꾸므로 실측 반폭만 본다.
        /// </summary>
        private const float DeckSurfaceMargin = 0.5f;

        public bool TryGetCarAtZ(float worldZ, out int carIndex, out CarState car, out float carCenterZ)
        {
            if (_layoutSettings != null)
            {
                for (int i = 0; i < CarCount; i++)
                {
                    // 파괴된 칸은 갑판이 없다 — 이탈 칸은 갑판이 남아 있으므로 허용한다
                    // (오프셋 반영. 창고 접근과 같은 규약).
                    if (!TryGetCar(i, out CarState candidate) || candidate.Health <= 0f)
                    {
                        continue;
                    }

                    float ejectOffset = GetEjectOffset(i);
                    if (_layoutSettings.IsZOnCar(worldZ, i, ejectOffset))
                    {
                        carIndex = i;
                        car = candidate;
                        carCenterZ = _layoutSettings.CarCenterZ(i, ejectOffset);
                        return true;
                    }
                }
            }

            carIndex = -1;
            car = default;
            carCenterZ = 0f;
            return false;
        }

        public bool TryGetDeckSurface(Vector3 position, out float deckHeight, out int carIndex)
        {
            deckHeight = 0f;

            // 폭 게이트는 칸별로 본다 — 판자 증축이 칸마다 다르기 때문이다 (건축 개편 3차 §2.9).
            // Z는 칸 길이가 아니라 갑판 유효 길이로 좁힌다 — 앞뒤 끝 행은 콜라이더가 없어 밟을 수
            // 없으므로, 그 위에 물건이 얹히면 공중에 뜬다 (건축 개편 §7.2).
            if (!TryGetCarAtZ(position.z, out carIndex, out CarState car, out float carCenterZ)
                || !_layoutSettings.IsZOnDeck(position.z, carCenterZ)
                || !TrainLayoutMath.IsWithinDeckAperture(
                    position, DeckHalfWidth(car, position.x), _layoutSettings.DeckHeight, DeckSurfaceMargin))
            {
                carIndex = -1;
                return false;
            }

            deckHeight = _layoutSettings.DeckHeight;
            return true;
        }

        public float GetDeckHalfWidthAt(Vector3 position)
        {
            if (_layoutSettings == null)
            {
                return 0f;
            }

            return TryGetCarAtZ(position.z, out _, out CarState car, out _)
                ? DeckHalfWidth(car, position.x)
                : _layoutSettings.CarWidth * 0.5f;
        }

        /// <summary>그 칸의 그 쪽(X 부호) 갑판 반폭 — 판자 증축 반영 (건축 개편 3차).</summary>
        private float DeckHalfWidth(CarState car, float worldX)
        {
            int planks = worldX < 0f ? car.LeftPlanks : car.RightPlanks;
            return PlankGridLogic.DeckHalfWidth(
                _layoutSettings.CarWidth, _layoutSettings.StructureCellSize, planks);
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
            // (M5 8차 — 갑판이 사라지므로 지상 낙하). 블록 해제는 항목 제거 전에 — 배출 위치가 항목에서 나온다.
            ServerReleaseStorageBlocksOnCar(index, StorageReleaseMode.GroundBundle);
            ServerRemoveStructuresOnCar(index);

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

        /// <summary>
        /// 칸 위 건축물에 데미지를 적용한다 — 대상은 그리드 항목 Id (건축 개편 1차 — 안정 참조).
        /// 파괴되면 항목을 리스트에서 제거한다 — 그 자리에 새로 지을 수 있다. 몬스터 파괴는
        /// 자원 반환 경로를 타지 않는다 (요구사항 — 창고 보따리 배출만 예외).
        /// </summary>
        public void ServerApplyStructureDamage(int structureId, float amount)
        {
            if (!IsServer)
            {
                return;
            }

            CarState[] cars = SnapshotCars();
            StructureEntry[] structures = SnapshotStructures();
            if (!StructureGridLogic.TryFindById(structures, structureId, out int entryIndex))
            {
                return;
            }

            CarDamageResult result = StructureGridLogic.ApplyDamage(structures, cars, entryIndex, amount);
            if (result == CarDamageResult.Ignored)
            {
                return;
            }

            if (result != CarDamageResult.Destroyed)
            {
                if (!_structures[entryIndex].Equals(structures[entryIndex]))
                {
                    _structures[entryIndex] = structures[entryIndex];
                }

                return;
            }

            StructureEntry destroyed = structures[entryIndex];

            // 창고 파괴 = 내용물이 보따리로 그 자리 갑판 위에 떨어진다 (M5 8차 — 칸은 살아 있다).
            // 몬스터 파괴는 자원 반환 경로를 타지 않는다 — 보따리 배출만 예외 (요구사항).
            if (ProvidesStorageBlock(destroyed.Kind) && ServiceLocator.TryGet(out ITrainStorage storage))
            {
                storage.ServerReleaseBlock(destroyed.Id, StorageReleaseMode.DeckBundle);
            }

            _structures.RemoveAt(entryIndex);
            BroadcastStructureDestroyedRpc(destroyed.Id, destroyed.CarIndex, destroyed.Kind);
        }

        /// <summary>
        /// 칸 위 건축물 항목을 전부 제거한다 — 칸 파괴·슬롯 재건(잔해 제거) 확정 지점에서 호출한다(서버).
        /// 이탈은 제거가 아니다 — 항목이 carIndex로 남아 재결합 시 그대로 복원된다.
        /// 창고 블록 해제(<see cref="ServerReleaseStorageBlocksOnCar"/>)를 먼저 호출해야 한다 —
        /// 배출 위치·블록 매핑이 항목에서 나온다.
        /// </summary>
        private void ServerRemoveStructuresOnCar(int carIndex)
        {
            for (int i = _structures.Count - 1; i >= 0; i--)
            {
                if (_structures[i].CarIndex == carIndex)
                {
                    _structures.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 칸 위 창고 건축물들의 저장 블록을 해제한다 (건축 개편 2차 §2.8) — 칸 파괴(지상 투척)·
        /// 슬롯 재건(소실) 확정 지점에서 항목 제거 <b>전에</b> 호출한다(서버).
        /// </summary>
        private void ServerReleaseStorageBlocksOnCar(int carIndex, StorageReleaseMode mode)
        {
            if (!ServiceLocator.TryGet(out ITrainStorage storage))
            {
                return;
            }

            for (int i = 0; i < _structures.Count; i++)
            {
                StructureEntry entry = _structures[i];
                if (entry.CarIndex == carIndex && ProvidesStorageBlock(entry.Kind))
                {
                    storage.ServerReleaseBlock(entry.Id, mode);
                }
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
                    // 건축물의 index는 그리드 항목 Id다 (건축 개편 1차 — 부위 식별 규약).
                    CarState[] cars = SnapshotCars();
                    StructureEntry[] structures = SnapshotStructures();
                    if (!StructureGridLogic.TryFindById(structures, index, out int entryIndex)
                        || !StructureGridLogic.Repair(structures, cars, entryIndex, amount))
                    {
                        return false;
                    }

                    if (!_structures[entryIndex].Equals(structures[entryIndex]))
                    {
                        _structures[entryIndex] = structures[entryIndex];
                    }

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
                // 후미 증설 — 칸·연결부·이탈 오프셋을 함께 늘린다 (건축물 그리드는 칸 수와 무관한 평탄 리스트).
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
                TrainStateLogic.RebuildSlot(cars, couplings, slot, carMax, couplingMax);

                WriteBackCars(cars);
                WriteBackCouplings(couplings);

                // 재건은 잔해 제거 — 옛 건축물 항목(소실 칸의 보존분)과 그 창고 블록도 남아 있을
                // 이유가 없다. 소실이므로 보따리 없이 비운다 (블록 해제가 항목 제거보다 먼저).
                ServerReleaseStorageBlocksOnCar(slot, StorageReleaseMode.Discard);
                ServerRemoveStructuresOnCar(slot);

                ResetEjectSimulation(slot);
            }

            BroadcastCarBuiltRpc(slot, rebuilt);
            return true;
        }

        public bool CanPlaceStructure(int carIndex, int cellX, int cellZ, int rotation, StructureKind kind)
        {
            if (_structureCatalog == null)
            {
                return false;
            }

            _structureCatalog.GetFootprint(kind, out int width, out int length);
            return CanPlaceStructureSized(carIndex, cellX, cellZ, rotation, kind, width, length);
        }

        /// <summary>
        /// 크기를 지정한 설치 판정 (천막 계획 §4.2) — 가변 크기 종류는 카탈로그 발자국이 최소값일 뿐이라
        /// 실제 크기를 인자로 받는다. 점유 모양(<see cref="StructureOccupancy"/>)을 풀어 넘겨
        /// 천막 안쪽이 빈 자리로 취급되게 한다(결정 ⑥).
        /// </summary>
        public bool CanPlaceStructureSized(int carIndex, int cellX, int cellZ, int rotation,
            StructureKind kind, int width, int length)
        {
            if (_layoutSettings == null || _structureCatalog == null)
            {
                return false;
            }

            StructureEntry[] entries = QueryStructures();
            return StructureGridLogic.CanPlace(entries, ResolveOccupancies(entries), QueryCars(), carIndex,
                cellX, cellZ, rotation, kind, width, length, _structureCatalog.IsPlaceable(kind),
                _structureCatalog.GetOccupancy(kind),
                _layoutSettings.CarWidth, _layoutSettings.DeckLength, _layoutSettings.StructureCellSize);
        }

        /// <summary>
        /// 기존 항목들의 점유 모양을 항목 순서대로 푼다 — 순수 판정 함수가 카탈로그를 모르게 하려고
        /// 호출부에서 미리 풀어 넘기는 배열이다. 매 프레임 프리뷰가 호출하므로 버퍼를 재사용한다
        /// (버퍼가 항목 수보다 길 수 있으나 판정은 인덱스 범위로만 읽는다).
        /// </summary>
        private StructureOccupancy[] ResolveOccupancies(StructureEntry[] entries)
        {
            if (entries == null)
            {
                return null;
            }

            if (_occupancyBuffer == null || _occupancyBuffer.Length < entries.Length)
            {
                _occupancyBuffer = new StructureOccupancy[Mathf.Max(8, entries.Length)];
            }

            for (int i = 0; i < entries.Length; i++)
            {
                _occupancyBuffer[i] = _structureCatalog.GetOccupancy(entries[i].Kind);
            }

            return _occupancyBuffer;
        }

        /// <summary>
        /// 지정 자리(칸·셀·회전)에 건축물 1개를 설치한다 (건축 개편 1차) — 프리뷰와 같은 순수 판정을
        /// 다시 통과해야 확정된다. 종류별 최대 체력·점유 면적은 설치 시점 카탈로그 값을 항목에 싣는다.
        /// </summary>
        public bool ServerTryBuildStructure(int carIndex, int cellX, int cellZ, int rotation, StructureKind kind)
        {
            if (_structureCatalog == null)
            {
                return false;
            }

            _structureCatalog.GetFootprint(kind, out int catalogWidth, out int catalogLength);
            return ServerTryBuildStructureSized(carIndex, cellX, cellZ, rotation, kind,
                catalogWidth, catalogLength);
        }

        /// <summary>
        /// 크기를 지정해 건축물 1채를 설치한다 (천막 계획 §4.2) — 가변 크기 종류(천막)는 드래그가 정한
        /// 발자국이 항목에 실린다. 프리뷰와 같은 순수 판정(<see cref="CanPlaceStructureSized"/>)을
        /// 다시 통과해야 확정되는 규약은 고정 크기 경로와 같다.
        /// </summary>
        public bool ServerTryBuildStructureSized(int carIndex, int cellX, int cellZ, int rotation,
            StructureKind kind, int width, int length)
        {
            if (!IsServer || !CanPlaceStructureSized(carIndex, cellX, cellZ, rotation, kind, width, length))
            {
                return false;
            }

            float structureMax = _structureCatalog.GetMaxHealth(kind, 1f);
            var entry = new StructureEntry
            {
                Id = _nextStructureId++,
                CarIndex = (byte)carIndex,
                CellX = (byte)cellX,
                CellZ = (byte)cellZ,
                Rotation = (byte)(rotation & 3),
                Kind = kind,
                FootprintWidth = (byte)width,
                FootprintLength = (byte)length,
                Health = structureMax,
                MaxHealth = structureMax,
            };

            _structures.Add(entry);

            // 창고 설치 = 저장 블록 할당 (건축 개편 2차 §2.8 — 블록 = 건축물 Id).
            if (ProvidesStorageBlock(kind) && ServiceLocator.TryGet(out ITrainStorage storage))
            {
                storage.ServerAllocateBlock(entry.Id);
            }

            BroadcastStructureBuiltRpc(entry);
            return true;
        }

        public int GetStructureDemolishRefund(StructureKind kind)
        {
            float ratio = _expansionSettings != null ? _expansionSettings.DemolishRefundRatio : 0f;
            return StructureGridLogic.RefundAmount(GetStructureBuildCost(kind), ratio);
        }

        /// <summary>
        /// 건축물 철거 (건축 개편 2차 — 결정 ④·⑤): 창고면 내용물을 그 자리 갑판 보따리로 배출한 뒤
        /// 항목을 제거한다. 반환 자원 지급은 호출부(망치 RPC)가 이어서 확정한다.
        /// </summary>
        public bool ServerTryDemolishStructure(int structureId, out StructureEntry removed)
        {
            removed = default;
            if (!IsServer)
            {
                return false;
            }

            CarState[] cars = SnapshotCars();
            StructureEntry[] structures = SnapshotStructures();
            if (!StructureGridLogic.TryFindById(structures, structureId, out int entryIndex)
                || !StructureGridLogic.CanDemolish(structures, cars, entryIndex))
            {
                return false;
            }

            removed = structures[entryIndex];

            // 점유 중인 거치 무기는 철거되지 않는다 (M7 4차 §2.7) — 붙어 있는 사람의 발밑이
            // 사라지는 것을 남이 결정하게 두지 않는다. 내리면 곧바로 철거할 수 있다.
            if (IsOccupiedMountedWeapon(removed))
            {
                removed = default;
                return false;
            }

            // 창고 철거 — 내용물 보따리와 반환 자원 보따리는 별개로 스폰된다 (§2.5 — 묶으면
            // 해체 UI가 혼합 목록이 되어 혼란). 블록 해제는 항목 제거 전에.
            if (ProvidesStorageBlock(removed.Kind) && ServiceLocator.TryGet(out ITrainStorage storage))
            {
                storage.ServerReleaseBlock(removed.Id, StorageReleaseMode.DeckBundle);
            }

            _structures.RemoveAt(entryIndex);
            BroadcastStructureDemolishedRpc(removed.Id, removed.CarIndex, removed.Kind);
            return true;
        }

        // ── 판자 증축 (건축 개편 3차 — 결정 ⑥: 셀 열 단위) ──────────────────

        public int PlankBuildCost => _expansionSettings != null ? _expansionSettings.PlankBuildCost : 0;

        /// <summary>좌/우 각 최대 판자 열 수 — 증축 판정의 상한 (에셋 값, 좌표계 예약으로 클램프됨).</summary>
        private int MaxPlankColumns => _expansionSettings != null ? _expansionSettings.MaxPlankColumns : 0;

        public int PlankDemolishRefund
        {
            get
            {
                float ratio = _expansionSettings != null ? _expansionSettings.DemolishRefundRatio : 0f;
                return StructureGridLogic.RefundAmount(PlankBuildCost, ratio);
            }
        }

        public Game.Gameplay.Inventory.ResourceType PlankRefundResource => _expansionSettings != null
            ? _expansionSettings.PlankRefundResource
            : Game.Gameplay.Inventory.ResourceType.Wood;

        public bool CanBuildPlank(int carIndex, PlankSide side)
        {
            return PlankGridLogic.CanBuildPlank(QueryCars(), carIndex, side, MaxPlankColumns);
        }

        /// <summary>
        /// 칸 옆면에 판자 1열을 붙인다 (건축 개편 3차) — 프리뷰와 같은 순수 판정을 다시 통과해야
        /// 확정된다. 열 수만 늘리면 그리드 유효 열·갑판 폭·판자 뷰가 함께 따라온다.
        /// </summary>
        public bool ServerTryBuildPlank(int carIndex, PlankSide side)
        {
            return CanBuildPlank(carIndex, side) && ServerChangePlanks(carIndex, side, +1);
        }

        public bool CanRemovePlank(int carIndex, PlankSide side)
        {
            if (_layoutSettings == null)
            {
                return false;
            }

            return PlankGridLogic.CanRemovePlank(QueryStructures(), QueryCars(), carIndex, side,
                _layoutSettings.CarWidth, _layoutSettings.StructureCellSize);
        }

        /// <summary>
        /// 칸 옆면 가장 바깥 판자 1열을 뜯는다 (건축 개편 3차) — 그 열 위에 건축물이 있으면 기각된다
        /// (계획서 §2.9). 반환 자원 지급은 호출부(망치 RPC)가 이어서 확정한다.
        /// </summary>
        public bool ServerTryRemovePlank(int carIndex, PlankSide side)
        {
            return CanRemovePlank(carIndex, side) && ServerChangePlanks(carIndex, side, -1);
        }

        /// <summary>
        /// 판자 열 수를 한 칸 옮긴다 (증축 +1 / 철거 -1) — 판정은 호출부가 이미 통과시켰다.
        /// 별도 권위 이벤트가 없는 이유: <see cref="CarState"/> 자체가 복제되므로 이 대입이
        /// 전 피어에서 <see cref="CarStateChangedEvent"/>를 낳고, 판자 뷰는 그것으로 동기화된다.
        /// </summary>
        private bool ServerChangePlanks(int carIndex, PlankSide side, int delta)
        {
            if (!IsServer)
            {
                return false;
            }

            CarState car = _cars[carIndex];
            car.SetPlanks(side, (byte)Mathf.Max(0, car.Planks(side) + delta));
            _cars[carIndex] = car;
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

            // 건축물 그리드는 빈 리스트로 시작한다 — 건축물은 설치(수리 망치 우클릭)로만 생긴다.
            // Id 발급도 새 판 기준으로 되감는다 (재시작 = 씬 재로드 경로).
            _nextStructureId = 1;

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
            bool logDrift = _qaLogEjectDisplayDrift && Time.unscaledTime >= _nextDriftLogTime;

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

                // 저항 인원 변화 관측 (M5 8차 — 7차 버그 5): 복제 지연 동안 옛 인원으로 적분된
                // 드리프트를 빠르게 회수하도록 보정률을 한시 상향한다. 상시 상향은 틱 계단이 다시 보인다.
                if (grabbers != _displayGrabberCounts[i])
                {
                    _displayGrabberCounts[i] = grabbers;
                    _displayBoostUntil[i] = Time.unscaledTime + _durabilitySettings.EjectDisplayCorrectionBoostSeconds;
                }

                float correctionRate = _durabilitySettings.EjectDisplayCorrectionRate;
                if (Time.unscaledTime < _displayBoostUntil[i])
                {
                    correctionRate *= _durabilitySettings.EjectDisplayCorrectionBoostMultiplier;
                }

                _displayOffsets[i] = EjectMotionMath.StepDisplayOffset(
                    _displayOffsets[i], target, netVelocity, dt,
                    correctionRate, EjectDisplaySnapMeters);

                if (logDrift)
                {
                    // 검증 R9 수치화 — "세기 비교"를 육안이 아닌 수치로: 복제 원값과 표시의 차·저항 입력.
                    GameLog.Info(LogCategory.Train, $"이탈 표시 드리프트 #{i}: 복제={target:F2}m 표시={_displayOffsets[i]:F2}m "
                                              + $"차={target - _displayOffsets[i]:+0.00;-0.00}m 인원={grabbers} 표시속도={netVelocity:F2}m/s");
                }
            }

            if (logDrift)
            {
                _nextDriftLogTime = Time.unscaledTime + EjectDriftLogIntervalSeconds;
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
            Array.Resize(ref _displayGrabberCounts, count);
            Array.Resize(ref _displayBoostUntil, count);

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
            _displayGrabberCounts[index] = 0;
            _displayBoostUntil[index] = 0f;
        }

        private float MaxHealthFor(CarType type)
        {
            return _durabilitySettings != null ? _durabilitySettings.MaxHealthFor(type) : float.PositiveInfinity;
        }

        // 읽기 전용 판정용 재사용 버퍼 — 설치·판자 프리뷰는 조준 중 매 프레임 도는 경로라,
        // 변이 경로(Snapshot*/WriteBack*)와 달리 사본을 새로 만들지 않는다. 순수 로직이 배열을
        // 수정하지 않는 판정(Can*)에만 쓴다.
        private CarState[] _queryCars;
        private StructureEntry[] _queryStructures;

        private CarState[] QueryCars()
        {
            if (_queryCars == null || _queryCars.Length != _cars.Count)
            {
                _queryCars = new CarState[_cars.Count];
            }

            for (int i = 0; i < _cars.Count; i++)
            {
                _queryCars[i] = _cars[i];
            }

            return _queryCars;
        }

        private StructureEntry[] QueryStructures()
        {
            if (_queryStructures == null || _queryStructures.Length != _structures.Count)
            {
                _queryStructures = new StructureEntry[_structures.Count];
            }

            for (int i = 0; i < _structures.Count; i++)
            {
                _queryStructures[i] = _structures[i];
            }

            return _queryStructures;
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

        private StructureEntry[] SnapshotStructures()
        {
            var snapshot = new StructureEntry[_structures.Count];
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

        private void OnStructuresChanged(NetworkListEvent<StructureEntry> change)
        {
            switch (change.Type)
            {
                case NetworkListEvent<StructureEntry>.EventType.Add:
                case NetworkListEvent<StructureEntry>.EventType.Insert:
                    EventBus<StructureEntryChangedEvent>.Publish(
                        new StructureEntryChangedEvent(StructureListChange.Added, change.Value));
                    break;

                case NetworkListEvent<StructureEntry>.EventType.Value:
                    EventBus<StructureEntryChangedEvent>.Publish(
                        new StructureEntryChangedEvent(StructureListChange.Updated, change.Value));
                    break;

                case NetworkListEvent<StructureEntry>.EventType.Remove:
                case NetworkListEvent<StructureEntry>.EventType.RemoveAt:
                    EventBus<StructureEntryChangedEvent>.Publish(
                        new StructureEntryChangedEvent(StructureListChange.Removed, change.Value));
                    break;

                default:
                    // Clear·Full 등 목록 전체 변화 — 구독자는 리스트 전체를 다시 훑는다.
                    EventBus<StructureEntryChangedEvent>.Publish(
                        new StructureEntryChangedEvent(StructureListChange.Reset, default));
                    break;
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
        private void BroadcastStructureDestroyedRpc(int structureId, int carIndex, StructureKind kind)
        {
            EventBus<StructureDestroyedEvent>.Publish(new StructureDestroyedEvent(structureId, carIndex, kind));
        }

        [Rpc(SendTo.Everyone)]
        private void BroadcastStructureDemolishedRpc(int structureId, int carIndex, StructureKind kind)
        {
            EventBus<StructureDemolishedEvent>.Publish(new StructureDemolishedEvent(structureId, carIndex, kind));
        }

        [Rpc(SendTo.Everyone)]
        private void BroadcastStructureBuiltRpc(StructureEntry entry)
        {
            EventBus<StructureBuiltEvent>.Publish(new StructureBuiltEvent(entry));
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
