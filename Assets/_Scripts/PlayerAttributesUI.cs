using UnityEngine;
using UnityEngine.UI; // 如果使用旧版 UI Text
// using TMPro; // 如果使用 TextMeshPro, 取消注释这行

/// <summary>
/// 负责计算并显示玩家背包中物品提供的总属性。
/// </summary>
public class PlayerAttributesUI : MonoBehaviour
{
    [Header("UI Value Text References")]
    [Tooltip("Text UI element to display total Charm value.")]
    [SerializeField] private Text charmValueText; // 或者 TextMeshProUGUI charmValueText;
    [Tooltip("Text UI element to display total Knowledge value.")]
    [SerializeField] private Text knowledgeValueText; // 或者 TextMeshProUGUI knowledgeValueText;
    [Tooltip("Text UI element to display total Talent value.")]
    [SerializeField] private Text talentValueText; // 或者 TextMeshProUGUI talentValueText;
    [Tooltip("Text UI element to display total Wealth value.")]
    [SerializeField] private Text wealthValueText; // 或者 TextMeshProUGUI wealthValueText;

    [Header("Controller References")]
    [Tooltip("Reference to the InventoryController.")]
    [SerializeField] private InventoryController inventoryController;

    void Start()
    {
        if (inventoryController == null)
        {
            inventoryController = FindObjectOfType<InventoryController>();
            if (inventoryController == null)
            {
                Debug.LogError("PlayerAttributesUI: InventoryController not found in scene! Attributes will not be updated.", this);
                enabled = false; // 禁用此脚本
                return;
            }
        }

        // 验证 Text 引用是否都已设置
        if (charmValueText == null || knowledgeValueText == null || talentValueText == null || wealthValueText == null)
        {
            Debug.LogError("PlayerAttributesUI: One or more attribute value Text UI elements are not assigned in the Inspector!", this);
            enabled = false; // 禁用此脚本
            return;
        }

        // 初始更新一次
        UpdateAttributeDisplay();
    }

    void Update()
    {
        // 为了性能，可以考虑不是每帧都更新，
        // 而是仅当背包内容发生变化时更新 (例如通过事件系统)。
        // 但对于当前需求，"实时"通常意味着在 Update 中轮询。
        UpdateAttributeDisplay();
    }

    public void UpdateAttributeDisplay()
    {
        if (inventoryController == null || inventoryController.playerInventoryGrids == null)
        {
            return; // InventoryController 或其网格列表未准备好
        }

        int totalCharm = 0;
        int totalKnowledge = 0;
        int totalTalent = 0;
        int totalWealth = 0;

        foreach (ItemGrid playerGrid in inventoryController.playerInventoryGrids)
        {
            if (playerGrid != null)
            {
                // GetAllUniqueItems() 会返回该网格上所有不同的 InventoryItem 实例
                foreach (InventoryItem item in playerGrid.GetAllUniqueItems())
                {
                    if (item != null && item.jsonData != null)
                    {
                        // 无论物品是否隐藏，只要在背包中就计算属性
                        totalCharm += item.jsonData.charm;
                        totalKnowledge += item.jsonData.knowledge;
                        totalTalent += item.jsonData.talent;
                        totalWealth += item.jsonData.wealth;
                    }
                }
            }
        }

        // 更新 UI 文本
        if (charmValueText != null) charmValueText.text = totalCharm.ToString();
        if (knowledgeValueText != null) knowledgeValueText.text = totalKnowledge.ToString();
        if (talentValueText != null) talentValueText.text = totalTalent.ToString();
        if (wealthValueText != null) wealthValueText.text = totalWealth.ToString();
    }
} 