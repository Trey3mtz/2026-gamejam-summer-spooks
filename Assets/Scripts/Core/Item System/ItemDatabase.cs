using System.Collections.Generic;
using UnityEngine;

namespace SpookyGame.Core.Item_System
{
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "SpookyGame/Systems/Create Item Database")]
    public class ItemDatabase : ScriptableObject
    {
        [SerializeReference]
        public List<ItemDefinition> Entries; 
        private Dictionary<uint, ItemDefinition> m_LookupDictionnary;
        
        public ItemDefinition GetFromID(uint uniqueID)
        {
            if (m_LookupDictionnary.TryGetValue(uniqueID, out var entry))
            {
                return entry;
            }

            return null;
        }
        
        // This need to be called by whoever use this database to rebuild the lookup.
        // Used to use OnAfterDeserialize but we cannot control the order of deserialization, and Item could be
        // deserialized AFTER the database is, so the unique ID for that item was not ready yet.
        public void Init()
        {
            m_LookupDictionnary = new Dictionary<uint, ItemDefinition>();

            //rebuild the lookup
            foreach (var entry in Entries)
            {
                if (entry == null)
                {
                    continue;
                }
                
                m_LookupDictionnary.TryAdd(entry.ItemID, entry);
            }
        }
    }
}