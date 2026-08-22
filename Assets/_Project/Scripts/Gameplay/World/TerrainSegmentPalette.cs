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

            public GameObject Prefab => _prefab;

            public float Weight => _weight;

            public bool NoRepeatAdjacent => _noRepeatAdjacent;
        }

        [SerializeField] private Entry[] _segments;

        // 추첨 경로에서 배열을 새로 만들지 않도록 캐시한다 (RegionTimelineSettings와 같은 규약).
        private float[] _weights;
        private bool[] _noRepeat;

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

        private void RebuildCache()
        {
            int count = Count;
            if (count == 0)
            {
                _weights = null;
                _noRepeat = null;
                return;
            }

            _weights = new float[count];
            _noRepeat = new bool[count];
            for (int i = 0; i < count; i++)
            {
                Entry entry = _segments[i];
                // 프리팹이 비면 가중치 0 — 추첨에서 조용히 빠진다 (작업 중인 슬롯을 비워둘 수 있게).
                _weights[i] = entry == null || entry.Prefab == null ? 0f : Mathf.Max(0f, entry.Weight);
                _noRepeat[i] = entry != null && entry.NoRepeatAdjacent;
            }
        }

        private void OnValidate()
        {
            _weights = null;
            _noRepeat = null;
        }
    }
}
