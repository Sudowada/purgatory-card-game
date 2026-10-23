using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[RequireComponent(typeof(RadioInteractable))]
public class RadioTuner : MonoBehaviour
{
    public enum Control { Volume, Band, Tuning }
    public Control SelectedControl { get; private set; } = Control.Tuning;
    public bool KeyboardSelectionVisible { get; private set; }
    [Header("Keyboard adjustment")]
    [Range(5, 100)] public int volumeSteps = 50;
    public float keyRepeatDelay = 0.35f;
    public float keyRepeatInterval = 0.1f;
    float nextKeyRepeat;
    int heldDirection;
    [Header("Puzzle")]
    public string code = "ONE";
    public UnityEvent onCodeAccepted = new UnityEvent();
    [Header("Radio audio only (do not assign global music or SFX sources)")]
    public AudioSource radioAudio;
    public AudioSource staticAudio;
    public AudioClip transmission;
    [Range(0f, 1f)] public float volume = 0.5f;
    [Header("Optional dial needle")]
    public Transform needle;
    public Vector3 needleStart;
    public Vector3 needleEnd = new Vector3(0.1f, 0f, 0f);
    [Header("Readout")]
    public bool showReadout = true;

    public RadioStationCode State { get; private set; }
    public bool CanInteract => interaction != null && interaction.CanUseControls;
    public float NormalizedTuning => State != null ? State.Station / 12f : 0f;
    RadioInteractable interaction;
    GUIStyle label;
    string feedback = "";

    void Awake()
    {
        interaction = GetComponent<RadioInteractable>();
        try { State = new RadioStationCode(code); }
        catch (System.ArgumentException e) { Debug.LogError("Radio code: " + e.Message, this); enabled = false; }
    }

    void Start() { ApplyAudio(); UpdateNeedle(); }

    void Update()
    {
        if (!CanInteract || State == null) { heldDirection = 0; return; }
        var keys = Keyboard.current;
        if (keys == null) return;
        if (keys.fKey.wasPressedThisFrame) ToggleBand();
        if (keys.upArrowKey.wasPressedThisFrame) SelectControl((Control)(((int)SelectedControl + 2) % 3), true);
        if (keys.downArrowKey.wasPressedThisFrame) SelectControl((Control)(((int)SelectedControl + 1) % 3), true);
        int direction = keys.leftArrowKey.isPressed == keys.rightArrowKey.isPressed ? 0 : keys.rightArrowKey.isPressed ? 1 : -1;
        bool pressed = keys.leftArrowKey.wasPressedThisFrame || keys.rightArrowKey.wasPressedThisFrame;
        if (direction == 0) heldDirection = 0;
        else if (pressed || direction != heldDirection)
        {
            KeyboardSelectionVisible = true;
            AdjustSelected(direction);
            heldDirection = direction;
            nextKeyRepeat = Time.unscaledTime + Mathf.Max(0.1f, keyRepeatDelay);
        }
        else if (Time.unscaledTime >= nextKeyRepeat)
        {
            AdjustSelected(direction);
            nextKeyRepeat = Time.unscaledTime + Mathf.Max(0.03f, keyRepeatInterval);
        }
        if (keys.spaceKey.wasPressedThisFrame) ConfirmStation();
        if (keys.backspaceKey.wasPressedThisFrame) { State.Backspace(); feedback = ""; }
        if (keys.deleteKey.wasPressedThisFrame) { State.Clear(); feedback = ""; }
    }

    public void SelectControl(Control control, bool keyboard = false)
    {
        SelectedControl = control;
        KeyboardSelectionVisible = keyboard;
        heldDirection = 0;
    }
    void AdjustSelected(int direction)
    {
        if (SelectedControl == Control.Volume) SetVolume(volume + direction / (float)Mathf.Max(5, volumeSteps));
        else if (SelectedControl == Control.Tuning) SetTuning((State.Station + direction) / 12f);
        else if ((direction < 0 && State.Band != RadioBand.AM) || (direction > 0 && State.Band != RadioBand.FM)) ToggleBand();
    }

    public void SetTuning(float normalized)
    {
        if (!CanInteract || State == null) return;
        State.Tune(Mathf.RoundToInt(Mathf.Clamp01(normalized) * 12f));
        UpdateNeedle();
    }
    public void SetVolume(float normalized)
    {
        if (!CanInteract) return;
        int steps = Mathf.Max(5, volumeSteps);
        volume = Mathf.RoundToInt(Mathf.Clamp01(normalized) * steps) / (float)steps;
        ApplyAudio();
    }
    public void ToggleBand()
    {
        if (!CanInteract || State == null) return;
        State.ToggleBand();
        // Keep the knob and needle position when changing bands.
    }
    public void ConfirmStation()
    {
        if (!CanInteract || State == null || State.Solved) return;
        if (State.Confirm())
        {
            feedback = "Signal received";
            if (staticAudio != null) staticAudio.Stop();
            if (radioAudio != null && transmission != null)
            {
                radioAudio.clip = transmission;
                radioAudio.loop = false;
                radioAudio.Play();
            }
            onCodeAccepted.Invoke();
        }
        else feedback = State.Entry.Length == State.Code.Length ? "No response — try another sequence" : "Station recorded";
    }
    void ApplyAudio()
    {
        if (radioAudio != null) radioAudio.volume = volume;
        if (staticAudio != null) staticAudio.volume = volume;
    }
    void UpdateNeedle()
    {
        if (needle != null) needle.localPosition = Vector3.Lerp(needleStart, needleEnd, NormalizedTuning);
    }
    void OnGUI()
    {
        if (!showReadout || !CanInteract || State == null) return;
        if (label == null) label = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        float width = Mathf.Min(760f, Screen.width - 24f);
        Rect panel = new Rect((Screen.width - width) / 2f, Screen.height - 190f, width, 178f);
        GUI.Box(panel, "");
        // Do not display the letter mapping or answer to the player.
        GUI.Label(new Rect(panel.x, panel.y + 8f, width, 30f), $"{State.Band}   {State.Frequency} Hz    |    Volume {Mathf.RoundToInt(volume * 100)}%", label);
        GUI.Label(new Rect(panel.x + 6f, panel.y + 40f, width - 12f, 48f),
            "Drag knobs   |   Click switch or F: AM/FM   |   Space: confirm\nUp/Down: select control   |   Left/Right: adjust", label);
        GUI.Label(new Rect(panel.x, panel.y + 90f, width, 40f), $"Selected: {SelectedControl}   |   Backspace: undo   |   Delete: clear   |   Esc: leave", label);
        GUI.Label(new Rect(panel.x, panel.y + 132f, width, 36f), $"Stations: {State.Entry.Length}/{State.Code.Length}   {feedback}", label);
    }
}
