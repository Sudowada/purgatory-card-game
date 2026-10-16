using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Supplies panel references and the existing E interaction. Never owns the camera
/// or cursor; switches are used directly from the exploration view.
/// </summary>
public class SwitchPanelInteract : MonoBehaviour
{
    [Header("Interaction")]
    public float interactDistance = 5f;

    [Header("References")]
    public FirstPersonController playerController;
    public Camera playerCamera;

    [Header("Switches")]
    public PuzzleSwitch[] switches = new PuzzleSwitch[4];

    void Awake()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerController == null && playerCamera != null)
            playerController = playerCamera.GetComponentInParent<FirstPersonController>();
        foreach (var sw in switches)
            if (sw != null) sw.ConfigurePanel(this);
    }

    void LateUpdate()
    {
        // Keep E prompts useful without entering a panel mode.
        var kb = Keyboard.current;
        if (kb == null || !kb.eKey.wasPressedThisFrame) return;
        foreach (var sw in switches)
            if (sw != null && sw.TryClickFromPanel()) break;
    }

    // Retained for existing UnityEvents/callers. Do not change another
    // interaction's controller or cursor state.
    public void ForceExit()
    {
        foreach (var sw in switches)
            if (sw != null) sw.CancelInteraction();
    }

    void OnDisable() => ForceExit();
}
