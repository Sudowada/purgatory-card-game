using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 4f;

    [Header("Look")]
    public float mouseSensitivity = 2f;
    public Transform cameraTransform;

    private CharacterController _cc;
    private float _xRotation = 0f;

    // Allow environmental interactions (such as the toilet) to keep mouse
    // look active while preventing walking. Other gameplay is unchanged.
    public bool MovementLocked { get; set; }

    // Temporary camera pitch added by use animations without overriding the
    // player's look angle. This avoids snapping the view when the animation ends.
    public float LookPitchOffset { get; set; }

    void Awake() => _cc = GetComponent<CharacterController>();

    void Update()
    {
        if (!enabled) return;

        var mouse = Mouse.current;
        var kb    = Keyboard.current;
        if (mouse == null || kb == null) return;

        // Mouse look
        Vector2 mouseDelta = mouse.delta.ReadValue() * mouseSensitivity * 0.1f;
        _xRotation -= mouseDelta.y;
        _xRotation  = Mathf.Clamp(_xRotation, -80f, 80f);
        cameraTransform.localRotation = Quaternion.Euler(
            Mathf.Clamp(_xRotation + LookPitchOffset, -80f, 80f), 0f, 0f);
        transform.Rotate(Vector3.up * mouseDelta.x);

        // Keep the existing mouse-look controls while a use animation locks
        // translation. SimpleMove(zero) still applies CharacterController gravity.
        if (MovementLocked)
        {
            _cc.SimpleMove(Vector3.zero);
            return;
        }

        // Movement
        float h = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
        float v = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
        Vector3 move = transform.right * h + transform.forward * v;
        _cc.SimpleMove(move * moveSpeed);
    }
}