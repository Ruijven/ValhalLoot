using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace ValhalLoot
{
    /// <summary>
    /// Custom extension methods for ItemDrop.ItemData to replace missing Jotunn functionality
    /// </summary>
    public static class ItemDataExtensions
    {
        private const string CustomDataPrefix = "CustomData_";
        
        /// <summary>
        /// Tries to get custom data from an item with the specified key
        /// </summary>
        /// <param name="item">The item to get data from</param>
        /// <param name="key">The key to look for</param>
        /// <param name="value">The output value if found</param>
        /// <returns>True if the data was found, false otherwise</returns>
        public static bool TryGetCustomData(this ItemDrop.ItemData item, string key, out string value)
        {
            value = string.Empty; // Initialize with empty string instead of null
            if (item == null || string.IsNullOrEmpty(key))
            {
                return false;
            }
            
            string fullKey = CustomDataPrefix + key;
            if (item.m_customData != null && item.m_customData.TryGetValue(fullKey, out string tempValue))
            {
                value = tempValue ?? string.Empty; // Ensure value is never null
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Gets or creates custom data for an item with the specified key
        /// </summary>
        /// <param name="item">The item to get or create data for</param>
        /// <param name="key">The key to look for or create</param>
        /// <returns>The custom data dictionary</returns>
        public static Dictionary<string, string> GetOrCreateCustomData(this ItemDrop.ItemData item, string key)
        {
            if (item == null)
            {
                return new Dictionary<string, string>();
            }
            
            if (item.m_customData == null)
            {
                item.m_customData = new Dictionary<string, string>();
            }
            
            string fullKey = CustomDataPrefix + key;
            if (!item.m_customData.ContainsKey(fullKey))
            {
                item.m_customData[fullKey] = "{}";
            }
            
            return item.m_customData;
        }
    }
}
