using System.Collections.Generic;
using UnityEngine;

public enum EnemyAttackKind { Decay, Plague, Spread, Fester, Wither, Flank, PointValue }

/// <summary>One announced attack per committed hand. Staging blocks are reversible.</summary>
[DisallowMultipleComponent]
public sealed class RandomEnemyTelegraph : MonoBehaviour
{
    [SerializeField] private HorizontalCardHolder hand;
    [SerializeField, Range(1, 3)] private int targetsPerAttack = 2;
    [SerializeField, Range(0f, 2f)] private float decayFraction = 0.30f;
    [Tooltip("Reserved for decay mitigation. Applied before rounding down when below one.")]
    [SerializeField, Range(0f, 1f)] private float decayMultiplier = 1f;
    [Header("Attack Selection")]
    [SerializeField] private bool randomAttackKind = true;
    [SerializeField] private EnemyAttackKind attackKind = EnemyAttackKind.Plague;
    [SerializeField, Range(1, 14)] private int pointDamage = 2;
    public EnemyAttackKind CurrentAttack { get; private set; }
    public event System.Action<Card> CardRotted;
    private readonly Dictionary<Card, float> announcedAmounts = new Dictionary<Card, float>();
    private readonly HashSet<Card> targets = new HashSet<Card>();
    private readonly HashSet<Card> stagedBlocks = new HashSet<Card>();
    private readonly HashSet<CardAttackVisuals> activeVisuals = new HashSet<CardAttackVisuals>();

    public void Initialize(HorizontalCardHolder holder) => hand = holder;
    public bool IsTargeted(Card card) => card != null && targets.Contains(card);

    public void RollNextTelegraph()
    {
        ClearTelegraph();
        if (hand == null) return;
        CurrentAttack = randomAttackKind ? (EnemyAttackKind)Random.Range(0, 7) : attackKind;
        var eligible = new List<Card3DInteractable>();
        foreach (var data in hand.cards)
        {
            if (data == null) continue;
            var physical = data.GetComponentInParent<Card3DInteractable>(true);
            if (physical != null && physical.PhysicalState == CardPhysicalState.InHand)
                eligible.Add(physical);
        }
        // Snapshot recipients AND amounts before staging can reorder or recycle slots.
        int count = Mathf.Min(CurrentAttack == EnemyAttackKind.Plague ? 3 : targetsPerAttack, eligible.Count);
        if (CurrentAttack != EnemyAttackKind.PointValue && RelicSystem.Instance != null && RelicSystem.Instance.GetDecayAll())
        {
            foreach (var physical in eligible) Announce(physical);
            return;
        }
        if (CurrentAttack == EnemyAttackKind.Flank)
        {
            if (eligible.Count > 0) Announce(eligible[0]);
            if (eligible.Count > 1) Announce(eligible[eligible.Count - 1]);
            return;
        }
        if (CurrentAttack == EnemyAttackKind.Spread || CurrentAttack == EnemyAttackKind.Fester || CurrentAttack == EnemyAttackKind.Wither)
        {
            for (int i = 0; i < eligible.Count; i++)
            {
                var source = eligible[i].SourceCard;
                if (CurrentAttack == EnemyAttackKind.Wither ||
                    (CurrentAttack == EnemyAttackKind.Fester && source.decayLevel > 0)) Announce(eligible[i]);
                if (CurrentAttack != EnemyAttackKind.Spread || source.decayLevel <= 0) continue;
                // Only original immediate neighbours; new infections never propagate this turn.
                if (i > 0) Announce(eligible[i - 1]);
                if (i + 1 < eligible.Count) Announce(eligible[i + 1]);
            }
            return;
        }
        for (int i = 0; i < count; i++)
        {
            int pick = Random.Range(i, eligible.Count);
            (eligible[i], eligible[pick]) = (eligible[pick], eligible[i]);
            var card = eligible[i];
            Announce(card);
        }
    }

    private void Announce(Card3DInteractable physical)
    {
        var card = physical.SourceCard;
        if (card == null || !targets.Add(card)) return;
        float amount;
        if (CurrentAttack == EnemyAttackKind.PointValue) amount = pointDamage;
        else if (CurrentAttack == EnemyAttackKind.Fester) amount = card.decayLevel;
        else if (CurrentAttack == EnemyAttackKind.Wither || CurrentAttack == EnemyAttackKind.Spread) amount = 1;
        else
        {
            float fraction = CurrentAttack == EnemyAttackKind.Plague ? 0.30f :
                CurrentAttack == EnemyAttackKind.Flank ? 0.50f : decayFraction;
            amount = Mathf.Max(0, card.faceValue) * fraction;
        }
        announcedAmounts[card] = amount;
        var visuals = physical.GetComponent<CardAttackVisuals>();
        if (visuals == null) visuals = physical.gameObject.AddComponent<CardAttackVisuals>();
        visuals.SetPresentationVisible(hand.IsCardVisible(card));
        activeVisuals.Add(visuals);
        visuals.SetTargeted(true);
        int displayed = decayMultiplier < 1f && CurrentAttack != EnemyAttackKind.PointValue
            ? Mathf.FloorToInt(amount * decayMultiplier) : Mathf.CeilToInt(amount);
        visuals.SetIntent(CurrentAttack + " " + displayed + (CurrentAttack == EnemyAttackKind.PointValue ? " pts" : " rot"));
    }

    public void ResolveOfferedCard(Card3DInteractable card)
    {
        if (card == null || !IsTargeted(card.SourceCard) || !stagedBlocks.Add(card.SourceCard)) return;
        card.GetComponent<CardAttackVisuals>()?.PlayShieldBlock();
    }

    public void UndoStagedCard(Card3DInteractable card)
    {
        if (card == null || card.SourceCard == null || !stagedBlocks.Remove(card.SourceCard)) return;
        var visuals = card.GetComponent<CardAttackVisuals>();
        if (visuals != null) { visuals.CancelShieldBlock(); visuals.SetTargeted(true); }
    }

    public void ConfirmStagedBlocks()
    {
        foreach (var card in stagedBlocks) targets.Remove(card);
        stagedBlocks.Clear();
    }

    /// <summary>Resolve the announced snapshot before physical slots receive new data.
    /// No adjacency processing: Spread must be a separate, explicitly announced attack.</summary>
    public void ResolveAttack(List<Card> played, bool frozen)
    {
        var relics = RelicSystem.Instance;
        bool pointAttack = CurrentAttack == EnemyAttackKind.PointValue;
        bool apply = pointAttack || (!frozen && (relics == null || relics.ShouldDecayThisTurn()));
        if (apply && hand != null)
        {
            var victims = new List<Card>(targets);
            foreach (var card in victims)
            {
                if (card == null || (played != null && played.Contains(card)) || stagedBlocks.Contains(card) || !hand.cards.Contains(card)) continue;
                float baseAmount = announcedAmounts.TryGetValue(card, out var announced) ? announced :
                    Mathf.Max(0, card.faceValue) * decayFraction;
                if (pointAttack) { card.ApplyPointDamage(Mathf.CeilToInt(baseAmount)); continue; }
                if (relics != null && relics.IsDecayProtected(card, hand.cards)) continue;
                float raw = baseAmount * decayMultiplier;
                int amount = decayMultiplier < 1f ? Mathf.FloorToInt(raw) : Mathf.CeilToInt(raw);
                if (relics != null && relics.HasRelic(RelicType.DoubleDecay)) amount *= 2;
                if (amount > 0)
                {
                    bool rotted = relics != null && relics.GetDecayRotOnStack()
                        ? card.ForceFullDecay() : card.ApplyDecay(amount);
                    if (rotted) CardRotted?.Invoke(card);
                }
            }
            if (!pointAttack) relics?.OnDecayPhaseEnd(hand.cards);
        }
        ClearTelegraph();
    }

    public void ClearTelegraph()
    {
        foreach (var visual in activeVisuals)
            if (visual != null) visual.SetTargeted(false);
        activeVisuals.Clear();
        targets.Clear();
        announcedAmounts.Clear();
        stagedBlocks.Clear();
    }

    private void OnDisable() => ClearTelegraph();
}
