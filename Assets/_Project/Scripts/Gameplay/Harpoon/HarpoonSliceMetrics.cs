using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Game.Gameplay.Harpoon
{
    /// <summary>
    /// M1 손맛 검증(슬라이스 스펙 §3.2)의 Q1~Q4 계측을 한곳에서 집계·출력하는 정적 허브.
    /// 이벤트 발생 시에만 문자열을 만들고(프레임당 할당 없음 — Q3의 프레임 샘플은 float 연산만),
    /// 모든 기록 메서드는 <see cref="ConditionalAttribute"/>로 에디터·개발 빌드에서만 컴파일된다
    /// (릴리스 빌드에서는 호출 자체가 제거되어 비용 0).
    /// 기록은 소유자(로컬 플레이어) 경로에서만 호출되므로, 집계는 프로세스당 로컬 플레이어 1명 기준이다.
    /// </summary>
    public static class HarpoonSliceMetrics
    {
        private const string EditorSymbol = "UNITY_EDITOR";
        private const string DevBuildSymbol = "DEVELOPMENT_BUILD";

        private static int _fireCount;
        private static int _localHitCount;
        private static int _missCount;
        private static int _approvedCount;
        private static int _rejectedCount;

        private static double _latencySumMs;
        private static double _latencyMinMs;
        private static double _latencyMaxMs;

        private static TowMotionAnalyzer _towAnalyzer;

        /// <summary>Q1·Q4 — 발사 1회 기록. 입력 프레임과 현재(연출 발행 완료) 프레임의 차로 Q1을 증명한다.</summary>
        [Conditional(EditorSymbol), Conditional(DevBuildSymbol)]
        public static void RecordFire(int inputFrame)
        {
            _fireCount++;
            int delta = Time.frameCount - inputFrame;
            Debug.Log($"[SliceMetrics] Q1 — 발사 입력 프레임 {inputFrame} → 로컬 연출 발행 프레임 {Time.frameCount} (Δ {delta} 프레임, 목표 ≤ 1)");
        }

        /// <summary>Q4 분모 — 로컬 명중 판정 1회 기록.</summary>
        [Conditional(EditorSymbol), Conditional(DevBuildSymbol)]
        public static void RecordLocalHit()
        {
            _localHitCount++;
        }

        /// <summary>참고 — 빗나감 1회 기록 (Q4 분모에는 포함하지 않는다).</summary>
        [Conditional(EditorSymbol), Conditional(DevBuildSymbol)]
        public static void RecordMiss()
        {
            _missCount++;
        }

        /// <summary>Q2 — 로컬 명중 → 그랩 승인 수신 지연 기록. 누적 최소/평균/최대와 Q4 집계를 함께 출력한다.</summary>
        [Conditional(EditorSymbol), Conditional(DevBuildSymbol)]
        public static void RecordGrabApproved(double latencyMs)
        {
            _approvedCount++;
            _latencySumMs += latencyMs;
            _latencyMinMs = _approvedCount == 1 ? latencyMs : System.Math.Min(_latencyMinMs, latencyMs);
            _latencyMaxMs = System.Math.Max(_latencyMaxMs, latencyMs);

            double avg = _latencySumMs / _approvedCount;
            Debug.Log($"[SliceMetrics] Q2 — 로컬 명중 → 그랩 승인 수신: {latencyMs:F0} ms (n={_approvedCount}, 최소 {_latencyMinMs:F0} / 평균 {avg:F0} / 최대 {_latencyMaxMs:F0} ms, 목표 프로파일 ② ≤ 250 ms)");
            LogTallySummary();
        }

        /// <summary>
        /// Q4 분자 — 클라 명중 표시 후 호스트 거부(판정 불일치) 1회 기록.
        /// 등급 부족(M5 5차)은 경합이 아니라 <b>규칙에 의한 정상 거부</b>이므로 불일치율에 세지 않는다 —
        /// 세면 상위 자원을 쏴 볼 때마다 Q4 지표가 오염된다.
        /// </summary>
        [Conditional(EditorSymbol), Conditional(DevBuildSymbol)]
        public static void RecordGrabRejected(GrabVerdict verdict)
        {
            if (verdict == GrabVerdict.InsufficientTier)
            {
                Debug.Log("[SliceMetrics] 그랩 거부 — 집게 등급 부족 (규칙 거부, Q4 불일치 아님)");
                return;
            }

            _rejectedCount++;
            Debug.Log($"[SliceMetrics] Q4 — 호스트 거부 발생: {verdict}");
            LogTallySummary();
        }

        /// <summary>Q3 — 그랩 승인 시점부터 견인 프레임 샘플 추적을 시작한다.</summary>
        [Conditional(EditorSymbol), Conditional(DevBuildSymbol)]
        public static void BeginTowTracking(float reelSpeed)
        {
            _towAnalyzer = new TowMotionAnalyzer(reelSpeed);
        }

        /// <summary>Q3 — 견인 중 프레임 샘플 1개 공급. 워프/역행 검출 시에만 로그를 남긴다.</summary>
        [Conditional(EditorSymbol), Conditional(DevBuildSymbol)]
        public static void RecordTowSample(Vector3 hookPosition, Vector3 anchorPosition, float deltaTime)
        {
            if (_towAnalyzer == null)
            {
                return;
            }

            TowSampleVerdict verdict = _towAnalyzer.Feed(hookPosition, anchorPosition, deltaTime);
            if (verdict != TowSampleVerdict.Normal)
            {
                Debug.LogWarning($"[SliceMetrics] Q3 이상 — {verdict}: 프레임 이동 {_towAnalyzer.LastStep:F2} m, 총구 거리 {_towAnalyzer.LastDistance:F2} m (프레임 {Time.frameCount})");
            }
        }

        /// <summary>Q3 — 견인 종료(도착·해제·취소) 시 요약을 출력하고 추적을 닫는다.</summary>
        [Conditional(EditorSymbol), Conditional(DevBuildSymbol)]
        public static void EndTowTracking(string reason)
        {
            if (_towAnalyzer == null)
            {
                return;
            }

            if (_towAnalyzer.SampleCount > 0)
            {
                string result = _towAnalyzer.IsClean ? "정상 (워프/역행 없음)" : "이상 감지";
                Debug.Log($"[SliceMetrics] Q3 — 견인 종료({reason}): 샘플 {_towAnalyzer.SampleCount} 프레임, 워프 {_towAnalyzer.WarpCount} · 역행 {_towAnalyzer.RegressionCount}, 최대 프레임 이동 {_towAnalyzer.MaxStep:F2} m → {result}");
            }

            _towAnalyzer = null;
        }

        /// <summary>테스트·세션 재시작용 초기화 (아키텍처 규칙 — 정적 상태는 Clear 제공).</summary>
        public static void Clear()
        {
            _fireCount = 0;
            _localHitCount = 0;
            _missCount = 0;
            _approvedCount = 0;
            _rejectedCount = 0;
            _latencySumMs = 0.0;
            _latencyMinMs = 0.0;
            _latencyMaxMs = 0.0;
            _towAnalyzer = null;
        }

        private static void LogTallySummary()
        {
            // 불일치율 = 거부 / 로컬 명중 (§3.2 Q4 — 클라 명중 표시가 전제이므로 분모는 발사가 아닌 로컬 명중).
            double rate = _localHitCount > 0 ? _rejectedCount * 100.0 / _localHitCount : 0.0;
            Debug.Log($"[SliceMetrics] Q4 — 발사 {_fireCount} · 빗나감 {_missCount} · 로컬 명중 {_localHitCount} · 승인 {_approvedCount} · 거부 {_rejectedCount} → 불일치율 {rate:F1} % (목표 < 1 %, 표본 100회+)");
        }
    }
}
