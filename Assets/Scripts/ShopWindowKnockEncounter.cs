using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Attach to the shop root alongside ShopWindowInteractable.
public class ShopWindowKnockEncounter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private FirstPersonController playerController;
    [SerializeField] private Transform door;
    [SerializeField] private Collider knockCollider;
    [SerializeField] private CameraShakeImpulse cameraShake;
    [SerializeField] private GameObject yuriEyes;
    [Header("Interaction")]
    [SerializeField, Min(1)] private int knocksRequired = 3;
    [SerializeField, Min(0.1f)] private float interactDistance = 3f;
    [SerializeField, Min(0.01f)] private float knockCooldown = 0.2f;
    [SerializeField] private bool onlyBeforeFirstHand = true;
    [Header("Door: offset from its initial local rotation")]
    [SerializeField] private Vector3 openRotationOffset = new Vector3(0f, 100f, 0f);
    [SerializeField] private Vector3 knockRotationOffset = new Vector3(0f, 0.6f, 0f);
    [SerializeField, Min(0.01f)] private float openDuration = 0.45f;
    [SerializeField, Min(0.01f)] private float slamDuration = 0.14f;
    [Header("Dialogue")]
    [SerializeField] private string firstLine = "WHAT DO YOU WANT?!?";
    [SerializeField] private string secondLine = "...";
    [SerializeField, Min(0.1f)] private float firstLineDuration = 2f;
    [SerializeField, Min(0.1f)] private float secondLineDuration = 1.2f;
    [Header("Offer button impact defaults")]
    [SerializeField] private float slamShakeStrength = 0.018f;
    [SerializeField] private float slamShakeDuration = 0.085f;
    [Header("Optional audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip knockClip;
    [SerializeField] private AudioClip slamClip;

    [Header("Click diagnostics")]
    [SerializeField] private bool debugClicks = true;
    private Quaternion closedRotation;
    private int knockCount;
    private float nextKnockTime;
    private bool busy, completed, controlsCaptured, controllerWasEnabled;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLock;
    private Coroutine knockRoutine;
    private GameObject dialogueRoot;
    private TextMeshProUGUI dialogueText;

    private void Start()
    {
        if (playerCamera == null && playerController != null)
            playerCamera = playerController.GetComponentInChildren<Camera>();
        if (playerCamera == null) playerCamera = Camera.main;
        if (cameraShake == null && playerCamera != null)
            cameraShake = playerCamera.GetComponentInParent<CameraShakeImpulse>();
        if (door == null || knockCollider == null || playerCamera == null)
        {
            Debug.LogError("[Yuri] Assign Door, Knock Collider and Player Camera.", this);
            enabled = false;
            return;
        }
        closedRotation = door.localRotation;
        if (yuriEyes != null) yuriEyes.SetActive(false);
        BuildDialogue();
        Trace($"Ready on '{name}'. Camera='{playerCamera.name}', door='{door.name}', collider='{knockCollider.name}', distance={interactDistance:0.##}. Click the GAME view while aiming at the hatch.");
    }

    private void Trace(string message)
    {
        if (debugClicks) Debug.Log("[YuriClick] " + message, this);
    }
    private void Update()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
        Trace("Left click received.");
        if (busy) { Trace("Blocked: encounter is playing."); return; }
        if (completed) { Trace("Blocked: encounter already completed. Start a fresh Play session to repeat."); return; }
        if (Time.unscaledTime < nextKnockTime) { Trace("Blocked: knock cooldown."); return; }
        if (playerCamera == null || door == null || knockCollider == null)
        { Trace("Blocked: missing Camera, Door or Knock Collider reference."); return; }
        if (!knockCollider.enabled || !knockCollider.gameObject.activeInHierarchy)
        { Trace("Blocked: Knock Collider is disabled or its object is inactive."); return; }
        if (GameStateManager.Instance != null &&
            GameStateManager.Instance.CurrentState != GameState.Exploration)
        { Trace("Blocked: player must be in Exploration, not CardGame."); return; }
        var gm = GameManager.Instance;
        if (gm != null && (gm.IsShopOpen || gm.RunFinished))
        { Trace("Blocked: shop is open or run has ended."); return; }
        if (onlyBeforeFirstHand && gm != null &&
            (gm.CurrentRoundNumber > 1 || gm.HandsRemaining < gm.MaximumHands || gm.RoundScore != 0))
        {
            Trace($"Blocked: first-hand restriction. Round={gm.CurrentRoundNumber}, hands={gm.HandsRemaining}/{gm.MaximumHands}, score={gm.RoundScore}.");
            return;
        }
        Ray ray = Cursor.lockState == CursorLockMode.Locked
            ? playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
            : playerCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            Trace($"No collider hit within {interactDistance:0.##}m using camera '{playerCamera.name}'.");
            if (debugClicks && Physics.Raycast(ray, out RaycastHit farther, Mathf.Max(10f, interactDistance),
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                Trace($"Farther hit: '{farther.collider.name}' at {farther.distance:0.##}m.");
            if (knockCollider.isTrigger) Trace("Assigned Knock Collider is a trigger; this ray ignores triggers. Uncheck Is Trigger.");
            if ((Physics.DefaultRaycastLayers & (1 << knockCollider.gameObject.layer)) == 0)
                Trace("Assigned Knock Collider is on an excluded raycast layer. Use Default.");
            Debug.DrawRay(ray.origin, ray.direction * interactDistance, Color.red, 2f);
            return;
        }
        if (hit.collider != knockCollider)
        {
            Trace($"Blocked by '{hit.collider.name}' at {hit.distance:0.##}m. Expected '{knockCollider.name}' (collider instance {knockCollider.GetInstanceID()}); hit instance {hit.collider.GetInstanceID()}.");
            if (debugClicks && knockCollider.Raycast(ray, out RaycastHit targetHit, interactDistance))
                Trace($"Assigned door is behind the first hit at {targetHit.distance:0.##}m.");
            return;
        }
        Trace($"Door hit at {hit.distance:0.##}m.");
        AcceptKnock();
    }
    private void AcceptKnock()
    {
        nextKnockTime = Time.unscaledTime + knockCooldown;
        if (audioSource != null && knockClip != null) audioSource.PlayOneShot(knockClip);
        knockCount++;
        Trace($"Knock accepted {knockCount}/{knocksRequired}.");
        if (knockRoutine != null) StopCoroutine(knockRoutine);
        door.localRotation = closedRotation;
        if (knockCount >= knocksRequired) StartCoroutine(Encounter());
        else knockRoutine = StartCoroutine(Knock());
    }
    [ContextMenu("Test Knock (Play Mode)")]
    private void TestKnock()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || busy || completed || door == null || dialogueRoot == null) return;
        Trace("Manual knock test: bypassing raycast and state restrictions.");
        AcceptKnock();
    }

    private IEnumerator Knock()
    {
        Quaternion bumped = closedRotation * Quaternion.Euler(knockRotationOffset);
        yield return RotateDoor(closedRotation, bumped, 0.045f, false);
        yield return RotateDoor(bumped, closedRotation, 0.09f, false);
        knockRoutine = null;
    }

    private IEnumerator Encounter()
    {
        busy = true;
        controlsCaptured = true;
        controllerWasEnabled = playerController != null && playerController.enabled;
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        if (playerController != null) playerController.enabled = false;
        Quaternion opened = closedRotation * Quaternion.Euler(openRotationOffset);
        yield return RotateDoor(closedRotation, opened, openDuration, false);
        if (yuriEyes != null) yuriEyes.SetActive(true);
        dialogueRoot.SetActive(true);
        dialogueText.text = firstLine;
        yield return new WaitForSecondsRealtime(firstLineDuration);
        dialogueText.text = secondLine;
        yield return new WaitForSecondsRealtime(secondLineDuration);
        dialogueRoot.SetActive(false);
        yield return RotateDoor(opened, closedRotation, slamDuration, true);
        if (yuriEyes != null) yuriEyes.SetActive(false);
        if (audioSource != null && slamClip != null) audioSource.PlayOneShot(slamClip);
        if (cameraShake != null) cameraShake.Kick(slamShakeStrength, slamShakeDuration);
        else Debug.LogWarning("[Yuri] Assign the Offer button's Camera Shake component for slam feedback.", this);
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, slamShakeDuration));
        RestoreControls();
        completed = true;
        busy = false;
    }

    private IEnumerator RotateDoor(Quaternion from, Quaternion to, float duration, bool accelerate)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            t = accelerate ? t * t * t : 1f - Mathf.Pow(1f - t, 3f);
            door.localRotation = Quaternion.Slerp(from, to, t);
            yield return null;
        }
        door.localRotation = to;
    }

    private void BuildDialogue()
    {
        dialogueRoot = new GameObject("YuriDialogue", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = dialogueRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = dialogueRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var textObject = new GameObject("DialogueText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(dialogueRoot.transform, false);
        dialogueText = textObject.GetComponent<TextMeshProUGUI>();
        dialogueText.fontSize = 36;
        dialogueText.color = new Color(0.9f, 0.85f, 0.72f);
        dialogueText.alignment = TextAlignmentOptions.Center;
        dialogueText.raycastTarget = false;
        var rect = dialogueText.rectTransform;
        rect.anchorMin = new Vector2(0.15f, 0);
        rect.anchorMax = new Vector2(0.85f, 0);
        rect.pivot = new Vector2(0.5f, 0);
        rect.anchoredPosition = new Vector2(0, 65);
        rect.sizeDelta = new Vector2(0, 110);
        dialogueRoot.SetActive(false);
    }

    private void RestoreControls()
    {
        if (!controlsCaptured) return;
        if (playerController != null) playerController.enabled = controllerWasEnabled;
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
        controlsCaptured = false;
    }
    private void OnDisable()
    {
        StopAllCoroutines();
        knockRoutine = null;
        if (door != null && (busy || knockCount > 0)) door.localRotation = closedRotation;
        if (dialogueRoot != null) dialogueRoot.SetActive(false);
        if (yuriEyes != null) yuriEyes.SetActive(false);
        RestoreControls();
        busy = false;
        knockCount = 0;
    }
    private void OnDestroy()
    {
        if (dialogueRoot != null) Destroy(dialogueRoot);
    }
}
