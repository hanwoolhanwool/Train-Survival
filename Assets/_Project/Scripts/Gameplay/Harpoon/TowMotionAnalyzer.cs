using UnityEngine;

namespace Game.Gameplay.Harpoon
{
    /// <summary>견인 프레임 샘플의 판정 결과 (Q3).</summary>
    public enum TowSampleVerdict
    {
        /// <summary>정상 이동.</summary>
        Normal,

        /// <summary>한 프레임 이동량이 릴·플레이어 상대 속도로 설명할 수 없게 큼 (워프).</summary>
        Warp,

        /// <summary>총구까지의 거리가 지금까지의 최소치보다 다시 늘어남 (역행).</summary>
        Regression,
    }

    /// <summary>
    /// Q3 계측(슬라이스 스펙 §3.2) — 견인 중 대상 이동의 워프/역행을 프레임 단위로 검출하는 순수 로직.
    /// 견인 표시는 30 Hz 스냅샷 + 보간이라 미세 변동이 정상이므로, 허용치를 넘는 이상만 집계한다.
    /// 역행은 순간 노이즈가 아닌 "최소 도달 거리 대비 되돌아간 양"으로 판정해 느린 되밀림도 잡는다.
    /// </summary>
    public sealed class TowMotionAnalyzer
    {
        // 플레이어가 후퇴(최대 7 m/s)해도 릴(8 m/s)이 이기므로, 상대 이동 허용치는 두 속도의 합으로 잡는다.
        private const float RelativeMotionAllowance = 7f;
        private const float WarpToleranceFactor = 3f;
        private const float MinWarpStep = 1f;
        private const float RegressionEpsilon = 0.3f;

        /// <summary>
        /// 견인 시작 유예 (기본값) — 승인 도착부터 첫 30 Hz 견인 스냅샷까지는 대상이 아직 컨베이어로
        /// 밀리는 중이고 위치 전환 스냅이 겹치는 구조적 구간이라(§2.4 탄성 흡수 구간) 이상으로 집계하지 않는다.
        /// </summary>
        public const float DefaultStartGraceDuration = 0.2f;

        private readonly float _reelSpeed;
        private readonly float _startGraceDuration;

        private bool _hasSample;
        private Vector3 _previousPosition;
        private float _minDistance;
        private bool _inRegression;
        private float _elapsed;

        public TowMotionAnalyzer(float reelSpeed, float startGraceDuration = DefaultStartGraceDuration)
        {
            _reelSpeed = Mathf.Max(0.1f, reelSpeed);
            _startGraceDuration = Mathf.Max(0f, startGraceDuration);
        }

        public int SampleCount { get; private set; }

        public int WarpCount { get; private set; }

        public int RegressionCount { get; private set; }

        /// <summary>관측된 최대 한 프레임 이동량 (m).</summary>
        public float MaxStep { get; private set; }

        /// <summary>직전 샘플의 한 프레임 이동량 (m) — 이상 로그 출력용.</summary>
        public float LastStep { get; private set; }

        /// <summary>직전 샘플의 총구까지 거리 (m) — 이상 로그 출력용.</summary>
        public float LastDistance { get; private set; }

        public bool IsClean => WarpCount == 0 && RegressionCount == 0;

        public TowSampleVerdict Feed(Vector3 hookPosition, Vector3 anchorPosition, float deltaTime)
        {
            float distance = Vector3.Distance(hookPosition, anchorPosition);
            LastDistance = distance;

            if (!_hasSample)
            {
                _hasSample = true;
                _previousPosition = hookPosition;
                _minDistance = distance;
                LastStep = 0f;
                SampleCount = 1;
                _elapsed = 0f;
                return TowSampleVerdict.Normal;
            }

            SampleCount++;
            _elapsed += deltaTime;

            float step = Vector3.Distance(hookPosition, _previousPosition);
            _previousPosition = hookPosition;
            LastStep = step;

            // 시작 유예 중에는 기준점(최소 거리)만 따라가고 워프/역행·최대 이동량을 집계하지 않는다.
            if (_elapsed < _startGraceDuration)
            {
                _minDistance = distance;
                _inRegression = false;
                return TowSampleVerdict.Normal;
            }

            if (step > MaxStep)
            {
                MaxStep = step;
            }

            if (deltaTime > 0f)
            {
                float warpThreshold = Mathf.Max((_reelSpeed + RelativeMotionAllowance) * deltaTime * WarpToleranceFactor, MinWarpStep);
                if (step > warpThreshold)
                {
                    WarpCount++;
                    // 워프로 튄 위치를 새 기준으로 삼아, 같은 사건이 역행으로 중복 집계되지 않게 한다.
                    _minDistance = distance;
                    _inRegression = false;
                    return TowSampleVerdict.Warp;
                }
            }

            if (distance > _minDistance + RegressionEpsilon)
            {
                // 상승 에지에서만 1회 집계 — 한 번의 되밀림이 프레임 수만큼 불어나지 않게 한다.
                if (!_inRegression)
                {
                    _inRegression = true;
                    RegressionCount++;
                }

                return TowSampleVerdict.Regression;
            }

            _inRegression = false;
            if (distance < _minDistance)
            {
                _minDistance = distance;
            }

            return TowSampleVerdict.Normal;
        }
    }
}
