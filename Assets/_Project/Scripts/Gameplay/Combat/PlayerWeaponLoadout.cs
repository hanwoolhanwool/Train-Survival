using Game.Core.Events;
using Game.Gameplay.Harpoon;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// 무기 슬롯 전환 — 1 = 집게, 2 = 리볼버 (기획서 §6.2 — 집게도 무기 슬롯 사용).
    /// 두 무기가 같은 좌클릭 입력을 쓰므로, 활성 슬롯에만 입력 게이트를 연다.
    /// 전환은 소유자 로컬 결정이며 판정 권위와 무관하다.
    /// </summary>
    public sealed class PlayerWeaponLoadout : NetworkBehaviour
    {
        [SerializeField] private HarpoonController _harpoon;
        [SerializeField] private RevolverController _revolver;

        private WeaponSlot _currentSlot;

        public WeaponSlot CurrentSlot => _currentSlot;

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                Select(WeaponSlot.Harpoon);
            }
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                Select(WeaponSlot.Harpoon);
            }
            else if (keyboard.digit2Key.wasPressedThisFrame)
            {
                Select(WeaponSlot.Revolver);
            }
        }

        private void Select(WeaponSlot slot)
        {
            _currentSlot = slot;

            if (_harpoon != null)
            {
                _harpoon.InputEnabled = slot == WeaponSlot.Harpoon;
            }

            if (_revolver != null)
            {
                _revolver.InputEnabled = slot == WeaponSlot.Revolver;
            }

            EventBus<WeaponSelectedLocalEvent>.Publish(new WeaponSelectedLocalEvent(slot));
        }
    }
}
