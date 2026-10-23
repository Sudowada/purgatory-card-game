using UnityEngine;
using UnityEngine.InputSystem;

public class RadioConfirmButton : MonoBehaviour
{
    public RadioTuner radio;
    void Start() { if (radio == null) radio = GetComponentInParent<RadioTuner>(); }
    void Update()
    {
        if (radio == null || !radio.CanInteract || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
        var camera = radio.GetComponent<RadioInteractable>().playerCamera;
        if (camera != null && Physics.Raycast(camera.ScreenPointToRay(Mouse.current.position.ReadValue()), out var hit, 10f)
            && hit.collider.GetComponentInParent<RadioConfirmButton>() == this)
            radio.ConfirmStation();
    }
}
