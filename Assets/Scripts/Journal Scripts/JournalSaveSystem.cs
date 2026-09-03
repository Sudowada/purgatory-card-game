using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles saving and loading all journal state to PlayerPrefs.
/// Called automatically by JournalManager whenever state changes.
/// You should never need to call this directly.
///
/// Save keys:
///   journal_unlocked_notes          — comma-separated unlocked note IDs
///   journal_read_notes              — comma-separated read note IDs
///   journal_unlocked_relics         — comma-separated unlocked relic IDs
///   journal_examined_relics         — comma-separated examined relic IDs
///   journal_upgraded_relics         — comma-separated upgraded relic IDs (permanent)
///   journal_collectible_states      — semicolon-separated id:state pairs (persistent)
///   journal_collectibles_ever_found — comma-separated collectible IDs ever picked up
/// </summary>
public static class JournalSaveSystem
{
    // ── PlayerPrefs keys ──────────────────────────────────────────────────────

    private const string KEY_UNLOCKED_NOTES         = "journal_unlocked_notes";
    private const string KEY_READ_NOTES             = "journal_read_notes";
    private const string KEY_UNLOCKED_RELICS        = "journal_unlocked_relics";
    private const string KEY_EXAMINED_RELICS        = "journal_examined_relics";
    private const string KEY_UPGRADED_RELICS        = "journal_upgraded_relics";
    private const string KEY_COLLECTIBLE_STATES     = "journal_collectible_states";
    private const string KEY_COLLECTIBLES_FOUND     = "journal_collectibles_ever_found";

    private const char   SEPARATOR                  = ',';
    private const char   PAIR_SEPARATOR             = ';';
    private const char   KV_SEPARATOR               = ':';

    // ── Save data container ───────────────────────────────────────────────────

    public class JournalSaveData
    {
        public List<string>              unlockedNoteIDs      = new List<string>();
        public List<string>              readNoteIDs          = new List<string>();
        public List<string>              unlockedRelicIDs     = new List<string>();
        public List<string>              examinedRelicIDs     = new List<string>();
        public List<string>              upgradedRelicIDs     = new List<string>();
        public Dictionary<string, int>   collectibleStates    = new Dictionary<string, int>();
        public List<string>              collectiblesEverFound = new List<string>();
    }

    // ── Save ──────────────────────────────────────────────────────────────────

    public static void SaveJournalState(
        HashSet<string>              unlockedNoteIDs,
        HashSet<string>              readNoteIDs,
        HashSet<string>              unlockedRelicIDs,
        HashSet<string>              examinedRelicIDs,
        HashSet<string>              upgradedRelicIDs,
        Dictionary<string, int>      collectibleStates,
        HashSet<string>              collectiblesEverFound)
    {
        PlayerPrefs.SetString(KEY_UNLOCKED_NOTES,     Serialize(unlockedNoteIDs));
        PlayerPrefs.SetString(KEY_READ_NOTES,          Serialize(readNoteIDs));
        PlayerPrefs.SetString(KEY_UNLOCKED_RELICS,     Serialize(unlockedRelicIDs));
        PlayerPrefs.SetString(KEY_EXAMINED_RELICS,     Serialize(examinedRelicIDs));
        PlayerPrefs.SetString(KEY_UPGRADED_RELICS,     Serialize(upgradedRelicIDs));
        PlayerPrefs.SetString(KEY_COLLECTIBLE_STATES,  SerializeKV(collectibleStates));
        PlayerPrefs.SetString(KEY_COLLECTIBLES_FOUND,  Serialize(collectiblesEverFound));
        PlayerPrefs.Save();

#if UNITY_EDITOR
        Debug.Log($"[JournalSaveSystem] Saved — " +
                  $"{unlockedNoteIDs.Count} notes, " +
                  $"{unlockedRelicIDs.Count} relics, " +
                  $"{collectibleStates.Count} collectible states.");
#endif
    }

    // ── Load ──────────────────────────────────────────────────────────────────

    public static JournalSaveData LoadJournalState()
    {
        if (!PlayerPrefs.HasKey(KEY_UNLOCKED_NOTES))
        {
            Debug.Log("[JournalSaveSystem] No save data found. Starting fresh.");
            return null;
        }

        var data = new JournalSaveData
        {
            unlockedNoteIDs      = Deserialize(PlayerPrefs.GetString(KEY_UNLOCKED_NOTES,    "")),
            readNoteIDs          = Deserialize(PlayerPrefs.GetString(KEY_READ_NOTES,         "")),
            unlockedRelicIDs     = Deserialize(PlayerPrefs.GetString(KEY_UNLOCKED_RELICS,    "")),
            examinedRelicIDs     = Deserialize(PlayerPrefs.GetString(KEY_EXAMINED_RELICS,    "")),
            upgradedRelicIDs     = Deserialize(PlayerPrefs.GetString(KEY_UPGRADED_RELICS,    "")),
            collectibleStates    = DeserializeKV(PlayerPrefs.GetString(KEY_COLLECTIBLE_STATES, "")),
            collectiblesEverFound = Deserialize(PlayerPrefs.GetString(KEY_COLLECTIBLES_FOUND, ""))
        };

#if UNITY_EDITOR
        Debug.Log($"[JournalSaveSystem] Loaded — " +
                  $"{data.unlockedNoteIDs.Count} notes, " +
                  $"{data.unlockedRelicIDs.Count} relics, " +
                  $"{data.collectibleStates.Count} collectible states.");
#endif
        return data;
    }

    // ── Run reset — call at the start of every new run ────────────────────────

    /// <summary>
    /// Resets per-run state (isPickedUpThisRun) without touching
    /// persistent state (collectible visual states, relic upgrades, notes).
    /// Call this at the start of every run.
    /// </summary>
    public static void ResetRunState()
    {
        // Per-run pickup state is not persisted to PlayerPrefs —
        // it lives only in JournalManager's runtime HashSet.
        // JournalManager.ResetRunState() handles clearing it.
        Debug.Log("[JournalSaveSystem] Run state reset. Persistent states preserved.");
    }

    // ── Full delete — only call on new game ───────────────────────────────────

    public static void DeleteSaveData()
    {
        PlayerPrefs.DeleteKey(KEY_UNLOCKED_NOTES);
        PlayerPrefs.DeleteKey(KEY_READ_NOTES);
        PlayerPrefs.DeleteKey(KEY_UNLOCKED_RELICS);
        PlayerPrefs.DeleteKey(KEY_EXAMINED_RELICS);
        PlayerPrefs.DeleteKey(KEY_UPGRADED_RELICS);
        PlayerPrefs.DeleteKey(KEY_COLLECTIBLE_STATES);
        PlayerPrefs.DeleteKey(KEY_COLLECTIBLES_FOUND);
        PlayerPrefs.Save();
        Debug.Log("[JournalSaveSystem] All save data deleted.");
    }

    public static bool HasSaveData() => PlayerPrefs.HasKey(KEY_UNLOCKED_NOTES);

    // ── Serialization helpers ─────────────────────────────────────────────────

    private static string Serialize(IEnumerable<string> ids)
    {
        if (ids == null) return "";
        return string.Join(SEPARATOR.ToString(), ids);
    }

    private static List<string> Deserialize(string raw)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(raw)) return result;
        foreach (var part in raw.Split(SEPARATOR))
        {
            string t = part.Trim();
            if (!string.IsNullOrEmpty(t)) result.Add(t);
        }
        return result;
    }

    // Serializes Dictionary<string,int> as "id:value;id:value"
    private static string SerializeKV(Dictionary<string, int> dict)
    {
        if (dict == null || dict.Count == 0) return "";
        var parts = new List<string>();
        foreach (var kvp in dict)
            parts.Add($"{kvp.Key}{KV_SEPARATOR}{kvp.Value}");
        return string.Join(PAIR_SEPARATOR.ToString(), parts);
    }

    private static Dictionary<string, int> DeserializeKV(string raw)
    {
        var result = new Dictionary<string, int>();
        if (string.IsNullOrEmpty(raw)) return result;
        foreach (var pair in raw.Split(PAIR_SEPARATOR))
        {
            var kv = pair.Split(KV_SEPARATOR);
            if (kv.Length == 2 && int.TryParse(kv[1], out int val))
                result[kv[0].Trim()] = val;
        }
        return result;
    }
}