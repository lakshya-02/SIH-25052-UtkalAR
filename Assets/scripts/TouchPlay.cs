using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Handles clicking or touching objects to play associated audio clips with language support.
/// 1. First click on a model plays its audio.
/// 2. A second click on the same model stops the audio.
/// 3. Clicking a different model stops the old one and plays the new one.
/// </summary>
public class TouchPlay : MonoBehaviour
{
    public enum Language
    {
        English = 0,
        Hindi = 1,
        Odia = 2
    }

    [System.Serializable]
    public class ModelAudio
    {
        [Tooltip("The name of the model GameObject. This is used to match the clicked object to its audio clips.")]
        public string modelName;
        [Header("Audio Clips")]
        public AudioClip english;
        public AudioClip hindi;
        public AudioClip odia;
    }

    [Header("Language Settings")]
    [Tooltip("Default language is English. Can be changed at runtime via SetLanguage().")]
    public Language currentLanguage = Language.English;
    private const string PlayerPrefsKey = "app_language";

    [Header("Model & Audio Mappings")]
    public List<ModelAudio> modelClips = new List<ModelAudio>();

    [Header("Audio Behavior")]
    [Tooltip("If true, the audio clip will loop until stopped.")]
    public bool loop = false;
    [Range(0f, 1f)]
    public float volume = 1.0f;

    [Header("Raycast Settings")]
    [Tooltip("Which layers should be checked for clicks. Set to 'Everything' by default.")]
    public LayerMask clickableLayers = ~0;
    [Tooltip("How far to check for a click from the camera.")]
    public float rayDistance = 500f;

    [Header("Debugging")]
    [Tooltip("Enable to see logs for raycast hits/misses and audio playback.")]
    public bool debugLogs = false;

    private AudioSource _audioSource;
    private readonly Dictionary<string, ModelAudio> _lookup = new Dictionary<string, ModelAudio>();
    private string _currentlyPlayingModelKey;
    private Camera _mainCamera;
    private bool _isPaused;

    private void Awake()
    {
        _mainCamera = Camera.main;
        if (_mainCamera == null && debugLogs)
        {
            Debug.LogWarning("[TouchPlay] Could not find a camera tagged 'MainCamera'. Clicks will not work.");
        }

        // Ensure we have a dedicated AudioSource on a child object to avoid moving this GameObject.
        SetupAudioSource();

        // Load saved language preference or use the default.
        SetLanguage(PlayerPrefs.GetInt(PlayerPrefsKey, (int)currentLanguage), false);

        // Build the dictionary for quick lookups.
        BuildLookup();
    }

    private void Update()
    {
        HandleInput();
    }

    private void OnValidate()
    {
        // When values are changed in the Inspector, rebuild the lookup table.
        BuildLookup();
    }
    
    private void SetupAudioSource()
    {
        // Try to find an existing source on a child first.
        _audioSource = GetComponentInChildren<AudioSource>();

        if (_audioSource == null)
        {
            // If none exists, create a new child GameObject to host it.
            var audioHost = new GameObject("[TouchPlay] Audio Source Host");
            audioHost.transform.SetParent(this.transform);
            _audioSource = audioHost.AddComponent<AudioSource>();
        }

        _audioSource.playOnAwake = false;
    }

    /// <summary>
    /// Populates the dictionary for fast model lookup by name.
    /// </summary>
    private void BuildLookup()
    {
        _lookup.Clear();
        foreach (var entry in modelClips)
        {
            if (entry != null && !string.IsNullOrEmpty(entry.modelName))
            {
                var key = NormalizeKey(entry.modelName);
                if (!_lookup.ContainsKey(key))
                {
                    _lookup.Add(key, entry);
                }
            }
        }
    }

    /// <summary>
    /// Handles input from both the old Input Manager and the new Input System.
    /// </summary>
    private void HandleInput()
    {
        // Ignore clicks over UI elements.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Vector2? clickPosition = null;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            clickPosition = Mouse.current.position.ReadValue();
        }
        else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            clickPosition = Touchscreen.current.primaryTouch.position.ReadValue();
        }
#else
        if (Input.GetMouseButtonDown(0))
        {
            clickPosition = Input.mousePosition;
        }
        else if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            clickPosition = Input.GetTouch(0).position;
        }
#endif

        if (clickPosition.HasValue)
        {
            TryRaycast(clickPosition.Value);
        }
    }

    /// <summary>
    /// Performs a raycast from the camera to the click position.
    /// </summary>
    private void TryRaycast(Vector2 screenPosition)
    {
        if (_mainCamera == null) return;

        var ray = _mainCamera.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out var hit, rayDistance, clickableLayers))
        {
            HandleHit(hit.transform, hit.point);
        }
        else if (debugLogs)
        {
            Debug.Log("[TouchPlay] Raycast missed. Make sure your models have colliders and are on a clickable layer.");
        }
    }

    /// <summary>
    /// Called when a raycast hits a collider.
    /// </summary>
    private void HandleHit(Transform hitTransform, Vector3 hitPoint)
    {
        string hitModelKey = FindModelKeyForTransform(hitTransform);

        if (string.IsNullOrEmpty(hitModelKey))
        {
            if (debugLogs) Debug.Log($"[TouchPlay] Clicked on '{hitTransform.name}', but it does not match any model in the list.");
            return;
        }

        // Case 1: A different model is clicked.
        if (_currentlyPlayingModelKey != hitModelKey)
        {
            PlayAudioForKey(hitModelKey);
        }
        // Case 2: The same model is clicked again.
        else
        {
            // If it's playing, pause it.
            if (_audioSource.isPlaying)
            {
                PauseAudio();
            }
            // If it's paused, resume it.
            else if (_isPaused)
            {
                ResumeAudio();
            }
            // If it's stopped (e.g., finished playing), play it again.
            else
            {
                PlayAudioForKey(hitModelKey);
            }
        }
    }

    /// <summary>
    /// Finds the matching model key by checking the hit transform and its parents.
    /// </summary>
    private string FindModelKeyForTransform(Transform transform)
    {
        var currentTransform = transform;
        while (currentTransform != null)
        {
            var key = NormalizeKey(currentTransform.name);
            if (_lookup.ContainsKey(key))
            {
                return key;
            }
            currentTransform = currentTransform.parent;
        }
        return null;
    }

    /// <summary>
    /// Plays the audio associated with a given model key.
    /// </summary>
    private void PlayAudioForKey(string modelKey)
    {
        if (!_lookup.TryGetValue(modelKey, out var modelAudio))
        {
            return;
        }

        var clip = GetClipForLanguage(modelAudio, currentLanguage);
        if (clip == null)
        {
            if (debugLogs) Debug.LogWarning($"[TouchPlay] No audio clip found for model '{modelAudio.modelName}' in language '{currentLanguage}'.");
            return;
        }

        // Stop any previous audio.
        StopAudio();

        // Configure and play the new clip.
        _audioSource.clip = clip;
        _audioSource.volume = volume;
        _audioSource.loop = loop;
        _audioSource.spatialBlend = 0.0f; // Force 2D audio

        _audioSource.Play();
        _isPaused = false;
        _currentlyPlayingModelKey = modelKey;

        if (debugLogs) Debug.Log($"[TouchPlay] Playing audio for '{modelAudio.modelName}'.");
    }

    /// <summary>
    /// Stops the currently playing audio.
    /// </summary>
    public void StopAudio()
    {
        if (_audioSource.isPlaying || _isPaused)
        {
            _audioSource.Stop();
            if (debugLogs) Debug.Log($"[TouchPlay] Audio stopped for model '{_currentlyPlayingModelKey}'.");
        }
        _currentlyPlayingModelKey = null;
        _isPaused = false;
    }

    /// <summary>
    /// Pauses the currently playing audio.
    /// </summary>
    public void PauseAudio()
    {
        if (_audioSource.isPlaying)
        {
            _audioSource.Pause();
            _isPaused = true;
            if (debugLogs) Debug.Log($"[TouchPlay] Audio paused for model '{_currentlyPlayingModelKey}'.");
        }
    }

    /// <summary>
    /// Resumes the paused audio.
    /// </summary>
    public void ResumeAudio()
    {
        if (_isPaused)
        {
            _audioSource.UnPause();
            _isPaused = false;
            if (debugLogs) Debug.Log($"[TouchPlay] Audio resumed for model '{_currentlyPlayingModelKey}'.");
        }
    }

    /// <summary>
    /// Sets the active language for audio playback.
    /// </summary>
    public void SetLanguage(int languageIndex, bool savePreference = true)
    {
        if (System.Enum.IsDefined(typeof(Language), languageIndex))
        {
            currentLanguage = (Language)languageIndex;
            if (savePreference)
            {
                PlayerPrefs.SetInt(PlayerPrefsKey, languageIndex);
                PlayerPrefs.Save();
            }
            if (debugLogs) Debug.Log($"[TouchPlay] Language set to {currentLanguage}.");
        }
    }

    private AudioClip GetClipForLanguage(ModelAudio entry, Language lang)
    {
        switch (lang)
        {
            case Language.Hindi:
                return entry.hindi != null ? entry.hindi : entry.english; // Fallback to English
            case Language.Odia:
                return entry.odia != null ? entry.odia : entry.english; // Fallback to English
            default: // Language.English
                return entry.english;
        }
    }

    private string NormalizeKey(string name)
    {
        // Lowercase, no spaces, and remove "(Clone)" suffix for reliable matching.
        return name.Trim().ToLowerInvariant().Replace(" ", "").Replace("(clone)", "");
    }
}