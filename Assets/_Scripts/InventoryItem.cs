using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 库存物品逻辑
/// </summary>
public class InventoryItem : MonoBehaviour
{
    /// <summary>
    /// 物品的显示状态
    /// </summary>
    public enum ItemDisplayState 
    {
        Revealed, // 正常显示物品图标
        Hidden,   // 显示为黑色方块
        Searching // 视觉上可能与Hidden相同，但表示正在进行搜索动画
    }

    public JsonItemData jsonData { get; private set; }
    public ItemDisplayState currentDisplayState { get; private set; } = ItemDisplayState.Revealed;

    [Header("UI References")]
    [SerializeField] private Image itemImage; // 主物品图标
    [SerializeField] private Image blackOverlayImage; // 用于隐藏状态的黑色覆盖层
    [Tooltip("The GameObject for the search/hidden overlay mask.")]
    [SerializeField] private GameObject searchOverlay; // 在Inspector中指定这个遮罩对象

    [Tooltip("The GameObject for the magnifying glass icon, child of SearchOverlay.")]
    [SerializeField] private GameObject magnifyingGlassIconGO; // 在Inspector中指定放大镜图标对象

    private Animator magnifyingGlassAnimator;

    [Header("Rarity Background")]
    [Tooltip("Assign the child Image component used for rarity background. Set its RectTransform to stretch.")]
    [SerializeField] private Image rarityBackgroundImage;

    // Define colors for rarities. Adjust as needed. Alpha is set to 0.5 for some transparency.
    // Index corresponds to Rarity value (0 = Common, 1 = Uncommon, etc.)
    private static readonly Color[] rarityColors = new Color[]
    {
        new Color(0.8f, 0.8f, 0.8f, 0.3f),  // Rarity 0 (Common - light grey, more transparent)
        new Color(0.4f, 0.8f, 0.4f, 0.4f),  // Rarity 1 (Uncommon - green)
        new Color(0.4f, 0.6f, 0.9f, 0.4f),  // Rarity 2 (Rare - blue)
        new Color(0.7f, 0.4f, 0.9f, 0.4f),  // Rarity 3 (Very Rare - purple)
        new Color(0.9f, 0.65f, 0.3f, 0.4f), // Rarity 4 (Epic - orange)
        new Color(0.9f, 0.3f, 0.3f, 0.4f),  // Rarity 5 (Legendary - red)
        new Color(0.9f, 0.8f, 0.3f, 0.5f)   // Rarity 6 (Mythic - gold, slightly less transparent)
        // Add more colors if your Rarity goes higher
    };

    /// <summary>
    /// 物品高度 (考虑旋转)
    /// </summary>
    public int HEIGHT
    {
        get
        {
            if (jsonData == null) return 1; // 安全回退
            return !rotated ? jsonData.ParsedHeight : jsonData.ParsedWidth;
        }
    }

    /// <summary>
    /// 物品宽度 (考虑旋转)
    /// </summary>
    public int WIDTH
    {
        get
        {
            if (jsonData == null) return 1; // 安全回退
            return !rotated ? jsonData.ParsedWidth : jsonData.ParsedHeight;
        }
    }

    /// <summary>
    /// 物品在网格的X轴位置
    /// </summary>
    public int onGridPositionX;

    /// <summary>
    /// 物品在网格的Y轴位置
    /// </summary>
    public int onGridPositionY;

    /// <summary>
    /// 物品是否旋转过
    /// </summary>
    public bool rotated = false;

    private void Awake()
    {
        // 确保引用已设置，如果未在Inspector中设置，则尝试获取
        if (itemImage == null) itemImage = GetComponent<Image>();
        if (itemImage == null) Debug.LogError("InventoryItem: itemImage is null and GetComponent<Image> failed.", this);
        
        // blackOverlayImage 必须在Inspector中设置，因为它是一个特定的子对象
        if (blackOverlayImage == null) Debug.LogError("InventoryItem: blackOverlayImage is not assigned in the Inspector!", this);
        
        // 初始时，根据默认状态更新视觉
        if (blackOverlayImage != null) blackOverlayImage.gameObject.SetActive(false);
        if (itemImage != null) itemImage.gameObject.SetActive(true);

        if (searchOverlay == null)
        {
            Debug.LogWarning("SearchOverlay not assigned in Inspector for item: " + (jsonData != null ? jsonData.Name : "Unassigned Item"), this.gameObject);
        }

        if (magnifyingGlassIconGO != null)
        {
            magnifyingGlassAnimator = magnifyingGlassIconGO.GetComponent<Animator>();
            if (magnifyingGlassAnimator == null)
            {
                Debug.LogError("MagnifyingGlassIconGO on " + (jsonData != null ? jsonData.Name : "Unassigned Item") + " does not have an Animator component!", this.gameObject);
            }
        }
        else
        {
            Debug.LogWarning("MagnifyingGlassIconGO not assigned in Inspector for item: " + (jsonData != null ? jsonData.Name : "Unassigned Item"), this.gameObject);
        }

        if (rarityBackgroundImage == null)
        {
            Debug.LogWarning("InventoryItem: rarityBackgroundImage is not assigned in the Inspector! Rarity background will not be shown.", this);
        }
        else
        {
            rarityBackgroundImage.gameObject.SetActive(true); // Ensure it's active
        }

        // 根据初始 currentDisplayState 设置视觉状态
        // 确保在 Awake 的末尾或 Start 中调用一次 SetDisplayState 以应用初始状态
        SetDisplayState(currentDisplayState, true); // forceUpdate = true
    }

    /// <summary>
    /// 设置物品数据
    /// </summary>
    /// <param name="itemJsonData">物品的JSON数据</param>
    public void Set(JsonItemData itemJsonData)
    {
        jsonData = itemJsonData;
        if (jsonData == null) 
        {
            Debug.LogError("InventoryItem.Set called with null JsonItemData", this);
            if(itemImage != null) itemImage.sprite = null; 
            if(rarityBackgroundImage != null) rarityBackgroundImage.gameObject.SetActive(false); // Hide background if no data
            return;
        }
        else
        {
            if(rarityBackgroundImage != null) rarityBackgroundImage.gameObject.SetActive(true);
        }

        // Log parsed dimensions
        Debug.Log($"[InventoryItem.Set] Item: {jsonData.Name}, Type: '{jsonData.Type}', ParsedWidth: {jsonData.ParsedWidth}, ParsedHeight: {jsonData.ParsedHeight}"); 

        if (ItemDataLoader.Instance != null)
        {
            if(itemImage != null) itemImage.sprite = ItemDataLoader.Instance.GetSpriteByRes(jsonData.Res);
        }
        else
        {   
            if(itemImage != null) itemImage.sprite = null; 
            Debug.LogError("ItemDataLoader.Instance is null. Cannot load sprite for item: " + jsonData.Name, this);
        }

        // Calculate size based on grid tile size and item's parsed dimensions
        float actualTileSizeWidth = ItemGrid.tileSizeWidth;   // Using direct const, assuming ItemGrid scaling is handled elsewhere or not an issue for this part
        float actualTileSizeHeight = ItemGrid.tileSizeHeight;
        // If your ItemGrid itself can be scaled, and tileSizeWidth/Height are base values, you might need an effective tile size here.
        // For now, assume these are the correct final pixel sizes for a 1x1 cell.

        Vector2 size = new Vector2();
        size.x = jsonData.ParsedWidth * actualTileSizeWidth; 
        size.y = jsonData.ParsedHeight * actualTileSizeHeight; 
        GetComponent<RectTransform>().sizeDelta = size;
        
        // Log the calculated sizeDelta
        Debug.Log($"[InventoryItem.Set] Item: {jsonData.Name}, Applied sizeDelta: ({size.x}, {size.y}) based on tileW: {actualTileSizeWidth}, tileH: {actualTileSizeHeight}");

        // Set rarity background color
        if (rarityBackgroundImage != null)
        {
            if (jsonData.Rarity >= 0 && jsonData.Rarity < rarityColors.Length)
            {
                rarityBackgroundImage.color = rarityColors[jsonData.Rarity];
            }
            else
            {
                // Default color for undefined rarities (e.g., very transparent or a specific debug color)
                rarityBackgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 0.2f); 
                Debug.LogWarning($"[InventoryItem.Set] Item '{jsonData.Name}' (ID: {jsonData.Id}) has Rarity {jsonData.Rarity}, which is out of defined rarityColors range. Using default background.");
            }
        }

        // Default to revealed. ShopController will override for new shop items.
        SetDisplayState(ItemDisplayState.Revealed, true); 
    }

    /// <summary>
    /// 设置物品的显示状态并更新视觉效果。
    /// </summary>
    /// <param name="newState">新的显示状态</param>
    /// <param name="forceUpdate">是否强制更新视觉，即使状态未改变</param>
    public void SetDisplayState(ItemDisplayState newState, bool forceUpdate = false)
    {
        if (currentDisplayState == newState && !forceUpdate)
        {
            return;
        }
        // Debug.Log($"[InventoryItem] {jsonData?.Name} Setting DisplayState from {currentDisplayState} to {newState}. Force: {forceUpdate}");
        currentDisplayState = newState;

        bool showOverlay = newState == ItemDisplayState.Hidden || newState == ItemDisplayState.Searching;
        bool showMagnifyingGlass = newState == ItemDisplayState.Searching;

        if (searchOverlay != null)
        {
            if (searchOverlay.activeSelf != showOverlay)
            {
                searchOverlay.SetActive(showOverlay);
            }
        }

        if (magnifyingGlassIconGO != null)
        {
            if (magnifyingGlassIconGO.activeSelf != showMagnifyingGlass)
            {
                magnifyingGlassIconGO.SetActive(showMagnifyingGlass);
            }

            if (magnifyingGlassAnimator != null)
            {
                // 只有在需要显示放大镜时才启用Animator组件本身，否则禁用以节省性能
                if (magnifyingGlassAnimator.enabled != showMagnifyingGlass)
                {
                    magnifyingGlassAnimator.enabled = showMagnifyingGlass;
                }

                // 如果Animator被启用 (即showMagnifyingGlass为true), 它会自动播放默认的循环动画。
                // 如果需要确保从头播放，可以取消注释下一行：
                // if (showMagnifyingGlass) magnifyingGlassAnimator.Play("MagnifyingGlassCircularPath", -1, 0f);
            }
        }

        if (itemImage != null) // itemImage 是物品本身的图标
        {
            // The itemImage itself should generally be visible unless the whole item is 'hidden' by the overlay.
            // If the 'Hidden' or 'Searching' state implies the itemImage itself should also be invisible (e.g. black square takes over everything),
            // then manage itemImage.gameObject.SetActive(!showOverlay) here.
            // However, usually the overlay sits on top or the itemImage is part of what gets "blacked out" by the overlay.
            // For now, let's assume itemImage visibility is primarily controlled by the overlay's presence.
            // If rarity background should also be hidden when item is "Hidden/Searching", handle it here:
            if (rarityBackgroundImage != null)
            {
                rarityBackgroundImage.gameObject.SetActive(!showOverlay); // Hide background if item is hidden/searching
            }
        }
        // Debug.Log($"[InventoryItem] {jsonData?.Name} new state {newState}. Overlay: {searchOverlay?.activeSelf}, Glass: {magnifyingGlassIconGO?.activeSelf}, Animator: {magnifyingGlassAnimator?.enabled}");
    }

    /// <summary>
    /// 旋转物品
    /// </summary>
    public void Rotate()
    {
        rotated = !rotated;

        // Update the visual size of the InventoryItem itself based on new WIDTH/HEIGHT
        float actualTileSizeWidth = ItemGrid.tileSizeWidth;
        float actualTileSizeHeight = ItemGrid.tileSizeHeight;
        Vector2 newSize = new Vector2(WIDTH * actualTileSizeWidth, HEIGHT * actualTileSizeHeight);
        GetComponent<RectTransform>().sizeDelta = newSize;

        // The rarityBackgroundImage, if set to stretch with parent, will automatically resize.
        // No explicit resizing needed for it here if its RectTransform anchors are set to stretch.

        // If itemImage also needs explicit resizing (e.g., not stretching, or needs aspect ratio adjustments):
        // if (itemImage != null) { itemImage.rectTransform.sizeDelta = newSize; }

        Debug.Log($"[InventoryItem.Rotate] Item '{jsonData?.Name}' rotated. New visual size: {newSize}. Rotated state: {rotated}");

        // The InventoryController will likely handle re-checking placement and highlighting after calling Rotate.
    }
}