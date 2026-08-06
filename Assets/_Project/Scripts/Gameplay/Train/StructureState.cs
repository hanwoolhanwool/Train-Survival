using System;
using Unity.Netcode;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 칸 위 건축물 하나의 네트워크 직렬화 상태 (기획서 §9 — 칸 위의 각 건축물도 개별 파괴 가능).
    /// 칸당 건축물 1개, 인덱스 = 칸 인덱스로 1:1 대응한다 (자유 다중 설치는 후속 확장).
    /// 종류(<see cref="StructureKind"/>)는 M5 3차에서 도입 — 정의는 <see cref="StructureCatalog"/>가 든다.
    /// 칸이 파괴·소실되면 건축물도 함께 사라진다.
    /// </summary>
    public struct StructureState : INetworkSerializable, IEquatable<StructureState>
    {
        /// <summary>이 칸에 건축물이 있는지 — 없는 칸(일반 화물칸 등)은 false로 슬롯만 유지한다.</summary>
        public bool Present;

        /// <summary>건축물 종류 — Present가 false면 의미 없다. default = Dome(M3 온실 돔과 하위 호환).</summary>
        public StructureKind Kind;

        public float Health;

        public float MaxHealth;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Present);
            serializer.SerializeValue(ref Kind);
            serializer.SerializeValue(ref Health);
            serializer.SerializeValue(ref MaxHealth);
        }

        public bool Equals(StructureState other)
        {
            return Present == other.Present
                && Kind == other.Kind
                && Health.Equals(other.Health)
                && MaxHealth.Equals(other.MaxHealth);
        }
    }
}
