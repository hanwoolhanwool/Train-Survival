using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 원경 풍차의 로터 회전 (대초원 지역 구현 계획 §4.6 · 결정 ⑥) — <b>대초원 고유 코드 1건</b>.
    ///
    /// <para><b>왜 이것만 코드인가.</b> 대초원은 4지역 중 유일하게 날씨가 0종이라
    /// (<c>Region_Grassland._weathers: []</c>) 원경을 지웠다 다시 드러낼 사건이 없다.
    /// 원경이 4일 · 1,560초 내내 같은 상태로 노출되는데, 800 m 거리에서 시차 이동의 각속도는
    /// <b>0.013 °/s</b>(0.18 m/s ÷ 800 m)라 정지 사진과 구분되지 않는다. 날개 회전은
    /// <b>60 °/s</b>(10 rpm)로 그보다 <b>약 4,650배</b> 빠르다 — 대초원 원경의 유일한 맥박이다.</para>
    ///
    /// <para><b>상태가 없다.</b> 각도가 경과 시간 하나의 순수 함수(<see cref="ResolveAngle"/>)라
    /// 누적 오차가 생기지 않고, 늦게 켜져도·프레임을 건너뛰어도 같은 각도로 간다 —
    /// <see cref="DistantSceneryLayer"/>가 누적 주행 거리에 대해 세운 규약과 같은 모양이다.</para>
    ///
    /// <para><b>복제가 없다.</b> 게임플레이 파생이 0이라 결정론 대상이 아니고
    /// <c>NetworkObject</c>를 만들지 않는다. 위상이 피어마다 달라도 무방하다 —
    /// 800 m 원경이라 대조할 기준이 없다. 대신 <b>기수마다</b> 속도와 위상을 흔들어
    /// 군락이 한 몸처럼 돌지 않게 한다(계획 §4.6).</para>
    /// </summary>
    public sealed class DistantWindmillSpin : MonoBehaviour
    {
        /// <summary>계획 §4.6이 정한 기준 회전 — 10 rpm = 60 °/s.</summary>
        public const float ReferenceDegreesPerSecond = 60f;

        [Tooltip("회전축 (로컬). 풍차 로터는 날개면의 법선 = 전방(+Z)을 돈다.")]
        [SerializeField] private Vector3 _axis = Vector3.forward;

        [Tooltip("초당 회전 각도. 기준은 10 rpm = 60 °/s이고, 기수마다 ±10 % 흔들어 둔다.")]
        [SerializeField] private float _degreesPerSecond = ReferenceDegreesPerSecond;

        [Tooltip("시작 위상(도). 군락이 한 몸처럼 돌지 않게 기수마다 다르게 준다.")]
        [SerializeField] private float _phaseDegrees;

        public float DegreesPerSecond => _degreesPerSecond;

        public float PhaseDegrees => _phaseDegrees;

        /// <summary>
        /// 경과 시간에서 회전 각도를 낸다 — <b>상태 없는 순수 함수</b>. 항상 <c>[0, 360)</c>이라
        /// 오래 돌아도 float 정밀도가 무너지지 않는다.
        /// </summary>
        public static float ResolveAngle(float elapsedSeconds, float degreesPerSecond, float phaseDegrees)
        {
            return Mathf.Repeat(phaseDegrees + elapsedSeconds * degreesPerSecond, 360f);
        }

        /// <summary>
        /// 회전이 <b>시차 이동보다 몇 배 빠른가</b> — 계획 §4.6이 결정 ⑥의 근거로 쓴 자다.
        /// 시차 각속도 = 실효 속도 ÷ 거리(라디안)를 도(度)로 환산해 비교한다.
        /// </summary>
        public static float AngularSpeedRatioOverParallax(
            float degreesPerSecond, float parallaxSpeedMetersPerSecond, float distanceMeters)
        {
            if (distanceMeters <= 0f || parallaxSpeedMetersPerSecond <= 0f)
            {
                return float.PositiveInfinity;
            }

            float parallaxDegreesPerSecond = Mathf.Atan(parallaxSpeedMetersPerSecond / distanceMeters) * Mathf.Rad2Deg;
            return Mathf.Abs(degreesPerSecond) / parallaxDegreesPerSecond;
        }

        private void LateUpdate()
        {
            Vector3 axis = _axis.sqrMagnitude < 1e-6f ? Vector3.forward : _axis;
            transform.localRotation = Quaternion.AngleAxis(
                ResolveAngle(Time.time, _degreesPerSecond, _phaseDegrees), axis.normalized);
        }
    }
}
