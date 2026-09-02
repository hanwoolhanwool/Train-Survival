using System.Text;
using Game.Core.Diagnostics;
using Game.Utilities.Performance;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI.Debugging
{
    /// <summary>
    /// 개발 중 눈으로 보는 성능 오버레이 — <b>기록·비교에는 쓰지 않는다</b>
    /// (성능 프로파일링 자동화 계획 결정 ②-B).
    ///
    /// <para><b>왜 이 값으로 판정하면 안 되는가.</b> 에디터 플레이 모드는 창 포커스를 잃으면
    /// 프레임이 흐르지 않고(계획 §1.3), 에디터 자체 부하가 섞인다. 여기 뜨는 숫자는
    /// "지금 뭔가 크게 잘못됐나"를 즉시 알아채기 위한 것이지 기준선이 아니다.
    /// <b>회귀 판정은 빌드 벤치(<c>-perfrun</c>)만 한다.</b></para>
    ///
    /// <para>토글은 <c>F6</c>. 개발 빌드·에디터에서만 컴파일된다.</para>
    /// </summary>
    public sealed class PerfHudView : MonoBehaviour
    {
        /// <summary>표시값 갱신 주기(초). 매 프레임 갱신하면 숫자가 떨려 읽을 수 없다.</summary>
        private const float RefreshInterval = 0.25f;

        /// <summary>이동 평균 창 — 이보다 짧으면 값이 튀고, 길면 변화를 늦게 본다.</summary>
        private const int SampleWindow = 120;

        private static PerfHudView _instance;

        private readonly float[] _frameMs = new float[SampleWindow];
        private int _sampleCount;
        private int _writeIndex;
        private float _refreshTimer;
        private bool _visible = true;
        private string _text = string.Empty;
        private GUIStyle _style;

        private PerfProbe _probe;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (!Application.isEditor && !Debug.isDebugBuild)
            {
                return;
            }

            // 벤치 주행 중에는 띄우지 않는다 — HUD 의 GUI 비용이 측정값에 섞이면 안 된다.
            if (PerfRunArgsResolver.Resolve(System.Environment.GetCommandLineArgs()).IsAutomatedRun)
            {
                return;
            }

            if (_instance != null)
            {
                return;
            }

            var host = new GameObject(nameof(PerfHudView));
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<PerfHudView>();
        }

        private void OnEnable()
        {
            _probe = new PerfProbe(SampleWindow);
            _probe.Start();
        }

        private void OnDisable()
        {
            _probe?.Dispose();
            _probe = null;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f6Key.wasPressedThisFrame)
            {
                _visible = !_visible;
            }

            if (!_visible || _probe == null)
            {
                return;
            }

            float deltaMs = Time.unscaledDeltaTime * 1000f;
            _frameMs[_writeIndex] = deltaMs;
            _writeIndex = (_writeIndex + 1) % SampleWindow;
            if (_sampleCount < SampleWindow)
            {
                _sampleCount++;
            }

            _probe.Sample(Time.unscaledDeltaTime);

            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer >= RefreshInterval)
            {
                _refreshTimer = 0f;
                Rebuild();
            }
        }

        private void Rebuild()
        {
            var values = new double[_sampleCount];
            for (int i = 0; i < _sampleCount; i++)
            {
                values[i] = _frameMs[i];
            }

            PerfDistribution frame = PerfStats.Describe(values);

            PerfSample latest = _probe.SampleCount > 0
                ? _probe.Samples[_probe.SampleCount - 1]
                : default;

            var builder = new StringBuilder(256);
            builder.AppendLine("<b>PERF</b>  (F6 토글 · 기록용 아님)");
            builder.AppendLine($"frame  p50 {frame.P50:0.00}  p95 {frame.P95:0.00}  max {frame.Max:0.00} ms");

            if (_probe.FrameTimingAvailable)
            {
                PerfBottleneck bottleneck = PerfStats.DetermineBottleneck(
                    latest.CpuMainMs, latest.CpuRenderMs, latest.GpuMs);

                builder.AppendLine(
                    $"cpu {latest.CpuMainMs:0.00} / render {latest.CpuRenderMs:0.00} / gpu {latest.GpuMs:0.00} ms");
                builder.AppendLine($"병목  {bottleneck}");
            }
            else
            {
                builder.AppendLine("<color=#ffcc55>Frame Timing Stats 꺼짐 — 스레드별 시간 없음</color>");
            }

            builder.AppendLine($"draw {latest.DrawCalls}  setPass {latest.SetPassCalls}  tris {latest.Triangles:N0}");
            builder.Append($"GC/frame {latest.GcAllocBytes:N0} B");

            _text = builder.ToString();
        }

        private void OnGUI()
        {
            if (!_visible || string.IsNullOrEmpty(_text))
            {
                return;
            }

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 12,
                    richText = true,
                    padding = new RectOffset(8, 8, 6, 6),
                };
            }

            GUI.Label(new Rect(8f, 8f, 340f, 104f), _text, _style);
        }
    }
}
