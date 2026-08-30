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

        [Tooltip("천이 얹히는 높이 (m) — 사람이 지나갈 수 있어야 한다.")]
        [SerializeField, Min(0.5f)] private float _canopyHeight = 2.4f;

        [Tooltip("기둥이 모서리 셀 중심에서 안쪽으로 들어오는 거리 (m) — 0이면 셀 한가운데에 선다.")]
        [SerializeField, Min(0f)] private float _postInset;

        public void ApplyFootprint(int width, int length, float cellSize)
        {
            if (cellSize <= 0f)
            {
                return;
            }

            float sizeX = Mathf.Max(1, width) * cellSize;
            float sizeZ = Mathf.Max(1, length) * cellSize;

            if (_canopy != null)
            {
                // 천은 두께(Y)를 건드리지 않는다 — 늘어나는 것은 덮는 넓이뿐이다.
                Vector3 scale = _canopy.localScale;
                _canopy.localScale = new Vector3(sizeX, scale.y, sizeZ);
                _canopy.localPosition = new Vector3(0f, _canopyHeight, 0f);
            }

            ApplyPosts(sizeX, sizeZ, cellSize);
        }

        /// <summary>
        /// 기둥을 네 모서리에 놓는다 — 모서리 셀의 중심이 기본 자리이고, 안쪽 여유만큼 당긴다.
        /// 기둥이 넷보다 적으면 있는 만큼만 쓴다(프리팹이 덜 채워져도 예외를 내지 않는다).
        /// </summary>
        private void ApplyPosts(float sizeX, float sizeZ, float cellSize)
        {
            if (_posts == null)
            {
                return;
            }

            // 모서리 셀 중심 = 발자국 반폭에서 셀 절반만큼 안쪽.
            float x = Mathf.Max(0f, sizeX * 0.5f - cellSize * 0.5f - _postInset);
            float z = Mathf.Max(0f, sizeZ * 0.5f - cellSize * 0.5f - _postInset);

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
                Vector3 local = post.localPosition;
                post.localPosition = new Vector3(signX * x, local.y, signZ * z);
            }
        }
    }
}
