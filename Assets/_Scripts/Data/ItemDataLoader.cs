using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq; // Required for Linq operations like Sum and Any

public class ItemDataLoader : MonoBehaviour
{
    public static ItemDataLoader Instance { get; private set; }

    public List<JsonItemData> AllItems { get; private set; }
    private Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

    // Define weights for rarities. Index corresponds to Rarity value.
    // Example: Rarity 0 gets weight 100, Rarity 1 gets 75, etc.
    // Higher weight = higher chance. Adjust these values to tune probabilities.
    // Make sure this array is large enough to cover your max rarity value, 
    // or handle cases where Rarity might exceed this array's bounds.
    public static readonly float[] RaritySpawnWeights = 
    {
        100f, // Rarity 0 (Common)
        75f,  // Rarity 1 (Uncommon)
        50f,  // Rarity 2 (Rare)
        25f,  // Rarity 3 (Very Rare)
        10f,  // Rarity 4 (Epic)
        5f,   // Rarity 5 (Legendary)
        1f    // Rarity 6 (Mythic) - example, extend as needed
    };

    // 可以考虑在 Inspector 中配置 JSON 文件名和路径
    [SerializeField] private string jsonFileName = "Items.json";
    [SerializeField] private string jsonDataPath = "GameData/"; // 相对于 StreamingAssets

    private Dictionary<int, Sprite> loadedSpritesCache = new Dictionary<int, Sprite>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 使其在场景切换时持久存在
            LoadItemData();
        }
        else
        {
            Destroy(gameObject); // 防止重复实例
        }
    }

    private void LoadItemData()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, jsonDataPath, jsonFileName);
        AllItems = new List<JsonItemData>();

        if (File.Exists(filePath))
        {
            string dataAsJson = File.ReadAllText(filePath);
            JsonItemDataCollection collection = JsonUtility.FromJson<JsonItemDataCollection>(dataAsJson);
            if (collection != null && collection.items != null)
            {
                AllItems.AddRange(collection.items);
                Debug.Log($"Loaded {AllItems.Count} items from {filePath}");
            }
            else
            {
                Debug.LogError($"Failed to parse JSON or collection/items array is null in {filePath}. Trying to parse as a direct list.");
                // 尝试直接解析为 List<JsonItemData>，如果顶层不是对象而是数组
                List<JsonItemData> directList = JsonUtility.FromJson<List<JsonItemData>>("{\"items\":" + dataAsJson + "}"); // JsonUtility 需要一个对象作为根
                 if (directList != null) {
                    AllItems.AddRange(directList);
                    Debug.Log($"Loaded {AllItems.Count} items directly as a list from {filePath} (wrapped for JsonUtility)");
                 }
                 else {
                    Debug.LogError($"Failed to parse JSON as a direct list either from {filePath}.");
                 }
            }
        }
        else
        { 
            Debug.LogError($"Cannot find item JSON file at {filePath}");
        }
    }

    public Sprite GetSpriteByRes(string resPath)
    {
        if (string.IsNullOrEmpty(resPath)) return null;

        if (spriteCache.TryGetValue(resPath, out Sprite sprite))
        {
            return sprite;
        }
        else
        {
            // Assuming sprites are in a folder named "ItemIcons" inside a "Resources" folder.
            // Example resPath could be "ItemIcons/101" or just "101" if they are directly in ItemIcons.
            // Ensure your Res field in JSON matches this structure, or adjust path here.
            Sprite loadedSprite = Resources.Load<Sprite>($"{resPath}"); 
            if (loadedSprite != null)
            {
                spriteCache.Add(resPath, loadedSprite);
                return loadedSprite;
            }
            else
            {
                Debug.LogWarning($"[ItemDataLoader] Sprite not found at Resources/ItemIcons/{resPath}. Make sure the Res value and path are correct.");
                return null; 
            }
        }
    }

    /// <summary>
    /// Selects a random item from the provided list based on their Rarity and defined RaritySpawnWeights.
    /// </summary>
    /// <param name="itemsToChooseFrom">A pre-filtered list of items to select from.</param>
    /// <returns>A randomly selected JsonItemData, or null if the list is empty or no valid weights.</returns>
    public JsonItemData SelectRandomItemByRarity(List<JsonItemData> itemsToChooseFrom)
    {
        if (itemsToChooseFrom == null || itemsToChooseFrom.Count == 0)
        {
            return null;
        }

        List<float> weights = new List<float>();
        float totalWeight = 0f;

        foreach (JsonItemData item in itemsToChooseFrom)
        {
            float weight = 0f;
            if (item.Rarity >= 0 && item.Rarity < RaritySpawnWeights.Length)
            {
                weight = RaritySpawnWeights[item.Rarity];
            }
            else if (item.Rarity >= RaritySpawnWeights.Length) // Rarity exceeds defined weights, use lowest defined weight
            {
                weight = RaritySpawnWeights[RaritySpawnWeights.Length - 1];
                 Debug.LogWarning($"[ItemDataLoader] Item '{item.Name}' (ID: {item.Id}) has Rarity {item.Rarity} which exceeds defined RaritySpawnWeights length. Using lowest weight: {weight}");
            }
            else // Negative rarity, assign a very low weight or handle as an error
            {
                weight = 0.1f; // Default small weight for undefined negative rarities
                Debug.LogWarning($"[ItemDataLoader] Item '{item.Name}' (ID: {item.Id}) has negative Rarity {item.Rarity}. Assigning minimal weight.");
            }
            weights.Add(weight);
            totalWeight += weight;
        }

        if (totalWeight <= 0) // No items with positive weight
        {
            // Fallback to uniform random if all weights are zero or negative (should not happen with proper setup)
            if (itemsToChooseFrom.Count > 0) {
                 Debug.LogWarning("[ItemDataLoader] Total weight for item selection is zero or negative. Falling back to uniform random selection.");
                return itemsToChooseFrom[Random.Range(0, itemsToChooseFrom.Count)];
            }
            return null;
        }

        float randomNumber = Random.Range(0, totalWeight);
        float cumulativeWeight = 0f;

        for (int i = 0; i < itemsToChooseFrom.Count; i++)
        {
            cumulativeWeight += weights[i];
            if (randomNumber < cumulativeWeight)
            {
                return itemsToChooseFrom[i];
            }
        }
        
        // Fallback in case of floating point issues, though should be rare.
        // Return last item if loop finishes without selection (should not happen if totalWeight > 0 and list not empty)
        Debug.LogWarning("[ItemDataLoader] Weighted random selection did not pick an item through cumulative weight. This is unexpected. Returning last valid item if possible.");
        return itemsToChooseFrom.LastOrDefault(item => item != null); 
    }
} 