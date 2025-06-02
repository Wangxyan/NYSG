using UnityEngine;
using System.Collections; // Required for Coroutines
using System.Collections.Generic;
using System.Linq; // For OrderBy
using UnityEngine.UI; // 添加对 UI 系统的引用

[RequireComponent(typeof(AudioSource))] // Ensure AudioSource exists
public class ShopController : MonoBehaviour
{
    [Header("Shop Configuration")]
    [Tooltip("The ItemGrid component used for the shop display.")]
    public ItemGrid shopItemGrid; // 商店的ItemGrid

    [Tooltip("Width of the shop in 3x3 grid units.")]
    public int shopWidthInShopUnits = 2; // 商店宽度 (单位: 3x3格子)

    [Tooltip("Height of the shop in 3x3 grid units.")]
    public int shopHeightInShopUnits = 3; // 商店高度 (单位: 3x3格子)

    [Header("Search Animation & Sound")]
    [Tooltip("Base search time for Rarity 0 items (seconds).")]
    [SerializeField] private float baseSearchTime = 1f;
    [Tooltip("Additional search time per rarity level (seconds).")]
    [SerializeField] private float searchTimePerRarity = 0.5f;
    [Tooltip("Sound played when an item search is complete. Array index corresponds to Rarity. Ensure size matches max rarity.")]
    [SerializeField] private AudioClip[] searchCompleteSoundsByRarity;

    [Header("References")]
    [Tooltip("Reference to the main InventoryController.")]
    public InventoryController inventoryController;

    [Tooltip("Optional button to trigger shop refresh.")]
    public Button refreshButton; // 刷新按钮 (可选)

    private const int SHOP_UNIT_SIZE = 3; // 每个商店单元的大小 (3x3)
    private Queue<InventoryItem> itemsToSearchQueue = new Queue<InventoryItem>();
    private Coroutine currentSearchCoroutine;
    private AudioSource audioSource;
    private List<InventoryItem> currentShopItemsInternal = new List<InventoryItem>(); // Renamed to avoid confusion with any public property if ever added

    void Start()
    {
        audioSource = GetComponent<AudioSource>();

        if (inventoryController == null)
        {
            inventoryController = FindObjectOfType<InventoryController>();
            if (inventoryController == null)
            {
                Debug.LogError("ShopController: InventoryController not found in scene!");
                enabled = false; // 禁用此脚本如果找不到 InventoryController
                return;
            }
        }

        if (shopItemGrid == null)
        {
            Debug.LogError("ShopController: Shop ItemGrid is not assigned!");
            enabled = false; // 禁用此脚本如果 ItemGrid 未分配
            return;
        }

        // 配置 ItemGrid 的实际尺寸
        // 注意：ItemGrid 自身应该通过其 gridSizeWidth 和 gridSizeHeight 属性来设置其大小。
        // ShopController 确保这些值与 shopWidthInShopUnits * SHOP_UNIT_SIZE 匹配。
        // 这一步最好在编辑器中手动设置 ItemGrid，或者由 ItemGrid 的 Start/Awake 逻辑处理。
        // 这里我们假设 ItemGrid 已经被正确设置为 shopWidthInShopUnits * SHOP_UNIT_SIZE 和 shopHeightInShopUnits * SHOP_UNIT_SIZE

        if (refreshButton != null)
        {
            refreshButton.onClick.AddListener(RefreshShop);
        }

        RefreshShop(); // 初始加载一次商店物品
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.D))
        {
            RefreshShop();
        }
    }

    public void RefreshShop()
    {
        Debug.Log("[ShopController] RefreshShop Initiated.");
        if (shopItemGrid == null || 
            ItemDataLoader.Instance == null || 
            ItemDataLoader.Instance.AllItems == null || 
            ItemDataLoader.Instance.AllItems.Count == 0 || 
            inventoryController == null)
        {
            Debug.LogWarning("ShopController: Dependencies not met (shopGrid, ItemDataLoader, or InventoryController).");
            return;
        }

        if (currentSearchCoroutine != null)
        {
            StopCoroutine(currentSearchCoroutine);
            currentSearchCoroutine = null;
            Debug.Log("[ShopController] Stopped ongoing search coroutine.");
        }
        itemsToSearchQueue.Clear();
        Debug.Log("[ShopController] Search queue cleared.");

        // 1. Identify player items already pending search (these should not be destroyed by shop clear)
        List<InventoryItem> playerItemsPendingSearch = inventoryController.GetPendingSearchItemsInPlayerInventories();
        Debug.Log($"[ShopController] Found {playerItemsPendingSearch.Count} items in player inventories pending search.");

        // 2. Clear old items that were part of THIS shop's previous generation
        Debug.Log($"[ShopController] Clearing {currentShopItemsInternal.Count} internally tracked shop items.");
        foreach (InventoryItem oldShopItem in new List<InventoryItem>(currentShopItemsInternal)) // Iterate copy
        {
            if (oldShopItem != null)
            {
                if (shopItemGrid != null) shopItemGrid.ClearGridReference(oldShopItem);
                Destroy(oldShopItem.gameObject);
                // Debug.Log($"[ShopController] Destroyed internally tracked old shop item: {oldShopItem.jsonData?.Name}");
            }
        }
        currentShopItemsInternal.Clear();

        // 3. Fallback: Clean any other items physically on the shop grid 
        //    that are NOT player's pending items and NOT part of a fresh generation.
        if (shopItemGrid != null)
        {
            List<InventoryItem> itemsCurrentlyOnShopGrid = shopItemGrid.GetAllUniqueItems();
            Debug.Log($"[ShopController] Fallback check: Found {itemsCurrentlyOnShopGrid.Count} unique items on shop grid.");
            foreach (InventoryItem itemOnGrid in itemsCurrentlyOnShopGrid)
            {
                if (itemOnGrid != null && !playerItemsPendingSearch.Contains(itemOnGrid))
                {
                    // This item is on the shop grid, not a player's pending item, 
                    // and wasn't in currentShopItemsInternal (or it would have been destroyed already).
                    // It's likely an orphan and should be removed.
                    Debug.LogWarning($"[ShopController] Fallback: Destroying orphaned item {itemOnGrid.jsonData?.Name} found on shop grid.");
                    shopItemGrid.ClearGridReference(itemOnGrid);
                    Destroy(itemOnGrid.gameObject);
                }
            }
        }
        
        // 4. Enqueue player's pending items (they are already in the correct display state)
        // Optional: Sort playerPendingItems if needed. For now, using order from controller.
        foreach (InventoryItem playerItem in playerItemsPendingSearch)
        {
            itemsToSearchQueue.Enqueue(playerItem);
            Debug.Log($"[ShopController] Enqueued player inventory item for search: {playerItem.jsonData?.Name} (State: {playerItem.currentDisplayState})");
        }

        // 5. Generate and enqueue new shop items
        List<InventoryItem> newlyGeneratedShopItems = new List<InventoryItem>();
        for (int yShopUnit = 0; yShopUnit < shopHeightInShopUnits; yShopUnit++)
        {
            for (int xShopUnit = 0; xShopUnit < shopWidthInShopUnits; xShopUnit++)
            {
                InventoryItem generatedItem = GenerateItemForShopUnit(xShopUnit * SHOP_UNIT_SIZE, yShopUnit * SHOP_UNIT_SIZE);
                if (generatedItem != null) { newlyGeneratedShopItems.Add(generatedItem); }
            }
        }
        
        newlyGeneratedShopItems = newlyGeneratedShopItems.OrderBy(item => item.onGridPositionY).ThenBy(item => item.onGridPositionX).ToList();
        Debug.Log($"[ShopController] Generated and sorted {newlyGeneratedShopItems.Count} new items for the shop.");

        foreach (var newShopItem in newlyGeneratedShopItems)
        {
            newShopItem.SetDisplayState(InventoryItem.ItemDisplayState.Hidden, true);
            currentShopItemsInternal.Add(newShopItem); // Track for next clear
            itemsToSearchQueue.Enqueue(newShopItem);
            Debug.Log($"[ShopController] Enqueued new shop item for search: {newShopItem.jsonData?.Name}");
        }

        // 6. Start search process
        Debug.Log($"[ShopController] Total items in search queue: {itemsToSearchQueue.Count}");
        StartNextSearchInQueue();
    }

    private InventoryItem GenerateItemForShopUnit(int unitStartX, int unitStartY)
    {
        if (ItemDataLoader.Instance == null || ItemDataLoader.Instance.AllItems == null || ItemDataLoader.Instance.AllItems.Count == 0)
        {
            Debug.LogWarning("ShopController: ItemDataLoader not ready or no items loaded for shop.");
            return null;
        }

        List<JsonItemData> suitableAndValidItems = new List<JsonItemData>();
        foreach (JsonItemData itemJsonData in ItemDataLoader.Instance.AllItems)
        {
            if (itemJsonData.itemType == 1 &&
                itemJsonData.Level == 1 &&
                itemJsonData.ParsedWidth <= SHOP_UNIT_SIZE &&
                itemJsonData.ParsedHeight <= SHOP_UNIT_SIZE)
            {
                suitableAndValidItems.Add(itemJsonData);
            }
        }

        if (suitableAndValidItems.Count == 0)
        {
            Debug.LogWarning("ShopController: No suitable items (type 1, level 1, <= 3x3) found.");
            return null;
        }

        JsonItemData selectedJsonItemData = ItemDataLoader.Instance.SelectRandomItemByRarity(suitableAndValidItems);
        if (selectedJsonItemData == null)
        {
            Debug.LogError("ShopController: SelectRandomItemByRarity returned null.");
            return null;
        }

        if (inventoryController == null || inventoryController.itemPrefab == null)
        {
            Debug.LogError("ShopController: InventoryController or its itemPrefab is not assigned!");
            return null;
        }
        GameObject itemGO = Instantiate(inventoryController.itemPrefab);
        InventoryItem newItem = itemGO.GetComponent<InventoryItem>();
        newItem.Set(selectedJsonItemData);

        int targetX = unitStartX + (SHOP_UNIT_SIZE - newItem.WIDTH) / 2;
        int targetY = unitStartY + (SHOP_UNIT_SIZE - newItem.HEIGHT); 

        if (!shopItemGrid.BoundryCheck(targetX, targetY, newItem.WIDTH, newItem.HEIGHT))
        {
            Debug.LogWarning($"ShopController: Calculated position for {selectedJsonItemData.Name} at ({targetX},{targetY}) is out of bounds. Destroying.");
            Destroy(itemGO);
            return null;
        }

        List<InventoryItem> displacedItems = new List<InventoryItem>();
        bool placed = shopItemGrid.PlaceItem(newItem, targetX, targetY, displacedItems);

        if (!placed)
        {
            Debug.LogWarning($"ShopController: Could not place {selectedJsonItemData.Name} at ({targetX},{targetY}). Destroying.");
            Destroy(itemGO);
            return null;
        }
        else
        {
            // newItem.SetDisplayState(InventoryItem.ItemDisplayState.Hidden, true); // This is now done in RefreshShop
            // DO NOT add to currentShopItems here, RefreshShop will do it after collecting all generated items.
            // DO NOT add to itemsToSearchQueue here.
            Debug.Log($"[ShopController] Generated item (for shop queue): {selectedJsonItemData.Name} (Rarity: {selectedJsonItemData.Rarity}) at ({targetX},{targetY}).");
            if (displacedItems.Count > 0)
            {
                Debug.LogWarning($"ShopController: {displacedItems.Count} items displaced by {selectedJsonItemData.Name}. Destroying them.");
                foreach (var dItem in displacedItems) { if (dItem != null) Destroy(dItem.gameObject); }
            }
            return newItem;
        }
    }

    private void StartNextSearchInQueue()
    {
        if (itemsToSearchQueue.Count > 0)
        {
            InventoryItem itemToSearch = itemsToSearchQueue.Dequeue();
            if (itemToSearch != null && itemToSearch.gameObject != null && itemToSearch.gameObject.activeInHierarchy) 
            {
                currentSearchCoroutine = StartCoroutine(SearchItemCoroutine(itemToSearch));
            }
            else 
            {
                 Debug.LogWarning($"[ShopController] Dequeued item but it's invalid. Skipping. Trying next.");
                 StartNextSearchInQueue(); 
            }
        }
        else { currentSearchCoroutine = null; Debug.Log("[ShopController] Search queue depleted."); }
    }

    private IEnumerator SearchItemCoroutine(InventoryItem item)
    {
        if (item == null || item.jsonData == null || item.gameObject == null || !item.gameObject.activeInHierarchy)
        {
            Debug.LogWarning("[ShopController] SearchItemCoroutine: Item invalid at start. Aborting search for it.");
            StartNextSearchInQueue(); 
            yield break;
        }
        
        string parentGridName = (item.transform.parent != null && item.transform.parent.GetComponent<ItemGrid>() != null) ? item.transform.parent.name : "Unknown (moved or not on a grid)";
        Debug.Log($"[ShopController] Starting search for {item.jsonData.Name} (Rarity: {item.jsonData.Rarity}, State: {item.currentDisplayState}) on grid '{parentGridName}'.");
        
        item.SetDisplayState(InventoryItem.ItemDisplayState.Searching, true); // Ensure it's in searching state

        float searchDuration = baseSearchTime + (item.jsonData.Rarity * searchTimePerRarity);
        yield return new WaitForSeconds(Mathf.Max(0.1f, searchDuration));
        
        if (item == null || item.gameObject == null || !item.gameObject.activeInHierarchy) // Re-check after wait
        {
            Debug.LogWarning($"[ShopController] Item {item?.jsonData?.Name} became invalid during search. Aborting reveal.");
            StartNextSearchInQueue(); 
            yield break;
        }

        Debug.Log($"[ShopController] Search complete for {item.jsonData.Name}. Revealing.");
        item.SetDisplayState(InventoryItem.ItemDisplayState.Revealed, true);

        if (audioSource != null && searchCompleteSoundsByRarity != null)
        {
            int r = item.jsonData.Rarity;
            if (r >= 0 && r < searchCompleteSoundsByRarity.Length && searchCompleteSoundsByRarity[r] != null) audioSource.PlayOneShot(searchCompleteSoundsByRarity[r]);
            else if (r >= searchCompleteSoundsByRarity.Length && searchCompleteSoundsByRarity.Length > 0) audioSource.PlayOneShot(searchCompleteSoundsByRarity[searchCompleteSoundsByRarity.Length -1]);
        }
        StartNextSearchInQueue();
    }
    
    /// <summary>
    /// Called by InventoryController when an item is moved back to the shop.
    /// Ensures the item is immediately visible and not part of the search queue.
    /// It finds space for the item and places it.
    /// </summary>
    public void AddItemToShopDirectly(InventoryItem item)
    {
        if (item == null || shopItemGrid == null) { if(item!=null) Destroy(item.gameObject); return; }
        Vector2Int? pos = shopItemGrid.FindSpaceForObject(item);
        if (pos.HasValue)
        {
            List<InventoryItem> displaced = new List<InventoryItem>(); 
            if (shopItemGrid.PlaceItem(item, pos.Value.x, pos.Value.y, displaced))
            {
                item.SetDisplayState(InventoryItem.ItemDisplayState.Revealed, true);
                if (!currentShopItemsInternal.Contains(item)) { currentShopItemsInternal.Add(item); } // Track if it ends up in shop
                foreach (var d in displaced) { if (d != null) { shopItemGrid.ClearGridReference(d); Destroy(d.gameObject); currentShopItemsInternal.Remove(d); } }
            }
            else { Destroy(item.gameObject); }
        }
        else { Destroy(item.gameObject); }
    }

    /// <summary>
    /// Called by InventoryController when an item is picked up from the shop grid.
    /// This removes the item from the shop's internal tracking list so it won't be destroyed on refresh.
    /// </summary>
    /// <param name="item">The item that was picked up from the shop.</param>
    public void NotifyItemPickedUpFromShop(InventoryItem item)
    {
        if (item != null && currentShopItemsInternal.Contains(item))
        {
            currentShopItemsInternal.Remove(item);
            Debug.Log($"[ShopController] Item {item.jsonData?.Name} picked from shop, untracked from internal list.");
        }
        // Note: If this item was in itemsToSearchQueue, SearchItemCoroutine's validity checks should handle it.
    }
} 