using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 세그먼트의 변주 지점 (레벨 디자인 가이드 §4.4·§4.5) — 타일당 4~10개.
    /// 타일이 풀에서 켜질 때마다 <b>스스로</b> 확률 판정·회전 지터·배율을 적용해,
    /// 같은 베이스가 매번 다르게 보이게 한다.
    ///
    /// <para>계산은 <see cref="ScatterVariationLogic"/>(순수 함수)이 소유하고, 이 컴포넌트는
    /// 난수를 굴려 트랜스폼에 얹는 일만 한다.</para>
    ///
    /// <para><b>순수 장식에만 붙인다.</b> 변주는 각 피어 로컬이라 콜라이더가 달린 것에 적용하면
    /// 피어마다 다른 벽이 생겨 몬스터 경로가 갈린다 — 클리어 존 검사기가 이를 경고로 잡는다.</para>
    /// </summary>
    public sealed class ScatterSlot : MonoBehaviour
    {
        [Tooltip("이 슬롯이 보일 확률 (0 = 항상 숨김 · 1 = 항상 표시).")]
        [Range(0f, 1f)]
        [SerializeField] private float _density = 0.5f;

        [Tooltip("Y축 회전 지터 폭(도). 0이면 저작된 방향을 그대로 쓴다.")]
        [SerializeField] private float _yawJitterDegrees = ScatterVariationLogic.DefaultYawJitterDegrees;

        [Tooltip("균등 배율 범위. 1~1이면 크기를 건드리지 않는다.")]
        [SerializeField] private Vector2 _scaleRange = Vector2.one;

        // 저작된 값 — 변주는 이 기준 위에 얹는다. 매번 누적하면 슬롯이 계속 커지고 돌아간다.
        private Quaternion _baseRotation;
        private Vector3 _baseScale;
        private bool _cached;

        public float Density => _density;

        /// <summary>이번 활성에서 이 슬롯이 보이는가 — 검수·디버깅용.</summary>
        public bool IsShown { get; private set; }

        private void Awake()
        {
            CacheBase();
        }

        private void OnEnable()
        {
            // 슬롯 자신은 항상 활성으로 두고 <b>자식만</b> 토글한다 — 슬롯을 꺼 버리면
            // 다음에 타일이 켜져도 OnEnable이 오지 않아 그 자리가 영영 비어 있게 된다.
            Apply(Random.value, Random.value, Random.value);
        }

        /// <summary>난수를 밖에서 주입해 적용한다 — 재현 가능한 검수·테스트 경로.</summary>
        public void Apply(float showRoll, float yawRoll, float scaleRoll)
        {
            CacheBase();

            IsShown = ScatterVariationLogic.ShouldShow(_density, showRoll);
            SetChildrenActive(IsShown);

            if (!IsShown)
            {
                return;
            }

            float yaw = ScatterVariationLogic.YawFor(yawRoll, _yawJitterDegrees);
            float scale = ScatterVariationLogic.ScaleFor(scaleRoll, _scaleRange.x, _scaleRange.y);

            transform.localRotation = _baseRotation * Quaternion.Euler(0f, yaw, 0f);
            transform.localScale = _baseScale * scale;
        }

        private void CacheBase()
        {
            if (_cached)
            {
                return;
            }

            _baseRotation = transform.localRotation;
            _baseScale = transform.localScale;
            _cached = true;
        }

        private void SetChildrenActive(bool active)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                transform.GetChild(i).gameObject.SetActive(active);
            }
        }
    }
}
