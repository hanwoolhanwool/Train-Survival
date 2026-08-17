using System;
using Unity.Netcode;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 그리드 위 건축물 하나의 네트워크 직렬화 상태 (건축 개편 1차 — 칸당 1슬롯 → 그리드 다중 설치).
    /// <see cref="TrainState"/>의 평탄 리스트(NetworkList)에 담기며, <b>리스트에 있음 = 설치됨</b>이다
    /// (구 StructureState.Present 대체 — 파괴되면 항목이 제거되고 그 자리에 새로 지을 수 있다).
    /// Id는 서버 발급 일련번호 — 철거·피해 RPC가 리스트 인덱스 대신 이 값으로 지목한다(제거·재정렬에 안전).
    /// 점유 면적은 설치 시점 카탈로그 값을 항목에 싣는다 — 카탈로그 조정·후발 접속과 무관하게
    /// 전 피어의 점유 판정이 같은 값으로 성립한다 (MaxHealth 스냅샷과 같은 규약).
    /// </summary>
    public struct StructureEntry : INetworkSerializable, IEquatable<StructureEntry>
    {
        /// <summary>서버 발급 일련번호 (1부터 — 0은 무효). 안정 참조 키.</summary>
        public ushort Id;

        public byte CarIndex;

        /// <summary>점유 영역 좌하단 셀의 열 — 고정 예약 좌표계 (<see cref="StructureGridLogic"/> §2.3).</summary>
        public byte CellX;

        /// <summary>점유 영역 좌하단 셀의 행 (칸 후미 쪽이 0, 전방으로 증가).</summary>
        public byte CellZ;

        /// <summary>설치 회전 0~3 (× 90°) — 홀수면 점유 가로·세로가 스왑된다.</summary>
        public byte Rotation;

        public StructureKind Kind;

        /// <summary>회전 전 기준 점유 가로 셀 수 — 설치 시점 카탈로그 값 스냅샷.</summary>
        public byte FootprintWidth;

        /// <summary>회전 전 기준 점유 세로 셀 수 — 설치 시점 카탈로그 값 스냅샷.</summary>
        public byte FootprintLength;

        public float Health;

        public float MaxHealth;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Id);
            serializer.SerializeValue(ref CarIndex);
            serializer.SerializeValue(ref CellX);
            serializer.SerializeValue(ref CellZ);
            serializer.SerializeValue(ref Rotation);
            serializer.SerializeValue(ref Kind);
            serializer.SerializeValue(ref FootprintWidth);
            serializer.SerializeValue(ref FootprintLength);
            serializer.SerializeValue(ref Health);
            serializer.SerializeValue(ref MaxHealth);
        }

        public bool Equals(StructureEntry other)
        {
            return Id == other.Id
                && CarIndex == other.CarIndex
                && CellX == other.CellX
                && CellZ == other.CellZ
                && Rotation == other.Rotation
                && Kind == other.Kind
                && FootprintWidth == other.FootprintWidth
                && FootprintLength == other.FootprintLength
                && Health.Equals(other.Health)
                && MaxHealth.Equals(other.MaxHealth);
        }
    }
}
