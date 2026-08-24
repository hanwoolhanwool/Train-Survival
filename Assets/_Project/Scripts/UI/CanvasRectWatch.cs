using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 캔버스 rect가 마지막 배치 이후 달라졌는지 지켜보는 눈금 —
    /// <b>점 앵커</b>로 놓인 UI가 해상도 변화를 놓치지 않게 하는 유일한 수단이다.
    ///
    /// <para><b>왜 프레임마다 재는가</b>: <c>OnRectTransformDimensionsChange</c>는 <b>자기 rect의
    /// 크기가 실제로 변할 때만</b> 온다. 앵커가 한 점(<c>anchorMin == anchorMax</c>)인 rect는
    /// 부모가 아무리 커져도 크기가 <c>sizeDelta</c> 그대로라 <b>통지가 오지 않는다</b>.
    /// 배너·공고대·대기실 패널이 전부 그런 rect다 — 크기를 캔버스 높이에서 직접 내기 때문이다.
    /// 실측으로 확인했다: 부모를 1080에서 1440으로 늘려도 콜백이 한 번도 오지 않았고,
    /// 자식의 <c>sizeDelta</c>는 1668.36에 그대로 머물렀다(기대값 2224.48).</para>
    ///
    /// <para><b>왜 <c>OnEnable</c> 한 번으로는 부족한가</b>: 빌드 첫 실행에서는 캔버스 rect가
    /// <c>OnEnable</c>보다 <b>늦게</b> 확정된다 — 창이 만들어지고 전체화면으로 넘어가고
    /// <c>CanvasScaler</c>가 첫 <c>Update</c>에서 배율을 잡을 때까지. 그래서 로비를 처음 열면
    /// 비율이 어긋난 채로 굳고, 게임에 들어갔다 나와 <c>Main</c> 씬을 다시 로드하면 그제야 맞는다 —
    /// 두 번째 <c>OnEnable</c>은 이미 확정된 캔버스를 보기 때문이다. 그 차이가 곧 이 버그였다.</para>
    ///
    /// <para>비교는 <see cref="Vector2"/> 하나뿐이라, 매 프레임 확인해도 값이 그대로면 아무 일도
    /// 일어나지 않는다. 배치를 다시 하는 것은 <b>실제로 크기가 달라진 프레임</b>뿐이다.</para>
    /// </summary>
    public struct CanvasRectWatch
    {
        private Vector2 _applied;
        private bool _hasApplied;

        /// <summary>
        /// 이 크기로 다시 배치해야 하는가.
        ///
        /// <para>아직 유효하지 않은 크기(0 이하)는 <b>변화로 치지 않는다</b> — 캔버스가 서기 전의
        /// 한두 프레임이 여기 걸리는데, 그때 배치해 봐야 0으로 접힌 rect가 나온다.</para>
        /// </summary>
        public bool NeedsApply(Vector2 canvasSize)
        {
            if (canvasSize.x <= 0f || canvasSize.y <= 0f)
            {
                return false;
            }

            return !_hasApplied || canvasSize != _applied;
        }

        /// <summary>이 크기로 배치를 마쳤다고 적어 둔다.</summary>
        public void MarkApplied(Vector2 canvasSize)
        {
            _applied = canvasSize;
            _hasApplied = true;
        }

        /// <summary>
        /// 다음 확인에서 크기가 같아도 다시 배치하게 되돌린다 —
        /// 스프라이트 교체처럼 <b>캔버스 크기 밖의 사정</b>이 바뀌었을 때.
        /// </summary>
        public void Invalidate()
        {
            _hasApplied = false;
        }
    }
}
