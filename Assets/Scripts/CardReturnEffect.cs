using UnityEngine;

/// <summary>
/// Effect on the current logical card, not on every future card drawn into
/// this physical display slot. GameManager resets it when assigning a draw.
/// Attach to the GameObject containing Card, or use GameManager.SetReturnEffect.
/// </summary>
[DisallowMultipleComponent]
public sealed class CardReturnEffect : MonoBehaviour
{
    [SerializeField] private bool returnWhenPlayed;
    public bool ReturnWhenPlayed => returnWhenPlayed;

    public void SetReturnWhenPlayed(bool enabled) => returnWhenPlayed = enabled;

    public static bool HasReturn(Card card)
    {
        if (card == null) return false;
        var effect = card.GetComponent<CardReturnEffect>();
        return effect != null && effect.returnWhenPlayed;
    }

    public static void SetOnCard(Card card, bool enabled)
    {
        if (card == null) return;
        var effect = card.GetComponent<CardReturnEffect>();
        if (effect == null && enabled) effect = card.gameObject.AddComponent<CardReturnEffect>();
        if (effect != null) effect.returnWhenPlayed = enabled;
    }
}
