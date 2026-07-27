using Game.Core.Pooling;
using Game.Core.Services;
using Game.Gameplay.Harpoon;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 이탈 칸의 손잡이 그랩 앵커 (손잡이-이탈저항 스펙 §5). 집게로 잡으면 릴 감기지 않고 그 칸의 저항 인원을 +1 한다.
    /// 호스트가 칸의 이탈 오프셋을 따라 위치를 계산·복제하고, 클라이언트는 복제 위치로 표시한다(집게 명중 판정용 콜라이더).
    /// 칸이 이탈할 때 <see cref="TrainState"/>가 앞·뒤 끝에 하나씩 스폰하고, 소실 시 despawn한다. NetworkObject 프리팹.
    /// </summary>
    public sealed class HandrailAnchor : NetworkBehaviour, IGrabbable, IPoolable
    {
        private const ulong NoGrabber = ulong.MaxValue;

        [SerializeField, Min(1f)] private float _positionLerpRate = 25f;

        private readonly NetworkVariable<Vector3> _syncedPosition = new NetworkVariable<Vector3>();
        private readonly NetworkVariable<int> _carIndex = new NetworkVariable<int>(-1);
        private readonly NetworkVariable<ulong> _grabberClientId = new NetworkVariable<ulong>(NoGrabber);

        private Vector3 _endLocalPosition;
        private int _pendingCarIndex = -1;
        private Vector3 _pendingEndLocalPosition;
        private bool _hasPending;
        private bool _claimed;

        public GrabKind Kind => GrabKind.Anchor;

        /// <summary>이탈 중인 칸의 손잡이만 잡을 수 있다(스펙: 이탈 중인 칸만). 소실·재결합·미이탈이면 불가.</summary>
        public bool IsAvailableForGrab => IsSpawned && !_claimed && IsCarEjecting();

        public bool IsClaimed => _claimed;

        /// <summary>서버 전용 — 스폰 직전 (칸 인덱스, 슬롯 기준 칸 끝 위치)를 예약한다. OnNetworkSpawn에서 반영된다.</summary>
        public void ServerSetup(int carIndex, Vector3 endLocalPosition)
        {
            _pendingCarIndex = carIndex;
            _pendingEndLocalPosition = endLocalPosition;
            _hasPending = true;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer && _hasPending)
            {
                _carIndex.Value = _pendingCarIndex;
                _endLocalPosition = _pendingEndLocalPosition;
                _grabberClientId.Value = NoGrabber;
                _hasPending = false;
                UpdateServerPosition();
            }

            _claimed = false;

            // 풀에서 꺼낸 앵커를 열차 계층 아래로 정리한다(부모 없는 DontDestroyOnLoad 상주 대신).
            // 위치는 월드 좌표로 직접 구동하므로 부모는 계층 정리 용도일 뿐이다.
            ReparentUnderTrain();
        }

        private void ReparentUnderTrain()
        {
            if (ServiceLocator.TryGet(out ITrainState train) && train is Component trainComponent)
            {
                transform.SetParent(trainComponent.transform, worldPositionStays: true);
            }
        }

        public bool TryClaimGrab(ulong grabberClientId)
        {
            if (!IsServer || !IsAvailableForGrab)
            {
                return false;
            }

            _claimed = true;
            _grabberClientId.Value = grabberClientId;

            if (ServiceLocator.TryGet(out ITrainGrabResistance resist))
            {
                resist.AddGrabber(_carIndex.Value);
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
            _grabberClientId.Value = NoGrabber;

            if (ServiceLocator.TryGet(out ITrainGrabResistance resist))
            {
                resist.RemoveGrabber(_carIndex.Value);
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

            if (IsServer)
            {
                UpdateServerPosition();
            }
            else
            {
                float t = 1f - Mathf.Exp(-_positionLerpRate * Time.deltaTime);
                transform.position = Vector3.Lerp(transform.position, _syncedPosition.Value, t);
            }
        }

        /// <summary>호스트: 칸의 이탈 오프셋만큼 슬롯 끝에서 뒤로 민 위치로 앵커를 배치하고 복제한다.</summary>
        private void UpdateServerPosition()
        {
            float offset = ServiceLocator.TryGet(out ITrainState train) ? train.GetEjectOffset(_carIndex.Value) : 0f;

            // 열차 원점 고정(미회전) → 슬롯 기준 로컬 좌표가 곧 월드 좌표. 뒤(-Z)로 오프셋만큼 민다.
            Vector3 pos = _endLocalPosition + Vector3.back * offset;
            transform.position = pos;
            _syncedPosition.Value = pos;
        }

        private bool IsCarEjecting()
        {
            return ServiceLocator.TryGet(out ITrainState train)
                && train.TryGetCar(_carIndex.Value, out CarState car)
                && !car.Attached && car.Health > 0f;
        }

        public void OnSpawned()
        {
        }

        public void OnDespawned()
        {
            // 소실 despawn 시 잡고 있던 저항이 남지 않도록 정리(정상 흐름에선 소실=0인이라 무해).
            if (IsServer)
            {
                ReleaseGrab();
            }

            _hasPending = false;
            _claimed = false;
        }
    }
}
