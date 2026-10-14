using System.Linq;
using System.Collections.Generic;
using UnityEngine;
public enum CardRarity { Common, Uncommon, Rare }
[System.Serializable]
public class ShopCard
{
    public int value;
    public string suit;
    public CardRarity rarity;
    public int cost;
    public string displayName;
    private static string GetDisplayValue(int v) => v switch
    {
        1  => "A",
        11 => "J",
        12 => "Q",
        13 => "K",
        _  => v.ToString()
    };
    public ShopCard(int v, string s, CardRarity r)
    {
        value       = v;
        suit        = s;
        rarity      = r;
        displayName = GetDisplayValue(v) + s;
        cost        = r switch
        {
            CardRarity.Rare     => 7,
            CardRarity.Uncommon => 4,
            _                   => 2
        };
    }
}
/// <summary>
/// Manages shop inventory, gold, deck pool, and purchases.
/// Attach to the same GameObject as GameManager.
/// </summary>
public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }
    private readonly string[] suits = { "♠", "♥", "♦", "♣" };
    [Header("Gold")]
    public int   playerGold        = 0;
    public float rareChanceModifier = 0f;
    public List<string> collectibles   = new List<string>();
    public int          bonusStartGold = 0;
    public int baseGoldPerRound = 3;
    public int burnCost         = 4;
    // One shop visit per cleared encounter. Reroll state is shared by all buttons.
    public int PaidRerollsThisVisit { get; private set; }
    public int SavedFreeRerolls { get; private set; }
    public int VisitFreeRerolls { get; private set; }
    public int FreeRerolls => SavedFreeRerolls + VisitFreeRerolls;
    public int NextPaidRerollCost => 2 + PaidRerollsThisVisit;
    public int CurrentRerollCost => FreeRerolls > 0 ? 0 : NextPaidRerollCost;
    private int shopVisitId = -1;
    private bool shopVisitActive;
    private bool tokenGrantedThisVisit;
    private int clearedEncounterCount;
    private readonly HashSet<int> rewardedEncounterIds = new HashSet<int>();

    public void BeginShopVisit(int encounterId)
    {
        if (shopVisitId != encounterId)
        {
            shopVisitId = encounterId;
            PaidRerollsThisVisit = 0;
            VisitFreeRerolls = 0;
            tokenGrantedThisVisit = false;
        }
        shopVisitActive = true;
        GrantVisitTokenIfOwned();
    }

    public void EndShopVisit()
    {
        shopVisitActive = false;
        VisitFreeRerolls = 0;
        // Reopening the same encounter's shop does not grant another token.
    }

    private void GrantVisitTokenIfOwned()
    {
        if (!shopVisitActive || tokenGrantedThisVisit || RelicSystem.Instance == null ||
            !RelicSystem.Instance.HasRelic(RelicType.RerollToken)) return;
        VisitFreeRerolls++;
        tokenGrantedThisVisit = true;
    }

    public void OnEconomyRelicAcquired(RelicType type)
    {
        if (type == RelicType.RerollToken) GrantVisitTokenIfOwned();
    }

    public void OnEconomyRelicRemoved(RelicType type)
    {
        if (type == RelicType.RerollToken) VisitFreeRerolls = 0;
    }

    // Call only after replacement stock has been successfully generated.
    public bool TryPayForReroll(out int goldPaid, out string reason)
    {
        goldPaid = 0;
        if (!shopVisitActive) { reason = "Open the shop first."; return false; }
        if (VisitFreeRerolls > 0) VisitFreeRerolls--;
        else if (SavedFreeRerolls > 0) SavedFreeRerolls--;
        else
        {
            goldPaid = NextPaidRerollCost;
            if (!SpendGold(goldPaid))
            { goldPaid = 0; reason = "Not enough gold to reroll."; return false; }
            PaidRerollsThisVisit++;
        }
        RelicSystem.Instance?.OnShopRerolled();
        reason = goldPaid == 0 ? "Free reroll used." : $"Rerolled for {goldPaid} gold.";
        return true;
    }

    // AwardGold is the existing successful-encounter hook. Deduplicate by
    // the existing GameManager encounter counter (named CurrentRoundNumber).
    private void RecordClearedEncounter()
    {
        var gm = GameManager.Instance;
        if (gm != null && !rewardedEncounterIds.Add(gm.CurrentRoundNumber)) return;
        clearedEncounterCount++;
        if (clearedEncounterCount % 3 != 0 || RelicSystem.Instance == null ||
            !RelicSystem.Instance.HasRelic(RelicType.CoinFlip)) return;
        bool won = Random.value < 0.5f;
        if (won) SavedFreeRerolls++;
        Debug.Log(won ? "[Relics] Coin Flip: earned one saved free reroll."
                      : "[Relics] Coin Flip: no free reroll this round.");
    }

    public static int GetRelicPurchaseCost(RelicType type, int legacyCost)
    {
        switch (type)
        {
            case RelicType.LoadedDice: return 7;
            case RelicType.RerollToken: return 4;
            case RelicType.CoinFlip: return 2;
            default: return Mathf.Max(0, legacyCost);
        }
    }

    [ContextMenu("Playtest: Grant Economy Relics and 30 Gold")]
    private void GrantEconomyPlaytestRelics()
    {
        if (!Application.isPlaying)
        { Debug.LogWarning("[Shop] Start Play mode before granting test relics."); return; }
        var system = RelicSystem.Instance;
        if (system == null) return;
        foreach (var type in new[] { RelicType.LoadedDice, RelicType.RerollToken, RelicType.CoinFlip })
        {
            if (!system.CanAcquireRelic(type)) continue;
            system.AddRelic(type);
            purchasePrices[type] = GetRelicPurchaseCost(type, 0);
        }
        AddGold(30);
        Debug.Log("[Shop] Economy playtest ready. Existing owned relics were kept.");
    }

    // Current shop offerings
    [HideInInspector] public Relic      offeredRelic;
    [HideInInspector] public int        offeredRelicCost;
    [HideInInspector] public ShopCard   offeredCard;
    [HideInInspector] public Consumable offeredConsumable;
    // Full deck pool
    private List<(int value, string suit)> deckPool = new List<(int, string)>();
    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        InitializeDeckPool();
    }
    // ── Deck Pool ─────────────────────────────────────────────────────────────
    private void InitializeDeckPool()
    {
        deckPool.Clear();
        foreach (string s in suits)
            for (int v = 1; v <= 13; v++)
                deckPool.Add((v, s));
    }
    public List<(int value, string suit)> GetShuffledDeck()
    {
        var shuffled = new List<(int, string)>(deckPool);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }
        return shuffled;
    }
    public void AddCardToDeckPool(int value, string suit) => deckPool.Add((value, suit));
    public void BurnCardFromDeckPool(int value, string suit)
    {
        int idx = deckPool.FindIndex(c => c.value == value && c.suit == suit);
        if (idx >= 0) deckPool.RemoveAt(idx);
    }
    // ── Gold ──────────────────────────────────────────────────────────────────
    public void ApplyRunStartBonuses()
    {
        playerGold += bonusStartGold;
        if (bonusStartGold > 0)
            Debug.Log($"[ShopManager] Applied {bonusStartGold} bonus starting gold.");
    }
    private readonly Dictionary<RelicType, int> purchasePrices = new Dictionary<RelicType, int>();
    public int LastBaseGold { get; private set; }
    public int LastScoreGold { get; private set; }
    public int LastInterestGold { get; private set; }
    public int LastAwardedGold => LastBaseGold + LastScoreGold + LastInterestGold;
    public int GetRelicSaleValue(RelicType type) => purchasePrices.TryGetValue(type, out int paid) ? paid / 2 : 0;
    public bool TrySellRelic(RelicType type, out string reason)
    {
        var gm = GameManager.Instance;
        var offering = FindFirstObjectByType<CardOfferingController>();
        var window = FindFirstObjectByType<ShopWindowInteractable>();
        bool browsing = window != null && window.CanInteract;
        if (window != null && window.IsBusy)
        { reason = "Wait for the shop window to finish moving."; return false; }
        if (gm != null && ((!gm.IsShopOpen && !browsing && !gm.CanPlayOffering) ||
            (offering != null && offering.OfferedCount > 0)))
        { reason = "Finish the current hand before selling."; return false; }
        var system = RelicSystem.Instance;
        if (system == null || !system.HasRelic(type))
        { reason = "Relic is no longer owned."; return false; }
        int value = GetRelicSaleValue(type);
        system.RemoveRelic(type);
        purchasePrices.Remove(type);
        AddGold(value);
        reason = $"Sold for {value} gold.";
        return true;
    }
    public void AwardGold(int scoreThisRound, int threshold)
    {
        RecordClearedEncounter();
        int excess  = Mathf.Max(0, scoreThisRound - threshold);
        int bonus   = excess / 10;
        // Interest: +1 gold per 5 held, applied before adding base
        int interest = playerGold / 5;
        int awarded = baseGoldPerRound + bonus + interest;
        LastBaseGold = baseGoldPerRound;
        LastScoreGold = bonus;
        LastInterestGold = interest;
        playerGold += awarded;
        Debug.Log($"[ShopManager] Gold: +{awarded} ({baseGoldPerRound} base + {bonus} score bonus + {interest} interest). Total: {playerGold}");
    }
    public bool SpendGold(int amount)
    {
        if (amount < 0 || playerGold < amount) return false;
        playerGold -= amount;
        return true;
    }
    public void AddGold(int amount)    => playerGold = Mathf.Max(0, playerGold + amount);
    public void RemoveGold(int amount) => playerGold = Mathf.Max(0, playerGold - amount);
    public void AddCollectible(string id)
    {
        if (!collectibles.Contains(id))
        {
            collectibles.Add(id);
            CollectiblesTable.Instance?.Refresh();
        }
    }
    public bool HasCollectible(string id) => collectibles.Contains(id);
    // ── Old Shop Generation (UI panel shop) ───────────────────────────────────
    public void GenerateShop(int roundNumber)
    {
        offeredRelic      = GenerateRelic(roundNumber);
        offeredCard       = GenerateCard(roundNumber);
        offeredConsumable = GenerateConsumable();
    }
    // ── Unlock Conditions ─────────────────────────────────────────────────────
    private static readonly List<(RelicType type, string requirement, System.Func<int, bool> check)>
        _unlockConditions = new List<(RelicType, string, System.Func<int, bool>)>
    {
        (RelicType.TimeEngine,       "Reach 500 total score",
            _ => RunStats.Instance != null && RunStats.Instance.TotalScore >= 500),
        (RelicType.GrindCore,        "Reach 1000 total score",
            _ => RunStats.Instance != null && RunStats.Instance.TotalScore >= 1000),
        (RelicType.HighValueInvest,  "Reach 1500 total score",
            _ => RunStats.Instance != null && RunStats.Instance.TotalScore >= 1500),
        (RelicType.ComboBank,        "Survive 3 rounds",
            _ => RunStats.Instance != null && RunStats.Instance.RoundsSurvived >= 3),
        (RelicType.TurnInvestor,     "Survive 5 rounds",
            _ => RunStats.Instance != null && RunStats.Instance.RoundsSurvived >= 5),
        (RelicType.GlassCannon,      "Survive 6 rounds",
            _ => RunStats.Instance != null && RunStats.Instance.RoundsSurvived >= 6),
        (RelicType.Purifier,         "Own 2 other relics",
            _ => RelicSystem.Instance != null && RelicSystem.Instance.activeRelics.Count >= 2),
        (RelicType.NecromancyRelic,  "Own 3 other relics",
            _ => RelicSystem.Instance != null && RelicSystem.Instance.activeRelics.Count >= 3),
        (RelicType.AnchorCard,       "Own 4 other relics",
            _ => RelicSystem.Instance != null && RelicSystem.Instance.activeRelics.Count >= 4),
        (RelicType.CardCycler,       "Play 5 hands",
            _ => RunStats.Instance != null && RunStats.Instance.HandsPlayed >= 5),
        (RelicType.DrawReactor,      "Play 10 hands",
            _ => RunStats.Instance != null && RunStats.Instance.HandsPlayed >= 10),
        (RelicType.InstantRot,       "Survive 4 rounds and own 2 relics",
            _ => RunStats.Instance != null && RunStats.Instance.RoundsSurvived >= 4
              && RelicSystem.Instance != null && RelicSystem.Instance.activeRelics.Count >= 2),
        (RelicType.PlagueAll,        "Survive 7 rounds",
            _ => RunStats.Instance != null && RunStats.Instance.RoundsSurvived >= 7),
    };
    public static bool IsRelicUnlocked(RelicType type, int round)
    {
        foreach (var (t, _, check) in _unlockConditions)
            if (t == type) return check(round);
        return true;
    }
    public static string GetUnlockRequirement(RelicType type)
    {
        foreach (var (t, req, _) in _unlockConditions)
            if (t == type) return req;
        return null;
    }
    private Relic GenerateRelic(int round)
    {
        var allTypes = new List<RelicType>((RelicType[])System.Enum.GetValues(typeof(RelicType)));
        if (RelicSystem.Instance != null)
            allTypes.RemoveAll(t => RelicSystem.Instance.HasRelic(t));
        allTypes.RemoveAll(t => !IsRelicUnlocked(t, round));
        if (allTypes.Count == 0)
        {
            allTypes = new List<RelicType>((RelicType[])System.Enum.GetValues(typeof(RelicType)));
            if (RelicSystem.Instance != null)
                allTypes.RemoveAll(t => RelicSystem.Instance.HasRelic(t));
        }
        if (allTypes.Count == 0) return null;
        RelicType picked;
        if (rareChanceModifier > 0f && Random.value < rareChanceModifier)
        {
            var premium = allTypes.Where(t => GetUnlockRequirement(t) != null).ToList();
            picked = premium.Count > 0
                ? premium[Random.Range(0, premium.Count)]
                : allTypes[Random.Range(0, allTypes.Count)];
        }
        else if (rareChanceModifier < 0f && Random.value < -rareChanceModifier)
        {
            var common = allTypes.Where(t => GetUnlockRequirement(t) == null).ToList();
            picked = common.Count > 0
                ? common[Random.Range(0, common.Count)]
                : allTypes[Random.Range(0, allTypes.Count)];
        }
        else
        {
            picked = allTypes[Random.Range(0, allTypes.Count)];
        }
        offeredRelicCost = GetRelicPurchaseCost(picked, 5 + (round / 2));
        return RelicSystem.MakeRelicStatic(picked);
    }
    private ShopCard GenerateCard(int round)
    {
        float rareChance     = Mathf.Clamp01(round * 0.05f + rareChanceModifier);
        float uncommonChance = Mathf.Clamp01(0.15f + round * 0.05f);
        CardRarity rarity;
        float roll = Random.value;
        if (roll < rareChance)          rarity = CardRarity.Rare;
        else if (roll < uncommonChance) rarity = CardRarity.Uncommon;
        else                            rarity = CardRarity.Common;
        int value = rarity switch
        {
            CardRarity.Rare     => new int[] { 1, 11, 12, 13 }[Random.Range(0, 4)],
            CardRarity.Uncommon => Random.Range(6, 11),
            _                   => Random.Range(2, 6)
        };
        string suit = suits[Random.Range(0, suits.Length)];
        return new ShopCard(value, suit, rarity);
    }
    private Consumable GenerateConsumable()
    {
        var allTypes = ConsumableSystem.AllTypes();
        return ConsumableSystem.MakeConsumable(allTypes[Random.Range(0, allTypes.Count)]);
    }
    // ── Purchases ─────────────────────────────────────────────────────────────
    public bool TryBuyRelic(Relic relic, int cost, out string reason)
    {
        if (relic == null) { reason = "Sold out."; return false; }
        var system = RelicSystem.Instance;
        if (system == null) { reason = "Relic system unavailable."; return false; }
        if (system.HasRelic(relic.type)) { reason = "Already owned."; return false; }
        if (!system.CanAcquireRelic(relic.type)) { reason = "Relic slots full."; return false; }
        cost = GetRelicPurchaseCost(relic.type, cost);
        if (!SpendGold(Mathf.Max(0, cost))) { reason = "Not enough gold."; return false; }
        system.AddRelic(relic.type);
        purchasePrices[relic.type] = Mathf.Max(0, cost);
        reason = "Relic acquired.";
        return true;
    }
    public bool BuyRelic()
    {
        if (!TryBuyRelic(offeredRelic, offeredRelicCost, out _)) return false;
        offeredRelic = null;
        return true;
    }
    public bool BuyCard()
    {
        if (offeredCard == null || !SpendGold(offeredCard.cost)) return false;
        AddCardToDeckPool(offeredCard.value, offeredCard.suit);
        offeredCard = null;
        return true;
    }
    public bool BuyConsumable()
    {
        if (offeredConsumable == null || !SpendGold(offeredConsumable.cost)) return false;
        ConsumableSystem.Instance?.UseConsumable(offeredConsumable.type);
        offeredConsumable = null;
        return true;
    }
    public bool BurnCard(Card card)
    {
        if (!SpendGold(burnCost)) return false;
        BurnCardFromDeckPool(card.cardValue, card.cardSuit);
        return true;
    }
    // ── World Shop (3D exploration room window) ───────────────────────────────
    /// <summary>
    /// Called by ShopWindowInteractable to generate the world shop relic pool.
    /// Returns relics and their costs, weighted by current stage.
    /// </summary>
    public (List<Relic> relics, List<int> costs) GenerateWorldShop()
    {
        int stage = GameManager.Instance != null
            ? GameManager.Instance.CurrentStageNumber
            : 1;
        stage = Mathf.Clamp(stage, 1, 8);
        int relicCount = GetWorldRelicCount(stage);
        var relics    = new List<Relic>();
        var costs     = new List<int>();
        var usedTypes = new HashSet<RelicType>();
        // Exclude already owned relics
        if (RelicSystem.Instance != null)
            foreach (var r in RelicSystem.Instance.activeRelics)
                usedTypes.Add(r.type);
        for (int i = 0; i < relicCount; i++)
        {
            var picked = PickWeightedRelic(stage, usedTypes);
            if (!picked.HasValue) break;
            RelicType type = picked.Value;
            usedTypes.Add(type);
            var relic = RelicSystem.MakeRelicStatic(type);
            relics.Add(relic);
            costs.Add(GetRelicPurchaseCost(type, GetWorldRelicCost(relic.category, stage)));
        }
        return (relics, costs);
    }
    private int GetWorldRelicCount(int stage) => 5;

    private RelicType? PickWeightedRelic(int stage, HashSet<RelicType> exclude)
    {
        // Rares can appear early, so Loaded Dice can build over a full run.
        float progress = (stage - 1) / 7f;
        float commonWeight = Mathf.Lerp(0.65f, 0.25f, progress);
        float rareWeight = Mathf.Clamp(Mathf.Lerp(0.10f, 0.40f, progress)
            + rareChanceModifier, 0.05f, 0.70f);
        commonWeight = Mathf.Min(commonWeight, 1f - rareWeight);
        float roll = Random.value;
        RelicRarity target = roll < rareWeight ? RelicRarity.Rare
            : roll < rareWeight + commonWeight ? RelicRarity.Common : RelicRarity.Uncommon;
        var available = new List<RelicType>();
        foreach (RelicType type in System.Enum.GetValues(typeof(RelicType)))
            if (!exclude.Contains(type)) available.Add(type);
        if (available.Count == 0) return null;
        var candidates = available.Where(type => RelicSystem.GetRarity(type) == target).ToList();
        if (candidates.Count == 0) candidates = available;
        return candidates[Random.Range(0, candidates.Count)];
    }

    private int GetWorldRelicCost(RelicCategory cat, int stage)
    {
        int baseCost = cat switch
        {
            RelicCategory.RuleChanger       => 20,
            RelicCategory.DecayManipulation => 20,
            RelicCategory.ComboEngine       => 30,
            RelicCategory.ScalingMultiplier => 35,
            RelicCategory.CardManipulation  => 30,
            RelicCategory.RiskChallenge     => 50,
            _                               => 25
        };
        return baseCost + (stage - 1) * 3;
    }
}
