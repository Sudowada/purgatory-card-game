using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Central manager for all puzzle state.
/// Saves/loads via PlayerPrefs so progress persists across runs.
/// Add to a persistent GameObject in your exploration scene.
/// </summary>
public class PuzzleManager : MonoBehaviour
{
    public static PuzzleManager Instance { get; private set; }

    // All relics the player has permanently collected
    private HashSet<string> _collectedRelics = new HashSet<string>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadProgress();
    }

    // ── Relic Collection ─────────────────────────────────────────────

    public bool HasRelic(string relicID)
        => _collectedRelics.Contains(relicID);

    public void CollectRelic(string relicID)
    {
        if (_collectedRelics.Contains(relicID)) return;
        _collectedRelics.Add(relicID);
        SaveProgress();
        Debug.Log($"[PuzzleManager] Relic collected: {relicID}");
    }

    // ── Generic Puzzle Flags (for non-relic puzzles later) ───────────

    public void SetFlag(string flagKey, bool value)
    {
        PlayerPrefs.SetInt(flagKey, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    public bool GetFlag(string flagKey)
        => PlayerPrefs.GetInt(flagKey, 0) == 1;

    public void IncrementCounter(string counterKey, int amount = 1)
    {
        int current = PlayerPrefs.GetInt(counterKey, 0);
        PlayerPrefs.SetInt(counterKey, current + amount);
        PlayerPrefs.Save();
    }

    public int GetCounter(string counterKey)
        => PlayerPrefs.GetInt(counterKey, 0);

    // ── Save / Load ───────────────────────────────────────────────────

    void SaveProgress()
    {
        PlayerPrefs.SetString("CollectedRelics", string.Join(",", _collectedRelics));
        PlayerPrefs.Save();
    }

    void LoadProgress()
    {
        string saved = PlayerPrefs.GetString("CollectedRelics", "");
        if (string.IsNullOrEmpty(saved)) return;
        foreach (var id in saved.Split(','))
            if (!string.IsNullOrEmpty(id))
                _collectedRelics.Add(id);
    }

    // ── Debug ─────────────────────────────────────────────────────────

    [ContextMenu("Clear All Progress")]
    public void ClearAllProgress()
    {
        PlayerPrefs.DeleteAll();
        _collectedRelics.Clear();
        Debug.Log("[PuzzleManager] All progress cleared.");
    }
}
