using UnityEngine;

namespace Game.Gameplay.Monsters
{
    /// <summary>
    /// 보스 페이즈의 순수 계산 로직 (M7 2차 — <see cref="WaveMath"/>·<see cref="StampedeMath"/>와 나란한 축).
    /// 체력 비율이 페이즈를 결정하고, 페이즈가 이동 속도·패턴 빈도·고유 패턴 해금을 결정한다.
    /// 상태를 갖지 않으므로 호스트가 확정한 체력만 있으면 어느 피어에서도 같은 값이 나온다.
    /// </summary>
    public static class BossPhaseMath
    {
        /// <summary>
        /// 체력 비율에서 현재 페이즈 인덱스를 유도한다 (0 = 1페이즈).
        /// 임계값은 내림차순 비율 배열이며, <b>비율이 임계값 이하로 내려간 순간</b> 다음 페이즈다
        /// (경계값 포함 — 정확히 50 %면 이미 2페이즈).
        /// </summary>
        /// <param name="healthRatio">현재 체력 / 최대 체력 (0~1).</param>
        /// <param name="thresholds">페이즈 전환 임계 비율 (내림차순). null·빈 배열이면 항상 0.</param>
        public static int EvaluatePhase(float healthRatio, float[] thresholds)
        {
            if (thresholds == null || thresholds.Length == 0)
            {
                return 0;
            }

            float ratio = Mathf.Clamp01(healthRatio);
            int phase = 0;
            for (int i = 0; i < thresholds.Length; i++)
            {
                if (ratio <= thresholds[i])
                {
                    phase = i + 1;
                }
            }

            return phase;
        }

        /// <summary>페이즈가 오를수록 빨라지는 이동 속도 배율 (페이즈 0 = 1배).</summary>
        public static float SpeedMultiplier(int phaseIndex, float bonusPerPhase)
        {
            return 1f + Mathf.Max(0, phaseIndex) * Mathf.Max(0f, bonusPerPhase);
        }

        /// <summary>
        /// 페이즈가 오를수록 짧아지는 패턴 쿨다운 배율 (페이즈 0 = 1배).
        /// 단계마다 <paramref name="scalePerPhase"/>를 거듭 곱한다.
        /// </summary>
        public static float CooldownScale(int phaseIndex, float scalePerPhase)
        {
            float scale = Mathf.Clamp(scalePerPhase, 0.1f, 1f);
            float result = 1f;
            for (int i = 0; i < Mathf.Max(0, phaseIndex); i++)
            {
                result *= scale;
            }

            return result;
        }

        /// <summary>고유 패턴이 이 페이즈에서 해금됐는가 (결정 ② — 숲·대초원은 페이즈 2부터).</summary>
        public static bool IsSignatureUnlocked(int phaseIndex, int unlockPhaseIndex)
        {
            return phaseIndex >= Mathf.Max(0, unlockPhaseIndex);
        }

        /// <summary>
        /// 이번 발동에 실제로 스폰할 개체 수 — <b>합산 cap</b>(대역폭 방어선)이 상한이다.
        /// 보스 소속 개체는 자기 상한을 넘지 않고, 동시에 밤 웨이브 개체와 합쳐도 합산 상한을 넘지 않는다.
        /// </summary>
        /// <param name="requested">정의가 요구하는 1회 소환 수.</param>
        /// <param name="ownedAlive">현재 살아 있는 보스 소속 개체 수.</param>
        /// <param name="ownedCap">보스 소속 개체의 동시 상한.</param>
        /// <param name="otherAlive">보스와 무관하게 살아 있는 개체 수 (밤 웨이브 등).</param>
        /// <param name="combinedCap">전체 동시 존재 합산 상한.</param>
        public static int PlanSignatureSpawnCount(
            int requested, int ownedAlive, int ownedCap, int otherAlive, int combinedCap)
        {
            if (requested <= 0)
            {
                return 0;
            }

            int byOwnCap = Mathf.Max(0, ownedCap) - Mathf.Max(0, ownedAlive);
            int byCombinedCap = Mathf.Max(0, combinedCap) - (Mathf.Max(0, ownedAlive) + Mathf.Max(0, otherAlive));

            return Mathf.Max(0, Mathf.Min(requested, Mathf.Min(byOwnCap, byCombinedCap)));
        }
    }
}
