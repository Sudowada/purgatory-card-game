using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Owns the logical order and world-space layout of the player's 3D hand.
///
/// Card3Droot moves to these slots.
/// The Card child handles idle / hover animation separately.
/// </summary>
public class HandHolder3D : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private Transform handAnchor;
    [SerializeField] private float cardSpacing = 0.085f;
    [SerializeField] private float arcHeight = 0.025f;
    [SerializeField] private float depthStep = 0.0015f;

    [Tooltip("Maximum visual fan rotation at the far left/right cards.")]
    [SerializeField] private float maxFanAngle = 10f;

    [Tooltip("Local axis used for fan rotation. Usually Z for a face-on hand.")]
    [SerializeField] private Vector3 fanRotationAxis = Vector3.forward;

    [Tooltip("Extra local rotation needed by your Card3Droot prefab. Your screenshot currently has Y = 90.")]
    [SerializeField] private Vector3 cardRotationOffset = new Vector3(0f, 90f, 0f);

    [Header("Movement")]
    [SerializeField] private float layoutTweenDuration = 0.12f;
    [SerializeField] private Ease layoutEase = Ease.OutCubic;

    [Header("Dragging")]
    [Tooltip("Local normal of the invisible plane the dragged card follows.")]
    [SerializeField] private Vector3 dragPlaneNormalLocal = Vector3.up;
    [SerializeField] private float dragPlaneOffset = 0f;
    [SerializeField] private float dragLift = 0.035f;

    private readonly List<Card3DInteractable> cards = new List<Card3DInteractable>();
    private Card3DInteractable draggedCard;

    public IReadOnlyList<Card3DInteractable> Cards => cards;
    public float LayoutTweenDuration => layoutTweenDuration;

    private void Awake()
    {
        if (handAnchor == null)
            handAnchor = transform;
    }

    public void RegisterCard(Card3DInteractable card)
    {
        if (card == null || cards.Contains(card))
            return;

        cards.Add(card);
        card.SetHandHolder(this);
        LayoutHand();
    }

    public void UnregisterCard(Card3DInteractable card)
    {
        if (card == null)
            return;

        cards.Remove(card);

        if (draggedCard == card)
            draggedCard = null;

        LayoutHand();
    }

    public void BeginDrag(Card3DInteractable card)
    {
        if (card == null || !cards.Contains(card))
            return;

        draggedCard = card;
    }

    public void UpdateDraggedCard(Card3DInteractable card)
    {
        if (card == null || card != draggedCard || cards.Count <= 1)
            return;

        int oldIndex = cards.IndexOf(card);
        int newIndex = GetClosestIndexForWorldPosition(card.transform.position);

        if (oldIndex == newIndex)
            return;

        cards.RemoveAt(oldIndex);
        newIndex = Mathf.Clamp(newIndex, 0, cards.Count);
        cards.Insert(newIndex, card);

        LayoutHand();
    }

    public void EndDrag(Card3DInteractable card)
    {
        if (card == null)
            return;

        if (draggedCard == card)
            draggedCard = null;

        LayoutHand();
    }

    /// <summary>
    /// Removes a card for the Offering and returns the index it occupied.
    /// </summary>
    public int RemoveForOffering(Card3DInteractable card)
    {
        if (card == null)
            return -1;

        int index = cards.IndexOf(card);

        if (index < 0)
            return -1;

        cards.RemoveAt(index);

        if (draggedCard == card)
            draggedCard = null;

        LayoutHand();
        return index;
    }

    public void ReturnFromOffering(Card3DInteractable card, int preferredIndex)
    {
        if (card == null)
            return;

        if (cards.Contains(card))
            cards.Remove(card);

        int index = Mathf.Clamp(preferredIndex, 0, cards.Count);
        cards.Insert(index, card);

        card.SetHandHolder(this);
        card.ReturnToHandState();

        LayoutHand();
    }

    public void LayoutHand()
    {
        int count = cards.Count;

        for (int i = 0; i < count; i++)
        {
            Card3DInteractable card = cards[i];

            if (card == null || card == draggedCard || card.IsOffered)
                continue;

            GetPoseForIndex(i, count, out Vector3 position, out Quaternion rotation);
            card.TweenRootTo(position, rotation, layoutTweenDuration, layoutEase);
        }
    }

    public bool TryGetDragWorldPoint(Camera cameraToUse, Vector2 screenPosition, out Vector3 worldPoint)
    {
        worldPoint = Vector3.zero;

        if (cameraToUse == null || handAnchor == null)
            return false;

        Vector3 normal = handAnchor.TransformDirection(dragPlaneNormalLocal.normalized);
        Vector3 planePoint = handAnchor.position + normal * dragPlaneOffset;

        Plane plane = new Plane(normal, planePoint);
        Ray ray = cameraToUse.ScreenPointToRay(screenPosition);

        if (!plane.Raycast(ray, out float enter))
            return false;

        worldPoint = ray.GetPoint(enter) + normal * dragLift;
        return true;
    }

    private int GetClosestIndexForWorldPosition(Vector3 worldPosition)
    {
        int count = cards.Count;

        if (count <= 1)
            return 0;

        Vector3 local = handAnchor.InverseTransformPoint(worldPosition);

        float totalWidth = cardSpacing * (count - 1);
        float firstX = -totalWidth * 0.5f;

        float rawIndex = (local.x - firstX) / Mathf.Max(cardSpacing, 0.0001f);
        return Mathf.Clamp(Mathf.RoundToInt(rawIndex), 0, count - 1);
    }

    private void GetPoseForIndex(
        int index,
        int count,
        out Vector3 worldPosition,
        out Quaternion worldRotation)
    {
        if (count <= 0)
        {
            worldPosition = handAnchor.position;
            worldRotation = handAnchor.rotation;
            return;
        }

        float centered = count == 1
            ? 0f
            : (index / (float)(count - 1)) * 2f - 1f;

        float x = (index - (count - 1) * 0.5f) * cardSpacing;

        // Center cards sit slightly higher; outer cards form a shallow arc.
        float y = arcHeight * (1f - centered * centered);

        // Tiny depth ordering reduces cards sitting perfectly coplanar.
        float z = index * depthStep;

        Vector3 localPosition = new Vector3(x, y, z);
        worldPosition = handAnchor.TransformPoint(localPosition);

        float fanAngle = -centered * maxFanAngle;

        Quaternion baseRotation =
            handAnchor.rotation * Quaternion.Euler(cardRotationOffset);

        Quaternion fanRotation =
            Quaternion.AngleAxis(fanAngle, fanRotationAxis.normalized);

        worldRotation = baseRotation * fanRotation;
    }
}
