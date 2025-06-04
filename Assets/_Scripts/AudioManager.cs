using UnityEngine;
using System.Collections.Generic; // For item-specific sounds if needed

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;         // For background music
    [SerializeField] private AudioSource sfxSource;           // For general one-shot sound effects
    [SerializeField] private AudioSource countdownSfxSource;  // Potentially for repeating countdown ticks
    // You can add more sfxSources if you anticipate many sounds playing simultaneously and overlapping

    [Header("Music Clips")]
    [SerializeField] private AudioClip backgroundMusic;

    [Header("UI & General SFX")] 
    [SerializeField] private AudioClip shopRefreshSound;
    [SerializeField] private AudioClip itemCraftedSound;
    [SerializeField] private AudioClip itemToBagSound;
    [SerializeField] private AudioClip bagFullSound;
    [SerializeField] private AudioClip itemToShopSound;
    // Placeholder for generic item select/place if item-specific is too complex initially
    [SerializeField] private AudioClip genericItemSelectSound;
    [SerializeField] private AudioClip genericItemPlaceSound;
    // New SFX definitions start here
    [SerializeField] private AudioClip gamePhaseEndSound; 
    [SerializeField] private AudioClip itemDisplacedToShopSound; 
    [SerializeField] private AudioClip quickMoveToShopSound; 
    [SerializeField] private AudioClip quickMoveToBagSound; 
    [SerializeField] private AudioClip quickMoveToBagFailedSound;
    // New UI SFX definitions start here
    [SerializeField] private AudioClip resultsWindowPopupSound;
    [SerializeField] private AudioClip itemDetailsPopupSound;
    [SerializeField] private AudioClip preCountdownDisappearSound;
    [SerializeField] private AudioClip feedbackMessagePopupSound;

    [Header("Timer SFX")]
    [SerializeField] private AudioClip preCountdownStartSound; // Sound for "3, 2, 1, Go!" sequence start
    [SerializeField] private AudioClip lastTenSecondsTickSound;  // e.g., a tick-tock sound
    [SerializeField] private AudioClip finalCountdownBeep; // e.g., a beep for each second from 10 down to 1

    [Header("Shop Item Search SFX")]
    [Tooltip("Sound played when an item search is complete. Array index corresponds to Rarity. Ensure size matches max rarity + 1.")]
    [SerializeField] private AudioClip[] searchCompleteSoundsByRarity;

    // Cache for loaded item-specific audio clips
    private Dictionary<string, AudioClip> loadedItemAudioClips = new Dictionary<string, AudioClip>();

    // For item-specific sounds (Advanced - requires data setup)
    // public Dictionary<string, AudioClip> itemSelectSounds = new Dictionary<string, AudioClip>();
    // public Dictionary<string, AudioClip> itemPlaceSounds = new Dictionary<string, AudioClip>();

    private bool lastTenSecondsPlaying = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Keep AudioManager across scenes
        }
        else
        {
            Destroy(gameObject); // Destroy duplicate
            return;
        }

        // Ensure AudioSources are assigned or add them if missing (basic setup)
        if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        if (countdownSfxSource == null) countdownSfxSource = gameObject.AddComponent<AudioSource>();

        musicSource.loop = true;
        // musicSource.playOnAwake = false; // control BGM play explicitly
        // sfxSource.playOnAwake = false;
        // countdownSfxSource.playOnAwake = false;
    }

    void Start()
    {
        PlayBackgroundMusic();
    }

    public void PlayBackgroundMusic()
    {
        if (backgroundMusic != null && musicSource != null)
        {
            musicSource.clip = backgroundMusic;
            musicSource.Play();
        }
        else
        {
            Debug.LogWarning("AudioManager: BackgroundMusic or MusicSource not set.");
        }
    }

    private AudioClip GetOrLoadItemAudioClip(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        if (loadedItemAudioClips.TryGetValue(path, out AudioClip clip))
        {
            return clip; // Return cached clip
        }
        else
        {
            AudioClip loadedClip = Resources.Load<AudioClip>(path);
            if (loadedClip != null)
            {
                loadedItemAudioClips[path] = loadedClip; // Cache it
            }
            else
            {
                Debug.LogWarning($"AudioManager: Failed to load AudioClip from Resources at path: {path}");
            }
            return loadedClip;
        }
    }

    private void PlaySFX(AudioClip clip, AudioSource sourceToUse = null)
    {
        if (clip == null) 
        {
            // Debug.LogWarning("AudioManager: AudioClip is null, cannot play SFX.");
            return;
        }
        AudioSource source = sourceToUse ?? sfxSource; // Use specified source or default sfxSource
        if (source != null) {
             source.PlayOneShot(clip);
        }
        else {
            Debug.LogWarning("AudioManager: SFX AudioSource not set.");
        }
    }

    // --- Public methods to play specific sounds ---
    public void PlayShopRefreshSound() { PlaySFX(shopRefreshSound); }
    public void PlayItemCraftedSound() { PlaySFX(itemCraftedSound); }
    public void PlayItemToBagSound() { PlaySFX(itemToBagSound); }
    public void PlayBagFullSound() { PlaySFX(bagFullSound); }
    public void PlayItemToShopSound() { PlaySFX(itemToShopSound); }
    public void PlayPreCountdownStartSound() { PlaySFX(preCountdownStartSound); }
    
    // Item specific sounds (Example - can be expanded)
    public void PlayItemSelectSound(JsonItemData itemData)
    {
        if (itemData != null && !string.IsNullOrEmpty(itemData.SelectAudioPath))
        {
            AudioClip itemSpecificClip = GetOrLoadItemAudioClip(itemData.SelectAudioPath);
            if (itemSpecificClip != null)
            {
                PlaySFX(itemSpecificClip);
                return;
            }
        }
        PlaySFX(genericItemSelectSound); // Fallback to generic
    }

    public void PlayItemPlaceSound(JsonItemData itemData)
    {
        if (itemData != null && !string.IsNullOrEmpty(itemData.PlaceAudioPath))
        {
            AudioClip itemSpecificClip = GetOrLoadItemAudioClip(itemData.PlaceAudioPath);
            if (itemSpecificClip != null)
            {
                PlaySFX(itemSpecificClip);
                return;
            }
        }
        PlaySFX(genericItemPlaceSound); // Fallback to generic
    }

    // --- Countdown Sounds --- 
    public void StartLastTenSecondsTicks()
    {
        if (lastTenSecondsTickSound != null && countdownSfxSource != null && !lastTenSecondsPlaying)
        {
            lastTenSecondsPlaying = true;
            countdownSfxSource.clip = lastTenSecondsTickSound;
            countdownSfxSource.loop = true; // Loop the tick-tock sound
            countdownSfxSource.Play();
            Debug.Log("AudioManager: Started last 10 seconds ticking sound.");
        }
    }

    public void PlayFinalCountdownBeep()
    {
        // Use the general sfxSource for distinct beeps, or countdownSfxSource if it's not looping a tick
        PlaySFX(finalCountdownBeep, sfxSource); 
    }

    public void StopLastTenSecondsTicks()
    {
        if (countdownSfxSource != null && lastTenSecondsPlaying)
        {
            countdownSfxSource.Stop();
            countdownSfxSource.loop = false;
            lastTenSecondsPlaying = false;
            Debug.Log("AudioManager: Stopped last 10 seconds ticking sound.");
        }
    }

    // Call this if game ends before countdown naturally finishes or if restarting
    public void ResetCountdownSoundsState()
    {
        StopLastTenSecondsTicks();
        // Any other countdown related sound state reset
    }

    // New SFX methods
    public void PlayGamePhaseEndSound() { PlaySFX(gamePhaseEndSound); }
    public void PlayItemDisplacedToShopSound() { PlaySFX(itemDisplacedToShopSound); }
    public void PlayQuickMoveToShopSound() { PlaySFX(quickMoveToShopSound); }
    public void PlayQuickMoveToBagSound() { PlaySFX(quickMoveToBagSound); }
    public void PlayQuickMoveToBagFailedSound() { PlaySFX(quickMoveToBagFailedSound); }

    // New UI SFX methods
    public void PlayResultsWindowPopupSound() { PlaySFX(resultsWindowPopupSound); }
    public void PlayItemDetailsPopupSound() { PlaySFX(itemDetailsPopupSound); }
    public void PlayPreCountdownDisappearSound() { PlaySFX(preCountdownDisappearSound); }
    public void PlayFeedbackMessagePopupSound() { PlaySFX(feedbackMessagePopupSound); }

    public void PlaySearchCompleteSound(int rarity)
    {
        if (searchCompleteSoundsByRarity == null || searchCompleteSoundsByRarity.Length == 0) return;

        AudioClip clipToPlay = null;
        if (rarity >= 0 && rarity < searchCompleteSoundsByRarity.Length)
        {
            clipToPlay = searchCompleteSoundsByRarity[rarity];
        }
        else if (rarity >= searchCompleteSoundsByRarity.Length) // Rarity too high, fallback to highest defined
        {
            clipToPlay = searchCompleteSoundsByRarity[searchCompleteSoundsByRarity.Length - 1];
        }
        // If clipToPlay is still null (e.g., slot was empty), PlaySFX will handle it (by doing nothing)
        PlaySFX(clipToPlay);
    }
} 