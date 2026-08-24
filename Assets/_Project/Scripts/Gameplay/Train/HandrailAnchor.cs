using Game.Core.Logging;
using Game.Core.Services;
using Game.Gameplay.Harpoon;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 각 칸의 손잡이 그랩 앵커 (손잡이-이탈저항 스펙 §5). 집게로 잡으면 릴 감기지 않고 그 칸의 저항 인원을 +1 한다.
    /// 씬에 정적 배치된 InScenePlaced NetworkObject다 — 처음부터 상시 존재하고, 그 칸이 이탈해 뒤로 밀려나는
    /// 동안에만 잡을 수 있다(소실·미이탈이면 불가). 위치는 복제된 이탈 오프셋(<see cref="ITrainState.GetEjectOffset"/>)을
    /// 읽어 기준 슬롯 위치에서 뒤로 밀어 계산한다 — <see cref="CarView"/>와 동일 소스라 전 피어 위치가 일치한다.
    /// NetworkObject는 집게가 대상을 식별(NetworkObjectReference)하는 데만 쓰인다(위치는 복제하지 않는다).
    /// </summary>
    public sealed class HandrailAnchor : NetworkBehaviour, IGrabbable
    {
        [Tooltip("이 손잡이가 속한 칸의 편성 인덱스(0 = 기관차). TrainState의 칸 배열과 대응.")]
        [SerializeField, Min(0)] private int _carIndex;

        [Tooltip("이탈 칸이 뒤로 이만큼(m) 멀어지면 표현을 끈다 — CarView의 소실 표현과 같은 거리로 맞춘다.")]
        [SerializeField, Min(5f)] private float _ejectHideMeters = 50f;

        // 이탈 오프셋 0(붙어 있을 때)일 때의 손잡이 위치 — 씬 배치 위치를 그대로 기준으로 캐시한다.
        private Vector3 _baseSlotPosition;
        private bool _claimed;

        private Renderer[] _renderers;
        private Collider[] _colliders;
        private bool _presentationVisible = true;

        // 편성이 선 뒤에도 스폰이 안 되는 앵커를 잡아내는 일회성 진단 (좌/우 한쪽만 잡히던 증상의 확인용).
        // 표현은 스폰과 무관하게 돌지만 잡기는 신원이 필요하므로, 이 상태는 그 피어에서 "못 잡는 손잡이"가 된다.
        private const float NotSpawnedGraceSeconds = 5f;
        private float _notSpawnedSeconds;
        private bool _reportedNotSpawned;

        public GrabKind Kind => GrabKind.Anchor;

        /// <summary>
        /// 손잡이는 등급 잠금이 없다 — 이탈 저항은 1단계 집게로도 성립해야 한다.
        /// <b>잡을 수는 있고, 잡은 동안 무기를 못 바꿀 뿐이다</b> (1단계 전환 게이트,
        /// <see cref="HarpoonSwitchRules"/> — 집게 단계별 파지 계획 §3.2).
        /// </summary>
        public int RequiredHarpoonTier => 1;

        /// <summary>이탈 중인 칸의 손잡이만 잡을 수 있다(스펙: 이탈 중이고 소실 전인 칸만). 서버 기준 진실.</summary>
        public bool IsAvailableForGrab =>
            IsSpawned && !_claimed
            && ServiceLocator.TryGet(out ITrainState train) && train.IsCarGrabbable(_carIndex);

        public bool IsClaimed => _claimed;

        private void Awake()
        {
            // 씬에 저작된 배치 위치가 곧 슬롯(오프셋 0) 기준이다. 이후 Update가 이 값에서 오프셋만큼 뒤로 민다.
            _baseSlotPosition = transform.position;

            _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            _colliders = GetComponentsInChildren<Collider>(includeInactive: true);
        }

        public override void OnNetworkSpawn()
        {
            _claimed = false;
            _notSpawnedSeconds = 0f;
        }

        public bool TryClaimGrab(ulong grabberClientId)
        {
            if (!IsServer || !IsAvailableForGrab)
            {
                return false;
            }

            _claimed = true;

            if (ServiceLocator.TryGet(out ITrainGrabResistance resist))
            {
                resist.AddGrabber(_carIndex);
            }

            return true;
        }

        public void ReleaseGrab()
        {
            if (!IsServer || !_claimed)
            {
                return;
            }

            _claimed = false;

            if (ServiceLocator.TryGet(out ITrainGrabResistance resist))
            {
                resist.RemoveGrabber(_carIndex);
            }
        }

        // 앵커는 릴 감기·회수·예측 고정을 쓰지 않는다 — 붙잡기 전용.
        public void UpdateTowPosition(Vector3 position)
        {
        }

        /// <summary>앵커에는 획득 개념이 없다 — 애초에 도착 판정을 타지 않지만(집게가 Anchor에서 앞서 return)
        /// 계약상 "확정되지 않았다"를 돌려준다.</summary>
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

        private void Update()
        {
            // 칸의 이탈 오프셋만큼 기준 슬롯 위치에서 뒤(-Z)로 민다. 열차 원점 고정(미회전)이라 로컬=월드.
            // CarView가 읽는 것과 동일한 복제 오프셋이므로 호스트·클라 모두에서 위치가 일치한다.
            //
            // 표현(위치·가시성)은 <b>스폰 여부를 게이트로 두지 않는다</b> — 같은 오프셋을 소비하는
            // <see cref="CarView"/>가 순수 MonoBehaviour로 게이트 없이 도는 것과 짝을 맞춘 것이다.
            // 게이트를 두면 어느 한 앵커의 스폰이 늦거나 실패한 피어에서 <b>그 앵커만</b> 슬롯에 얼어붙어,
            // 좌/우 쌍 중 한쪽만 이탈 칸을 따라가는 상태가 된다 — 스펙 §5의 "전 피어 위치가 일치"가 깨진다.
            // 네트워크 신원이 필요한 것은 잡기 쪽(IsAvailableForGrab·TryClaimGrab)뿐이므로 거기서만 본다.
            float offset = 0f;
            CarState car = default;
            bool hasCar = false;
            bool hasTrain = ServiceLocator.TryGet(out ITrainState train);
            if (hasTrain)
            {
                offset = train.GetEjectOffset(_carIndex);
                hasCar = train.TryGetCar(_carIndex, out car);
            }

            transform.position = _baseSlotPosition + Vector3.back * offset;

            // 잡을 수 없는 상태가 되면(재결합으로 편성 복귀·소실) 호스트가 잡기를 끊는다 —
            // 앵커는 despawn하지 않으므로 집게 쪽 스폰 검사로는 잡히지 않는다. 로프는 집게가 점유 해제를 보고 함께 끊는다.
            if (IsSpawned)
            {
                if (IsServer && _claimed && hasTrain && !train.IsCarGrabbable(_carIndex))
                {
                    ReleaseGrab();
                }
            }
            else
            {
                ReportIfNeverSpawned(hasTrain);
            }

            // 표현은 칸과 운명을 같이한다 — 파괴된 칸은 즉시, 이탈 칸은 소실 표현 거리에서 함께 사라지고,
            // 증설 전 예비 슬롯(편성 밖 인덱스)의 손잡이는 칸이 생길 때까지 숨긴다.
            // NetworkObject 자체는 despawn하지 않는다(스펙 §5 — 재결합·다음 판 대비 씬 유지).
            bool visible = !hasTrain
                || (hasCar && (TrainStateLogic.IsCarPresent(car)
                    || (car.Health > 0f && offset < _ejectHideMeters)));
            SetPresentationVisible(visible);
        }

        /// <summary>
        /// 편성이 이미 선 피어에서 앵커가 <see cref="NetworkObject"/>로 스폰되지 않은 채 남아 있으면
        /// 한 번만 보고한다 — 그 피어에서는 <b>보이는데 잡히지 않는</b> 손잡이가 되기 때문이다
        /// (씬 인스턴스의 GlobalObjectIdHash 불일치 등 소프트 싱크 실패의 증상).
        /// <see cref="GameLog.Error"/>인 이유: 릴리스 빌드에서도 남아야 원인을 확정할 수 있다.
        /// </summary>
        private void ReportIfNeverSpawned(bool hasTrain)
        {
            if (!hasTrain || _reportedNotSpawned)
            {
                return;
            }

            // 편성 등록과 NGO 스폰 사이의 몇 프레임을 오탐하지 않도록 유예를 둔다.
            _notSpawnedSeconds += Time.deltaTime;
            if (_notSpawnedSeconds < NotSpawnedGraceSeconds)
            {
                return;
            }

            _reportedNotSpawned = true;
            GameLog.Error(LogCategory.Train,
                $"손잡이 앵커 '{name}'(칸 {_carIndex})가 편성이 선 뒤에도 스폰되지 않았다 — " +
                "이 피어에서는 표현만 따라가고 집게로 잡을 수 없다.", this);
        }

        private void SetPresentationVisible(bool visible)
        {
            if (_presentationVisible == visible)
            {
                return;
            }

            _presentationVisible = visible;
            for (int i = 0; i < _renderers.Length; i++)
            {
                _renderers[i].enabled = visible;
            }

            for (int i = 0; i < _colliders.Length; i++)
            {
                _colliders[i].enabled = visible;
            }
        }
    }
}
