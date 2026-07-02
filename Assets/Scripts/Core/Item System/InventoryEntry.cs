using System;
using Unity.Netcode;

namespace SpookyGame 
{
    // Structs are value types. They allocate on the stack, meaning zero GC overhead.
    // INetworkSerializable allows NGO to sync this data efficiently.
    [Serializable]
    public struct InventoryEntry : INetworkSerializable, IEquatable<InventoryEntry>
    {
        public uint ItemID;      // A hash of the item's name/GUID for lookup
        public int StackSize;

        public bool IsEmpty => ItemID == 0 || StackSize <= 0;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ItemID);
            serializer.SerializeValue(ref StackSize);
        }

        public bool Equals(InventoryEntry other)
        {
            return ItemID == other.ItemID && StackSize == other.StackSize;
        }
    }
}
