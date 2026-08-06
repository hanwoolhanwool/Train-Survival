using Unity.Netcode;

namespace Game.Gameplay.Inventory
{
    /// <summary>
    /// 인벤토리 슬롯의 네트워크 직렬화 표현 — 개인 핫바(<see cref="PlayerInventory"/>)와
    /// 공유 창고가 같은 표현을 쓴다(무기·장비도 슬롯 하나로 담긴다). 외부에는
    /// <see cref="HotbarSlotView"/>로만 노출한다.
    /// </summary>
    public struct NetworkSlot : INetworkSerializable, System.IEquatable<NetworkSlot>
    {
        public HotbarItemType ItemType;
        public byte Count;
        public ResourceType Resource;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ItemType);
            serializer.SerializeValue(ref Count);
            serializer.SerializeValue(ref Resource);
        }

        public bool Equals(NetworkSlot other)
        {
            return ItemType == other.ItemType && Count == other.Count && Resource == other.Resource;
        }
    }
}
