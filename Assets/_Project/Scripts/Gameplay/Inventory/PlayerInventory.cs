using Game.Core.Events;
using Unity.Netcode;

namespace Game.Gameplay.Inventory
{
    /// <summary>
    /// 개인 인벤토리 — 호스트 권위 (기획서 §3.4, 네트워크 문서 §4: 개인 소유물이라도 증감 확정은 호스트).
    /// M2는 자원 1종이므로 단일 카운트 NetworkVariable로 동기화하고, 슬롯 표시는 HUD가
    /// <see cref="InventoryMath.GetSlotFill"/>로 유도한다. Player 프리팹에 부착한다.
    /// </summary>
    public sealed class PlayerInventory : NetworkBehaviour, IResourceInventory
    {
        [UnityEngine.SerializeField] private InventorySettings _settings;

        private readonly NetworkVariable<int> _count = new NetworkVariable<int>();

        public int Count => _count.Value;

        public int Capacity => _settings != null ? _settings.Capacity : 0;

        public bool IsFull => Count >= Capacity;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _count.Value = 0;
            }

            _count.OnValueChanged += OnCountChanged;
        }

        public override void OnNetworkDespawn()
        {
            _count.OnValueChanged -= OnCountChanged;
        }

        public bool ServerTryAdd(int amount)
        {
            if (!IsServer || _settings == null || !InventoryMath.CanAdd(_count.Value, amount, Capacity))
            {
                return false;
            }

            _count.Value += amount;
            return true;
        }

        public bool ServerTryRemove(int amount)
        {
            if (!IsServer || !InventoryMath.CanRemove(_count.Value, amount))
            {
                return false;
            }

            _count.Value -= amount;
            return true;
        }

        private void OnCountChanged(int previous, int current)
        {
            EventBus<InventoryChangedEvent>.Publish(new InventoryChangedEvent(
                OwnerClientId, IsOwner, current, Capacity,
                _settings != null ? _settings.SlotCount : 0,
                _settings != null ? _settings.StackSize : 1));
        }
    }
}
