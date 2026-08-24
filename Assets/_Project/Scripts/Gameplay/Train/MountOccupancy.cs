using System;
using Unity.Netcode;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 거치 무기 하나의 점유 상태 (M7 4차 §2.2) — <b>리스트에 있음 = 점유 중</b>이다
    /// (<see cref="StructureEntry"/>의 "리스트에 있음 = 설치됨"과 같은 규약).
    /// <para>
    /// 점유를 <see cref="StructureEntry"/>에 싣지 않는 이유(결정 ⑤): 그 리스트는 전 피어에 복제되는데
    /// 조준각을 함께 실으면 사람이 포신을 돌리는 동안 매 프레임 리스트가 dirty가 되고, 건축물이
    /// 20개면 20개 항목이 함께 흐른다. <b>주기가 다른 두 값을 한 리스트에 담지 않는다</b> —
    /// 점유는 드물게 바뀌고(이 리스트), 조준각은 매 프레임 바뀐다(표현 전용 중계, §2.4).
    /// </para>
    /// </summary>
    public struct MountOccupancy : INetworkSerializable, IEquatable<MountOccupancy>
    {
        /// <summary>점유된 건축물의 서버 발급 Id (<see cref="StructureEntry.Id"/>) — 안정 참조 키.</summary>
        public ushort StructureId;

        /// <summary>점유자 클라이언트 Id.</summary>
        public ulong OccupantClientId;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref StructureId);
            serializer.SerializeValue(ref OccupantClientId);
        }

        public bool Equals(MountOccupancy other)
        {
            return StructureId == other.StructureId && OccupantClientId == other.OccupantClientId;
        }
    }
}
