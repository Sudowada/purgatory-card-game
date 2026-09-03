using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

/// <summary>
/// Shows the player a choice of deck modifiers when a relic is collected.
/// Wire up to a Canvas in your scene.
/// </summary>
public class DeckChoiceUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject panel;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;
    public Transform buttonContainer;
    public GameObject choiceButtonPrefab;  // a prefab with Button + TextMeshProUGUI

    private Action _onChoiceComplete;
    private RelicModifierData _currentRelic;

    // All available deck options to present
    private static readonly List<(string label, string key, string description)> DeckOptions = new()
    {
        ("Face Cards Only",   "deck_faceonly",   "Your starting deck contains only face cards (J, Q, K)."),
        ("Number Cards Only", "deck_numberonly",  "Your starting deck contains only number cards (2-10)."),
        ("Jokers Only",       "deck_jokersonly",  "Your starting deck is packed with joker cards."),
        ("Balanced Deck",     "deck_balanced",    "Keep the standard balanced starting deck."),
    };

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
    }

    public void Show(RelicModifierData relic, Action onComplete)
    {
        _currentRelic = relic;
        _onChoiceComplete = onComplete;

        if (titleText != null)
            titleText.text = $"You found: {relic.relicName}";
        if (descriptionText != null)
            descriptionText.text = "Choose your starting deck for future runs:";

        // Clear old buttons
        foreach (Transform child in buttonContainer)
            Destroy(child.gameObject);

        // Build choice buttons
        foreach (var option in DeckOptions)
        {
            var btnGO = Instantiate(choiceButtonPrefab, buttonContainer);
            var desc  = btnGO.GetComponentsInChildren<TextMeshProUGUI>();
            var btn   = btnGO.GetComponent<Button>();

            if (desc.Length > 0) desc[0].text = option.label;
            if (desc.Length > 1) desc[1].text = option.description;

            string capturedKey = option.key;
            btn.onClick.AddListener(() => OnOptionChosen(capturedKey));
        }

        if (panel != null) panel.SetActive(true);
    }

    void OnOptionChosen(string deckKey)
    {
        // Save the choice persistently
        PlayerPrefs.SetString("SelectedDeckModifier", deckKey);
        PlayerPrefs.Save();

        Debug.Log($"[DeckChoiceUI] Player chose deck: {deckKey}");

        if (panel != null) panel.SetActive(false);
        _onChoiceComplete?.Invoke();
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }
}
