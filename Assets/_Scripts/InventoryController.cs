using System.Collections.Generic;
using UnityEngine;
using System.Linq; // 用于 FindAll 等 Linq 操作，如果需要的话

/// <summary>
/// 库存控制器
/// </summary>
public class InventoryController : MonoBehaviour
{
    /// <summary>
    /// 所选物品网格
    /// </summary>
    private ItemGrid selectedItemGrid;

    /// <summary>
    /// 所选物品网格
    /// </summary>
    public ItemGrid SelectedItemGrid
    {
        get => selectedItemGrid;
        set
        {
            selectedItemGrid = value;
            inventoryHighlight.SetParent(value);
        }
    }

    /// <summary>
    /// 所选物品
    /// </summary>
    private InventoryItem selectedItem;

    /// <summary>
    /// 重叠物品
    /// </summary>
    private InventoryItem overlapItem;

    /// <summary>
    /// 所选物品的矩形变换
    /// </summary>
    private RectTransform rectTransform;

    /// <summary>
    /// 物品预制体
    /// </summary>
    [SerializeField] public GameObject itemPrefab;

    /// <summary>
    /// UI画布变换
    /// </summary>
    [SerializeField] private Transform canvasTransform;

    /// <summary>
    /// 库存物品高亮逻辑相关
    /// </summary>
    private InventoryHighlight inventoryHighlight;

    /// <summary>
    /// 上次高亮的位置
    /// </summary>
    private Vector2Int oldPosition;

    /// <summary>
    /// 要高亮的物品
    /// </summary>
    private InventoryItem itemToHighlight;

    [Header("Player Inventory Grids")]
    [Tooltip("List of ItemGrids that constitute the player's main inventories (excluding shop).")]
    public List<ItemGrid> playerInventoryGrids = new List<ItemGrid>();

    private ItemGrid shopItemGrid; // 需要商店网格的引用
    private ShopController shopControllerInstance; // 引用 ShopController

    private void Awake()
    {
        inventoryHighlight = GetComponent<InventoryHighlight>();
    }

    void Start() // 或者 Awake，确保 ItemDataLoader 和其他依赖项已准备好
    {
        // 示例：获取对商店网格的引用 (你需要根据你的场景设置来调整)
        ShopController shopCtrl = FindObjectOfType<ShopController>();
        if (shopCtrl != null)
        {
            shopItemGrid = shopCtrl.shopItemGrid;
            shopControllerInstance = shopCtrl; // 保存 ShopController 实例
        }
        else
        {
            Debug.LogWarning("InventoryController: ShopController not found, cannot assign shopItemGrid for item displacement.");
        }
    }

    private void Update()
    {
        HandleMouseInteractions();
        ItemIconDrag(); // Keeps selected item icon following mouse if an item is selected

        if (Input.GetKeyDown(KeyCode.Q))
        {
            if (selectedItem == null) CreateRandomItem();
        }

        if (Input.GetKeyDown(KeyCode.W))
        {
             // InsertRandomItem(); // 这个方法也需要适配新的数据和拾取逻辑
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            if (selectedItem != null) selectedItem.Rotate();
        }
    }

    private void HandleMouseInteractions()
    {
        // 获取当前鼠标下的网格和格子位置
        // selectedItemGrid 由 GridInteract 更新 (当鼠标悬停在某个 ItemGrid 上时)
        // hoveredGrid 就是当前的 selectedItemGrid
        ItemGrid hoveredGrid = this.selectedItemGrid; // Renaming for clarity in this context
        Vector2Int hoveredTilePos = Vector2Int.zero;

        if (hoveredGrid != null)
        {
            // 计算鼠标在当前悬停网格中的精确格子位置 (用于高亮和潜在操作)
            // 使用与 HandleHighlight 中类似的逻辑来获取 positionOnGrid
            Vector2 mousePosForGrid = Input.mousePosition;
            if (selectedItem != null) // 如果正在拖拽，应用偏移以使物品中心大致对齐鼠标
            {
                Vector2 effectiveTileSize = hoveredGrid.GetEffectiveTileSize();
                if (effectiveTileSize.x > 0 && effectiveTileSize.y > 0)
                {
                    mousePosForGrid.x -= (selectedItem.WIDTH - 1) * effectiveTileSize.x / 2;
                    mousePosForGrid.y += (selectedItem.HEIGHT - 1) * effectiveTileSize.y / 2;
                }
            }
            hoveredTilePos = hoveredGrid.GetTileGridPosition(mousePosForGrid);
        }

        // 更新高亮 (高亮逻辑现在独立于点击)
        if (hoveredGrid != null)
        {
            HandleHighlight(hoveredGrid, hoveredTilePos); // 将参数传递给高亮处理
        }
        else
        {
            inventoryHighlight.Show(false); // 鼠标不在任何网格上
        }

        // 处理拾取
        if (selectedItem == null) // 如果没有物品被选中
        {
            if (Input.GetMouseButtonDown(0) && hoveredGrid != null && hoveredTilePos.x != -1)
            {
                Debug.Log($"[HandleMouseInteractions] MouseButtonDown(0) while no item selected. Hovering grid: {hoveredGrid.name}, pos: {hoveredTilePos}");
                InventoryItem itemAtPos = hoveredGrid.GetItem(hoveredTilePos.x, hoveredTilePos.y);
                if (itemAtPos != null)
                {
                    PickUpItemFromGrid(itemAtPos, hoveredGrid);
                }
            }
        }
        // 处理放下
        else // 如果有物品被选中 (正在拖拽)
        {
            if (Input.GetMouseButtonUp(0))
            {
                Debug.Log($"[HandleMouseInteractions] MouseButtonUp(0) while {selectedItem.jsonData.Name} is selected.");
                if (hoveredGrid != null && hoveredTilePos.x != -1) // 必须在某个有效网格位置松开
                {
                    AttemptPlaceOrCombine(selectedItem, hoveredGrid, hoveredTilePos);
                }
                else // 如果在网格外部松开，或者无效位置
                {
                    // 行为待定：放回原处？丢弃？这里先简单地不放下，让玩家继续拖
                    // 或者，如果有一个"来源网格"，可以尝试放回。目前简化，不处理。
                    Debug.Log("[HandleMouseInteractions] Dropped item outside a valid grid or on invalid tile. Item remains selected (for now).");
                    // To drop it (e.g., destroy or return to a default inventory):
                    // Destroy(selectedItem.gameObject);
                    // selectedItem = null; 
                }
            }
        }
    }
    
    // 修改后的高亮处理，接收参数
    private void HandleHighlight(ItemGrid currentGrid, Vector2Int positionOnGrid) 
    {
        if (positionOnGrid.x < 0 || positionOnGrid.y < 0) 
        {
            inventoryHighlight.Show(false);
            oldPosition = positionOnGrid; 
            return;
        }

        // Simplified update check for now, can be refined later if performance is an issue.
        // bool selectedItemStateChangedRecently = false; 
        // if (oldPosition == positionOnGrid && this.selectedItemGrid == currentGrid && !selectedItemStateChangedRecently ...)
        // ...
        oldPosition = positionOnGrid;

        if (selectedItem == null)
        {
            itemToHighlight = currentGrid.GetItem(positionOnGrid.x, positionOnGrid.y);
            if (itemToHighlight != null)
            {
                inventoryHighlight.Show(true);
                inventoryHighlight.SetParent(currentGrid);
                inventoryHighlight.SetSize(itemToHighlight);
                inventoryHighlight.SetPosition(currentGrid, itemToHighlight);
                // For non-dragging highlight, use a default positive color or specific neutral one if desired
                // inventoryHighlight.UpdateHighlightColor(true); 
            }
            else
            {
                inventoryHighlight.Show(false);
            }
        }
        else // Player is dragging selectedItem
        {
            bool isInBounds = currentGrid.BoundryCheck(
                positionOnGrid.x, positionOnGrid.y,
                selectedItem.WIDTH, selectedItem.HEIGHT);
            
            inventoryHighlight.Show(isInBounds);
            inventoryHighlight.SetParent(currentGrid);
            inventoryHighlight.SetSize(selectedItem);
            inventoryHighlight.SetPosition(currentGrid, selectedItem, positionOnGrid.x, positionOnGrid.y);

            if (isInBounds)
            {
                bool isPositiveHighlight = false; // Default to red unless a green condition is met

                // 1. Check for exact match and potential combination (highest priority for green)
                List<InventoryItem> itemsInTargetAreaForCombineCheck = new List<InventoryItem>();
                RectInt potentialPlacementRect = new RectInt(positionOnGrid.x, positionOnGrid.y, selectedItem.WIDTH, selectedItem.HEIGHT);
                currentGrid.GetItemsInRect(potentialPlacementRect, itemsInTargetAreaForCombineCheck);

                InventoryItem singleItemUnderneath = null;
                bool exactMatchFound = false;

                if (itemsInTargetAreaForCombineCheck.Count == 1)
                {
                    singleItemUnderneath = itemsInTargetAreaForCombineCheck[0];
                    if (singleItemUnderneath != null && 
                        singleItemUnderneath != selectedItem && // Cannot combine with self in this context
                        singleItemUnderneath.onGridPositionX == positionOnGrid.x &&
                        singleItemUnderneath.onGridPositionY == positionOnGrid.y &&
                        singleItemUnderneath.WIDTH == selectedItem.WIDTH &&
                        singleItemUnderneath.HEIGHT == selectedItem.HEIGHT)
                    {
                        exactMatchFound = true;
                    }
                }

                if (exactMatchFound && CanCombineItems(selectedItem.jsonData, singleItemUnderneath.jsonData))
                {
                    isPositiveHighlight = true; // Combinable, so green
                }
                else
                {
                    // 2. If not combinable on exact match, check for non-displacing placement
                    //    (Re-using itemsInTargetAreaForCombineCheck as it contains the same items relevant here)
                    List<InventoryItem> itemsPotentiallyDisplaced = itemsInTargetAreaForCombineCheck;

                    bool willDisplaceOthers = false;
                    if (itemsPotentiallyDisplaced.Count == 0) {
                        willDisplaceOthers = false; // No items in the area
                    }
                    // Check if the only item in the area is the selectedItem itself (i.e., hovering over its original spot)
                    else if (itemsPotentiallyDisplaced.Count == 1 && itemsPotentiallyDisplaced.Contains(selectedItem)) {
                        willDisplaceOthers = false; 
                    }
                    else {
                        // If there are items, and at least one of them is NOT the selectedItem, it means others will be displaced.
                        // Or, if there are multiple items, displacement will occur.
                        willDisplaceOthers = itemsPotentiallyDisplaced.Any(item => item != selectedItem) || itemsPotentiallyDisplaced.Count > 1;
                    }

                    if (!willDisplaceOthers) // In bounds AND no displacement of OTHERS
                    {
                        isPositiveHighlight = true; // Green
                    }
                    else // In bounds BUT displaces others (and not combinable)
                    {
                        isPositiveHighlight = false; // Red
                    }
                }
                inventoryHighlight.UpdateHighlightColor(isPositiveHighlight);
            }
            else // Out of bounds
            {
                inventoryHighlight.UpdateHighlightColor(false); // Red
            }
        }
    }

    private bool CanCombineItems(JsonItemData itemData1, JsonItemData itemData2)
    {
        if (itemData1 == null || itemData2 == null) return false;

        // Check for same item type (ID), same level, and same weapon group
        bool sameId = itemData1.Id == itemData2.Id;
        bool sameLevel = itemData1.Level == itemData2.Level;
        // Ensuring weaponGroupNum is considered for combination eligibility, not just finding next level.
        // If items must be of the same group to combine (even if ID implies group), check here.
        // Assuming ID already implies a weaponGroup or this check is sufficient for your design.
        bool sameGroup = itemData1.weaponGroupNum == itemData2.weaponGroupNum; 

        if (sameId && sameLevel && sameGroup) // Added sameGroup check for consistency
        {
            if (ItemDataLoader.Instance == null || ItemDataLoader.Instance.AllItems == null) 
            {
                Debug.LogError("[CanCombineItems] ItemDataLoader not ready or no items loaded.");
                return false;
            }
            // Check if a next level item exists in the same group
            int targetLevel = itemData1.Level + 1;
            return ItemDataLoader.Instance.AllItems.Any(item => item.weaponGroupNum == itemData1.weaponGroupNum && item.Level == targetLevel);
        }
        return false;
    }

    private void PickUpItemFromGrid(InventoryItem itemToPickUp, ItemGrid sourceGrid)
    {
        if (itemToPickUp == null || sourceGrid == null)
        {
            Debug.LogError("[PickUpItemFromGrid] Called with null itemToPickUp or sourceGrid.");
            return;
        }

        Debug.Log($"[PickUpItemFromGrid] Picking up {itemToPickUp.jsonData.Name} from grid {sourceGrid.name}");
        selectedItem = sourceGrid.PickUpItem(itemToPickUp.onGridPositionX, itemToPickUp.onGridPositionY); 

        if (selectedItem != null)
        {
            // If the item was picked up from the shop, notify the ShopController
            if (shopControllerInstance != null && sourceGrid == shopItemGrid) // shopItemGrid is the reference to the shop's grid
            {
                shopControllerInstance.NotifyItemPickedUpFromShop(selectedItem); // or itemToPickUp, should be the same object
            }

            rectTransform = selectedItem.GetComponent<RectTransform>();
            rectTransform.SetParent(canvasTransform); 
            rectTransform.SetAsLastSibling();
        }
        else
        {
            Debug.LogError($"[PickUpItemFromGrid] PickUpItem returned null for {itemToPickUp.jsonData.Name}, but itemToPickUp was presumably valid. This might happen if it was already picked up or grid logic issue.");
        }
    }

    // Renamed and refactored from old PlaceItem and TryCombineItems logic
    private void AttemptPlaceOrCombine(InventoryItem itemBeingPlaced, ItemGrid targetGrid, Vector2Int targetTilePos)
    {
        Debug.Log($"[AttemptPlaceOrCombine] Attempting for {itemBeingPlaced.jsonData.Name} onto grid {targetGrid.name} at {targetTilePos}");

        // --- Strict Combination Check First ---
        List<InventoryItem> itemsInTargetArea = new List<InventoryItem>();
        RectInt potentialPlacementRect = new RectInt(targetTilePos.x, targetTilePos.y, itemBeingPlaced.WIDTH, itemBeingPlaced.HEIGHT);
        
        if (!targetGrid.BoundryCheck(potentialPlacementRect.x, potentialPlacementRect.y, potentialPlacementRect.width, potentialPlacementRect.height))
        {   
            Debug.Log($"[AttemptPlaceOrCombine] Potential placement area for {itemBeingPlaced.jsonData.Name} is out of bounds. Cannot place or combine. Item remains selected.");
            return;
        }

        targetGrid.GetItemsInRect(potentialPlacementRect, itemsInTargetArea);

        InventoryItem singleItemUnderneath = null;
        bool exactMatchFound = false;

        if (itemsInTargetArea.Count == 1)
        {
            singleItemUnderneath = itemsInTargetArea[0];
            if (singleItemUnderneath != null && 
                singleItemUnderneath != itemBeingPlaced && 
                singleItemUnderneath.onGridPositionX == targetTilePos.x &&
                singleItemUnderneath.onGridPositionY == targetTilePos.y &&
                singleItemUnderneath.WIDTH == itemBeingPlaced.WIDTH &&
                singleItemUnderneath.HEIGHT == itemBeingPlaced.HEIGHT)
            {
                exactMatchFound = true;
            }
        }

        if (exactMatchFound)
        {
            Debug.Log($"[AttemptPlaceOrCombine] Exact match found with {singleItemUnderneath.jsonData.Name}. Attempting combination.");
            // Pass the correct grid and position for combination context
            bool combinedSuccessfully = TryCombineItems(itemBeingPlaced, singleItemUnderneath, targetGrid, new Vector2Int(singleItemUnderneath.onGridPositionX, singleItemUnderneath.onGridPositionY)); 
            if (combinedSuccessfully)
            {
                Debug.Log("[AttemptPlaceOrCombine] Combination successful.");
                selectedItem = null; 
                return; 
            }
            else
            {
                Debug.Log("[AttemptPlaceOrCombine] Combination attempted with exact match but failed (e.g., no valid upgrade). Proceeding to placement/displacement.");
                // DO NOT return here. Allow fall-through to displacement logic below.
                // The 'singleItemUnderneath' will be part of 'displacedItems' if placement proceeds.
            }
        }
        // --- End of Strict Combination Check ---

        Debug.Log("[AttemptPlaceOrCombine] Proceeding with placement/displacement logic.");
        List<InventoryItem> displacedItems = new List<InventoryItem>(); 
        bool placedSuccessfully = targetGrid.PlaceItem(itemBeingPlaced, targetTilePos.x, targetTilePos.y, displacedItems);

        if (placedSuccessfully)
        {
            Debug.Log($"[AttemptPlaceOrCombine] Successfully placed {itemBeingPlaced.jsonData.Name} into {targetGrid.name}. {displacedItems.Count} items were displaced.");
            selectedItem = null; 

            if (displacedItems.Count > 0) 
            {
                Debug.Log($"[AttemptPlaceOrCombine] {displacedItems.Count} items were displaced. Attempting to move them to shop.");
                foreach (InventoryItem displacedItem in displacedItems)
                {
                    Debug.Log($"[AttemptPlaceOrCombine] Moving displaced item {displacedItem.jsonData.Name} from {targetGrid.name} to shop.");
                    MoveItemToShop(displacedItem);
                }
            }
        }
        else
        {
            Debug.Log($"[AttemptPlaceOrCombine] Failed to place {itemBeingPlaced.jsonData.Name} into {targetGrid.name}. Item remains selected.");
        }
    }

    private void MoveItemToShop(InventoryItem itemToMove)
    {
        if (itemToMove == null) return;

        if (shopControllerInstance != null)
        {
            Debug.Log($"[InventoryController] Moving item {itemToMove.jsonData.Name} to shop via ShopController.AddItemToShopDirectly.");
            shopControllerInstance.AddItemToShopDirectly(itemToMove);
        }
        else
        {
            Debug.LogWarning($"[InventoryController] ShopController instance is null. Cannot move {itemToMove.jsonData.Name} to shop. Item will be destroyed.");
            Destroy(itemToMove.gameObject);
        }
    }

    // TryCombineItems needs to accept the grid where combination is happening for item removal
    private bool TryCombineItems(InventoryItem placedItem, InventoryItem existingItem, ItemGrid currentGrid, Vector2Int combinePos)
    {
        if (placedItem?.jsonData == null || existingItem?.jsonData == null) return false;

        Debug.Log($"[TryCombineItems] Checking: {placedItem.jsonData.Name} (L{placedItem.jsonData.Level}) + {existingItem.jsonData.Name} (L{existingItem.jsonData.Level}) on grid {currentGrid.name}");

        bool sameId = placedItem.jsonData.Id == existingItem.jsonData.Id;
        bool sameLevel = placedItem.jsonData.Level == existingItem.jsonData.Level;
        bool sameGroup = placedItem.jsonData.weaponGroupNum == existingItem.jsonData.weaponGroupNum;

        Debug.Log($"[TryCombineItems] Conditions: SameID: {sameId}, SameLevel: {sameLevel}, SameGroup: {sameGroup}");

        if (sameId && sameLevel && sameGroup)
        {
            int currentLevel = placedItem.jsonData.Level;
            int groupNum = placedItem.jsonData.weaponGroupNum;
            JsonItemData nextLevelJsonData = ItemDataLoader.Instance?.AllItems?.FirstOrDefault(item => item.weaponGroupNum == groupNum && item.Level == currentLevel + 1);

            if (nextLevelJsonData != null)
            {
                Debug.Log($"[TryCombineItems] Found next level: {nextLevelJsonData.Name}. Combining on grid {currentGrid.name}");

                // placedItem is the item being dragged (not on any grid system yet).
                // existingItem is the item already on currentGrid at combinePos.

                Debug.Log($"[TryCombineItems] Removing existingItem: {existingItem.jsonData.Name} from grid {currentGrid.name} at its pos ({existingItem.onGridPositionX},{existingItem.onGridPositionY})");
                currentGrid.ClearGridReference(existingItem); // Remove the item that was on the grid

                Debug.Log($"[TryCombineItems] Destroying GameObjects: {placedItem.jsonData.Name} (dragged) and {existingItem.jsonData.Name} (was on grid)");
                Destroy(placedItem.gameObject);   // Destroy the dragged item
                Destroy(existingItem.gameObject); // Destroy the item that was on the grid

                InventoryItem upgradedItem = Instantiate(itemPrefab).GetComponent<InventoryItem>();
                upgradedItem.Set(nextLevelJsonData);
                
                InventoryItem tempOverlapForUpgraded = null;
                Debug.Log($"[TryCombineItems] Placing upgraded item {upgradedItem.jsonData.Name} at {combinePos} on grid {currentGrid.name}");
                // This call to PlaceItem will need to be updated later if its signature changes for multi-displacement
                bool placementOfUpgraded = currentGrid.PlaceItem(upgradedItem, combinePos.x, combinePos.y, ref tempOverlapForUpgraded);
                
                if (!placementOfUpgraded) {
                    Debug.LogError($"[TryCombineItems] CRITICAL: Failed to place upgraded item {nextLevelJsonData.Name}. Upgraded item lost!");
                    Destroy(upgradedItem.gameObject);
                    return false; 
                }
                if (tempOverlapForUpgraded != null) {
                     Debug.LogWarning($"[TryCombineItems] Upgraded item unexpectedly overlapped with {tempOverlapForUpgraded.jsonData.Name}. Destroying this unexpected overlap.");
                     currentGrid.ClearGridReference(tempOverlapForUpgraded);
                     Destroy(tempOverlapForUpgraded.gameObject);
                }
                return true; 
            }
            else {
                Debug.Log("[TryCombineItems] No next level item found.");
            }
        }
        return false; 
    }

    /// <summary>
    /// 生成一个随机物品 (现在考虑稀有度)
    /// </summary>
    private void CreateRandomItem()
    {
        if (ItemDataLoader.Instance == null || ItemDataLoader.Instance.AllItems == null || ItemDataLoader.Instance.AllItems.Count == 0)
        {
            Debug.LogError("InventoryController: ItemDataLoader not ready or no items loaded.");
            return;
        }

        // 筛选合法的 itemType (例如 itemType == 1)
        List<JsonItemData> validItemsToSpawn = ItemDataLoader.Instance.AllItems.FindAll(item => item.itemType == 1);
        if (validItemsToSpawn.Count == 0)
        {
            Debug.LogError("InventoryController: No valid items (itemType == 1) found in ItemDataLoader to create randomly.");
            return;
        }

        // 使用 ItemDataLoader 中的加权随机选择方法
        JsonItemData selectedJsonItem = ItemDataLoader.Instance.SelectRandomItemByRarity(validItemsToSpawn);
        
        if (selectedJsonItem == null) // Should not happen if validItemsToSpawn is not empty and weights are set up
        {
            Debug.LogError("InventoryController: SelectRandomItemByRarity returned null even with valid items. Check weights or selection logic.");
            return;
        }
        
        InventoryItem inventoryItem = Instantiate(itemPrefab).GetComponent<InventoryItem>();
        RectTransform itemRectTransform = inventoryItem.GetComponent<RectTransform>(); // Renamed for clarity
        itemRectTransform.SetParent(canvasTransform); 
        itemRectTransform.SetAsLastSibling();

        inventoryItem.Set(selectedJsonItem); 
        selectedItem = inventoryItem; // This item is now being "dragged" by the mouse implicitly after creation

        Debug.Log($"[InventoryController] Created random item: {selectedJsonItem.Name} (Rarity: {selectedJsonItem.Rarity})");
    }

    /// <summary>
    /// 使所选物品的图标跟随鼠标移动
    /// </summary>
    private void ItemIconDrag()
    {
        if (selectedItem != null)
        {
            rectTransform.position = Input.mousePosition;
            rectTransform.SetParent(canvasTransform);
        }
    }

    // Helper for HandleHighlight to get the grid position of the mouse, possibly adjusted if an item is selected.
    // This is similar to the logic that was inside HandleMouseInteractions for hoveredTilePos
    private Vector2Int GetMouseGridPosition(ItemGrid grid) {
        if (grid == null) return new Vector2Int(-1,-1);

        Vector2 mousePos = Input.mousePosition;
        if (selectedItem != null) {
            Vector2 effectiveTileSize = grid.GetEffectiveTileSize();
            if (effectiveTileSize.x > 0 && effectiveTileSize.y > 0) {
                mousePos.x -= (selectedItem.WIDTH - 1) * effectiveTileSize.x / 2;
                mousePos.y += (selectedItem.HEIGHT - 1) * effectiveTileSize.y / 2;
            }
        }
        return grid.GetTileGridPosition(mousePos);
    }

    /// <summary>
    /// Retrieves all items from player inventories that are currently in a Hidden or Searching state.
    /// </summary>
    /// <returns>A list of items pending search in player inventories.</returns>
    public List<InventoryItem> GetPendingSearchItemsInPlayerInventories()
    {
        List<InventoryItem> pendingSearchItems = new List<InventoryItem>();
        if (playerInventoryGrids == null || playerInventoryGrids.Count == 0)
        {
            Debug.LogWarning("[InventoryController] playerInventoryGrids list is not assigned or empty. Cannot fetch pending search items from player inventories.");
            return pendingSearchItems;
        }

        foreach (ItemGrid playerGrid in playerInventoryGrids)
        {
            if (playerGrid != null)
            {
                List<InventoryItem> itemsInThisGrid = playerGrid.GetAllUniqueItems();
                foreach (InventoryItem item in itemsInThisGrid)
                {
                    if (item != null && 
                        (item.currentDisplayState == InventoryItem.ItemDisplayState.Hidden || 
                         item.currentDisplayState == InventoryItem.ItemDisplayState.Searching))
                    {
                        if (!pendingSearchItems.Contains(item)) // Ensure uniqueness across multiple grids if an item could somehow be listed twice
                        {
                            pendingSearchItems.Add(item);
                        }
                    }
                }
            }
        }
        Debug.Log($"[InventoryController] Found {pendingSearchItems.Count} items pending search in player inventories.");
        return pendingSearchItems;
    }
}