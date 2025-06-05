using System.Collections.Generic;
using UnityEngine;
using System.Linq; // 用于 FindAll 等 Linq 操作，如果需要的话
using UnityEngine.EventSystems; // Added for RaycastResult
using System.Collections; // Added for Coroutines
using UnityEngine.UI; // Added for UI Text

/// <summary>
/// 库存控制器
/// </summary>
public class InventoryController : MonoBehaviour
{
    /// <summary>
    /// 所选物品网格
    /// </summary>
    private ItemGrid selectedItemGrid;
    private ItemGrid previousSelectedItemGrid; // Added as class member

    /// <summary>
    /// 所选物品网格
    /// </summary>
    public ItemGrid SelectedItemGrid
    {
        get => selectedItemGrid;
        set
        {
            // if (selectedItemGrid == value) return; // Optimization: no change

            if (selectedItemGrid != null && selectedItemGrid != value)
            {
                // Simulate OnPointerExit if GridInteract didn't handle it or if we're forcing a change
                // This is tricky; direct calls to OnPointerExit might not be what we want.
                // GridInteract should ideally handle its own state.
                // For now, we rely on GridInteract to set selectedItemGrid to null on exit.
            }

            selectedItemGrid = value;
            inventoryHighlight.SetParent(value); // Parent can be null if selectedItemGrid is null

            if (selectedItemGrid != null && selectedItemGrid != previousSelectedItemGrid)
            {
                 // Simulate OnPointerEnter if needed, though GridInteract should do this.
            }
            // previousSelectedItemGrid = selectedItemGrid; // Keep track of the grid change
        }
    }

    /// <summary>
    /// 所选物品
    /// </summary>
    private InventoryItem selectedItem;

    /// <summary>
    /// 重叠物品 - This seems to be a legacy/specific use field. Review if still needed broadly.
    /// For TryCombineItems, a local variable is used.
    /// For PlaceItem that returns a single overlap, it's passed by ref.
    /// </summary>
    // private InventoryItem overlapItem; // Might be unused if local refs are preferred.

    /// <summary>
    /// 所选物品的矩形变换
    /// </summary>
    private RectTransform rectTransform; // This is assigned in ItemIconDrag from selectedItem

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

    // --- New fields for item details popup and click vs drag ---
    [Header("Item Details Popup")]
    [Tooltip("Prefab for the item details popup window. Assign in Inspector.")]
    public ItemDetailsPopup itemDetailsPopupPrefab;
    [Tooltip("Prefab for the popup when clicking a hidden/unidentified item. Assign in Inspector.")]
    public GameObject undiscoveredItemPopupPrefab; // New Prefab reference

    private ItemDetailsPopup currentDetailsPopupInstance;
    private GameObject currentUndiscoveredPopupInstance; // Instance for the new popup

    private InventoryItem itemClickedOrPressed; // Item under cursor on mouse down
    private ItemGrid gridUnderPointerAtPress; // Grid under cursor on mouse down
    private float pointerDownTimestamp;
    private Vector3 pointerDownScreenPosition;

    [Tooltip("Delay in seconds before a press is considered a drag.")]
    public float dragStartDelay = 0.2f;
    [Tooltip("Pixel distance threshold before a press is considered a drag.")]
    public float dragStartDistanceThreshold = 10f;
    // --- End new fields ---

    [Header("Effects")] // 新增一个Header用于特效相关的字段
    [Tooltip("Assign the GameObject prefab to play on successful item combination.")]
    [SerializeField] private GameObject combineEffectPrefab; // 用于合成特效的预制体 (通用GameObject)
    [Tooltip("Duration for the combine effect to last before being destroyed.")]
    [SerializeField] private float combineEffectDuration = 2f; // 特效持续时间
    [Tooltip("Assign a UI Panel (RectTransform or Transform) that will host the instantiated effects. Ensure this panel is ordered correctly in your UI hierarchy to appear above grids.")]
    [SerializeField] private Transform effectsHostPanel; // 用于承载特效的Panel

    [Header("Feedback UI")]
    [Tooltip("Text UI element to display messages like 'insufficient space'. Assign in Inspector.")]
    public Text feedbackMessageText; // Or TextMeshProUGUI
    [Tooltip("How long the feedback message stays visible (seconds).")]
    public float feedbackMessageDuration = 2f;

    [Header("Feedback Animation")]
    [Tooltip("Initial scale factor when message appears.")]
    public float feedbackAppearScaleFactor = 1.2f;
    [Tooltip("Duration of the appear scale animation (seconds).")]
    public float feedbackAppearAnimDuration = 0.2f;
    [Tooltip("Duration of the disappear fade animation (seconds).")]
    public float feedbackDisappearFadeDuration = 0.3f;

    private bool gamePhaseOver = false; // Flag to freeze interactions
    private Coroutine activeFeedbackMessageCoroutine = null;
    private Coroutine activeAppearAnimationCoroutine = null; // For managing scale animation

    private bool hasLoadedInventory = false; // 防止重复加载

    private void Awake()
    {
        inventoryHighlight = GetComponent<InventoryHighlight>();
        if (canvasTransform == null)
        {
            // Try to find a Canvas in parent if not assigned, common setup
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                canvasTransform = parentCanvas.transform;
            }
            else
            {
                Debug.LogError("InventoryController: Canvas Transform is not assigned and couldn't be found in parents!", this);
            }
        }

        if (feedbackMessageText != null)
        {
            feedbackMessageText.gameObject.SetActive(false); // Start with message hidden
        }
        else
        {
            Debug.LogWarning("InventoryController: FeedbackMessageText UI is not assigned. UI feedback messages will not be shown.");
        }

        // 确保特效承载Panel的引用存在
        if (effectsHostPanel == null)
        {
            Debug.LogWarning("InventoryController: effectsHostPanel is not assigned. Effects might not be parented correctly or appear as intended. Consider assigning a dedicated UI panel for effects.");
            // 如果未分配，可以默认使用canvasTransform，但用户明确要求用panel承载
            // effectsHostPanel = canvasTransform; 
        }
        else
        {
            // 可选：尝试将特效面板置于其父级（canvasTransform）的最顶层，但这取决于层级结构
            // effectsHostPanel.SetAsLastSibling(); 
            // 更推荐用户在编辑器中手动调整好 effectsHostPanel 的层级顺序
        }
    }

    void Start() // 或者 Awake，确保 ItemDataLoader 和其他依赖项已准备好
    {
        ShopController shopCtrl = FindObjectOfType<ShopController>();
        if (shopCtrl != null)
        {
            shopItemGrid = shopCtrl.shopItemGrid;
            shopControllerInstance = shopCtrl; 
        }
        else
        {
            Debug.LogWarning("InventoryController: ShopController not found, cannot assign shopItemGrid for item displacement.");
        }

        if (itemDetailsPopupPrefab == null)
        {
            Debug.LogWarning("InventoryController: ItemDetailsPopupPrefab is not assigned in the Inspector. Item details functionality will be disabled.");
        }
        if (undiscoveredItemPopupPrefab == null)
        {
            Debug.LogWarning("InventoryController: UndiscoveredItemPopupPrefab is not assigned. Hidden item popup functionality will be limited.");
        }

        // Attempt to load inventory state if data exists from a previous scene
        if (GameDataManager.Instance != null && GameDataManager.Instance.HasPersistedData && !hasLoadedInventory)
        {
            LoadInventoryState();
        }
    }

    private void Update()
    {
        if (gamePhaseOver) 
        {
            // Optionally clear selected item or hide highlight if game phase ends abruptly
            if (selectedItem != null) {
                // Decide how to handle a selected item when game ends: return to original spot, destroy, etc.
                // For now, let's just deselect it to prevent it from being stuck with the mouse cursor image.
                // A more robust solution might involve returning it to a grid or specific logic.
                Destroy(selectedItem.gameObject); // Simplest: destroy if player was holding it
                selectedItem = null;
                inventoryHighlight.Show(false);
            }
            return; // Skip all interactions
        }

        HandleMouseInteractions();
        ItemIconDrag();

        if (Input.GetKeyDown(KeyCode.Q))
        {
            if (selectedItem == null) CreateRandomItem();
        }

        // W key was previously for InsertRandomItem, now repurposed
        // if (Input.GetKeyDown(KeyCode.W))
        // {
        //      // InsertRandomItem(); 
        // }

        if (Input.GetKeyDown(KeyCode.R))
        {
            if (selectedItem != null) selectedItem.Rotate();
        }

        // New Shortcut Keys E and W
        if (Input.GetKeyDown(KeyCode.E))
        {
            HandleQuickMoveToShop();
        }
        if (Input.GetKeyDown(KeyCode.W))
        {
            HandleQuickMoveToPlayerInventory();
        }
    }

    private void HandleMouseInteractions()
    {
        ItemGrid currentHoveredGrid; // Renamed from out parameter for clarity
        Vector2Int mousePosOnGrid = GetCurrentMouseGridPosition(out currentHoveredGrid);

        // The selectedItemGrid should now be managed by GetCurrentMouseGridPosition via the property setter
        // HandleHighlight will use this.selectedItemGrid (which is currentHoveredGrid if one is found)
        HandleHighlight(currentHoveredGrid, mousePosOnGrid); 
        // previousSelectedItemGrid = currentHoveredGrid; // This is problematic if currentHoveredGrid is null, should be managed by SelectedItemGrid setter or more carefully.
                                                         // For now, let's rely on this.selectedItemGrid being the source of truth.

        if (Input.GetMouseButtonDown(0))
        {
            // Logic for handling popup closure can be added here if needed
            // e.g., if clicking outside the popup while it's open.
            CloseAllPopups(); // Close any open popups on a new click before processing further

            if (selectedItem == null) 
            {
                if (currentHoveredGrid != null && currentHoveredGrid.IsValidGridPosition(mousePosOnGrid))
                {
                    itemClickedOrPressed = currentHoveredGrid.GetItem(mousePosOnGrid.x, mousePosOnGrid.y);
                    if (itemClickedOrPressed != null)
                    {
                        gridUnderPointerAtPress = currentHoveredGrid;
                        pointerDownTimestamp = Time.time;
                        pointerDownScreenPosition = Input.mousePosition;
                    }
                    else
                    {
                        itemClickedOrPressed = null;
                        gridUnderPointerAtPress = null;
                    }
                }
            }
        }

        if (Input.GetMouseButton(0) && itemClickedOrPressed != null && selectedItem == null)
        {
            bool timeThresholdMet = Time.time - pointerDownTimestamp > dragStartDelay;
            bool distanceThresholdMet = Vector3.Distance(Input.mousePosition, pointerDownScreenPosition) > dragStartDistanceThreshold;

            if (timeThresholdMet || distanceThresholdMet)
            {
                PickUpItemFromGrid(itemClickedOrPressed, gridUnderPointerAtPress); 
                itemClickedOrPressed = null; 
                gridUnderPointerAtPress = null;

                if (currentDetailsPopupInstance != null && currentDetailsPopupInstance.gameObject.activeSelf)
                {
                    currentDetailsPopupInstance.Hide(); 
                }
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (itemClickedOrPressed != null && selectedItem == null) 
            {
                ShowItemDetails(itemClickedOrPressed);
                itemClickedOrPressed = null;
                gridUnderPointerAtPress = null;
            }
            else if (selectedItem != null) 
            {
                if (currentHoveredGrid != null && currentHoveredGrid.IsValidGridPosition(mousePosOnGrid))
                {
                    AttemptPlaceOrCombine(selectedItem, currentHoveredGrid, mousePosOnGrid);
                }
                else
                {
                    Debug.Log($"Item {selectedItem.jsonData.Name} dropped outside a valid grid. Trying to move to shop.");
                    if (shopItemGrid != null)
                    {
                         MoveItemToShop(selectedItem, shopItemGrid.FindSpaceForObject(selectedItem.jsonData.ParsedWidth, selectedItem.jsonData.ParsedHeight));
                         // selectedItem = null; // MoveItemToShop will set selectedItem to null if successfully moved
                    } else {
                        Debug.LogWarning("ShopItemGrid not available to move dropped item. Item remains selected.");
                    }
                }
            }
            itemClickedOrPressed = null;
            gridUnderPointerAtPress = null;
        }
    }
    
    private void HandleHighlight(ItemGrid currentGrid, Vector2Int positionOnGrid) 
    {
        // Use this.selectedItemGrid as the primary grid for highlighting if valid,
        // currentGrid is the one directly under mouse, might differ from selectedItemGrid
        // if GridInteract events are delayed or if mouse just exited.
        ItemGrid gridToHighlightOn = this.selectedItemGrid; // Prefer the grid context established by GridInteract/GetCurrentMouseGridPosition
        if (gridToHighlightOn == null) gridToHighlightOn = currentGrid; // Fallback if no selectedItemGrid (e.g. mouse just entered)

        if (gridToHighlightOn == null || positionOnGrid.x < 0 || positionOnGrid.y < 0 || !gridToHighlightOn.IsValidGridPosition(positionOnGrid))
        {
            inventoryHighlight.Show(false);
            oldPosition = positionOnGrid; // Store invalid pos as well to detect change
            return;
        }
        
        // if (oldPosition == positionOnGrid && this.previousSelectedItemGrid == gridToHighlightOn && selectedItem == null) return; // Optimization for static highlight
        oldPosition = positionOnGrid;
        // this.previousSelectedItemGrid = gridToHighlightOn; // Update previous grid for next frame's optimization check

        if (selectedItem == null) // Not dragging anything, highlight item under cursor
        {
            itemToHighlight = gridToHighlightOn.GetItem(positionOnGrid.x, positionOnGrid.y);
            if (itemToHighlight != null)
            {
                inventoryHighlight.Show(true);
                // inventoryHighlight.SetParent(gridToHighlightOn); // Setter of SelectedItemGrid handles this
                inventoryHighlight.SetSize(itemToHighlight);
                inventoryHighlight.SetPosition(gridToHighlightOn, itemToHighlight);
            }
            else
            {
                inventoryHighlight.Show(false);
            }
        }
        else // Player is dragging selectedItem
        {
            bool isInBounds = gridToHighlightOn.BoundryCheck(
                positionOnGrid.x, positionOnGrid.y,
                selectedItem.WIDTH, selectedItem.HEIGHT);
            
            inventoryHighlight.Show(isInBounds);
            // inventoryHighlight.SetParent(gridToHighlightOn); // Setter of SelectedItemGrid handles this
            inventoryHighlight.SetSize(selectedItem);
            inventoryHighlight.SetPosition(gridToHighlightOn, selectedItem, positionOnGrid.x, positionOnGrid.y);

            if (isInBounds)
            {
                bool isPositiveHighlight = false; 
                List<InventoryItem> itemsInTargetAreaForCombineCheck = new List<InventoryItem>();
                RectInt potentialPlacementRect = new RectInt(positionOnGrid.x, positionOnGrid.y, selectedItem.WIDTH, selectedItem.HEIGHT);
                gridToHighlightOn.GetItemsInRect(potentialPlacementRect, itemsInTargetAreaForCombineCheck);

                InventoryItem singleItemUnderneath = null;
                bool exactMatchFound = false;

                if (itemsInTargetAreaForCombineCheck.Count == 1)
                {
                    singleItemUnderneath = itemsInTargetAreaForCombineCheck[0];
                    if (singleItemUnderneath != selectedItem && 
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
                    isPositiveHighlight = true; 
                }
                else
                {
                    List<InventoryItem> itemsPotentiallyDisplaced = itemsInTargetAreaForCombineCheck;
                    bool willDisplaceOthers = false;
                    if (itemsPotentiallyDisplaced.Count == 0) {
                        willDisplaceOthers = false; 
                    }
                    else if (itemsPotentiallyDisplaced.Count == 1 && itemsPotentiallyDisplaced.Contains(selectedItem)) {
                        willDisplaceOthers = false; 
                    }
                    else {
                        willDisplaceOthers = itemsPotentiallyDisplaced.Any(item => item != selectedItem) || itemsPotentiallyDisplaced.Count > 1;
                    }

                    if (!willDisplaceOthers) 
                    {
                        isPositiveHighlight = true; 
                    }
                    else 
                    {
                        isPositiveHighlight = false; 
                    }
                }
                inventoryHighlight.UpdateHighlightColor(isPositiveHighlight);
            }
            else 
            {
                inventoryHighlight.UpdateHighlightColor(false); 
            }
        }
    }

    private bool CanCombineItems(JsonItemData itemData1, JsonItemData itemData2)
    {
        if (itemData1 == null || itemData2 == null) return false;
        bool sameId = itemData1.Id == itemData2.Id;
        bool sameLevel = itemData1.Level == itemData2.Level;
        bool sameGroup = itemData1.weaponGroupNum == itemData2.weaponGroupNum; 

        if (sameId && sameLevel && sameGroup)
        {
            if (ItemDataLoader.Instance == null || ItemDataLoader.Instance.AllItems == null) 
            {
                Debug.LogError("[CanCombineItems] ItemDataLoader not ready or no items loaded.");
                return false;
            }
            int targetLevel = itemData1.Level + 1;
            return ItemDataLoader.Instance.AllItems.Any(item => item.weaponGroupNum == itemData1.weaponGroupNum && item.Level == targetLevel);
        }
        return false;
    }

    private void PickUpItemFromGrid(InventoryItem itemToPickUp, ItemGrid sourceGrid)
    {
        if (itemToPickUp == null || sourceGrid == null)
        {
            Debug.LogError("Cannot pick up null item or from null grid.");
            return;
        }

        selectedItem = sourceGrid.PickUpItem(itemToPickUp.onGridPositionX, itemToPickUp.onGridPositionY);
        if (selectedItem != null)
        {
            Debug.Log($"Picked up {selectedItem.jsonData.Name} from {sourceGrid.name} at {new Vector2Int(selectedItem.onGridPositionX, selectedItem.onGridPositionY)}.");
            AudioManager.Instance?.PlayItemSelectSound(selectedItem.jsonData);
            rectTransform = selectedItem.GetComponent<RectTransform>(); 
        rectTransform.SetParent(canvasTransform);
        rectTransform.SetAsLastSibling();

            // Notify ShopController if item was picked from the shop grid
            if (shopControllerInstance != null && sourceGrid == shopItemGrid)
            {
                shopControllerInstance.NotifyItemPickedUpFromShop(selectedItem);
            }
        }
        else
        {
            Debug.LogError($"Failed to pick up item from {sourceGrid.name} at {new Vector2Int(itemToPickUp.onGridPositionX, itemToPickUp.onGridPositionY)}. GetItem returned null after PickUpItem call.");
        }
    }
    
    private void AttemptPlaceOrCombine(InventoryItem itemBeingPlaced, ItemGrid targetGrid, Vector2Int targetTilePos)
    {
        Debug.Log($"[AttemptPlaceOrCombine] Attempting for {itemBeingPlaced.jsonData.Name} onto grid {targetGrid.name} at {targetTilePos}");

        if (!targetGrid.BoundryCheck(targetTilePos.x, targetTilePos.y, itemBeingPlaced.WIDTH, itemBeingPlaced.HEIGHT))
        {   
            Debug.Log($"[AttemptPlaceOrCombine] Potential placement area for {itemBeingPlaced.jsonData.Name} is out of bounds. Cannot place or combine. Item remains selected.");
            return; 
        }

        List<InventoryItem> itemsInTargetArea = new List<InventoryItem>();
        RectInt potentialPlacementRect = new RectInt(targetTilePos.x, targetTilePos.y, itemBeingPlaced.WIDTH, itemBeingPlaced.HEIGHT);
        targetGrid.GetItemsInRect(potentialPlacementRect, itemsInTargetArea);

        InventoryItem singleItemUnderneath = null;
        bool exactMatchFound = false;

        if (itemsInTargetArea.Count == 1)
        {
            singleItemUnderneath = itemsInTargetArea[0];
            if (singleItemUnderneath != itemBeingPlaced && 
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
            bool combinedSuccessfully = TryCombineItems(itemBeingPlaced, singleItemUnderneath, targetGrid, new Vector2Int(singleItemUnderneath.onGridPositionX, singleItemUnderneath.onGridPositionY)); 
            if (combinedSuccessfully)
            {
                Debug.Log("[AttemptPlaceOrCombine] Combination successful.");
                // AudioManager.Instance?.PlayItemPlaceSound(itemBeingPlaced.jsonData); // Sound played by TryCombineItems (as itemCraftedSound)
                selectedItem = null; 
                return; 
            }
            else
            {
                Debug.Log("[AttemptPlaceOrCombine] Combination attempted with exact match but failed. Proceeding to placement/displacement.");
            }
        }
        
        Debug.Log("[AttemptPlaceOrCombine] Proceeding with placement/displacement logic.");
        List<InventoryItem> displacedItems = new List<InventoryItem>(); 
        bool placedSuccessfully = targetGrid.PlaceItem(itemBeingPlaced, targetTilePos.x, targetTilePos.y, displacedItems);

        if (placedSuccessfully)
        {
            Debug.Log($"[AttemptPlaceOrCombine] Successfully placed {itemBeingPlaced.jsonData.Name} into {targetGrid.name}. {displacedItems.Count} items were displaced.");
            
            // Play specific placement sound based on item data (if available), or generic place sound
            AudioManager.Instance?.PlayItemPlaceSound(itemBeingPlaced.jsonData); 

            if (playerInventoryGrids.Contains(targetGrid)) 
            {
                // This sound is for *generic* item to bag, specific quick move has its own sound
                // AudioManager.Instance?.PlayItemToBagSound(); // Covered by PlayItemPlaceSound if bag is the target, or if more specific sound is needed for general bag placement
            }
            else if (targetGrid == shopItemGrid) 
            {
                // This sound is for *generic* item to shop, specific quick move / displacement have own sounds
                // AudioManager.Instance?.PlayItemToShopSound(); // Covered by PlayItemPlaceSound if shop is target, or if more specific sound needed
            }

            selectedItem = null; 

            if (displacedItems.Count > 0) 
            {
                Debug.Log($"[AttemptPlaceOrCombine] {displacedItems.Count} items were displaced. Attempting to move them to shop.");
                foreach (InventoryItem displacedItem in displacedItems)
                {
                    if (displacedItem == itemBeingPlaced) continue; 
                    Debug.Log($"[AttemptPlaceOrCombine] Moving displaced item {displacedItem.jsonData.Name} from {targetGrid.name} to shop.");
                    bool movedToShop = MoveItemToShop(displacedItem, shopItemGrid.FindSpaceForObject(displacedItem.jsonData.ParsedWidth, displacedItem.jsonData.ParsedHeight), true);
                    // Sound for displacement is now handled within MoveItemToShop if context is passed
                }
            }
        }
        else
        {
            Debug.Log($"[AttemptPlaceOrCombine] Failed to place {itemBeingPlaced.jsonData.Name} into {targetGrid.name}. Item remains selected.");
        }
    }

    private bool MoveItemToShop(InventoryItem itemToMove, Vector2Int? positionInShop, bool isDisplacement = false)
    {
        if (itemToMove == null) return false;
        if (shopItemGrid == null)
        {
            Debug.LogWarning($"Shop grid is not set for {itemToMove.jsonData.Name}. Cannot move item to shop. Destroying item.");
            if (itemToMove == selectedItem) selectedItem = null; 
            Destroy(itemToMove.gameObject);
            return false;
        }

        if (!positionInShop.HasValue) 
        {
            positionInShop = shopItemGrid.FindSpaceForObject(itemToMove.jsonData.ParsedWidth, itemToMove.jsonData.ParsedHeight);
        }

        if (positionInShop.HasValue)
        {
            InventoryItem itemAlreadyAtShopPos = shopItemGrid.GetItem(positionInShop.Value.x, positionInShop.Value.y);
            if (itemAlreadyAtShopPos != null && itemAlreadyAtShopPos != itemToMove) { // Should not happen if FindSpace is good
                 Debug.LogWarning($"Shop slot {positionInShop.Value} for item {itemToMove.jsonData.Name} is already occupied by {itemAlreadyAtShopPos.jsonData.Name}. Destroying incoming item {itemToMove.jsonData.Name}.");
                 if (itemToMove == selectedItem) selectedItem = null;
                 Destroy(itemToMove.gameObject);
                 return false;
            }
            
            InventoryItem overlapItemInShop = null; // Using simpler PlaceItem overload
            List<InventoryItem> displacedInShop = new List<InventoryItem>(); // PlaceItem expects this
            bool placed = shopItemGrid.PlaceItem(itemToMove, positionInShop.Value.x, positionInShop.Value.y, displacedInShop); 
            
            if (placed)
            {
                Debug.Log($"Moved item {itemToMove.jsonData.Name} to shop at {positionInShop.Value}.");
                if (isDisplacement)
                {
                    AudioManager.Instance?.PlayItemDisplacedToShopSound();
                }
                else
                {
                    // This path is for direct moves to shop (not E key, that has its own sound)
                    // Could be a generic "item placed in shop" sound or item-specific place sound
                    AudioManager.Instance?.PlayItemPlaceSound(itemToMove.jsonData);
                    // Or specifically AudioManager.Instance?.PlayItemToShopSound(); if PlayItemPlaceSound isn't generic enough for this context.
                }
                
                if (selectedItem == itemToMove) 
                {
                    selectedItem = null;
                }
                if (displacedInShop.Any(i => i != itemToMove)) 
                {
                     Debug.LogWarning($"Unexpected overlap in shop with items when placing {itemToMove.jsonData.Name}. Destroying the overlapped items in shop.");
                     foreach(var dItem in displacedInShop)
                     {
                         if(dItem != itemToMove) 
                         {
                            shopItemGrid.ClearGridReference(dItem); 
                            Destroy(dItem.gameObject);
                         }
                     }
                }
                return true;
            }
            else
            {
                Debug.LogWarning($"Failed to place item {itemToMove.jsonData.Name} in shop at determined space {positionInShop.Value}. Destroying item.");
                if (itemToMove == selectedItem) selectedItem = null;
                Destroy(itemToMove.gameObject); 
                return false;
            }
        }
        else 
        {
            Debug.LogWarning($"No space found in shop for {itemToMove.jsonData.Name}. Destroying item.");
            if (itemToMove == selectedItem) selectedItem = null;
            Destroy(itemToMove.gameObject);
            return false;
        }
    }
    
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
                AudioManager.Instance?.PlayItemCraftedSound(); // Play sound
                Debug.Log($"[TryCombineItems] Found next level: {nextLevelJsonData.Name}. Combining on grid {currentGrid.name}");
                
                Vector2Int existingItemOriginalPos = new Vector2Int(existingItem.onGridPositionX, existingItem.onGridPositionY);

                // 在销毁旧物品之前，获取特效播放的位置
                Vector3 effectPosition = existingItem.transform.position;

                Debug.Log($"[TryCombineItems] Removing existingItem: {existingItem.jsonData.Name} from grid {currentGrid.name} at its pos {existingItemOriginalPos}");
                currentGrid.ClearGridReference(existingItem); 

                Debug.Log($"[TryCombineItems] Destroying GameObjects: {placedItem.jsonData.Name} (dragged/just placed) and {existingItem.jsonData.Name} (was on grid)");
                Destroy(placedItem.gameObject);   
                Destroy(existingItem.gameObject); 

                InventoryItem upgradedItem = Instantiate(itemPrefab).GetComponent<InventoryItem>();
                upgradedItem.Set(nextLevelJsonData);
                
                List<InventoryItem> displacedByUpgraded = new List<InventoryItem>();
                Debug.Log($"[TryCombineItems] Placing upgraded item {upgradedItem.jsonData.Name} at {combinePos} on grid {currentGrid.name}");
                
                bool placementOfUpgraded = currentGrid.PlaceItem(upgradedItem, combinePos.x, combinePos.y, displacedByUpgraded);
                
                if (!placementOfUpgraded) {
                    Debug.LogError($"[TryCombineItems] CRITICAL: Failed to place upgraded item {nextLevelJsonData.Name}. Upgraded item lost!");
                    Destroy(upgradedItem.gameObject); 
                    return false; 
                }
                if (displacedByUpgraded.Any(i => i != upgradedItem)) { 
                     Debug.LogWarning($"[TryCombineItems] Upgraded item unexpectedly displaced other items. This should ideally not happen if space was cleared. Handling displaced items.");
                     foreach(var dispItem in displacedByUpgraded)
                     {
                         if(dispItem == upgradedItem) continue;
                         Debug.LogWarning($"    Displaced by upgrade: {dispItem.jsonData.Name}. Moving to shop.");
                         MoveItemToShop(dispItem, null, true); 
                     }
                }

                // 播放合成特效
                if (combineEffectPrefab != null)
                {
                    Transform parentForEffect = (effectsHostPanel != null) ? effectsHostPanel : canvasTransform; // 如果指定了Panel则用Panel，否则用Canvas
                    GameObject effectInstance = Instantiate(combineEffectPrefab, effectPosition, Quaternion.identity, parentForEffect);
                    effectInstance.SetActive(true); // 确保实例化的特效是激活状态
                    effectInstance.transform.SetAsLastSibling(); // 将特效设置为其新父级（effectsHostPanel或canvasTransform）的最后一个子对象
                    Destroy(effectInstance, combineEffectDuration); 
                    Debug.Log($"[TryCombineItems] Playing combine effect (GameObject) at {effectPosition} under '{parentForEffect.name}' for {combineEffectDuration} seconds, set to render on top within its parent.");
                }
                else
                {
                    Debug.LogWarning("[TryCombineItems] combineEffectPrefab (GameObject) is not assigned in InventoryController. No effect played.");
                }

                return true; 
            }
            else {
                Debug.Log("[TryCombineItems] No next level item found.");
            }
        }
        return false; 
    }

    private void CreateRandomItem()
    {
        if (ItemDataLoader.Instance == null || ItemDataLoader.Instance.AllItems == null || ItemDataLoader.Instance.AllItems.Count == 0)
        {
            Debug.LogError("InventoryController: ItemDataLoader not ready or no items loaded.");
            return;
        }
        
        List<JsonItemData> validItemsToSpawn = ItemDataLoader.Instance.AllItems.FindAll(item => item.itemType == 1);
        if (validItemsToSpawn.Count == 0)
        {
            Debug.LogError("InventoryController: No valid items (itemType == 1) found in ItemDataLoader to create randomly.");
            return;
        }
        
        JsonItemData selectedJsonItem = ItemDataLoader.Instance.SelectRandomItemByRarity(validItemsToSpawn);
        
        if (selectedJsonItem == null) 
        {
            Debug.LogError("InventoryController: SelectRandomItemByRarity returned null. Check weights or selection logic.");
            return;
        }
        
        InventoryItem inventoryItem = Instantiate(itemPrefab).GetComponent<InventoryItem>();
        rectTransform = inventoryItem.GetComponent<RectTransform>(); 
        rectTransform.SetParent(canvasTransform); 
        rectTransform.SetAsLastSibling();

        inventoryItem.Set(selectedJsonItem); 
        selectedItem = inventoryItem; 

        Debug.Log($"[InventoryController] Created random item: {selectedJsonItem.Name} (Rarity: {selectedJsonItem.Rarity})");
    }

    private void ItemIconDrag()
    {
        if (selectedItem != null)
        {
            if (rectTransform == null) // Ensure rectTransform is valid
            {
                rectTransform = selectedItem.GetComponent<RectTransform>();
                rectTransform.SetParent(canvasTransform); // Ensure parent is correct
            }
            rectTransform.position = Input.mousePosition;
            rectTransform.SetAsLastSibling(); // Keep on top while dragging
        }
    }

    private Vector2Int GetMouseGridPosition(ItemGrid grid) {
        if (grid == null) return new Vector2Int(-1,-1);

        Vector2 mousePos = Input.mousePosition;
        if (selectedItem != null) { // Adjust mouse position if dragging for better centering
            Vector2 effectiveTileSize = grid.GetEffectiveTileSize();
            if (effectiveTileSize.x > 0 && effectiveTileSize.y > 0) {
                // This offset calculation might need fine-tuning based on item pivot and desired feel
                mousePos.x -= (selectedItem.WIDTH - 1) * effectiveTileSize.x * 0.5f; 
                mousePos.y += (selectedItem.HEIGHT - 1) * effectiveTileSize.y * 0.5f;
            }
        }
        return grid.GetTileGridPosition(mousePos);
    }
    
    public List<InventoryItem> GetPendingSearchItemsInPlayerInventories()
    {
        List<InventoryItem> pendingSearchItems = new List<InventoryItem>();
        if (playerInventoryGrids == null || playerInventoryGrids.Count == 0)
        {
            Debug.LogWarning("[InventoryController] playerInventoryGrids list is not assigned or empty.");
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
                        if (!pendingSearchItems.Contains(item)) 
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
    
    private void ShowItemDetails(InventoryItem inventoryItem)
    {
        CloseAllPopups(); // Close existing popups first

        if (inventoryItem == null)
        {
            Debug.LogError("ShowItemDetails: inventoryItem is null.");
            return;
        }

        if (inventoryItem.currentDisplayState == InventoryItem.ItemDisplayState.Hidden || 
            inventoryItem.currentDisplayState == InventoryItem.ItemDisplayState.Searching)
        {
            if (undiscoveredItemPopupPrefab != null)
            {
                if (currentUndiscoveredPopupInstance == null)
                {
                    currentUndiscoveredPopupInstance = Instantiate(undiscoveredItemPopupPrefab, canvasTransform);
                }
                currentUndiscoveredPopupInstance.SetActive(true);
                AudioManager.Instance?.PlayItemDetailsPopupSound(); // Play sound for undiscovered/searching popup
                Debug.Log($"Showing undiscovered/searching item popup for: {inventoryItem.jsonData?.Name}");
            }
            else
            {
                Debug.LogWarning("UndiscoveredItemPopupPrefab not set. Cannot show special popup.");
            }
        }
        else 
        {
            if (itemDetailsPopupPrefab == null)
            {
                Debug.LogWarning("ItemDetailsPopupPrefab not set. Cannot show details.");
                return;
            }
            if (inventoryItem.jsonData == null) 
            {
                Debug.LogError("Cannot show details for an item with no jsonData, even if not hidden.");
                return;
            }

            if (currentDetailsPopupInstance == null)
            {
                currentDetailsPopupInstance = Instantiate(itemDetailsPopupPrefab, canvasTransform);
            }
            
            Sprite itemSprite = ItemDataLoader.Instance.GetSpriteByRes(inventoryItem.jsonData.Res);
            currentDetailsPopupInstance.Show(inventoryItem.jsonData, itemSprite); 
            AudioManager.Instance?.PlayItemDetailsPopupSound(); // Play sound for full details popup
            Debug.Log($"Showing full details for: {inventoryItem.jsonData.Name}");
        }
    }

    private void CloseAllPopups()
    {
        if (currentDetailsPopupInstance != null && currentDetailsPopupInstance.gameObject.activeSelf)
        {
            currentDetailsPopupInstance.Hide(); // Assuming ItemDetailsPopup has a Hide() method that sets it inactive
        }
        if (currentUndiscoveredPopupInstance != null && currentUndiscoveredPopupInstance.activeSelf)
        {
            currentUndiscoveredPopupInstance.SetActive(false);
        }
    }

    private Vector2Int GetCurrentMouseGridPosition(out ItemGrid currentGridUnderMouseOutput)
    {
        currentGridUnderMouseOutput = null;
        RaycastResult firstValidRaycastResult = GetFirstRaycastResult(); // This now prioritizes GridInteract
        GameObject hitObject = firstValidRaycastResult.gameObject;

        if (hitObject != null)
        {
            GridInteract gridInteract = hitObject.GetComponent<GridInteract>();
            if (gridInteract != null && gridInteract.itemGrid != null) // gridInteract.itemGrid needs to be public
            {
                currentGridUnderMouseOutput = gridInteract.itemGrid;
                if (this.selectedItemGrid != currentGridUnderMouseOutput)
                {
                   this.SelectedItemGrid = currentGridUnderMouseOutput; // Use property to update and set highlight parent
                }
                // Pass the screenPosition from the raycast result for coordinate conversion
                return currentGridUnderMouseOutput.GetTileGridPosition(firstValidRaycastResult.screenPosition);
            }
        }
        
        // If no grid was hit by the current raycast, and a grid was previously selected,
        // it implies the mouse has exited that grid. GridInteract's OnPointerExit should handle setting
        // this.SelectedItemGrid to null. If it hasn't (e.g. race condition, or object destroyed),
        // we ensure it's cleared here if no new grid is detected.
        if (this.selectedItemGrid != null && currentGridUnderMouseOutput == null) {
            this.SelectedItemGrid = null; // Clear selected grid if mouse is now over nothing (or non-grid UI)
        }
        return new Vector2Int(-1, -1); 
    }
    
    private RaycastResult GetFirstRaycastResult()
    {
        PointerEventData pointerEventData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerEventData, results);
        
        // Prioritize UI elements with GridInteract first
        foreach(var r in results) {
            if (r.gameObject != null && r.gameObject.GetComponent<GridInteract>() != null) {
                return r; // Return the first one found that has GridInteract
            }
        }
        // If no GridInteract found, but other UI elements were hit, return the first one.
        // This might be a popup or other UI, not a grid.
        // If the popup should block grid interaction, this is okay.
        // If grids should always be preferred even if under other UI, more complex raycasting is needed.
        return results.Count > 0 ? results[0] : default(RaycastResult); // default is empty RaycastResult
    }

    // --- Public Methods for External Access ---
    public List<InventoryItem> GetAllPlayerInventoryItems()
    {
        List<InventoryItem> allItems = new List<InventoryItem>();
        if (playerInventoryGrids == null) 
        {
            Debug.LogWarning("InventoryController.GetAllPlayerInventoryItems: playerInventoryGrids list is null.");
            return allItems;
        }

        foreach (ItemGrid grid in playerInventoryGrids)
        {
            if (grid != null)
            {
                // Assuming ItemGrid has a public method GetAllUniqueItems() 
                // that returns List<InventoryItem> based on prior usage in ShopController.
                List<InventoryItem> itemsInGrid = grid.GetAllUniqueItems(); 
                if (itemsInGrid != null)
                {
                    allItems.AddRange(itemsInGrid);
                }
            }
            else
            {
                Debug.LogWarning("InventoryController.GetAllPlayerInventoryItems: Found a null ItemGrid in playerInventoryGrids.");
            }
        }
        return allItems;
    }

    public void NotifyGamePhaseOver()
    {
        gamePhaseOver = true;
        Debug.Log("[InventoryController] Game phase is over. Interactions are now frozen.");
        // Consider if selectedItem should be cleared or handled here as well
        if (selectedItem != null && inventoryHighlight != null) {
             inventoryHighlight.Show(false); // Hide highlight if an item was being dragged/evaluated
        }
        // If an item is selected and being dragged, it might be good to cancel the drag
        // or return the item to its original position if possible/desired.
        // For now, the Update() check will handle destroying it if still selected.
    }

    private void HandleQuickMoveToShop()
    {
        if (gamePhaseOver) return;

        InventoryItem itemToMove = null;
        ItemGrid sourceGridOfHoveredItem = null;

        if (selectedItem != null) 
        {
            itemToMove = selectedItem;
            Debug.Log($"[Shortcut E] Moving selected item {itemToMove.jsonData.Name} to shop.");
        }
        else 
        {
            ItemGrid currentHoveredGrid;
            Vector2Int mousePosOnGrid = GetCurrentMouseGridPosition(out currentHoveredGrid);
            if (currentHoveredGrid != null && currentHoveredGrid.IsValidGridPosition(mousePosOnGrid))
            {
                InventoryItem hoveredItem = currentHoveredGrid.GetItem(mousePosOnGrid.x, mousePosOnGrid.y);
                if (hoveredItem != null)
                {
                    sourceGridOfHoveredItem = currentHoveredGrid; 
                    itemToMove = sourceGridOfHoveredItem.PickUpItem(mousePosOnGrid.x, mousePosOnGrid.y); 
                    if (itemToMove != null) {
                        Debug.Log($"[Shortcut E] Picked up hovered item {itemToMove.jsonData.Name} from {sourceGridOfHoveredItem.name} to move to shop.");
                        itemToMove.transform.SetParent(canvasTransform); 
                        itemToMove.transform.SetAsLastSibling();
                    } else {
                         Debug.LogError("[Shortcut E] Failed to pick up hovered item.");
                         return; 
                    }
                }
            }
        }

        if (itemToMove != null)
        {
            bool moved = MoveItemToShop(itemToMove, null, false); // isDisplacement is false for quick move
            if (moved)
            {
                AudioManager.Instance?.PlayQuickMoveToShopSound();
            }
            // selectedItem is handled by MoveItemToShop if it was the one moved
        }
        else
        {
            Debug.Log("[Shortcut E] No item selected or hovered to move to shop.");
        }
    }

    private void HandleQuickMoveToPlayerInventory()
    {
        if (gamePhaseOver) return;
        if (playerInventoryGrids == null || playerInventoryGrids.Count == 0)
        {
            Debug.LogWarning("[Shortcut W] No player inventory grids assigned.");
            return;
        }

        InventoryItem itemToMove = null;
        ItemGrid sourceGridOfHoveredItem = null; 
        Vector2Int originalPosOfHoveredItem = Vector2Int.zero; 
        InventoryItem originalSelectedItemRef = selectedItem; 

        if (selectedItem != null) 
        {
            itemToMove = selectedItem;
            Debug.Log($"[Shortcut W] Attempting to move selected item {itemToMove.jsonData.Name} to player inventory.");
        }
        else 
        {
            ItemGrid currentHoveredGrid;
            Vector2Int mousePosOnGrid = GetCurrentMouseGridPosition(out currentHoveredGrid);
            if (currentHoveredGrid != null && currentHoveredGrid.IsValidGridPosition(mousePosOnGrid))
            {
                InventoryItem hoveredItem = currentHoveredGrid.GetItem(mousePosOnGrid.x, mousePosOnGrid.y);
                if (hoveredItem != null)
                {
                    if (playerInventoryGrids.Contains(currentHoveredGrid))
                    {
                        Debug.Log("[Shortcut W] Hovered item is already in a player inventory. W key does nothing.");
                        return;
                    }
                    sourceGridOfHoveredItem = currentHoveredGrid;
                    originalPosOfHoveredItem = new Vector2Int(hoveredItem.onGridPositionX, hoveredItem.onGridPositionY); 
                    itemToMove = sourceGridOfHoveredItem.PickUpItem(mousePosOnGrid.x, mousePosOnGrid.y); 
                    if (itemToMove != null) {
                        Debug.Log($"[Shortcut W] Picked up hovered item {itemToMove.jsonData.Name} from {sourceGridOfHoveredItem.name} to move to player inventory.");
                        itemToMove.transform.SetParent(canvasTransform);
                        itemToMove.transform.SetAsLastSibling();
                    } else {
                        Debug.LogError("[Shortcut W] Failed to pick up hovered item for move.");
                        return;
                    }
                }
            }
        }

        if (itemToMove != null)
        {
            bool placedInPlayerInventory = false;
            foreach (ItemGrid playerGrid in playerInventoryGrids)
            {
                if (playerGrid == null) continue;
                Vector2Int? availablePosition = playerGrid.FindSpaceForObject(itemToMove.jsonData.ParsedWidth, itemToMove.jsonData.ParsedHeight);
                if (availablePosition.HasValue)
                {
                    List<InventoryItem> displacedItems = new List<InventoryItem>();
                    if (playerGrid.PlaceItem(itemToMove, availablePosition.Value.x, availablePosition.Value.y, displacedItems))
                    {
                        Debug.Log($"[Shortcut W] Successfully placed {itemToMove.jsonData.Name} into player grid {playerGrid.name}.");
                        AudioManager.Instance?.PlayQuickMoveToBagSound(); // New sound for quick move success
                        placedInPlayerInventory = true;
                        if (itemToMove == originalSelectedItemRef) 
                        {
                            selectedItem = null;
                            if(inventoryHighlight != null) inventoryHighlight.Show(false);
                        }
                        if (displacedItems.Count > 0) {
                            Debug.LogWarning($"[Shortcut W] {displacedItems.Count} items displaced in player inventory by quick move of {itemToMove.jsonData.Name}. Moving them to shop.");
                            foreach(var displaced in displacedItems) {
                                if (displaced == itemToMove) continue;
                                MoveItemToShop(displaced, null, true); // Pass true for isDisplacement
                            }
                        }
                        break; 
                    }
                }
            }

            if (!placedInPlayerInventory)
            {
                Debug.Log($"[Shortcut W] No space found in player inventories for {itemToMove.jsonData.Name}.");
                ShowTemporaryMessage("背包没有足够的空间"); 
                AudioManager.Instance?.PlayQuickMoveToBagFailedSound(); // New sound for quick move fail (no space)

                if (sourceGridOfHoveredItem != null && itemToMove != originalSelectedItemRef) 
                {
                    Debug.Log($"[Shortcut W] Returning {itemToMove.jsonData.Name} to its original grid {sourceGridOfHoveredItem.name} at {originalPosOfHoveredItem}.");
                    List<InventoryItem> displacedReturning = new List<InventoryItem>();
                    if (!sourceGridOfHoveredItem.PlaceItem(itemToMove, originalPosOfHoveredItem.x, originalPosOfHoveredItem.y, displacedReturning))
                    {
                        Debug.LogWarning($"[Shortcut W] Could not return {itemToMove.jsonData.Name} to its original spot. Destroying it.");
                        Destroy(itemToMove.gameObject);
                    }
                    // No specific sound for returning to original spot after fail, or could add one.
                }
                // If it was the selectedItem, it remains selected, no specific sound for that failure case beyond the message & bag full sound.
            }
        }
        else
        {
            Debug.Log("[Shortcut W] No item selected or hovered to move to player inventory.");
        }
    }

    private void ShowTemporaryMessage(string message)
    {
        if (feedbackMessageText == null) return;

        if (activeAppearAnimationCoroutine != null)
        {
            StopCoroutine(activeAppearAnimationCoroutine);
            activeAppearAnimationCoroutine = null;
        }
        if (activeFeedbackMessageCoroutine != null)
        {
            StopCoroutine(activeFeedbackMessageCoroutine);
            activeFeedbackMessageCoroutine = null;
        }

        feedbackMessageText.text = message;
        feedbackMessageText.gameObject.SetActive(true);
        AudioManager.Instance?.PlayFeedbackMessagePopupSound(); // Play sound when feedback message appears
        
        Color originalColor = feedbackMessageText.color;
        feedbackMessageText.color = new Color(originalColor.r, originalColor.g, originalColor.b, 1f);

        activeAppearAnimationCoroutine = StartCoroutine(AnimateMessageAppearCoroutine());
        activeFeedbackMessageCoroutine = StartCoroutine(HideMessageAfterDelayCoroutine());
    }

    private IEnumerator AnimateMessageAppearCoroutine()
    {
        if (feedbackMessageText == null) yield break;

        Transform textTransform = feedbackMessageText.transform;
        textTransform.localScale = Vector3.one * feedbackAppearScaleFactor;
        
        float timer = 0f;
        while (timer < feedbackAppearAnimDuration)
        {
            if (feedbackMessageText == null) yield break; // Object might be destroyed
            textTransform.localScale = Vector3.Lerp(
                Vector3.one * feedbackAppearScaleFactor, 
                Vector3.one, 
                timer / feedbackAppearAnimDuration
            );
            timer += Time.deltaTime;
            yield return null;
        }
        if (feedbackMessageText != null) textTransform.localScale = Vector3.one; // Ensure final scale is exactly 1
        activeAppearAnimationCoroutine = null;
    }

    private IEnumerator HideMessageAfterDelayCoroutine()
    {
        yield return new WaitForSeconds(feedbackMessageDuration);
        
        if (feedbackMessageText == null || !feedbackMessageText.gameObject.activeSelf) 
        {
            activeFeedbackMessageCoroutine = null;
            yield break; 
        }

        Color originalColor = feedbackMessageText.color;
        float timer = 0f;

        while (timer < feedbackDisappearFadeDuration)
        {
            if (feedbackMessageText == null) yield break; // Object might be destroyed
            float alpha = Mathf.Lerp(originalColor.a, 0f, timer / feedbackDisappearFadeDuration);
            feedbackMessageText.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            timer += Time.deltaTime;
            yield return null;
        }

        if (feedbackMessageText != null) // Final cleanup
        {
            feedbackMessageText.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f); // Ensure fully transparent
            feedbackMessageText.gameObject.SetActive(false);
            feedbackMessageText.color = originalColor; // Reset color for next time (alpha will be set to 1 in ShowTempMsg)
        }
        activeFeedbackMessageCoroutine = null;
    }

    // --- Save and Load Logic ---
    public void SaveInventoryState()
    {
        if (GameDataManager.Instance == null)
        {
            Debug.LogError("[InventoryController.SaveInventoryState] GameDataManager.Instance is null. Cannot save inventory.");
            return;
        }

        GameDataManager.Instance.persistedPlayerItems.Clear(); // Clear previous save data

        if (playerInventoryGrids == null || playerInventoryGrids.Count == 0)
        {
            Debug.LogWarning("[InventoryController.SaveInventoryState] No playerInventoryGrids assigned. Nothing to save.");
            GameDataManager.Instance.MarkDataAsPersisted(); // Still mark as persisted so attributes might save
            return;
        }

        Debug.Log($"[InventoryController.SaveInventoryState] Saving inventory items from {playerInventoryGrids.Count} grids.");
        foreach (ItemGrid grid in playerInventoryGrids)
        {
            if (grid == null)
            {
                Debug.LogWarning("[InventoryController.SaveInventoryState] Encountered a null ItemGrid in playerInventoryGrids. Skipping.");
                continue;
            }

            List<InventoryItem> itemsInGrid = grid.GetAllUniqueItems();
            Debug.Log($"[InventoryController.SaveInventoryState] Grid '{grid.gridId}' has {itemsInGrid.Count} unique items to save.");
            foreach (InventoryItem item in itemsInGrid)
            {
                if (item != null && item.jsonData != null)
                {
                    PersistedItemInfo persistedInfo = new PersistedItemInfo(
                        grid.gridId,
                        item.jsonData.Id,
                        item.onGridPositionX,
                        item.onGridPositionY,
                        item.rotated,
                        item.currentDisplayState
                    );
                    GameDataManager.Instance.persistedPlayerItems.Add(persistedInfo);
                    Debug.Log($"    Saved: {item.jsonData.Name} (ID: {item.jsonData.Id}) from grid '{grid.gridId}' at ({item.onGridPositionX},{item.onGridPositionY}), Rotated: {item.rotated}, State: {item.currentDisplayState}");
                }
            }
        }
        GameDataManager.Instance.MarkDataAsPersisted();
        Debug.Log($"[InventoryController.SaveInventoryState] Inventory save complete. Total items persisted: {GameDataManager.Instance.persistedPlayerItems.Count}");
    }

    public void LoadInventoryState()
    {
        if (hasLoadedInventory) 
        {
            Debug.LogWarning("[InventoryController.LoadInventoryState] Inventory already loaded. Aborting to prevent duplicates.");
            return;
        }

        if (GameDataManager.Instance == null || !GameDataManager.Instance.HasPersistedData)
        {
            Debug.LogWarning("[InventoryController.LoadInventoryState] GameDataManager.Instance is null or no persisted data found. Cannot load inventory.");
            return;
        }

        if (ItemDataLoader.Instance == null)
        {
            Debug.LogError("[InventoryController.LoadInventoryState] ItemDataLoader.Instance is null. Cannot load item details.");
            return;
        }

        if (playerInventoryGrids == null || playerInventoryGrids.Count == 0)
        {
            Debug.LogWarning("[InventoryController.LoadInventoryState] No playerInventoryGrids assigned. Cannot place loaded items.");
            return;
        }
        
        // Optional: Clear any items that might be in the grids by default in the new scene before loading
        // This depends on your scene setup. If grids are meant to be empty, this is a good idea.
        ClearAllPlayerInventoryGrids();
        Debug.Log($"[InventoryController.LoadInventoryState] Cleared existing items from player grids before loading.");

        Debug.Log($"[InventoryController.LoadInventoryState] Loading {GameDataManager.Instance.persistedPlayerItems.Count} items.");

        foreach (PersistedItemInfo persistedInfo in GameDataManager.Instance.persistedPlayerItems)
        {
            ItemGrid targetGrid = playerInventoryGrids.FirstOrDefault(g => g != null && g.gridId == persistedInfo.gridId);
            if (targetGrid == null)
            {
                Debug.LogWarning($"[InventoryController.LoadInventoryState] Could not find ItemGrid with ID '{persistedInfo.gridId}' for item ID {persistedInfo.itemId}. Item cannot be loaded.");
                continue;
            }

            JsonItemData itemJsonData = ItemDataLoader.Instance.AllItems.FirstOrDefault(itemData => itemData.Id == persistedInfo.itemId);
            if (itemJsonData == null)
            {
                Debug.LogWarning($"[InventoryController.LoadInventoryState] Could not find JsonItemData for item ID {persistedInfo.itemId}. Item cannot be loaded.");
                continue;
            }

            if (itemPrefab == null)
            {
                Debug.LogError("[InventoryController.LoadInventoryState] itemPrefab is not assigned! Cannot instantiate items.");
                return; // Critical error
            }

            InventoryItem newItem = Instantiate(itemPrefab).GetComponent<InventoryItem>();
            if (canvasTransform == null) 
            {
                 Debug.LogError("[InventoryController.LoadInventoryState] canvasTransform is null! Items will not be parented correctly.");
                 // Attempt to find it again, though ideally it should be set in Awake/Start of InventoryController
                 Canvas parentCanvas = GetComponentInParent<Canvas>();
                 if (parentCanvas != null) canvasTransform = parentCanvas.transform;
                 if (canvasTransform == null) { Destroy(newItem.gameObject); continue; }
            }
            newItem.transform.SetParent(canvasTransform); // Parent to canvas first for Set() to work correctly if it uses GetComponentInParent<Canvas>
            
            newItem.Set(itemJsonData);
            newItem.rotated = persistedInfo.isRotated; // Apply rotation before placing if it affects size
            // newItem.SetDisplayState(persistedInfo.displayState, true); // Set might default to Revealed, override if needed

            // Ensure the item's RectTransform is updated after rotation for correct placement dimensions
            if(newItem.rotated) newItem.Rotate(); // Call rotate to flip dimensions if it was saved rotated but Set() doesn't handle initial rotated state size.
            if(!newItem.rotated) { // If not rotated, ensure its size is set correctly by Set() without a redundant Rotate() call
                Vector2 size = new Vector2(newItem.jsonData.ParsedWidth * ItemGrid.tileSizeWidth, newItem.jsonData.ParsedHeight * ItemGrid.tileSizeHeight); 
                newItem.GetComponent<RectTransform>().sizeDelta = size;
            }

            List<InventoryItem> tempListForDisplaced = new List<InventoryItem>(); // Temporary list for the PlaceItem call
            bool placed = targetGrid.PlaceItem(newItem, persistedInfo.positionX, persistedInfo.positionY, tempListForDisplaced);
            
            if (tempListForDisplaced.Count > 0)
            {
                Debug.LogWarning($"[InventoryController.LoadInventoryState] When loading {newItem.jsonData.Name}, {tempListForDisplaced.Count} items were unexpectedly displaced. This shouldn't happen if grids were cleared properly. Destroying displaced items.");
                foreach(var displacedItem in tempListForDisplaced)
                {
                    if(displacedItem != null && displacedItem.gameObject != null) Destroy(displacedItem.gameObject);
                }
            }

            if (placed)
            {
                // Set display state AFTER placement, as some logic might depend on item being on grid
                newItem.SetDisplayState(persistedInfo.displayState, true);
                Debug.Log($"    Loaded and placed: {itemJsonData.Name} (ID: {itemJsonData.Id}) onto grid '{targetGrid.gridId}' at ({persistedInfo.positionX},{persistedInfo.positionY}), Rotated: {persistedInfo.isRotated}, State: {persistedInfo.displayState}");
            }
            else
            {
                Debug.LogWarning($"[InventoryController.LoadInventoryState] Failed to place item {itemJsonData.Name} (ID: {itemJsonData.Id}) onto grid '{targetGrid.gridId}' at ({persistedInfo.positionX},{persistedInfo.positionY}). Item might be lost or already an item there.");
                Destroy(newItem.gameObject); // Clean up unplaced item
            }
        }
        hasLoadedInventory = true;
        Debug.Log("[InventoryController.LoadInventoryState] Inventory load complete.");
    }

    // Helper method to clear all items from player inventory grids
    public void ClearAllPlayerInventoryGrids()
    {
        if (playerInventoryGrids == null) return;
        foreach(ItemGrid grid in playerInventoryGrids)
        {
            if (grid != null)
            {
                List<InventoryItem> itemsToClear = grid.GetAllUniqueItems();
                foreach(InventoryItem item in itemsToClear)
                {
                    if (item != null)
                    {
                        grid.ClearGridReference(item); // Important to remove from grid's internal slot array
                        Destroy(item.gameObject);
                    }
                }
            }
        }
        Debug.Log("[InventoryController] All player inventory grids cleared.");
    }
    // --- End Save and Load Logic ---
}