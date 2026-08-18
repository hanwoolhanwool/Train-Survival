using Game.Gameplay.Harpoon;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 플레이어 동료의 집게 그랩 관심사 (기획서 §3.1 — 2단계부터 잡히는 대상).
    /// <see cref="MonsterGrabTarget"/>과 같은 <see cref="IGrabbable"/> 계약을 따르므로
    /// <b>집게 쪽 코드는 한 줄도 바뀌지 않는다</b> — 도착 결과를 대상이 정하기 때문이다 (OCP).
    ///
    /// <para><b>다른 대상과 결정적으로 다른 점은 위치 권위다.</b> 자원·몬스터는 서버가 위치를 대입하지만
    /// 플레이어의 위치는 소유자 권위(<c>OwnerNetworkTransform</c>)라 서버가 직접 옮길 수 없다.
    /// 그래서 서버는 <b>"누가 나를 끄는가"와 릴 속도만 복제</b>하고, 끌려가는 계산은 소유자가
    /// <see cref="IExternalTow"/>로 로컬에서 수행한다 — 새 위치 스트림이 없고, 끌리는 쪽 화면이
    /// 왕복 지연으로 튀지 않는다. 서버는 복제돼 올라온 실제 위치로 도착만 판정한다.</para>
    ///
    /// <para>다운 조건은 두지 않는다 — <b>멀쩡한 동료도 잡힌다</b> (계획 확정 ⑤).
    /// 안전장치는 "끌려오는 방향이 언제나 그래버 쪽"이라는 점 하나다: 밀어낼 수단이 없어
    /// 열차 밖으로 내보낼 수 없고, 도착 반경에서 멈춘다. 뿌리치기는 필요해지면 그때 얹는다.</para>
    ///
    /// 예측 고정(Begin/CancelPredictedTow)은 no-op다 — 대상의 위치를 쏜 쪽이 흔들 수 없다.
    /// </summary>
    [RequireComponent(typeof(NetworkPlayerController))]
    [RequireComponent(typeof(PlayerHealth))]
    public sealed class PlayerGrabTarget : NetworkBehaviour, IGrabbable, IExternalTow
    {
        /// <summary>동료를 끌 수 있는 최소 집게 등급 (기획서 §3.1 — 2단계의 첫 체감 보상).</summary>
        private const int RequiredTier = 2;

        // 그래버 (서버 write) — 소유자는 이 참조로 앵커를 스스로 계산한다. 전 피어가 읽을 수 있어
        // "끌리는 중"이라는 사실 자체는 모두가 같은 값으로 판정한다.
        private readonly NetworkVariable<NetworkObjectReference> _grabber =
            new NetworkVariable<NetworkObjectReference>();

        // 그래버 등급의 릴 속도 (서버 write) — 등급이 사거리뿐 아니라 끌어오는 속도도 바꾼다.
        private readonly NetworkVariable<float> _reelSpeed = new NetworkVariable<float>();

        private NetworkPlayerController _controller;
        private PlayerHealth _health;

        // 서버 전용 점유 표시 — 복제 값(_grabber)과 별개로 둔다. 디스폰 처리 중에는 IsSpawned·복제 값이
        // 이미 정리됐을 수 있어, 거기에 기대면 "끌리던 채로 사라진" 경우의 뒷정리를 건너뛴다.
        private bool _claimed;

        public GrabKind Kind => GrabKind.Reel;

        /// <summary>동료는 2단계부터 (기획서 §3.1). 1단계로는 로프가 미끄러진다.</summary>
        public int RequiredHarpoonTier => RequiredTier;

        /// <summary>살아 있고(이탈 사망 대기 포함) 아무도 끌고 있지 않으면 잡힌다 — 다운 조건 없음.</summary>
        public bool IsAvailableForGrab => IsSpawned && !_claimed && _health != null && _health.IsAlive;

        /// <summary>서버 기준 진실 (<see cref="IGrabbable"/> 계약).</summary>
        public bool IsClaimed => _claimed;

        // ── IExternalTow — 소유자 구동 견인 ────────────────────────────────

        /// <summary>
        /// 끌리는 중인가 — <b>복제 값 기준</b>이라 소유자·원격 피어도 같은 판정을 얻는다
        /// (서버 전용인 <see cref="IsClaimed"/>와 출처가 다르다: 견인 구동은 소유자가 한다).
        /// </summary>
        public bool IsTowed => IsSpawned && _grabber.Value.TryGet(out NetworkObject _);

        public bool TryGetTowStep(out Vector3 anchor, out float speed)
        {
            anchor = default;
            speed = 0f;

            if (!IsSpawned || !_grabber.Value.TryGet(out NetworkObject grabber) || grabber == null)
            {
                return false;
            }

            // 서버 견인(HarpoonController.ServerUpdateTow)과 같은 앵커·같은 속도를 쓴다 —
            // 두 계산이 갈리면 소유자는 도착했는데 서버는 아니라고 보는 구간이 생긴다.
            anchor = grabber.transform.position + Vector3.up * HarpoonController.TowAnchorHeight;
            speed = Mathf.Max(0f, _reelSpeed.Value);
            return true;
        }

        private void Awake()
        {
            _controller = GetComponent<NetworkPlayerController>();
            _health = GetComponent<PlayerHealth>();
        }

        private void Update()
        {
            // 서버 전용 — 끌려오던 동료가 죽으면 그랩을 끊는다. 대상이 디스폰되지 않는 종류라
            // 집게 쪽 디스폰 감시에 걸리지 않으므로 여기서 알린다. 안 끊으면 부활 후에도
            // 구속 상태(Grabbed)가 남아 조작이 막힌다.
            if (!IsServer || !_claimed || _health == null || _health.IsAlive)
            {
                return;
            }

            ServerNotifyGrabberRelease();
            ReleaseGrab();
        }

        // ── IGrabbable — 서버 확정 ────────────────────────────────────────

        public bool TryClaimGrab(ulong grabberClientId)
        {
            if (!IsServer || !IsAvailableForGrab || grabberClientId == OwnerClientId)
            {
                // 자기 자신은 못 잡는다 — 자기 훅으로 자기를 끌면 위치가 발산한다.
                return false;
            }

            NetworkObject grabber = ResolveGrabber(grabberClientId);
            if (grabber == null)
            {
                return false;
            }

            _claimed = true;
            _grabber.Value = new NetworkObjectReference(grabber);
            _reelSpeed.Value = grabber.TryGetComponent(out HarpoonController harpoon) ? harpoon.ReelSpeed : 0f;

            // 호스트 개입 상태 머신(§4.2)의 Grabbed로 올린다 — 소유자 이동 입력이 멈추고
            // 그 자리를 이 컴포넌트의 견인이 대신한다. 시선은 남는다.
            _controller?.ServerSetMovementState(PlayerMovementState.Grabbed);
            return true;
        }

        /// <summary>
        /// 서버 전용 — 계약상 견인 위치 갱신이지만 <b>여기서는 아무것도 하지 않는다</b>.
        /// 플레이어의 위치는 소유자 권위라 서버가 대입할 수 없고, 대신 소유자가
        /// <see cref="TryGetTowStep"/>으로 같은 앵커를 향해 스스로 움직인다.
        /// "도착했는가"는 복제돼 올라온 실제 위치로 서버가 판정하므로 이 무시가 판정을 흐리지 않는다.
        /// </summary>
        public void UpdateTowPosition(Vector3 position)
        {
        }

        public void ReleaseGrab()
        {
            if (!IsServer)
            {
                return;
            }

            _claimed = false;
            _grabber.Value = default;
            _reelSpeed.Value = 0f;

            // 구속 해제 — 현재 Grabbed의 사용처가 견인뿐이라 Normal로 되돌린다.
            // 다른 구속(몬스터 붙잡기 등)이 생기면 그때 상태 소유권을 정리한다.
            _controller?.ServerSetMovementState(PlayerMovementState.Normal);
        }

        /// <summary>
        /// 도착 결과 — <b>그 자리에 선다</b> (확정 ⑤). 수납도, 파지 유지도 아니므로 확정 불성립을
        /// 돌려주면 집게가 기존 "해제 + 강제 해제 통지" 경로로 되돌아간다. 들어 옮기기는 비범위다.
        /// </summary>
        public GrabCompletionResult TryCompleteGrab(in GrabCompletion completion)
        {
            return GrabCompletionResult.Rejected;
        }

        public void BeginPredictedTow()
        {
        }

        public void CancelPredictedTow()
        {
        }

        public override void OnNetworkDespawn()
        {
            // 서버 로컬 플래그로 판정한다 — 이 시점에는 IsSpawned·복제 값이 이미 정리됐을 수 있다.
            if (IsServer && _claimed)
            {
                ServerNotifyGrabberRelease();
                ReleaseGrab();
            }
        }

        /// <summary>서버 전용 — 그래버 쪽 로프·훅도 함께 정리한다 (대상 사정에 의한 해제).</summary>
        private void ServerNotifyGrabberRelease()
        {
            if (_grabber.Value.TryGet(out NetworkObject grabber) && grabber != null
                && grabber.TryGetComponent(out HarpoonController harpoon))
            {
                harpoon.ServerForceReleaseTow();
            }
        }

        private NetworkObject ResolveGrabber(ulong grabberClientId)
        {
            if (NetworkManager == null
                || !NetworkManager.ConnectedClients.TryGetValue(grabberClientId, out NetworkClient client))
            {
                return null;
            }

            return client.PlayerObject;
        }
    }
}
