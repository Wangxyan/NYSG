using UnityEngine;
using UnityEngine.UI; // 如果使用TMPro，请取消注释下一行并替换Text为TextMeshProUGUI
// using TMPro; 

/// <summary>
/// Manages the item details pop-up window.
/// </summary>
public class ItemDetailsPopup : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Image UI element to display the item's icon.")]
    public Image itemIconImage;

    [Tooltip("Text UI element for the item's name.")]
    public Text itemNameText; // Or public TextMeshProUGUI itemNameText;

    [Tooltip("Text UI element for the item's description.")]
    public Text itemDescriptionText; // Or public TextMeshProUGUI itemDescriptionText;

    [Tooltip("Optional button to close the popup.")]
    public Button closeButton;

    void Awake()
    {
        // Initialize: hide on start
        gameObject.SetActive(false);

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Hide);
        }
    }

    /// <summary>
    /// Populates the popup with item data and displays it.
    /// </summary>
    /// <param name="itemData">The JsonItemData of the item to display.</param>
    /// <param name="icon">The sprite for the item's icon.</param>
    public void Show(JsonItemData itemData, Sprite icon)
    {
        if (itemData == null)
        {
            Debug.LogError("ItemDetailsPopup: JsonItemData is null. Cannot show details.", this);
            Hide(); // Hide if data is invalid
            return;
        }

        if (itemIconImage != null)
        {
            itemIconImage.sprite = icon;
            itemIconImage.enabled = (icon != null); // Only enable if icon is present
        }
        else
        {
            Debug.LogWarning("ItemDetailsPopup: Item Icon Image UI is not assigned.", this);
        }

        if (itemNameText != null)
        {
            itemNameText.text = itemData.Name;
        }
        else
        {
            Debug.LogWarning("ItemDetailsPopup: Item Name Text UI is not assigned.", this);
        }

        if (itemDescriptionText != null)
        {
            itemDescriptionText.text = itemData.Description;
        }
        else
        {
            Debug.LogWarning("ItemDetailsPopup: Item Description Text UI is not assigned.", this);
        }

        gameObject.SetActive(true);
    }

    /// <summary>
    /// Hides the pop-up window.
    /// </summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }
} 