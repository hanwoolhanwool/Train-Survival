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

        /// <summary>
        /// 좌측(-X)에 덧댄 판자 열 수 (건축 개편 3차 — 결정 ⑥: 셀 열 단위 증축).
        /// 상한은 에셋(<see cref="TrainExpansionSettings.MaxPlankColumns"/>)과 좌표계 예약
        /// (<see cref="StructureGridLogic.MaxPlankColumnsPerSide"/>) 중 작은 쪽이다.
        /// </summary>
        public byte LeftPlanks;

        /// <summary>우측(+X)에 덧댄 판자 열 수 — <see cref="LeftPlanks"/>와 같은 규약.</summary>
        public byte RightPlanks;

        /// <summary>그 쪽 판자 열 수 — 좌/우 분기를 값 쪽에 모은다(판정·뷰·조준이 같은 접근자를 쓴다).</summary>
        public byte Planks(PlankSide side)
        {
            return side == PlankSide.Left ? LeftPlanks : RightPlanks;
        }

        /// <summary>그 쪽 판자 열 수를 정한다 — 호스트 변이 전용(복제는 목록 대입이 맡는다).</summary>
        public void SetPlanks(PlankSide side, byte columns)
        {
            if (side == PlankSide.Left)
            {
                LeftPlanks = columns;
            }
            else
            {
                RightPlanks = columns;
            }
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Type);
            serializer.SerializeValue(ref Health);
            serializer.SerializeValue(ref MaxHealth);
            serializer.SerializeValue(ref Attached);
            serializer.SerializeValue(ref LeftPlanks);
            serializer.SerializeValue(ref RightPlanks);
        }

        public bool Equals(CarState other)
        {
            return Type == other.Type
                && Health.Equals(other.Health)
                && MaxHealth.Equals(other.MaxHealth)
                && Attached == other.Attached
                && LeftPlanks == other.LeftPlanks
                && RightPlanks == other.RightPlanks;
        }
    }
}
