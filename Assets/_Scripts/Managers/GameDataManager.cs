using System.Collections.Generic;
using UnityEngine;

public class GameDataManager : MonoBehaviour
{
    public static GameDataManager Instance { get; private set; }

    public List<PersistedItemInfo> persistedPlayerItems = new List<PersistedItemInfo>();
    public PersistentPlayerAttributes playerAttributes = new PersistentPlayerAttributes();

    // Flag to indicate if data has been loaded from a previous session/scene for the first time
    // This helps differentiate between starting a new game vs loading persisted data
    public bool HasPersistedData { get; private set; } = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[GameDataManager] Instance created and marked DontDestroyOnLoad.");
        }
        else if (Instance != this)
        {
            Debug.LogWarning("[GameDataManager] Duplicate instance detected. Destroying self.");
            Destroy(gameObject);
        }
    }

    public void ClearPersistedData() // Call this when starting a truly new game
    {
        persistedPlayerItems.Clear();
        playerAttributes = new PersistentPlayerAttributes(); // Reset to default attributes
        HasPersistedData = false;
        Debug.Log("[GameDataManager] All persisted data cleared.");
    }

    // Call this method when data is successfully saved to indicate that there's data to load in the next scene
    public void MarkDataAsPersisted()
    {
        HasPersistedData = true;
    }

    // Example: Method to prepare for loading into a new scene
    // You might call this from a loading screen or a scene transition manager
    public void PrepareForNewSceneLoad()
    {
        // This is a good place if you need to do any pre-processing of data
        // before the new scene's Awake/Start methods try to access it.
        // For now, just logging.
        if (HasPersistedData)
        {
            Debug.Log("[GameDataManager] Preparing for new scene load with persisted data.");
        }
        else
        {
            Debug.Log("[GameDataManager] Preparing for new scene load with NO persisted data (new game or data cleared).");
        }
    }
} 