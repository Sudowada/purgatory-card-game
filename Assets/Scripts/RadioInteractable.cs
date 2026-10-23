using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class RadioInteractable : MonoBehaviour
{
    [Header("Zoom Settings")]
    public Transform zoomTarget;
    public float zoomDuration = 0.6f;
    public AnimationCurve zoomCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [Header("Interaction")]
    public float interactDistance = 3f;
    [Header("References")]
    public FirstPersonController playerController;
    public Camera playerCamera;
    public GameObject knobUIHint;

    public static RadioInteractable Active { get; private set; }
    public bool CanUseControls => Active == this && zoomed && !zooming &&
        (SettingsUI.Instance == null || !SettingsUI.Instance.IsOpen);
    static int escapeFrame = -1;
    RadioHoverHighlight highlight;
    bool zoomed, zooming, captured;
    bool previousControllerEnabled, previousCursorVisible;
    CursorLockMode previousLock;
    Vector3 originalCameraPosition;
    Quaternion originalCameraRotation;

    void Awake()
    {
        highlight = GetComponent<RadioHoverHighlight>();
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerController == null) playerController = FindFirstObjectByType<FirstPersonController>();
        if (knobUIHint != null) knobUIHint.SetActive(false);
    }

    // SettingsUI calls this too, so Escape cannot both leave the radio and open
    // settings in one frame, regardless of script execution order.
    public static bool HandleEscape()
    {
        if (escapeFrame == Time.frameCount) return true;
        if (Active == null) return false;
        escapeFrame = Time.frameCount;
        Active.ForceExit();
        return true;
    }

    void Update()
    {
        var keys = Keyboard.current;
        if (keys == null) return;
        if (Active == this && keys.escapeKey.wasPressedThisFrame) { HandleEscape(); return; }
        if (captured && GameStateManager.Instance != null && GameStateManager.Instance.CurrentState != GameState.Exploration)
        { StopAllCoroutines(); Restore(); return; }
        if (zooming || zoomed || Active != null || !keys.eKey.wasPressedThisFrame) return;
        if (SettingsUI.Instance != null && SettingsUI.Instance.IsOpen) return;
        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState != GameState.Exploration) return;
        if (playerCamera == null || zoomTarget == null || playerController == null) return;
        if (!playerController.enabled) return;
        var ray = playerCamera.ScreenPointToRay(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        if (Physics.Raycast(ray, out var hit, interactDistance) && (hit.transform == transform || hit.transform.IsChildOf(transform)))
            StartCoroutine(ZoomIn());
    }

    IEnumerator ZoomIn()
    {
        Active = this;
        captured = true;
        zooming = true;
        originalCameraPosition = playerCamera.transform.position;
        originalCameraRotation = playerCamera.transform.rotation;
        previousControllerEnabled = playerController.enabled;
        previousLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        playerController.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (highlight != null) highlight.SetZoomedIn(true);
        float elapsed = 0f;
        while (elapsed < zoomDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = zoomCurve.Evaluate(Mathf.Clamp01(elapsed / zoomDuration));
            playerCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(originalCameraPosition, zoomTarget.position, t),
                Quaternion.Slerp(originalCameraRotation, zoomTarget.rotation, t));
            yield return null;
        }
        playerCamera.transform.SetPositionAndRotation(zoomTarget.position, zoomTarget.rotation);
        zoomed = true;
        zooming = false;
        if (knobUIHint != null) knobUIHint.SetActive(true);
    }

    IEnumerator ZoomOut()
    {
        zooming = true;
        zoomed = false;
        if (knobUIHint != null) knobUIHint.SetActive(false);
        var startPosition = playerCamera.transform.position;
        var startRotation = playerCamera.transform.rotation;
        float elapsed = 0f;
        while (elapsed < zoomDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = zoomCurve.Evaluate(Mathf.Clamp01(elapsed / zoomDuration));
            playerCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(startPosition, originalCameraPosition, t),
                Quaternion.Slerp(startRotation, originalCameraRotation, t));
            yield return null;
        }
        Restore();
    }

    public void ForceExit()
    {
        if (!captured) return;
        StopAllCoroutines();
        if (isActiveAndEnabled && playerCamera != null) StartCoroutine(ZoomOut());
        else Restore();
    }

    void Restore()
    {
        if (!captured) return;
        captured = false;
        if (playerCamera != null) playerCamera.transform.SetPositionAndRotation(originalCameraPosition, originalCameraRotation);
        bool exploring = GameStateManager.Instance == null || GameStateManager.Instance.CurrentState == GameState.Exploration;
        if (playerController != null) playerController.enabled = previousControllerEnabled && exploring;
        Cursor.lockState = exploring ? previousLock : CursorLockMode.None;
        Cursor.visible = exploring ? previousCursorVisible : true;
        if (knobUIHint != null) knobUIHint.SetActive(false);
        if (highlight != null) highlight.SetZoomedIn(false);
        zoomed = zooming = false;
        if (Active == this) Active = null;
    }
    void OnDisable() { StopAllCoroutines(); Restore(); }
}
