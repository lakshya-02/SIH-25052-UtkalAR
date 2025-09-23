using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ImageTracker : MonoBehaviour
{
    [Tooltip("Prefabs whose names match the reference image names in your XRReferenceImageLibrary.")]
    public GameObject[] ARPrefabs;

    [Tooltip("If true, destroy the model when tracking is lost (Vuforia-like). If false, just disable it.")]
    public bool destroyOnLost = true;

    private ARTrackedImageManager _trackedImages;
    private readonly Dictionary<TrackableId, GameObject> _spawned = new Dictionary<TrackableId, GameObject>();

    void Awake()
    {
        _trackedImages = GetComponent<ARTrackedImageManager>();
    }

    void OnEnable()
    {
        if (_trackedImages != null)
            _trackedImages.trackedImagesChanged += OnTrackedImagesChanged;
    }

    void OnDisable()
    {
        if (_trackedImages != null)
            _trackedImages.trackedImagesChanged -= OnTrackedImagesChanged;
    }

    private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs args)
    {
        // Added: instantiate and parent to the tracked image
        foreach (var tracked in args.added)
        {
            TryEnsureInstance(tracked);
        }

        // Updated: if tracking, ensure instance exists and active; else remove/disable
        foreach (var tracked in args.updated)
        {
            if (tracked.trackingState == TrackingState.Tracking)
            {
                TryEnsureInstance(tracked, setActive: true);
            }
            else
            {
                HandleLost(tracked);
            }
        }

        // Removed: clean up
        foreach (var tracked in args.removed)
        {
            HandleLost(tracked, force: true);
        }
    }

    private void TryEnsureInstance(ARTrackedImage tracked, bool setActive = true)
    {
        var id = tracked.trackableId;
        if (!_spawned.TryGetValue(id, out var go) || go == null)
        {
            var prefab = FindPrefabForReference(tracked.referenceImage.name);
            if (prefab == null)
            {
                Debug.LogWarning($"ImageTracker: No prefab found matching reference image '{tracked.referenceImage.name}'.");
                return;
            }

            go = Instantiate(prefab, tracked.transform);
            go.name = tracked.referenceImage.name; // clean name for any later lookups
            _spawned[id] = go;
        }

        if (go != null)
        {
            if (setActive)
            {
                if (!go.activeSelf) go.SetActive(true);
            }
        }
    }

    private void HandleLost(ARTrackedImage tracked, bool force = false)
    {
        var id = tracked.trackableId;
        if (_spawned.TryGetValue(id, out var go) && go != null)
        {
            if (destroyOnLost || force)
            {
                Destroy(go);
                _spawned.Remove(id);
            }
            else
            {
                go.SetActive(false);
            }
        }
    }

    private GameObject FindPrefabForReference(string referenceName)
    {
        foreach (var p in ARPrefabs)
        {
            if (p != null && p.name == referenceName)
                return p;
        }
        return null;
    }
}