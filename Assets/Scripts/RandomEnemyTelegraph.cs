using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Playtest-only random telegraph selector. Call RollNextTelegraph after a hand is
/// dealt/refilled, and ResolveOfferedCard when an attacked card is committed.
/// Does not apply damage yet: that belongs to the upcoming combat resolver.
/// </summary>
public sealed class RandomEnemyTelegraph : MonoBehaviour
{
    [SerializeField] private HorizontalCardHolder hand;
    [SerializeField, Range(1, 3)] private int targetsPerAttack = 2;
    private readonly HashSet<Card> targets = new HashSet<Card>();
    private readonly HashSet<Card> stagedBlocks = new HashSet<Card>();

    public bool IsTargeted(Card card) => card != null && targets.Contains(card);

    public void RollNextTelegraph()
    {
        ClearTelegraph();
        if (hand == null) return;
        var eligible = new List<Card3DInteractable>();
        foreach (var card in hand.GetComponentsInChildren<Card3DInteractable>(true))
            if (card.SourceCard != null && card.PhysicalState == CardPhysicalState.InHand)
                eligible.Add(card);

        int count = Mathf.Min(targetsPerAttack, eligible.Count);
        for (int i = 0; i < count; i++)
        {
            int pick = Random.Range(i, eligible.Count);
            (eligible[i], eligible[pick]) = (eligible[pick], eligible[i]);
            var card = eligible[i];
            targets.Add(card.SourceCard);
            var visuals = card.GetComponent<CardAttackVisuals>();
            if (visuals != null) visuals.SetTargeted(true);
        }
    }

    public void ResolveOfferedCard(Card3DInteractable card)
    {
        if (card == null || card.SourceCard == null || !targets.Contains(card.SourceCard)) return;
        stagedBlocks.Add(card.SourceCard);
        var visuals = card.GetComponent<CardAttackVisuals>();
        if (visuals != null) visuals.PlayShieldBlock();
    }

    public void UndoStagedCard(Card3DInteractable card)
    {
        if (card == null || card.SourceCard == null) return;
        if (!stagedBlocks.Remove(card.SourceCard)) return;
        var visuals = card.GetComponent<CardAttackVisuals>();
        if (visuals != null) { visuals.CancelShieldBlock(); visuals.SetTargeted(true); }
    }

    public void ConfirmStagedBlocks()
    {
        foreach (var card in stagedBlocks) targets.Remove(card);
        stagedBlocks.Clear();
    }

    public void ClearTelegraph()
    {
        if (hand != null)
            foreach (var card in hand.GetComponentsInChildren<Card3DInteractable>(true))
            {
                var visuals = card.GetComponent<CardAttackVisuals>();
                if (visuals != null) visuals.SetTargeted(false);
            }
        targets.Clear();
        stagedBlocks.Clear();
    }

    private void OnDisable() => ClearTelegraph();
}
