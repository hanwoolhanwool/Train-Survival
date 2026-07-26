using System;
using Unity.Netcode;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 열차 한 칸의 네트워크 직렬화 상태 (개발 가이드 §6.3 — 호스트 소유 단일 상태 모델의 원소).
    /// 편성 순서 = 인덱스 순서(0 = 기관차 선두, 값이 클수록 후방). 상태 모델이 M6 재접속 복원의 원천이므로
    /// UI·복원이 설정(SO) 없이도 읽을 수 있도록 최대 체력까지 값에 담는다.
    /// </summary>
    public struct CarState : INetworkSerializable, IEquatable<CarState>
    {
        public CarType Type;

        public float Health;

        public float MaxHealth;

        /// <summary>편성에 연결된 상태인지 — false면 연결부 파괴로 이탈해 후미로 떨어져 나간 칸이다.</summary>
        public bool Attached;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Type);
            serializer.SerializeValue(ref Health);
            serializer.SerializeValue(ref MaxHealth);
            serializer.SerializeValue(ref Attached);
        }

        public bool Equals(CarState other)
        {
            return Type == other.Type
                && Health.Equals(other.Health)
                && MaxHealth.Equals(other.MaxHealth)
                && Attached == other.Attached;
        }
    }
}
