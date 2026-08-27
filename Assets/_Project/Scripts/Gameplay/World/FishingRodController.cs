using Game.Core.Events;
using Game.Core.Logging;
using Game.Core.Services;
using Game.Gameplay.Region;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.World
{
    /// <summary>낚시 국면 — 소유자 표현과 서버 판정이 같은 값을 본다.</summary>
    public enum FishingPhase : byte
    {
        /// <summary>대기 — 던질 수 있다.</summary>
        Idle = 0,

        /// <summary>던졌고 입질을 기다린다.</summary>
        Waiting = 1,

        /// <summary>입질 중 — 챔질 창이 열려 있다.</summary>
        Biting = 2
    }

    /// <summary>낚시 국면이 바뀔 때 소유자에게 발행되는 로컬 표현 이벤트 (HUD·사운드용).</summary>
    public readonly struct FishingPhaseChangedLocalEvent
    {
        public readonly FishingPhase Phase;

        public FishingPhaseChangedLocalEvent(FishingPhase phase)
        {
            Phase = phase;
        }
    }

    /// <summary>물고기가 올라왔을 때 소유자에게 발행되는 로컬 표현 이벤트.</summary>
    public readonly struct FishCaughtLocalEvent
    {
        public readonly int Count;

        public FishCaughtLocalEvent(int count)
        {
            Count = count;
        }
    }

    /// <summary>
    /// 낚싯대 (바다 지역 구현 계획 §7) — <b>끌낚시</b>다.
    ///
    /// <para><b>왜 끌낚시인가.</b> 열차가 6 m/s로 달리므로 찌를 월드에 두면 3.3초 만에
    /// 사거리를 벗어난다. 찌를 열차 소속으로 두면 물리적으로도 맞고,
    /// <b>속도가 입질에 곱해져</b> 기관실 속도 조절이 낚시 전략이 된다.</para>
    ///
    /// <para><b>권위</b>: 대기 시간·챔질 판정·지급이 전부 서버다. 소유자는 입력만 보내고
    /// 국면 통지를 받아 표현한다 — 총기의 "보고 후 서버 재검증"과 같은 규약이다.</para>
    /// </summary>
    public sealed class FishingRodController : NetworkBehaviour
    {
        [SerializeField] private FishingSettings _settings;

        [Tooltip("조준 기준 — 보통 카메라. 비면 이 오브젝트의 forward를 쓴다.")]
        [SerializeField] private Transform _aimSource;

        [SerializeField] private Inventory.PlayerInventory _inventory;

        // 서버 상태
        private FishingPhase _serverPhase;
        private float _serverBiteAt;      // 입질 시각 (서버 시간)
        private float _serverBiteEndAt;   // 챔질 창이 닫히는 시각

        // 소유자 표현 상태
        private FishingPhase _ownerPhase;

        /// <summary>도구 슬롯 활성 여부 — <c>HotbarController</c>가 선택 슬롯 기준으로 제어한다.</summary>
        public bool InputEnabled { get; set; }

        /// <summary>소유자 화면이 보는 국면.</summary>
        public FishingPhase Phase => _ownerPhase;

        public override void OnNetworkSpawn()
        {
            if (_inventory == null)
            {
                _inventory = GetComponent<Inventory.PlayerInventory>();
            }
        }

        private void Update()
        {
            if (IsServer)
            {
                ServerTickBite();
            }

            if (!IsSpawned || !IsOwner || _settings == null)
            {
                return;
            }

            // 도구를 바꾸면 던져 둔 줄은 거둔다 — 다른 무기를 들고 낚시가 진행되면 안 된다.
            if (!InputEnabled)
            {
                if (_ownerPhase != FishingPhase.Idle)
                {
                    SetOwnerPhase(FishingPhase.Idle);
                    CancelServerRpc();
                }

                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            if (_ownerPhase == FishingPhase.Biting)
            {
                HookServerRpc();
                SetOwnerPhase(FishingPhase.Idle);
                return;
            }

            if (_ownerPhase == FishingPhase.Waiting)
            {
                // 기다리는 중 다시 누르면 거둔다 — 헛챔질로 낭비하지 않게.
                CancelServerRpc();
                SetOwnerPhase(FishingPhase.Idle);
                return;
            }

            if (!TryGetAim(out Vector3 origin, out Vector3 direction) || !TryGetWaterY(out float waterY))
            {
                return;
            }

            if (!FishingLogic.CanCast(origin, direction, waterY, _settings.CastRange))
            {
                return;
            }

            CastServerRpc(origin, direction);
            SetOwnerPhase(FishingPhase.Waiting);
        }

        private void SetOwnerPhase(FishingPhase phase)
        {
            if (_ownerPhase == phase)
            {
                return;
            }

            _ownerPhase = phase;
            EventBus<FishingPhaseChangedLocalEvent>.Publish(new FishingPhaseChangedLocalEvent(phase));
        }

        private bool TryGetAim(out Vector3 origin, out Vector3 direction)
        {
            Transform source = _aimSource != null ? _aimSource : transform;
            origin = source.position;
            direction = source.forward;
            return true;
        }

        private static bool TryGetWaterY(out float waterY)
        {
            waterY = 0f;

            if (!ServiceLocator.TryGet(out IRegionService region))
            {
                return false;
            }

            RegionDefinition definition = region.CurrentRegion;
            if (definition == null || !definition.HasWater)
            {
                return false;
            }

            waterY = definition.WaterSurfaceY;
            return true;
        }

        // ── 서버 ──

        private void ServerTickBite()
        {
            if (_serverPhase == FishingPhase.Waiting && Time.time >= _serverBiteAt)
            {
                _serverPhase = FishingPhase.Biting;
                _serverBiteEndAt = _serverBiteAt + _settings.HookWindowSeconds;
                NotifyBiteRpc();
                return;
            }

            // 창을 놓치면 조용히 풀린다 — 놓쳤다는 것 자체가 피드백이다.
            if (_serverPhase == FishingPhase.Biting && Time.time > _serverBiteEndAt)
            {
                _serverPhase = FishingPhase.Idle;
                NotifyMissRpc();
            }
        }

        [Rpc(SendTo.Server)]
        private void CastServerRpc(Vector3 origin, Vector3 direction)
        {
            if (_settings == null || _serverPhase != FishingPhase.Idle)
            {
                return;
            }

            // 소유자 판정을 그대로 믿지 않는다 — 물 지역인지·사거리 안인지 서버가 다시 본다.
            if (!TryGetWaterY(out float waterY)
                || !FishingLogic.CanCast(origin, direction.normalized, waterY, _settings.CastRange))
            {
                return;
            }

            float scrollSpeed = ServiceLocator.TryGet(out IWorldScrollService scroll) ? scroll.ScrollSpeed : 0f;
            float delay = FishingLogic.BiteDelaySeconds(
                Random.value, scrollSpeed, _settings.ReferenceScrollSpeed,
                _settings.MinBiteDelaySeconds, _settings.MaxBiteDelaySeconds, _settings.SpeedInfluence);

            _serverPhase = FishingPhase.Waiting;
            _serverBiteAt = Time.time + delay;
        }

        [Rpc(SendTo.Server)]
        private void CancelServerRpc()
        {
            _serverPhase = FishingPhase.Idle;
        }

        [Rpc(SendTo.Server)]
        private void HookServerRpc()
        {
            if (_settings == null || _serverPhase != FishingPhase.Biting)
            {
                return;
            }

            float sinceBite = Time.time - _serverBiteAt;
            _serverPhase = FishingPhase.Idle;

            if (!FishingLogic.IsWithinHookWindow(sinceBite, _settings.HookWindowSeconds))
            {
                NotifyMissRpc();
                return;
            }

            int count = FishingLogic.CatchCount(Random.value, _settings.DoubleCatchChance);
            if (_inventory == null || !_inventory.ServerTryAdd(_settings.CatchType, count))
            {
                // 가방이 차 있으면 놓친다 — 물고기가 사라지는 것이 아니라 올리지 못한 것이다.
                NotifyMissRpc();
                return;
            }

            NotifyCatchRpc(count);
        }

        // ── 소유자 통지 ──

        [Rpc(SendTo.Owner)]
        private void NotifyBiteRpc()
        {
            SetOwnerPhase(FishingPhase.Biting);
        }

        [Rpc(SendTo.Owner)]
        private void NotifyMissRpc()
        {
            SetOwnerPhase(FishingPhase.Idle);
        }

        [Rpc(SendTo.Owner)]
        private void NotifyCatchRpc(int count)
        {
            SetOwnerPhase(FishingPhase.Idle);
            EventBus<FishCaughtLocalEvent>.Publish(new FishCaughtLocalEvent(count));
            GameLog.Info(LogCategory.World, $"생선 {count}마리를 낚았다.");
        }
    }
}
