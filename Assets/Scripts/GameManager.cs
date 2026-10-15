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
    [SerializeField, Min(0f)] private float initialDrawStagger = 0.07f;
    [Header("Enemy Scaling")]
    [SerializeField] private int enemyBaseMin       = 1;
    [SerializeField] private int enemyBaseMax       = 4;
    [SerializeField] private int enemyScalePerRound = 1;
    [Header("Threshold Scaling")]
    [SerializeField] private int baseThreshold     = 250;
    [SerializeField] private int thresholdPerRound = 50;
    [Header("Eight Round Run")]
    [SerializeField, Min(1)] private int playableRounds = 8;
    [SerializeField, Min(1f)] private float scoreGrowthPerRound = 1.4f;
    private bool runFinished, runWon, endScreenDismissed;
    public int PlayableRounds => Mathf.Max(1, playableRounds);
    public bool RunFinished => runFinished;
    public bool RunWon => runWon;
    public int GetRoundTarget(int round)
    {
        int index = Mathf.Max(0, round - 1);
        // A growth of 1 restores the older linear scaling option.
        float target = scoreGrowthPerRound > 1f
            ? Mathf.Max(1, baseThreshold) * Mathf.Pow(scoreGrowthPerRound, index)
            : Mathf.Max(1, baseThreshold) + index * Mathf.Max(0, thresholdPerRound);
        return Mathf.Max(1, Mathf.RoundToInt(target / 10f) * 10);
    }
    [ContextMenu("Apply Eight Round Playtest Defaults")]
    private void ApplyEightRoundPlaytestDefaults()
    {
        playableRounds = 8; baseThreshold = 250; scoreGrowthPerRound = 1.4f;
        for (int round = 1; round <= PlayableRounds; round++)
            Debug.Log($"[Run] Round {round}/{PlayableRounds}: target {GetRoundTarget(round)}");
    }
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
    // Piles belong to this round. ShopManager's master deck is untouched.
    private readonly List<(int value, string suit)> sacrificedPile =
        new List<(int, string)>();
    private readonly HashSet<(int value, string suit)> returnCardIdentities =
        new HashSet<(int, string)>();
    private bool deferOfferingThresholdTransition;
    private bool handInitialized;
    private bool roundStarted;
    private bool pendingRoundStart;
    private bool roundEndInProgress;
    private GameStateManager observedStateManager;
    private GameState lastObservedCardState;
    private bool hasObservedCardState;
    private void Update()
    {
        if (!handInitialized) return;
        var stateManager = GameStateManager.Instance;
        if (observedStateManager != stateManager)
        {
            if (observedStateManager != null)
                observedStateManager.OnStateChanged -= HandleCardGameStateChanged;
            observedStateManager = stateManager;
            if (observedStateManager != null)
                observedStateManager.OnStateChanged += HandleCardGameStateChanged;
            hasObservedCardState = false;
        }
        // Recover a state switch that happened before we subscribed. The event
        // and this fallback share one handler; neither can deal a second round.
        if (stateManager != null &&
            (!hasObservedCardState || lastObservedCardState != stateManager.CurrentState))
            HandleCardGameStateChanged(stateManager.CurrentState);
    }
    private bool IsSeated => GameStateManager.Instance == null ||
        GameStateManager.Instance.CurrentState == GameState.CardGame;
    public string OfferingBlockReason
    {
        get
        {
            if (runFinished) return runWon ? "run completed" : "run ended";
            if (!handInitialized) return "hand initialization has not finished";
            if (!IsSeated) return "player is in Exploration; sit at the table first";
            if (!roundStarted || pendingRoundStart) return "round has not started";
            if (roundEndInProgress) return "cards are being collected into the deck";
            if (isAnimating) return "scoring/dealing is still running";
            if (inShop) return "shop is open";
            if (_inputBlocked) return "gameplay input is blocked";
            if (handsLeft <= 0) return "no hands remaining";
            return "ready";
        }
    }
    [Header("Card Play Rules")]
    [Tooltip("Runtime hook for the no_sacrifice relic. Played cards are discarded for this round.")]
    [SerializeField] private bool noSacrificeActive;
    public void SetNoSacrificeActive(bool active) => noSacrificeActive = active;
    public bool CanPlayOffering => !runFinished && handInitialized && roundStarted && IsSeated &&
        !pendingRoundStart && !roundEndInProgress && !isAnimating && !inShop &&
        !_inputBlocked && handsLeft > 0;
    public bool ShouldRefillOffering => !runFinished && !_thresholdReached && handsLeft > 0;
    public int CardsRemainingInDrawPile => Mathf.Max(0, currentDeck.Count - deckIndex);
    /// <summary>Immediate discard replacement. Decay of any amount prevents discard.
    /// Existing announced targets keep tracking the slot, so discarding cannot erase an attack.</summary>
    public bool TryDiscardCard(Card card)
    {
        if (!CanPlayOffering || card == null || card.decayLevel > 0 || card.isDecayed ||
            !handHolder.cards.Contains(card) || CardsRemainingInDrawPile <= 0) return false;
        discardPile.Add((card.cardValue, card.cardSuit));
        card.Deselect();
        TryAssignNextCard(card);
        RefreshOfferingHUD();
        return true;
    }
    // Attach/update the effect on a currently drawn card. The current deck uses
    // value/suit identities; duplicate cards with different effects will need
    // persistent card IDs when the shop/deck data model is expanded.
    public void SetReturnEffect(Card card, bool enabled)
    {
        if (card == null) return;
        var identity = (card.cardValue, card.cardSuit);
        if (enabled) returnCardIdentities.Add(identity);
        else returnCardIdentities.Remove(identity);
        CardReturnEffect.SetOnCard(card, enabled);
    }
    public bool TryDrawOfferingReplacement(Card slot)
    {
        return ShouldRefillOffering && TryAssignNextCard(slot);
    }
    public void RefreshOfferingHUD()
    {
        RegisterCardEvents();
        UpdateHUD();
        RefreshScorePreview();
        deckVisual?.UpdateCounts(CardsRemainingInDrawPile,
            discardPile.Count + sacrificedPile.Count);
    }
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
        Debug.Log("[HandFlow] GameManager Awake — seated-hand fix v2.");
        if (GetComponent<ScoreHUD>() == null) gameObject.AddComponent<ScoreHUD>();
        if (GetComponent<ShopRelicInterface>() == null) gameObject.AddComponent<ShopRelicInterface>();
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
    private RandomEnemyTelegraph enemyTelegraph;
    void Start()
    {
        Debug.Log("[HandFlow] GameManager Start — preparing hand initialization.");
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
        // Card scripts must complete Start before their events are registered.
        if (handHolder.cards.Count == 0)
        {
            Debug.LogError("[HandFlow] No hand cards spawned. Check Hand Holder and Card 3D Prefab.");
            yield break;
        }
        var offeringController = FindFirstObjectByType<CardOfferingController>();
        GameObject telegraphOwner = offeringController != null ? offeringController.gameObject : gameObject;
        enemyTelegraph = telegraphOwner.GetComponent<RandomEnemyTelegraph>();
        if (enemyTelegraph == null) enemyTelegraph = telegraphOwner.AddComponent<RandomEnemyTelegraph>();
        enemyTelegraph.Initialize(handHolder);
        RegisterCardEvents();
        handInitialized = true;
        Debug.Log($"[HandFlow] Initialized {handHolder.cards.Count} cards; state={GameStateManager.Instance?.CurrentState}.");
        observedStateManager = GameStateManager.Instance;
        if (observedStateManager != null)
            observedStateManager.OnStateChanged += HandleCardGameStateChanged;
        StartRound();
    }
    private void OnDestroy()
    {
        if (observedStateManager != null)
            observedStateManager.OnStateChanged -= HandleCardGameStateChanged;
        if (Instance == this) Instance = null;
    }
    private void HandleCardGameStateChanged(GameState state)
    {
        if (!handInitialized || runFinished) return;
        hasObservedCardState = true;
        lastObservedCardState = state;
        Debug.Log($"[HandFlow] Received {state}; pendingRoundStart={pendingRoundStart}, roundStarted={roundStarted}.");
        if (state == GameState.Exploration)
        {
            handHolder.SetHandVisible(false);
            return;
        }
        if (roundEndInProgress) return;
        if (_waitingToReturn)
        {
            _waitingToReturn = false;
            ContinueAfterRound();
        }
        else if (pendingRoundStart || !roundStarted)
        {
            if (!inShop && !_thresholdReached) StartRound();
        }
        else if (roundStarted && !inShop && !isAnimating)
        {
            // Re-entering an unfinished round preserves the current hand.
            handHolder.SetHandVisible(true);
        }
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
        if (inShop || isAnimating || _inputBlocked) return;
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
            float displayMult = result.CombinedMultiplier;
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
    public ComboResult LastHandResult { get; private set; }
    public int HandScoreRevision { get; private set; }
    private IEnumerator AnimateScorePreview(ComboResult result, int scoreBeforeHand, int scoreGained)
    {
        LastHandResult = result;
        HandScoreRevision++;
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
            float displayMult = result.CombinedMultiplier;
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
            if (!deferOfferingThresholdTransition) OnThresholdReached();
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
        if (runFinished) return;
        if (roundNumber >= PlayableRounds) { FinishRun(true); return; }
        if (!IsSeated)
        {
            pendingRoundStart = true;
            roundStarted = false;
            Debug.Log("[HandFlow] Waiting for the player to enter CardGame.");
            SetInputBlocked(true);
            handHolder.SetHandVisible(false);
            return;
        }
        pendingRoundStart = false;
        roundStarted = true;
        Debug.Log("[HandFlow] Starting seated round and dealing hand.");
        BeginSeatedRound();
    }
    private void BeginSeatedRound()
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
        enemyTelegraph?.ClearTelegraph();
        roundNumber++;
        stageNumber = Mathf.Clamp(roundNumber, 1, 8);
        handsLeft   = maxHands;
        LastHandResult = null;
        if (roundNumber == 1)
            ShopManager.Instance?.ApplyRunStartBonuses();
        roundScore       = 0;
        currentThreshold = GetRoundTarget(roundNumber);
        int minVal       = enemyBaseMin + (roundNumber - 1) * enemyScalePerRound;
        int maxVal       = enemyBaseMax + (roundNumber - 1) * enemyScalePerRound;
        currentEnemyCard = Random.Range(minVal, maxVal + 1);
        currentDeck = ShopManager.Instance != null
            ? ShopManager.Instance.GetShuffledDeck()
            : BuildDefaultDeck();
        deckIndex = 0;
        discardPile.Clear();
        sacrificedPile.Clear();
        selectedCards.Clear();
        FindFirstObjectByType<CardOfferingController>()?.RestoreEmptySlotsForRound();
        var dealtCards = handHolder.cards.OrderBy(c => c.transform.GetSiblingIndex()).ToList();
        foreach (Card card in dealtCards)
            AssignNextCard(card);
        deckVisual?.UpdateCounts(currentDeck.Count - deckIndex, 0);
        RegisterCardEvents();
        UpdateHUD();
        var offering = FindFirstObjectByType<CardOfferingController>();
        if (offering != null && offering.DeckDrawPoint != null)
            StartCoroutine(DrawInitial3DHand(dealtCards, offering));
        else
        {
            handHolder.SetHandVisible(true);
            if (cardAnimator != null) StartCoroutine(cardAnimator.DrawCards(dealtCards));
            enemyTelegraph?.RollNextTelegraph();
        }
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
        TryAssignNextCard(card);
    }
    private bool TryAssignNextCard(Card card)
    {
        // Never wrap around: spent cards cannot reappear within this round.
        if (card == null || deckIndex >= currentDeck.Count)
            return false;
        var next = currentDeck[deckIndex++];
        card.cardValue = next.value;
        card.cardSuit = next.suit;
        card.decayLevel = 0;
        card.isDecayed = false;
        CardReturnEffect.SetOnCard(card, returnCardIdentities.Contains(next));
        card.InitCardStats();
        return true;
    }
    private IEnumerator DrawInitial3DHand(List<Card> drawn, CardOfferingController offering)
    {
        isAnimating = true;
        offering.SetResolutionInputLocked(true);
        handHolder.SetHandVisible(false);
        foreach (Card card in drawn)
        {
            var physical = card.GetComponentInParent<Card3DInteractable>(true);
            if (physical != null)
            {
                offering.PlaceNewDrawAtDeck(physical);
                physical.ReturnToHandState();
                physical.gameObject.SetActive(true);
                handHolder.SetCardVisible(card, true);
                yield return new WaitForSeconds(initialDrawStagger);
            }
        }
        yield return new WaitForSeconds(offering.RefillSettleDuration);
        if (IsSeated) handHolder.SetHandVisible(true);
        else handHolder.SetHandVisible(false);
        offering.SetResolutionInputLocked(false);
        isAnimating = false;
        enemyTelegraph?.RollNextTelegraph();
        Debug.Log($"[HandFlow] Deal finished; visible={IsSeated}, handSize={handHolder.cards.Count}.");
    }
    // ── Play ──────────────────────────────────────────────────────────────────
    public void OnPlayPressed()
    {
        if (runFinished || isAnimating || inShop || _inputBlocked) return;
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
        enemyTelegraph?.ResolveAttack(toPlay, frozen);
        if (frozen) ConsumableSystem.Instance.ConsumeDecayFreeze();
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
        enemyTelegraph?.RollNextTelegraph();
        isAnimating = false;
    }
    // ── Play (from the 3D altar / CardOfferingController) ───────────────────────
    // Parallel path to OnPlayPressed/PlayRoutine above — kept separate so the
    // existing 2D-selection flow is never touched. Scoring math, relics,
    // consumables, decay and event triggers are identical; the differences are:
    //   - toPlay is passed in directly (already chosen via altar staging)
    //   - no cardAnimator.PlayCards() call, since CardOfferingController's own
    //     burn/altar visuals already represent the card being played
    //   - onComplete fires after scoring/decay. The controller then returns
    //     Return cards unchanged and refills other slots from the draw pile.
    public void PlayOfferedCards(List<Card> toPlay, System.Action onComplete = null)
    {
        if (!CanPlayOffering || toPlay == null || toPlay.Count == 0)
        {
            Debug.LogWarning("[GameManager] Offering rejected before scoring.");
            return;
        }
        StartCoroutine(PlayOfferedCardsRoutine(toPlay, onComplete));
    }
    private IEnumerator PlayOfferedCardsRoutine(List<Card> toPlay, System.Action onComplete)
    {
        isAnimating = true;
        deferOfferingThresholdTransition = true;
        selectedCards.Clear();
        // Capture effects before score/event callbacks can change the card data.
        foreach (Card card in toPlay)
        {
            var identity = (card.cardValue, card.cardSuit);
            if (CardReturnEffect.HasReturn(card)) returnCardIdentities.Add(identity);
            else
            {
                returnCardIdentities.Remove(identity);
                if (noSacrificeActive) discardPile.Add(identity);
                else sacrificedPile.Add(identity);
            }
        }
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
        Coroutine scoreAnimation = StartCoroutine(AnimateScorePreview(result, scoreBefore, scoreGained));
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
        // No cardAnimator.PlayCards() here — the altar's own burn/flame
        // visual is the "play" animation for this path.
        yield return new WaitForSeconds(0.3f);
        bool frozen = ConsumableSystem.Instance != null
            && ConsumableSystem.Instance.decayFrozenThisRound;
        enemyTelegraph?.ResolveAttack(toPlay, frozen);
        if (frozen) ConsumableSystem.Instance.ConsumeDecayFreeze();
        yield return new WaitForSeconds(0.1f);
        // Finish scoring before refilling or opening the next-round screen.
        yield return scoreAnimation;
        onComplete?.Invoke();
        var offering = FindFirstObjectByType<CardOfferingController>();
        if (onComplete != null && offering != null)
            yield return new WaitUntil(() => !offering.InputLocked);
        if (ShouldRefillOffering) enemyTelegraph?.RollNextTelegraph();
        RefreshOfferingHUD();
        isAnimating = false;
        deferOfferingThresholdTransition = false;
        if (_thresholdReached)
            OnThresholdReached();
        else if (handsLeft <= 0 || handHolder.cards.Count == 0)
            TriggerGameOver();
    }
    // ── Threshold / Round Complete ────────────────────────────────────────────
    private void OnThresholdReached()
    {
        if (runFinished || roundEndInProgress) return;
        enemyTelegraph?.ClearTelegraph();
        roundEndInProgress = true;
        roundStarted = false;
        SetInputBlocked(true);
        StartCoroutine(CollectRoundThenShowResults());
    }
    private IEnumerator CollectRoundThenShowResults()
    {
        isAnimating = true;
        var offering = FindFirstObjectByType<CardOfferingController>();
        if (offering != null) offering.SetResolutionInputLocked(true);
        if (offering != null && offering.DeckDrawPoint != null)
            yield return handHolder.CollectHandToDeck(offering.DeckDrawPoint);
        else
        {
            Debug.LogWarning("[GameManager] Assign Deck Draw Point to animate the round-end collection.");
            handHolder.SetHandVisible(false);
        }
        isAnimating = false;
        roundEndInProgress = false;
        if (offering != null) offering.SetResolutionInputLocked(false);
        ShowRoundResults();
    }
    private void ShowRoundResults()
    {
        Debug.Log("[GameManager] OnThresholdReached fired!");
        ShopManager.Instance?.AwardGold(roundScore, currentThreshold);
        if (roundNumber >= PlayableRounds) { FinishRun(true); return; }
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
        // The deck interaction can arrive before the camera/state transition
        // completes. OnStateChanged will deal once CardGame is active.
        if (!handInitialized || roundEndInProgress) return;
        if (!IsSeated) return;
        if (_waitingToReturn)
        {
            _waitingToReturn = false;
            ContinueAfterRound();
        }
        else if (pendingRoundStart)
            StartRound();
    }
    private void ContinueAfterRound()
    {
        if (runFinished) return;
        if (roundNumber >= PlayableRounds) { FinishRun(true); return; }
        SetInputBlocked(false);
        if (roundNumber == 1 || roundNumber % 2 == 0)
            OpenShop();
        else
            StartRound();
    }
    private void TriggerGameOver()
    {
        if (runFinished) return;
        FinishRun(false);
        if (gameOverPanel != null)     gameOverPanel.SetActive(true);
        if (gameOverScoreText != null) gameOverScoreText.text = $"Final Score: {totalScore}";
        Debug.Log($"GAME OVER — Final Score: {totalScore}");
    }
    // ── Shop ──────────────────────────────────────────────────────────────────
    public void OpenShop()
    {
        if (inShop || runFinished) return;
        inShop = true;
        roundStarted = false;
        if (handHolder != null) handHolder.SetHandVisible(false);
        ShopManager.Instance?.GenerateShop(roundNumber);
        if (shopPanel != null) shopPanel.SetActive(false);
        var window = FindFirstObjectByType<ShopWindowInteractable>();
        if (window != null) window.OpenForRun();
        else
        {
            Debug.LogWarning("[Shop] Add and configure ShopWindowInteractable on the shop window.");
            LeaveTableShop();
        }
    }
    [ContextMenu("Test 3D Shop (Play Mode)")]
    private void TestTableShop()
    {
        if (!Application.isPlaying || !handInitialized || isAnimating || roundEndInProgress) return;
        var offering = FindFirstObjectByType<CardOfferingController>();
        if (offering != null && offering.OfferedCount > 0) return;
        OpenShop();
    }
    public void LeaveTableShop()
    {
        if (inShop) OnLeaveShop();
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
    public int CurrentStageNumber => stageNumber;
    public int RoundScore => roundScore;
    public int TotalRunScore => totalScore;
    public int RoundTarget => currentThreshold;
    public int HandsRemaining => handsLeft;
    public int MaximumHands => maxHands;
    public int EnemyCardValue => currentEnemyCard;
    public bool IsShopOpen => inShop;
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
    private void FinishRun(bool won)
    {
        if (runFinished) return;
        enemyTelegraph?.ClearTelegraph();
        runFinished = true; runWon = won; endScreenDismissed = false;
        roundStarted = false; pendingRoundStart = false; _waitingToReturn = false;
        inShop = false;
        SetInputBlocked(true);
        if (handHolder != null) handHolder.SetHandVisible(false);
        var offering = FindFirstObjectByType<CardOfferingController>();
        if (offering != null) offering.SetResolutionInputLocked(true);
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        ShowMessage(won ? $"RUN COMPLETE — {PlayableRounds} rounds cleared!" : "RUN ENDED");
        Debug.Log($"[Run] {(won ? "Victory" : "Defeat")} at round {roundNumber}/{PlayableRounds}. Total score: {totalScore}");
    }
    private void OnGUI()
    {
        if (!runFinished || endScreenDismissed) return;
        float scale = Mathf.Max(0.1f, Mathf.Min(Screen.width / 1600f, Screen.height / 900f));
        Matrix4x4 previous = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
        float width = Screen.width / scale, height = Screen.height / scale;
        Rect panel = new Rect((width - 550) * 0.5f, (height - 270) * 0.5f, 550, 270);
        GUI.Box(panel, "");
        var heading = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
        var body = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(panel.x + 20, panel.y + 25, 510, 45), runWon ? "RUN COMPLETE" : "RUN ENDED", heading);
        GUI.Label(new Rect(panel.x + 20, panel.y + 85, 510, 80),
            $"Round {roundNumber}/{PlayableRounds}\nTotal score: {totalScore}", body);
        if (GUI.Button(new Rect(panel.x + 120, panel.y + 190, 310, 45), "Return to the room"))
        {
            endScreenDismissed = true;
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            GameStateManager.Instance?.GoToExploration();
        }
        GUI.matrix = previous;
    }

}
