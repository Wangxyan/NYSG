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
            return;
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
        if (currentDisplayState == newState && !forceUpdate) return;

        currentDisplayState = newState;
        // Debug.Log($"Item {jsonData?.Name} changing state to {newState}");

        if (blackOverlayImage == null || itemImage == null) 
        {
            // Debug.LogError("InventoryItem: UI references (blackOverlayImage or itemImage) are null in SetDisplayState.", this);
            return; // 避免在编辑器预览或未完全初始化时出错
        }

        switch (currentDisplayState)
        {
            case ItemDisplayState.Hidden:
                blackOverlayImage.gameObject.SetActive(true);
                itemImage.gameObject.SetActive(false); // 隐藏实际图标以确保黑色完全覆盖且无交互
                break;
            case ItemDisplayState.Searching:
                blackOverlayImage.gameObject.SetActive(true); // 视觉上与Hidden相同，动画由外部控制
                itemImage.gameObject.SetActive(false);
                break;
            case ItemDisplayState.Revealed:
                blackOverlayImage.gameObject.SetActive(false);
                itemImage.gameObject.SetActive(true);
                break;
        }
    }

    /// <summary>
    /// 旋转物品
    /// </summary>
    public void Rotate()
    {
        rotated = !rotated;

        RectTransform rectTransform = GetComponent<RectTransform>();
        rectTransform.rotation = Quaternion.Euler(0, 0, rotated ? 90f : 0f);
    }
}