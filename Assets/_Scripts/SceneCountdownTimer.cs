using UnityEngine;
using UnityEngine.UI; // For UI Text
// using TMPro; // Uncomment if using TextMeshPro
using UnityEngine.SceneManagement; // Required for scene management
using System.Collections; // Required for IEnumerator if used for delays
using System.Collections.Generic;
using System.Linq; // Added for Linq operations like Sum

/// <summary>
/// Manages a countdown timer that loads a specified scene when it finishes.
/// Also includes a pre-countdown animation and a results window.
/// </summary>
public class SceneCountdownTimer : MonoBehaviour
{
    [Header("Timer Configuration")]
    [Tooltip("Total duration of the countdown in seconds.")]
    [SerializeField] private float countdownDurationSeconds = 180f; // Default to 3 minutes

    [Tooltip("The name of the scene to load when the countdown finishes.")]
    [SerializeField] private string targetSceneName = "YourTargetSceneName"; // IMPORTANT: User must set this

    [Header("Main Timer UI")]
    [Tooltip("Text UI element to display the remaining time.")]
    [SerializeField] private Text countdownText; // Or TextMeshProUGUI

    [Header("Pre-Countdown Animation UI")]
    [Tooltip("Panel/GameObject holding the pre-countdown animation elements.")]
    [SerializeField] private GameObject preCountdownPanel;
    [Tooltip("Text UI element for the pre-countdown numbers (3, 2, 1, Go!).")]
    [SerializeField] private Text preCountdownAnimText; // Or TextMeshProUGUI
    [Tooltip("Start scale for the pre-countdown numbers.")]
    [SerializeField] private float preAnimStartScale = 2f;
    [Tooltip("End scale for the pre-countdown numbers.")]
    [SerializeField] private float preAnimEndScale = 1f;
    [Tooltip("Duration of each number's scale animation.")]
    [SerializeField] private float preAnimScaleDuration = 0.5f;
    [Tooltip("Pause duration between pre-countdown numbers.")]
    [SerializeField] private float preAnimPauseTime = 0.5f;

    [Header("Results Window UI")]
    [Tooltip("Panel/GameObject for the results window shown when timer ends.")]
    [SerializeField] private GameObject resultsWindowPanel;
    [Tooltip("Text UI for the title of the results window.")]
    [SerializeField] private Text resultsTitleText; // Or TextMeshProUGUI
    [Tooltip("Text UI to display player attributes in the results window.")]
    [SerializeField] private Text resultsAttributesText; // Or TextMeshProUGUI
    [Tooltip("Button to proceed to the next scene from the results window.")]
    [SerializeField] private Button loadNextSceneButton;

    [Header("Results Window Content")]
    [Tooltip("The parent Transform for instantiating result item entries (usually the ScrollView's Content object).")]
    [SerializeField] private RectTransform resultsScrollViewContent;
    [Tooltip("Prefab for displaying a single item in the results list.")]
    [SerializeField] private ResultItemEntry resultItemEntryPrefab;
    [Tooltip("Text UI element to display the total sum of player's item attributes.")]
    [SerializeField] private Text totalStatsText; // Or TextMeshProUGUI // This will be deprecated or repurposed

    [Header("Individual Total Stats Texts")] // New Header for individual stat totals
    [Tooltip("Text UI for total Charm.")]
    [SerializeField] private Text totalCharmText;
    [Tooltip("Text UI for total Knowledge.")]
    [SerializeField] private Text totalKnowledgeText;
    [Tooltip("Text UI for total Talent.")]
    [SerializeField] private Text totalTalentText;
    [Tooltip("Text UI for total Wealth.")]
    [SerializeField] private Text totalWealthText;

    [Header("Early Exit")]
    [Tooltip("Button to allow the player to end the current phase early.")]
    [SerializeField] private Button earlyExitButton;

    private float currentTimeRemaining;
    private bool timerIsRunning = false;
    private bool preCountdownFinished = false;
    private bool gamePhaseEnded = false; // New flag to track if the game phase is over

    // References to other controllers
    private InventoryController inventoryControllerInstance;
    private ShopController shopControllerInstance;

    void Start()
    {
        // Validate UI references
        if (countdownText == null)
        {
            Debug.LogError("SceneCountdownTimer: Main Countdown Text UI is not assigned!", this);
            enabled = false; return;
        }
        if (preCountdownPanel == null || preCountdownAnimText == null)
        {
            Debug.LogWarning("SceneCountdownTimer: Pre-Countdown UI (Panel or Text) not fully assigned. Pre-animation might not work.", this);
            // Allow to continue without pre-animation if only that is missing
        }
        if (resultsWindowPanel == null || resultsTitleText == null || resultsAttributesText == null || loadNextSceneButton == null)
        {
            Debug.LogError("SceneCountdownTimer: Results Window UI elements not fully assigned!", this);
            enabled = false; return;
        }
        if (resultsScrollViewContent == null || resultItemEntryPrefab == null)
        {
            Debug.LogError("SceneCountdownTimer: Results Window Content UI (ScrollViewContent, ItemEntryPrefab) not fully assigned!", this);
            enabled = false; return;
        }
        // Validate new individual stat texts if they are considered essential
        if (totalCharmText == null || totalKnowledgeText == null || totalTalentText == null || totalWealthText == null)
        {
            Debug.LogWarning("SceneCountdownTimer: One or more individual total stat Text UIs are not assigned. These totals will not be displayed.", this);
            // Decide if this should be an error (enabled = false; return;) or just a warning
        }

        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError("SceneCountdownTimer: Target Scene Name is not specified!", this);
            enabled = false; return;
        }

        // Find other controllers
        inventoryControllerInstance = FindObjectOfType<InventoryController>();
        shopControllerInstance = FindObjectOfType<ShopController>();

        if (inventoryControllerInstance == null)
        {
            Debug.LogWarning("SceneCountdownTimer: InventoryController not found in scene. Cannot notify it to freeze.");
        }
        if (shopControllerInstance == null)
        {
            Debug.LogWarning("SceneCountdownTimer: ShopController not found in scene. Cannot notify it to freeze.");
        }

        // Setup Early Exit Button
        if (earlyExitButton != null)
        {
            earlyExitButton.onClick.AddListener(TriggerEarlyExit);
        }
        else
        {
            Debug.LogWarning("SceneCountdownTimer: Early Exit Button is not assigned.", this);
        }

        // Initial UI states
        if(preCountdownPanel != null) preCountdownPanel.SetActive(false);
        resultsWindowPanel.SetActive(false);
        countdownText.gameObject.SetActive(false); // Hide main timer initially

        gamePhaseEnded = false; // Ensure flag is reset on start
        StartCoroutine(PlayPreCountdownAnimationThenStartMainTimer());
    }

    private IEnumerator PlayPreCountdownAnimationThenStartMainTimer()
    {
        if (preCountdownPanel != null && preCountdownAnimText != null)
        {
            preCountdownPanel.SetActive(true);
            countdownText.gameObject.SetActive(false); // Ensure main timer text is hidden

            yield return AnimatePreCountdownText("3");
            yield return new WaitForSeconds(preAnimPauseTime);
            yield return AnimatePreCountdownText("2");
            yield return new WaitForSeconds(preAnimPauseTime);
            yield return AnimatePreCountdownText("1");
            yield return new WaitForSeconds(preAnimPauseTime);
            yield return AnimatePreCountdownText("游戏开始!", false); // Don't scale down "游戏开始!" as much, or make it instant
            yield return new WaitForSeconds(preAnimPauseTime * 1.5f); // Hold "游戏开始!" a bit longer

            preCountdownPanel.SetActive(false);
        }
        else
        {
            Debug.LogWarning("SceneCountdownTimer: Skipping pre-countdown animation as UI elements are not set.");
        }
        
        preCountdownFinished = true;

        // Initialize and start the main game timer
        InitializeAndStartMainTimer();

        // After pre-countdown and main timer init, trigger shop setup for ALL shop controllers
        ShopController[] shopControllers = FindObjectsOfType<ShopController>();
        if (shopControllers != null && shopControllers.Length > 0)
        {
            foreach (ShopController sc in shopControllers)
            {
                sc.TriggerInitialShopSetup();
            }
            Debug.Log($"SceneCountdownTimer: Triggered initial shop setup for {shopControllers.Length} ShopController(s).");
        }
        else
        {
            Debug.LogWarning("SceneCountdownTimer: No ShopControllers found in scene. Cannot trigger initial shop setup.");
        }
    }

    private IEnumerator AnimatePreCountdownText(string message, bool scaleDown = true)
    {
        if (preCountdownAnimText == null) yield break;

        preCountdownAnimText.text = message;
        RectTransform rt = preCountdownAnimText.GetComponent<RectTransform>();
        if (rt == null) yield break;

        float currentScaleValue = preAnimStartScale;
        rt.localScale = Vector3.one * currentScaleValue;
        preCountdownAnimText.gameObject.SetActive(true); // Ensure text is active

        if (scaleDown)
        {
            float timer = 0f;
            while (timer < preAnimScaleDuration)
            {
                currentScaleValue = Mathf.Lerp(preAnimStartScale, preAnimEndScale, timer / preAnimScaleDuration);
                rt.localScale = Vector3.one * currentScaleValue;
                timer += Time.deltaTime;
                yield return null;
            }
            rt.localScale = Vector3.one * preAnimEndScale;
        }
        else // For "游戏开始!" - maybe just set to end scale or a slightly larger one
        {
            rt.localScale = Vector3.one * (preAnimEndScale > 1f ? preAnimEndScale : 1.2f); // Ensure it's visible
        }
    }
    
    private void InitializeAndStartMainTimer()
    {
        countdownText.gameObject.SetActive(true);
        currentTimeRemaining = countdownDurationSeconds;
        timerIsRunning = true;
        UpdateTimerDisplay(); 
    }

    void Update()
    {
        if (gamePhaseEnded || !preCountdownFinished || !timerIsRunning) return; 

        if (currentTimeRemaining > 0)
        {
            currentTimeRemaining -= Time.deltaTime;
            UpdateTimerDisplay();
        }
        else
        {
            currentTimeRemaining = 0;
            timerIsRunning = false;
            UpdateTimerDisplay(); 
            EndGamePhaseAndShowResults(); // Changed from direct ShowResultsWindow()
        }
    }

    void UpdateTimerDisplay()
    {
        if (countdownText != null)
        {
            // Format the time (e.g., MM:SS)
            int minutes = Mathf.FloorToInt(currentTimeRemaining / 60F);
            int seconds = Mathf.FloorToInt(currentTimeRemaining % 60F);
            countdownText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }

    // Helper class to store aggregated item statistics for the results screen
    public class AggregatedItemStats
    {
        public JsonItemData RepresentativeItemData { get; set; } // To get Name, Icon Res, etc.
        public int Quantity { get; set; }
        public float TotalCharm { get; set; }
        public float TotalKnowledge { get; set; }
        public float TotalTalent { get; set; } // Assuming JsonItemData has this field
        public float TotalWealth { get; set; } // Assuming JsonItemData has this field

        public AggregatedItemStats(JsonItemData itemData)
        {
            RepresentativeItemData = itemData;
            Quantity = 0; // Will be incremented
            TotalCharm = 0f;
            TotalKnowledge = 0f;
            TotalTalent = 0f;
            TotalWealth = 0f;
        }

        public void AddItemInstance(JsonItemData itemDataInstance)
        {
            Quantity++;
            TotalCharm += itemDataInstance.charm;       // Direct access
            TotalKnowledge += itemDataInstance.knowledge; // Direct access
            TotalTalent += itemDataInstance.talent;     // Direct access, assuming field exists
            TotalWealth += itemDataInstance.wealth;     // Direct access, assuming field exists
        }
    }

    void ShowResultsWindow()
    {
        Debug.Log("Showing results window.");
        countdownText.gameObject.SetActive(false); 
        resultsWindowPanel.SetActive(true);

        resultsTitleText.text = "时间到!"; 
        resultsAttributesText.gameObject.SetActive(false); 

        foreach (Transform child in resultsScrollViewContent)
        {
            Destroy(child.gameObject);
        }

        if (inventoryControllerInstance == null || totalStatsText == null)
        {
            Debug.LogError("ShowResultsWindow: PlayerInventory or ItemDataLoader not found!");
            if(totalStatsText != null) totalStatsText.text = "错误：无法加载物品数据";
            return;
        }

        List<InventoryItem> playerItems = inventoryControllerInstance.GetAllPlayerInventoryItems();
        Dictionary<string, AggregatedItemStats> aggregatedItems = new Dictionary<string, AggregatedItemStats>();

        // Grand totals, calculated from all individual items before aggregation for display consistency
        float grandTotalCharm = 0;
        float grandTotalKnowledge = 0;
        float grandTotalTalent = 0;
        float grandTotalWealth = 0;

        foreach (InventoryItem itemInstance in playerItems)
        {
            if (itemInstance == null || itemInstance.jsonData == null) continue;
            JsonItemData currentJsonData = itemInstance.jsonData;
            string itemIdString = currentJsonData.Id.ToString(); // Convert Id to string once

            // Aggregate for grouped display
            if (!aggregatedItems.ContainsKey(itemIdString))
            {
                aggregatedItems[itemIdString] = new AggregatedItemStats(currentJsonData);
            }
            aggregatedItems[itemIdString].AddItemInstance(currentJsonData);

            // Sum for grand totals line
            grandTotalCharm += currentJsonData.charm;
            grandTotalKnowledge += currentJsonData.knowledge;
            grandTotalTalent += currentJsonData.talent; // Direct access
            grandTotalWealth += currentJsonData.wealth; // Direct access
        }
        
        if (aggregatedItems.Count == 0)
        {
            Debug.Log("No items in player inventory to display.");
        }

        // Sort aggregated items by name for consistent display order (optional)
        List<AggregatedItemStats> sortedAggregatedItems = aggregatedItems.Values.OrderBy(agg => agg.RepresentativeItemData.Name).ToList();

        foreach (AggregatedItemStats aggregatedStat in sortedAggregatedItems)
        {
            ResultItemEntry entry = Instantiate(resultItemEntryPrefab, resultsScrollViewContent);
            Sprite itemSprite = ItemDataLoader.Instance.GetSpriteByRes(aggregatedStat.RepresentativeItemData.Res);
            // Populate with aggregated data
            entry.Populate(aggregatedStat.RepresentativeItemData, 
                           itemSprite, 
                           aggregatedStat.Quantity, 
                           aggregatedStat.TotalCharm, 
                           aggregatedStat.TotalKnowledge, 
                           aggregatedStat.TotalTalent, 
                           aggregatedStat.TotalWealth);
        }

        // Update the old combined text (optional, could be removed or hidden)
        if (totalStatsText != null) 
        {
            // totalStatsText.text = string.Format(
            //     "总计加成: 魅力 {0:F0} | 知识 {1:F0} | 才艺 {2:F0} | 财富 {3:F0}",
            //     grandTotalCharm, grandTotalKnowledge, grandTotalTalent, grandTotalWealth
            // );
            totalStatsText.gameObject.SetActive(false); // Example: Hide the old combined text field
        }

        // Populate new individual Text fields
        if (totalCharmText != null) totalCharmText.text = string.Format("{0:F0}", grandTotalCharm);
        if (totalKnowledgeText != null) totalKnowledgeText.text = string.Format("{0:F0}", grandTotalKnowledge);
        if (totalTalentText != null) totalTalentText.text = string.Format("{0:F0}", grandTotalTalent);
        if (totalWealthText != null) totalWealthText.text = string.Format("{0:F0}", grandTotalWealth);
        
        loadNextSceneButton.onClick.RemoveAllListeners(); 
        loadNextSceneButton.onClick.AddListener(ProceedToNextScene);
    }

    void ProceedToNextScene()
    {
        Debug.Log($"Proceeding to scene: {targetSceneName}");
        SceneManager.LoadScene(targetSceneName);
    }
    
    // Optional: Public method to manually start or restart the timer if needed from elsewhere (might need adjustment for pre-countdown)
    public void StartTimer(float duration, string sceneName)
    {
        // This method might need more thought if called externally, 
        // as it would bypass the pre-countdown animation currently tied to Start().
        // For now, let's assume it's mainly for initial setup or a full restart including pre-animation.
        Debug.LogWarning("SceneCountdownTimer: External StartTimer call. Consider if pre-countdown animation should play.");
        
        // Stop any ongoing coroutines to prevent conflicts if restarting
        StopAllCoroutines(); 
        
        countdownDurationSeconds = duration;
        targetSceneName = sceneName;
        preCountdownFinished = false; // Reset pre-countdown flag
        timerIsRunning = false; // Ensure timer is stopped before pre-animation

        // Re-hide/show UI for a fresh start
        if(preCountdownPanel != null) preCountdownPanel.SetActive(false);
        resultsWindowPanel.SetActive(false);
        countdownText.gameObject.SetActive(false);

        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError("SceneCountdownTimer: Target Scene Name is not specified for StartTimer!", this);
            if(countdownText != null) countdownText.text = "Error!";
            return;
        }
        
        StartCoroutine(PlayPreCountdownAnimationThenStartMainTimer());
    }

    void TriggerEarlyExit()
    {
        Debug.Log("Early exit triggered by player.");
        if (gamePhaseEnded) return; // Prevent multiple calls

        EndGamePhaseAndShowResults();
    }

    void EndGamePhaseAndShowResults()
    {
        if (gamePhaseEnded) return; // Ensure this logic runs only once
        gamePhaseEnded = true;
        timerIsRunning = false; // Stop the timer explicitly if it was running

        Debug.Log("SceneCountdownTimer: Game phase ending. Notifying controllers and showing results.");

        // Notify other controllers to freeze their operations
        inventoryControllerInstance?.NotifyGamePhaseOver();
        // shopControllerInstance?.NotifyGamePhaseOver(); // Old way, notifying only one

        ShopController[] allShopControllers = FindObjectsOfType<ShopController>();
        if (allShopControllers != null && allShopControllers.Length > 0)
        {
            foreach (ShopController sc in allShopControllers)
            {
                sc.NotifyGamePhaseOver();
            }
            Debug.Log($"SceneCountdownTimer: Notified {allShopControllers.Length} ShopController(s) of game phase over.");
        }
        else
        {
            Debug.LogWarning("SceneCountdownTimer: No ShopControllers found to notify of game phase over.");
        }

        // Hide early exit button if it exists, as game is now over
        if (earlyExitButton != null)
        {
            earlyExitButton.gameObject.SetActive(false);
        }

        ShowResultsWindow();
    }
} 