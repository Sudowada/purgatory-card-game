using UnityEngine;

[CreateAssetMenu(fileName = "NewRelicModifier", menuName = "Purgatory/Relic Modifier")]
public class RelicModifierData : ScriptableObject
{
    [Header("Identity")]
    public string relicID;          // unique key, e.g. "relic_shadowed_crown"
    public string relicName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Deck Modifier")]
    public DeckModifierType deckModifier = DeckModifierType.None;
    public int startingGoldBonus = 0;
    public string startingJokerID = "";  // if modifier is StartWithJoker

    public enum DeckModifierType
    {
        None,
        FaceCardsOnly,
        NumberCardsOnly,
        JokersOnly,
        StartWithJoker,
        IncreasedStartingGold
    }
}
