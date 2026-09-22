using DG.Tweening;
using UnityEngine;

/// <summary>
/// Attach this later to the physical 3D OFFER / SACRIFICE button you model in Blender.
///
/// Mouse click and Space both call the same Offering controller.
/// Space can still animate this physical button through PlayPressAnimation().
/// </summary>
public class OfferingConfirmButton3D : MonoBehaviour
{
    [SerializeField] private CardOfferingController offeringController;
    [SerializeField] private Transform movingButtonPart;

    [Header("Press Animation")]
    [SerializeField] private Vector3 localPressOffset = new Vector3(0f, -0.015f, 0f);
    [SerializeField] private float pressDownDuration = 0.06f;
    [SerializeField] private float pressUpDuration = 0.10f;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip pressSound;

    private Vector3 baseLocalPosition;
    private Sequence pressSequence;

    private void Awake()
    {
        if (movingButtonPart == null)
            movingButtonPart = transform;

        baseLocalPosition = movingButtonPart.localPosition;

        if (offeringController == null)
            offeringController = FindFirstObjectByType<CardOfferingController>();
    }

    private void OnMouseDown()
    {
        if (offeringController == null ||
            offeringController.InputLocked ||
            !offeringController.IsFull)
            return;

        PlayPressAnimation();
        offeringController.ConfirmOffering();
    }

    public void PlayPressAnimation()
    {
        if (movingButtonPart == null)
            return;

        if (pressSequence != null && pressSequence.IsActive())
            pressSequence.Kill(false);

        if (audioSource != null && pressSound != null)
            audioSource.PlayOneShot(pressSound);

        movingButtonPart.DOKill(false);

        pressSequence = DOTween.Sequence();
        pressSequence.Append(
            movingButtonPart
                .DOLocalMove(baseLocalPosition + localPressOffset, pressDownDuration)
                .SetEase(Ease.OutQuad)
        );
        pressSequence.Append(
            movingButtonPart
                .DOLocalMove(baseLocalPosition, pressUpDuration)
                .SetEase(Ease.OutBack)
        );
    }
}
