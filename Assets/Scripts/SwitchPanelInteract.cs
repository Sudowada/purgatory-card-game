using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// Zooms into the switch panel. While zoomed:
/// - Cursor is unlocked and visible
/// - Inside boundary: subtle parallax sway, switches are interactable
/// - Push to edge and keep going: transitions to free look
/// - Look back at panel: smoothly re-locks to zoom point
/// </summary>
public class SwitchPanelInteract : MonoBehaviour
{
    [Header("Zoom Settings")]
    public Transform zoomTarget;
    public float zoomDuration = 0.6f;
    public AnimationCurve zoomCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Interaction")]
    public float interactDistance = 5f;

    [Header("References")]
    public FirstPersonController playerController;
    public Camera playerCamera;

    [Header("Switches")]
    public PuzzleSwitch[] switches = new PuzzleSwitch[4];

    [Header("Parallax Sway (locked mode)")]
    public float parallaxStrength  = 0.004f;
    public float parallaxStrengthY = 0.002f;
    public float maxParallaxOffset = 0.04f;
    public float parallaxSmoothing = 6f;

    [Header("Idle Sway")]
    public float idleSwayStrength = 0.003f;
    public float idleSwaySpeed    = 0.8f;

    [Header("Free Look Settings")]
    [Tooltip("Angle from panel center before free look kicks in")]
    public float freeLookThreshold = 30f;
    [Tooltip("How fast the camera rotates in free look")]
    public float freeLookSensitivity = 0.15f;
    [Tooltip("Angle from panel center to re-lock (must be less than freeLookThreshold)")]
    public float reLockAngle = 20f;
    [Tooltip("How fast the camera smoothly re-locks to the panel")]
    public float reLockSpeed = 3f;

    // ── State Machine ─────────────────────────────────────────────────
    private enum PanelState { Outside, ZoomingIn, Locked, TransitionToFree, FreeLook, ReLocking, ZoomingOut }
    private PanelState _state = PanelState.Outside;

    private Vector3    _originalCamPos;
    private Quaternion _originalCamRot;

    // Parallax
    private Vector3 _parallaxOffset       = Vector3.zero;
    private Vector3 _parallaxTargetOffset = Vector3.zero;
    private float   _idleSwayTime         = 0f;

    // Free look
    private float _freeLookYaw   = 0f;
    private float _freeLookPitch = 0f;

    // Re-lock interpolation
    private Quaternion _reLockStartRot;

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        switch (_state)
        {
            case PanelState.Outside:
                HandleOutsideUpdate(kb);
                break;
            case PanelState.Locked:
                HandleLockedUpdate(kb);
                break;
            case PanelState.FreeLook:
                HandleFreeLookUpdate(kb);
                break;
            case PanelState.ReLocking:
                HandleReLockingUpdate();
                break;
        }
    }

    // ── Outside ───────────────────────────────────────────────────────

    void HandleOutsideUpdate(Keyboard kb)
    {
        if (!kb.eKey.wasPressedThisFrame) return;

        Ray ray = playerCamera.ScreenPointToRay(
            new Vector2(Screen.width / 2f, Screen.height / 2f));
        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance)) return;

        foreach (var sw in switches)
        {
            if (sw == null) continue;
            if (hit.transform == sw.transform || hit.transform.IsChildOf(sw.transform))
            {
                StartCoroutine(ZoomIn());
                return;
            }
        }
    }

    // ── Locked (panel view with parallax) ─────────────────────────────

    void HandleLockedUpdate(Keyboard kb)
    {
        if (kb.escapeKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)
        {
            StartCoroutine(ZoomOut());
            return;
        }

        var mouse = Mouse.current;
        if (mouse == null) return;

        // Parallax sway
        Vector2 delta = mouse.delta.ReadValue();

        _parallaxTargetOffset.x = Mathf.Clamp(
            _parallaxTargetOffset.x + delta.x * parallaxStrength,
            -maxParallaxOffset, maxParallaxOffset);
        _parallaxTargetOffset.y = Mathf.Clamp(
            _parallaxTargetOffset.y + delta.y * parallaxStrengthY,
            -maxParallaxOffset, maxParallaxOffset);

        // Idle breathing
        _idleSwayTime += Time.deltaTime * idleSwaySpeed;
        float idleX = (Mathf.PerlinNoise(_idleSwayTime, 0f) - 0.5f) * 2f * idleSwayStrength;
        float idleY = (Mathf.PerlinNoise(0f, _idleSwayTime) - 0.5f) * 2f * idleSwayStrength;

        // Slowly drift back toward center
        _parallaxTargetOffset = Vector3.Lerp(_parallaxTargetOffset, Vector3.zero,
            Time.deltaTime * 0.4f);
        _parallaxOffset = Vector3.Lerp(_parallaxOffset, _parallaxTargetOffset,
            Time.deltaTime * parallaxSmoothing);

        // Apply position sway
        Vector3 basePos = zoomTarget.position;
        playerCamera.transform.position = basePos
            + zoomTarget.right * (_parallaxOffset.x + idleX)
            + zoomTarget.up    * (_parallaxOffset.y + idleY);

        // Check if mouse is pushing past the threshold boundary
        // Use accumulated offset as a proxy for "pushing to edge"
        float offsetMagnitude = _parallaxTargetOffset.magnitude;
        bool pushingOut = offsetMagnitude >= maxParallaxOffset * 0.95f &&
                          delta.magnitude > 1f;

        if (pushingOut)
        {
            // Transition to free look
            _state = PanelState.FreeLook;
            _freeLookYaw   = zoomTarget.eulerAngles.y;
            _freeLookPitch = zoomTarget.eulerAngles.x;
            SetSwitchesInteractable(false);
            Debug.Log("[SwitchPanel] Entering free look");
        }
        else
        {
            // Keep rotation locked to zoom target
            playerCamera.transform.rotation = zoomTarget.rotation;
        }
    }

    // ── Free Look ─────────────────────────────────────────────────────

    void HandleFreeLookUpdate(Keyboard kb)
    {
        if (kb.escapeKey.wasPressedThisFrame)
        {
            StartCoroutine(ZoomOut());
            return;
        }

        var mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 delta = mouse.delta.ReadValue();
        _freeLookYaw   += delta.x * freeLookSensitivity;
        _freeLookPitch -= delta.y * freeLookSensitivity;
        _freeLookPitch  = Mathf.Clamp(_freeLookPitch, -89f, 89f);

        playerCamera.transform.rotation = Quaternion.Euler(_freeLookPitch, _freeLookYaw, 0f);
        playerCamera.transform.position = zoomTarget.position; // stay at zoom position

        // Check angle back toward panel
        float angleToPanel = Quaternion.Angle(
            playerCamera.transform.rotation, zoomTarget.rotation);

        if (angleToPanel <= reLockAngle)
        {
            // Start smooth re-lock
            _state = PanelState.ReLocking;
            _reLockStartRot = playerCamera.transform.rotation;
            Debug.Log("[SwitchPanel] Re-locking to panel");
        }
    }

    // ── Re-Locking ────────────────────────────────────────────────────

    void HandleReLockingUpdate()
    {
        float angleToPanel = Quaternion.Angle(
            playerCamera.transform.rotation, zoomTarget.rotation);

        playerCamera.transform.rotation = Quaternion.Slerp(
            playerCamera.transform.rotation,
            zoomTarget.rotation,
            Time.deltaTime * reLockSpeed);

        playerCamera.transform.position = zoomTarget.position;

        // Once close enough, fully lock and re-enable switches
        if (angleToPanel < 1f)
        {
            playerCamera.transform.rotation = zoomTarget.rotation;
            _parallaxOffset       = Vector3.zero;
            _parallaxTargetOffset = Vector3.zero;
            _state = PanelState.Locked;
            SetSwitchesInteractable(true);
            Debug.Log("[SwitchPanel] Re-locked to panel");
        }
    }

    // ── Zoom Coroutines ───────────────────────────────────────────────

    IEnumerator ZoomIn()
    {
        _state = PanelState.ZoomingIn;
        playerController.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        _originalCamPos = playerCamera.transform.position;
        _originalCamRot = playerCamera.transform.rotation;

        _parallaxOffset       = Vector3.zero;
        _parallaxTargetOffset = Vector3.zero;
        _idleSwayTime         = Random.Range(0f, 100f);

        float elapsed = 0f;
        while (elapsed < zoomDuration)
        {
            elapsed += Time.deltaTime;
            float t = zoomCurve.Evaluate(Mathf.Clamp01(elapsed / zoomDuration));
            playerCamera.transform.position = Vector3.Lerp(_originalCamPos, zoomTarget.position, t);
            playerCamera.transform.rotation = Quaternion.Slerp(_originalCamRot, zoomTarget.rotation, t);
            yield return null;
        }

        playerCamera.transform.position = zoomTarget.position;
        playerCamera.transform.rotation = zoomTarget.rotation;

        SetSwitchesInteractable(true);
        _state = PanelState.Locked;
    }

    IEnumerator ZoomOut()
    {
        _state = PanelState.ZoomingOut;
        SetSwitchesInteractable(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;

        Vector3    startPos = playerCamera.transform.position;
        Quaternion startRot = playerCamera.transform.rotation;

        float elapsed = 0f;
        while (elapsed < zoomDuration)
        {
            elapsed += Time.deltaTime;
            float t = zoomCurve.Evaluate(Mathf.Clamp01(elapsed / zoomDuration));
            playerCamera.transform.position = Vector3.Lerp(startPos, _originalCamPos, t);
            playerCamera.transform.rotation = Quaternion.Slerp(startRot, _originalCamRot, t);
            yield return null;
        }

        playerCamera.transform.position = _originalCamPos;
        playerCamera.transform.rotation = _originalCamRot;
        playerController.enabled = true;
        _parallaxOffset       = Vector3.zero;
        _parallaxTargetOffset = Vector3.zero;
        _state = PanelState.Outside;
    }

    void SetSwitchesInteractable(bool interactable)
    {
        foreach (var sw in switches)
            if (sw != null) sw.SetInteractable(interactable);
    }

    public void ForceExit()
    {
        StopAllCoroutines();
        SetSwitchesInteractable(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        playerController.enabled = true;
        _parallaxOffset = _parallaxTargetOffset = Vector3.zero;
        _state = PanelState.Outside;
    }
}