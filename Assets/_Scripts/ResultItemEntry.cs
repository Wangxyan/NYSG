using UnityEngine;
using UnityEngine.UI;
// using TMPro; // Uncomment if using TextMeshPro

public class ResultItemEntry : MonoBehaviour
{
    public Image itemIconImage;
    public Text itemNameText;         // Or TextMeshProUGUI
    public Text itemQuantityText;     // Or TextMeshProUGUI
    public Text charmValueText;       // Or TextMeshProUGUI
    public Text knowledgeValueText;   // Or TextMeshProUGUI
    public Text talentValueText;      // Or TextMeshProUGUI - Assuming 'talent' exists in JsonItemData
    public Text wealthValueText;      // Or TextMeshProUGUI - Assuming 'wealth' exists in JsonItemData

    public void Populate(JsonItemData representativeItemData, Sprite icon, int quantity, float totalCharm, float totalKnowledge, float totalTalent, float totalWealth)
    {
        if (representativeItemData == null)
        {
            Debug.LogError("ResultItemEntry: representativeItemData is null!");
            if(itemIconImage != null) itemIconImage.enabled = false;
            if(itemNameText != null) itemNameText.text = "Error";
            if(itemQuantityText != null) itemQuantityText.text = "-";
            if(charmValueText != null) charmValueText.text = "-";
            if(knowledgeValueText != null) knowledgeValueText.text = "-";
            if(talentValueText != null) talentValueText.text = "-";
            if(wealthValueText != null) wealthValueText.text = "-";
            return;
        }

        if (itemIconImage != null)
        {
            itemIconImage.sprite = icon;
            itemIconImage.enabled = (icon != null);
        }
        if (itemNameText != null) itemNameText.text = representativeItemData.Name;
        if (itemQuantityText != null) itemQuantityText.text = quantity.ToString();
        
        if (charmValueText != null) charmValueText.text = totalCharm.ToString("F0"); 
        if (knowledgeValueText != null) knowledgeValueText.text = totalKnowledge.ToString("F0");
        if (talentValueText != null) talentValueText.text = totalTalent.ToString("F0");
        if (wealthValueText != null) wealthValueText.text = totalWealth.ToString("F0");
    }
} 