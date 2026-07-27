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

        public GrabKind Kind => GrabKind.Anchor;

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

        public void CompleteGrab()
        {
        }

        public void BeginPredictedTow()
        {
        }

        public void CancelPredictedTow()
        {
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            // 칸의 이탈 오프셋만큼 기준 슬롯 위치에서 뒤(-Z)로 민다. 열차 원점 고정(미회전)이라 로컬=월드.
            // CarView가 읽는 것과 동일한 복제 오프셋이므로 호스트·클라 모두에서 위치가 일치한다.
            float offset = 0f;
            CarState car = default;
            bool hasCar = false;
            if (ServiceLocator.TryGet(out ITrainState train))
            {
                offset = train.GetEjectOffset(_carIndex);
                hasCar = train.TryGetCar(_carIndex, out car);
            }

            transform.position = _baseSlotPosition + Vector3.back * offset;

            // 표현은 칸과 운명을 같이한다 — 파괴된 칸은 즉시, 이탈 칸은 소실 표현 거리에서 함께 사라진다.
            // NetworkObject 자체는 despawn하지 않는다(스펙 §5 — 재결합·다음 판 대비 씬 유지).
            bool visible = !hasCar
                || TrainStateLogic.IsCarPresent(car)
                || (car.Health > 0f && offset < _ejectHideMeters);
            SetPresentationVisible(visible);
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
