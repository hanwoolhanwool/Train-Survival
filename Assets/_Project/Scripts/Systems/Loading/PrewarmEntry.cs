using System;
using UnityEngine;

namespace Game.Systems.Loading
{
    /// <summary>
    /// "이 프리팹을 몇 개" — 인스펙터에서 손으로 적는 프리웜 한 줄.
    ///
    /// <para>계산으로 나오는 수량(지형 타일 등)은 여기 적지 않는다. 이건 <b>계산할 근거가 없어
    /// 감으로 정하는 것</b>들의 자리다 — 첫 전투 프레임의 탄착 효과처럼.</para>
    /// </summary>
    [Serializable]
    public struct PrewarmEntry
    {
        [SerializeField]
        [Tooltip("미리 만들어 둘 프리팹. 실제로 쓰이는 것과 같은 에셋이어야 풀이 히트한다.")]
        private GameObject _prefab;

        [SerializeField, Min(0)]
        [Tooltip("개수.")]
        private int _count;

        public GameObject Prefab => _prefab;

        public int Count => _count;
    }
}
