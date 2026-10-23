using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Small labels attached to controls; no instruction panel.</summary>
public static class RadioControlReadout
{
    static GUIStyle label, value, status;
    public static bool IsHovered(Camera camera, Component control)
    {
        if (Mouse.current == null || !Physics.Raycast(camera.ScreenPointToRay(Mouse.current.position.ReadValue()), out var hit, 10f)) return false;
        if (control is RadioKnob) return hit.collider.GetComponentInParent<RadioKnob>() == control;
        return hit.collider.GetComponentInParent<RadioBandSwitch>() == control;
    }
    public static void Draw(Vector3 screen, string name, string number, bool focused, float offset, string message = "")
    {
        if (screen.z <= 0f) return;
        if (label == null)
        {
            label = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            value = new GUIStyle(label) { fontSize = 22 };
            status = new GUIStyle(label) { fontSize = 14 };
        }
        float y = Screen.height - screen.y;
        Color previous = GUI.color;
        GUI.color = focused ? new Color(1f, 0.88f, 0.58f) : Color.white;
        GUI.Label(new Rect(screen.x - 70f, y + 38f, 140f, 28f), name, label);
        if (focused) GUI.Label(new Rect(screen.x - 80f, y - offset - 32f, 160f, 32f), number, value);
        GUI.color = previous;
        if (!string.IsNullOrEmpty(message)) GUI.Label(new Rect(screen.x - 160f, y + 65f, 320f, 28f), message, status);
    }
}
