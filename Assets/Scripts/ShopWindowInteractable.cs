using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class ShopWindowInteractable : MonoBehaviour
{
    [Header("Zoom Settings")]
    public Transform      zoomTarget;
    public float          zoomDuration = 0.6f;
    public AnimationCurve zoomCurve    = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Interaction")]
    public float interactDistance = 3f;

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
        BuildHUD();
    }

    void BuildHUD()
    {
        _hudCanvas = new GameObject("ShopHUD");
        _hudCanvas.transform.SetParent(
            relicDisplayPoint != null ? relicDisplayPoint : transform, false);
        _hudCanvas.transform.localPosition = new Vector3(0f, 0.22f, 0.06f);
        _hudCanvas.transform.localRotation = Quaternion.identity;
        _hudCanvas.transform.localScale    = Vector3.one * 0.001f;

        var canvas = _hudCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        _hudCanvas.AddComponent<CanvasScaler>();
        _hudCanvas.AddComponent<GraphicRaycaster>();

        var rt       = _hudCanvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(400f, 80f);

        var goldGO  = new GameObject("GoldDisplay");
        goldGO.transform.SetParent(_hudCanvas.transform, false);
        _goldText   = goldGO.AddComponent<TextMeshProUGUI>();
        _goldText.fontSize      = 18f;
        _goldText.color         = new Color(0.85f, 0.70f, 0.15f);
        _goldText.fontStyle     = FontStyles.Bold;
        _goldText.alignment     = TextAlignmentOptions.Left;
        _goldText.raycastTarget = false;
        var goldRT  = goldGO.GetComponent<RectTransform>();
        goldRT.anchorMin = new Vector2(0f,   0.5f);
        goldRT.anchorMax = new Vector2(0.5f, 1f);
        goldRT.offsetMin = new Vector2(10f,  0f);
        goldRT.offsetMax = Vector2.zero;

        var fbGO    = new GameObject("Feedback");
        fbGO.transform.SetParent(_hudCanvas.transform, false);
        _feedbackText           = fbGO.AddComponent<TextMeshProUGUI>();
        _feedbackText.fontSize  = 14f;
        _feedbackText.color     = new Color(0.9f, 0.85f, 0.7f, 0f);
        _feedbackText.alignment = TextAlignmentOptions.Center;
        _feedbackText.raycastTarget = false;
        var fbRT    = fbGO.GetComponent<RectTransform>();
        fbRT.anchorMin = new Vector2(0.25f, 0f);
        fbRT.anchorMax = new Vector2(0.75f, 0.5f);
        fbRT.offsetMin = Vector2.zero;
        fbRT.offsetMax = Vector2.zero;

        var leaveGO  = new GameObject("LeaveButton");
        leaveGO.transform.SetParent(_hudCanvas.transform, false);
        var leaveImg = leaveGO.AddComponent<Image>();
        leaveImg.color = new Color(0.45f, 0.15f, 0.08f);
        var leaveBtn = leaveGO.AddComponent<Button>();
        leaveBtn.targetGraphic = leaveImg;
        leaveBtn.onClick.AddListener(OnLeaveClicked);
        var leaveRT  = leaveGO.GetComponent<RectTransform>();
        leaveRT.anchorMin = new Vector2(0.72f, 0.2f);
        leaveRT.anchorMax = new Vector2(0.98f, 0.8f);
        leaveRT.offsetMin = Vector2.zero;
        leaveRT.offsetMax = Vector2.zero;

        var lblGO  = new GameObject("LeaveLabel");
        lblGO.transform.SetParent(leaveGO.transform, false);
        var lblTMP = lblGO.AddComponent<TextMeshProUGUI>();
        lblTMP.text          = "Leave";
        lblTMP.fontSize      = 14f;
        lblTMP.color         = boneWhite;
        lblTMP.alignment     = TextAlignmentOptions.Center;
        lblTMP.raycastTarget = false;
        var lblRT  = lblGO.GetComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero;
        lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = Vector2.zero;
        lblRT.offsetMax = Vector2.zero;

        _hudCanvas.SetActive(false);
    }

    void Update()
    {
        if (_isZooming || _isAnimating) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        if (_isZoomedIn && kb.escapeKey.wasPressedThisFrame)
        {
            StartCoroutine(CloseAndZoomOut());
            return;
        }

        if (!_isZoomedIn && kb.eKey.wasPressedThisFrame)
        {
            Ray ray = playerCamera.ScreenPointToRay(
                new Vector2(Screen.width / 2f, Screen.height / 2f));
            if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance)) return;
            if (hit.transform != transform && !hit.transform.IsChildOf(transform)) return;
            StartCoroutine(ZoomInAndOpen());
        }
    }

    IEnumerator ZoomInAndOpen()
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
            float t  = zoomCurve.Evaluate(Mathf.Clamp01(elapsed / zoomDuration));
            playerCamera.transform.position = Vector3.Lerp(_originalCamPos, zoomTarget.position, t);
            playerCamera.transform.rotation = Quaternion.Slerp(_originalCamRot, zoomTarget.rotation, t);
            yield return null;
        }
        playerCamera.transform.position = zoomTarget.position;
        playerCamera.transform.rotation = zoomTarget.rotation;
        _isZoomedIn = true;
        _isZooming  = false;

        GenerateShopRelics();
        yield return StartCoroutine(OpenWindow());

        SpawnRelicsOnShelf();
        yield return StartCoroutine(SlideCardsOut());

        _hudCanvas?.SetActive(true);
        UpdateGoldDisplay();
    }

    IEnumerator CloseAndZoomOut()
    {
        _isAnimating = true;
        _hudCanvas?.SetActive(false);

        yield return StartCoroutine(SlideCardsIn());

        foreach (var obj in _relicObjs)
            if (obj != null) Destroy(obj);
        foreach (var card in _cards)
            if (card != null) Destroy(card.gameObject);
        _relicObjs.Clear();
        _cards.Clear();

        yield return StartCoroutine(CloseWindow());

        _isZooming = true;
        float elapsed  = 0f;
        Vector3    startPos = playerCamera.transform.position;
        Quaternion startRot = playerCamera.transform.rotation;

        while (elapsed < zoomDuration)
        {
            elapsed += Time.deltaTime;
            float t  = zoomCurve.Evaluate(Mathf.Clamp01(elapsed / zoomDuration));
            playerCamera.transform.position = Vector3.Lerp(startPos, _originalCamPos, t);
            playerCamera.transform.rotation = Quaternion.Slerp(startRot, _originalCamRot, t);
            yield return null;
        }

        playerCamera.transform.position = _originalCamPos;
        playerCamera.transform.rotation = _originalCamRot;
        playerController.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        _isZoomedIn  = false;
        _isZooming   = false;
        _isAnimating = false;
    }

    IEnumerator OpenWindow()
    {
        PlaySound(windowOpenClip);
        if (windowSlider != null)
            yield return windowSlider.DOLocalRotate(windowOpenRot, windowOpenDuration)
                .SetEase(Ease.OutCubic).WaitForCompletion();
        else
            yield return new WaitForSeconds(windowOpenDuration);
    }

    IEnumerator CloseWindow()
    {
        PlaySound(windowCloseClip);
        if (windowSlider != null)
            yield return windowSlider.DOLocalRotate(windowClosedRot, windowOpenDuration * 0.7f)
                .SetEase(Ease.InCubic).WaitForCompletion();
        else
            yield return new WaitForSeconds(windowOpenDuration * 0.7f);
    }

    void SpawnRelicsOnShelf()
    {
        // ShelfTop world position — everything spawns relative to this
        Vector3 spawnOrigin = relicDisplayPoint.position;
        Debug.Log($"[Shop] SpawnOrigin world pos: {spawnOrigin}");

        // Cards lay flat on shelf — Euler(90,0,0) makes canvas face upward
        Quaternion cardFacing = Quaternion.Euler(90f, 90f, 0f);

        // Window slot — cards slide FROM here
        Vector3 windowWorldPos = windowSlider != null
            ? windowSlider.position
            : transform.position;

        // Space along world X (along shelf length)
        Vector3 spacingAxis = Vector3.right;

        for (int i = 0; i < _relics.Count; i++)
        {
            float offset = (i - (_relics.Count - 1) * 0.5f) * relicSpacing;

            // ── Relic 3D object ───────────────────────────────────────────
            // Spawn AT spawnOrigin — ShelfTop is already above shelf surface
            Vector3 relicWorldPos = spawnOrigin + spacingAxis * offset + Vector3.up * 0.06f;

            GameObject relicObj = RelicPrefabFactory.Create(_relics[i], relicDisplayPoint);
            relicObj.transform.position = relicWorldPos;
            relicObj.transform.rotation = Quaternion.identity;

            if (relicObj.GetComponent<Collider>() == null)
            {
                var col = relicObj.AddComponent<BoxCollider>();
                col.size = Vector3.one * 1.5f;
            }

            if (_purchased[i])
            {
                var r = relicObj.GetComponentInChildren<Renderer>();
                if (r != null) r.material.color = Color.gray;
            }

            Vector3 targetScale = relicObj.transform.localScale;
            relicObj.transform.localScale = Vector3.zero;
            relicObj.transform.DOScale(targetScale, 0.3f).SetEase(Ease.OutBack);
            _relicObjs.Add(relicObj);

            // ── Paper card ────────────────────────────────────────────────
            if (paperCardPrefab != null)
            {
                // Final position: at spawnOrigin height + tiny offset
                Vector3 visibleWorldPos = spawnOrigin
                    + spacingAxis * offset
                    + Vector3.up * paperYOffset;

                // Start position: coming from window slot
                Vector3 hiddenWorldPos = windowWorldPos + spacingAxis * offset;

                GameObject cardObj = Instantiate(paperCardPrefab);
                cardObj.transform.SetParent(relicDisplayPoint, true);
                cardObj.transform.position = hiddenWorldPos;
                cardObj.transform.rotation = cardFacing;

                // Convert to local for Initialize
                Vector3 hiddenLocal  = relicDisplayPoint.InverseTransformPoint(hiddenWorldPos);
                Vector3 visibleLocal = relicDisplayPoint.InverseTransformPoint(visibleWorldPos);

                var card = cardObj.GetComponent<RelicPaperCard>();
                if (card != null)
                    card.Initialize(
                        _relics[i], _costs[i], _purchased[i], i,
                        hiddenLocal, visibleLocal,
                        OnRelicBought);

                _cards.Add(card);
            }

            PlaySound(relicAppearClip);
        }
    }

    IEnumerator SlideCardsOut()
    {
        foreach (var card in _cards)
        {
            if (card == null) continue;
            PlaySound(paperSlideClip);
            StartCoroutine(card.SlideOut(paperSlideDuration));
            yield return new WaitForSeconds(paperStaggerDelay);
        }
        yield return new WaitForSeconds(paperSlideDuration);
    }

    IEnumerator SlideCardsIn()
    {
        foreach (var card in _cards)
            if (card != null) StartCoroutine(card.SlideIn());
        yield return new WaitForSeconds(0.35f);
    }

    void OnRelicBought(int index)
    {
        if (index < 0 || index >= _relics.Count) return;
        if (_purchased[index]) return;

        int  cost = _costs[index];
        bool ok   = ShopManager.Instance != null && ShopManager.Instance.playerGold >= cost;

        if (ok)
        {
            ShopManager.Instance.RemoveGold(cost);
            RelicSystem.Instance?.AddRelic(_relics[index].type);
            _purchased[index] = true;

            if (index < _relicObjs.Count && _relicObjs[index] != null)
            {
                var r = _relicObjs[index].GetComponentInChildren<Renderer>();
                if (r != null)
                    DOTween.To(() => r.material.color, c => r.material.color = c, Color.gray, 0.4f);
                Vector3 ts = _relicObjs[index].transform.localScale;
                _relicObjs[index].transform.DOScale(ts * 0.8f, 0.3f);
            }

            if (index < _cards.Count && _cards[index] != null)
                _cards[index].MarkPurchased();

            foreach (var card in _cards)
                card?.RefreshGold();

            PlaySound(buySuccessClip);
            UpdateGoldDisplay();
            ShowFeedback("Relic acquired.");
        }
        else
        {
            PlaySound(buyFailClip);
            ShowFeedback("Not enough gold.");
        }
    }

    void OnLeaveClicked()
    {
        if (_isAnimating) return;
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
            for (int i = 0; i < Mathf.Min(3, allTypes.Length); i++)
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
}