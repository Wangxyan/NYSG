using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // Required for Image

/// <summary>
/// 库存物品的高亮逻辑处理相关
/// </summary>
public class InventoryHighlight : MonoBehaviour
{
    /// <summary>
    /// 高亮的矩形变换
    /// </summary>
    [SerializeField] public RectTransform highlighter;

    private Image highlightImageComponent;

    [Header("Highlight Colors & Alpha")]
    public Color canPlaceNoDisplacementColor = Color.green;
    public Color cannotPlaceOrDisplacesColor = Color.red;
    [Range(0f, 1f)]
    public float highlightAlpha = 0.5f; // Default to 50% transparency

    private void Awake()
    {
        if (highlighter != null)
        {
            highlightImageComponent = highlighter.GetComponent<Image>();
            if (highlightImageComponent == null)
            {
                Debug.LogError("[InventoryHighlight] Highlighter RectTransform does not have an Image component!");
            }
        }
        else
        {
            Debug.LogError("[InventoryHighlight] Highlighter RectTransform is not assigned!");
        }
        // Apply initial alpha to default colors, or ensure UpdateHighlightColor is called once.
    }

    /// <summary>
    /// 根据条件显示或隐藏高亮
    /// </summary>
    /// <param name="b">条件</param>
    public void Show(bool b)
    {
        if (highlighter != null)
        {
            highlighter.gameObject.SetActive(b);
        }
    }

    /// <summary>
    /// 更新高亮颜色, 并应用透明度
    /// </summary>
    /// <param name="isPositiveHighlight">True则为正面颜色 (如绿色), False则为负面颜色 (如红色)</param>
    public void UpdateHighlightColor(bool isPositiveHighlight)
    {
        if (highlightImageComponent != null)
        {
            Color baseColor = isPositiveHighlight ? canPlaceNoDisplacementColor : cannotPlaceOrDisplacesColor;
            baseColor.a = highlightAlpha; // Apply the alpha
            highlightImageComponent.color = baseColor;
        }
    }

    /// <summary>
    /// 根据目标物品的大小设置高亮的大小
    /// </summary>
    /// <param name="targetItem">目标物品</param>
    public void SetSize(InventoryItem targetItem)
    {
        Vector2 size = new Vector2();
        size.x = targetItem.WIDTH * ItemGrid.tileSizeWidth;
        size.y = targetItem.HEIGHT * ItemGrid.tileSizeHeight;
        highlighter.sizeDelta = size;
    }

    /// <summary>
    /// 根据目标物品的位置设置高亮的位置
    /// </summary>
    /// <param name="targetGrid"></param>
    /// <param name="targetItem"></param>
    public void SetPosition(ItemGrid targetGrid, InventoryItem targetItem)
    {
        SetParent(targetGrid);

        Vector2 pos = targetGrid.CalculatePositionOnGrid(targetItem, targetItem.onGridPositionX, targetItem.onGridPositionY);

        highlighter.localPosition = pos;
    }

    /// <summary>
    /// 设置高亮的父物体为目标网格
    /// </summary>
    /// <param name="targetGrid">目标网格</param>
    public void SetParent(ItemGrid targetGrid)
    {
        if (targetGrid == null) return;
        highlighter.SetParent(targetGrid.GetComponent<RectTransform>());
    }

    /// <summary>
    /// 设置高亮的位置为目标网格中的目标物品位置
    /// </summary>
    /// <param name="targetGrid">目标网格</param>
    /// <param name="targetItem">目标物品</param>
    /// <param name="posX">位置X轴</param>
    /// <param name="posY">位置Y轴</param>
    public void SetPosition(ItemGrid targetGrid, InventoryItem targetItem, int posX, int posY)
    {
        Vector2 pos = targetGrid.CalculatePositionOnGrid(targetItem, posX, posY);

        highlighter.localPosition = pos;
    }
}
