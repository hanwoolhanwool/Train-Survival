using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 이탈 칸 이동의 순수 계산 (손잡이-이탈저항 스펙 §4·§9). 네트워크·씬 의존이 없어 EditMode로 전 경계 검증한다.
    /// 밀려나는 속도 = 스크롤 + 추가 후퇴, 저항 = 손잡이 잡은 인원 × 1인 견인력. 순 속도가 음수면 슬롯으로 당겨진다.
    /// 분리 직후에는 관성으로 열차 속도를 유지하다가 감속도만큼 서서히 속도를 잃는다 — 밀림 속도가 0에서 목표까지 램프한다.
    /// </summary>
    public static class EjectMotionMath
    {
        /// <summary>이탈 칸이 관성을 전부 잃었을 때 도달하는 목표 밀림 속도(m/s) = 스크롤 + 추가 후퇴.</summary>
        public static float ComputeTargetPushSpeed(float scrollSpeed, float ejectExtraSpeed)
        {
            return Mathf.Max(0f, scrollSpeed) + Mathf.Max(0f, ejectExtraSpeed);
        }

        /// <summary>
        /// 현재 밀림 속도를 목표를 향해 감속도(m/s²)만큼 근접시킨다. 분리 순간 0(열차와 같은 속도)에서 시작해
        /// 서서히 올라가고, 스크롤이 느려져 목표가 낮아지면 같은 비율로 따라 내려온다.
        /// </summary>
        public static float StepPushSpeed(float pushSpeed, float targetPushSpeed, float deceleration, float deltaTime)
        {
            return Mathf.MoveTowards(
                Mathf.Max(0f, pushSpeed), Mathf.Max(0f, targetPushSpeed), Mathf.Max(0f, deceleration) * deltaTime);
        }

        /// <summary>이탈 칸의 순 이동 속도(m/s). +면 후퇴(멀어짐), -면 전진(슬롯으로 당김).</summary>
        public static float ComputeNetVelocity(float pushSpeed, int grabberCount, float pullPerGrabber)
        {
            float resistance = Mathf.Max(0, grabberCount) * Mathf.Max(0f, pullPerGrabber);
            return Mathf.Max(0f, pushSpeed) - resistance;
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
