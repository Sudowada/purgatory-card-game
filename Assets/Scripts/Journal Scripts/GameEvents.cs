using System;
using System.Collections.Generic;

/// <summary>
/// Static event bus. Decouples all game systems from each other.
/// Nothing needs a reference to anything else — just fire an event ID
/// and anything subscribed to it will respond.
///
/// Usage:
///   Subscribe:   GameEvents.Subscribe("found_tooth_box", OnToothBoxFound);
///   Unsubscribe: GameEvents.Unsubscribe("found_tooth_box", OnToothBoxFound);
///   Fire:        GameEvents.Fire("found_tooth_box");
/// </summary>
public static class GameEvents
{
    // ── Internal listener registry ────────────────────────────────────────────

    private static readonly Dictionary<string, Action> listeners
        = new Dictionary<string, Action>();

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Subscribe a callback to an event ID.
    /// Safe to call multiple times — duplicate subscriptions are prevented.
    /// </summary>
    public static void Subscribe(string eventID, Action callback)
    {
        if (string.IsNullOrEmpty(eventID))
        {
            UnityEngine.Debug.LogWarning("[GameEvents] Subscribe called with null or empty eventID.");
            return;
        }

        if (!listeners.ContainsKey(eventID))
            listeners[eventID] = null;

        listeners[eventID] += callback;
    }

    /// <summary>
    /// Unsubscribe a callback from an event ID.
    /// Safe to call even if the event ID does not exist.
    /// Always call this in OnDestroy to avoid ghost listeners.
    /// </summary>
    public static void Unsubscribe(string eventID, Action callback)
    {
        if (string.IsNullOrEmpty(eventID)) return;

        if (listeners.ContainsKey(eventID))
            listeners[eventID] -= callback;
    }

    /// <summary>
    /// Fire an event by ID. All subscribers are notified immediately.
    /// Safe to fire an event ID that has no subscribers — nothing happens.
    /// </summary>
    public static void Fire(string eventID)
    {
        if (string.IsNullOrEmpty(eventID))
        {
            UnityEngine.Debug.LogWarning("[GameEvents] Fire called with null or empty eventID.");
            return;
        }

        if (listeners.TryGetValue(eventID, out Action action))
            action?.Invoke();

#if UNITY_EDITOR
        UnityEngine.Debug.Log($"[GameEvents] Fired: '{eventID}'");
#endif
    }

    /// <summary>
    /// Clears all listeners. Call this on scene unload or game reset
    /// to prevent stale callbacks from a previous session.
    /// </summary>
    public static void ClearAll()
    {
        listeners.Clear();
#if UNITY_EDITOR
        UnityEngine.Debug.Log("[GameEvents] All listeners cleared.");
#endif
    }

    /// <summary>
    /// Returns true if the given event ID has at least one subscriber.
    /// Useful for debugging orphaned event IDs.
    /// </summary>
    public static bool HasListeners(string eventID)
    {
        return listeners.ContainsKey(eventID) && listeners[eventID] != null;
    }
}
