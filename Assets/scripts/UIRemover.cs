using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class UIRemover : MonoBehaviour
{
    [Tooltip("The UI RawImage to hide when an image is tracked.")]
    public RawImage instructionImage;

    [Tooltip("Assign the ARTrackedImageManager from your scene here.")]
    public ARTrackedImageManager trackedImageManager;

    private void OnEnable()
    {
        if (trackedImageManager != null)
        {
            trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
        }
        else
        {
            Debug.LogError("[UIRemover] ARTrackedImageManager is not assigned. The script will not work.", this);
        }
    }

    private void OnDisable()
    {
        if (trackedImageManager != null)
        {
            trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
        }
    }
    private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        bool isAnyImageBeingTracked = false;

        // Check all currently known tracked images.
        foreach (var trackedImage in trackedImageManager.trackables)
        {
            // If any image is actively being tracked, we set the flag and can stop checking.
            if (trackedImage.trackingState == TrackingState.Tracking)
            {
                isAnyImageBeingTracked = true;
                break;
            }
        }

        if (instructionImage != null)
        {
            instructionImage.gameObject.SetActive(!isAnyImageBeingTracked);
        }
    }
}
