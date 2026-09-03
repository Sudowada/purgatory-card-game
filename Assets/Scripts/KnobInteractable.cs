using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

public enum KnobType { Master, MusicVolume, BGVolume }

public class KnobInteractable : MonoBehaviour
{
    [Header("Knob Identity")]
    public KnobType knobType = KnobType.Master;

    [Header("Knob Settings")]
    public float totalRotation   = 270f;
    public float dragSensitivity = 0.5f;

    [Header("Audio Mixer")]
    [Tooltip("Drag your RadioMixer asset here")]
    public AudioMixer audioMixer;

    [Header("Click Sound")]
    public AudioSource clickAudioSource;
    public AudioClip   clickClip;
    [Range(0.01f, 1f)] public float clickInterval = 0.08f;
    [Range(0f,    1f)] public float clickVolume   = 0.4f;

    private bool   _isDragging      = false;
    private float  _lastMouseX;
    private float  _angleAccum      = 0f;
    private float  _normalizedValue = 0.5f;

    /// <summary>Current volume level 0-1. Read by KnobVolumeDisplay.</summary>
    public float NormalizedValue => _normalizedValue;
    private float  _totalAngle      = 0f;
    private Camera _cam;

    private float _lockedY;
    private float _lockedZ;
    private float _startX;

    private float _minAngle => -totalRotation / 2f;
    private float _maxAngle =>  totalRotation / 2f;

    private static KnobInteractable _activeDrag = null;

    // ✅ Hardcoded parameter names matching your Audio Mixer exactly
    private string MixerParam => knobType switch
    {
        KnobType.Master      => "Master",
        KnobType.MusicVolume => "MusicVolume",
        KnobType.BGVolume    => "BGVolume",
        _                    => "Master"
    };

    void Start()
    {
        _cam     = FindActiveCamera();
        _startX  = transform.localEulerAngles.x;
        _lockedY = transform.localEulerAngles.y;
        _lockedZ = transform.localEulerAngles.z;

        // Set initial volume to 50%
        ApplyVolume(_normalizedValue);
    }

    void Update()
    {
        if (_cam == null || !_cam.enabled)
            _cam = FindActiveCamera();

        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame && HitThisKnob())
        {
            _activeDrag = this;
            _isDragging = true;
            _lastMouseX = mouse.position.ReadValue().x;
            _angleAccum = 0f;
        }

        if (mouse.leftButton.wasReleasedThisFrame && _activeDrag == this)
        {
            _activeDrag = null;
            _isDragging = false;
        }

        if (!_isDragging || _activeDrag != this) return;

        float mouseX      = mouse.position.ReadValue().x;
        float deltaX      = (mouseX - _lastMouseX) * dragSensitivity;
        _lastMouseX       = mouseX;

        if (Mathf.Approximately(deltaX, 0f)) return;

        float newTotal    = Mathf.Clamp(_totalAngle + deltaX, _minAngle, _maxAngle);
        float actualDelta = newTotal - _totalAngle;
        _totalAngle       = newTotal;

        if (Mathf.Approximately(actualDelta, 0f)) return;

        // Spin the knob visually
        transform.localEulerAngles = new Vector3(
            _startX + _totalAngle,
            _lockedY,
            _lockedZ
        );

        // Update volume
        _normalizedValue = Mathf.InverseLerp(_minAngle, _maxAngle, _totalAngle);
        ApplyVolume(_normalizedValue);

        // Click sound
        _angleAccum += Mathf.Abs(actualDelta);
        if (_angleAccum >= clickInterval * 100f)
        {
            _angleAccum = 0f;
            PlayClick();
        }
    }

    void ApplyVolume(float normalized)
    {
        if (audioMixer == null)
        {
            Debug.LogWarning($"[{gameObject.name}] AudioMixer not assigned!");
            return;
        }

        float dB = normalized > 0.0001f
            ? Mathf.Log10(normalized) * 20f
            : -80f;

        if (knobType == KnobType.Master)
        {
            // ✅ Master knob sets ALL three groups simultaneously
            // This ensures muting master actually silences everything
            audioMixer.SetFloat("Master",      dB);
            audioMixer.SetFloat("MusicVolume", dB);
            audioMixer.SetFloat("BGVolume",    dB);
            Debug.Log($"[{gameObject.name}] Master → all groups set to {dB:F1}dB");
        }
        else
        {
            if (audioMixer.SetFloat(MixerParam, dB))
                Debug.Log($"[{gameObject.name}] {MixerParam} = {dB:F1}dB (normalized: {normalized:F2})");
            else
                Debug.LogError($"[{gameObject.name}] FAILED to set '{MixerParam}'");
        }
    }

    bool HitThisKnob()
    {
        if (_cam == null) return false;
        Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, 100f)) return false;
        Transform t = hit.transform;
        while (t != null)
        {
            if (t == transform) return true;
            t = t.parent;
        }
        return false;
    }

    Camera FindActiveCamera()
    {
        foreach (var cam in Camera.allCameras)
            if (cam.enabled && cam.gameObject.activeInHierarchy)
                return cam;
        return Camera.main;
    }

    void PlayClick()
    {
        if (clickAudioSource != null && clickClip != null)
            clickAudioSource.PlayOneShot(clickClip, clickVolume);
    }
}