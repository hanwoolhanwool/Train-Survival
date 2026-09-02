namespace Game.Core.Diagnostics
{
    /// <summary>
    /// 한 프레임의 성능 지표. 링버퍼에 프레임 수만큼 쌓이므로 <b>참조 타입이면 안 된다</b> —
    /// 60초 주행이면 수천 개가 쌓이고, 측정 자체가 GC를 만들면 재려는 값이 오염된다.
    /// </summary>
    /// <remarks>
    /// 세 프레임 시간(<see cref="CpuMainMs"/> · <see cref="CpuRenderMs"/> · <see cref="GpuMs"/>) 중
    /// 가장 큰 것이 병목이다 — 이 struct 가 존재하는 이유가 그 판정이다
    /// (성능 프로파일링 자동화 계획 §4.2).
    /// </remarks>
    public readonly struct PerfSample
    {
        public PerfSample(
            int frameIndex,
            float timeSeconds,
            double cpuMainMs,
            double cpuRenderMs,
            double gpuMs,
            long standardDrawCalls,
            long srpBatcherDrawCalls,
            long instancedDrawCalls,
            long brgDrawCalls,
            long setPassCalls,
            long triangles,
            long vertices,
            long shadowCasters,
            long renderTextureBytes,
            long usedBufferBytes,
            long visibleSkinnedMeshes,
            long gcAllocBytes,
            long gcUsedBytes,
            long totalUsedBytes,
            long textureMemoryBytes,
            long meshMemoryBytes)
        {
            FrameIndex = frameIndex;
            TimeSeconds = timeSeconds;
            CpuMainMs = cpuMainMs;
            CpuRenderMs = cpuRenderMs;
            GpuMs = gpuMs;
            StandardDrawCalls = standardDrawCalls;
            SrpBatcherDrawCalls = srpBatcherDrawCalls;
            InstancedDrawCalls = instancedDrawCalls;
            BrgDrawCalls = brgDrawCalls;
            SetPassCalls = setPassCalls;
            Triangles = triangles;
            Vertices = vertices;
            ShadowCasters = shadowCasters;
            RenderTextureBytes = renderTextureBytes;
            UsedBufferBytes = usedBufferBytes;
            VisibleSkinnedMeshes = visibleSkinnedMeshes;
            GcAllocBytes = gcAllocBytes;
            GcUsedBytes = gcUsedBytes;
            TotalUsedBytes = totalUsedBytes;
            TextureMemoryBytes = textureMemoryBytes;
            MeshMemoryBytes = meshMemoryBytes;
        }

        /// <summary>워밍업을 버린 뒤부터 0에서 시작하는 측정 프레임 번호.</summary>
        public int FrameIndex { get; }

        /// <summary>측정 시작으로부터 흐른 시간(초). 스파이크가 주기적인지 판정하는 축이다(§4.3).</summary>
        public float TimeSeconds { get; }

        public double CpuMainMs { get; }

        public double CpuRenderMs { get; }

        /// <summary>Frame Timing Stats 가 꺼져 있으면 0으로 남는다 — 그 자체가 진단 정보다(§1.1).</summary>
        public double GpuMs { get; }

        /// <summary>
        /// 배칭되지 않은 일반 드로우콜.
        /// </summary>
        /// <remarks>
        /// <b>Unity 6에는 "Draw Calls Count"라는 단일 카운터가 없다</b>(2026-09-02 실측 — 카운터
        /// 6,368종을 전량 열거해 확인했고, 그 이름으로 읽으면 조용히 0이 나온다). 드로우콜은
        /// 제출 경로별로 <c>Standard</c> · <c>SRP Batcher</c> · <c>Standard Instanced</c> ·
        /// <c>BRG</c>로 쪼개져 있으므로 <see cref="DrawCalls"/>처럼 합산해야 예산 문서 §6의
        /// 게임 뷰 통계값(1,110)과 같은 자가 된다.
        /// </remarks>
        public long StandardDrawCalls { get; }

        /// <summary>SRP Batcher가 제출한 드로우콜 — URP를 쓰는 이 프로젝트의 주력 경로다.</summary>
        public long SrpBatcherDrawCalls { get; }

        public long InstancedDrawCalls { get; }

        /// <summary>BatchRendererGroup(GPU Resident Drawer가 켜지면 여기로 옮겨간다) 드로우콜.</summary>
        public long BrgDrawCalls { get; }

        /// <summary>제출 경로를 가리지 않은 드로우콜 합계.</summary>
        public long DrawCalls => StandardDrawCalls + SrpBatcherDrawCalls + InstancedDrawCalls + BrgDrawCalls;

        public long SetPassCalls { get; }

        public long Triangles { get; }

        public long Vertices { get; }

        public long ShadowCasters { get; }

        public long RenderTextureBytes { get; }

        public long UsedBufferBytes { get; }

        public long VisibleSkinnedMeshes { get; }

        /// <summary>이 프레임에 일어난 GC 할당. 정상 주행에서는 0에 가까워야 한다(§4.6).</summary>
        public long GcAllocBytes { get; }

        public long GcUsedBytes { get; }

        public long TotalUsedBytes { get; }

        /// <summary>지역 전환에서 해제가 누락되면 여기가 단조 증가한다(§4.2).</summary>
        public long TextureMemoryBytes { get; }

        public long MeshMemoryBytes { get; }

        /// <summary>세 프레임 시간 중 가장 큰 값 — 이 프레임을 붙잡고 있던 쪽.</summary>
        public double SlowestThreadMs
        {
            get
            {
                double slowest = CpuMainMs > CpuRenderMs ? CpuMainMs : CpuRenderMs;
                return GpuMs > slowest ? GpuMs : slowest;
            }
        }
    }
}
