using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 한 지역의 지형 세그먼트 팔레트 (레벨 디자인 가이드 §4.6).
    /// 구성비 기준은 기본형 5(가중 0.65) / 특징형 3(0.25) / 이벤트형 2(0.10)이며,
    /// 지역당 10종이면 같은 타일이 평균 <b>133초마다</b> 재등장한다(가이드 §2.2 — 미러링 포함 20변종).
    ///
    /// <para>추첨은 <see cref="SegmentPickLogic"/>이 타일 인덱스에서 결정론적으로 수행한다 —
    /// 네트워크 상태를 만들지 않고도 전 피어가 같은 지형을 본다.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "TerrainSegmentPalette", menuName = "Game/Terrain Segment Palette")]
    public sealed class TerrainSegmentPalette : ScriptableObject
    {
        [System.Serializable]
        public sealed class Entry
        {
            [Tooltip("이 세그먼트의 타일 프리팹 (길이 40 m · 경계면 y 0 규격을 지켜야 한다).")]
            [SerializeField] private GameObject _prefab;

            [Tooltip("추첨 가중치. 기본형 0.65 / 특징형 0.25 / 이벤트형 0.10을 종수로 나눈 값이 기준이다.")]
            [SerializeField, Min(0f)] private float _weight = 1f;

            [Tooltip("같은 세그먼트가 연달아 나오지 않게 한다 — 교량·유적처럼 강한 것에만 켠다.")]
            [SerializeField] private bool _noRepeatAdjacent;

            [Tooltip("이 세그먼트가 속한 구간 군 (북극 계획 §5.3). 아래 '구간 편성'이 비어 있으면 무시된다 — " +
                "군 번호의 의미는 팔레트 저자가 정한다(북극: 0 얼음 · 1 전이 · 2 바다).")]
            [SerializeField] private int _group;

            public GameObject Prefab => _prefab;

            public float Weight => _weight;

            public bool NoRepeatAdjacent => _noRepeatAdjacent;

            /// <summary>이 세그먼트가 속한 구간 군. <see cref="TerrainSegmentPalette.GroupSchedule"/>이 비면 의미 없다.</summary>
            public int Group => _group;
        }

        [SerializeField] private Entry[] _segments;

        [Tooltip("한 바퀴의 구간 군을 타일 한 장에 하나씩 늘어놓은 배열 (북극 계획 §5.3). " +
            "북극 = [0,0,0,0,0,0, 1, 2,2,2,2,2, 1] 13장(520 m · 87초). " +
            "비우면 타일마다 독립 추첨 — 다른 네 지역은 비워 둔다(팔레트 폴백과 같은 규약).")]
        [SerializeField] private int[] _groupSchedule;

        // 추첨 경로에서 배열을 새로 만들지 않도록 캐시한다 (RegionTimelineSettings와 같은 규약).
        private float[] _weights;
        private bool[] _noRepeat;
        private int[] _groups;
        private float[] _scratch;

        public int Count => _segments == null ? 0 : _segments.Length;

        /// <summary>인덱스의 프리팹. 범위 밖이거나 비었으면 null.</summary>
        public GameObject GetPrefab(int index)
        {
            if (_segments == null || index < 0 || index >= _segments.Length)
            {
                return null;
            }

            return _segments[index].Prefab;
        }

        /// <summary>가중치 배열 (캐시). 비어 있으면 null.</summary>
        public float[] GetWeights()
        {
            if (_weights == null || _weights.Length != Count)
            {
                RebuildCache();
            }

            return _weights;
        }

        /// <summary>인접 반복 금지 플래그 배열 (캐시).</summary>
        public bool[] GetNoRepeatFlags()
        {
            if (_noRepeat == null || _noRepeat.Length != Count)
            {
                RebuildCache();
            }

            return _noRepeat;
        }

        /// <summary>
        /// 구간 편성 (북극 계획 §5.3) — 한 바퀴의 군 번호 배열. 비어 있으면 <c>null</c>이고,
        /// 그때는 추첨이 <b>타일마다 독립</b>으로 떨어진다.
        /// </summary>
        public int[] GroupSchedule =>
            _groupSchedule == null || _groupSchedule.Length == 0 ? null : _groupSchedule;

        /// <summary>세그먼트별 구간 군 번호 (캐시). 비어 있으면 null.</summary>
        public int[] GetEntryGroups()
        {
            if (_groups == null || _groups.Length != Count)
            {
                RebuildCache();
            }

            return _groups;
        }

        /// <summary>
        /// 군 마스크를 쓸 버퍼 (캐시) — 추첨 경로에서 배열을 새로 만들지 않기 위한 것이고,
        /// 값은 호출 사이에 보존되지 않는다.
        /// </summary>
        public float[] GetWeightScratch()
        {
            if (_scratch == null || _scratch.Length != Count)
            {
                RebuildCache();
            }

            return _scratch;
        }

        private void RebuildCache()
        {
            int count = Count;
            if (count == 0)
            {
                _weights = null;
                _noRepeat = null;
                _groups = null;
                _scratch = null;
                return;
            }

            _weights = new float[count];
            _noRepeat = new bool[count];
            _groups = new int[count];
            _scratch = new float[count];
            for (int i = 0; i < count; i++)
            {
                Entry entry = _segments[i];
                // 프리팹이 비면 가중치 0 — 추첨에서 조용히 빠진다 (작업 중인 슬롯을 비워둘 수 있게).
                _weights[i] = entry == null || entry.Prefab == null ? 0f : Mathf.Max(0f, entry.Weight);
                _noRepeat[i] = entry != null && entry.NoRepeatAdjacent;
                _groups[i] = entry == null ? 0 : entry.Group;
            }
        }

        private void OnValidate()
        {
            _weights = null;
            _noRepeat = null;
            _groups = null;
            _scratch = null;
        }
    }
}
