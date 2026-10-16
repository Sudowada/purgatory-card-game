using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections;

public enum SwitchType { Lever, Binary }

public class PuzzleSwitch : MonoBehaviour
{
    [Header("Switch Type")]
    public SwitchType switchType = SwitchType.Lever;

    [Header("References")]
    public Camera playerCamera;
    public float interactDistance = 3f;

    [Header("Lever Handle")]
    public Transform leverHandle;
    public float leverUpAngle   = 0f;
    public float leverDownAngle = 180f;

    [Header("Snap Animation")]
    public float snapDuration    = 0.25f;
    public float overshootAmount = 15f;
    public float settleDuration  = 0.08f;

    [Header("Drag Settings")]
    public float dragSensitivity = 1.2f;
    [Min(1f)] public float clickMovementThreshold = 6f;
    [Min(1f)] public float dragThreshold = 12f;
    [Min(0f)] public float dragArmTime = 0.15f;
    [Min(1f)] public float lookCancelAngle = 45f;

    private SwitchPanelInteract _panel;
    private FirstPersonController _controller;
    private bool _pointerHeld;
    private float _pressTime;
    private Vector2 _gestureDelta;
    private float _gestureTravel;
    private Quaternion _pressRotation;
    private float _pressAngle;
    private Vector3 _hitPoint;
    private Vector3 _pressWorldPoint;
    private float _snapTargetAngle;
    private static PuzzleSwitch _activeSwitch;

    [Header("Lever Light")]
    public Light leverLight;
    public Color leverOnColor  = new Color(1f, 0.6f, 0.1f);
    public Color leverOffColor = new Color(0.1f, 0.1f, 0.1f);

    [Header("Morse (Lever only — assign MorseFlicker component)")]
    public MorseFlicker morseFlicker;

    [Header("Binary Switch Lights")]
    public Light binaryLight;
    public Color binaryUpColor        = Color.red;
    public Color binaryDownColor      = Color.blue;
    public float binaryLightIntensity = 1.5f;

    [Header("Renderer")]
    public Renderer switchRenderer;
    public int materialIndex = 0;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip   clickClip;
    public AudioClip   clunkClip;

    // State
    public bool IsOn { get; private set; } = false;

    private int   _binaryState    = 1;
    private float _leverNormal    = 0f;
    private bool  _isSolved       = false;
    private bool  _isInteractable = true;
    private bool  _isAnimating    = false;
    private bool  _isDragging     = false;
    private bool  _inMiddle       = false;
    private Coroutine _snapCoroutine;

    private float _currentAngle;

    private const float MiddleThreshold = 0.15f;

    void Start()
    {
        _currentAngle = leverUpAngle;
        ApplyHandleAngle(_currentAngle);
        UpdateVisuals();
    }

    public void ConfigurePanel(SwitchPanelInteract panel)
    {
        _panel = panel;
        if (panel.playerCamera != null) playerCamera = panel.playerCamera;
        _controller = panel.playerController;
    }

    bool CanInteract()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        if (_controller == null && playerCamera != null)
            _controller = playerCamera.GetComponentInParent<FirstPersonController>();
        return !_isSolved && _isInteractable && isActiveAndEnabled
            && (_panel == null || _panel.isActiveAndEnabled)
            && playerCamera != null && playerCamera.isActiveAndEnabled
            && _controller != null && _controller.isActiveAndEnabled
            && Cursor.lockState == CursorLockMode.Locked
            && (GameStateManager.Instance == null
                || GameStateManager.Instance.CurrentState == GameState.Exploration)
            && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject());
    }

    float InteractionDistance => _panel != null
        ? Mathf.Min(interactDistance, _panel.interactDistance) : interactDistance;

    bool HitThisSwitch()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit, InteractionDistance)
            || (hit.transform != transform && !hit.transform.IsChildOf(transform))) return false;
        _hitPoint = hit.point;
        return true;
    }

    // Runs after FirstPersonController.Update so raycasts use this frame's look.
    void LateUpdate()
    {
        var mouse = Mouse.current;
        if (mouse == null || !CanInteract() || mouse.rightButton.isPressed
            || (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame))
        {
            CancelInteraction();
            return;
        }

        if (!_pointerHeld)
        {
            if (_activeSwitch != null || _isAnimating
                || !mouse.leftButton.wasPressedThisFrame || !HitThisSwitch()) return;
            _activeSwitch = this;
            _pointerHeld = true;
            _pressTime = Time.unscaledTime;
            _pressRotation = playerCamera.transform.rotation;
            _pressAngle = _currentAngle;
            _pressWorldPoint = _hitPoint;
            _gestureDelta = Vector2.zero;
            _gestureTravel = 0f;
            // The down-frame delta happened before capture; do not count it.
            return;
        }

        Vector2 delta = mouse.delta.ReadValue();
        _gestureDelta += delta;
        _gestureTravel += delta.magnitude;

        // A turn-away is always look input, even after a drag has armed.
        if (Vector3.Distance(playerCamera.transform.position, _pressWorldPoint) > InteractionDistance
            || Quaternion.Angle(_pressRotation, playerCamera.transform.rotation) > lookCancelAngle
            || Mathf.Abs(_gestureDelta.x) > Mathf.Max(dragThreshold, clickMovementThreshold))
        {
            CancelInteraction();
            return;
        }

        if (!_isDragging && _gestureTravel > clickMovementThreshold)
        {
            // Require a brief intentional hold, then predominantly vertical movement.
            // A quick camera swipe cancels rather than becoming a click on release.
            if (Time.unscaledTime - _pressTime < dragArmTime
                || Mathf.Abs(_gestureDelta.x) > Mathf.Abs(_gestureDelta.y))
            {
                CancelInteraction();
                return;
            }
            if (Mathf.Abs(_gestureDelta.y) >= Mathf.Max(dragThreshold, clickMovementThreshold))
                _isDragging = true;
        }

        if (_isDragging)
        {
            _currentAngle = Mathf.Clamp(_pressAngle - _gestureDelta.y * dragSensitivity,
                Mathf.Min(leverUpAngle, leverDownAngle),
                Mathf.Max(leverUpAngle, leverDownAngle));
            ApplyHandleAngle(_currentAngle);
            UpdateVisualsFromAngle();
        }

        if (mouse.leftButton.wasReleasedThisFrame || !mouse.leftButton.isPressed)
        {
            bool dragged = _isDragging;
            bool clicked = !dragged && _gestureTravel <= clickMovementThreshold && HitThisSwitch();
            _pointerHeld = _isDragging = false;
            _activeSwitch = null;
            if (dragged) SnapToNearestState();
            else if (clicked) OnClicked();
        }
    }

    public bool TryClickFromPanel()
    {
        if (!CanInteract() || _isAnimating || _activeSwitch != null
            || (Mouse.current != null && (Mouse.current.leftButton.isPressed
                || Mouse.current.rightButton.isPressed)) || !HitThisSwitch()) return false;
        OnClicked();
        return true;
    }

    public void CancelInteraction()
    {
        if (_isDragging)
        {
            // Restore the preview only; cancellation must never notify puzzle logic.
            _currentAngle = _pressAngle;
            ApplyHandleAngle(_currentAngle);
            UpdateVisuals();
        }
        _pointerHeld = _isDragging = false;
        if (_activeSwitch == this) _activeSwitch = null;
    }

    void OnDisable()
    {
        CancelInteraction();
        // Deactivating a GameObject stops coroutines. Finish an accepted snap
        // so re-enabling the switch cannot leave input blocked indefinitely.
        if (_snapCoroutine != null) StopCoroutine(_snapCoroutine);
        _snapCoroutine = null;
        if (_isAnimating)
        {
            _currentAngle = _snapTargetAngle;
            ApplyHandleAngle(_currentAngle);
            _isAnimating = false;
        }
    }
    void OnApplicationFocus(bool focused)
    {
        if (!focused) CancelInteraction();
    }

    void OnClicked()
    {
        PlaySound(clickClip);
        if (switchType == SwitchType.Lever)
            HandleLeverClick();
        else
            HandleBinaryClick();
    }

    void HandleLeverClick()
    {
        IsOn = !IsOn;
        _leverNormal = IsOn ? 0f : 1f;
        SetMiddleState(false);
        SnapTo(IsOn ? leverUpAngle : leverDownAngle);
        _puzzle?.OnSwitchPressed(this);
        _colorPuzzle?.OnSwitchStateChanged(this, IsOn);
        UpdateVisuals();
    }

    void HandleBinaryClick()
    {
        _binaryState = _binaryState >= 0 ? -1 : 1;
        IsOn = _binaryState == 1;
        SnapTo(_binaryState == 1 ? leverUpAngle : leverDownAngle);
        _puzzle?.OnSwitchPressed(this);
        _colorPuzzle?.OnSwitchStateChanged(this, IsOn);
        UpdateVisuals();
    }

    void SnapToNearestState()
    {
        float normalized = Mathf.InverseLerp(leverUpAngle, leverDownAngle, _currentAngle);

        if (switchType == SwitchType.Binary)
        {
            if (normalized < 0.5f - MiddleThreshold)
            {
                _binaryState = 1; IsOn = true;
                SetMiddleState(false);
                SnapTo(leverUpAngle);
            }
            else if (normalized > 0.5f + MiddleThreshold)
            {
                _binaryState = -1; IsOn = false;
                SetMiddleState(false);
                SnapTo(leverDownAngle);
            }
            else
            {
                _binaryState = 0; IsOn = false;
                SetMiddleState(false);
                SnapTo(Mathf.Lerp(leverUpAngle, leverDownAngle, 0.5f));
            }
        }
        else
        {
            if (normalized < 0.5f - MiddleThreshold)
            {
                IsOn = true; _leverNormal = 0f;
                SetMiddleState(false);
                SnapTo(leverUpAngle);
            }
            else if (normalized > 0.5f + MiddleThreshold)
            {
                IsOn = false; _leverNormal = 1f;
                SetMiddleState(false);
                SnapTo(leverDownAngle);
            }
            else
            {
                IsOn = false; _leverNormal = 0.5f;
                SetMiddleState(true);
                SnapTo(Mathf.Lerp(leverUpAngle, leverDownAngle, 0.5f));
            }
        }

        _puzzle?.OnSwitchPressed(this);
        _colorPuzzle?.OnSwitchStateChanged(this, IsOn);
        UpdateVisuals();
    }

    void SetMiddleState(bool entering)
    {
        if (switchType != SwitchType.Lever) return;
        if (entering && !_inMiddle)
        {
            _inMiddle = true;
            morseFlicker?.StartMorse();
        }
        else if (!entering && _inMiddle)
        {
            _inMiddle = false;
            morseFlicker?.StopMorse();
        }
    }

    void SnapTo(float targetAngle)
    {
        _snapTargetAngle = targetAngle;
        if (_snapCoroutine != null) StopCoroutine(_snapCoroutine);
        _snapCoroutine = StartCoroutine(SnapCoroutine(targetAngle));
    }

    IEnumerator SnapCoroutine(float targetAngle)
    {
        _isAnimating = true;
        float startAngle = _currentAngle;
        float overshootTarget = targetAngle +
            (targetAngle > startAngle ? overshootAmount : -overshootAmount);

        float elapsed = 0f;
        while (elapsed < snapDuration)
        {
            elapsed += Time.deltaTime;
            float t     = Mathf.Clamp01(elapsed / snapDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            _currentAngle = Mathf.Lerp(startAngle, overshootTarget, eased);
            ApplyHandleAngle(_currentAngle);
            yield return null;
        }

        float settleStart = _currentAngle;
        elapsed = 0f;
        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / settleDuration);
            _currentAngle = Mathf.Lerp(settleStart, targetAngle, t);
            ApplyHandleAngle(_currentAngle);
            yield return null;
        }

        _currentAngle = targetAngle;
        ApplyHandleAngle(_currentAngle);
        PlaySound(clunkClip);
        _isAnimating = false;
    }

    void ApplyHandleAngle(float angle)
    {
        if (leverHandle == null) return;
        Vector3 e = leverHandle.localEulerAngles;
        leverHandle.localEulerAngles = new Vector3(e.x, angle, e.z);
    }

    void UpdateVisualsFromAngle()
    {
        if (switchType != SwitchType.Lever) return;
        float normalized = Mathf.InverseLerp(leverUpAngle, leverDownAngle, _currentAngle);
        bool inMiddle = normalized > 0.5f - MiddleThreshold &&
                        normalized < 0.5f + MiddleThreshold;

        if (leverLight != null && !inMiddle)
        {
            leverLight.color     = normalized < 0.5f ? leverOnColor : leverOffColor;
            leverLight.intensity = normalized < 0.5f ? 1.2f : 0f;
        }
    }

    void UpdateVisuals()
    {
        if (switchType == SwitchType.Lever)
        {
            if (!_inMiddle && leverLight != null)
            {
                leverLight.color     = IsOn ? leverOnColor : leverOffColor;
                leverLight.intensity = IsOn ? 1.2f : 0f;
            }
            UpdateRendererColor(IsOn ? leverOnColor : leverOffColor);
        }
        else
        {
            if (binaryLight != null)
            {
                switch (_binaryState)
                {
                    case 1:
                        binaryLight.color     = binaryUpColor;
                        binaryLight.intensity = binaryLightIntensity;
                        break;
                    case -1:
                        binaryLight.color     = binaryDownColor;
                        binaryLight.intensity = binaryLightIntensity;
                        break;
                    default:
                        binaryLight.intensity = 0f;
                        break;
                }
            }
            UpdateRendererColor(_binaryState == 1 ? binaryUpColor
                               : _binaryState == -1 ? binaryDownColor
                               : Color.black);
        }
    }

    void UpdateRendererColor(Color color)
    {
        if (switchRenderer == null) return;
        var mats = switchRenderer.materials;
        if (materialIndex < mats.Length)
        {
            mats[materialIndex].SetColor("_BaseColor",     color);
            mats[materialIndex].SetColor("_EmissionColor", color * 2f);
            mats[materialIndex].EnableKeyword("_EMISSION");
        }
        switchRenderer.materials = mats;
    }

    private SwitchSequencePuzzle _puzzle;
    private ColorSequencePuzzle _colorPuzzle;

    public void RegisterPuzzle(SwitchSequencePuzzle puzzle) => _puzzle = puzzle;

    public void RegisterColorPuzzle(ColorSequencePuzzle puzzle) => _colorPuzzle = puzzle;

    public void SetInteractable(bool interactable)
    {
        if (!interactable) CancelInteraction();
        _isInteractable = interactable;
    }

    public void SetActivated(bool active)
    {
        CancelInteraction();
        IsOn = active;
        _leverNormal = active ? 0f : 1f;
        SetMiddleState(false);
        SnapTo(active ? leverUpAngle : leverDownAngle);
        UpdateVisuals();
    }

    public void SetSolvedState()
    {
        CancelInteraction();
        _isSolved    = true;
        IsOn         = true;
        _leverNormal = 0f;
        SetMiddleState(false);
        SnapTo(leverUpAngle);
        UpdateVisuals();
    }

    public int GetBinaryState() => _binaryState;

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}