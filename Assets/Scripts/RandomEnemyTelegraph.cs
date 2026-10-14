using System.Collections.Generic;
using UnityEngine;

/// <summary>One announced attack per committed hand. Staging blocks are reversible.</summary>
[DisallowMultipleComponent]
public sealed class RandomEnemyTelegraph : MonoBehaviour
{
    [SerializeField] private HorizontalCardHolder hand;
    [SerializeField, Range(1, 3)] private int targetsPerAttack = 2;
    [SerializeField, Range(0f, 2f)] private float decayFraction = 0.30f;
    [Tooltip("Reserved for decay mitigation. Applied before rounding down when below one.")]
    [SerializeField, Range(0f, 1f)] private float decayMultiplier = 1f;
    private readonly HashSet<Card> targets = new HashSet<Card>();
    private readonly HashSet<Card> stagedBlocks = new HashSet<Card>();
    private readonly HashSet<CardAttackVisuals> activeVisuals = new HashSet<CardAttackVisuals>();

    public void Initialize(HorizontalCardHolder holder) => hand = holder;
    public bool IsTargeted(Card card) => card != null && targets.Contains(card);

    public void RollNextTelegraph()
    {
        ClearTelegraph();
        if (hand == null) return;
        var eligible = new List<Card3DInteractable>();
        foreach (var data in hand.cards)
        {
            if (data == null) continue;
            var physical = data.GetComponentInParent<Card3DInteractable>(true);
            if (physical != null && physical.PhysicalState == CardPhysicalState.InHand)
                eligible.Add(physical);
        }
        int count = Mathf.Min(targetsPerAttack, eligible.Count);
        for (int i = 0; i < count; i++)
        {
            int pick = Random.Range(i, eligible.Count);
            (eligible[i], eligible[pick]) = (eligible[pick], eligible[i]);
            var card = eligible[i];
            targets.Add(card.SourceCard);
            var visuals = card.GetComponent<CardAttackVisuals>();
            if (visuals == null) visuals = card.gameObject.AddComponent<CardAttackVisuals>();
            visuals.SetPresentationVisible(hand.IsCardVisible(card.SourceCard));
            activeVisuals.Add(visuals);
            visuals.SetTargeted(true);
        }
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
        bool apply = !frozen && (relics == null || relics.ShouldDecayThisTurn());
        if (apply && hand != null)
        {
            // PlagueAll explicitly broadens the attack; ordinary attacks only hit announced cards.
            var victims = relics != null && relics.GetDecayAll()
                ? new List<Card>(hand.cards) : new List<Card>(targets);
            foreach (var card in victims)
            {
                if (card == null || played.Contains(card) || !hand.cards.Contains(card)) continue;
                if (relics != null && relics.IsDecayProtected(card, hand.cards)) continue;
                float raw = Mathf.Max(0, card.faceValue) * decayFraction * decayMultiplier;
                int amount = decayMultiplier < 1f ? Mathf.FloorToInt(raw) : Mathf.CeilToInt(raw);
                if (relics != null && relics.HasRelic(RelicType.DoubleDecay)) amount *= 2;
                if (amount > 0)
                {
                    if (relics != null && relics.GetDecayRotOnStack()) card.ForceFullDecay();
                    else card.ApplyDecay(amount);
                }
            }
            relics?.OnDecayPhaseEnd(hand.cards);
        }
        ClearTelegraph();
    }

    public void ClearTelegraph()
    {
        foreach (var visual in activeVisuals)
            if (visual != null) visual.SetTargeted(false);
        activeVisuals.Clear();
        targets.Clear();
        stagedBlocks.Clear();
    }

    private void OnDisable() => ClearTelegraph();
}
