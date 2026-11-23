using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class LightConditionClue : MonoBehaviour
{
    public string puzzleId = "cell_wall_keyhole";
    public Camera playerCamera;
    public FirstPersonController playerController;
    [Min(0.1f)] public float interactDistance = 3f;
    public Light[] requiredLights = System.Array.Empty<Light>();
    public Light[] forbiddenLights = System.Array.Empty<Light>();
    public TMP_Text writing;
    public Collider writingTarget;
    public Collider keyholeTarget;
    public Light writingGlow;
    public UnityEvent onKeyholeClicked = new UnityEvent();

    [ColorUsage(false, true)] public Color idleGlow = new Color(0.85f, 0.55f, 1f);
    [ColorUsage(false, true)] public Color flashGlow = Color.white;
    [Min(0.1f)] public float revealDuration = 0.65f;
    [Min(1f)] public float flashMultiplier = 3f;

    private bool discovered;
    private bool revealing;
    private bool ready;
    private float elapsed;
    private Vector3 keyholeScale;
    private float baseGlowIntensity;
    private string SaveKey => "light_clue_" + puzzleId + "_discovered";

    private void OnEnable()
    {
        if (string.IsNullOrWhiteSpace(puzzleId) || playerCamera == null || writing == null ||
            writingTarget == null || keyholeTarget == null ||
            writingTarget.isTrigger || keyholeTarget.isTrigger ||
            transform.IsChildOf(writingTarget.transform) || transform.IsChildOf(keyholeTarget.transform) ||
            writingTarget.transform.IsChildOf(keyholeTarget.transform) ||
            keyholeTarget.transform.IsChildOf(writingTarget.transform) ||
            !writing.transform.IsChildOf(writingTarget.transform))
        {
            Debug.LogError("Assign a puzzle ID, camera, writing and separate writing and keyhole targets.", this);
            enabled = false;
            return;
        }

        keyholeScale = keyholeTarget.transform.localScale;
        baseGlowIntensity = writingGlow != null ? writingGlow.intensity : 0f;
        discovered = PlayerPrefs.GetInt(SaveKey) == 1;
        ready = true;
        ApplyVisibility();
    }

    private void OnDisable()
    {
        if (!ready) return;

        revealing = false;
        if (writingGlow != null) writingGlow.intensity = baseGlowIntensity;
        writingTarget.gameObject.SetActive(false);
        keyholeTarget.transform.localScale = keyholeScale;
        keyholeTarget.enabled = discovered;
        keyholeTarget.gameObject.SetActive(discovered);
        ready = false;
    }

    private void LateUpdate()
    {
        if (revealing)
        {
            AdvanceReveal();
            return;
        }

        ApplyVisibility();
        var target = discovered ? keyholeTarget : writingTarget;
        if (!target.gameObject.activeInHierarchy || !CanInteract() ||
            Mouse.current?.leftButton.wasPressedThisFrame != true)
            return;

        var ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
        if (!Physics.Raycast(ray, out var hit, interactDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || hit.collider != target)
            return;

        if (discovered)
        {
            onKeyholeClicked.Invoke();
            return;
        }

        discovered = true;
        revealing = true;
        elapsed = 0f;
        writingTarget.enabled = false;
        writing.color = flashGlow;
        if (writingGlow != null) writingGlow.intensity = baseGlowIntensity * flashMultiplier;
        PlayerPrefs.SetInt(SaveKey, 1);
        PlayerPrefs.Save();
    }

    private void AdvanceReveal()
    {
        elapsed += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, revealDuration));
        writing.color = Color.Lerp(flashGlow, idleGlow, progress);
        if (writingGlow != null)
            writingGlow.intensity = baseGlowIntensity * Mathf.Lerp(flashMultiplier, 1f, progress);

        if (progress >= 0.35f)
        {
            keyholeTarget.transform.localScale = keyholeScale * Mathf.Lerp(0.15f, 1f,
                Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, progress)));
            keyholeTarget.gameObject.SetActive(true);
        }

        if (progress < 1f) return;
        revealing = false;
        keyholeTarget.transform.localScale = keyholeScale;
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        bool visible = !discovered && MatchesLights();
        if (visible && writing.color != idleGlow) writing.color = idleGlow;
        writingTarget.enabled = visible;
        writingTarget.gameObject.SetActive(visible);
        keyholeTarget.enabled = discovered;
        keyholeTarget.gameObject.SetActive(discovered);
    }

    private bool MatchesLights()
    {
        foreach (var light in requiredLights)
            if (light == null || !IsLit(light)) return false;

        foreach (var light in forbiddenLights)
            if (light == null || IsLit(light)) return false;

        return true;
    }

    private static bool IsLit(Light light) => light.isActiveAndEnabled && light.intensity > 0f;

    private bool CanInteract() =>
        (playerController == null || (playerController.isActiveAndEnabled && !playerController.MovementLocked)) &&
        (GameStateManager.Instance == null || GameStateManager.Instance.CurrentState == GameState.Exploration) &&
        (SettingsUI.Instance == null || !SettingsUI.Instance.IsOpen) &&
        (GameManager.Instance == null || !GameManager.Instance.IsShopOpen) &&
        RadioInteractable.Active == null;

    [ContextMenu("Clear Saved Clue Discovery")]
    private void ClearSavedDiscovery()
    {
        if (Application.isPlaying || string.IsNullOrWhiteSpace(puzzleId)) return;

        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
    }
}
