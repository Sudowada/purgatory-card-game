using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
[DefaultExecutionOrder(10000)]
public class ShopWindowInteractable : MonoBehaviour
{
    [Header("Zoom Settings")]
    public Transform      zoomTarget;
    public float          zoomDuration = 0.6f;
    public AnimationCurve zoomCurve    = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [Header("Interaction")]
    public float interactDistance = 3f;
    [Tooltip("Assign only the hatch collider, not a box covering the merchandise.")]
    public Collider interactionCollider;
    public Vector3 relicOffset = new Vector3(0f, 0.06f, 0f);
    public Vector3 paperOffset = new Vector3(0f, 0f, -0.08f);
    public Vector3 paperRotation = new Vector3(90f, 0f, 0f);
    public int RerollCost => ShopManager.Instance != null ? ShopManager.Instance.CurrentRerollCost : 2;
    public bool IsBusy => _isZooming || _isAnimating;
    public bool CanBrowse => GameManager.Instance != null &&
        !GameManager.Instance.RunFinished &&
        (GameManager.Instance.IsShopOpen || GameManager.Instance.CurrentRoundNumber > 1 ||
         (GameManager.Instance.RoundTarget > 0 && GameManager.Instance.RoundScore >= GameManager.Instance.RoundTarget));
    [Header("Click diagnostics")]
    public bool debugClicks = true;
    private void ClickTrace(string message)
    { if (debugClicks) Debug.Log("[ShopClick] " + message, this); }
    private bool previousControllerEnabled, previousCursorVisible;
    private Renderer[] hiddenPlayerRenderers;
    private bool[] previousPlayerRendererStates;

    // Keep the player body from clipping through the close shop camera. Store
    // each renderer's original state so already-hidden parts stay hidden later.
    private void HidePlayerModel()
    {
        GameStateManager.Instance?.SetPlayerModelVisible(false);
        if (playerController == null) return;

        hiddenPlayerRenderers = playerController.GetComponentsInChildren<Renderer>(true);
        previousPlayerRendererStates = new bool[hiddenPlayerRenderers.Length];
        for (int i = 0; i < hiddenPlayerRenderers.Length; i++)
        {
            Renderer renderer = hiddenPlayerRenderers[i];
            if (renderer == null) continue;
            previousPlayerRendererStates[i] = renderer.enabled;
            renderer.enabled = false;
        }
    }

    private void RestorePlayerModel()
    {
        if (hiddenPlayerRenderers != null)
        {
            for (int i = 0; i < hiddenPlayerRenderers.Length; i++)
            {
                Renderer renderer = hiddenPlayerRenderers[i];
                if (renderer != null && previousPlayerRendererStates != null &&
                    i < previousPlayerRendererStates.Length)
                    renderer.enabled = previousPlayerRendererStates[i];
            }
        }
        hiddenPlayerRenderers = null;
        previousPlayerRendererStates = null;

        // Preserve the visibility expected by the current game state. In card
        // mode the body stays hidden; in exploration it becomes visible again.
        if (GameStateManager.Instance != null)
            GameStateManager.Instance.SetPlayerModelVisible(
                GameStateManager.Instance.CurrentState == GameState.Exploration);
    }
    private CursorLockMode previousCursorLock;
    private bool cameraSaved;
    private Vector3 heldCameraPosition;
    private Quaternion heldCameraRotation;
    private Vector3 shopCameraPosition;
    private Quaternion shopCameraRotation;

    // Write after ordinary player/camera updates so they cannot pull the view
    // back to the player's head while browsing or returning from the shop.
    void LateUpdate()
    {
        if (cameraSaved && playerCamera != null)
            playerCamera.transform.SetPositionAndRotation(heldCameraPosition, heldCameraRotation);
    }

    private void SetShopCameraPose(Vector3 position, Quaternion rotation)
    {
        heldCameraPosition = position;
        heldCameraRotation = rotation;
        playerCamera.transform.SetPositionAndRotation(position, rotation);
    }
    [Header("References")]
    public FirstPersonController playerController;
    public Camera                playerCamera;
    [Header("Window Animation")]
    public Transform windowSlider;
    public Vector3   windowClosedRot     = new Vector3(0f, 0f,  0f);
    public Vector3   windowOpenRot       = new Vector3(0f, 0f, -77.5f);
    public float     windowOpenDuration  = 0.6f;
    [Header("Shelf Settings")]
    public Transform relicDisplayPoint;
    public float     relicSpacing = 0.12f;
    [Tooltip("Distance between the first and last relic centers, in shelf-local units. Set 0 to use Relic Spacing instead.")]
    [Min(0f)] public float shelfDisplayWidth = 0.8f;
    [Header("Paper Card Settings")]
    public GameObject paperCardPrefab;
    public float      paperYOffset       = -0.02f;
    public float      paperStaggerDelay  =  0.18f;
    public float      paperSlideDuration =  0.45f;
    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip   windowOpenClip;
    public AudioClip   windowCloseClip;
    public AudioClip   relicAppearClip;
    public AudioClip   paperSlideClip;
    public AudioClip   buySuccessClip;
    public AudioClip   buyFailClip;
    // ── Runtime state ─────────────────────────────────────────────────────────
    private bool _isZoomedIn  = false;
    private bool _isZooming   = false;
    private bool _isAnimating = false;
    private Vector3    _originalCamPos;
    private Quaternion _originalCamRot;
    private List<Relic>          _relics    = new List<Relic>();
    private List<int>            _costs     = new List<int>();
    private List<bool>           _purchased = new List<bool>();
    private List<GameObject>     _relicObjs = new List<GameObject>();
    private List<RelicPaperCard> _cards     = new List<RelicPaperCard>();
    // ── Runtime UI ────────────────────────────────────────────────────────────
    private GameObject      _hudCanvas;
    private TextMeshProUGUI _goldText;
    private TextMeshProUGUI _feedbackText;
    private static readonly Color boneWhite = new Color(0.90f, 0.85f, 0.72f);
    void Start()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerController == null && playerCamera != null)
            playerController = playerCamera.GetComponentInParent<FirstPersonController>();
        if (FindFirstObjectByType<ShopRelicInterface>() == null)
            gameObject.AddComponent<ShopRelicInterface>();
        ClickTrace($"Ready on '{name}'. Camera={(playerCamera != null ? playerCamera.name : "MISSING")}, collider={(interactionCollider != null ? interactionCollider.name : "root/children fallback")}, distance={interactDistance:0.##}m.");
    }
    private int stockRound = -1;
    private int purchaseFrame = -1;
    private bool previousInputBlocked;
    public bool IsOpen => _isZoomedIn;

    // Called as a new round begins so the next shop visit gets fresh inventory.
    public void InvalidateStockForNextVisit() => stockRound = -1;
    public bool CanInteract => _isZoomedIn && !_isZooming && !_isAnimating;
    public void OpenForRun()
    {
        if (_isZoomedIn || _isZooming || _isAnimating) { ClickTrace("Open rejected: already open or moving."); return; }
        if (!CanBrowse) { ClickTrace("Open rejected: first round has not been cleared, or run has ended."); return; }
        ClickTrace("Opening shop.");
        if (playerCamera == null || zoomTarget == null || relicDisplayPoint == null)
        { Debug.LogError("[Shop] Assign Player Camera, Zoom Target, and Relic Display Point."); return; }
        StartCoroutine(ZoomInAndOpen());
    }
    public void ContinueRun() => OnLeaveClicked();
    public bool IsSold(int index) => index < 0 || index >= _purchased.Count || _purchased[index];
    public void BuyDisplayedRelic(int index) { if (CanInteract) OnRelicBought(index); }
    void Update()
    {
        var kb = Keyboard.current;
        bool click = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool key = kb != null && kb.eKey.wasPressedThisFrame;
        if (_isZoomedIn)
        {
            if (!IsBusy && kb != null && kb.escapeKey.wasPressedThisFrame && !OwnedRelicHotbar.HandleEscape()) OnLeaveClicked();
            return;
        }
        if (!click && !key) return;
        ClickTrace("Interaction input received.");
        if (IsBusy) { ClickTrace("Blocked: shop is moving."); return; }
        var gm = GameManager.Instance;
        if (!CanBrowse)
        {
            ClickTrace(gm == null ? "Blocked: GameManager instance is missing." :
                $"Blocked: first round not cleared or run ended. Round={gm.CurrentRoundNumber}, score={gm.RoundScore}/{gm.RoundTarget}, shop={gm.IsShopOpen}, runFinished={gm.RunFinished}.");
            return;
        }
        if (playerCamera == null) { ClickTrace("Blocked: Player Camera is missing."); return; }
        bool exploring = GameStateManager.Instance != null &&
            GameStateManager.Instance.CurrentState == GameState.Exploration;
        if (gm != null && gm.IsInputBlocked && !exploring)
        { ClickTrace("Blocked: gameplay input is locked outside Exploration."); return; }
        var offering = FindFirstObjectByType<CardOfferingController>();
        if (offering != null && offering.OfferedCount > 0)
        { ClickTrace($"Blocked: {offering.OfferedCount} cards are still on the altar."); return; }
        Vector2 point = Cursor.lockState == CursorLockMode.Locked || key
            ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
            : Mouse.current.position.ReadValue();
        Ray ray = playerCamera.ScreenPointToRay(point);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            ClickTrace($"No collider hit within {interactDistance:0.##}m using camera '{playerCamera.name}'.");
            if (debugClicks && Physics.Raycast(ray, out RaycastHit farther, Mathf.Max(10f, interactDistance),
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                ClickTrace($"Farther hit: '{farther.collider.name}' at {farther.distance:0.##}m.");
            if (interactionCollider != null && interactionCollider.isTrigger)
                ClickTrace("Interaction Collider is a trigger; uncheck Is Trigger.");
            return;
        }
        bool hatch = interactionCollider != null ? hit.collider == interactionCollider
            : hit.transform == transform || hit.transform.IsChildOf(transform);
        if (!hatch)
        {
            ClickTrace($"Hit '{hit.collider.name}' at {hit.distance:0.##}m; expected '{(interactionCollider != null ? interactionCollider.name : name)}'.");
            return;
        }
        ClickTrace($"Hatch hit at {hit.distance:0.##}m.");
        OpenForRun();
    }
    IEnumerator ZoomInAndOpen()
    {
        _isZooming = true;
        _isAnimating = true;
        previousInputBlocked = GameManager.Instance != null && GameManager.Instance.IsInputBlocked;
        GameManager.Instance?.SetInputBlocked(true);
        previousControllerEnabled = playerController != null && playerController.enabled;
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        // Hide the player before moving the camera or revealing shop content.
        HidePlayerModel();
        if (playerController != null) playerController.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        _originalCamPos = playerCamera.transform.position;
        _originalCamRot = playerCamera.transform.rotation;
        shopCameraPosition = zoomTarget.position;
        shopCameraRotation = zoomTarget.rotation;
        cameraSaved = true;
        // Enter shop view immediately, then hold this world pose until leaving.
        SetShopCameraPose(shopCameraPosition, shopCameraRotation);
        _isZoomedIn = true;
        _isZooming  = false;
        int round = GameManager.Instance != null ? GameManager.Instance.CurrentRoundNumber : 0;
        ShopManager.Instance?.BeginShopVisit(round);
        if (stockRound != round) { GenerateShopRelics(); stockRound = round; }
        yield return StartCoroutine(OpenWindow());
        SpawnRelicsOnShelf();
        yield return StartCoroutine(SlideCardsOut());
        _hudCanvas?.SetActive(true);
        UpdateGoldDisplay();
        _isAnimating = false;
    }
    IEnumerator CloseAndZoomOut()
    {
        _isAnimating = true;
        ShopManager.Instance?.EndShopVisit();
        _hudCanvas?.SetActive(false);
        yield return StartCoroutine(SlideCardsIn());
        ClearDisplays();
        yield return StartCoroutine(CloseWindow());
        _isZooming = true;
        float elapsed  = 0f;
        Vector3    startPos = playerCamera.transform.position;
        Quaternion startRot = playerCamera.transform.rotation;
        while (elapsed < zoomDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t  = zoomCurve.Evaluate(Mathf.Clamp01(elapsed / zoomDuration));
            SetShopCameraPose(Vector3.Lerp(startPos, _originalCamPos, t),
                Quaternion.Slerp(startRot, _originalCamRot, t));
            yield return null;
        }
        SetShopCameraPose(_originalCamPos, _originalCamRot);
        GameManager.Instance?.SetInputBlocked(previousInputBlocked);
        if (playerController != null) playerController.enabled = previousControllerEnabled;
        RestorePlayerModel();
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
        cameraSaved = false;
        _isZoomedIn  = false;
        _isZooming   = false;
        _isAnimating = false;
        if (GameManager.Instance != null && GameManager.Instance.IsShopOpen) GameManager.Instance.LeaveTableShop();
    }
    IEnumerator OpenWindow()
    {
        PlaySound(windowOpenClip);
        if (windowSlider != null)
            yield return windowSlider.DOLocalRotate(windowOpenRot, windowOpenDuration)
                .SetEase(Ease.OutCubic).SetUpdate(true).WaitForCompletion();
        else
            yield return new WaitForSecondsRealtime(windowOpenDuration);
    }
    IEnumerator CloseWindow()
    {
        PlaySound(windowCloseClip);
        if (windowSlider != null)
            yield return windowSlider.DOLocalRotate(windowClosedRot, windowOpenDuration * 0.7f)
                .SetEase(Ease.InCubic).SetUpdate(true).WaitForCompletion();
        else
            yield return new WaitForSecondsRealtime(windowOpenDuration * 0.7f);
    }
    void SpawnRelicsOnShelf()
    {
        float spacing = shelfDisplayWidth > 0f && _relics.Count > 1
            ? shelfDisplayWidth / (_relics.Count - 1) : relicSpacing;
        for (int i = 0; i < _relics.Count; i++)
        {
            float offset = (i - (_relics.Count - 1) * 0.5f) * spacing;
            GameObject slot = ShopRelicInterface.CreateRelicSlot(_relics[i], relicDisplayPoint,
                new Vector3(offset, 0f, 0f) + relicOffset);
            var target = slot.GetComponent<ShopRelicTarget>();
            target.window = this; target.index = i; target.cost = _costs[i];
            var visual = slot.GetComponentInChildren<RelicObject3D>();
            if (visual != null) visual.SetSold(_purchased[i]);
            _relicObjs.Add(slot);
            // Always provide a price slip, even before a paper prefab is assigned.
            GameObject paper = paperCardPrefab != null
                ? Instantiate(paperCardPrefab, relicDisplayPoint)
                : new GameObject("RelicPriceSlip");
            paper.transform.SetParent(relicDisplayPoint, false);
            paper.transform.localScale = Vector3.one;
            paper.transform.localRotation = Quaternion.Euler(paperRotation);
            Vector3 visible = new Vector3(offset, paperYOffset, 0f) + paperOffset;
            Vector3 hidden = visible + Vector3.forward * 0.12f;
            RelicPaperCard card = paper.GetComponent<RelicPaperCard>();
            if (card == null) card = paper.AddComponent<RelicPaperCard>();
            card.Initialize(_relics[i], _costs[i], _purchased[i], i, hidden, visible, null);
            _cards.Add(card);
            PlaySound(relicAppearClip);
        }
    }
    void ClearDisplays()
    {
        foreach (var obj in _relicObjs) if (obj != null) { obj.SetActive(false); Destroy(obj); }
        foreach (var card in _cards) if (card != null) { card.gameObject.SetActive(false); Destroy(card.gameObject); }
        _relicObjs.Clear(); _cards.Clear();
    }
    public void RerollDisplayedRelics()
    {
        if (!CanInteract || OwnedRelicHotbar.Inspecting || purchaseFrame == Time.frameCount) return;
        purchaseFrame = Time.frameCount;
        var shop = ShopManager.Instance;
        if (shop == null) return;
        if (shop.playerGold < RerollCost)
        { ShopRelicInterface.Message = "Not enough gold to reroll."; PlaySound(buyFailClip); return; }
        var stock = shop.GenerateWorldShop();
        if (stock.relics.Count == 0)
        { ShopRelicInterface.Message = "No relics available."; return; }
        if (!shop.TryPayForReroll(out int paid, out string reason))
        { ShopRelicInterface.Message = reason; PlaySound(buyFailClip); return; }
        _relics.Clear(); _relics.AddRange(stock.relics);
        _costs.Clear(); _costs.AddRange(stock.costs);
        _purchased.Clear();
        foreach (var relic in _relics) _purchased.Add(false);
        ClearDisplays();
        SpawnRelicsOnShelf();
        StartCoroutine(FinishReroll());
        UpdateGoldDisplay();
        ShopRelicInterface.Message = reason;
    }
    IEnumerator FinishReroll()
    {
        _isAnimating = true;
        yield return SlideCardsOut();
        _isAnimating = false;
    }
    IEnumerator SlideCardsOut()
    {
        foreach (var card in _cards)
        {
            if (card == null) continue;
            PlaySound(paperSlideClip);
            StartCoroutine(card.SlideOut(paperSlideDuration));
            yield return new WaitForSecondsRealtime(paperStaggerDelay);
        }
        yield return new WaitForSecondsRealtime(paperSlideDuration);
    }
    IEnumerator SlideCardsIn()
    {
        foreach (var card in _cards)
            if (card != null) StartCoroutine(card.SlideIn());
        yield return new WaitForSecondsRealtime(0.35f);
    }
    void OnRelicBought(int index)
    {
        if (!CanInteract || OwnedRelicHotbar.Inspecting || purchaseFrame == Time.frameCount) return;
        if (index < 0 || index >= _relics.Count) return;
        purchaseFrame = Time.frameCount;
        if (_purchased[index]) return;
        int  cost = _costs[index];
        string reason = "Shop unavailable.";
        bool ok = ShopManager.Instance != null &&
            ShopManager.Instance.TryBuyRelic(_relics[index], cost, out reason);
        if (ok)
        {
            _purchased[index] = true;
            if (index < _relicObjs.Count && _relicObjs[index] != null)
            {
                var visual = _relicObjs[index].GetComponentInChildren<RelicObject3D>();
                if (visual != null) visual.SetSold(true);
            }
            if (index < _cards.Count && _cards[index] != null)
                _cards[index].MarkPurchased();
            foreach (var card in _cards)
                card?.RefreshGold();
            PlaySound(buySuccessClip);
            UpdateGoldDisplay();
            ShowFeedback("Relic acquired.");
            ShopRelicInterface.Message = "Relic acquired.";
        }
        else
        {
            PlaySound(buyFailClip);
            ShowFeedback(reason);
            ShopRelicInterface.Message = reason;
        }
    }
    public void OnLeaveClicked()
    {
        if (!CanInteract) return;
        StartCoroutine(CloseAndZoomOut());
    }
    void GenerateShopRelics()
    {
        _relics.Clear();
        _costs.Clear();
        _purchased.Clear();
        if (ShopManager.Instance != null)
        {
            var (relics, costs) = ShopManager.Instance.GenerateWorldShop();
            _relics.AddRange(relics);
            _costs.AddRange(costs);
        }
        else
        {
            var allTypes = (RelicType[])System.Enum.GetValues(typeof(RelicType));
            for (int i = 0; i < Mathf.Min(5, allTypes.Length); i++)
            {
                _relics.Add(RelicSystem.MakeRelicStatic(allTypes[i]));
                _costs.Add(25);
            }
        }
        for (int i = 0; i < _relics.Count; i++)
            _purchased.Add(RelicSystem.Instance != null
                        && RelicSystem.Instance.HasRelic(_relics[i].type));
    }
    void UpdateGoldDisplay()
    {
        int gold = ShopManager.Instance?.playerGold ?? 0;
        if (_goldText != null) _goldText.text = $"✦ {gold}g";
    }
    void ShowFeedback(string msg)
    {
        if (_feedbackText == null) return;
        _feedbackText.text = msg;
        _feedbackText.DOFade(1f, 0.1f);
        DOVirtual.DelayedCall(2.5f, () =>
        {
            if (_feedbackText != null) _feedbackText.DOFade(0f, 0.4f);
        });
    }
    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
    void OnDisable()
    {
        if (_isZoomedIn || cameraSaved) ShopManager.Instance?.EndShopVisit();
        StopAllCoroutines();
        if (windowSlider != null) windowSlider.DOKill();
        ClearDisplays();
        if (cameraSaved)
        {
            if (playerCamera != null)
            { playerCamera.transform.position = _originalCamPos; playerCamera.transform.rotation = _originalCamRot; }
            if (playerController != null) playerController.enabled = previousControllerEnabled;
            Cursor.lockState = previousCursorLock; Cursor.visible = previousCursorVisible;
            GameManager.Instance?.SetInputBlocked(previousInputBlocked);
        }
        cameraSaved = false;
        _isZoomedIn = _isZooming = _isAnimating = false;
    }

}
