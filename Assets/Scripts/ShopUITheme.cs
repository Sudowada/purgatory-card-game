using UnityEngine;

/// <summary>
/// Shared, allocation-free IMGUI color primitives for the 3D shop's screen overlays.
/// Matches ScoreHUD's warm brass accents and charcoal panels without shaders,
/// textures, or scene/prefab modifications.
/// </summary>
public static class ShopUITheme
{
    public static readonly Color Background = new Color(0.055f, 0.060f, 0.075f, 0.94f);
    public static readonly Color Tile = new Color(0.14f, 0.15f, 0.16f, 0.96f);
    public static readonly Color Outline = new Color(0.34f, 0.29f, 0.21f, 0.94f);
    public static readonly Color Brass = new Color(0.85f, 0.66f, 0.32f);
    public static readonly Color Gold = new Color(0.95f, 0.77f, 0.36f);
    public static readonly Color Ivory = new Color(0.92f, 0.88f, 0.78f);
    public static readonly Color Muted = new Color(0.73f, 0.71f, 0.66f);
    public static readonly Color Disabled = new Color(0.35f, 0.35f, 0.34f);

    public static void Fill(Rect rect, Color color)
    {
        if (Event.current == null || Event.current.type != EventType.Repaint ||
            rect.width <= 0f || rect.height <= 0f) return;
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    public static void Frame(Rect rect)
    {
        Fill(rect, Outline);
        Fill(new Rect(rect.x + 1f, rect.y + 1f,
            Mathf.Max(0f, rect.width - 2f), Mathf.Max(0f, rect.height - 2f)), Background);
        Fill(new Rect(rect.x + 2f, rect.y + 2f,
            Mathf.Max(0f, rect.width - 4f), 2f), Brass);
    }

    public static void Cell(Rect rect)
    {
        Fill(rect, Outline);
        Fill(new Rect(rect.x + 1f, rect.y + 1f,
            Mathf.Max(0f, rect.width - 2f), Mathf.Max(0f, rect.height - 2f)), Tile);
    }

    public static void ButtonBackground(Rect rect, bool enabled, bool hovered, bool primary)
    {
        Fill(rect, enabled ? Brass : Disabled);
        Color body = !enabled ? new Color(0.16f, 0.16f, 0.17f) :
            primary ? (hovered ? new Color(0.40f, 0.32f, 0.17f) :
                                  new Color(0.31f, 0.26f, 0.17f)) :
                      (hovered ? new Color(0.25f, 0.25f, 0.24f) : Tile);
        Fill(new Rect(rect.x + 1f, rect.y + 1f,
            Mathf.Max(0f, rect.width - 2f), Mathf.Max(0f, rect.height - 2f)), body);
    }
}
