using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Creates a World Space canvas positioned on the 3D desk surface.
/// Cards in the hand are repositioned onto this canvas so they appear
/// to physically sit on the table like Inscryption.
/// 
/// HOW TO USE:
/// 1. Add to any GameObject in the scene (e.g. GameManager)
/// 2. Assign the deskSurface transform (the desk mesh or an empty at table height)
/// 3. Assign your HorizontalCardHolder
/// 4. Hit Play — cards will be repositioned onto the desk
/// </summary>
public class CardTableLayout : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Empty GameObject placed at the desk surface center")]
    public Transform deskSurface;

    [Tooltip("Your HorizontalCardHolder that holds the hand")]
    public HorizontalCardHolder cardHolder;

    [Tooltip("The main camera")]
    public Camera mainCamera;

    [Header("Table Canvas Settings")]
    [Tooltip("Width of the card area on the table in world units")]
    public float tableWidth  = 8f;
    [Tooltip("Height of the card area in world units")]
    public float tableHeight = 3f;
    [Tooltip("Tilt angle — how much the cards face the camera (0=flat on table, 90=facing camera)")]
    [Range(0f, 90f)]
    public float tiltAngle = 25f;

    [Header("Card Settings")]
    public float cardScale    = 0.006f; // scale from UI pixels to world units
    public float cardSpacing  = 1.1f;   // spacing multiplier between cards
    public float hoverHeight  = 0.15f;  // how high cards rise when selected

    [Header("Fan Settings")]
    public bool  useFan       = true;
    public float fanAngle     = 18f;    // total fan spread in degrees
    public float fanRaise     = 0.3f;   // how much center cards raise

    // ── Runtime ──────────────────────────────────────────────────────────────
    private Canvas      _tableCanvas;
    private RectTransform _tableRT;
    private bool        _initialized;

    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (deskSurface == null)
        {
            // Auto-create a desk anchor at a reasonable table height
            var anchor = new GameObject("DeskAnchor");
            anchor.transform.position = new Vector3(0f, 1.2f, 0.5f);
            deskSurface = anchor.transform;
            Debug.Log("[CardTableLayout] No desk surface assigned — created DeskAnchor at Y=1.2. " +
                "Move it to match your desk height.");
        }

        BuildTableCanvas();
        MoveCardHolderToTable();
    }

    void BuildTableCanvas()
    {
        // Create a World Space canvas at the desk position
        var go      = new GameObject("TableCardCanvas");
        _tableCanvas = go.AddComponent<Canvas>();
        _tableCanvas.renderMode   = RenderMode.WorldSpace;
        _tableCanvas.sortingOrder = 10;

        // Size and position
        _tableRT              = go.GetComponent<RectTransform>();
        _tableRT.sizeDelta    = new Vector2(tableWidth / cardScale, tableHeight / cardScale);
        _tableRT.localScale   = Vector3.one * cardScale;

        // Position on desk, tilted toward camera
        go.transform.position = deskSurface.position;
        go.transform.rotation = Quaternion.Euler(90f - tiltAngle, 0f, 0f);

        // Add canvas scaler
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        // Add raycaster so cards can still be clicked
        go.AddComponent<GraphicRaycaster>();

        _initialized = true;
        Debug.Log("[CardTableLayout] Table canvas created at " + deskSurface.position);
    }

    void MoveCardHolderToTable()
    {
        if (cardHolder == null)
        {
            // Try to find it
            cardHolder = FindFirstObjectByType<HorizontalCardHolder>();
            if (cardHolder == null)
            {
                Debug.LogWarning("[CardTableLayout] No HorizontalCardHolder found.");
                return;
            }
        }

        // Reparent the card holder to the table canvas
        var holderRT = cardHolder.GetComponent<RectTransform>();
        if (holderRT == null) return;

        cardHolder.transform.SetParent(_tableRT, false);

        // Center it on the table canvas
        holderRT.anchorMin        = new Vector2(0.5f, 0.3f);
        holderRT.anchorMax        = new Vector2(0.5f, 0.3f);
        holderRT.pivot            = new Vector2(0.5f, 0.5f);
        holderRT.anchoredPosition = Vector2.zero;
        holderRT.sizeDelta        = new Vector2(tableWidth / cardScale * 0.9f, 300f);

        Debug.Log("[CardTableLayout] Card holder moved to table canvas.");
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Call to reposition table canvas (e.g. after scene load)</summary>
    public void RefreshPosition()
    {
        if (_tableCanvas == null || deskSurface == null) return;
        _tableCanvas.transform.position = deskSurface.position;
        _tableCanvas.transform.rotation = Quaternion.Euler(90f - tiltAngle, 0f, 0f);
    }

    /// <summary>Smoothly tilt table canvas toward/away from camera</summary>
    public void SetTilt(float angle, float duration = 0.3f)
    {
        tiltAngle = angle;
        _tableCanvas.transform.DORotate(
            new Vector3(90f - angle, 0f, 0f), duration)
            .SetEase(Ease.OutCubic);
    }

    void OnDrawGizmosSelected()
    {
        if (deskSurface == null) return;

        // Draw the table card area in the editor
        Gizmos.color = new Color(0.8f, 0.6f, 0.2f, 0.4f);
        var center = deskSurface.position;
        var rot    = Quaternion.Euler(90f - tiltAngle, 0f, 0f);
        var right  = rot * Vector3.right   * tableWidth  * 0.5f;
        var up     = rot * Vector3.up      * tableHeight * 0.5f;

        Vector3 tl = center - right + up;
        Vector3 tr = center + right + up;
        Vector3 bl = center - right - up;
        Vector3 br = center + right - up;

        Gizmos.DrawLine(tl, tr);
        Gizmos.DrawLine(tr, br);
        Gizmos.DrawLine(br, bl);
        Gizmos.DrawLine(bl, tl);

        Gizmos.color = new Color(0.8f, 0.6f, 0.2f, 0.15f);
        Gizmos.DrawMesh(
            Resources.GetBuiltinResource<Mesh>("Quad.fbx"),
            center, rot, new Vector3(tableWidth, tableHeight, 1f));
    }
}
