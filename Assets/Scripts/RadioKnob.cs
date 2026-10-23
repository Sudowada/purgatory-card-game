using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Precision drag with discrete detents. No inertia or smoothing.</summary>
public class RadioKnob : MonoBehaviour
{
    public enum Function { Tuning, Volume }
    public Function function;
    public RadioTuner radio;
    public Transform visualPivot;
    public Vector3 localAxis = Vector3.forward;
    public float rotationDegrees = 270f;
    public bool reverseVisualRotation = true;
    [Tooltip("Normalized travel per screen pixel. 0.0015 requires about 667 pixels for a full turn.")]
    public float dragSensitivity = 0.0015f;
    [Header("Optional detent sound")]
    public AudioSource clickSource;
    public AudioClip clickClip;
    [Range(0f, 1f)] public float clickVolume = 0.3f;
    [Header("Hover and keyboard readout")]
    public bool showFloatingValue = true;
    public Transform readoutAnchor;
    public float readoutOffsetPixels = 58f;

    Quaternion initialRotation;
    bool dragging;
    RadioDialMotion motion;

    Vector2 lastPointer;

    static RadioKnob activeDrag;

    float CurrentValue => radio == null ? 0f : function == Function.Volume ? radio.volume : radio.NormalizedTuning;
    int Steps => function == Function.Tuning ? RadioStationCode.StationsPerBand - 1 : radio != null ? Mathf.Max(5, radio.volumeSteps) : 50;
    bool KeyboardSelected => radio != null && radio.KeyboardSelectionVisible && radio.SelectedControl ==
        (function == Function.Volume ? RadioTuner.Control.Volume : RadioTuner.Control.Tuning);
    float previousDisplayed;

    void Start()
    {
        if (radio == null) radio = GetComponentInParent<RadioTuner>();
        if (visualPivot == null) visualPivot = transform;
        initialRotation = visualPivot.localRotation;
        previousDisplayed = CurrentValue;
        DrawRotation();
    }
    void Update()
    {
        if (radio == null || !radio.CanInteract) { CancelDrag(); return; }
        var mouse = Mouse.current;
        if (mouse == null) { CancelDrag(); return; }
        if (mouse.leftButton.wasPressedThisFrame && activeDrag == null)
        {
            var camera = radio.GetComponent<RadioInteractable>().playerCamera;
            if (camera != null && Physics.Raycast(camera.ScreenPointToRay(mouse.position.ReadValue()), out var hit, 10f)
                && hit.collider.GetComponentInParent<RadioKnob>() == this)
            {
                dragging = true;
                activeDrag = this;
                motion = new RadioDialMotion(CurrentValue, Steps, dragSensitivity);
                radio.SelectControl(function == Function.Volume ? RadioTuner.Control.Volume : RadioTuner.Control.Tuning);
                lastPointer = mouse.position.ReadValue();
            }
        }
        if (!mouse.leftButton.isPressed) { CancelDrag(); DrawRotation(); return; }
        if (!dragging || activeDrag != this) { DrawRotation(); return; }

        // Retain fractional travel between detents, but ignore travel before grab.
        Vector2 pointer = mouse.position.ReadValue();
        float travel = pointer.x - lastPointer.x;
        lastPointer = pointer;
        motion.Move(travel);
        float snapped = motion.Value;
        if (function == Function.Tuning) radio.SetTuning(snapped); else radio.SetVolume(snapped);

        DrawRotation();
    }
    void DrawRotation()
    {
        if (!Mathf.Approximately(previousDisplayed, CurrentValue))
        {
            previousDisplayed = CurrentValue;
            if (clickSource != null && clickClip != null) clickSource.PlayOneShot(clickClip, clickVolume);
        }
        visualPivot.localRotation = initialRotation * Quaternion.AngleAxis((CurrentValue - 0.5f) * rotationDegrees * (reverseVisualRotation ? -1f : 1f), localAxis.normalized);
    }
    void OnGUI()
    {
        if (radio == null || radio.State == null || !radio.CanInteract) return;
        var camera = radio.GetComponent<RadioInteractable>().playerCamera;
        if (camera == null) return;
        Vector3 position = camera.WorldToScreenPoint(readoutAnchor != null ? readoutAnchor.position : transform.position);
        if (position.z <= 0f) return;

        string text = function == Function.Volume ? $"{Mathf.RoundToInt(radio.volume * 100)}%" : $"{radio.State.Frequency} Hz";
        bool hovered = RadioControlReadout.IsHovered(camera, this);
        RadioControlReadout.Draw(position, function == Function.Volume ? "VOL." : "TUNE", text,
            showFloatingValue && (dragging || KeyboardSelected || hovered), readoutOffsetPixels,
            function == Function.Tuning ? radio.Status : "");
    }
    void CancelDrag() { dragging = false; if (activeDrag == this) activeDrag = null; }
    void OnDisable() { CancelDrag(); }
}
