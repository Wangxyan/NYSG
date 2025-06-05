using System;
using System.Collections.Generic;
using UnityEngine; // Required for ItemDisplayState if it's part of InventoryItem

// If ItemDisplayState is defined inside InventoryItem, we might need a more direct reference
// or ensure InventoryItem itself is accessible or its enum is defined globally.
// For now, assuming ItemDisplayState is accessible or we'll use an int.
// Let's assume InventoryItem.ItemDisplayState is accessible.
// If not, you might need to define a similar enum here or use int.

[Serializable]
public class PersistedItemInfo
{
    public string gridId; // Identifier for the ItemGrid this item belongs to
    public int itemId;    // JsonItemData.Id
    public int positionX;
    public int positionY;
    public bool isRotated;
    public InventoryItem.ItemDisplayState displayState; // Assuming InventoryItem.ItemDisplayState is accessible

    // Constructor for convenience
    public PersistedItemInfo(string gridId, int itemId, int posX, int posY, bool rotated, InventoryItem.ItemDisplayState state)
    {
        this.gridId = gridId;
        this.itemId = itemId;
        this.positionX = posX;
        this.positionY = posY;
        this.isRotated = rotated;
        this.displayState = state;
    }
}

[Serializable]
public class PersistentPlayerAttributes
{
    public int charm;
    public int knowledge;
    public int talent;
    public int wealth;
    // Add any other player-specific attributes here

    public PersistentPlayerAttributes()
    {
        charm = 0;
        knowledge = 0;
        talent = 0;
        wealth = 0;
    }

    public PersistentPlayerAttributes(int charm, int knowledge, int talent, int wealth)
    {
        this.charm = charm;
        this.knowledge = knowledge;
        this.talent = talent;
        this.wealth = wealth;
    }
} 