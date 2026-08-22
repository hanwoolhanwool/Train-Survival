using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 랜드마크·유적이 놓일 자리의 희소도 (레벨 디자인 가이드 §4.4).
    /// 흔한 자리에 진귀한 것을 놓으면 "지나쳤음을 깨닫게" 하는 힘이 사라진다.
    /// </summary>
    public enum LandmarkRarity : byte
    {
        /// <summary>흔한 자리 — 이정표·신호기 같은 낮은 흔적.</summary>
        Common = 0,

        /// <summary>드문 자리 — 폐선로 분기·급수탑처럼 눈에 띄는 것.</summary>
        Uncommon = 1,

        /// <summary>진귀한 자리 — 유적 입구·난파 열차. 팔레트 이벤트형이 쓴다.</summary>
        Rare = 2,
    }

    /// <summary>
    /// 세그먼트가 제공하는 랜드마크 배치 지점 (레벨 디자인 가이드 §4.4) — 타일당 0~1개.
    /// <see cref="ResourceAnchor"/>와 같은 규약을 쓴다: 빈 마커 + 정적 레지스트리 + 사용 플래그.
    ///
    /// <para>소비자(랜드마크·유적 배치기)는 계획 3차에서 붙는다. 지금은 자리를 저작할 수 있게
    /// 마커와 조회만 세워 둔다 — 2차에 만드는 세그먼트가 나중에 다시 열리지 않도록.</para>
    /// </summary>
    public sealed class LandmarkSlot : MonoBehaviour
    {
        [Tooltip("이 자리에 어울리는 희소도 — 배치기가 무엇을 놓을지 고르는 입력.")]
        [SerializeField] private LandmarkRarity _rarity = LandmarkRarity.Common;

        // 활성 슬롯 레지스트리 — 타일이 풀에서 켜지고 꺼질 때마다 갱신된다.
        private static readonly List<LandmarkSlot> ActiveSlots = new List<LandmarkSlot>(16);

        public LandmarkRarity Rarity => _rarity;

        /// <summary>이번 활성 구간에서 이미 무언가 놓인 자리인지. 타일이 재사용되면 리셋된다.</summary>
        public bool IsUsed { get; private set; }

        public static IReadOnlyList<LandmarkSlot> Active => ActiveSlots;

        /// <summary>테스트 전용 — 정적 레지스트리를 비운다 (EditMode TearDown 규약).</summary>
        public static void ClearRegistry()
        {
            ActiveSlots.Clear();
        }

        public void MarkUsed()
        {
            IsUsed = true;
        }

        /// <summary>
        /// 희소도가 맞는 미사용 자리를 고른다. 없으면 null — 호출자는 이번 타일을 그냥 지나친다.
        /// 랜드마크는 <b>없어도 되는 것</b>이라 폴백이 필요 없다(자원과 다른 점).
        /// </summary>
        /// <param name="rarity">요구 희소도. 이보다 흔한 자리에는 놓지 않는다.</param>
        public static LandmarkSlot TryPick(LandmarkRarity rarity)
        {
            for (int i = 0; i < ActiveSlots.Count; i++)
            {
                LandmarkSlot slot = ActiveSlots[i];
                if (slot == null || slot.IsUsed)
                {
                    continue;
                }

                if (IsEligible(slot.Rarity, rarity))
                {
                    return slot;
                }
            }

            return null;
        }

        /// <summary>
        /// 자리의 희소도가 요구 희소도를 감당하는가 — 순수 판정.
        /// 진귀한 것은 진귀한 자리에만, 흔한 것은 아무 자리에나 놓을 수 있다.
        /// </summary>
        public static bool IsEligible(LandmarkRarity slotRarity, LandmarkRarity required)
        {
            return slotRarity >= required;
        }

        private void OnEnable()
        {
            // 풀에서 다시 켜진 타일의 자리는 "아직 안 쓴 자리"로 돌아간다 —
            // 리셋을 빠뜨리면 재사용 타일에 랜드마크가 영원히 안 놓인다.
            IsUsed = false;
            ActiveSlots.Add(this);
        }

        private void OnDisable()
        {
            ActiveSlots.Remove(this);
        }
    }
}
