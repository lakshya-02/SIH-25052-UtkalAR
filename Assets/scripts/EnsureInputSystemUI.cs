using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM && UNITY_INPUT_SYSTEM_EXISTS
using UnityEngine.InputSystem.UI;
#endif

// Attach this to any GameObject in your first scene (e.g., an empty object named _Bootstrap).
// It will ensure the EventSystem uses the correct UI input module when the new Input System is active.
public class EnsureInputSystemUI : MonoBehaviour
{
    void Awake()
    {
        var es = EventSystem.current;
        if (es == null)
        {
            // Try find or create
            es = FindObjectOfType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem));
                es = go.GetComponent<EventSystem>();
            }
        }

        if (es == null) return;

#if ENABLE_INPUT_SYSTEM && UNITY_INPUT_SYSTEM_EXISTS
        // If the project uses the new Input System (Active Input Handling includes it),
        // replace StandaloneInputModule with InputSystemUIInputModule
        var legacy = es.GetComponent<StandaloneInputModule>();
        var newModule = es.GetComponent<InputSystemUIInputModule>();
        if (newModule == null)
        {
            newModule = es.gameObject.AddComponent<InputSystemUIInputModule>();
        }
        if (legacy != null)
        {
            Destroy(legacy);
        }
#else
        // If only legacy is enabled, make sure a StandaloneInputModule exists
        var legacy = es.GetComponent<StandaloneInputModule>();
        if (legacy == null)
        {
            es.gameObject.AddComponent<StandaloneInputModule>();
        }
#endif
    }
}
