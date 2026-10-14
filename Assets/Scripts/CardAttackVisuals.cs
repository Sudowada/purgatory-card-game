using UnityEngine;

/// <summary>
/// Optional visual layer for telegraphed attacks. Add to a Card3DInteractable.
/// Assign pixel-art prefabs in the Inspector; no combat rules live here.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Card3DInteractable))]
public sealed class CardAttackVisuals : MonoBehaviour
{
    [Header("Card Anchors")]
    [SerializeField] private Transform cardVisualRoot;
    [Tooltip("Optional anchor above the card. Parent it to the moving visual root.")]
    [SerializeField] private Transform attackAnchor;
    [Tooltip("Optional anchor centered on the face. Parent it to the moving visual root.")]
    [SerializeField] private Transform shieldAnchor;

    [Header("Art Slots (Prefabs)")]
    [SerializeField] private GameObject attackIndicatorPrefab;
    [SerializeField] private GameObject shieldImpactPrefab;

    [Header("Offsets (local to moving card visual)")]
    [SerializeField] private Vector3 attackOffset = new Vector3(0f, 0.015f, 0.07f);
    [SerializeField] private Vector3 shieldOffset = new Vector3(0f, 0.003f, 0f);

    [Header("Attack Float")]
    [SerializeField, Min(0f)] private float bobHeight = 0.003f;
    [SerializeField, Min(0f)] private float bobSpeed = 2.5f;
    [SerializeField, Min(0.01f)] private float shieldDuration = 0.35f;

    private GameObject attackInstance;
    private GameObject shieldInstance;
    private Vector3 attackRest;
    private float shieldEndTime;

    private void Awake()
    {
        if (cardVisualRoot == null)
        {
            var card = GetComponent<Card3DInteractable>();
            cardVisualRoot = card != null ? card.VisualRoot : null;
        }
    }

    private Transform Root => cardVisualRoot != null ? cardVisualRoot : transform;

    public void SetTargeted(bool targeted)
    {
        if (!targeted)
        {
            if (attackInstance != null) Destroy(attackInstance);
            attackInstance = null;
            return;
        }

        if (attackInstance != null || attackIndicatorPrefab == null) return;
        Transform parent = attackAnchor != null ? attackAnchor : Root;
        attackInstance = Instantiate(attackIndicatorPrefab, parent, false);
        attackRest = attackAnchor != null ? Vector3.zero : attackOffset;
        attackInstance.transform.localPosition = attackRest;
        attackInstance.transform.localRotation = Quaternion.identity;
    }

    /// <summary>Call when an attacked card is successfully offered, before its burn animation.</summary>
    public void PlayShieldBlock()
    {
        SetTargeted(false);
        if (shieldImpactPrefab == null) return;
        if (shieldInstance != null) Destroy(shieldInstance);
        Transform parent = shieldAnchor != null ? shieldAnchor : Root;
        shieldInstance = Instantiate(shieldImpactPrefab, parent, false);
        shieldInstance.transform.localPosition = shieldAnchor != null ? Vector3.zero : shieldOffset;
        shieldInstance.transform.localRotation = Quaternion.identity;
        shieldEndTime = Time.time + shieldDuration;
    }

    /// <summary>Undo should immediately end the block effect without locking card input.</summary>
    public void CancelShieldBlock()
    {
        if (shieldInstance != null) Destroy(shieldInstance);
        shieldInstance = null;
    }

    private void LateUpdate()
    {
        if (attackInstance != null)
            attackInstance.transform.localPosition = attackRest +
                Vector3.up * (Mathf.Sin(Time.time * bobSpeed * Mathf.PI * 2f) * bobHeight);

        if (shieldInstance != null && Time.time >= shieldEndTime)
        {
            Destroy(shieldInstance);
            shieldInstance = null;
        }
    }

    private void OnDisable()
    {
        if (attackInstance != null) Destroy(attackInstance);
        if (shieldInstance != null) Destroy(shieldInstance);
        attackInstance = null;
        shieldInstance = null;
    }
}
