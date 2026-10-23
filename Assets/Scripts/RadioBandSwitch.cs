using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Top-right AM/FM switch. F remains a keyboard alternative.</summary>
public class RadioBandSwitch : MonoBehaviour
{
    public RadioTuner radio;
    public Transform visualPivot;
    public Vector3 localAxis = Vector3.right;
    public float amAngle = -20f;
    public float fmAngle = 20f;
    public AudioSource clickSource;
    public AudioClip clickClip;
    [Range(0f, 1f)] public float clickVolume = 0.3f;
    Quaternion initialRotation;
    RadioBand previousBand;

    void Start()
    {
        if (radio == null) radio = GetComponentInParent<RadioTuner>();
        if (visualPivot == null) visualPivot = transform;
        initialRotation = visualPivot.localRotation;
        if (radio != null && radio.State != null) previousBand = radio.State.Band;
        UpdateVisual();
    }
    void Update()
    {
        if (radio == null || radio.State == null) return;
        var mouse = Mouse.current;
        if (radio.CanInteract && mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            var camera = radio.GetComponent<RadioInteractable>().playerCamera;
            if (camera != null && Physics.Raycast(camera.ScreenPointToRay(mouse.position.ReadValue()), out var hit, 10f)
                && hit.collider.GetComponentInParent<RadioBandSwitch>() == this)
            {
                radio.SelectControl(RadioTuner.Control.Band);
                radio.ToggleBand();
            }
        }
        if (previousBand != radio.State.Band)
        {
            previousBand = radio.State.Band;
            if (clickSource != null && clickClip != null) clickSource.PlayOneShot(clickClip, clickVolume);
        }
        UpdateVisual();
    }
    void UpdateVisual()
    {
        if (radio == null || radio.State == null) return;
        visualPivot.localRotation = initialRotation * Quaternion.AngleAxis(
            radio.State.Band == RadioBand.AM ? amAngle : fmAngle, localAxis.normalized);
    }
}
