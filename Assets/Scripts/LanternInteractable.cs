using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class LanternInteractable : MonoBehaviour
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

    [Header("Knob")]
    [SerializeField] private Transform knob;
    [SerializeField] private float minAngle = 0f;
    [SerializeField] private float maxAngle = 180f;

    [Header("Light")]
    [SerializeField] private Light lanternLight;
    [SerializeField] private float minIntensity = 0.05f;
    [SerializeField] private float maxIntensity = 3.5f;

    public float NormalizedValue => _currentNormalized;

    private bool _isZoomedIn = false;
    private bool _isZooming  = false;
    private bool _isInteracting = false;
    private float _currentNormalized = 0.5f;
    private Vector3    _originalCamPos;
    private Quaternion _originalCamRot;

    void Start()
    {
        ApplyBrightness(_currentNormalized);
    }

    void Update()
    {
        if (_isZooming) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        // Exit on Escape or E
        if ((kb.escapeKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame) && _isZoomedIn)
        {
            StartCoroutine(ZoomOut());
            return;
        }

        // Interact on E
        if (!_isZoomedIn && kb.eKey.wasPressedThisFrame)
        {
            Ray ray = playerCamera.ScreenPointToRay(new Vector2(Screen.width / 2, Screen.height / 2));
            if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                    StartCoroutine(ZoomIn());
        }

        // Knob dragging while zoomed in
        if (_isZoomedIn)
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame) _isInteracting = true;
            if (mouse.leftButton.wasReleasedThisFrame) _isInteracting = false;

            if (_isInteracting)
            {
                float delta = mouse.delta.x.ReadValue() * 0.8f
                            + mouse.delta.y.ReadValue() * 0.4f;
                _currentNormalized = Mathf.Clamp01(_currentNormalized + delta * Time.deltaTime);
                ApplyBrightness(_currentNormalized);
                RotateKnob(_currentNormalized);
            }
        }
    }

    IEnumerator ZoomIn()
    {
        _isZooming = true;
        playerController.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        _originalCamPos = playerCamera.transform.position;
        _originalCamRot = playerCamera.transform.rotation;

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
        _isZoomedIn = true;
        _isZooming  = false;
    }

    IEnumerator ZoomOut()
    {
        _isZooming = true;
        _isInteracting = false;

        float elapsed  = 0f;
        Vector3    startPos = playerCamera.transform.position;
        Quaternion startRot = playerCamera.transform.rotation;

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
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        _isZoomedIn = false;
        _isZooming  = false;
    }

    public void ForceExit()
    {
        if (_isZoomedIn && !_isZooming) StartCoroutine(ZoomOut());
        else
        {
            StopAllCoroutines();
            playerController.enabled = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
            _isZoomedIn = _isZooming = _isInteracting = false;
        }
    }

    void ApplyBrightness(float normalized)
    {
        if (lanternLight != null)
            lanternLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, normalized);
    }

    void RotateKnob(float normalized)
    {
        if (knob == null) return;
        float angle = Mathf.Lerp(minAngle, maxAngle, normalized);
        knob.localEulerAngles = new Vector3(0f, angle, 0f);
    }
}