using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Hand")]
    [SerializeField] private HorizontalCardHolder handHolder;

    [Header("Enemy Scaling")]
    [SerializeField] private int enemyBaseMin       = 1;
    [SerializeField] private int enemyBaseMax       = 4;
    [SerializeField] private int enemyScalePerRound = 1;

    [Header("Threshold Scaling")]
    [SerializeField] private int baseThreshold     = 250;
    [SerializeField] private int thresholdPerRound = 50;

    // ── UI References ─────────────────────────────────────────────────────────
    private TextMeshProUGUI scoreText;
    private TextMeshProUGUI roundText;
    private TextMeshProUGUI enemyCardText;
    private TextMeshProUGUI comboResultText;
    private TextMeshProUGUI handsText;
    private Image           scoreFill;
    private Image           scorePreviewFill;
    private TextMeshProUGUI goldText;
    private Button          playButton;

    private GameObject      shopPanel;
    private TextMeshProUGUI shopRelicName;
    private TextMeshProUGUI shopRelicDesc;
    private TextMeshProUGUI shopRelicCost;
    private TextMeshProUGUI shopCardName;
    private TextMeshProUGUI shopCardRarity;
    private TextMeshProUGUI shopCardCost;
    private TextMeshProUGUI shopConsumableName;
    private TextMeshProUGUI shopConsumableDesc;
    private TextMeshProUGUI shopConsumableCost;
    private TextMeshProUGUI shopBurnCost;
    private Button          shopBuyRelicBtn;
    private Button          shopBuyCardBtn;
    private Button          shopBuyConsumableBtn;
    private Button          shopBurnBtn;
    private Button          shopLeaveBtn;

    private GameObject      gameOverPanel;
    private TextMeshProUGUI gameOverScoreText;

    // ── Runtime refs ──────────────────────────────────────────────────────────
    private AltarScene   altarScene;
    private CardAnimator cardAnimator;
    private DeckVisual   deckVisual;
    private List<(int value, string suit)> discardPile = new List<(int, string)>();

    // ── Game State ────────────────────────────────────────────────────────────
    private List<Card> selectedCards = new List<Card>();
    private int  roundScore          = 0;
    private int  totalScore          = 0;
    private int  roundNumber         = 0;
    private int  stageNumber         = 1;
    private int  currentEnemyCard    = 0;
    private int  currentThreshold    = 0;
    private bool isAnimating         = false;
    private bool inShop              = false;
    private int  handsLeft           = 5;
    private int  maxHands            = 5;
    private bool _waitingToReturn    = false;
    private bool _thresholdReached   = false;

    private List<(int value, string suit)> currentDeck = new List<(int, string)>();
    private int deckIndex = 0;

    private bool _inputBlocked        = false;
    private int  _queuedNextCardValue = -1;
    private int  _queuedDecayPenalty  = 0;

    // ── Breakdown row cache ───────────────────────────────────────────────────
    private TextMeshProUGUI[] _breakSrc = new TextMeshProUGUI[3];
    private TextMeshProUGUI[] _breakVal = new TextMeshProUGUI[3];
    private bool _breakdownCached = false;

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (GetComponent<RelicSystem>()       == null) gameObject.AddComponent<RelicSystem>();
        if (GetComponent<ShopManager>()       == null) gameObject.AddComponent<ShopManager>();
        if (GetComponent<ConsumableSystem>()  == null) gameObject.AddComponent<ConsumableSystem>();
        if (GetComponent<RunStats>()          == null) gameObject.AddComponent<RunStats>();
        if (GetComponent<GameOverSequence>()  == null) gameObject.AddComponent<GameOverSequence>();
        if (GetComponent<CardAnimator>()      == null) gameObject.AddComponent<CardAnimator>();
        if (GetComponent<AudioManager>()      == null) gameObject.AddComponent<AudioManager>();
        if (GetComponent<EventManager>()      == null) gameObject.AddComponent<EventManager>();
        if (GetComponent<DocumentSystem>()    == null) gameObject.AddComponent<DocumentSystem>();
        if (GetComponent<CollectiblesTable>() == null) gameObject.AddComponent<CollectiblesTable>();
        if (GetComponent<SettingsUI>()        == null) gameObject.AddComponent<SettingsUI>();
        if (GetComponent<RoundSelectScreen>() == null) gameObject.AddComponent<RoundSelectScreen>();
    }

    void Start()
    {
        StartCoroutine(WaitForHandThenInit());
    }

    private IEnumerator WaitForHandThenInit()
    {
        yield return new WaitForSeconds(0.3f);
        yield return null;

        if (handHolder == null)
        {
            Debug.LogError("[GameManager] HAND HOLDER IS NULL.");
            yield break;
        }
        altarScene   = FindFirstObjectByType<AltarScene>();
        cardAnimator = GetComponent<CardAnimator>();
        deckVisual   = GetComponent<DeckVisual>();
        HideOriginalDeckUI();
        RegisterCardEvents();
        StartRound();
    }

    // ── UI Wiring ─────────────────────────────────────────────────────────────

    public void SetUIReferences(
        TextMeshProUGUI score, TextMeshProUGUI round, TextMeshProUGUI enemy,
        TextMeshProUGUI combo, TextMeshProUGUI hands, TextMeshProUGUI gold,
        Button play,
        GameObject shop,
        TextMeshProUGUI relicName,  TextMeshProUGUI relicDesc,  TextMeshProUGUI relicCost,
        TextMeshProUGUI cardName,   TextMeshProUGUI cardRarity, TextMeshProUGUI cardCost,
        TextMeshProUGUI consumName, TextMeshProUGUI consumDesc, TextMeshProUGUI consumCost,
        TextMeshProUGUI burnCost,
        Button buyRelic, Button buyCard, Button buyConsumable, Button burn, Button leave,
        GameObject gameOver, TextMeshProUGUI gameOverScore,
        TextMeshProUGUI enemyCardVal = null)
    {
        scoreText       = score;
        roundText       = round;
        enemyCardText   = enemy;
        comboResultText = combo;
        handsText       = hands;
        goldText        = gold;

        var barGO = GameObject.Find("ScoreBar");
        if (barGO != null) scoreFill = barGO.GetComponent<Image>();
        var previewGO = GameObject.Find("ScoreBarPreview");
        if (previewGO != null) scorePreviewFill = previewGO.GetComponent<Image>();

        playButton           = play;
        shopPanel            = shop;
        shopRelicName        = relicName;
        shopRelicDesc        = relicDesc;
        shopRelicCost        = relicCost;
        shopCardName         = cardName;
        shopCardRarity       = cardRarity;
        shopCardCost         = cardCost;
        shopConsumableName   = consumName;
        shopConsumableDesc   = consumDesc;
        shopConsumableCost   = consumCost;
        shopBurnCost         = burnCost;
        shopBuyRelicBtn      = buyRelic;
        shopBuyCardBtn       = buyCard;
        shopBuyConsumableBtn = buyConsumable;
        shopBurnBtn          = burn;
        shopLeaveBtn         = leave;
        gameOverPanel        = gameOver;
        gameOverScoreText    = gameOverScore;
        if (enemyCardVal != null) enemyCardText = enemyCardVal;

        if (shopBuyRelicBtn)      shopBuyRelicBtn.onClick.AddListener(OnBuyRelic);
        if (shopBuyCardBtn)       shopBuyCardBtn.onClick.AddListener(OnBuyCard);
        if (shopBuyConsumableBtn) shopBuyConsumableBtn.onClick.AddListener(OnBuyConsumable);
        if (shopBurnBtn)          shopBurnBtn.onClick.AddListener(OnBurnCard);
        if (shopLeaveBtn)         shopLeaveBtn.onClick.AddListener(OnLeaveShop);
    }

    // ── Card Events ───────────────────────────────────────────────────────────

    private void HideOriginalDeckUI()
    {
        string[] toHide = { "Deck", "JokerCardGroup", "ConsumableGroup" };
        foreach (string n in toHide)
        {
            var go = GameObject.Find(n);
            if (go != null && go.GetComponent<RectTransform>() != null)
                go.SetActive(false);
        }
    }

    private void RegisterCardEvents()
    {
        foreach (Card card in handHolder.cards)
        {
            card.CardSelectedEvent.RemoveListener(OnCardSelectionChanged);
            card.CardSelectedEvent.AddListener(OnCardSelectionChanged);
        }
    }

    private void OnCardSelectionChanged(Card card, bool isSelected)
    {
        if (inShop) return;

        int maxPlay = RelicSystem.Instance != null ? RelicSystem.Instance.GetMaxPlay() : 3;
        if (isSelected)
        {
            if (selectedCards.Count >= maxPlay)
            {
                card.Deselect();
                ShowMessage($"Max {maxPlay} cards!");
                return;
            }
            if (!selectedCards.Contains(card))
                selectedCards.Add(card);
        }
        else
        {
            selectedCards.Remove(card);
        }

        UpdateHUD();
        RefreshScorePreview();
    }

    // ── Score Preview ─────────────────────────────────────────────────────────

    private void CacheBreakdownRefs()
    {
        if (_breakdownCached) return;
        for (int i = 0; i < 3; i++)
        {
            var srcGO = GameObject.Find($"BreakSrc{i}");
            var valGO = GameObject.Find($"BreakVal{i}");
            _breakSrc[i] = srcGO?.GetComponent<TextMeshProUGUI>();
            _breakVal[i] = valGO?.GetComponent<TextMeshProUGUI>();
        }
        _breakdownCached = true;
    }

    private void SetBreakdownRow(int row, string src, string val)
    {
        if (row < 0 || row >= 3) return;
        if (_breakSrc[row] != null) _breakSrc[row].text = src;
        if (_breakVal[row] != null) _breakVal[row].text = val;
    }

    private void ClearBreakdown()
    {
        for (int i = 0; i < 3; i++)
            SetBreakdownRow(i, "", "");
    }

    private void RefreshScorePreview()
    {
        CacheBreakdownRefs();

        var comboNameTMP = FindTMP("ComboText");
        var chipsTMP     = FindTMP("PreviewBase");
        var multTMP      = FindTMP("PreviewMult");
        var finalTMP     = FindTMP("PreviewFinal");
        var tierTMP      = FindTMP("PreviewTier");
        var nextHintTMP  = FindTMP("PreviewNextHint");

        if (selectedCards.Count == 0)
        {
            if (comboNameTMP != null) comboNameTMP.text = "Select cards";
            if (chipsTMP     != null) chipsTMP.text     = "—";
            if (multTMP      != null) multTMP.text      = "—";
            if (finalTMP     != null) { finalTMP.text   = "—"; finalTMP.color = new Color(0.25f,0.92f,0.42f,1f); }
            ClearBreakdown();
            TierBarController.Instance?.SetTier(0, 0f, "");
            return;
        }

        int minimumBase = RelicSystem.Instance != null ? RelicSystem.Instance.GetMinimumBase() : 0;
        var result = ComboEvaluator.Evaluate(selectedCards, currentEnemyCard, minimumBase, stageNumber);

        if (comboNameTMP != null) comboNameTMP.text = result.comboName ?? "High Card";

        if (chipsTMP != null)
            StartCoroutine(FlickerNumber(chipsTMP, result.points, 0.18f));

        if (multTMP != null)
        {
            float displayMult = result.additiveMult * result.tierMult;
            StartCoroutine(FlickerText(multTMP, $"{displayMult:0.##}x", 0.12f));
        }

        if (finalTMP != null)
        {
            Color targetCol = result.isPenalty
                ? new Color(0.92f, 0.25f, 0.25f, 1f)
                : result.containsDecayedCard
                    ? new Color(0.90f, 0.45f, 0.20f, 1f)
                    : new Color(0.25f, 0.92f, 0.42f, 1f);
            finalTMP.color = targetCol;
            StartCoroutine(FlickerNumber(finalTMP, result.finalScore, 0.22f));
        }

        // ── Tier bar ─────────────────────────────────────────────────────────
        int tierIndex = result.isPenalty ? 0 : Mathf.Clamp(result.diffDots, 0, 8);

        float fillFraction = 0f;
        if (!result.isPenalty)
        {
            if (result.nextTierAt == -1)
                fillFraction = 1f; // max tier
            else if (result.nextTierAt > 0)
                fillFraction = Mathf.Clamp01(1f - (float)result.nextTierAt / 6f);
        }

        string nextHint = result.isPenalty
            ? $"+{Mathf.Abs(result.difference)} to escape"
            : result.nextTierAt > 0
                ? $"+{result.nextTierAt} → ×{result.nextTierMult:0.##}"
                : "MAX TIER";

        TierBarController.Instance?.SetTier(tierIndex, fillFraction, nextHint);

        // Keep tierTMP in sync for fallback
        if (tierTMP != null)
            tierTMP.text = result.isPenalty ? "PENALTY" : result.diffDotStr;

        if (nextHintTMP != null)
            nextHintTMP.text = nextHint;

        // ── Breakdown rows ────────────────────────────────────────────────────
        // Row 0: Base points
        SetBreakdownRow(0,
            $"Base points ({result.comboName})",
            $"{result.points} pts");

        // Row 1: Additive mult source
        if (result.additiveMult > 1f)
        {
            float comboBonus = result.additiveMult - 1f;
            SetBreakdownRow(1,
                $"{result.comboName} combo",
                $"+{comboBonus:0.##} add mult");
        }
        else
        {
            SetBreakdownRow(1, "No combo bonus", "×1.0");
        }

        // Row 2: Tier mult source
        if (result.isPenalty)
        {
            SetBreakdownRow(2,
                $"PENALTY (diff {result.difference})",
                $"−{result.penaltyAmount} pts");
        }
        else
        {
            SetBreakdownRow(2,
                $"Tier {tierIndex} (diff +{result.difference})",
                $"×{result.tierMult:0.##} tier");
        }
    }

    static (int nextAt, float nextMult) NextTier(int diff)
    {
        if (diff < 0)   return (-diff,     1.00f);
        if (diff <= 2)  return (3  - diff, 1.25f);
        if (diff <= 5)  return (6  - diff, 1.50f);
        if (diff <= 9)  return (10 - diff, 2.00f);
        if (diff <= 14) return (15 - diff, 2.50f);
        if (diff <= 19) return (20 - diff, 3.00f);
        if (diff <= 24) return (25 - diff, 3.75f);
        if (diff <= 30) return (31 - diff, 5.00f);
        return (-1, 5.00f);
    }

    private IEnumerator AnimateScorePreview(ComboResult result, int scoreBeforeHand, int scoreGained)
    {
        var chipsTMP = FindTMP("PreviewBase");
        var multTMP  = FindTMP("PreviewMult");
        var finalTMP = FindTMP("PreviewFinal");
        var comboTMP = FindTMP("ComboText");

        if (comboTMP != null)
            comboTMP.text = result.comboName ?? "High Card";

        if (chipsTMP != null)
        {
            float t = 0f; float dur = 0.35f;
            while (t < dur)
            {
                t += Time.deltaTime;
                chipsTMP.text = Mathf.RoundToInt(Mathf.Lerp(0, result.points, t / dur)).ToString();
                yield return null;
            }
            chipsTMP.text = result.points.ToString();
        }

        yield return new WaitForSeconds(0.08f);

        if (multTMP != null)
        {
            float displayMult = result.additiveMult * result.tierMult;
            multTMP.text = $"{displayMult:0.##}x";
            var multGO = GameObject.Find("MultPill");
            if (multGO != null)
                multGO.transform.DOPunchScale(Vector3.one * 0.12f, 0.25f, 5, 0.5f);
        }

        yield return new WaitForSeconds(0.12f);

        if (finalTMP != null)
        {
            float t = 0f; float dur = 0.4f;
            Color targetCol = result.isPenalty
                ? new Color(0.92f, 0.25f, 0.25f, 1f)
                : result.containsDecayedCard
                    ? new Color(0.90f, 0.45f, 0.20f, 1f)
                    : new Color(0.25f, 0.92f, 0.42f, 1f);
            finalTMP.color = targetCol;
            while (t < dur)
            {
                t += Time.deltaTime;
                finalTMP.text = Mathf.RoundToInt(Mathf.Lerp(0, result.finalScore, t / dur)).ToString();
                yield return null;
            }
            finalTMP.text = result.finalScore.ToString();
            var scoreGO = GameObject.Find("ScorePill");
            if (scoreGO != null)
                scoreGO.transform.DOPunchScale(Vector3.one * 0.15f, 0.3f, 6, 0.5f);
        }

        yield return new WaitForSeconds(0.1f);
        yield return StartCoroutine(FlyScoreToHUD(scoreGained));

        roundScore += scoreGained;
        totalScore += scoreGained;

        if (scoreText != null)
            StartCoroutine(CountUpScore(scoreText, scoreBeforeHand, roundScore, 0.5f));
        else
            UpdateHUD();

        if (!_thresholdReached && roundScore >= currentThreshold)
        {
            _thresholdReached = true;
            SetInputBlocked(true);
            OnThresholdReached();
        }
    }

    private IEnumerator FlickerNumber(TextMeshProUGUI tmp, int target, float duration)
    {
        if (tmp == null) yield break;
        float elapsed = 0f;
        int steps = Mathf.Max(4, Mathf.RoundToInt(duration / 0.04f));
        for (int i = 0; i < steps - 1; i++)
        {
            tmp.text = Random.Range(0, Mathf.Max(target + 1, 10)).ToString();
            elapsed += duration / steps;
            yield return new WaitForSeconds(duration / steps);
        }
        tmp.text = target.ToString();
        tmp.transform.parent?.DOPunchScale(Vector3.one * 0.08f, 0.2f, 4, 0.5f);
    }

    private IEnumerator FlickerText(TextMeshProUGUI tmp, string target, float duration)
    {
        if (tmp == null) yield break;
        string[] noise = { "x?", "x!", "x..", "x~" };
        int steps = Mathf.Max(3, Mathf.RoundToInt(duration / 0.04f));
        for (int i = 0; i < steps - 1; i++)
        {
            tmp.text = noise[Random.Range(0, noise.Length)];
            yield return new WaitForSeconds(duration / steps);
        }
        tmp.text = target;
        tmp.transform.parent?.DOPunchScale(Vector3.one * 0.08f, 0.2f, 4, 0.5f);
    }

    private IEnumerator FlyScoreToHUD(int score)
    {
        if (scoreText == null) yield break;

        Canvas fxCanvas = null;
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c.gameObject.name == "CardFXCanvas") { fxCanvas = c; break; }
        if (fxCanvas == null) yield break;

        var flyGO = new GameObject("FlyScore");
        flyGO.transform.SetParent(fxCanvas.transform, false);
        var flyRT = flyGO.AddComponent<RectTransform>();
        flyRT.anchorMin = flyRT.anchorMax = new Vector2(0.5f, 0.5f);
        flyRT.pivot     = new Vector2(0.5f, 0.5f);

        var previewGO = GameObject.Find("PreviewFinal");
        Vector2 startPos = Vector2.zero;
        if (previewGO != null)
        {
            Vector3 wp = previewGO.transform.position;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                fxCanvas.GetComponent<RectTransform>(),
                RectTransformUtility.WorldToScreenPoint(null, wp),
                null, out startPos);
        }

        Vector2 endPos = Vector2.zero;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            fxCanvas.GetComponent<RectTransform>(),
            RectTransformUtility.WorldToScreenPoint(null, scoreText.transform.position),
            null, out endPos);

        flyRT.anchoredPosition = startPos;
        flyRT.sizeDelta        = new Vector2(200f, 50f);

        var flyTMP = flyGO.AddComponent<TMPro.TextMeshProUGUI>();
        flyTMP.text      = score >= 0 ? $"+{score}" : $"{score}";
        flyTMP.color     = score >= 0
            ? new Color(0.25f, 0.92f, 0.42f, 1f)
            : new Color(0.92f, 0.25f, 0.25f, 1f);
        flyTMP.fontSize  = 28f;
        flyTMP.fontStyle = TMPro.FontStyles.Bold;
        flyTMP.alignment = TMPro.TextAlignmentOptions.Center;

        float    dur = 0.55f;
        Sequence seq = DOTween.Sequence();
        seq.Append(flyRT.DOAnchorPos(endPos, dur).SetEase(Ease.InCubic));
        seq.Join(flyGO.transform.DOScale(0.4f, dur).SetEase(Ease.InCubic));
        seq.Join(flyTMP.DOFade(0f, dur * 0.4f).SetDelay(dur * 0.6f));
        yield return seq.WaitForCompletion();

        Destroy(flyGO);
    }

    private IEnumerator CountUpScore(TextMeshProUGUI tmp, int from, int to, float duration)
    {
        if (tmp == null) { UpdateHUD(); yield break; }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed  += Time.deltaTime;
            float t   = Mathf.Clamp01(elapsed / duration);
            float ease = 1f - Mathf.Pow(1f - t, 3f);
            tmp.text  = Mathf.RoundToInt(Mathf.Lerp(from, to, ease)).ToString();
            yield return null;
        }
        tmp.text = to.ToString();
        tmp.transform.DOPunchScale(Vector3.one * 0.2f, 0.35f, 6, 0.4f);
        tmp.DOColor(new Color(0.25f, 0.92f, 0.42f, 1f), 0.1f)
           .OnComplete(() => tmp.DOColor(Color.white, 0.3f));
        UpdateHUD();
    }

    TextMeshProUGUI FindTMP(string goName)
    {
        var go = GameObject.Find(goName);
        return go?.GetComponent<TextMeshProUGUI>();
    }

    // ── Round Flow ────────────────────────────────────────────────────────────

    private void StartRound()
    {
        isAnimating       = false;
        _thresholdReached = false;
        _inputBlocked     = false;

        if (_queuedDecayPenalty > 0)
        {
            for (int i = 0; i < _queuedDecayPenalty; i++)
                InfectRandomCard();
            _queuedDecayPenalty = 0;
        }

        roundNumber++;
        stageNumber = Mathf.Clamp(((roundNumber - 1) / 3) + 1, 1, 8);
        handsLeft   = maxHands;

        if (roundNumber == 1)
            ShopManager.Instance?.ApplyRunStartBonuses();

        roundScore       = 0;
        currentThreshold = baseThreshold + (roundNumber - 1) * thresholdPerRound;

        int minVal       = enemyBaseMin + (roundNumber - 1) * enemyScalePerRound;
        int maxVal       = enemyBaseMax + (roundNumber - 1) * enemyScalePerRound;
        currentEnemyCard = Random.Range(minVal, maxVal + 1);

        currentDeck = ShopManager.Instance != null
            ? ShopManager.Instance.GetShuffledDeck()
            : BuildDefaultDeck();
        deckIndex = 0;

        var dealtCards = handHolder.cards.OrderBy(c => c.transform.GetSiblingIndex()).ToList();
        foreach (Card card in dealtCards)
            AssignNextCard(card);

        deckVisual?.UpdateCounts(currentDeck.Count - deckIndex, 0);
        RegisterCardEvents();
        UpdateHUD();
        if (cardAnimator != null) StartCoroutine(cardAnimator.DrawCards(dealtCards));
        ShowMessage($"Round {roundNumber} (Stage {stageNumber}) | Enemy: {currentEnemyCard} | Target: {currentThreshold}");
    }

    private List<(int, string)> BuildDefaultDeck()
    {
        string[] suits = { "\u2660", "\u2665", "\u2666", "\u2663" };
        var deck = new List<(int, string)>();
        foreach (string s in suits)
            for (int v = 1; v <= 13; v++)
                deck.Add((v, s));
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }
        return deck;
    }

    private void AssignNextCard(Card card)
    {
        if (deckIndex >= currentDeck.Count) deckIndex = 0;
        card.cardValue  = currentDeck[deckIndex].value;
        card.cardSuit   = currentDeck[deckIndex].suit;
        card.decayLevel = 0;
        card.isDecayed  = false;
        card.InitCardStats();
        deckIndex++;
    }

    // ── Play ──────────────────────────────────────────────────────────────────

    public void OnPlayPressed()
    {
        if (isAnimating || inShop || _inputBlocked) return;

        if (selectedCards.Count == 0)
        {
            ShowMessage("Select cards to play!");
            return;
        }

        int maxPlay = RelicSystem.Instance != null ? RelicSystem.Instance.GetMaxPlay() : 3;
        foreach (Card fc in handHolder.cards.Where(c => c.isDecayed && !selectedCards.Contains(c)))
        {
            if (selectedCards.Count >= maxPlay) break;
            selectedCards.Add(fc);
        }

        StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        isAnimating = true;

        List<Card> toPlay = new List<Card>(selectedCards);
        selectedCards.Clear();
        handsLeft = Mathf.Max(0, handsLeft - 1);

        int minimumBase = RelicSystem.Instance != null ? RelicSystem.Instance.GetMinimumBase() : 0;

        int aceSwapBonus = ConsumableSystem.Instance != null
            ? ConsumableSystem.Instance.ConsumeAceSwap(toPlay) : 0;

        Card swappedCard   = null;
        int  originalValue = 0;
        if (aceSwapBonus > 0)
        {
            swappedCard           = toPlay.OrderBy(c => c.cardValue).First();
            originalValue         = swappedCard.cardValue;
            swappedCard.cardValue = 11;
        }

        ComboResult result = ComboEvaluator.Evaluate(toPlay, currentEnemyCard, minimumBase, stageNumber);

        if (swappedCard != null)
            swappedCard.cardValue = originalValue;

        if (RelicSystem.Instance != null)
            result.finalScore = RelicSystem.Instance.ApplyRelicModifiers(result);

        int scoreBoost = 0;
        if (!result.isPenalty)
        {
            scoreBoost = ConsumableSystem.Instance != null
                ? ConsumableSystem.Instance.ConsumeScoreBoost() : 0;
            result.finalScore += scoreBoost;
        }

        int scoreBefore = roundScore;
        int scoreGained = result.finalScore;

        StartCoroutine(AnimateScorePreview(result, scoreBefore, scoreGained));
        ShowMessage(result.description + (scoreBoost > 0 ? $" +{scoreBoost} boost!" : ""));

        if (!_inputBlocked)
        {
            var eventIDs = CardPatternSystem.CheckPatterns(
                toPlay, handHolder.cards.ToList(), roundNumber);
            if (eventIDs.Count > 0)
                EventManager.Instance?.TriggerEvents(eventIDs, toPlay);
        }

        foreach (Card card in toPlay)
            card.Deselect();

        if (cardAnimator != null)
            yield return StartCoroutine(cardAnimator.PlayCards(toPlay, result));
        else
            yield return new WaitForSeconds(0.3f);

        bool frozen = ConsumableSystem.Instance != null
            && ConsumableSystem.Instance.decayFrozenThisRound;
        if (!frozen)
            ApplyDecayToHand(toPlay);
        else
            ConsumableSystem.Instance.ConsumeDecayFreeze();

        yield return new WaitForSeconds(0.1f);

        var freeSlots = toPlay.ToList();
        freeSlots.Sort((a, b) =>
            b.transform.GetSiblingIndex().CompareTo(a.transform.GetSiblingIndex()));

        foreach (Card slot in freeSlots)
            AssignNextCard(slot);

        if (cardAnimator != null)
            yield return StartCoroutine(cardAnimator.DrawCards(freeSlots));
        else
            yield return new WaitForSeconds(0.2f);

        UpdateHUD();
        deckVisual?.UpdateCounts(currentDeck.Count - deckIndex, 0);
        isAnimating = false;
    }

    // ── Threshold / Round Complete ────────────────────────────────────────────

    private void OnThresholdReached()
    {
        Debug.Log("[GameManager] OnThresholdReached fired!");
        ShopManager.Instance?.AwardGold(roundScore, currentThreshold);

        var rss = RoundSelectScreen.Instance;
        if (rss != null)
        {
            rss.Show(roundNumber, () =>
            {
                if (GameStateManager.Instance != null)
                {
                    _waitingToReturn = true;
                    GameStateManager.Instance.GoToExploration();
                    ShowReturnHint();
                }
                else
                {
                    ContinueAfterRound();
                }
            });
        }
        else
        {
            ContinueAfterRound();
        }
    }

    private void ShowReturnHint()
    {
        ShowMessage("Explore freely. Find the deck of cards to return to the table.");
    }

    public void OnDeckInteracted()
    {
        if (!_waitingToReturn) return;
        _waitingToReturn = false;
        ContinueAfterRound();
    }

    private void ContinueAfterRound()
    {
        SetInputBlocked(false);
        if (roundNumber % 2 == 0)
            OpenShop();
        else
            StartRound();
    }

    private void TriggerGameOver()
    {
        if (gameOverPanel != null)     gameOverPanel.SetActive(true);
        if (gameOverScoreText != null) gameOverScoreText.text = $"Final Score: {totalScore}";
        Debug.Log($"GAME OVER — Final Score: {totalScore}");
    }

    // ── Shop ──────────────────────────────────────────────────────────────────

    public void OpenShop()
    {
        inShop = true;
        ShopManager.Instance?.GenerateShop(roundNumber);
        if (shopPanel != null)
        {
            shopPanel.SetActive(true);
            RefreshShopUI();
        }
    }

    private void RefreshShopUI()
    {
        var shop = ShopManager.Instance;
        if (shop == null) return;

        var goldDisp = GameObject.Find("GoldDisp");
        if (goldDisp != null)
        {
            var t = goldDisp.GetComponent<TMPro.TextMeshProUGUI>();
            if (t != null) t.text = $"✦ {shop.playerGold}g";
        }

        if (shop.offeredRelic != null)
        {
            if (shopRelicName) shopRelicName.text = shop.offeredRelic.displayName;
            string req  = ShopManager.GetUnlockRequirement(shop.offeredRelic.type);
            string desc = shop.offeredRelic.description;
            if (shopRelicDesc) shopRelicDesc.text = req != null
                ? $"{desc}\n<color=#E8A030>[Unlocked: {req}]</color>"
                : desc;
            if (shopRelicCost) shopRelicCost.text = $"{shop.offeredRelicCost}g";
        }
        else if (shopRelicName) shopRelicName.text = "Sold Out";

        if (shop.offeredCard != null)
        {
            if (shopCardName)   shopCardName.text   = shop.offeredCard.displayName;
            if (shopCardRarity) shopCardRarity.text = shop.offeredCard.rarity.ToString();
            if (shopCardCost)   shopCardCost.text   = $"{shop.offeredCard.cost}g";
        }
        else if (shopCardName) shopCardName.text = "Sold Out";

        if (shop.offeredConsumable != null)
        {
            if (shopConsumableName) shopConsumableName.text = shop.offeredConsumable.displayName;
            if (shopConsumableDesc) shopConsumableDesc.text = shop.offeredConsumable.description;
            if (shopConsumableCost) shopConsumableCost.text = $"{shop.offeredConsumable.cost}g";
        }
        else if (shopConsumableName) shopConsumableName.text = "Sold Out";

        if (shopBurnCost) shopBurnCost.text = $"Cost: {shop.burnCost}g";
        UpdateHUD();
    }

    private void OnBuyRelic()
    {
        if (ShopManager.Instance != null && ShopManager.Instance.BuyRelic())
        { ShowMessage("Relic acquired!"); RefreshShopUI(); }
        else ShowMessage("Not enough gold!");
    }

    private void OnBuyCard()
    {
        if (ShopManager.Instance != null && ShopManager.Instance.BuyCard())
        { ShowMessage("Card added to deck!"); RefreshShopUI(); }
        else ShowMessage("Not enough gold!");
    }

    private void OnBuyConsumable()
    {
        if (ShopManager.Instance != null && ShopManager.Instance.BuyConsumable())
        { ShowMessage("Consumable used!"); RefreshShopUI(); }
        else ShowMessage("Not enough gold!");
    }

    private void OnBurnCard()
    {
        Card target = handHolder.cards.FirstOrDefault(c => c.selected);
        if (target == null) { ShowMessage("Select a card in hand to burn!"); return; }

        if (ShopManager.Instance != null && ShopManager.Instance.BurnCard(target))
        {
            target.Deselect();
            AssignNextCard(target);
            ShowMessage($"Burned {target.GetDisplayValue()}{target.cardSuit}!");
            RefreshShopUI();
        }
        else ShowMessage("Not enough gold!");
    }

    private void OnLeaveShop()
    {
        inShop = false;
        if (shopPanel != null) shopPanel.SetActive(false);
        StartRound();
    }

    // ── Decay ─────────────────────────────────────────────────────────────────

    private void ApplyDecayToHand(List<Card> justPlayed)
    {
        int  decayCount = RelicSystem.Instance != null ? RelicSystem.Instance.GetDecayCount()     : 3;
        bool rotOnStack = RelicSystem.Instance != null && RelicSystem.Instance.GetDecayRotOnStack();
        bool decayAll   = RelicSystem.Instance != null && RelicSystem.Instance.GetDecayAll();

        List<Card> hand = handHolder.cards.Where(c => !justPlayed.Contains(c)).ToList();

        if (decayAll)
        {
            foreach (var card in hand)
            {
                bool justFullyDecayed = card.ApplyDecay();
                if (justFullyDecayed)
                    ShowMessage($"{card.GetDisplayValue()}{card.cardSuit} fully decayed — must play it!");
            }
        }
        else
        {
            List<Card> alreadyDecayed = hand.Where(c =>  c.isDecayed).ToList();
            List<Card> healthy        = hand.Where(c => !c.isDecayed).ToList();

            for (int i = healthy.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (healthy[i], healthy[j]) = (healthy[j], healthy[i]);
            }

            List<Card> targets = new List<Card>(alreadyDecayed);
            targets.AddRange(healthy);

            int actual = Mathf.Min(decayCount, targets.Count);
            for (int i = 0; i < actual; i++)
            {
                bool justFullyDecayed = rotOnStack
                    ? targets[i].ForceFullDecay()
                    : targets[i].ApplyDecay();

                if (justFullyDecayed)
                    ShowMessage($"{targets[i].GetDisplayValue()}{targets[i].cardSuit} fully decayed — must play it!");
            }
        }

        if (handHolder.cards.All(c => c.isDecayed) && roundScore < currentThreshold)
            TriggerGameOver();
    }

    // ── HUD ───────────────────────────────────────────────────────────────────

    private void UpdateHUD()
    {
        int gold = ShopManager.Instance != null ? ShopManager.Instance.playerGold : 0;

        if (scoreText)     scoreText.text     = roundScore.ToString();
        if (roundText)     roundText.text     = roundNumber.ToString();
        if (enemyCardText) enemyCardText.text = currentEnemyCard > 0 ? currentEnemyCard.ToString() : "—";
        if (goldText)      goldText.text      = $"{gold}g";
        if (handsText)     handsText.text     = $"{handsLeft}/{maxHands}";

        if (scoreFill != null && currentThreshold > 0)
        {
            float progress = Mathf.Clamp01((float)roundScore / currentThreshold);
            var rt = scoreFill.GetComponent<RectTransform>();
            if (rt != null)
            {
                float current = rt.anchorMax.x;
                DOTween.To(() => current, x => {
                    current = x;
                    rt.anchorMax = new Vector2(x, rt.anchorMax.y);
                }, progress, 0.6f).SetEase(Ease.OutCubic);
            }
        }

        var neededLbl = GameObject.Find("ScoreBarLabel");
        if (neededLbl != null)
        {
            var t = neededLbl.GetComponent<TMPro.TextMeshProUGUI>();
            if (t != null) t.text = $"{Mathf.Max(0, currentThreshold - roundScore)} needed";
        }
        var targetVal = GameObject.Find("TargetVal");
        if (targetVal != null)
        {
            var t = targetVal.GetComponent<TMPro.TextMeshProUGUI>();
            if (t != null) t.text = $"/ {currentThreshold}";
        }
        var scoreNeeded = GameObject.Find("ScoreNeeded");
        if (scoreNeeded != null)
        {
            var t = scoreNeeded.GetComponent<TMPro.TextMeshProUGUI>();
            if (t != null) t.text = $"Score {currentThreshold}";
        }

        bool hasForcedPlay = handHolder != null && handHolder.cards.Any(c => c.isDecayed);
        if (playButton != null)
        {
            var colors = playButton.colors;
            colors.normalColor = hasForcedPlay
                ? new Color(0.9f, 0.3f, 0.3f)
                : new Color(0.15f, 0.38f, 0.09f);
            playButton.colors = colors;
        }
    }

    public void ShowMessage(string msg)
    {
        if (comboResultText == null) return;
        comboResultText.text = msg;
        comboResultText.DOFade(1f, 0.1f);
        DOVirtual.DelayedCall(3f, () =>
        {
            if (comboResultText != null)
                comboResultText.DOFade(0f, 0.5f);
        });
    }

    // ── Event System Helpers ──────────────────────────────────────────────────

    public void SetInputBlocked(bool blocked) => _inputBlocked = blocked;
    public bool IsInputBlocked => _inputBlocked;
    public int  CurrentRoundNumber => roundNumber;

    public void ModifyGold(int delta)
    {
        if (ShopManager.Instance == null) return;
        if (delta >= 0) ShopManager.Instance.AddGold(delta);
        else            ShopManager.Instance.RemoveGold(-delta);
        UpdateHUD();
    }

    public void QueueNextCardValue(int value) => _queuedNextCardValue = value;
    public void QueueDecayPenalty(int stacks) => _queuedDecayPenalty += stacks;

    public void AddBonusScore(int amount)
    {
        roundScore += amount;
        totalScore += amount;
        UpdateHUD();
    }

    public void InfectRandomCard()
    {
        var targets = handHolder.cards.Where(c => !c.isDecayed).ToList();
        if (targets.Count == 0) return;
        var target = targets[Random.Range(0, targets.Count)];
        target.ApplyDecay();
    }
}