using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// Temporary diagnostic tool — press F12 to log all canvases.
/// Uses the New Input System.
/// </summary>
public class OverlayDebugger : MonoBehaviour
{
    private Keyboard _kb;

    void OnEnable()  => _kb = Keyboard.current;
    void OnDisable() => _kb = null;

    void Update()
    {
        if (_kb != null && _kb.f12Key.wasPressedThisFrame)
            RunDiagnostic();
    }

    void RunDiagnostic()
    {
        Debug.Log("========== OVERLAY DEBUGGER ==========");

        var allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        Debug.Log($"Total Canvases: {allCanvases.Length}");

        foreach (var c in allCanvases)
        {
            string active    = c.gameObject.activeInHierarchy ? "ACTIVE" : "inactive";
            var    raycaster = c.GetComponent<GraphicRaycaster>();
            string raycast   = raycaster != null
                ? (raycaster.enabled ? "RAYCASTER ON ⚠" : "raycaster off")
                : "no raycaster";

            Debug.Log($"  [{c.sortingOrder}] '{c.gameObject.name}' | {active} | {raycast}");

            foreach (Transform child in c.transform)
            {
                var img = child.GetComponent<Image>();
                string imgInfo = img != null
                    ? $"Image alpha={img.color.a:F2} raycastTarget={img.raycastTarget}"
                    : "no Image";
                Debug.Log($"    Child: '{child.name}' active={child.gameObject.activeSelf} | {imgInfo}");
            }
        }

        Debug.Log("--- Active Raycasters (blocking clicks) ---");
        var allRaycasters = FindObjectsByType<GraphicRaycaster>(FindObjectsSortMode.None);
        foreach (var r in allRaycasters)
            if (r.enabled && r.gameObject.activeInHierarchy)
                Debug.Log($"  BLOCKING: '{r.gameObject.name}' sortOrder={r.GetComponent<Canvas>()?.sortingOrder}");

        Debug.Log("========== END ==========");
    }
}