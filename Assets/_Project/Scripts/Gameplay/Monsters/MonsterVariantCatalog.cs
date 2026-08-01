using UnityEngine;

namespace Game.Gameplay.Monsters
{
    /// <summary>
    /// 웨이브에 등장하는 몬스터 변종 목록 (기획서 §5 — Day 비례 난이도의 "패턴" 축).
    /// 변종은 프리팹이 아니라 <see cref="MonsterSettings"/>로 구분한다 — 이동속도·체력·공격·도약
    /// 사거리 조합이 곧 행동 차이이므로, 같은 프리팹에 다른 설정을 주입하는 것으로 성립한다.
    /// 인덱스가 곧 복제 식별자이므로 <b>배열 순서를 바꾸면 진행 중인 세션의 변종이 뒤바뀐다.</b>
    /// </summary>
    [CreateAssetMenu(fileName = "MonsterVariantCatalog", menuName = "Game/Monster Variant Catalog")]
    public sealed class MonsterVariantCatalog : ScriptableObject
    {
        [SerializeField] private MonsterSettings[] _variants;

        private int[] _minDaysCache;
        private float[] _weightsCache;

        public int Count => _variants == null ? 0 : _variants.Length;

        /// <summary>인덱스의 변종. 범위 밖이면 null (호출부가 기본 설정으로 되돌아간다).</summary>
        public MonsterSettings GetVariant(int index)
        {
            if (_variants == null || index < 0 || index >= _variants.Length)
            {
                return null;
            }

            return _variants[index];
        }

        /// <summary>변종별 등장 시작 Day (<see cref="MonsterVariantPicker"/> 입력). 매 스폰 할당을 피해 캐시한다.</summary>
        public int[] GetMinDays()
        {
            EnsureCache();

            return _minDaysCache;
        }

        /// <summary>변종별 추첨 가중치 (<see cref="MonsterVariantPicker"/> 입력).</summary>
        public float[] GetWeights()
        {
            EnsureCache();

            return _weightsCache;
        }

        private void EnsureCache()
        {
            int count = Count;

            if (_minDaysCache == null || _minDaysCache.Length != count)
            {
                _minDaysCache = new int[count];
                _weightsCache = new float[count];
            }

            for (int i = 0; i < count; i++)
            {
                MonsterSettings variant = _variants[i];
                _minDaysCache[i] = variant == null ? int.MaxValue : variant.MinDayToAppear;
                _weightsCache[i] = variant == null ? 0f : variant.SpawnWeight;
            }
        }

        private void OnValidate()
        {
            _minDaysCache = null;
            _weightsCache = null;
        }
    }
}
