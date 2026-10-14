using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Attach to the reroll button mesh. Remove OfferButtonInteractable from this button.</summary>
public class ShopRerollButton : MonoBehaviour
{
    public ShopWindowInteractable shopWindow;
    public Collider clickCollider;
    public Transform movingButton;
    public Vector3 localPressOffset = new Vector3(0f, -0.005f, 0f);
    private bool pressing;
    private Vector3 restPosition;
    void Start()
    {
        if (shopWindow == null) shopWindow = FindFirstObjectByType<ShopWindowInteractable>();
        if (clickCollider == null) clickCollider = GetComponent<Collider>();
        if (movingButton != null) restPosition = movingButton.localPosition;
    }
    void Update()
    {
        if (OwnedRelicHotbar.BlocksWorldInput || pressing || shopWindow == null || !shopWindow.CanInteract || Mouse.current == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
        Camera camera = shopWindow.playerCamera != null ? shopWindow.playerCamera : Camera.main;
        if (camera == null) return;
        if (!Physics.Raycast(camera.ScreenPointToRay(Mouse.current.position.ReadValue()), out var hit, 100f)) return;
        bool thisButton = clickCollider != null ? hit.collider == clickCollider
            : hit.transform == transform || hit.transform.IsChildOf(transform);
        if (!thisButton) return;
        shopWindow.RerollDisplayedRelics();
        if (movingButton != null) StartCoroutine(Press());
    }
    IEnumerator Press()
    {
        pressing = true;
        movingButton.localPosition = restPosition + localPressOffset;
        yield return new WaitForSecondsRealtime(0.1f);
        if (movingButton != null) movingButton.localPosition = restPosition;
        pressing = false;
    }
    void OnDisable()
    {
        StopAllCoroutines();
        if (pressing && movingButton != null) movingButton.localPosition = restPosition;
        pressing = false;
    }
}
