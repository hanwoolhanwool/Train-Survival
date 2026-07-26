using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 이탈 칸 이동의 순수 계산 (손잡이-이탈저항 스펙 §4·§9). 네트워크·씬 의존이 없어 EditMode로 전 경계 검증한다.
    /// 밀려나는 속도 = 스크롤 + 추가 후퇴, 저항 = 손잡이 잡은 인원 × 1인 견인력. 순 속도가 음수면 슬롯으로 당겨진다.
    /// </summary>
    public static class EjectMotionMath
    {
        /// <summary>이탈 칸의 순 이동 속도(m/s). +면 후퇴(멀어짐), -면 전진(슬롯으로 당김).</summary>
        public static float ComputeNetVelocity(float scrollSpeed, float ejectExtraSpeed, int grabberCount, float pullPerGrabber)
        {
            float push = Mathf.Max(0f, scrollSpeed) + Mathf.Max(0f, ejectExtraSpeed);
            float resistance = Mathf.Max(0, grabberCount) * Mathf.Max(0f, pullPerGrabber);
            return push - resistance;
        }

        /// <summary>이탈 오프셋을 순 속도로 전진/후퇴시킨다. 슬롯(0) 앞으로는 못 간다(재결합은 후속 단계).</summary>
        public static float StepOffset(float offset, float netVelocity, float deltaTime)
        {
            return Mathf.Max(0f, offset + netVelocity * deltaTime);
        }

        /// <summary>영구 소실 판정 — 아무도 안 잡은 채 소실 거리 이상 멀어졌는가(기획서 §9.1 회수 불가).</summary>
        public static bool IsCarLost(float offset, float lostDistance, int grabberCount)
        {
            return grabberCount <= 0 && offset >= lostDistance;
        }
    }
}
