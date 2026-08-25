using UnityEngine;

namespace Game.UI.Loading
{
    /// <summary>
    /// 로딩 중에 읽을 한 줄 —
    /// [인게임 진입 로딩 구현 계획](docs/plans/features/인게임-진입-로딩-구현-계획.md) §8.5.
    ///
    /// <para><b>게임의 규칙을 알려 주는 것만 넣는다.</b> 분위기 문구는 재미없어지는 속도가
    /// 빠르다 — 두 번째 로딩부터는 읽지 않게 되고, 그러면 팁 자리는 그냥 빈 줄이다.</para>
    ///
    /// <para><b>이미 구현된 규칙만 적는다.</b> 아직 없는 규칙을 적으면 플레이어가 그것을
    /// 찾다가 없다는 것을 알게 된다 — 없는 기능을 광고하는 셈이다.</para>
    ///
    /// <para><b>로딩당 하나이고 바뀌지 않는다</b>(§8.3) — 읽던 문장이 사라지는 것이
    /// 안 읽히는 것보다 나쁘다.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "LoadingTipCatalog", menuName = "Game/Loading Tip Catalog")]
    public sealed class LoadingTipCatalog : ScriptableObject
    {
        [SerializeField]
        [TextArea(1, 3)]
        [Tooltip("한 줄에 규칙 하나. 이미 구현된 것만 적는다.")]
        private string[] _tips;

        public int Count => _tips == null ? 0 : _tips.Length;

        /// <summary>인덱스의 문구. 범위 밖이면 빈 문자열.</summary>
        public string Get(int index)
        {
            if (_tips == null || index < 0 || index >= _tips.Length)
            {
                return string.Empty;
            }

            return _tips[index] ?? string.Empty;
        }

        /// <summary>
        /// 다음에 보여 줄 문구를 고른다 — <b>직전 것은 피한다.</b> 같은 팁이 연달아 나오면
        /// 목록이 하나뿐인 것처럼 보인다.
        ///
        /// <para>순수 함수라 난수를 밖에서 받는다(<paramref name="roll"/>은 0~1).
        /// 후보가 하나뿐이면 그것을 그대로 돌려준다 — 피할 곳이 없다.</para>
        /// </summary>
        public static int PickIndex(int count, int previous, float roll)
        {
            if (count <= 0)
            {
                return -1;
            }

            if (count == 1)
            {
                return 0;
            }

            int picked = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(roll) * count), 0, count - 1);
            if (picked != previous)
            {
                return picked;
            }

            // 직전과 같으면 한 칸 민다 — 분포가 아주 조금 기울지만 연속 반복은 사라진다.
            return (picked + 1) % count;
        }
    }
}
