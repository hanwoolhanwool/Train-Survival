using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace Game.Core.Diagnostics
{
    /// <summary>
    /// 런타임 카운터 수집기. <see cref="ProfilerRecorder"/>(카운터)와
    /// <see cref="FrameTimingManager"/>(스레드별 시간)를 한 덩어리로 묶는다.
    ///
    /// <para><b>빌드 Player에서 도는 유일한 정식 경로다</b> — 편집 모드에서는 렌더 카운터가 전부 0이고
    /// (§1.2), 에디터 플레이 모드는 포커스를 잃으면 프레임이 흐르지 않는다(§1.3).</para>
    ///
    /// <para><b>개발 빌드가 전제다.</b> 카운터 다수가 <c>DEVELOPMENT_BUILD</c>에서만 채워지므로
    /// 배포판은 이 값보다 빠르다 — 결과 JSON에 그 사실을 함께 남긴다(§7).</para>
    /// </summary>
    public sealed class PerfProbe : IDisposable
    {
        private const int FrameTimingCapacity = 1;

        private readonly List<PerfSample> _samples;
        private readonly FrameTiming[] _frameTimings = new FrameTiming[FrameTimingCapacity];

        private ProfilerRecorder _standardDrawCalls;
        private ProfilerRecorder _srpBatcherDrawCalls;
        private ProfilerRecorder _instancedDrawCalls;
        private ProfilerRecorder _brgDrawCalls;
        private ProfilerRecorder _setPassCalls;
        private ProfilerRecorder _triangles;
        private ProfilerRecorder _vertices;
        private ProfilerRecorder _shadowCasters;
        private ProfilerRecorder _renderTextureBytes;
        private ProfilerRecorder _usedBufferBytes;
        private ProfilerRecorder _visibleSkinnedMeshes;
        private ProfilerRecorder _gcAllocInFrame;
        private ProfilerRecorder _gcUsedMemory;
        private ProfilerRecorder _totalUsedMemory;
        private ProfilerRecorder _textureMemory;
        private ProfilerRecorder _meshMemory;

        private bool _started;
        private int _frameIndex;
        private float _elapsedSeconds;

        public PerfProbe(int expectedFrames = 4096)
        {
            _samples = new List<PerfSample>(expectedFrames < 16 ? 16 : expectedFrames);
        }

        /// <summary>지금까지 수집한 프레임들. 주행 중에는 계속 늘어난다.</summary>
        public IReadOnlyList<PerfSample> Samples => _samples;

        /// <summary>수집한 프레임 수.</summary>
        public int SampleCount => _samples.Count;

        /// <summary>
        /// <see cref="FrameTimingManager"/>가 실제로 값을 채우고 있는가.
        /// false면 Frame Timing Stats 가 꺼진 빌드다 — 결과 해석에서 GPU 항목을 믿으면 안 된다(§1.1).
        /// </summary>
        public bool FrameTimingAvailable { get; private set; }

        public void Start()
        {
            if (_started)
            {
                return;
            }

            // 이름 하나를 잘못 쓰면 예외 없이 0 이 나온다 — 아래 넷은 2026-09-02 에 카운터 6,368종을
            // 전량 열거해 확인한 실제 이름이다. "Draw Calls Count" 는 Unity 6 에 존재하지 않는다.
            _standardDrawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Standard Draw Calls Count");
            _srpBatcherDrawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SRP Batcher Draw Calls Count");
            _instancedDrawCalls = ProfilerRecorder.StartNew(
                ProfilerCategory.Render, "Standard Instanced Draw Calls Count");
            _brgDrawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "BRG Draw Calls Count");
            _setPassCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            _vertices = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");
            _shadowCasters = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count");
            _renderTextureBytes = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Render Textures Bytes");
            _usedBufferBytes = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Used Buffers Bytes");
            _visibleSkinnedMeshes = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Visible Skinned Meshes Count");
            _gcAllocInFrame = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _gcUsedMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory");
            _totalUsedMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
            _textureMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Texture Memory");
            _meshMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Mesh Memory");

            _started = true;
        }

        /// <summary>한 프레임을 기록한다. 매 프레임 호출되므로 <b>할당을 만들지 않는다</b>.</summary>
        public void Sample(float deltaTime)
        {
            if (!_started)
            {
                return;
            }

            _elapsedSeconds += deltaTime;

            double cpuMainMs = 0.0;
            double cpuRenderMs = 0.0;
            double gpuMs = 0.0;

            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(FrameTimingCapacity, _frameTimings) > 0)
            {
                cpuMainMs = _frameTimings[0].cpuMainThreadFrameTime;
                cpuRenderMs = _frameTimings[0].cpuRenderThreadFrameTime;
                gpuMs = _frameTimings[0].gpuFrameTime;

                if (cpuMainMs > 0.0 || gpuMs > 0.0)
                {
                    FrameTimingAvailable = true;
                }
            }

            _samples.Add(new PerfSample(
                _frameIndex,
                _elapsedSeconds,
                cpuMainMs,
                cpuRenderMs,
                gpuMs,
                _standardDrawCalls.LastValue,
                _srpBatcherDrawCalls.LastValue,
                _instancedDrawCalls.LastValue,
                _brgDrawCalls.LastValue,
                _setPassCalls.LastValue,
                _triangles.LastValue,
                _vertices.LastValue,
                _shadowCasters.LastValue,
                _renderTextureBytes.LastValue,
                _usedBufferBytes.LastValue,
                _visibleSkinnedMeshes.LastValue,
                _gcAllocInFrame.LastValue,
                _gcUsedMemory.LastValue,
                _totalUsedMemory.LastValue,
                _textureMemory.LastValue,
                _meshMemory.LastValue));

            _frameIndex++;
        }

        /// <summary>지표 하나를 뽑아 분포를 낸다. 주행이 끝난 뒤 한 번만 부르는 경로다.</summary>
        public PerfDistribution Describe(Func<PerfSample, double> selector)
        {
            if (selector == null || _samples.Count == 0)
            {
                return default;
            }

            var values = new double[_samples.Count];
            for (int i = 0; i < _samples.Count; i++)
            {
                values[i] = selector(_samples[i]);
            }

            return PerfStats.Describe(values);
        }

        /// <summary>지표 하나를 배열로 뽑는다 (스파이크 계산·30 FPS 카운트용).</summary>
        public double[] Collect(Func<PerfSample, double> selector)
        {
            if (selector == null)
            {
                return Array.Empty<double>();
            }

            var values = new double[_samples.Count];
            for (int i = 0; i < _samples.Count; i++)
            {
                values[i] = selector(_samples[i]);
            }

            return values;
        }

        public void Dispose()
        {
            if (!_started)
            {
                return;
            }

            _standardDrawCalls.Dispose();
            _srpBatcherDrawCalls.Dispose();
            _instancedDrawCalls.Dispose();
            _brgDrawCalls.Dispose();
            _setPassCalls.Dispose();
            _triangles.Dispose();
            _vertices.Dispose();
            _shadowCasters.Dispose();
            _renderTextureBytes.Dispose();
            _usedBufferBytes.Dispose();
            _visibleSkinnedMeshes.Dispose();
            _gcAllocInFrame.Dispose();
            _gcUsedMemory.Dispose();
            _totalUsedMemory.Dispose();
            _textureMemory.Dispose();
            _meshMemory.Dispose();

            _started = false;
        }
    }
}
