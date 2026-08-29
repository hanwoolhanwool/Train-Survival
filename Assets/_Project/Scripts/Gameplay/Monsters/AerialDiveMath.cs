using UnityEngine;

namespace Game.Gameplay.Monsters
{
    /// <summary>하늘 위협의 국면 (바다 계획 §13 — ㄷ 별들린 바닷새).</summary>
    public enum AerialPhase : byte
    {
        /// <summary>순항 — 손 무기가 닿지 않는 높이에서 표적 위를 따라다닌다.</summary>
        Cruise = 0,

        /// <summary>급강하 — 표적 높이까지 내려온다. <b>여기서부터 반격 창이 열린다.</b></summary>
        Dive = 1,

        /// <summary>체공 — 표적 높이에서 잠깐 머무르며 친다.</summary>
        Hover = 2,

        /// <summary>상승 — 순항 고도로 되돌아간다. 창이 닫히는 구간.</summary>
        Climb = 3,
    }

    /// <summary>
    /// 하늘 위협의 급강하 왕복 계산 (바다 지역 구현 계획 §13).
    ///
    /// <para><b>왜 왕복인가.</b> 상시 손 무기 사거리 안에 두면 하늘이라는 축이 사실상
    /// <i>"조금 높은 적"</i>이 되고, 상시 사거리 밖에 두면 거치 무기가 없는 사람은
    /// <b>대응 수단이 0</b>이 된다. 왕복은 그 사이를 가른다 — <b>내려온 순간에만 잡힌다.</b>
    /// 물고기 점프가 <i>도약하는 순간에만 맞는다</i>였던 것과 같은 문법이라, 지역 안에서
    /// 위협의 문법이 하나로 통일된다 (§8.2).</para>
    ///
    /// <para><b>반격 창은 고도가 정한다.</b> 산탄총 사거리가 20 m이고 갑판이 3.566이므로
    /// 손 무기가 닿는 천장은 <b>y 23.5</b>다. 순항 고도를 그 위에 두면 순항 중에는 못 잡고,
    /// 강하·체공·상승 동안만 잡힌다 — 그 시간이 곧 난이도다
    /// (<see cref="ReachWindowSeconds"/>가 그것을 잰다).</para>
    /// </summary>
    public static class AerialDiveMath
    {
        /// <summary>
        /// 다음 국면. 전이는 <b>한 방향으로만</b> 돈다 — 순항 → 강하 → 체공 → 상승 → 순항.
        ///
        /// <para>되돌아가는 전이가 없어야 표적이 잠깐 사라지거나 고도가 흔들려도 왕복이
        /// 도중에 끊기지 않는다. 표적을 잃으면 <b>상승으로 빠져나가</b> 순항으로 복귀한다.</para>
        /// </summary>
        /// <param name="altitude">현재 고도 (월드 y).</param>
        /// <param name="strikeY">표적을 칠 수 있는 높이.</param>
        /// <param name="cruiseY">순항 고도.</param>
        /// <param name="horizontalDistance">표적까지 수평 거리.</param>
        /// <param name="diveTriggerRange">이 거리 안이면 강하를 시작한다.</param>
        /// <param name="hoverRemaining">남은 체공 시간(초).</param>
        /// <param name="hasTarget">표적이 살아 있는가.</param>
        public static AerialPhase ResolvePhase(
            AerialPhase current, float altitude, float strikeY, float cruiseY,
            float horizontalDistance, float diveTriggerRange, float hoverRemaining, bool hasTarget)
        {
            switch (current)
            {
                case AerialPhase.Cruise:
                    return hasTarget && horizontalDistance <= diveTriggerRange
                        ? AerialPhase.Dive
                        : AerialPhase.Cruise;

                case AerialPhase.Dive:
                    if (!hasTarget)
                    {
                        return AerialPhase.Climb;
                    }

                    return altitude <= strikeY + ArrivalTolerance
                        ? AerialPhase.Hover
                        : AerialPhase.Dive;

                case AerialPhase.Hover:
                    return hoverRemaining > 0f && hasTarget ? AerialPhase.Hover : AerialPhase.Climb;

                default:
                    return altitude >= cruiseY - ArrivalTolerance
                        ? AerialPhase.Cruise
                        : AerialPhase.Climb;
            }
        }

        /// <summary>고도 도달 판정 여유 (m). 속도 × dt 보다 넉넉해야 국면이 멈추지 않는다.</summary>
        public const float ArrivalTolerance = 0.3f;

        /// <summary>이 국면이 향하는 고도.</summary>
        public static float TargetAltitude(AerialPhase phase, float cruiseY, float strikeY)
        {
            switch (phase)
            {
                case AerialPhase.Dive:
                case AerialPhase.Hover:
                    return strikeY;

                default:
                    return cruiseY;
            }
        }

        /// <summary>
        /// 이번 프레임의 고도. 목표를 <b>넘어가지 않는다</b> — 넘으면 국면 판정이 진동한다.
        /// </summary>
        public static float StepAltitude(float current, float target, float speed, float deltaTime)
        {
            return Mathf.MoveTowards(current, target, Mathf.Max(0f, speed) * Mathf.Max(0f, deltaTime));
        }

        /// <summary>
        /// 손에 든 무기가 닿는가 — <b>반격 창이 열려 있는가</b>와 같은 말이다.
        /// 사수와 비행체의 <b>수직 거리</b>만 본다(수평은 사수가 좁힐 수 있다).
        /// </summary>
        public static bool IsWithinWeaponReach(float flyerY, float shooterY, float weaponRange)
        {
            return flyerY - shooterY <= weaponRange;
        }

        /// <summary>
        /// 왕복 한 번에 <b>반격할 수 있는 시간</b>(초) — 난이도를 재는 자다.
        ///
        /// <para>강하·상승 중 무기가 닿는 구간과 체공 전부를 더한다. 이 값이 너무 짧으면
        /// 손 무기로는 사실상 대응이 불가능하고, 너무 길면 하늘이 위협이 되지 않는다.</para>
        /// </summary>
        public static float ReachWindowSeconds(
            float cruiseY, float strikeY, float shooterY, float weaponRange,
            float diveSpeed, float climbSpeed, float hoverSeconds)
        {
            float ceiling = shooterY + weaponRange;
            if (ceiling <= strikeY || diveSpeed <= 0f || climbSpeed <= 0f)
            {
                return Mathf.Max(0f, hoverSeconds);
            }

            // 순항이 천장 아래면 왕복 전체가 사거리 안이다.
            float entryHeight = Mathf.Min(cruiseY, ceiling) - strikeY;
            if (entryHeight <= 0f)
            {
                return Mathf.Max(0f, hoverSeconds);
            }

            return entryHeight / diveSpeed + Mathf.Max(0f, hoverSeconds) + entryHeight / climbSpeed;
        }

        /// <summary>
        /// 표적을 향한 <b>수평</b> 접근 속도. 하늘에는 막을 것이 없어 지상 조향(장애물 회피)을
        /// 쓰지 않는다. 다만 <b>월드는 뒤로 흐르므로</b> 스크롤을 더해야 제자리에 머물지 않는다.
        /// </summary>
        public static Vector3 ComputeApproachVelocity(
            Vector3 self, Vector3 target, float moveSpeed, float scrollSpeed)
        {
            Vector3 flat = new Vector3(target.x - self.x, 0f, target.z - self.z);
            Vector3 chase = flat.sqrMagnitude < 0.0001f
                ? Vector3.zero
                : flat.normalized * Mathf.Max(0f, moveSpeed);

            // 지상 몬스터가 컨베이어를 이겨야 하는 것과 같은 이유다 (MonsterSteering).
            chase.z -= scrollSpeed;
            return chase;
        }
    }
}
