using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public enum SwitchType { Lever, Binary }

public class PuzzleSwitch : MonoBehaviour
{
    [Header("Switch Type")]
    public SwitchType switchType = SwitchType.Lever;

    [Header("References")]
    public Camera playerCamera;
    public float interactDistance = 3f;
    public bool clickOnly;

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
    private bool  _isInteractable = false;
    private bool  _isAnimating    = false;
    private bool  _isDragging     = false;
    private bool  _inMiddle       = false;
    private Coroutine _snapCoroutine;

    private float _currentAngle;
    private bool _useWorldInteraction;
    private FirstPersonController _worldController;
    private bool _worldPress;
    private bool _worldDrag;
    private bool _previousLookLock;
    private float _worldDragDelta;
    private float _worldRestAngle;

    private const float MiddleThreshold = 0.15f;

    void Start()
    {
        if (!_useWorldInteraction)
            _currentAngle = leverUpAngle;
        else if (_snapCoroutine == null)
        {
            if (switchType == SwitchType.Binary) IsOn = _binaryState == 1;
            _currentAngle = RestAngle();
        }
        ApplyHandleAngle(_currentAngle);
        UpdateVisuals();
    }

    void Update()
    {
        if (_useWorldInteraction)
        {
            UpdateWorldInteraction();
            return;
        }

        if (_isSolved || !_isInteractable) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        // ── Drag handling ─────────────────────────────────────────────
        if (_isDragging)
        {
            if (mouse.leftButton.isPressed)
            {
                float mouseDelta = mouse.delta.y.ReadValue() * dragSensitivity;
                _currentAngle = Mathf.Clamp(
                    _currentAngle - mouseDelta,
                    Mathf.Min(leverUpAngle, leverDownAngle),
                    Mathf.Max(leverUpAngle, leverDownAngle));
                ApplyHandleAngle(_currentAngle);
                UpdateVisualsFromAngle();
            }
            else
            {
                _isDragging = false;
                SnapToNearestState();
            }
            return;
        }

        if (!mouse.leftButton.wasPressedThisFrame) return;

        Debug.Log($"[PuzzleSwitch] {gameObject.name} detected left click");

        // Raycast to check if clicking this switch
        Ray ray = playerCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            Debug.Log($"[PuzzleSwitch] {gameObject.name} raycast missed");
            return;
        }

        Debug.Log($"[PuzzleSwitch] {gameObject.name} raycast hit: {hit.transform.name}");

        if (hit.transform != transform && !hit.transform.IsChildOf(transform)) return;

        StartCoroutine(ClickOrDrag());
    }

    public void UseWorldInteraction(Camera camera, FirstPersonController controller)
    {
        _useWorldInteraction = true;
        if (camera != null) playerCamera = camera;
        _worldController = controller;
        ScreenReticle.EnsureExists();
    }

    void UpdateWorldInteraction()
    {
        var mouse = Mouse.current;
        if (_isSolved || playerCamera == null || mouse == null ||
            !FirstPersonController.AllowsWorldPuzzleInput(_worldController))
        {
            CancelWorldPress();
            return;
        }

        if (!_worldPress)
        {
            var ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            if (!Physics.Raycast(ray, out var hit, interactDistance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                hit.collider.GetComponentInParent<PuzzleSwitch>() != this)
                return;

            ScreenReticle.Instance?.ShowInteractionHint();
            if (!mouse.leftButton.wasPressedThisFrame) return;
            if (clickOnly)
            {
                OnClicked();
                return;
            }

            _worldPress = true;
            _worldDragDelta = 0f;
            if (_worldController != null)
            {
                _previousLookLock = _worldController.LookLocked;
                _worldController.LookLocked = true;
            }
            return;
        }

        if (!mouse.leftButton.isPressed)
        {
            bool dragged = _worldDrag;
            ReleaseWorldPress();
            if (dragged) SnapToNearestState();
            else OnClicked();
            return;
        }

        float delta = mouse.delta.ReadValue().y;
        if (!_worldDrag)
        {
            _worldDragDelta += delta;
            if (Mathf.Abs(_worldDragDelta) < 5f) return;

            _worldDrag = true;
            _worldRestAngle = RestAngle();
            if (_snapCoroutine != null)
            {
                StopCoroutine(_snapCoroutine);
                _snapCoroutine = null;
                _isAnimating = false;
            }
            delta = _worldDragDelta;
        }

        _currentAngle = Mathf.Clamp(_currentAngle - delta * dragSensitivity,
            Mathf.Min(leverUpAngle, leverDownAngle), Mathf.Max(leverUpAngle, leverDownAngle));
        ApplyHandleAngle(_currentAngle);
    }

    float RestAngle()
    {
        if (switchType == SwitchType.Binary)
            return Mathf.Lerp(leverDownAngle, leverUpAngle, (_binaryState + 1) * 0.5f);
        if (_inMiddle) return Mathf.Lerp(leverUpAngle, leverDownAngle, 0.5f);
        return IsOn ? leverUpAngle : leverDownAngle;
    }

    void OnDisable() => CancelWorldPress();

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) CancelWorldPress();
    }

    void ReleaseWorldPress()
    {
        if (_worldController != null) _worldController.LookLocked = _previousLookLock;
        _worldPress = false;
        _worldDrag = false;
    }

    void CancelWorldPress()
    {
        if (!_worldPress) return;

        if (_worldDrag)
        {
            _currentAngle = _worldRestAngle;
            ApplyHandleAngle(_currentAngle);
            UpdateVisuals();
        }
        ReleaseWorldPress();
    }

    IEnumerator ClickOrDrag()
    {
        var mouse = Mouse.current;
        float heldTime   = 0f;
        float mouseMoved = 0f;
        const float dragThreshold = 5f;
        const float holdThreshold = 0.15f;

        while (mouse.leftButton.isPressed)
        {
            heldTime   += Time.deltaTime;
            mouseMoved += Mathf.Abs(mouse.delta.y.ReadValue());
            if (heldTime > holdThreshold || mouseMoved > dragThreshold)
            {
                _isDragging = true;
                yield break;
            }
            yield return null;
        }
        OnClicked();
    }

    void OnClicked()
    {
        Debug.Log($"[PuzzleSwitch] OnClicked on {gameObject.name}");
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
        _isInteractable = interactable;
        Debug.Log($"[PuzzleSwitch] {gameObject.name} interactable set to {interactable}");
    }

    public void SetActivated(bool active)
    {
        IsOn = active;
        _leverNormal = active ? 0f : 1f;
        SetMiddleState(false);
        SnapTo(active ? leverUpAngle : leverDownAngle);
        UpdateVisuals();
    }

    public void SetSolvedState()
    {
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
