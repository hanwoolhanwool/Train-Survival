using System;
using UnityEngine;

namespace Game.Gameplay.Inventory
{
    /// <summary>손 점유 — 그 무기를 드는 데 몇 손이 필요한가 (기획서 §3.1 집게 파지 전환의 판정 축).</summary>
    public enum WeaponHandedness : byte
    {
        /// <summary>한손 — 2단계 이후 왼손에 집게를 든 채로 오른손에 함께 들 수 있다.</summary>
        One = 0,

        /// <summary>양손 — 두 손이 다 필요하다. 그랩을 유지한 채로는 들 수 없어 전환 시 그랩이 풀린다.</summary>
        Two = 1
    }

    /// <summary>
    /// 무기 손 점유 판정 데이터 (집게 단계별 파지 계획 §3.2 확정 ③) — "이 무기가 양손인가"만 정한다.
    /// <para>
    /// <b>표현용 파지 데이터(WeaponHoldSettings)와 값이 겹치지만 출처는 일부러 분리한다.</b>
    /// 파지 IK를 손보다가 게임 규칙(그랩 유지 중 무기 전환)이 따라 바뀌는 사고를 막기 위해서다.
    /// 두 에셋의 값이 어긋나지 않는지는 파지 시스템이 같은 브랜치에 모인 뒤 EditMode 테스트로 잡는다 (§6.3).
    /// </para>
    /// 미등재 종류는 <see cref="WeaponHandedness.One"/>으로 본다 — 등재를 잊어도 기존 조작이 그대로 성립하고,
    /// 양손으로 만들 무기만 명시적으로 적어 넣게 된다 (안전한 쪽이 기본값).
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponHandednessSettings", menuName = "Game/Weapon Handedness Settings")]
    public sealed class WeaponHandednessSettings : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private HotbarItemType _item;

            [SerializeField] private WeaponHandedness _handedness;

            public HotbarItemType Item => _item;

            public WeaponHandedness Handedness => _handedness;
        }

        [Tooltip("양손 무기만 적어 넣으면 된다 — 미등재는 전부 한손으로 본다.")]
        [SerializeField] private Entry[] _entries;

        /// <summary>이 종류의 손 점유. 미등재·None은 한손.</summary>
        public WeaponHandedness GetHandedness(HotbarItemType item)
        {
            Entry entry = Find(item);
            return entry != null ? entry.Handedness : WeaponHandedness.One;
        }

        /// <summary>양손 무기인가 — 그랩 유지 중 전환 판정(<c>HarpoonSwitchRules</c>)의 유일한 입력이다.</summary>
        public bool IsTwoHanded(HotbarItemType item)
        {
            return GetHandedness(item) == WeaponHandedness.Two;
        }

        private Entry Find(HotbarItemType item)
        {
            if (_entries == null || item == HotbarItemType.None)
            {
                return null;
            }

            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i] != null && _entries[i].Item == item)
                {
                    return _entries[i];
                }
            }

            return null;
        }
    }
}
