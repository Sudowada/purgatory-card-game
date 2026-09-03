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

    private RadioHoverHighlight _highlight;
    private bool _isZoomedIn = false;
    private bool _isZooming  = false;
    private Vector3    _originalCamPos;
    private Quaternion _originalCamRot;

    void Awake()
    {
        _highlight = GetComponent<RadioHoverHighlight>();
        if (knobUIHint != null) knobUIHint.SetActive(false);
    }

    void Update()
    {
        if (_isZooming) return;
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.escapeKey.wasPressedThisFrame && _isZoomedIn)
        {
            StartCoroutine(ZoomOut());
            return;
        }

        if (!_isZoomedIn && kb.eKey.wasPressedThisFrame)
        {
            Ray ray = playerCamera.ScreenPointToRay(new Vector2(Screen.width / 2, Screen.height / 2));
            if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                    StartCoroutine(ZoomIn());
        }
    }

    IEnumerator ZoomIn()
    {
        _isZooming = true;
        if (_highlight) _highlight.SetZoomedIn(true);
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
        if (knobUIHint != null) knobUIHint.SetActive(true);
        _isZoomedIn = true;
        _isZooming  = false;
    }

    IEnumerator ZoomOut()
    {
        _isZooming = true;
        if (knobUIHint != null) knobUIHint.SetActive(false);

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
        if (_highlight) _highlight.SetZoomedIn(false);
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
            if (knobUIHint != null) knobUIHint.SetActive(false);
            if (_highlight) _highlight.SetZoomedIn(false);
            playerController.enabled = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
            _isZoomedIn = _isZooming = false;
        }
    }
}