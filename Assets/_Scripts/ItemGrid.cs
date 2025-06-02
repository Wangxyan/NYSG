using UnityEngine;
using System.Collections.Generic; // Required for List

/// <summary>
/// 处理物品网格逻辑相关
/// </summary>
public class ItemGrid : MonoBehaviour
{
    /// <summary>
    /// 瓦片的宽度
    /// </summary>
    public const float tileSizeWidth = 32;

    /// <summary>
    /// 瓦片的高度
    /// </summary>
    public const float tileSizeHeight = 32;

    /// <summary>
    /// 库存物品格子数组
    /// </summary>
    private InventoryItem[,] inventoryItemSlot;

    /// <summary>
    /// 矩形变换组件
    /// </summary>
    private RectTransform rectTransform;

    /// <summary>
    /// 网格宽度
    /// </summary>
    [SerializeField] private int gridSizeWidth = 20;

    /// <summary>
    /// 网格高度
    /// </summary>
    [SerializeField] private int gridSizeHeight = 10;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    private void Start()
    {
        Init(gridSizeWidth, gridSizeHeight);
    }

    /// <summary>
    /// 初始化库存网格
    /// </summary>
    /// <param name="width">网格宽度</param>
    /// <param name="height">网格高度</param>
    public void Init(int width, int height)
    {
        this.gridSizeWidth = width;
        this.gridSizeHeight = height;

        inventoryItemSlot = new InventoryItem[width, height];
        Vector2 size = new Vector2(width * tileSizeWidth, height * tileSizeHeight);
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        rectTransform.sizeDelta = size;
        Debug.Log($"[ItemGrid.Init] Initialized grid with W:{width}, H:{height}. RectTransform size set to {size}");
    }

    /// <summary>
    /// 获得瓦片在网格上的位置
    /// </summary>
    /// <param name="mousePosition">鼠标位置</param>
    /// <returns>瓦片坐标, (-1,-1) if outside or error</returns>
    public Vector2Int GetTileGridPosition(Vector2 mousePosition)
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                Debug.LogError("[ItemGrid.GetTileGridPosition] rectTransform is null and GetComponent failed!");
                return new Vector2Int(-1, -1);
            }
        }

        Vector2 localPoint;
        Canvas canvas = GetComponentInParent<Canvas>();
        Camera eventCamera = null;
        if (canvas != null)
        {
            eventCamera = (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : (canvas.worldCamera ?? Camera.main);
        }
        else
        {
            eventCamera = Camera.main;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, mousePosition, eventCamera, out localPoint))
        {
            float pivotCorrectedX = localPoint.x + rectTransform.pivot.x * rectTransform.rect.width;
            float pivotCorrectedY = (rectTransform.rect.height * (1 - rectTransform.pivot.y)) - localPoint.y;
            
            // Calculate grid position based on constant tile sizes
            Vector2Int gridPos = new Vector2Int(
                Mathf.FloorToInt(pivotCorrectedX / tileSizeWidth),
                Mathf.FloorToInt(pivotCorrectedY / tileSizeHeight)
            );
            return gridPos;
        }
        return new Vector2Int(-1, -1); // Mouse is outside or camera issue
    }

    public Vector2 GetEffectiveTileSize()
    {
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null) {
             Debug.LogError("[ItemGrid.GetEffectiveTileSize] rectTransform is null!");
             return new Vector2(tileSizeWidth, tileSizeHeight); // Fallback
        }
        // Effective size should account for canvas scaling, not just local scale of the grid itself if that's not 1,1,1
        // However, for ScreenSpaceOverlay, localScale is typically what matters for visual pixel size of children.
        // If tiles are direct children and grid has a scale, this works.
        // If a more complex hierarchy or different render modes, this might need adjustment.
        Vector3 currentScale = rectTransform.lossyScale; // Using lossyScale for a more global scale factor
        // This assumes tileSizeWidth/Height are defined in a space where this lossyScale is meaningful.
        // Typically, for UI, you design at a reference resolution and let Canvas Scaler handle adjustments.
        // The tileSize constants are likely in "UI pixels" at the reference.
        // Let's assume for now tileSizeWidth/Height are the "final" pixel sizes desired IF scale is 1.
        // This might need to be re-evaluated depending on Canvas setup.
        // For simplicity, if we assume the grid cells are scaled by the ItemGrid's transform:
        currentScale = rectTransform.localScale; // Reverting to localScale as it's more direct for UI element children
        float effX = tileSizeWidth * currentScale.x;
        float effY = tileSizeHeight * currentScale.y;
        return new Vector2(effX > 0 ? effX : tileSizeWidth, effY > 0 ? effY : tileSizeHeight);
    }

    /// <summary>
    /// 获取指定矩形区域内的所有物品 (确保唯一性)
    /// </summary>
    /// <param name="area">要检查的矩形区域 (grid coordinates)</param>
    /// <param name="outOverlappingItems">用于存储找到的重叠物品的列表 (会被清空)</param>
    public void GetItemsInRect(RectInt area, List<InventoryItem> outOverlappingItems)
    {
        outOverlappingItems.Clear();
        if (inventoryItemSlot == null) {
            Debug.LogError("[ItemGrid.GetItemsInRect] inventoryItemSlot is not initialized!");
            return;
        }

        // Clamp the search area to the grid boundaries to avoid out-of-bounds access
        // and to correctly iterate only over valid cells.
        int startX = Mathf.Max(area.x, 0);
        int startY = Mathf.Max(area.y, 0);
        int endX = Mathf.Min(area.x + area.width, gridSizeWidth);
        int endY = Mathf.Min(area.y + area.height, gridSizeHeight);

        for (int x = startX; x < endX; x++)
        {
            for (int y = startY; y < endY; y++)
            {
                // No need for additional boundary checks here as loops are clamped
                InventoryItem itemInSlot = inventoryItemSlot[x, y];
                if (itemInSlot != null && !outOverlappingItems.Contains(itemInSlot))
                {
                    outOverlappingItems.Add(itemInSlot);
                }
            }
        }
    }
    
    /// <summary>
    /// 将物品放置到指定位置上。如果该位置已有物品，则这些物品会被清除引用并作为 displacedItemsOutput 返回。
    /// </summary>
    /// <param name="itemToPlace">要放置的物品</param>
    /// <param name="posX">目标位置X轴</param>
    /// <param name="posY">目标位置Y轴</param>
    /// <param name="displacedItemsOutput">被新物品挤占而移除的物品列表 (会被清空并填充)</param>
    /// <returns>如果物品成功放置则返回true，否则false (例如边界检查失败)</returns>
    public bool PlaceItem(InventoryItem itemToPlace, int posX, int posY, List<InventoryItem> displacedItemsOutput)
    {
        displacedItemsOutput.Clear();

        if (itemToPlace == null)
        {
            Debug.LogError("[ItemGrid.PlaceItem] itemToPlace is null.");
            return false;
        }
        if (inventoryItemSlot == null) {
            Debug.LogError("[ItemGrid.PlaceItem] inventoryItemSlot is not initialized!");
            return false;
        }


        if (!BoundryCheck(posX, posY, itemToPlace.WIDTH, itemToPlace.HEIGHT))
        {
            Debug.LogWarning($"[ItemGrid.PlaceItem] Boundary check failed for item {itemToPlace.jsonData.Name} at ({posX},{posY}) with size ({itemToPlace.WIDTH},{itemToPlace.HEIGHT}). Grid size: ({gridSizeWidth},{gridSizeHeight})");
            return false;
        }

        RectInt placementArea = new RectInt(posX, posY, itemToPlace.WIDTH, itemToPlace.HEIGHT);
        GetItemsInRect(placementArea, displacedItemsOutput);

        // Critical: Remove the item being placed from the displaced list IF it was already there.
        // This handles the case where an item is picked up and placed back into the same spot.
        // It would be found by GetItemsInRect, but it's not being "displaced" by itself in a way that requires it to be moved.
        bool selfDisplacement = displacedItemsOutput.Remove(itemToPlace);
        if(selfDisplacement){
            Debug.Log($"[ItemGrid.PlaceItem] Item {itemToPlace.jsonData.Name} was already in the target location. It's being re-placed.");
        }

        foreach (InventoryItem displacedItem in displacedItemsOutput)
        {
            // Ensure we are not trying to displace the item we are about to place if somehow it's still in the list
            // (though Remove above should handle the typical self-overlap case).
            if (displacedItem == itemToPlace) continue; 
            
            Debug.Log($"[ItemGrid.PlaceItem] Displacing pre-existing item: {displacedItem.jsonData.Name} which was at {displacedItem.onGridPositionX},{displacedItem.onGridPositionY}");
            ClearGridReference(displacedItem); // Clear its reference from the grid
        }
        // At this point, displacedItemsOutput contains items that were in the way (excluding itemToPlace itself) and have been cleared from the grid.

        // Place the new item
        RectTransform itemRectTransform = itemToPlace.GetComponent<RectTransform>();
        itemRectTransform.SetParent(this.rectTransform);

        for (int x = 0; x < itemToPlace.WIDTH; x++)
        {
            for (int y = 0; y < itemToPlace.HEIGHT; y++)
            {
                // Boundary check for writing, though BoundryCheck for the item overall should cover this.
                if ((posX + x < gridSizeWidth) && (posY + y < gridSizeHeight) && (posX +x >=0) && (posY+y >=0)) {
                     inventoryItemSlot[posX + x, posY + y] = itemToPlace;
                } else {
                    Debug.LogError($"[ItemGrid.PlaceItem] Critical error: Attempted to write item {itemToPlace.jsonData.Name} to slot ({posX+x},{posY+y}) which is out of bounds during placement loop. This should have been caught by BoundryCheck.");
                }
            }
        }

        itemToPlace.onGridPositionX = posX;
        itemToPlace.onGridPositionY = posY;

        Vector2 localItemPos = CalculatePositionOnGrid(itemToPlace, posX, posY);
        itemRectTransform.localPosition = localItemPos;
        
        Debug.Log($"[ItemGrid.PlaceItem] Successfully placed item {itemToPlace.jsonData.Name} at ({posX},{posY}). Displaced {displacedItemsOutput.Count} other items.");
        return true;
    }

    /// <summary>
    /// Legacy PlaceItem overload. Destroys any displaced items.
    /// </summary>
    public void PlaceItem(InventoryItem item, int posX, int posY)
    {
        List<InventoryItem> displaced = new List<InventoryItem>();
        PlaceItem(item, posX, posY, displaced); 
        if(displaced.Count > 0){
            Debug.LogWarning($"[ItemGrid.PlaceItem_Legacy_Void] {displaced.Count} items were displaced by {item.jsonData.Name} and are being DESTROYED as this legacy overload does not handle them.");
            foreach(var ditem in displaced) {
                if (ditem != null && ditem.gameObject != null) Destroy(ditem.gameObject); 
            }
        }
    }

    /// <summary>
    /// Legacy PlaceItem overload with ref overlapItem.
    /// Returns first displaced item as overlapItem, destroys others.
    /// </summary>
    public bool PlaceItem(InventoryItem item, int posX, int posY, ref InventoryItem overlapItem)
    {
        List<InventoryItem> displacedItems = new List<InventoryItem>();
        bool success = PlaceItem(item, posX, posY, displacedItems); // Calls the new main PlaceItem

        overlapItem = null; // Default to no overlap
        if (success)
        {
            if (displacedItems.Count > 0)
            {
                overlapItem = displacedItems[0]; // Return the first one
                Debug.LogWarning($"[ItemGrid.PlaceItem_Legacy_Ref] Item {item.jsonData.Name} displaced {displacedItems.Count} items. First one ({overlapItem.jsonData.Name}) returned as overlap. Others being DESTROYED.");
                for(int i = 1; i < displacedItems.Count; i++) {
                    if (displacedItems[i] != null && displacedItems[i].gameObject != null) {
                        Debug.Log($"[ItemGrid.PlaceItem_Legacy_Ref] Destroying extra displaced item: {displacedItems[i].jsonData.Name}");
                        Destroy(displacedItems[i].gameObject);
                    }
                }
            }
        }
        return success;
    }

    public Vector2 CalculatePositionOnGrid(InventoryItem item, int posX, int posY)
    {
        // Position is usually center of the item relative to grid's top-left.
        // If pivot of item is center (0.5, 0.5), then this calculation is fine.
        // UI Y typically increases upwards, grid Y increases downwards.
        // tileSize constants are pixel dimensions.
        float finalX = (posX * tileSizeWidth) + (item.WIDTH * tileSizeWidth * 0.5f);
        // For Y: grid 0,0 is top-left. UI local 0,0 for child is often bottom-left of parent rect if pivot is 0,0 for parent.
        // If parent (ItemGrid) pivot is top-left (0,1), local Y is negative downwards.
        // If parent pivot is center (0.5,0.5), calculations are relative to center.
        // Let's assume UI coordinates where Y is positive upwards.
        // We want item's top-left corner to be at (posX * tileW, posY * tileH) from grid's top-left.
        // Then adjust for item's pivot. If item pivot is (0.5, 0.5):
        // Item's localPosition should be (posX * tileW + itemVisualWidth*0.5, -(posY * tileH + itemVisualHeight*0.5) )
        // if the ItemGrid's RectTransform has its pivot at top-left (0,1).
        // The current formula seems to assume pivot at center of the ItemGrid rect for its children,
        // or a coordinate system where Y is inverted for localPosition.
        // Let's stick to the original calculation if it worked visually.
        // Origin (0,0) for localPosition is pivot of parent. If parent pivot is (0,1) [TopLeft]:
        // X increases to the right. Y decreases downwards.
        // To place item's center:
        // localX = (posX * tileSizeWidth) + (item.WIDTH * tileSizeWidth / 2)
        // localY = -( (posY * tileSizeHeight) + (item.HEIGHT * tileSizeHeight / 2) )
        // This seems correct for a TopLeft pivot on the ItemGrid.
        Vector2 position = new Vector2
        {
            x = posX * tileSizeWidth + tileSizeWidth * item.WIDTH / 2f,
            y = -(posY * tileSizeHeight + tileSizeHeight * item.HEIGHT / 2f)
        };
        return position;
    }

    /// <summary>
    /// Checks if an item overlaps with existing items in the grid.
    /// This is largely superseded by GetItemsInRect, but kept if specific single-overlap check is needed.
    /// The 'ref InventoryItem overlapItem' part makes it tricky.
    /// Better to use GetItemsInRect and check the count/content of the returned list.
    /// </summary>
    private bool OverlapCheck(int posX, int posY, int width, int height, ref InventoryItem overlapItem)
    {
        if (inventoryItemSlot == null) return false; // Grid not initialized

        List<InventoryItem> itemsInArea = new List<InventoryItem>();
        GetItemsInRect(new RectInt(posX, posY, width, height), itemsInArea);

        if (itemsInArea.Count == 0)
        {
            overlapItem = null;
            return true; // No overlap, clear to place
        }
        
        // Original OverlapCheck logic:
        // if it's a single item, it's a valid "overlap" to be replaced.
        // if multiple distinct items, it's an invalid placement (can't bridge multiple items).
        // This interpretation might be from the old PlaceItem logic.
        if (itemsInArea.Count == 1)
        {
            overlapItem = itemsInArea[0];
            return true; // Overlaps with a single item
        }
        else // Multiple distinct items in the area
        {
            Debug.LogWarning($"[ItemGrid.OverlapCheck] Placement at ({posX},{posY}) overlaps with multiple distinct items ({itemsInArea.Count}). Invalid placement by old rule.");
            overlapItem = itemsInArea[0]; // Still return first one for legacy ref
            return false; // Invalid placement by old rule if it spans multiple items
        }
    }

    /// <summary>
    /// Checks if a specific slot on the grid is occupied.
    /// Consider using GetItem(x,y) != null instead.
    /// </summary>
    private bool CheckAvailableSpace(int posX, int posY, int width, int height)
    {
        if (inventoryItemSlot == null) return true; // Or false, depends on interpretation if grid not ready

        List<InventoryItem> itemsInArea = new List<InventoryItem>();
        GetItemsInRect(new RectInt(posX, posY, width, height), itemsInArea);
        return itemsInArea.Count == 0; // Space is available if no items are in the rect
    }

    public InventoryItem PickUpItem(int x, int y)
    {
        if (!PositionCheck(x, y)) { // PositionCheck validates against gridSize
            Debug.LogWarning($"[ItemGrid.PickUpItem] Attempted to pick up from invalid position ({x},{y}). Grid size is ({gridSizeWidth},{gridSizeHeight}).");
            return null;
        }
        if (inventoryItemSlot == null) {
             Debug.LogError("[ItemGrid.PickUpItem] inventoryItemSlot is not initialized!");
             return null;
        }

        InventoryItem itemToPickUp = inventoryItemSlot[x, y];
        if (itemToPickUp == null)
        {
            // Debug.Log($"[ItemGrid.PickUpItem] No item at ({x},{y}) to pick up.");
            return null;
        }
        
        Debug.Log($"[ItemGrid.PickUpItem] Picking up {itemToPickUp.jsonData.Name} starting from its registered cell ({itemToPickUp.onGridPositionX},{itemToPickUp.onGridPositionY}), actual click on ({x},{y})");
        ClearGridReference(itemToPickUp); // Clear all cells occupied by this item
        return itemToPickUp;
    }

    public void ClearGridReference(InventoryItem item)
    {
        if (item == null) {
            // Debug.LogWarning("[ItemGrid.ClearGridReference] Attempted to clear a null item.");
            return;
        }
        if (inventoryItemSlot == null) {
             Debug.LogError("[ItemGrid.ClearGridReference] inventoryItemSlot is not initialized!");
             return;
        }

        // Use the item's stored onGridPositionX/Y and its WIDTH/HEIGHT
        // to know which cells it occupied.
        for (int ix = 0; ix < item.WIDTH; ix++)
        {
            for (int iy = 0; iy < item.HEIGHT; iy++)
            {
                int currentSlotX = item.onGridPositionX + ix;
                int currentSlotY = item.onGridPositionY + iy;

                if (currentSlotX >= 0 && currentSlotX < gridSizeWidth &&
                    currentSlotY >= 0 && currentSlotY < gridSizeHeight)
                {
                    if (inventoryItemSlot[currentSlotX, currentSlotY] == item)
                    {
                        inventoryItemSlot[currentSlotX, currentSlotY] = null;
                    }
                    else if (inventoryItemSlot[currentSlotX, currentSlotY] != null)
                    {
                        Debug.LogWarning($"[ItemGrid.ClearGridReference] Slot ({currentSlotX},{currentSlotY}) was expected to hold {item.jsonData.Name} (or be part of it) but holds {inventoryItemSlot[currentSlotX, currentSlotY].jsonData.Name} or was already null when clearing {item.jsonData.Name}. This might be an overlapping clear or stale data.");
                    }
                }
                else {
                    Debug.LogWarning($"[ItemGrid.ClearGridReference] While clearing {item.jsonData.Name}, calculated slot ({currentSlotX},{currentSlotY}) was out of grid bounds ({gridSizeWidth}x{gridSizeHeight}). Item's stored pos: ({item.onGridPositionX},{item.onGridPositionY}), size: ({item.WIDTH}x{item.HEIGHT}).");
                }
            }
        }
        // Debug.Log($"[ItemGrid.ClearGridReference] Cleared references for item {item.jsonData.Name} which was at ({item.onGridPositionX},{item.onGridPositionY})");
    }

    /// <summary>
    /// Checks if the given coordinates are within the grid boundaries.
    /// </summary>
    private bool PositionCheck(int posX, int posY)
    {
        if (posX < 0 || posY < 0 || posX >= gridSizeWidth || posY >= gridSizeHeight)
        {
            return false;
        }
        return true;
    }

    /// <summary>
    /// Checks if an item of given dimensions can be placed at posX, posY without going out of grid boundaries.
    /// </summary>
    public bool BoundryCheck(int posX, int posY, int width, int height)
    {
        if (PositionCheck(posX, posY) == false) { return false; }

        // Check if the item extends beyond the right or bottom edges
        if (posX + width > gridSizeWidth) { return false; }
        if (posY + height > gridSizeHeight) { return false; }

        return true;
    }

    public InventoryItem GetItem(int x, int y)
    {
        if (!PositionCheck(x,y)) {
            // Debug.LogWarning($"[ItemGrid.GetItem] Position ({x},{y}) is out of bounds for grid size ({gridSizeWidth}x{gridSizeHeight}).");
            return null;
        }
        if (inventoryItemSlot == null) {
             Debug.LogError("[ItemGrid.GetItem] inventoryItemSlot is not initialized!");
             return null;
        }
        return inventoryItemSlot[x, y];
    }

    public Vector2Int? FindSpaceForObject(InventoryItem itemToInsert)
    {
        if (inventoryItemSlot == null || itemToInsert == null) return null;

        int width = itemToInsert.WIDTH;
        int height = itemToInsert.HEIGHT;

        // Iterate through all possible top-left positions for the item
        for (int y = 0; y <= gridSizeHeight - height; y++) // Iterate rows
        {
            for (int x = 0; x <= gridSizeWidth - width; x++) // Iterate columns
            {
                // Check if this top-left position (x,y) can accommodate the item
                if (IsAreaClear(x, y, width, height))
                {
                    return new Vector2Int(x, y); // Found a clear space
                }
            }
        }
        return null; // No space found
    }

    /// <summary>
    /// Helper to check if a rectangular area is completely clear of items.
    /// </summary>
    private bool IsAreaClear(int startX, int startY, int width, int height)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (startX + x >= gridSizeWidth || startY + y >= gridSizeHeight || inventoryItemSlot[startX + x, startY + y] != null)
                {
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>
    /// Gets all unique inventory items currently placed on this grid.
    /// </summary>
    /// <returns>A list of unique InventoryItem objects.</returns>
    public List<InventoryItem> GetAllUniqueItems()
    {
        List<InventoryItem> uniqueItems = new List<InventoryItem>();
        if (inventoryItemSlot == null)
        {
            Debug.LogWarning("[ItemGrid.GetAllUniqueItems] inventoryItemSlot is not initialized!");
            return uniqueItems; // Return empty list
        }

        for (int x = 0; x < gridSizeWidth; x++)
        {
            for (int y = 0; y < gridSizeHeight; y++)
            {
                InventoryItem itemInSlot = inventoryItemSlot[x, y];
                if (itemInSlot != null && !uniqueItems.Contains(itemInSlot))
                {
                    uniqueItems.Add(itemInSlot);
                }
            }
        }
        return uniqueItems;
    }
}