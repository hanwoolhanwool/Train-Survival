using Game.Core.Logging;
using Game.Core.Services;
using Game.Gameplay.Inventory;
using Game.Systems.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// QA 테스트용 디버그 핫키 (릴리스에서는 <see cref="_enableQaKeys"/>를 끈다).
    /// <para>
    /// 키는 숫자패드의 <b>한 행 = 한 기능 그룹</b>으로 맞춰 두었다 — 손이 행을 기억하면 키를 외울 필요가 없다.
    /// <code>
    ///   Num │ / 보스 소환  │ * 몬스터 1기 │ − 웨이브 토글   ← 몬스터·보스
    ///    7  │ 8 칸 건설    │ 9 부위 피해  │ + 재시작        ← 편성·건축
    ///  연결부│              │              │
    ///    4  │ 5 피해 실측  │ 6 창고 경합  │                ← 자원·피해
    /// 자원지급│             │              │
    ///    1 낮│ 2 밤         │ 3 다음 Day   │  ↵             ← 사이클(DayCycleController 소유)
    ///    0 동시 그랩        │ . 보스 처치  │
    /// </code>
    /// 사이클 행(1·2·3)만 <c>DayCycleController</c>가 소유하므로 이 컴포넌트에서 다시 쓰면 안 된다.
    /// </para>
    /// <b>편성·건축 (7·8·9 행 + F2)</b>
    /// - 숫자패드 7 : 현재 표적 가능한(후미) 연결부 1개 파괴(후방 연쇄 이탈 테스트).
    /// - 숫자패드 8 : 칸 1칸 무료 건설 — 빈 슬롯(파괴·소실) 재건 우선, 없으면 후미 증설(비용 경로는 건설 포트로 검증).
    /// - 숫자패드 9 : 샘플 데미지 30 — <b>망치로 겨눈 부위</b>가 있으면 그 부위에, 없으면
    ///   표적 연결부·후미 칸·건축물 순의 폴백(특정 건축물을 골라 손상시킬 수 있게 한다, M5 4차 C7).
    /// - 숫자패드 + : 게임 재시작(지금 돌고 있는 인게임 씬 재로드, 호스트 권위로 편성·웨이브·사이클 초기화).
    /// - <b>F2</b> : 열차·궤도 높이 단계 순환 — 현재 → 아래 → 더 아래 → 현재
    ///   (열차 높이 스펙 <c>docs/specs/world/train-elevation.md</c>). 편성·손잡이·설비·궤도 타일과
    ///   갑판 기준선이 <b>같은 오프셋</b>으로 함께 움직이므로 건설·콜라이더 판정이 어긋나지 않는다.
    ///   숫자패드는 이미 다 찼으므로 새 키는 F 계열(F5~F7이 비어 있다)에 붙인다.
    /// - <b>F4</b> : 망치로 <b>겨눈 자리</b>에 거치 무기 즉시 설치(무료) + 소총탄 60 지급 (M7 4차).
    ///   Shift와 함께 누르면 자동 터렛, 그냥 누르면 거치 기관총이다. 겨눈 자리가 없으면(망치를 들지
    ///   않았거나 갑판을 겨누지 않았으면) 아무 일도 하지 않는다.
    /// <b>자원·피해 (4·5·6 행 + 0)</b>
    /// - 숫자패드 4 : 요청자에게 자원 지급(건자재·제작 재료·식재료 — 증설 비용·연료 투입·요리 테스트).
    /// - 숫자패드 5 : 피해 실측(M5 6차 — 검증 H7). 요청자에게 고정 피해 20을 물리 경로로 넣는다 —
    ///   장비 감산이 적용되므로 맨몸 20 vs 가죽 옷 17을 실측할 수 있다.
    /// - 숫자패드 6 : 공유 창고 동시 경합 재현(M5 5차 — 검증 G2). 전 피어가 같은 프레임에
    ///   같은 이동(창고 0 → 개인 0)을 요청하고, 총량 보존 여부를 호스트 콘솔에 찍는다.
    /// - 숫자패드 0 : 동시 그랩 경합 재현(M5 6차 — 검증 I1·I2). 호스트가 최근접 그랩 가능 자원을
    ///   골라 전 피어에 뿌리고, 각 피어가 수신 프레임에 자기 집게로 그랩을 요청한다.
    /// <b>몬스터·보스 (윗줄 연산자 + .)</b>
    /// - 숫자패드 / : 현재 지역 보스 즉시 소환(M7 2차). 밤·웨이브와 무관하게 1기를 세운다 —
    ///   숫자패드 −(웨이브 토글)와 조합하면 보스 단독 격리가 된다. <b>새벽 보류에는 걸리지 않는다.</b>
    /// - 숫자패드 * : 몬스터 단건 스폰(M5 6차). 요청자 전방 10 m 지상에 기본 변종 1마리 —
    ///   파지·투척·즉사 존 검증을 몬스터 1마리로 통제한다.
    /// - 숫자패드 − : 몬스터 웨이브 스폰 토글(M5 4차 — 밤 노숙 체온 검증용).
    /// - 숫자패드 . : 지역 보스 즉시 처치(M7 2차). 보스전을 치르지 않고 <b>처치 이후</b>를 검증한다 —
    ///   보스 핵 드랍·새벽 보류 해제·HUD 종료·처치 배너.
    /// 클라이언트 입력도 ServerRpc 경유로 호스트가 확정한다. Train(씬 NetworkObject)에 배치한다.
    /// </summary>
    public sealed class QaDebugHotkeys : NetworkBehaviour
    {
        private const float SampleDamage = 30f;
        private const float SelfDamage = 20f;
        private const float SingleMonsterSpawnDistance = 10f;

        [Tooltip("켜면 숫자패드가 행 단위로 묶인다 — [편성·건축] 7 = 연결부 파괴, 8 = 칸 건설, 9 = 부위 데미지, + = 재시작, F2 = 열차·궤도 높이 단계 / [자원·피해] 4 = 자원·식재료 지급, 5 = 피해 실측, 6 = 창고 동시 경합, 0 = 동시 그랩 / [몬스터·보스] / = 지역 보스 소환, * = 몬스터 단건 스폰, − = 웨이브 스폰 토글, . = 지역 보스 즉시 처치. QA 전용이므로 릴리스에서는 끈다.")]
        [SerializeField] private bool _enableQaKeys = true;

        // 로컬 망치가 마지막으로 알린 조준 부위 — 숫자패드 9의 데미지 대상 선택에 쓴다.
        private bool _hasHammerTarget;
        private TrainPartKind _hammerTargetKind;
        private int _hammerTargetIndex;

        // 로컬 설치 프리뷰가 마지막으로 알린 자리 — F4의 거치 무기 설치 지점 (M7 4차).
        // 설치 판정(그리드 내부·비점유)은 서버가 다시 보므로 여기서는 좌표만 기억한다.
        private bool _hasPlaceAim;
        private int _placeCarIndex;
        private int _placeCellX;
        private int _placeCellZ;
        private int _placeRotation;

        private void OnEnable()
        {
            Core.Events.EventBus<HammerTargetLocalEvent>.Subscribe(OnHammerTarget);
            Core.Events.EventBus<StructurePlaceAimLocalEvent>.Subscribe(OnStructurePlaceAim);
        }

        private void OnDisable()
        {
            Core.Events.EventBus<HammerTargetLocalEvent>.Unsubscribe(OnHammerTarget);
            Core.Events.EventBus<StructurePlaceAimLocalEvent>.Unsubscribe(OnStructurePlaceAim);
        }

        private void OnHammerTarget(HammerTargetLocalEvent evt)
        {
            _hasHammerTarget = evt.HasTarget;
            _hammerTargetKind = evt.Kind;
            _hammerTargetIndex = evt.Index;
        }

        private void OnStructurePlaceAim(StructurePlaceAimLocalEvent evt)
        {
            _hasPlaceAim = evt.Aiming;
            _placeCarIndex = evt.CarIndex;
            _placeCellX = evt.CellX;
            _placeCellZ = evt.CellZ;
            _placeRotation = evt.Rotation;
        }

        private void Update()
        {
            // 인게임 씬이 아니면 QA 키를 받지 않는다. 지금 이 컴포넌트는 인게임 씬의 Train에만
            // 얹혀 있어 이 조건은 항상 참이지만, 그 "배치가 곧 게이트"라는 암묵적 전제를 명시로 바꾼다 —
            // 무기 입력이 바로 그 전제에 기대다가 대기실에서 발사되는 사고를 냈다.
            if (!_enableQaKeys || !IsSpawned || !GameplaySceneRoute.IsActiveSceneGameplay())
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            // ── 편성·건축 : 7·8·9 행 + 재시작(+) + F2 ────────────────────────
            if (keyboard.numpad7Key.wasPressedThisFrame)
            {
                RequestBreakCouplingServerRpc();
            }

            if (keyboard.numpad8Key.wasPressedThisFrame)
            {
                RequestBuildCarServerRpc();
            }

            if (keyboard.numpad9Key.wasPressedThisFrame)
            {
                // 망치로 겨눈 부위가 있으면 그것을 때린다 — 폴백은 "뒤에서 첫 부위"라
                // 특정 건축물(예: 앞 칸의 화덕)을 손상시킬 수 없었다 (M5 4차 C7).
                if (_hasHammerTarget)
                {
                    RequestTargetedDamageServerRpc(_hammerTargetKind, _hammerTargetIndex);
                }
                else
                {
                    RequestSampleDamageServerRpc();
                }
            }

            if (keyboard.numpadPlusKey.wasPressedThisFrame)
            {
                RequestRestartServerRpc();
            }

            // 숫자패드가 아니라 F2다 — 숫자패드 12키는 위 세 그룹이 이미 만석으로 쓰고 있다.
            if (keyboard.f2Key.wasPressedThisFrame)
            {
                RequestCycleTrainElevationServerRpc();
            }

            // F4 — 겨눈 자리에 거치 무기 즉시 설치 + 소총탄 지급 (M7 4차).
            // 설치 비용·자원 채집을 건너뛰고 점유·사격·재장전만 반복 검증하기 위한 키다.
            if (keyboard.f4Key.wasPressedThisFrame && _hasPlaceAim)
            {
                StructureKind kind = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed
                    ? StructureKind.Turret
                    : StructureKind.MountedGun;
                RequestBuildMountedWeaponServerRpc(
                    _placeCarIndex, _placeCellX, _placeCellZ, _placeRotation, kind);
            }

            // ── 자원·피해 : 4·5·6 행 + 동시 그랩(0) ──────────────────────────
            if (keyboard.numpad4Key.wasPressedThisFrame)
            {
                RequestGrantResourcesServerRpc();
            }

            if (keyboard.numpad5Key.wasPressedThisFrame)
            {
                RequestSelfDamageServerRpc();
            }

            if (keyboard.numpad6Key.wasPressedThisFrame)
            {
                RequestStorageContentionServerRpc();
            }

            if (keyboard.numpad0Key.wasPressedThisFrame)
            {
                RequestSimultaneousGrabServerRpc();
            }

            // ── 몬스터·보스 : 윗줄 연산자(/ * −) + 보스 처치(.) ──────────────
            if (keyboard.numpadDivideKey.wasPressedThisFrame)
            {
                RequestSpawnBossServerRpc();
            }

            if (keyboard.numpadMultiplyKey.wasPressedThisFrame)
            {
                RequestSpawnSingleMonsterServerRpc();
            }

            if (keyboard.numpadMinusKey.wasPressedThisFrame)
            {
                RequestToggleMonsterSpawnServerRpc();
            }

            if (keyboard.numpadPeriodKey.wasPressedThisFrame)
            {
                RequestKillBossServerRpc();
            }
        }

        /// <summary>
        /// 지역 보스 즉시 처치 (M7 2차 검증) — 보스전을 끝까지 치르지 않고 <b>처치 이후</b>를 본다:
        /// 보스 핵 드랍·새벽 보류 해제·HUD 종료·처치 배너. 정상 사망 경로를 그대로 타므로
        /// 결과가 실제 처치와 같다.
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestKillBossServerRpc(RpcParams rpcParams = default)
        {
            if (!ServiceLocator.TryGet(out Cycle.INightHoldGate gate)
                || !(gate is Monsters.BossSpawner spawner))
            {
                GameLog.Info(LogCategory.Qa, "보스 처치 무효: 보스 스포너가 없다");
                return;
            }

            spawner.ServerKillBossForQa(rpcParams.Receive.SenderClientId);
        }

        /// <summary>
        /// 지역 보스 즉시 소환 (M7 2차) — 마지막 밤을 기다리지 않고 현재 지역 보스를 1기 세운다.
        /// 소환된 보스는 <b>새벽 보류에 등록되지 않으므로</b> 낮에도 시간이 정상 진행한다 —
        /// 패턴·페이즈·드랍만 격리해서 볼 수 있다 (밤 지속 규칙 자체는 마지막 밤에서 검증한다).
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestSpawnBossServerRpc()
        {
            if (!ServiceLocator.TryGet(out Cycle.INightHoldGate gate)
                || !(gate is Monsters.BossSpawner spawner))
            {
                GameLog.Info(LogCategory.Qa, "보스 소환 무효: 보스 스포너가 없다");
                return;
            }

            int dayNumber = ServiceLocator.TryGet(out Cycle.IDayCycleService cycle) ? cycle.DayNumber : 1;
            spawner.ServerSpawnBossForQa(dayNumber);
        }

        /// <summary>
        /// 몬스터 단건 스폰 (M5 6차) — 요청자 전방 10 m 지상에 기본 변종 1마리를 스폰한다.
        /// 웨이브를 기다리거나 여러 마리에 시달리지 않고 파지·투척·즉사 존을 1마리로 검증한다.
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestSpawnSingleMonsterServerRpc(RpcParams rpcParams = default)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null ||
                !manager.ConnectedClients.TryGetValue(rpcParams.Receive.SenderClientId, out NetworkClient client) ||
                client.PlayerObject == null)
            {
                return;
            }

            if (!ServiceLocator.TryGet(out Monsters.IWaveSpawnToggle toggle)
                || !(toggle is Monsters.MonsterWaveSpawner spawner))
            {
                GameLog.Info(LogCategory.Qa, "단건 스폰 무효: 웨이브 스포너가 없다");
                return;
            }

            // 요청자가 보는 방향의 수평 전방 — 어디를 보고 있든 눈앞 지상에 나온다.
            Transform player = client.PlayerObject.transform;
            Vector3 forward = player.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;

            spawner.ServerSpawnSingleForQa(player.position + forward * SingleMonsterSpawnDistance);
        }

        /// <summary>
        /// 피해 실측 (M5 6차 — 검증 H7, 3~5차 연속 이월 해소). 요청자에게 고정 피해를
        /// <b>물리 경로</b>(<see cref="Player.PlayerHealth.ApplyDamage"/>)로 넣는다 — 장비 감산이
        /// 적용되므로 착용 전후를 같은 기준으로 실측할 수 있다. 결과는 호스트 콘솔에 찍힌다.
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestSelfDamageServerRpc(RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null ||
                !manager.ConnectedClients.TryGetValue(senderId, out NetworkClient client) ||
                client.PlayerObject == null)
            {
                return;
            }

            var health = client.PlayerObject.GetComponent<Player.PlayerHealth>();
            if (health == null || !health.IsAlive)
            {
                GameLog.Info(LogCategory.Qa, $"피해 실측 무효: client={senderId} — 플레이어가 살아 있지 않다");
                return;
            }

            float before = health.Health;
            health.ApplyDamage(SelfDamage, senderId);
            GameLog.Info(LogCategory.Qa, $"피해 실측: client={senderId} 기준 {SelfDamage} → " +
                                   $"체력 {before:F1} → {health.Health:F1} (적용 {before - health.Health:F1})");
        }

        /// <summary>
        /// 동시 그랩 경합 재현 (M5 6차 — 검증 I1·I2). 호스트가 요청자 최근접 그랩 가능 자원 노드를
        /// 골라 전 피어에 브로드캐스트하고, 각 피어가 수신 프레임에 자기 집게로 그랩 요청을 발행한다 —
        /// 서버 도착이 붙어 "한쪽 승인 + 한쪽 다른 사람이 잡고 있다"가 재현된다.
        /// 창고 경합의 교훈대로 전제(대상 존재)를 시작 단계에서 검사하고, 승인·거부는
        /// 집게의 호스트 콘솔 로그로 확인한다 (무효 통과 방지).
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestSimultaneousGrabServerRpc(RpcParams rpcParams = default)
        {
            NetworkObject target = FindNearestGrabbableResource(rpcParams.Receive.SenderClientId);
            if (target == null)
            {
                GameLog.Info(LogCategory.Qa, "동시 그랩 무효: 그랩 가능한 자원 노드가 없다");
                return;
            }

            GameLog.Info(LogCategory.Qa, $"동시 그랩 트리거: 대상={target.name} — 전 피어가 같은 프레임에 그랩을 요청한다");
            TriggerSimultaneousGrabRpc(target);
        }

        private static NetworkObject FindNearestGrabbableResource(ulong requesterClientId)
        {
            NetworkManager manager = NetworkManager.Singleton;
            Vector3 origin = Vector3.zero;
            if (manager != null &&
                manager.ConnectedClients.TryGetValue(requesterClientId, out NetworkClient client) &&
                client.PlayerObject != null)
            {
                origin = client.PlayerObject.transform.position;
            }

            World.ResourceNode best = null;
            float bestSqr = float.MaxValue;
            foreach (World.ResourceNode node in FindObjectsByType<World.ResourceNode>(FindObjectsSortMode.None))
            {
                if (!node.IsAvailableForGrab)
                {
                    continue;
                }

                float sqr = (node.transform.position - origin).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = node;
                }
            }

            return best != null ? best.NetworkObject : null;
        }

        [Rpc(SendTo.Everyone)]
        private void TriggerSimultaneousGrabRpc(NetworkObjectReference targetRef)
        {
            if (!targetRef.TryGet(out NetworkObject target))
            {
                return;
            }

            NetworkObject player = NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null
                ? NetworkManager.Singleton.LocalClient.PlayerObject
                : null;
            var harpoon = player != null ? player.GetComponent<Harpoon.HarpoonController>() : null;
            harpoon?.QaRequestGrab(target);
        }

        /// <summary>
        /// 열차·궤도 높이 단계 순환 (열차 높이 스펙) — 편성·손잡이·설비·궤도 타일과 갑판 기준선을
        /// 한 오프셋으로 함께 내린다. 호스트가 단계를 확정하고 복제하므로 전 피어가 같은 높이를 보고,
        /// 후발 접속도 접속 시점의 단계를 그대로 받는다.
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestCycleTrainElevationServerRpc()
        {
            if (!ServiceLocator.TryGet(out ITrainElevation elevation))
            {
                GameLog.Info(LogCategory.Qa, "높이 단계 무효: 높이 컨트롤러가 없다 — 이 씬에는 배선되지 않았다");
                return;
            }

            elevation.ServerCycleStep();
        }

        /// <summary>
        /// 공유 창고 동시 경합 재현 (M5 5차 — 검증 G2). 호스트가 전 피어에 트리거를 뿌리고
        /// 각 피어가 수신 프레임에 같은 이동을 요청한다 — 서버 도착이 붙어 경합이 재현된다.
        /// 총량 보존 여부는 호스트 콘솔에 (요청 전 / 확정 후) 두 줄로 찍힌다.
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestStorageContentionServerRpc()
        {
            if (ServiceLocator.TryGet(out ITrainStorage storage) && storage is TrainStorage concrete)
            {
                concrete.ServerTriggerContentionTest();
            }
        }

        /// <summary>
        /// 몬스터 웨이브 스폰 토글 (M5 4차 — 밤 노숙 체온 검증의 선결 수단). 끄면 진행 중인 웨이브를
        /// 회수하고 다음 밤 웨이브도 시작하지 않는다. 상태는 스포너 콘솔 로그로 확인한다.
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestToggleMonsterSpawnServerRpc()
        {
            if (ServiceLocator.TryGet(out Monsters.IWaveSpawnToggle toggle))
            {
                toggle.ServerSetSpawnEnabled(!toggle.SpawnEnabled);
            }
        }

        /// <summary>
        /// 게임 재시작 — 호스트가 인게임 씬을 단일 모드로 재로드해 모든 네트워크 상태를 초기화한다.
        /// 인게임 씬은 <see cref="GameplaySceneRoute.Name"/> 하나뿐이라 재시작이 어느 씬을 올릴지
        /// 물을 필요가 없다.
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestRestartServerRpc()
        {
            if (ServiceLocator.TryGet(out INetworkSessionService session))
            {
                session.LoadGameplayScene(GameplaySceneRoute.Name);
            }
        }

        /// <summary>지금 표적 가능한 연결부(살아 있는 것 중 가장 후미 — 순차 파괴 규칙)를 찾아 파괴한다.</summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestBreakCouplingServerRpc()
        {
            if (!ServiceLocator.TryGet(out ITrainState train) || !ServiceLocator.TryGet(out ITrainDamageSink sink))
            {
                return;
            }

            for (int i = train.CouplingCount - 1; i >= 0; i--)
            {
                if (train.IsCouplingTargetable(i))
                {
                    sink.ApplyCouplingDamage(i, float.MaxValue);
                    return;
                }
            }
        }

        /// <summary>칸 1칸을 무료 건설한다(빈 슬롯 재건 우선) — 비용 지불 경로는 망치 칸 건설이 따로 검증한다.</summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestBuildCarServerRpc()
        {
            if (ServiceLocator.TryGet(out ITrainExpansion expansion))
            {
                expansion.ServerTryBuildCar();
            }
        }

        /// <summary>
        /// 겨눈 자리에 거치 무기 1개를 무료 설치하고 요청자에게 소총탄 60을 지급한다 (M7 4차).
        /// 설치 판정은 일반 경로와 같은 함수가 다시 본다 — 자리 규칙까지 건너뛰면 QA가 성립하지 않는다.
        /// 탄은 설치 성공 여부와 무관하게 준다: 이미 세워 둔 무기를 채우는 데도 같은 키를 쓴다.
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestBuildMountedWeaponServerRpc(
            int carIndex, int cellX, int cellZ, int rotation, StructureKind kind,
            RpcParams rpcParams = default)
        {
            if (ServiceLocator.TryGet(out ITrainExpansion expansion))
            {
                expansion.ServerTryBuildStructure(carIndex, cellX, cellZ, rotation, kind);
            }

            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null
                && manager.ConnectedClients.TryGetValue(rpcParams.Receive.SenderClientId, out NetworkClient client)
                && client.PlayerObject != null)
            {
                IResourceInventory inventory = client.PlayerObject.GetComponent<IResourceInventory>();
                inventory?.ServerTryAdd(ResourceType.RifleAmmo, 60);
            }
        }

        /// <summary>요청한 플레이어에게 자원 10개를 지급한다.</summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestGrantResourcesServerRpc(RpcParams rpcParams = default)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null ||
                !manager.ConnectedClients.TryGetValue(rpcParams.Receive.SenderClientId, out NetworkClient client) ||
                client.PlayerObject == null)
            {
                return;
            }

            IResourceInventory inventory = client.PlayerObject.GetComponent<IResourceInventory>();
            if (inventory != null)
            {
                // 검증 편의 — 건자재 2종 + 제작 재료 2종 + 식재료를 함께 지급해
                // 건설·제작·요리 경로를 모두 시험할 수 있게 한다 (식재료 4 = 스튜 1회 또는 구운 식사 2회).
                inventory.ServerTryAdd(ResourceType.Wood, 4);
                inventory.ServerTryAdd(ResourceType.Stone, 2);
                inventory.ServerTryAdd(ResourceType.Scrap, 2);
                inventory.ServerTryAdd(ResourceType.Niter, 2);
                inventory.ServerTryAdd(ResourceType.RawFood, 4);
                // M7 1차 — 벼·소금 지급으로 밥(벼 2)·보존식(식재료 2 + 소금 1) 요리 루프를 채집 없이 검증한다.
                inventory.ServerTryAdd(ResourceType.Rice, 4);
                inventory.ServerTryAdd(ResourceType.Salt, 2);
                // M7 3차 — 북극은 채집 자체가 희소(스폰 간격 ×2.5)해 방한 세트·정수 사슬 검증이 오래 걸린다.
                // 얼음 6(정수 2회) + 희귀 금속 5(세트 4부위 정확히 한 벌)를 지급해 채집 없이 시험할 수 있게 한다.
                inventory.ServerTryAdd(ResourceType.Ice, 6);
                inventory.ServerTryAdd(ResourceType.RareMetal, 5);
            }
        }

        /// <summary>
        /// 망치로 겨눈 부위 하나에 샘플 데미지를 넣는다 (M5 4차) — 겨눈 것만 맞으므로
        /// 특정 건축물(화덕 등)을 골라 손상·파괴시킬 수 있다. 부위 식별은 망치 RPC와 같은 (종류, 인덱스) 규약.
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestTargetedDamageServerRpc(TrainPartKind kind, int index)
        {
            if (!ServiceLocator.TryGet(out ITrainDamageSink sink))
            {
                return;
            }

            switch (kind)
            {
                case TrainPartKind.Coupling:
                    sink.ApplyCouplingDamage(index, SampleDamage);
                    break;

                case TrainPartKind.Car:
                    sink.ApplyCarDamage(index, SampleDamage);
                    break;

                case TrainPartKind.Structure:
                    sink.ApplyStructureDamage(index, SampleDamage);
                    break;
            }
        }

        /// <summary>수리 대상을 만들기 위해 표적 연결부·최후미 칸·살아 있는 건축물에 샘플 데미지를 넣는다 (겨눈 부위가 없을 때의 폴백).</summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestSampleDamageServerRpc()
        {
            if (!ServiceLocator.TryGet(out ITrainState train) || !ServiceLocator.TryGet(out ITrainDamageSink sink))
            {
                return;
            }

            for (int i = train.CouplingCount - 1; i >= 0; i--)
            {
                if (train.IsCouplingTargetable(i))
                {
                    sink.ApplyCouplingDamage(i, SampleDamage);
                    break;
                }
            }

            for (int i = train.CarCount - 1; i > 0; i--)
            {
                if (train.TryGetCar(i, out CarState car) && TrainStateLogic.IsCarPresent(car))
                {
                    sink.ApplyCarDamage(i, SampleDamage);
                    break;
                }
            }

            // 건축물 폴백 — 그리드 목록의 마지막 살아 있는 항목 (건축 개편 1차 — Id로 지목한다).
            for (int i = train.StructureCount - 1; i >= 0; i--)
            {
                if (train.TryGetStructureAt(i, out StructureEntry entry) && StructureGridLogic.IsAlive(entry))
                {
                    sink.ApplyStructureDamage(entry.Id, SampleDamage);
                    break;
                }
            }
        }
    }
}
