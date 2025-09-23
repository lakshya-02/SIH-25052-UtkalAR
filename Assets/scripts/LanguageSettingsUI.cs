using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LanguageSettingsUI : MonoBehaviour
{
    // Default language if none saved yet
    public TouchPlay.Language defaultLanguage = TouchPlay.Language.English;

    [Header("Optional UI References")]
    [Tooltip("Standard UI Dropdown (UGUI). If set, hook its OnValueChanged to OnDropdownChanged.")]
    public Dropdown dropdown;

    [Tooltip("TextMeshPro Dropdown. If set, hook its OnValueChanged to OnDropdownChanged.")]
    public TMP_Dropdown tmpDropdown;

    private const string PlayerPrefsKey = "app_language"; // must match TouchPlay

    private void Start()
    {
        // Load saved language or fallback to default
        int saved = PlayerPrefs.GetInt(PlayerPrefsKey, (int)defaultLanguage);
        saved = Mathf.Clamp(saved, 0, 2);

        // Initialize UI without firing events
        SetUIValueWithoutNotify(saved);

        // Apply to existing TouchPlay components without re-saving
        ApplyLanguage(saved, save: false);
    }

    private void SetUIValueWithoutNotify(int value)
    {
        if (dropdown != null)
        {
            dropdown.SetValueWithoutNotify(value);
        }
        if (tmpDropdown != null)
        {
            tmpDropdown.SetValueWithoutNotify(value);
        }
    }

    public void OnDropdownChanged(int value)
    {
        ApplyLanguage(value, save: true);
    }

    public void SetEnglish() => ApplyLanguage(0, save: true);
    public void SetHindi() => ApplyLanguage(1, save: true);
    public void SetOdia() => ApplyLanguage(2, save: true);

    private void ApplyLanguage(int value, bool save)
    {
        value = Mathf.Clamp(value, 0, 2);

        if (save)
        {
            PlayerPrefs.SetInt(PlayerPrefsKey, value);
            PlayerPrefs.Save();
        }

        // Update all TouchPlay instances in the scene (active or inactive)
        var all = FindObjectsOfType<TouchPlay>(includeInactive: true);
        foreach (var tp in all)
        {
            tp.SetLanguage(value);
        }

        // Keep UI in sync (in case the caller was a button)
        SetUIValueWithoutNotify(value);
    }
}
