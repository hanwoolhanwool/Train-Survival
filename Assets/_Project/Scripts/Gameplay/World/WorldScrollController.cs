using System.Collections.Generic;
using Game.Core.Diagnostics;
using Game.Core.Events;
using Game.Core.Services;
using Game.Gameplay.Region;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 월드 스크롤 속도·누적 주행 거리의 호스트 권위 소유자 (네트워크 문서 §4.1, §8 해소 항목 ①안).
    /// 호스트가 두 NetworkVariable을 갱신하고, 클라이언트는 수신 값 사이를 속도로 외삽 + 스무딩해
    /// <see cref="IWorldScrollService"/>로 노출한다. Game 씬에 1개 배치한다.
    /// 지역 전환 경계 복제(M6 1차 §2.4)도 이 컴포넌트가 소유한다 — 전환을 판정하는
    /// RegionController는 MonoBehaviour라 NetworkList를 가질 수 없다.
    /// </summary>
    // 주행 거리는 지형·자원·플레이어가 모두 읽는 <b>기준값</b>이라 소비자보다 먼저 갱신돼야 한다.
    // 뒤에서 갱신되면 소비자마다 한 프레임 전 값과 이번 프레임 값이 섞여 서로 어긋난다.
    [DefaultExecutionOrder(-190)]
    public sealed class WorldScrollController : NetworkBehaviour, IWorldScrollService, IWorldScrollSpeedControl,
        ITerrainBoundaryService
    {
        [SerializeField] private WorldScrollSettings _settings;

        private readonly NetworkVariable<float> _scrollSpeed = new NetworkVariable<float>();
        private readonly NetworkVariable<float> _traveledDistance = new NetworkVariable<float>();

        // 지역 전환 경계 기록 (TileIndex 오름차순). 첫 항목은 세션 최초 지역(int.MinValue = 전 구간).
        private readonly NetworkList<TerrainRegionBoundary> _boundaries = new NetworkList<TerrainRegionBoundary>();

        // NetworkList는 IReadOnlyList가 아니라서 순수 로직(TileStreamingLogic)에 바로 못 넘긴다 —
        // 변경 시마다 재구성하는 미러로 조회를 받는다.
        private readonly List<TerrainRegionBoundary> _boundaryMirror = new List<TerrainRegionBoundary>();

        private float _displayDistance;

        /// <summary>연료 상태가 정하는 기본 속도 — 환경 배율과 별개 레이어로 보관한다.</summary>
        private float _baseSpeed;

        /// <summary>날씨 등 일시적 환경 개입의 배율.</summary>
        private float _environmentMultiplier = 1f;

        public float ScrollSpeed => _scrollSpeed.Value;

        public float TraveledDistance => IsServer ? _traveledDistance.Value : _displayDistance;

        public override void OnNetworkSpawn()
        {
            if (IsServer && _settings != null)
            {
                _baseSpeed = _settings.BaseScrollSpeed;
                _environmentMultiplier = 1f;
                _scrollSpeed.Value = _baseSpeed;
            }

            _displayDistance = _traveledDistance.Value;

            if (!ServiceLocator.IsRegistered<IWorldScrollService>())
            {
                ServiceLocator.Register<IWorldScrollService>(this);
            }

            if (!ServiceLocator.IsRegistered<IWorldScrollSpeedControl>())
            {
                ServiceLocator.Register<IWorldScrollSpeedControl>(this);
            }

            if (!ServiceLocator.IsRegistered<ITerrainBoundaryService>())
            {
                ServiceLocator.Register<ITerrainBoundaryService>(this);
            }

            _boundaries.OnListChanged += OnBoundariesChanged;
            if (IsServer)
            {
                EventBus<RegionChangedEvent>.Subscribe(OnRegionChanged);
            }

            // 후발 접속은 스폰 시점에 목록이 이미 동기화돼 있다(변경 콜백 없이) — 한 번 정렬해 알린다.
            RebuildBoundaryMirror();
            EventBus<TerrainRegionBoundariesChangedEvent>.Publish(default);
        }

        public override void OnNetworkDespawn()
        {
            _boundaries.OnListChanged -= OnBoundariesChanged;
            if (IsServer)
            {
                EventBus<RegionChangedEvent>.Unsubscribe(OnRegionChanged);
            }

            if (ServiceLocator.TryGet(out IWorldScrollService service) && ReferenceEquals(service, this))
            {
                ServiceLocator.Unregister<IWorldScrollService>();
            }

            if (ServiceLocator.TryGet(out IWorldScrollSpeedControl control) && ReferenceEquals(control, this))
            {
                ServiceLocator.Unregister<IWorldScrollSpeedControl>();
            }

            if (ServiceLocator.TryGet(out ITerrainBoundaryService boundary) && ReferenceEquals(boundary, this))
            {
                ServiceLocator.Unregister<ITerrainBoundaryService>();
            }
        }

        /// <summary>기본 스크롤 속도를 변경한다 (연료 감속·초가속 연출). 호스트 전용.</summary>
        public void SetScrollSpeed(float speed)
        {
            if (!IsServer)
            {
                return;
            }

            _baseSpeed = Mathf.Max(0f, speed);
            ApplyEffectiveSpeed();
        }

        /// <summary>환경 배율을 변경한다 (날씨 감속 등). 호스트 전용.</summary>
        public void SetEnvironmentSpeedMultiplier(float multiplier)
        {
            if (!IsServer)
            {
                return;
            }

            _environmentMultiplier = Mathf.Max(0f, multiplier);
            ApplyEffectiveSpeed();
        }

        private void ApplyEffectiveSpeed()
        {
            _scrollSpeed.Value = Mathf.Max(0f, _baseSpeed * _environmentMultiplier);
        }

        public int ResolveRegionIndex(int tileIndex)
        {
            return TileStreamingLogic.ResolveRegionIndex(tileIndex, _boundaryMirror);
        }

        /// <summary>열차 발밑 지형의 지역 — 계약과 이유는 <see cref="ITerrainBoundaryService"/>에 있다.</summary>
        public int RegionIndexAtTrain
        {
            get
            {
                if (_settings == null)
                {
                    return -1;
                }

                return ResolveRegionIndex(TileStreamingLogic.GetCenterTileIndex(
                    _traveledDistance.Value, _settings.TileLength));
            }
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            using (GameProfilerMarkers.WorldScrollUpdate.Auto())
            {
                if (IsServer)
                {
                    _traveledDistance.Value += _scrollSpeed.Value * Time.deltaTime;
                    ServerEnsureInitialBoundary();
                    ServerTrimPassedBoundaries();
                }
                else
                {
                    float correctionRate = _settings != null ? _settings.CorrectionRate : 5f;
                    _displayDistance = WorldScrollMath.SmoothToward(
                        _displayDistance, _traveledDistance.Value, _scrollSpeed.Value, Time.deltaTime, correctionRate);
                }
            }
        }

        // ── 지역 전환 경계 기록 (호스트 전용, M6 1차 §2.4) ─────────────────

        /// <summary>
        /// 세션 최초 지역 기록 — RegionChangedEvent 최초 발행이 이 컴포넌트의 스폰보다 빠를 수
        /// 있어(씬 로드 vs 네트워크 스폰 순서) 이벤트에만 의존하지 않고 매 프레임 보정한다.
        /// </summary>
        private void ServerEnsureInitialBoundary()
        {
            if (_boundaries.Count > 0
                || !ServiceLocator.TryGet(out IRegionService region) || region.CurrentRegion == null)
            {
                return;
            }

            _boundaries.Add(new TerrainRegionBoundary(int.MinValue, region.RegionIndex));
        }

        private void OnRegionChanged(RegionChangedEvent evt)
        {
            if (!IsSpawned || !IsServer || _settings == null)
            {
                return;
            }

            if (_boundaries.Count == 0)
            {
                _boundaries.Add(new TerrainRegionBoundary(int.MinValue, evt.RegionIndex));
                return;
            }

            TerrainRegionBoundary last = _boundaries[_boundaries.Count - 1];
            if (last.RegionIndex == evt.RegionIndex)
            {
                // 지역 1개 순환 등 같은 지역 재진입 알림 — 프리팹 경계가 아니다.
                return;
            }

            int tileIndex = TileStreamingLogic.GetBoundaryTileIndex(
                _traveledDistance.Value, _settings.TileLength, _settings.TilesAhead);
            if (tileIndex <= last.TileIndex)
            {
                // 같은 타일에 연속 전환이 겹치면(정지 중 전환 등) 마지막 경계를 새 지역으로 대체 —
                // 오름차순 불변식을 지킨다.
                _boundaries[_boundaries.Count - 1] = new TerrainRegionBoundary(last.TileIndex, evt.RegionIndex);
                return;
            }

            _boundaries.Add(new TerrainRegionBoundary(tileIndex, evt.RegionIndex));
        }

        /// <summary>후방 가시 구간 밖으로 지나간 경계를 잘라낸다 — 순환 지형이라 방치하면
        /// 세션 시간에 비례해 누적된다 (§2.4 트림 규칙).</summary>
        private void ServerTrimPassedBoundaries()
        {
            if (_settings == null || _boundaries.Count < 2)
            {
                return;
            }

            TileStreamingLogic.GetVisibleRange(
                _traveledDistance.Value, _settings.TileLength, _settings.TilesAhead, _settings.TilesBehind,
                out int firstVisible, out _);

            int trimmable = TileStreamingLogic.CountTrimmableBoundaries(_boundaryMirror, firstVisible);
            for (int i = 0; i < trimmable; i++)
            {
                _boundaries.RemoveAt(0);
            }
        }

        private void OnBoundariesChanged(NetworkListEvent<TerrainRegionBoundary> changeEvent)
        {
            RebuildBoundaryMirror();
            EventBus<TerrainRegionBoundariesChangedEvent>.Publish(default);
        }

        private void RebuildBoundaryMirror()
        {
            _boundaryMirror.Clear();
            for (int i = 0; i < _boundaries.Count; i++)
            {
                _boundaryMirror.Add(_boundaries[i]);
            }
        }
    }
}
