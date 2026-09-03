using UnityEngine;

/// <summary>
/// Add this to your GameManager or RunManager.
/// Call GetDeckFilter() at the start of each run to apply persistent deck choices.
/// </summary>
public class DeckModifierApplier : MonoBehaviour
{
    public static string GetSelectedDeckModifier()
        => PlayerPrefs.GetString("SelectedDeckModifier", "deck_balanced");

    /// <summary>
    /// Call this when building the starting deck for a new run.
    /// Returns a filter the deck builder should apply.
    /// </summary>
    public static DeckFilter GetDeckFilter()
    {
        string key = GetSelectedDeckModifier();
        return key switch
        {
            "deck_faceonly"   => DeckFilter.FaceOnly,
            "deck_numberonly" => DeckFilter.NumberOnly,
            "deck_jokersonly" => DeckFilter.JokersOnly,
            _                 => DeckFilter.Balanced
        };
    }

    public enum DeckFilter
    {
        Balanced,
        FaceOnly,
        NumberOnly,
        JokersOnly
    }
}
