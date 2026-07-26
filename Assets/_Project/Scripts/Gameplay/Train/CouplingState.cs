using System;
using Unity.Netcode;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 칸과 칸 사이 연결부의 네트워크 직렬화 상태 (기획서 §9 — 연결부가 밤 방어전의 핵심 방어 목표).
    /// 인덱스 c의 연결부는 칸 c(전방)와 칸 c+1(후방)을 잇는다 → 연결부 수 = 칸 수 - 1.
    /// 연결부 파괴 시 칸은 멀쩡한 채로 후방 칸이 통째로 이탈한다(기획서: "3번 연결부 파괴 → 4번 칸 이하 이탈").
    /// </summary>
    public struct CouplingState : INetworkSerializable, IEquatable<CouplingState>
    {
        public float Health;

        public float MaxHealth;

        /// <summary>끊어졌는지 — true면 이 연결부 뒤 칸들이 이탈했다.</summary>
        public bool Broken;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Health);
            serializer.SerializeValue(ref MaxHealth);
            serializer.SerializeValue(ref Broken);
        }

        public bool Equals(CouplingState other)
        {
            return Health.Equals(other.Health)
                && MaxHealth.Equals(other.MaxHealth)
                && Broken == other.Broken;
        }
    }
}
