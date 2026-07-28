using System;
using Unity.Netcode;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 칸 위 건축물 하나의 네트워크 직렬화 상태 (기획서 §9 — 칸 위의 각 건축물도 개별 파괴 가능).
    /// M3에서는 칸당 붙박이 건축물 1개(온실칸 = 온실 돔)만 다루며, 인덱스 = 칸 인덱스로 1:1 대응한다
    /// (자유 건설로 칸당 여러 개가 되는 것은 M5 확장). 칸이 파괴·소실되면 건축물도 함께 사라진다.
    /// </summary>
    public struct StructureState : INetworkSerializable, IEquatable<StructureState>
    {
        /// <summary>이 칸에 건축물이 있는지 — 없는 칸(일반 화물칸 등)은 false로 슬롯만 유지한다.</summary>
        public bool Present;

        public float Health;

        public float MaxHealth;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Present);
            serializer.SerializeValue(ref Health);
            serializer.SerializeValue(ref MaxHealth);
        }

        public bool Equals(StructureState other)
        {
            return Present == other.Present
                && Health.Equals(other.Health)
                && MaxHealth.Equals(other.MaxHealth);
        }
    }
}
