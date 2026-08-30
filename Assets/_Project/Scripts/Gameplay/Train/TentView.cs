using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 천막 표현 (천막 계획 1차 §4.7) — 프리팹 하나가 드래그로 정해진 모든 크기를 감당한다.
    /// 천은 발자국만큼 늘어나고 <b>기둥 넷은 굵기를 유지한 채 모서리로 흩어진다</b>
    /// (루트를 통째로 스케일하면 기둥까지 늘어나 뭉갠다 — 계획 리스크 5).
    ///
    /// 콜라이더를 두지 않는다 — 점유가 기둥뿐이라도 사람은 천막을 그냥 통과해 걸어야 한다(결정 ⑥).
    /// 피해 표적면은 <see cref="StructureView"/>가 소유하므로 여기서는 모양만 맡는다.
    /// </summary>
    // RequireComponent를 달지 않는다 — 이 뷰는 StructureView의 API를 하나도 쓰지 않고,
    // 고스트 프리뷰에서는 <b>StructureView 없이 혼자</b> 살아야 하기 때문이다.
    // 프리뷰 사본은 상태 바인딩을 떼는데, 의존을 선언해 두면 StructureView를 떼지 못해
    // "Can't remove StructureView because TentView depends on it"으로 막힌다.
    // 실물 프리팹에는 둘이 함께 붙고, 그 배선은 TentCatalogAssetTests가 지킨다.
    public sealed class TentView : MonoBehaviour, IStructureFootprintView
    {
        [Tooltip("천(지붕) — 발자국만큼 X·Z로 늘어난다.")]
        [SerializeField] private Transform _canopy;

        [Tooltip("기둥 넷 — 발자국 모서리로 옮겨지고 굵기는 그대로다. 순서는 상관없다(모서리를 코드가 배정).")]
        [SerializeField] private Transform[] _posts;

        [Tooltip("천이 얹히는 높이 (m) — 사람이 지나다니는 아래를 넉넉히 남긴다. 기둥 길이도 이 값을 따른다.")]
        [SerializeField, Min(0.5f)] private float _canopyHeight = 3.1f;

        [Tooltip("기둥이 모서리 셀 중심에서 안쪽으로 들어오는 거리 (m) — 0이면 셀 한가운데에 선다.")]
        [SerializeField, Min(0f)] private float _postInset;

        [Tooltip("기둥 바깥으로 천이 더 나가는 처마 길이 (m). 0이면 천 끝이 기둥에 딱 맞아 " +
            "매어 놓은 티가 안 난다.")]
        [SerializeField, Min(0f)] private float _eaveOverhang = 0.35f;

        [Tooltip("처짐이 1배가 되는 기준 변 길이 (m) — 이보다 넓은 천은 더 처지고 좁으면 팽팽해진다. " +
            "메시의 sag·주름이 Y 스케일로 함께 늘어난다.")]
        [SerializeField, Min(0.5f)] private float _sagReferenceSpan = 2.6f;

        public void ApplyFootprint(int width, int length, float cellSize)
        {
            if (cellSize <= 0f)
            {
                return;
            }

            // 기둥은 <b>모서리 셀의 중심</b>에 서므로 기둥 사이 거리는 발자국보다 한 칸 짧다.
            // 천을 발자국 끝까지 늘리면 기둥보다 반 칸씩 밖으로 떠서, 매달린 데 없는 천이
            // 허공에서 꺾여 <b>찢어진 것처럼</b> 보인다 (Play 4회차 지적).
            // 기둥 사이에 걸고 처마만 조금 내보낸다.
            float postSpanX = Mathf.Max(1, width - 1) * cellSize;
            float postSpanZ = Mathf.Max(1, length - 1) * cellSize;
            float sizeX = postSpanX + _eaveOverhang * 2f;
            float sizeZ = postSpanZ + _eaveOverhang * 2f;

            if (_canopy != null)
            {
                // 처짐은 넓이를 따라간다 — Y 스케일이 메시의 sag·주름을 함께 늘린다.
                // 고정으로 두면 큰 천막일수록 평평해져 판때기로 보인다(실제 천은 넓을수록 더 처진다).
                float sagScale = Mathf.Clamp(Mathf.Max(sizeX, sizeZ) / _sagReferenceSpan, 0.75f, 2.6f);
                _canopy.localScale = new Vector3(sizeX, sagScale, sizeZ);
                _canopy.localPosition = new Vector3(0f, _canopyHeight, 0f);
            }

            ApplyPosts(postSpanX, postSpanZ);
        }

        /// <summary>
        /// 기둥을 네 모서리에 놓는다 — 모서리 셀의 중심이 기본 자리이고, 안쪽 여유만큼 당긴다.
        /// 기둥이 넷보다 적으면 있는 만큼만 쓴다(프리팹이 덜 채워져도 예외를 내지 않는다).
        /// </summary>
        private void ApplyPosts(float postSpanX, float postSpanZ)
        {
            if (_posts == null)
            {
                return;
            }

            float x = Mathf.Max(0f, postSpanX * 0.5f - _postInset);
            float z = Mathf.Max(0f, postSpanZ * 0.5f - _postInset);

            for (int i = 0; i < _posts.Length; i++)
            {
                Transform post = _posts[i];
                if (post == null)
                {
                    continue;
                }

                // 0=(-,-) 1=(+,-) 2=(-,+) 3=(+,+) — 네 모서리를 비트로 배정한다.
                float signX = (i & 1) == 0 ? -1f : 1f;
                float signZ = (i & 2) == 0 ? -1f : 1f;
                post.localPosition = new Vector3(signX * x, _canopyHeight * 0.5f, signZ * z);

                // 기둥 길이는 천 높이를 따른다 — 프리팹 값과 어긋나 천이 허공에 뜨거나
                // 기둥이 뚫고 나오는 일이 없게 한 곳에서 정한다. 굵기(X·Z)는 프리팹 것을 쓴다.
                Vector3 scale = post.localScale;
                post.localScale = new Vector3(scale.x, _canopyHeight, scale.z);
            }
        }
    }
}
